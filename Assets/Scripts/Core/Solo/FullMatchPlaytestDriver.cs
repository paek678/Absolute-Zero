#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.Core.Solo
{
    // Opt-in validation participant. Never changes shared state, settings or the Solo graph.
    public sealed class FullMatchPlaytestDriver : MonoBehaviour
    {
        public static event Func<PlayerState, MiniGameTicket, bool, bool> PresentMiniGameResult;

        [Serializable] public sealed class Summary
        {
            public int schema = 2, peer, players, seat = -1, turns, selections, miniGames, seed;
            public bool completed, visibleResult, healthyTelemetry, returnedToLobby;
            public string failure, policy, behavior, evidenceLevel = "preflight; balance attribution not certified";
            public double seconds;
        }
        Summary _summary;
        PlayerState _owner;
        MiniGameTicket? _ticket;
        double _ticketAt, _started, _turnAt;
        double _readyDelayOverride = -1;
        string _key;
        bool _driving, _attempted, _ready, _finished, _visible;
        int _rejections;
        System.Random _random;
        string _folder, _shared;
        uint _ghostRequest;
        bool _mediaVariety;
        readonly Dictionary<short, int> _mediaSelections = new();
        readonly List<string> _policies = new() { "balanced", "attacker", "survival", "resource", "utility", "cautious" };
        static string Arg(string key, string fallback = "") => PlaytestTelemetry.Arg(key, fallback);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Arg("--az-playtest-drive") != "1" || string.IsNullOrWhiteSpace(Arg("--az-playtest"))) return;
            var go = new GameObject(nameof(FullMatchPlaytestDriver)); DontDestroyOnLoad(go); go.AddComponent<FullMatchPlaytestDriver>();
        }
        async void Start()
        {
            _started = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true; Application.targetFrameRate = 30;
            _folder = Path.GetFullPath(Arg("--az-playtest")); _shared = Directory.GetParent(_folder).FullName;
            _summary = new Summary { peer = int.Parse(Arg("--az-playtest-peer", "0")), players = int.Parse(Arg("--az-playtest-players", "1")),
                seed = int.Parse(Arg("--az-playtest-seed", "6601")), policy = Arg("--az-playtest-policy", "balanced"),
                behavior = Arg("--az-playtest-behavior", "normal") };
            _random = new System.Random(_summary.seed);
            _mediaVariety = Arg("--az-media") == "timed" && Arg("--az-playtest-variety") == "1";
            _summary.evidenceLevel = Arg("--az-playtest-cohort", "instrumentation-preflight") + "; causal balance attribution not certified";
            Application.logMessageReceived += Log;
            try
            {
                _readyDelayOverride = double.Parse(Arg("--az-playtest-ready-delay", "-1"), System.Globalization.CultureInfo.InvariantCulture);
                if (double.IsNaN(_readyDelayOverride) || double.IsInfinity(_readyDelayOverride)
                    || (_readyDelayOverride != -1 && (_readyDelayOverride < .1 || _readyDelayOverride > 20)))
                    throw new InvalidOperationException("invalid test Ready delay");
                if (!_policies.Contains(_summary.policy) || _summary.players < 1 || _summary.players > 4) throw new InvalidOperationException("invalid manifest");
                if (_summary.behavior != "normal" && _summary.behavior != "idle") throw new InvalidOperationException("invalid behavior");
                await Wait(() => AppBootstrapper.Instance?.IsReady == true && PlaytestTelemetry.Current != null, 30, "app/recorder startup");
                if (_summary.players == 1)
                {
                    Button("SoloBtn").onClick.Invoke();
                    await Wait(() => GameObject.Find("MainUI/SoloSelectionPanel/Body/StartBtn") != null, 5, "Solo selection");
                    GameObject.Find("MainUI/SoloSelectionPanel/Body/StartBtn").GetComponent<Button>().onClick.Invoke();
                }
                else
                {
                    var coordinator = NetworkSessionCoordinator.Instance;
                    coordinator.SetMatchParameters(_summary.players == 2 ? GameMode.OneVsOne : GameMode.Multi, _summary.players);
                    string code = Path.Combine(_shared, "lobby.tmpdata");
                    if (_summary.peer == 0)
                    {
                        var created = await coordinator.CreateLobbyAsync("AZ playtest");
                        if (created.IsFailure) throw new InvalidOperationException("Lobby create: " + created.ErrorCode);
                        File.WriteAllText(code, coordinator.CurrentLobby.LobbyCode);
                        await Wait(() => coordinator.CurrentLobby?.Players.Count == _summary.players, 100, "all lobby participants");
                        var started = await coordinator.StartMatchAsHostAsync();
                        if (started.IsFailure) throw new InvalidOperationException("Match start: " + started.ErrorCode);
                    }
                    else
                    {
                        await Wait(() => File.Exists(code), 100, "lobby available");
                        var joined = await coordinator.JoinGameAsync(File.ReadAllText(code));
                        if (joined.IsFailure) throw new InvalidOperationException("Lobby join: " + joined.ErrorCode);
                    }
                }
                await Wait(() => FindOwner() != null && TurnManager.Instance != null, 90, "local participant");
                _owner = FindOwner(); _summary.seat = _owner.PlayerIndex;
                // Connection arrival order is not logical seat identity.
                _summary.seed = checked(_summary.seed - _summary.peer + _summary.seat);
                _random = new System.Random(_summary.seed);
                string lineup = Arg("--az-playtest-lineup");
                if (!string.IsNullOrEmpty(lineup))
                {
                    var policies = lineup.Split(',');
                    _summary.policy = policies[_summary.seat % policies.Length];
                    _readyDelayOverride = double.Parse(Arg("--az-playtest-ready-delay", "-1"), System.Globalization.CultureInfo.InvariantCulture);
                if (double.IsNaN(_readyDelayOverride) || double.IsInfinity(_readyDelayOverride)
                    || (_readyDelayOverride != -1 && (_readyDelayOverride < .1 || _readyDelayOverride > 20)))
                    throw new InvalidOperationException("invalid test Ready delay");
                if (!_policies.Contains(_summary.policy)) throw new InvalidOperationException("invalid seat policy");
                }
                _owner.OnMiniGameStart += MiniGame;
                _owner.OnItemSelectionRejected += Rejected;
                var root = MatchCompositionRoot.Instance;
                var rule = root.ActiveConfig.Rule;
                string configuration = FormattableString.Invariant($"mode={root.ActiveConfig.Mode};players={root.ActiveConfig.RequiredPlayerCount};effectiveDuelWins=2;kills={rule.KillsToWin};maxRounds={rule.MaxRounds};prepSeconds={rule.PrepPhaseDuration};initialRandom={rule.InitialRandomItems};maxRandom={rule.MaxRandomItems};topup={rule.DeathmatchGrantCount};ghost={rule.EnableGhostSystem};tarotEffectEnabled=false;timeScale={Time.timeScale}");
                PlaytestTelemetry.Current.Record("resolved_configuration", configuration);
                File.WriteAllText(Path.Combine(_folder, "configuration.txt"), configuration + "\n" + string.Join("\n", root.ItemManager.GetAllItems().Select((item, id) => item == null ? id + ":null" : id + ":enabled=" + ItemAvailability.IsEnabled(item) + ":" + JsonUtility.ToJson(item))));
                if (_summary.players == 1 && (AppBootstrapper.Instance.SessionRouter.LaunchContext.Solo.IsValidationFixture
                    || !FindObjectsByType<BotTurnController>(FindObjectsSortMode.None).Any()))
                    throw new InvalidOperationException("production Solo BT missing");
                PlaytestTelemetry.Current.Record("driver_started", "policy=" + _summary.policy + ";seed=" + _summary.seed, _summary.seat);
                _driving = true;
                await Wait(IsTerminal, int.Parse(Arg("--az-playtest-timeout", "900")), "natural match terminal");
                _driving = false;
                await Wait(() => _visible || Button("RematchBtn")?.interactable == true, 25, "visible match result");
                _summary.visibleResult = true;
                await Task.Delay(500);
                PlaytestTelemetry.Current.Record("terminal_checkpoint", "final shared state");
                SoloLaunchProbe.WriteRender(Path.Combine(_folder, "result.png"));
                _summary.completed = true;
                File.WriteAllText(Path.Combine(_folder, "terminal.ready"), "observed");
                await Wait(() => Enumerable.Range(0, _summary.players).All(i => File.Exists(Path.Combine(_shared, i.ToString(), "terminal.ready"))), 30, "all peers observed result");
                // Leave only after every peer has final evidence; teardown is a separate postcondition.
                if (_summary.players == 1) Button("LobbyBtn").onClick.Invoke();
                else if (_summary.peer == 0) await NetworkSessionCoordinator.Instance.LeaveAsync();
                await Wait(() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "LobbyScene"
                    && (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening), 30, "return to lobby and network shutdown");
                _summary.returnedToLobby = true;
                PlaytestTelemetry.Current.Record("teardown_complete", "LobbyScene; network stopped");
                await Task.Delay(500);
            }
            catch (Exception error)
            {
                _summary.failure = error.GetType().Name + ": " + error.Message;
                PlaytestTelemetry.Current?.Record("driver_failed", _summary.failure);
                try { SoloLaunchProbe.WriteRender(Path.Combine(_folder, "failure.png")); } catch { }
            }
            finally { Finish(); }
        }
        static PlayerState FindOwner() => FindObjectsByType<PlayerState>(FindObjectsSortMode.None)
            .FirstOrDefault(p => p.IsSpawned && p.IsParticipantReady && p.IsOwner && !p.IsBotControlled);
        static Button Button(string name) => FindObjectsByType<Button>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy);
        bool IsTerminal()
        {
            var root = MatchCompositionRoot.Instance;
            if (root == null) return false;
            if (root.ActiveConfig.Mode == GameMode.Multi)
                return root.NetworkState.TerminalResult.Value.WinnerMask != 0 && root.NetworkState.TerminalResult.Value.Released;
            var m = root.MatchManager;
            return m != null && (m.P1RoundWins.Value >= 2 || m.P2RoundWins.Value >= 2);
        }
        void Log(string message, string stack, LogType type)
        { if (message.StartsWith("[MatchResult] Visible seq=")) _visible = true; }
        void MiniGame(MiniGameTicket ticket)
        {
            _ticket = ticket; _ticketAt = Time.realtimeSinceStartupAsDouble;
            PlaytestTelemetry.Current?.Record("minigame_ticket", "attempt=" + ticket.AttemptId + ";limit=" + ticket.TimeLimit, _summary.seat, ticket.CopyId);
        }
        void Rejected(uint copy)
        {
            _rejections++;
            PlaytestTelemetry.Current?.Record("selection_rejected", "owner callback", _summary.seat, copy);
        }
        void Update()
        {
            if (!_driving || _owner == null || !_owner.IsSpawned || TurnManager.Instance == null) return;
            try { Drive(); }
            catch (Exception error) { _summary.failure = "driver: " + error.GetType().Name + ": " + error.Message; Finish(); }
        }
        void Drive()
        {
            var turn = TurnManager.Instance; var root = MatchCompositionRoot.Instance;
            if (turn.CurrentPhase.Value != TurnPhase.PrepPhase || IsTerminal()) return;
            string key = root.Generation + "/" + root.MatchManager.RoundNumber.Value + "/" + turn.TurnNumber.Value;
            double now = Time.realtimeSinceStartupAsDouble;
            if (_key != key)
            {
                _key = key; _turnAt = now; _attempted = _ready = false; _ticket = null; _rejections = 0; _summary.turns++;
            }
            if (root.ActiveConfig.Mode == GameMode.Multi && root.NetworkState.IsDeathmatchGrantViewBlocked) return;
            // Explicit boundary investigation: submit no selections, Ready or ghost commands.
            // Natural deadlines and death/round progression remain owned by the game.
            if (_summary.behavior == "idle") return;
            if (_owner.CurrentLifeState.Value != LifeState.Alive)
            {
                if (_attempted || now - _turnAt < 1) return;
                _attempted = true;
                var ns = root.NetworkState;
                var target = FindObjectsByType<PlayerState>(FindObjectsSortMode.None)
                    .Where(p => p.IsSpawned && p.CurrentLifeState.Value == LifeState.Alive).OrderBy(p => p.Temperature.Value).FirstOrDefault();
                if (target == null) return;
                byte skill = (byte)(_summary.policy == "utility" && (ns.GhostPossessionSpentMask.Value & (1 << _summary.seat)) == 0 ? 1 : 0);
                bool cooling = false;
                for (int i = 0; i < ns.GhostCooldowns.Count; i++)
                    if (ns.GhostCooldowns[i].Seat == _summary.seat && ns.GhostCooldowns[i].Skill == skill && ns.GhostCooldowns[i].RemainingTurns > 0) cooling = true;
                if (!cooling) turn.UseGhostSkillRpc(skill, (byte)target.PlayerIndex, ns.GhostMatchEpoch.Value, ns.GhostRoundEpoch.Value, turn.TurnNumber.Value, ++_ghostRequest);
                return;
            }
            if (_owner.IsReady.Value || _owner.Temperature.Value <= 0) return;
            if (_ticket.HasValue)
            {
                var ticket = _ticket.Value;
                // Explicit UI timeout test: let the real mini-game clock produce the failure.
                // No modeled result or early Ready may bypass that UI path.
                if (Arg("--az-playtest-minigame-input") == "none")
                {
                    if (now - _ticketAt <= ticket.TimeLimit + 1) return;
                    _ticket = null; _summary.miniGames++;
                    PlaytestTelemetry.Current?.Record("minigame_observed_timeout", "attempt=" + ticket.AttemptId, _summary.seat, ticket.CopyId);
                    return;
                }
                if (now - _ticketAt < Math.Min(2, Math.Max(.4, ticket.TimeLimit * .5))) return;
                _ticket = null; _summary.miniGames++;
                bool success = _random.NextDouble() < (_summary.policy == "cautious" ? .65 : .9);
                PlaytestTelemetry.Current?.Record("minigame_submit", "modeled=" + success + ";attempt=" + ticket.AttemptId, _summary.seat, ticket.CopyId);
                bool presented = false;
                if (PresentMiniGameResult != null)
                    foreach (Func<PlayerState, MiniGameTicket, bool, bool> handler in PresentMiniGameResult.GetInvocationList())
                        presented |= handler(_owner, ticket, success);
                if (!presented)
                    _owner.SubmitMiniGameResultServerRpc(ticket.Slot, success, ticket.MatchEpoch, ticket.RoundEpoch, ticket.Turn, ticket.AttemptId, ticket.CopyId);
                return;
            }
            if (!_attempted && now - _turnAt >= (_summary.policy == "cautious" ? 3 : .75))
            {
                if (_summary.turns <= 3) SoloLaunchProbe.WriteRender(Path.Combine(_folder, "prep-" + _summary.turns + ".png"));
                _attempted = true; Select(); return;
            }
            double readyDelay = _summary.policy switch { "attacker" => 1.25, "resource" => 1.75, "survival" => 2, "cautious" => 5, _ => 2.5 };
            if (_readyDelayOverride >= 0) readyDelay = _readyDelayOverride;
            if (_attempted && !_ready && !_ticket.HasValue && now - _turnAt > readyDelay
                && (_owner.HasSelectedItem.Value || _rejections > 0 || now - _turnAt > 8))
            {
                _ready = true; PlaytestTelemetry.Current?.Record("ready_sent", "policy", _summary.seat); ProbeInventoryCommands.Ready(_owner);
            }
        }
        void Select()
        {
            var targets = FindObjectsByType<PlayerState>(FindObjectsSortMode.None).Where(p => p.IsSpawned && p.IsParticipantReady
                && p.PlayerIndex != _summary.seat && p.CurrentLifeState.Value == LifeState.Alive && p.Temperature.Value > 0)
                .OrderBy(p => p.Temperature.Value).ThenBy(p => p.PlayerIndex).ToArray();
            if (targets.Length == 0) return;
            int tied = targets.TakeWhile(p => Mathf.Approximately(p.Temperature.Value, targets[0].Temperature.Value)).Count();
            int picked = _random.Next(tied);
            (targets[0], targets[picked]) = (targets[picked], targets[0]);
            var inv = _owner.GetInventory(); double best = double.NegativeInfinity; byte chosen = 0, targetSeat = 255; uint copy = 0;
            var choices = new List<string>();
            for (byte i = 0; i < inv.SlotStates.Count; i++)
            {
                var slot = inv.SlotStates[i]; var item = inv.GetItemData(i);
                if (!slot.IsUsable || !ItemAvailability.IsEnabled(item) || (_owner.IsBasicBlocked.Value && item.SlotType == ItemSlotType.Main)) continue;
                if (item is SpecialItemDataSO special && special.SpecialEffect == SpecialEffectType.RevealOpponent && !targets[0].IsReady.Value) continue;
                byte target = item.GetTargetMode() == TargetMode.Self ? ActionIntent.NoTarget : (byte)targets[0].PlayerIndex;
                // Restrict observations to own inventory and visible temperatures. Server remains admission authority.
                var estimate = BotDecisionModel.Estimate(ItemEffectRuleSnapshot.From(item, slot.ItemId), slot, target, 0,
                    _owner.Temperature.Value, targets[0].Temperature.Value, item.Persistence == ItemPersistence.Permanent);
                double score = _summary.policy switch
                {
                    "attacker" => estimate.Damage * 2 + estimate.Heal * .3 + estimate.Utility * .2,
                    "survival" => estimate.Heal * 2 + estimate.Defense + estimate.Damage * .5,
                    "resource" => estimate.Damage + estimate.Heal + (estimate.Permanent ? 4 : 0),
                    "utility" => estimate.Utility * 3 + estimate.Heal + estimate.Damage * .5,
                    _ => estimate.Damage + estimate.Heal + estimate.Utility + estimate.Defense * .2
                };
                // Recording-only input preference. Uses the real inventory/admission path;
                // ordinary balance cohorts and the production Solo BT remain unchanged.
                if (_mediaVariety)
                {
                    _mediaSelections.TryGetValue(slot.ItemId, out int previousSelections);
                    score -= previousSelections * 100;
                    if (item.Persistence != ItemPersistence.Permanent) score += 30;
                }
                if (Arg("--az-playtest-minigame-input") == "none" && item.RequiresMiniGame) score += 1000;
                choices.Add(slot.ItemId + ":" + slot.CopyId + ":" + score.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                score += _random.NextDouble() * .2;
                if (score <= best) continue;
                best = score; chosen = i; targetSeat = target; copy = slot.CopyId;
            }
            PlaytestTelemetry.Current?.Record("policy_candidates", string.Join(";", choices), _summary.seat);
            if (copy == 0) return;
            if (_mediaVariety)
            {
                short itemId = inv.SlotStates[chosen].ItemId;
                _mediaSelections.TryGetValue(itemId, out int previousSelections);
                _mediaSelections[itemId] = previousSelections + 1;
                PlaytestTelemetry.Current?.Record("media_variety_selection", inv.GetItemData(chosen).ItemName
                    + ";previousSelections=" + previousSelections, _summary.seat, copy);
            }
            _summary.selections++;
            PlaytestTelemetry.Current?.Record("select_sent", "slot=" + chosen + ";target=" + targetSeat, _summary.seat, copy);
            ProbeInventoryCommands.SelectItem(_owner, chosen, targetSeat);
        }
        async Task Wait(Func<bool> predicate, double seconds, string label)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate())
            {
                if (_finished) throw new OperationCanceledException();
                if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException(label);
                await Task.Delay(50);
            }
        }
        void Finish()
        {
            if (_finished) return; _finished = true; _driving = false;
            Application.logMessageReceived -= Log;
            if (_owner != null) { _owner.OnMiniGameStart -= MiniGame; _owner.OnItemSelectionRejected -= Rejected; }
            var recorder = PlaytestTelemetry.Current;
            bool errors = recorder == null || recorder.Errors != 0;
            bool closed = recorder != null && recorder.Close();
            _summary.healthyTelemetry = !errors && closed;
            _summary.seconds = Time.realtimeSinceStartupAsDouble - _started;
            File.WriteAllText(Path.Combine(_folder, "summary.json"), JsonUtility.ToJson(_summary, true));
            Application.Quit(_summary.completed && _summary.visibleResult && _summary.returnedToLobby && _summary.healthyTelemetry && _summary.failure == null ? 0 : 2);
        }
    }
}
#endif
