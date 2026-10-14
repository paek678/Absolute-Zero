using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using Unity.Behavior;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    // Scene-scoped decision lifetime. This never assigns a phase or executes an
    // item effect. The graph instance is manually ticked after live-key checks.
    public sealed class BotTurnController : MonoBehaviour
    {
        const int CoolingSamples = 3;
        TurnManager _turn;
        PlayerState _bot, _opponent;
        ResolvedSoloSettings _settings;
        TrustedBotCommandAdapter _commands;
        BehaviorGraphAgent _agent;
        PrepInputKey _key;
        bool _started, _running, _noItemReady;
        ulong _request, _operation;
        double _thinkUntil, _readyAt;
        System.Random _random;
        BotObservation _observation;
        BotScoreSheet _sheet;
        BotCandidate _chosen;
        // Public observation: opponent temperature at Prep start minus at their Ready.
        readonly List<float> _opponentCooling = new();
        PrepInputKey _coolingKey;
        bool _coolingOpen, _coolingRecorded;
        readonly Queue<string> _trace = new();
        public IReadOnlyCollection<string> Trace => _trace;
        public int RunCount { get; private set; }
        public int SelectionCount { get; private set; }
        public int MiniGameFailureCount { get; private set; }
        public int ReadyCount { get; private set; }
        public bool IsRunning => _running;
        public BehaviorGraph RuntimeGraph => _agent != null ? _agent.Graph : null;
        internal int RetryBudget => _settings?.RetryBudget ?? 0;
        internal bool InCrisis => _sheet != null && _sheet.Crisis;
        internal bool MustTakeLethal => _settings != null && BotDecisionModel.MustTakeLethal(_sheet, _settings.Judgement);
        double Now => NetworkManager.Singleton != null ? NetworkManager.Singleton.ServerTime.Time : double.NaN;

        void Update()
        {
            var router = AppBootstrapper.Instance?.SessionRouter;
            var settings = router?.LaunchContext?.Solo;
            if (settings == null || settings.IsValidationFixture || router.IsStopping
                || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            { Detach(); return; }
            if (_turn != TurnManager.Instance || !ReferenceEquals(_settings, settings))
            {
                Detach(); _turn = TurnManager.Instance; _settings = settings;
                if (_turn != null) _turn.PrepInputOpened += OnPrep;
            }
            // Subscribe, then query: late binding receives only the current remaining
            // window, and duplicate events cannot restart an already-run key.
            if (_turn != null && _turn.TryGetPrepInputSnapshot(out var snapshot))
            {
                OnPrep(snapshot);
                ObserveOpponentCooling(snapshot);
            }
            else _coolingOpen = false;
            if (!_running) return;
            if (!CanAct()) { StopRun("input closed, Ready, death or stale session"); return; }
            _agent.Graph.Tick();
            if (!_agent.Graph.IsRunning) StopRun("graph completed");
        }
        internal void OnPrep(PrepInputSnapshot snapshot)
        {
            if (!isActiveAndEnabled || _settings == null || snapshot == null || (_started && _key == snapshot.Key)) return;
            var registry = MatchCompositionRoot.Instance?.Registry;
            if (registry == null) return;
            var bot = registry.Players.FirstOrDefault(x => x.IsValid && x.State.IsBotControlled)?.State;
            var opponent = registry.Players.FirstOrDefault(x => x.IsValid && x.State != bot && !x.State.IsBotControlled)?.State;
            if (bot == null || opponent == null || !bot.IsParticipantReady || bot.IsReady.Value || bot.Temperature.Value <= 0) return;
            var commands = bot.GetBotCommands();
            if (commands == null || !commands.TryWindow(snapshot.Key, out _)) return;
            ResetCoolingFor(snapshot.Key);
            StopRun("new Prep"); _bot = bot; _opponent = opponent; _commands = commands; _key = snapshot.Key;
            if (_agent == null)
            {
                _agent = gameObject.AddComponent<BehaviorGraphAgent>();
                _agent.enabled = false; _agent.NetcodeRunOnlyOnOwner = false;
                _agent.Graph = _settings.Graph; _agent.Init();
            }
            int seed = BotDecisionModel.Seed(_settings.BotId, _key);
            _random = new System.Random(seed); _started = true; _running = true; RunCount++;
            Write("start seed=" + seed + " difficulty=" + _settings.DifficultyId);
            _agent.Graph.Restart();
        }
        internal bool CanAct() => isActiveAndEnabled && _running && _commands != null
            && _bot != null && _opponent != null && _bot.IsParticipantReady
            && !_bot.IsReady.Value && _bot.Temperature.Value > 0 && _opponent.Temperature.Value > 0
            && _commands.TryWindow(_key, out _);
        internal bool CaptureObservation()
        {
            if (!CanAct() || !_commands.TryWindow(_key, out var snapshot)) return false;
            var held = ReadPublicOpponentItems();
            _observation = new BotObservation(_key, (byte)_bot.PlayerIndex, (byte)_opponent.PlayerIndex,
                _bot.Temperature.Value, _opponent.Temperature.Value, _bot.FanSpeed.Value, _opponent.FanSpeed.Value,
                _opponent.IsReady.Value, snapshot.RemainingSeconds, OpponentPrepLoss(snapshot),
                held, Array.Empty<BotCandidate>(),
                _bot.IsFanActive.Value, _opponent.IsFanActive.Value, _bot.GetInventory().FindAvailableCopySlot() >= 0);
            _sheet = null; _chosen = default; _noItemReady = false;
            Write("observe remaining=" + snapshot.RemainingSeconds.ToString("F3") + " opponentItems=" + held.Count);
            SetDebugVariables(); return true;
        }
        List<BotOpponentItem> ReadPublicOpponentItems()
        {
            // Held opponent items are shown on screen (Q15). Their selection and use counts are not read.
            var held = new List<BotOpponentItem>();
            var slots = _opponent.GetInventory().SlotStates;
            bool basicsBlocked = _opponent.IsBasicBlocked.Value;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty || slot.ItemId < 0 || slot.ItemId >= _settings.Catalog.Count) continue;
                var item = _settings.Catalog[slot.ItemId];
                held.Add(new BotOpponentItem(slot.ItemId, ItemAvailability.IsEnabled(item)
                    && !(basicsBlocked && item.SlotType == ItemSlotType.Main)));
            }
            return held;
        }
        internal bool BuildCandidates()
        {
            if (!CanAct() || _observation == null) return false;
            var candidates = new List<BotCandidate>();
            var slots = _bot.GetInventory().SlotStates;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsUsable || slot.CopyId == 0 || slot.ItemId < 0 || slot.ItemId >= _settings.Catalog.Count) continue;
                var item = _settings.Catalog[slot.ItemId];
                byte target = item.GetTargetMode() == TargetMode.Self ? _observation.Seat : _observation.Opponent;
                if (!_bot.ServerValidateBotItem(_key, slot.CopyId, target, out _).Succeeded
                    || !_settings.TryGetItemDelay(slot.ItemId, out var delay)) continue;
                candidates.Add(new BotCandidate(slot.CopyId, target, slot.ItemId, delay,
                    BotDecisionModel.CurrentHeal(ItemEffectRuleSnapshot.From(item, slot.ItemId), slot)));
            }
            _observation = _observation.WithCandidates(candidates);
            Write("legal candidates=" + candidates.Count); return candidates.Count > 0;
        }
        internal bool ScoreCandidates()
        {
            if (!CanAct() || _observation == null) return false;
            _sheet = BotDecisionModel.Score(_observation, _settings.ItemProfiles, _settings.Judgement,
                _settings.Coefficients, _settings.ReadyReserveSeconds);
            Write($"score crisis={_sheet.Crisis} care={_sheet.Care:0.##} loss={_sheet.OwnLoss:0.##} threat={_sheet.Threat:0.##} ranked="
                + string.Join(",", _sheet.Ranked.Take(5).Select(x => ItemName(x.Candidate.Item) + ":" + x.Final.ToString("0.##") + (x.Lethal ? "!" : ""))));
            SetDebugVariables(); return _sheet.Crisis || _sheet.Ranked.Count > 0;
        }
        internal bool PickCrisis()
        {
            if (!CanAct() || _sheet == null || !_sheet.Crisis) return false;
            return PickCareOrReady(false);
        }
        bool PickCareOrReady(bool redirected)
        {
            BotDecisionModel.ChooseCrisisOrReady(_sheet, _settings.Judgement, _random, out _chosen, out _noItemReady);
            if (!_noItemReady) return Picked("crisis", true);
            Write("crisis without eligible care: no-item Ready");
            return true; // Terminal choice: the selector must not fall through to an attack.
        }
        internal bool PickLethal()
            => CanAct() && _sheet != null && Picked("lethal", BotDecisionModel.TryPickLethal(_sheet, _settings.Judgement, out _chosen));
        internal bool PickWeighted()
            => CanAct() && _sheet != null && Picked("odds", BotDecisionModel.TryPickWeighted(_sheet, _settings.Judgement, _random, out _chosen));
        bool Picked(string branch, bool found)
        {
            if (!found) { _chosen = default; return false; }
            if (branch != "crisis" && BotDecisionModel.NeedsCrisisRedirect(_sheet, _chosen))
                return PickCareOrReady(true); // At most one redirect, no repeated weighted draws.
            bool opponentReady = _opponent.IsReady.Value;
            double thought = _settings.MinimumThinkSeconds + _random.NextDouble() * (_settings.MaximumThinkSeconds - _settings.MinimumThinkSeconds);
            thought = Math.Min(thought, Math.Max(0, _observation.Remaining - (opponentReady ? 0 : _chosen.Delay) - _settings.ReadyReserveSeconds - 0.1));
            if (_observation.Temperature <= 5 || opponentReady) thought = 0;
            _thinkUntil = Now + thought;
            Write(branch + " copy=" + _chosen.Copy + " item=" + ItemName(_chosen.Item) + " target=" + _chosen.Target + " think=" + thought.ToString("F3"));
            SetDebugVariables(); return true;
        }
        internal Node.Status Think()
        {
            if (!CanAct()) return Node.Status.Failure;
            if (_noItemReady) return Node.Status.Success;
            if (!ChoiceStillValid()) return Node.Status.Failure;
            if (_opponent.IsReady.Value) return Node.Status.Success;
            return Now >= _thinkUntil || _bot.Temperature.Value <= 5 ? Node.Status.Success : Node.Status.Running;
        }
        internal bool Submit()
        {
            if (!CanAct()) return false;
            if (_noItemReady) return true;
            if (_chosen.Copy == 0 || !ChoiceStillValid()) return false;
            var result = _commands.BeginUse(_key, ++_request, _chosen.Copy, _chosen.Target);
            _operation = result.OperationId; Write("submit " + result.Status + ":" + result.Reason
                + " due=" + result.DueTime.ToString("F3") + " request=" + _request + " operation=" + _operation
                + " copy=" + _chosen.Copy + " target=" + _chosen.Target);
            return result.Status == PlayerActionStatus.Pending;
        }
        internal Node.Status AwaitUse()
        {
            if (!CanAct()) return Node.Status.Failure;
            if (_noItemReady) return Node.Status.Success;
            var result = _commands.Poll(_key, _operation);
            if (result.Status == PlayerActionStatus.Pending) return Node.Status.Running;
            Write("operation " + result.Status + ":" + result.Reason + " operation=" + _operation + " copy=" + _chosen.Copy);
            // A failed mini-game consumed the item like a human failure; Ready follows without another item.
            if (result.Status == PlayerActionStatus.Cancelled && result.Reason == PlayerActionReason.MiniGameFailed) { MiniGameFailureCount++; return Node.Status.Success; }
            if (result.Status != PlayerActionStatus.Queued) return Node.Status.Failure;
            SelectionCount++; return Node.Status.Success;
        }
        bool ChoiceStillValid()
        {
            if (_chosen.Copy == 0 || !_commands.TryWindow(_key, out var window)
                || !_bot.ServerValidateBotItem(_key, _chosen.Copy, _chosen.Target, out var candidate).Succeeded) return false;
            var slot = _bot.GetInventory().SlotStates[candidate.SlotIndex];
            var currentCopy = new BotCandidate(_chosen.Copy, _chosen.Target, slot.ItemId, _chosen.Delay,
                BotDecisionModel.CurrentHeal(ItemEffectRuleSnapshot.From(candidate.ItemData, slot.ItemId), slot));
            var fresh = new BotObservation(_key, _observation.Seat, _observation.Opponent,
                _bot.Temperature.Value, _opponent.Temperature.Value, _bot.FanSpeed.Value, _opponent.FanSpeed.Value,
                _opponent.IsReady.Value, window.RemainingSeconds, _observation.OpponentPrepLoss,
                ReadPublicOpponentItems(), new[] { currentCopy },
                _bot.IsFanActive.Value, _opponent.IsFanActive.Value, _bot.GetInventory().FindAvailableCopySlot() >= 0);
            var current = BotDecisionModel.Score(fresh, _settings.ItemProfiles, _settings.Judgement,
                _settings.Coefficients, _settings.ReadyReserveSeconds);
            if (current.Ranked.Count == 0) return false;
            if (!BotDecisionModel.CanSubmitChoice(current)) { Write("replan changed public survival bound"); return false; }
            return true;
        }
        void ResetCoolingFor(PrepInputKey next)
        {
            if (!_started || _key.Generation != next.Generation || _key.Round != next.Round)
            { _opponentCooling.Clear(); _coolingOpen = false; _coolingRecorded = false; }
        }

        internal bool ChooseReadyTime()
        {
            if (!CanAct() || !_commands.TryWindow(_key, out var snapshot)) return false;
            // When the human readied first, the bot readies at once (spec).
            _readyAt = _settings.ReadyPolicy == BotReadyPolicy.NearDeadline && _bot.Temperature.Value > 10 && !_opponent.IsReady.Value
                ? Math.Max(Now, snapshot.Deadline - Math.Max(0.15, _settings.ReadyReserveSeconds)) : Now;
            Write("ready at=" + _readyAt.ToString("F3")); return true;
        }
        internal Node.Status WaitReady() => !CanAct() ? Node.Status.Failure
            : Now >= _readyAt || _bot.Temperature.Value <= 10 || _opponent.IsReady.Value ? Node.Status.Success : Node.Status.Running;
        internal bool SubmitReady()
        {
            if (!CanAct()) return false;
            var result = _commands.Ready(_key); Write("ready " + result.Status + ":" + result.Reason);
            if (result.Status != PlayerActionStatus.ReadyAccepted) return false;
            ReadyCount++; return true;
        }
        internal bool Fallback()
        {
            if (!CanAct() || _commands.HasPending) return false;
            Write("fallback without new item"); return SubmitReady();
        }
        void ObserveOpponentCooling(PrepInputSnapshot snapshot)
        {
            if (_opponent == null) return;
            if (!_coolingOpen || _coolingKey != snapshot.Key) { _coolingOpen = true; _coolingKey = snapshot.Key; _coolingRecorded = false; }
            if (_coolingRecorded || !_opponent.IsReady.Value) return;
            int seat = _opponent.PlayerIndex;
            if (snapshot.TemperaturesAtStart == null || seat < 0 || seat >= snapshot.TemperaturesAtStart.Count) return;
            _opponentCooling.Add(Mathf.Max(0, snapshot.TemperaturesAtStart[seat] - _opponent.Temperature.Value));
            if (_opponentCooling.Count > CoolingSamples) _opponentCooling.RemoveAt(0);
            _coolingRecorded = true;
        }
        float OpponentPrepLoss(PrepInputSnapshot snapshot)
        {
            if (_opponentCooling.Count > 0) return _opponentCooling.Average();
            // Prior until observed: an opponent who readies halfway through the window.
            return _opponent.IsFanActive.Value ? (float)((snapshot.Deadline - snapshot.StartTime) * 0.5) * Mathf.Max(0, _opponent.FanSpeed.Value) : 0;
        }
        string ItemName(short item) => _settings != null && item >= 0 && item < _settings.Catalog.Count
            ? _settings.Catalog[item].name : item.ToString();
        void SetDebugVariables()
        {
            _agent.SetVariableValue("Round", _key.Round); _agent.SetVariableValue("Turn", _key.Turn);
            _agent.SetVariableValue("Own temperature", _observation.Temperature);
            _agent.SetVariableValue("Opponent temperature", _observation.OpponentTemperature);
            _agent.SetVariableValue("Remaining seconds", (float)_observation.Remaining);
            _agent.SetVariableValue("In crisis", InCrisis);
            _agent.SetVariableValue("Lethal available", _sheet != null && _sheet.HasLethal);
            _agent.SetVariableValue("Selected item", _chosen.Copy == 0 ? "" : ItemName(_chosen.Item));
            _agent.SetVariableValue("Selected copy", _chosen.Copy.ToString());
        }
        void Write(string message)
        {
            string line = $"g={_key.Generation} r={_key.Round} t={_key.Turn} seq={_key.Sequence} time={Now:F3} {message}";
            _trace.Enqueue(line); while (_trace.Count > 256) _trace.Dequeue();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[SoloBT] " + line);
#endif
        }
        void StopRun(string reason)
        {
            if (!_running) return;
            _running = false; _commands?.AbortPending(_key); _agent?.Graph?.End(); Write("stop " + reason);
        }
        void Detach()
        {
            StopRun("detach");
            if (_turn != null) _turn.PrepInputOpened -= OnPrep;
            _turn = null; _commands = null; _bot = null; _opponent = null; _settings = null; _sheet = null;
            _opponentCooling.Clear(); _coolingOpen = false; _coolingRecorded = false; _started = false; _noItemReady = false;
            _observation = null; _chosen = default;
            if (_agent != null) { Destroy(_agent); _agent = null; }
        }
        void OnDisable() => Detach();
        void OnDestroy() => Detach();
    }
}
