#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Validation.Actions
{
    // Real local NGO, ItemManager, TurnManager and CombatResolver. Only rendering
    // is omitted: the human ACK observer explicitly settles the headless view.
    public sealed partial class Plan036ActionProbe : MonoBehaviour
    {
        public SoloDuelDefinitionSO Encounter;
        public ItemDataSO[] Catalog;
        public CosmeticRegistrySO Cosmetics;
        public GameObject PlayerPrefab;
        public PlayerSpawnManager Spawner;
        const string Menu = "Assets/Tests/Plan036Actions/ActionFixture.unity";
        static Plan036ActionProbe _instance;
        readonly Report _report = new();
        readonly List<PrepInputSnapshot> _openings = new();
        readonly List<CombatResultData> _combatResults = new();
        readonly List<MiniGameTicket> _humanTickets = new();
        TurnManager _turn;
        PlayerState _human, _bot;
        int _botMiniGames;
        bool _finished;
        double _deadline;
        string _reportPath;

        [Serializable] sealed class Report
        {
            public bool passed;
            public int seed = 3605;
            public string scope = "T05 real local NGO shared actions, committed Prep, two normal combats plus a replaced-CopyId combat; explicit headless human presentation ACK, no BT or bot delay";
            public List<string> checks = new();
            public List<string> openings = new();
            public List<string> combat = new();
            public List<string> errors = new();
            public string failure;
        }

        void Awake()
        {
            if (_instance != null) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        async void Start()
        {
            if (_instance != this) return;
            _reportPath = Path.Combine(Application.persistentDataPath, "plan036-actions.json");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--plan036-report") _reportPath = Path.GetFullPath(args[i + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(_reportPath));
            _deadline = Time.realtimeSinceStartupAsDouble + 100;
            Application.logMessageReceived += OnLog;
            TurnManager.OnCombatResult += OnCombat;
            try { await Run(); _report.passed = _report.errors.Count == 0; }
            catch (Exception error) { _report.failure = error.ToString(); }
            Finish();
        }

        async Task Run()
        {
            UnityEngine.Random.InitState(_report.seed);
            var root = new GameObject("T05 application fixture");
            DontDestroyOnLoad(root);
            var nm = root.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { EnableSceneManagement = true };
            var transport = root.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = PlayerPrefab;
            nm.AddNetworkPrefab(PlayerPrefab);
            root.AddComponent<NetworkSessionCoordinator>();
            var app = root.AddComponent<AppBootstrapper>();
            await Until(() => app.IsReady, 5, "local application readiness");
            var solo = new SoloSessionCoordinator(app.SessionRouter, new NgoLocalHostRuntime(nm), 18786, ReturnToMenu);
            TurnManager.DebugBeforeReadyHandoff = _ => AttachTurn(TurnManager.Instance);
            var start = await solo.StartValidationAsync(Encounter,
                new SoloConfigurationContext(Encounter.SharedOneVsOneRule, Catalog, Cosmetics));
            Check(start.IsSuccess, "real Solo coordinator starts isolated action match: " + start.ErrorMessage);
            await Until(() => _openings.Count >= 1, 30, "first committed Prep signal");
            TurnManager.DebugBeforeReadyHandoff = null;
            _human = _turn.GetPlayer(0);
            _bot = _turn.GetPlayer(1);
            _human.OnMiniGameStart += OnHumanMiniGame;
            _bot.OnMiniGameStart += OnBotMiniGame;
            Check(nm.ConnectedClientsIds.Count == 1 && MatchCompositionRoot.Instance.Registry.ReadyCount == 2,
                "one real connection controls distinct human and bot participants");
            Check(ItemManager.Instance.GetAllItems().SequenceEqual(Catalog) && Catalog.Length == 21,
                "fixture uses the current actual 21-item duel registry");
            Check(_turn.TryGetPrepInputSnapshot(out var first) && first.Key == _openings[0].Key,
                "late subscriber query returns the same committed first-turn key");
            float[] originalFirstTemperatures = first.TemperaturesAtStart.ToArray();
            await Task.Delay(200);
            Check(_turn.TryGetPrepInputSnapshot(out var later) && later.Key == first.Key
                && later.RemainingSeconds < first.RemainingSeconds - 0.05
                && later.StartTime == first.StartTime && later.Deadline == first.Deadline,
                "catch-up snapshot returns remaining time without resetting the deadline");

            if (Environment.GetCommandLineArgs().Contains("--plan036-delay")) VerifyDelayMatrix(first.Key);
            VerifyHumanNormalSelection();
            VerifyHumanMiniGameFailure();
            var humanAttack = VerifyHumanMiniGameSuccess();
            var botAttack = VerifyBotSharedSelection(first.Key);
            if (Environment.GetCommandLineArgs().Contains("--plan036-delay")) await VerifyActualClockDelay(first.Key, botAttack);
            int humanUsesBefore = Uses(_human.GetInventory(), humanAttack.CopyId);
            int botUsesBefore = Uses(_bot.GetInventory(), botAttack.CopyId);
            Check(humanUsesBefore > 0 && botUsesBefore > 0 && _botMiniGames == 0,
                "both attacks are queued with remaining uses and bot emitted no mini-game ticket");

            // An actual emote creates the existing tail between input closure and Attack.
            _human.SendEmoteServerRpc(0);
            _human.PressReadyServerRpc();
            Check(_human.IsReady.Value && !_human.IsFanActive.Value, "human Ready retains the existing fan stop path");
            var ready = _bot.ServerPressBotReady(first.Key);
            Check(ready.Status == PlayerActionStatus.ReadyAccepted && _bot.IsReady.Value && !_bot.IsFanActive.Value,
                "shared bot Ready accepts the current key and stops its fan");
            Check(Uses(_human.GetInventory(), humanAttack.CopyId) == humanUsesBefore
                && Uses(_bot.GetInventory(), botAttack.CopyId) == botUsesBefore,
                "Ready does not consume either queued item");
            await Until(() => !_turn.TryGetPrepInputSnapshot(out _), 3, "committed window closes before emote tail");
            Check(_turn.CurrentPhase.Value == TurnPhase.PrepPhase,
                "input closes while the existing emote tail still reports Prep phase");
            uint botBeforeClosedRequest = _bot.GetInventory().CurrentFingerprint();
            var closed = _bot.ServerQueueBotItem(first.Key, botAttack);
            Check(closed.Status != PlayerActionStatus.Queued
                && _bot.GetInventory().CurrentFingerprint() == botBeforeClosedRequest,
                "closed input rejects late bot completion during the Prep emote tail");
            _human.CancelSelectionServerRpc();
            Check(_human.HasSelectedItem.Value, "human cannot cancel a readied selection during the tail");

            await Until(() => _combatResults.Count >= 1, 12, "first actual combat resolution");
            Check(Uses(_human.GetInventory(), humanAttack.CopyId) == humanUsesBefore - 1
                && Uses(_bot.GetInventory(), botAttack.CopyId) == botUsesBefore - 1,
                "existing CombatResolver consumes each queued attack exactly once");
            Check(_combatResults[0].EventCount == 2 && _combatResults[0].P1MainItemId == ItemId("Water Gun")
                && _combatResults[0].P2MainItemId == ItemId("Water Gun")
                && _combatResults[0].P1TempAfterCombat < _combatResults[0].P1TempBeforeCombat
                && _combatResults[0].P2TempAfterCombat < _combatResults[0].P2TempBeforeCombat,
                "actual combat publishes both selected attacks and resulting temperature damage");

            await Until(() => _openings.Count >= 2, 20, "second committed Prep signal");
            Check(_turn.TryGetPrepInputSnapshot(out var second) && second.Key.Turn == first.Key.Turn + 1
                && second.Key.Round == first.Key.Round && second.Key.Generation == first.Key.Generation
                && second.Key.Sequence != first.Key.Sequence,
                "second committed Prep has fresh turn/sequence and stable round/session identity");
            Check(!_human.IsReady.Value && !_bot.IsReady.Value && !_human.HasSelectedItem.Value
                && !_bot.HasSelectedItem.Value && !_human.GetActionQueue().selectedAction.HasValue
                && !_bot.GetActionQueue().selectedAction.HasValue && _human.IsFanActive.Value && _bot.IsFanActive.Value,
                "second-turn consumer observes completed selection/Ready/fan reset");
            uint beforeStale = _bot.GetInventory().CurrentFingerprint();
            var stale = _bot.ServerQueueBotItem(first.Key, botAttack);
            Check(stale.Status != PlayerActionStatus.Queued && !_bot.HasSelectedItem.Value
                && _bot.GetInventory().CurrentFingerprint() == beforeStale,
                "first-turn candidate cannot enter the second turn or debit inventory");

            // Use normal self-target items in the next real turn to cover recovery.
            var humanTea = FindUsable(_human.GetInventory(), "Warm Tea");
            _human.SelectItemServerRpc((byte)humanTea.Slot, ActionIntent.NoTarget, humanTea.CopyId);
            Check(_human.HasSelectedItem.Value && _human.GetActionQueue().selectedAction.Value.TargetSeat == ActionIntent.NoTarget,
                "human self-target selection uses the shared target policy");
            var botTea = FindUsable(_bot.GetInventory(), "Warm Tea");
            var validatedTea = _bot.ServerValidateBotItem(second.Key, botTea.CopyId, ActionIntent.NoTarget, out var teaCandidate);
            Check(validatedTea.Status == PlayerActionStatus.Validated
                && _bot.ServerQueueBotItem(second.Key, teaCandidate).Status == PlayerActionStatus.Queued,
                "bot self-target candidate follows the same recovery queue path");
            int humanTeaUses = Uses(_human.GetInventory(), humanTea.CopyId);
            int botTeaUses = Uses(_bot.GetInventory(), botTea.CopyId);
            Check(_bot.ServerPressBotReady(second.Key).Status == PlayerActionStatus.ReadyAccepted,
                "bot can Ready first on the second turn");
            _human.PressReadyServerRpc();
            await Until(() => _combatResults.Count >= 2, 10, "second actual combat resolution");
            Check(Uses(_human.GetInventory(), humanTea.CopyId) == humanTeaUses - 1
                && Uses(_bot.GetInventory(), botTea.CopyId) == botTeaUses - 1,
                "second real combat consumes each recovery copy exactly once");
            Check(_combatResults[1].EventCount == 2
                && _combatResults[1].P1TempAfterCombat >= _combatResults[1].P1TempBeforeCombat
                && _combatResults[1].P2TempAfterCombat >= _combatResults[1].P2TempBeforeCombat,
                "second real combat applies both recovery outcomes");
            await Until(() => _openings.Count >= 3, 15, "next Prep after two resolved turns");
            Check(_openings.Select(x => x.Key).Distinct().Count() == _openings.Count && _combatResults.Count == 2,
                "each observed Prep key publishes once and exactly two combats resolve");
            Check(_botMiniGames == 0 && _humanTickets.Count == 2,
                "only the two requested human mini-games emitted tickets");
            Check(first.TemperaturesAtStart.SequenceEqual(originalFirstTemperatures)
                && _openings[1].TemperaturesAtStart.All(x => x < 37),
                "committed temperature baselines retain their turn values after later combat");
            VerifyMiniGameInvalidation();
            await VerifyReplacedCopyCombat();
            TrustedBotCommandAdapter pendingAtExit = null;
            PrepInputKey exitKey = default;
            if (Environment.GetCommandLineArgs().Contains("--plan036-delay"))
            {
                await Until(() => _openings.Count >= 4, 15, "fourth Prep for pending shutdown");
                _turn.TryGetPrepInputSnapshot(out var exitWindow);
                exitKey = exitWindow.Key;
                var exitCopy = EnsureItem(_bot.GetInventory(), "Water Gun");
                pendingAtExit = _bot.GetBotCommands();
                Check(pendingAtExit.BeginUse(exitKey, 999, exitCopy.CopyId, 0).Status == PlayerActionStatus.Pending, "shutdown begins with a genuinely pending bot item");
            }
            var oldTurn = _turn;
            await solo.StopAsync();
            Check(!nm.IsListening && app.SessionRouter.Current == null && MatchCompositionRoot.Instance == null,
                "action match stops and releases its real participants and session");
            Check(oldTurn == null || !oldTurn.TryGetPrepInputSnapshot(out _),
                "retained turn reference cannot expose an open snapshot after shutdown");
            Check(UnityServices.State == ServicesInitializationState.Uninitialized,
                "shared command fixture performs no UGS initialization");
            if (pendingAtExit != null)
            {
                Check(pendingAtExit.Poll(exitKey, 999).Status == PlayerActionStatus.Stale, "shutdown invalidates pending callback before it can act");
                TurnManager.DebugBeforeReadyHandoff = _ => AttachTurn(TurnManager.Instance);
                var restarted = await solo.StartValidationAsync(Encounter, new SoloConfigurationContext(Encounter.SharedOneVsOneRule, Catalog, Cosmetics));
                Check(restarted.IsSuccess, "same local application restarts after pending shutdown");
                await Until(() => TurnManager.Instance != null && TurnManager.Instance.TryGetPrepInputSnapshot(out _), 30, "fresh Prep after pending shutdown");
                TurnManager.DebugBeforeReadyHandoff = null;
                TurnManager.Instance.TryGetPrepInputSnapshot(out var restartedWindow);
                var restartedBot = TurnManager.Instance.GetPlayer(1);
                uint freshInventory = restartedBot.GetInventory().CurrentFingerprint();
                Check(restartedWindow.Key.Generation != exitKey.Generation
                    && pendingAtExit.Poll(exitKey, 999).Status == PlayerActionStatus.Stale
                    && !restartedBot.HasSelectedItem.Value && restartedBot.GetInventory().CurrentFingerprint() == freshInventory,
                    "old operation cannot queue or debit the new session");
                await solo.StopAsync();
            }
            solo.Dispose();
            Destroy(root);
            Destroy(Spawner.gameObject);
        }

        async Task VerifyReplacedCopyCombat()
        {
            Check(_turn.TryGetPrepInputSnapshot(out var current), "third turn exposes its own committed key");
            var inventory = _bot.GetInventory();
            var copy = EnsureItem(inventory, "Water Gun");
            Check(_bot.ServerValidateBotItem(current.Key, copy.CopyId, 0, out var candidate).Status == PlayerActionStatus.Validated
                && _bot.ServerQueueBotItem(current.Key, candidate).Status == PlayerActionStatus.Queued,
                "replacement scenario queues the original physical copy normally");
            // Deliberate fixture mutation models a copy being replaced after queueing.
            // No ConsumeItem/effect or phase method is called by this driver.
            var replacement = inventory.AssignNewCopyId(inventory.SlotStates[candidate.SlotIndex]);
            inventory.SlotStates[candidate.SlotIndex] = replacement;
            Check(replacement.CopyId != candidate.CopyId
                && inventory.GetItemData(candidate.SlotIndex) == candidate.ItemData
                && _bot.GetActionQueue().selectedAction.Value.CopyId == candidate.CopyId,
                "same-SO replacement retains an explicitly stale queued CopyId");
            _human.PressReadyServerRpc();
            Check(_bot.ServerPressBotReady(current.Key).Status == PlayerActionStatus.ReadyAccepted,
                "replaced-copy scenario enters combat through normal Ready");
            await Until(() => _combatResults.Count >= 3, 10, "actual combat with replaced queued copy");
            Check(_combatResults[2].EventCount == 0 && Uses(inventory, replacement.CopyId) == replacement.RemainingUses
                && _combatResults[2].P1TempAfterCombat == _combatResults[2].P1TempBeforeCombat
                && _combatResults[2].P2TempAfterCombat == _combatResults[2].P2TempBeforeCombat,
                "actual CombatResolver rejects stale queued copy without effect or replacement consumption");
        }

        void VerifyMiniGameInvalidation()
        {
            var inventory = _human.GetInventory();
            int rejected = 0;
            void OnRejected(uint copyId) { rejected++; }
            _human.OnItemSelectionRejected += OnRejected;
            try
            {
                var copy = EnsureItem(inventory, "Water Gun");
                _human.SelectItemServerRpc((byte)copy.Slot, 1, copy.CopyId);
                var old = _humanTickets.Last();
                uint fingerprint = inventory.CurrentFingerprint();
                int tickets = _humanTickets.Count;
                _human.SelectItemServerRpc((byte)copy.Slot, 1, copy.CopyId);
                Check(rejected == 0 && _humanTickets.Count == tickets
                    && _human.DebugPendingMiniGameAttemptId == old.AttemptId,
                    "duplicate selection cannot cancel the active mini-game or start a second ticket");
                inventory.SlotStates.RemoveAt(copy.Slot);
                uint afterRemoval = inventory.CurrentFingerprint();
                Check(rejected == 1 && _human.DebugPendingMiniGameAttemptId == 0,
                    "copy invalidation clears its attempt and releases owner input exactly once");
                SubmitMiniGame(old, false);
                Check(rejected == 1 && inventory.CurrentFingerprint() == afterRemoval && !_human.HasSelectedItem.Value,
                    "late failure for invalidated copy neither consumes nor rejects another input");

                copy = EnsureItem(inventory, "Water Gun");
                _human.SelectItemServerRpc((byte)copy.Slot, 1, copy.CopyId);
                var current = _humanTickets.Last();
                fingerprint = inventory.CurrentFingerprint();
                SubmitMiniGame(old, true);
                Check(current.AttemptId > old.AttemptId && _human.DebugPendingMiniGameAttemptId == current.AttemptId
                    && inventory.CurrentFingerprint() == fingerprint && rejected == 1,
                    "old successful result cannot clear a newer pending attempt");
                int round = MatchCompositionRoot.Instance.MatchManager.RoundNumber.Value;
                MatchCompositionRoot.Instance.MatchManager.RoundNumber.Value = round + 1;
                try { SubmitMiniGame(current, false); }
                finally { MatchCompositionRoot.Instance.MatchManager.RoundNumber.Value = round; }
                Check(_human.DebugPendingMiniGameAttemptId == 0 && rejected == 2
                    && inventory.CurrentFingerprint() == fingerprint && !_human.HasSelectedItem.Value,
                    "round mismatch rejects even when wire epochs and turn are unchanged");

                _human.SelectItemServerRpc((byte)copy.Slot, 1, copy.CopyId);
                current = _humanTickets.Last();
                _human.CancelPendingMiniGame();
                _human.CancelPendingMiniGame();
                SubmitMiniGame(current, true);
                Check(rejected == 3 && inventory.CurrentFingerprint() == fingerprint && !_human.HasSelectedItem.Value,
                    "repeated cancellation and stale result emit one rejection and no item debit");
            }
            finally { _human.OnItemSelectionRejected -= OnRejected; }
        }

        void VerifyHumanNormalSelection()
        {
            var inventory = _human.GetInventory();
            var fan = FindUsable(inventory, "Fan");
            uint before = inventory.CurrentFingerprint();
            _human.SelectItemServerRpc((byte)fan.Slot, 0, fan.CopyId);
            Check(!_human.HasSelectedItem.Value && inventory.CurrentFingerprint() == before,
                "human attack rejects self-target without a queue or debit");
            _human.SelectItemServerRpc((byte)fan.Slot, 1, fan.CopyId);
            Check(_human.HasSelectedItem.Value && inventory.CurrentFingerprint() == before,
                "normal human selection queues without consuming inventory");
            var chosen = _human.GetActionQueue().selectedAction.Value;
            var tea = FindUsable(inventory, "Warm Tea");
            _human.SelectItemServerRpc((byte)tea.Slot, ActionIntent.NoTarget, tea.CopyId);
            Check(_human.GetActionQueue().selectedAction.Value.ItemData == chosen.ItemData,
                "second normal human selection cannot replace the queued item");
            _human.CancelSelectionServerRpc();
            Check(!_human.HasSelectedItem.Value && !_human.GetActionQueue().selectedAction.HasValue
                && inventory.CurrentFingerprint() == before, "human pre-Ready cancellation removes only the queue");
        }

        void VerifyHumanMiniGameFailure()
        {
            var copy = EnsureItem(_human.GetInventory(), "Hot Pack");
            int before = Uses(_human.GetInventory(), copy.CopyId);
            int tickets = _humanTickets.Count;
            _human.SelectItemServerRpc((byte)copy.Slot, ActionIntent.NoTarget, copy.CopyId);
            Check(_humanTickets.Count == tickets + 1 && !_human.HasSelectedItem.Value
                && Uses(_human.GetInventory(), copy.CopyId) == before,
                "human mini-game starts a real ticket without queueing or consuming");
            var ticket = _humanTickets.Last();
            SubmitMiniGame(ticket, false);
            Check(!_human.HasSelectedItem.Value && Uses(_human.GetInventory(), copy.CopyId) == before - 1,
                "human mini-game failure consumes exactly one existing use");
            uint fingerprint = _human.GetInventory().CurrentFingerprint();
            SubmitMiniGame(ticket, true);
            Check(!_human.HasSelectedItem.Value && _human.GetInventory().CurrentFingerprint() == fingerprint,
                "replayed failed mini-game ticket cannot queue or consume again");
        }

        PlayerActionCandidate VerifyHumanMiniGameSuccess()
        {
            var copy = EnsureItem(_human.GetInventory(), "Water Gun");
            int before = Uses(_human.GetInventory(), copy.CopyId);
            _human.SelectItemServerRpc((byte)copy.Slot, 1, copy.CopyId);
            var ticket = _humanTickets.Last();
            Check(ticket.CopyId == copy.CopyId && !_human.HasSelectedItem.Value,
                "human success scenario receives the current physical copy ticket");
            _human.SubmitMiniGameResultServerRpc(ticket.Slot, true, ticket.MatchEpoch, ticket.RoundEpoch,
                ticket.Turn, ticket.AttemptId + 1, ticket.CopyId);
            Check(!_human.HasSelectedItem.Value && _human.DebugPendingMiniGameAttemptId == ticket.AttemptId,
                "wrong mini-game attempt does not replace or complete the current ticket");
            SubmitMiniGame(ticket, true);
            Check(_human.HasSelectedItem.Value && Uses(_human.GetInventory(), copy.CopyId) == before,
                "successful human mini-game queues the item without early consumption");
            return new PlayerActionCandidate(_human.Binding, (byte)copy.Slot, copy.CopyId, 1,
                _human.GetInventory().GetItemData(copy.Slot), null);
        }

        PlayerActionCandidate VerifyBotSharedSelection(PrepInputKey key)
        {
            var copy = EnsureItem(_bot.GetInventory(), "Water Gun");
            var inventory = _bot.GetInventory();
            uint before = inventory.CurrentFingerprint();
            _bot.SelectItemServerRpc((byte)copy.Slot, 0, copy.CopyId);
            Check(!_bot.HasSelectedItem.Value && _botMiniGames == 0 && inventory.CurrentFingerprint() == before,
                "server-owned bot cannot use the human owner RPC adapter");
            Check(_human.ServerValidateBotItem(key, copy.CopyId, 1, out _).Status != PlayerActionStatus.Validated,
                "human participant cannot enter the bot shared-command adapter");
            Check(_bot.ServerValidateBotItem(key, copy.CopyId, 1, out _).Status != PlayerActionStatus.Validated,
                "bot attack rejects its own logical seat as target");
            Check(_bot.ServerValidateBotItem(key, uint.MaxValue, 0, out _).Status != PlayerActionStatus.Validated,
                "bot candidate rejects an unknown physical item copy");
            var valid = _bot.ServerValidateBotItem(key, copy.CopyId, 0, out var candidate);
            Check(valid.Status == PlayerActionStatus.Validated && candidate.CopyId == copy.CopyId
                && inventory.CurrentFingerprint() == before && !_bot.HasSelectedItem.Value,
                "shared candidate validation is read-only and retains exact CopyId/target");
            var queued = _bot.ServerQueueBotItem(key, candidate);
            Check(queued.Status == PlayerActionStatus.Queued && _bot.HasSelectedItem.Value
                && inventory.CurrentFingerprint() == before && _botMiniGames == 0,
                "bot shared seam queues a mini-game item without mini-game or debit");
            Check(_bot.ServerQueueBotItem(key, candidate).Status != PlayerActionStatus.Queued
                && inventory.CurrentFingerprint() == before,
                "shared queue refuses a second simultaneous bot selection");
            Check(_bot.ServerCancelBotSelection(key).Status == PlayerActionStatus.Cancelled
                && !_bot.HasSelectedItem.Value && inventory.CurrentFingerprint() == before,
                "bot pre-Ready cancellation mirrors human queue-only cancellation");
            Check(_bot.ServerValidateBotItem(key, copy.CopyId, 0, out candidate).Status == PlayerActionStatus.Validated
                && _bot.ServerQueueBotItem(key, candidate).Status == PlayerActionStatus.Queued,
                "bot can validate and queue again after explicit cancellation");
            return candidate;
        }

        void AttachTurn(TurnManager turn)
        {
            if (ReferenceEquals(_turn, turn)) return;
            if (_turn != null) _turn.PrepInputOpened -= OnPrepOpened;
            _turn = turn;
            _turn.PrepInputOpened += OnPrepOpened;
        }
        void OnPrepOpened(PrepInputSnapshot snapshot)
        {
            _openings.Add(snapshot);
            var match = MatchCompositionRoot.Instance;
            bool reset = snapshot.TemperaturesAtStart.Count == 2 && Enumerable.Range(0, 2).All(seat =>
            {
                var player = _turn.GetPlayer(seat);
                return player != null && !player.IsReady.Value && !player.HasSelectedItem.Value
                    && !player.GetActionQueue().selectedAction.HasValue && player.IsFanActive.Value
                    && snapshot.TemperaturesAtStart[seat] == player.Temperature.Value;
            });
            bool committed = _turn.CurrentPhase.Value == TurnPhase.PrepPhase && _turn.RemainingTime.Value > 0
                && snapshot.Key.Generation == match.Generation && snapshot.Key.Round == match.MatchManager.RoundNumber.Value
                && snapshot.Key.Turn == _turn.TurnNumber.Value && snapshot.RemainingSeconds > 0
                && snapshot.Deadline > snapshot.StartTime && snapshot.TemperaturesAtStart.Count == 2
                && _turn.TryGetPrepInputSnapshot(out var queried) && queried.Key == snapshot.Key;
            _report.openings.Add($"generation={snapshot.Key.Generation}; round={snapshot.Key.Round}; turn={snapshot.Key.Turn}; "
                + $"sequence={snapshot.Key.Sequence}; remaining={snapshot.RemainingSeconds:F4}; reset={reset}; committed={committed}");
            if (!reset || !committed) _report.errors.Add("Prep signal published before reset/time/baseline commit.");
        }
        void OnCombat(CombatResultData result)
        {
            if (_turn == null || _human == null || !_human.IsSpawned) return;
            _combatResults.Add(result);
            _report.combat.Add($"sequence={result.ResultSequence}; turn={_turn.TurnNumber.Value}; events={result.EventCount}; "
                + $"items={result.P1MainItemId},{result.P2MainItemId}; "
                + $"temperatures={result.P1TempBeforeCombat:F2}->{result.P1TempAfterCombat:F2},{result.P2TempBeforeCombat:F2}->{result.P2TempAfterCombat:F2}");
            _human.PresentationAckServerRpc(result.ResultSequence);
        }
        void OnHumanMiniGame(MiniGameTicket ticket) => _humanTickets.Add(ticket);
        void OnBotMiniGame(MiniGameTicket ticket) => _botMiniGames++;
        void SubmitMiniGame(MiniGameTicket ticket, bool success)
            => _human.SubmitMiniGameResultServerRpc(ticket.Slot, success, ticket.MatchEpoch, ticket.RoundEpoch,
                ticket.Turn, ticket.AttemptId, ticket.CopyId);
        (int Slot, uint CopyId) EnsureItem(PlayerInventory inventory, string name)
        {
            short id = ItemId(name);
            bool owned = false;
            foreach (var slot in inventory.SlotStates)
                if (slot.ItemId == id && slot.IsUsable) { owned = true; break; }
            if (!owned)
                Check(inventory.GrantSpecificItem(id), "fixture grants required existing item: " + name);
            return FindUsable(inventory, name);
        }
        (int Slot, uint CopyId) FindUsable(PlayerInventory inventory, string name)
        {
            short id = ItemId(name);
            for (int i = 0; i < inventory.SlotStates.Count; i++)
                if (inventory.SlotStates[i].ItemId == id && inventory.SlotStates[i].IsUsable)
                    return (i, inventory.SlotStates[i].CopyId);
            throw new InvalidOperationException("No usable fixture item: " + name);
        }
        short ItemId(string name)
        {
            int index = Array.FindIndex(Catalog, item => item != null && item.ItemName == name);
            if (index < 0) throw new InvalidOperationException("Current catalog item missing: " + name);
            return (short)index;
        }
        static int Uses(PlayerInventory inventory, uint copyId)
        {
            foreach (var slot in inventory.SlotStates) if (slot.CopyId == copyId && !slot.IsEmpty) return slot.RemainingUses;
            return 0;
        }
        static async Task ReturnToMenu()
        {
            if (SceneManager.GetActiveScene().path == Menu) return;
            var operation = SceneManager.LoadSceneAsync(Menu);
            while (!operation.isDone) await Task.Delay(20);
        }
        static async Task Until(Func<bool> predicate, double seconds, string label)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate())
            {
                if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException(label);
                await Task.Delay(20);
            }
        }
        void Check(bool pass, string label)
        {
            if (!pass) throw new InvalidOperationException(label);
            _report.checks.Add(label);
            Debug.Log("[PLAN036 T05] PASS " + label);
        }
        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _report.errors.Add(message + "\n" + stack);
        }
        void Update()
        {
            if (!_finished && _deadline > 0 && Time.realtimeSinceStartupAsDouble > _deadline)
            { _report.failure = "Shared action fixture watchdog timeout"; Finish(); }
        }
        void Finish()
        {
            if (_finished) return;
            _finished = true;
            Detach();
            File.WriteAllText(_reportPath, JsonUtility.ToJson(_report, true));
            if (!Application.isEditor) Application.Quit(_report.passed ? 0 : 1);
        }
        void Detach()
        {
            TurnManager.DebugBeforeReadyHandoff = null;
            TurnManager.OnCombatResult -= OnCombat;
            if (_turn != null) _turn.PrepInputOpened -= OnPrepOpened;
            if (_human != null) _human.OnMiniGameStart -= OnHumanMiniGame;
            if (_bot != null) _bot.OnMiniGameStart -= OnBotMiniGame;
            Application.logMessageReceived -= OnLog;
        }
        void OnDestroy() { if (_instance == this) _instance = null; Detach(); }
    }
}
#endif
