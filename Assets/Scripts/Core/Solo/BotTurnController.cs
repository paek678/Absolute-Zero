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
        TurnManager _turn;
        PlayerState _bot, _opponent;
        ResolvedSoloSettings _settings;
        TrustedBotCommandAdapter _commands;
        BehaviorGraphAgent _agent;
        PrepInputKey _key;
        bool _started, _running;
        ulong _request, _operation;
        double _thinkUntil, _readyAt;
        System.Random _random;
        BotObservation _observation;
        BotCandidate _chosen;
        readonly Queue<string> _trace = new();
        public IReadOnlyCollection<string> Trace => _trace;
        public int RunCount { get; private set; }
        public int SelectionCount { get; private set; }
        public int ReadyCount { get; private set; }
        public bool IsRunning => _running;
        public BehaviorGraph RuntimeGraph => _agent != null ? _agent.Graph : null;
        internal int RetryBudget => _settings?.RetryBudget ?? 0;
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
            if (_turn != null && _turn.TryGetPrepInputSnapshot(out var snapshot)) OnPrep(snapshot);
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
            StopRun("new Prep"); _bot = bot; _opponent = opponent; _commands = commands; _key = snapshot.Key;
            if (_agent == null)
            {
                _agent = gameObject.AddComponent<BehaviorGraphAgent>();
                _agent.enabled = false; _agent.NetcodeRunOnlyOnOwner = false;
                _agent.Graph = _settings.Graph; _agent.Init();
            }
            int seed = BotDecisionModel.Seed(_settings.BotId, _key);
            _random = new System.Random(seed); _started = true; _running = true; RunCount++;
            Write("start seed=" + seed);
            _agent.Graph.Restart();
        }
        internal bool CanAct() => isActiveAndEnabled && _running && _commands != null
            && _bot != null && _opponent != null && _bot.IsParticipantReady
            && !_bot.IsReady.Value && _bot.Temperature.Value > 0 && _opponent.Temperature.Value > 0
            && _commands.TryWindow(_key, out _);
        internal bool CaptureObservation()
        {
            if (!CanAct() || !_commands.TryWindow(_key, out var snapshot)) return false;
            _observation = new BotObservation(_key, (byte)_bot.PlayerIndex, (byte)_opponent.PlayerIndex,
                _bot.Temperature.Value, _opponent.Temperature.Value, _bot.FanSpeed.Value,
                _opponent.FanSpeed.Value, _opponent.IsReady.Value, snapshot.RemainingSeconds, Array.Empty<BotCandidate>());
            _chosen = default; Write("observe remaining=" + snapshot.RemainingSeconds.ToString("F3"));
            SetDebugVariables(); return true;
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
                candidates.Add(BotDecisionModel.Estimate(ItemEffectRuleSnapshot.From(item, slot.ItemId), slot, target, delay,
                    _observation.Temperature, _observation.OpponentTemperature, item.Persistence == ItemPersistence.Permanent));
            }
            _observation = new BotObservation(_key, _observation.Seat, _observation.Opponent,
                _observation.Temperature, _observation.OpponentTemperature, _observation.FanSpeed,
                _observation.OpponentFanSpeed, _observation.OpponentReady, _observation.Remaining, candidates);
            Write("legal candidates=" + candidates.Count); return candidates.Count > 0;
        }
        internal bool Choose(BotTactic tactic)
        {
            if (!CanAct() || _observation == null || !BotDecisionModel.TryChoose(_observation, tactic,
                _settings.TacticalWeights, _settings.DecisionNoise, _random, _settings.ReadyReserveSeconds, out _chosen)) return false;
            double thought = _settings.MinimumThinkSeconds + _random.NextDouble() * (_settings.MaximumThinkSeconds - _settings.MinimumThinkSeconds);
            thought = Math.Min(thought, Math.Max(0, _observation.Remaining - _chosen.Delay - _settings.ReadyReserveSeconds - 0.1));
            if (_observation.Temperature <= 5) thought = 0;
            _thinkUntil = Now + thought;
            Write(tactic + " copy=" + _chosen.Copy + " item=" + _chosen.Item + " target=" + _chosen.Target + " think=" + thought.ToString("F3"));
            SetDebugVariables(); return true;
        }
        internal Node.Status Think()
        {
            if (!CanAct()) return Node.Status.Failure;
            // Cooling continues during optional thinking. Re-enter the bounded
            // observation/choice sequence when survival becomes newly relevant;
            // ending the wait alone would submit the old high-temperature attack.
            if (_settings.TacticalWeights.Survival > 0 && _observation.Temperature > 12 && _bot.Temperature.Value <= 12)
            {
                Write("replan public survival threshold");
                return Node.Status.Failure;
            }
            return Now >= _thinkUntil || _bot.Temperature.Value <= 5 ? Node.Status.Success : Node.Status.Running;
        }
        internal bool Submit()
        {
            if (!CanAct() || _chosen.Copy == 0) return false;
            var result = _commands.BeginUse(_key, ++_request, _chosen.Copy, _chosen.Target);
            _operation = result.OperationId; Write("submit " + result.Status + ":" + result.Reason + " due=" + result.DueTime.ToString("F3"));
            return result.Status == PlayerActionStatus.Pending;
        }
        internal Node.Status AwaitUse()
        {
            if (!CanAct()) return Node.Status.Failure;
            var result = _commands.Poll(_key, _operation);
            if (result.Status == PlayerActionStatus.Pending) return Node.Status.Running;
            Write("operation " + result.Status + ":" + result.Reason);
            if (result.Status != PlayerActionStatus.Queued) return Node.Status.Failure;
            SelectionCount++; return Node.Status.Success;
        }
        internal bool ChooseReadyTime()
        {
            if (!CanAct() || !_commands.TryWindow(_key, out var snapshot)) return false;
            _readyAt = _settings.ReadyPolicy == BotReadyPolicy.NearDeadline && _bot.Temperature.Value > 10
                ? Math.Max(Now, snapshot.Deadline - Math.Max(0.15, _settings.ReadyReserveSeconds)) : Now;
            Write("ready at=" + _readyAt.ToString("F3")); return true;
        }
        internal Node.Status WaitReady() => !CanAct() ? Node.Status.Failure : Now >= _readyAt || _bot.Temperature.Value <= 10 ? Node.Status.Success : Node.Status.Running;
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
        void SetDebugVariables()
        {
            _agent.SetVariableValue("Round", _key.Round); _agent.SetVariableValue("Turn", _key.Turn);
            _agent.SetVariableValue("Own temperature", _observation.Temperature);
            _agent.SetVariableValue("Opponent temperature", _observation.OpponentTemperature);
            _agent.SetVariableValue("Remaining seconds", (float)_observation.Remaining);
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
            _turn = null; _commands = null; _bot = null; _opponent = null; _settings = null;
            if (_agent != null) { Destroy(_agent); _agent = null; }
        }
        void OnDisable() => Detach();
        void OnDestroy() => Detach();
    }
}
