#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.Validation.Actions
{
    public sealed partial class Plan036ActionProbe
    {
        // Explicit runtime fixture: the shipped lobby, scene, graph, controller,
        // NGO clock and command adapter run normally. Only initial public state
        // and inventories are controlled, using the existing fixture access.
        // No leaf replacement, frame-stamp override or alternate phase driver.
        static readonly string[] IntegrationCases = {
            "crisis-heal", "crisis-defense", "advanced-lethal", "beginner-choice",
            "no-care-ready", "retry-exhaustion", "pending-early-ready",
            "mini-failure", "copy-compaction", "deadline", "active-fan-delay",
            "active-fan-no-care", "pending-target-death", "pending-owner-death", "pending-session-exit"
        };
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        BotTurnController _integrationController;
        string _integrationCase;
        bool _integrationConfigured;
        int _integrationSessions;
        bool _beginnerAlternativeObserved;
        PrepInputKey _integrationKey;
        uint _integrationCopy;
        int _integrationUses, _integrationSlot;
        float _integrationHumanTemperature, _integrationBotTemperature;
        TrustedBotCommandAdapter _stoppedIntegrationAdapter;
        PrepInputKey _stoppedIntegrationKey;
        ulong _stoppedIntegrationOperation;
        bool _integrationPresentation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallSoloIntegrationFixture()
        {
            if (!Environment.GetCommandLineArgs().Contains("--solo-integration-fixture")) return;
            if (FindAnyObjectByType<Plan036ActionProbe>() == null)
                new GameObject("Explicit integrated Solo action fixture").AddComponent<Plan036ActionProbe>();
        }

        static object ReadField(object target, string name)
            => target.GetType().GetField(name, Hidden)?.GetValue(target)
               ?? throw new InvalidOperationException("Fixture field contract missing: " + name);

        async Task RunSoloIntegration()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "--solo-integration-case");
            _integrationCase = option >= 0 && option + 1 < args.Length ? args[option + 1] : "";
            _integrationPresentation = args.Contains("--solo-integration-presentation");
            Check(IntegrationCases.Contains(_integrationCase), "known explicit integration case: " + _integrationCase);
            _integrationSessions++;
            Check(!args.Contains("--az-playtest") && !args.Contains("--plan036-bt-run"),
                "integration fixture has no competing automatic input driver");
            _report.scope = "I05 unmocked production graph/session; explicit controlled initial state; case=" + _integrationCase;
            await Until(() => AppBootstrapper.Instance?.IsReady == true
                && GameObject.Find("MainUI/MainPanel/SoloBtn") != null, 20, "production lobby ready");
            var app = AppBootstrapper.Instance;
            var lobby = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(x => x.GetType().FullName == "AbsoluteZero.UI.LobbyUI.AZLobbyUI");
            Catalog = (AbsoluteZero.Core.Item.Data.ItemDataSO[])ReadField(lobby, "_soloCatalog");
            string requested = _integrationCase == "beginner-choice" ? "solo.encounter.beginner" : "solo.encounter.advanced";
            if (args.Contains("--solo-integration-switch-profile") && _integrationSessions > 1)
                requested = "solo.encounter.beginner";
            // Read-only discovery in this fixture. The later I06 production test
            // bridge must provide its own validated contract and negative cases.
            var presenter = ReadField(lobby, "_soloPresenter");
            var profiles = (Array)ReadField(presenter, "_profiles");
            var matches = Enumerable.Range(0, profiles.Length).Where(i =>
            {
                var entry = profiles.GetValue(i);
                var encounter = (SoloDuelDefinitionSO)entry.GetType().GetField("Encounter").GetValue(entry);
                return encounter != null && encounter.ConfigId == requested;
            }).ToArray();
            Check(matches.Length == 1, "fixture finds one filtered profile ID: " + requested);
            TurnManager.DebugBeforeReadyHandoff = _ =>
            {
                // Disable before the committed signal, so subscription order
                // cannot let the production graph start before fixture inputs.
                FindObjectsByType<BotTurnController>(FindObjectsSortMode.None).Single().enabled = false;
                AttachTurn(TurnManager.Instance);
                _turn.PrepInputOpened += ConfigureIntegrationCase;
            };
            try
            {
                ClickIntegrationButton("MainUI/MainPanel/SoloBtn");
                if (args.Contains("--solo-integration-selection-bridge"))
                {
                    var bridge = lobby as ISoloPlaytestSelection;
                    Check(bridge != null, "actual normal menu exposes the development selection contract");
                    Check(!bridge.TrySelectEncounter("unknown.encounter", out _, out _)
                        && app.SessionRouter.Current == null, "unknown explicit ID cannot launch or fall back");
                    Check(bridge.TrySelectEncounter(requested, out var difficulty, out var error),
                        "actual normal option event selects the requested ID: " + error);
                    string expected = requested == "solo.encounter.beginner" ? "solo.difficulty.beginner" : "solo.difficulty.advanced";
                    Check(difficulty == expected, "actual menu ID exposes the expected difficulty");
                }
                else
                {
                    var optionButton = GameObject.Find("MainUI/SoloSelectionPanel")
                        .GetComponentsInChildren<Button>(false).Single(x => x.name == "Profile" + matches[0]);
                    Check(optionButton.interactable, "filtered profile's actual view option is selectable");
                    optionButton.onClick.Invoke();
                }
                ClickIntegrationButton("MainUI/SoloSelectionPanel/Body/StartBtn");
                await Until(() => _integrationConfigured, 50, "committed production Prep and fixture state");
                TurnManager.DebugBeforeReadyHandoff = null;
                var settings = app.SessionRouter.LaunchContext.Solo;
                Check(settings.EncounterId == requested && !settings.IsValidationFixture,
                    "normal development launch resolves requested production encounter");
                if (args.Contains("--solo-integration-switch-profile"))
                {
                    var profile = PlaytestSoloEvidence.Capture(settings, requested,
                        requested == "solo.encounter.beginner" ? "solo.difficulty.beginner" : "solo.difficulty.advanced",
                        _integrationKey.Generation, _report.seed);
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(_reportPath),
                        "solo-profile-g" + _integrationKey.Generation + ".json"), JsonUtility.ToJson(profile, true));
                    if (_integrationSessions > 1)
                        Check(settings.DifficultyId == "solo.difficulty.beginner" && settings.ExtraItemDelaySeconds > 0,
                            "normal re-entry switches from Advanced to the Beginner settings and graph owner");
                }
                Check(NetworkManager.Singleton.ConnectedClientsIds.Count == 1
                    && MatchCompositionRoot.Instance.Registry.ReadyCount == 2,
                    "real local host has two distinct live participant bindings");
                var localTransport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
                Check(localTransport != null && localTransport.ConnectionData.Address == "127.0.0.1"
                    && localTransport.ConnectionData.Port == AppBootstrapper.ResolveDevelopmentSoloPort(args),
                    "actual loopback transport uses the requested development port");
                Check(_integrationController.RuntimeGraph == null || _integrationController.RuntimeGraph != settings.Graph,
                    "production graph is instantiated rather than mutated");
                if (_integrationCase == "deadline") await VerifyIntegrationDeadline();
                else
                {
                    _integrationController.enabled = true;
                    if (_integrationCase is "pending-early-ready" or "copy-compaction" or "active-fan-delay"
                        or "pending-target-death" or "pending-owner-death" or "pending-session-exit")
                    {
                        await Until(() => _bot.GetBotCommands().HasPending, 4, "actual graph admitted pending operation");
                        Check(!_bot.IsReady.Value && !_bot.HasSelectedItem.Value,
                            "pending graph operation cannot Ready or apply its item early");
                        if (_integrationCase == "copy-compaction")
                        {
                            _bot.GetInventory().CompactSlots();
                            Check(FindUsable(_bot.GetInventory(), "Water Gun").Slot != _integrationSlot,
                                "controlled compaction actually moved the selected copy");
                        }
                        else if (_integrationCase == "pending-early-ready")
                        {
                            var before = NetworkManager.Singleton.ServerTime.Time;
                            var due = _integrationController.Trace.Last(x => x.Contains("submit Pending"));
                            _human.SelectItemServerRpc((byte)FindUsable(_human.GetInventory(), "Fan").Slot,
                                1, FindUsable(_human.GetInventory(), "Fan").CopyId);
                            _human.PressReadyServerRpc();
                            Check(_human.IsReady.Value, "opponent Ready uses the ordinary human RPC");
                            await Until(() => _bot.IsReady.Value, 1.5, "pending delay shortened by opponent Ready");
                            Check(NetworkManager.Singleton.ServerTime.Time - before < 1.5,
                                "existing operation completes early with real elapsed time: " + due);
                        }
                    }
                    if (_integrationCase is "pending-target-death" or "pending-owner-death")
                        await VerifyIntegrationDeathCancellation();
                    else if (_integrationCase == "pending-session-exit" && _integrationSessions == 1)
                    {
                        _stoppedIntegrationAdapter = _bot.GetBotCommands();
                        _stoppedIntegrationKey = _integrationKey;
                        _stoppedIntegrationOperation = (ulong)ReadField(_integrationController, "_operation");
                        Check(_stoppedIntegrationAdapter.HasPending, "normal session exit begins with genuinely pending graph work");
                    }
                    else
                    {
                        if (_integrationCase == "pending-session-exit")
                        {
                            uint current = _bot.GetInventory().CurrentFingerprint();
                            Check(_integrationKey.Generation != _stoppedIntegrationKey.Generation
                                && !ReferenceEquals(_bot.GetBotCommands(), _stoppedIntegrationAdapter),
                                "normal re-entry creates a fresh binding and operation owner");
                            Check(_stoppedIntegrationAdapter.Poll(_stoppedIntegrationKey, _stoppedIntegrationOperation).Status == PlayerActionStatus.Stale
                                && _stoppedIntegrationAdapter.Ready(_stoppedIntegrationKey).Status == PlayerActionStatus.Stale
                                && _bot.GetInventory().CurrentFingerprint() == current && !_bot.IsReady.Value,
                                "old session poll and Ready cannot mutate the new participant");
                        }
                        await Until(() => _bot.IsReady.Value, 10, "real graph terminates with authoritative Ready");
                        VerifyIntegrationDecision(settings);
                        MarkIntegrationVideo("first controlled Prep assertions passed");
                        if (_integrationPresentation) await ObserveIntegrationPresentation();
                    }
                }
            }
            finally
            {
                // Keep the actual graph trace even if a later presentation
                // assertion fails; every generation gets its own evidence file.
                if (_integrationController != null && _integrationKey.Generation > 0)
                    File.WriteAllLines(Path.Combine(Path.GetDirectoryName(_reportPath),
                        _integrationCase + "-g" + _integrationKey.Generation + "-bot-trace.txt"),
                        _integrationController.Trace);
                MarkIntegrationVideo("normal session stopping");
                TurnManager.DebugBeforeReadyHandoff = null;
                if (_turn != null) _turn.PrepInputOpened -= ConfigureIntegrationCase;
                await app.SessionRouter.StopAsync();
            }
            await Until(() => app.SessionRouter.Current == null && !NetworkManager.Singleton.IsListening,
                15, "fixture session releases its host");
            Check(_integrationController == null && SceneManagerName() == "LobbyScene",
                "controller and graph are released on normal lobby return");
            if (_integrationCase == "pending-session-exit")
                Check(!_stoppedIntegrationAdapter.HasPending
                    && _stoppedIntegrationAdapter.Poll(_stoppedIntegrationKey, _stoppedIntegrationOperation).Status == PlayerActionStatus.Stale,
                    "normal session disposal cancels pending work and invalidates its callbacks");
            if ((_integrationCase == "beginner-choice" && !_beginnerAlternativeObserved)
                || (_integrationCase == "pending-session-exit" && _integrationSessions == 1))
            {
                // A predefined, bounded re-entry matrix supplies real distinct
                // session keys. Keep every trace; never alter the odds or seed.
                Check(_integrationSessions < 16, "bounded normal session re-entry matrix");
                _integrationConfigured = false; _integrationController = null;
                _human = null; _bot = null; _turn = null;
                await RunSoloIntegration();
            }
            else if (_integrationCase == "beginner-choice")
                Check(_beginnerAlternativeObserved, "beginner actually queued a nonlethal alternative while a lethal item was available");
        }

        static string SceneManagerName() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        void ClickIntegrationButton(string path)
        {
            var button = GameObject.Find(path)?.GetComponent<Button>();
            Check(button != null && button.interactable && button.gameObject.activeInHierarchy,
                "real visible/interactable button: " + path);
            button.onClick.Invoke();
        }

        void ConfigureIntegrationCase(PrepInputSnapshot snapshot)
        {
            if (_integrationConfigured) return;
            _integrationController = FindObjectsByType<BotTurnController>(FindObjectsSortMode.None).Single();
            Check(_integrationController.RunCount == 0, "initial fixture state is applied before any graph choice");
            _integrationController.enabled = false;
            _human = _turn.GetPlayer(0); _bot = _turn.GetPlayer(1);
            _integrationKey = snapshot.Key;
            _human.OnMiniGameStart += OnHumanMiniGame; _bot.OnMiniGameStart += OnBotMiniGame;
            _human.GetInventory().SlotStates.Clear(); _bot.GetInventory().SlotStates.Clear();
            _human.Temperature.Value = 30; _bot.Temperature.Value = 30;
            _human.IsFanActive.Value = false; _bot.IsFanActive.Value = false;
            EnsureItem(_human.GetInventory(), "Fan");
            switch (_integrationCase)
            {
                case "crisis-heal":
                    _bot.Temperature.Value = 5; _human.Temperature.Value = 1;
                    _human.GetInventory().SlotStates.Clear(); EnsureItem(_human.GetInventory(), "Water Gun");
                    EnsureItem(_bot.GetInventory(), "Fan"); EnsureItem(_bot.GetInventory(), "Warm Tea");
                    break;
                case "crisis-defense":
                    _bot.Temperature.Value = 1; _human.Temperature.Value = 1;
                    EnsureItem(_bot.GetInventory(), "Fan"); EnsureItem(_bot.GetInventory(), "Windbreaker");
                    break;
                case "advanced-lethal": case "beginner-choice":
                    _human.Temperature.Value = 2;
                    EnsureItem(_bot.GetInventory(), "Fan"); EnsureItem(_bot.GetInventory(), "Warm Tea");
                    _bot.Temperature.Value = 20;
                    break;
                case "no-care-ready":
                    _bot.Temperature.Value = 1; _human.Temperature.Value = 1;
                    EnsureItem(_bot.GetInventory(), "Fan");
                    break;
                case "retry-exhaustion": break;
                default:
                    if (_integrationCase == "mini-failure")
                        _human.Temperature.Value = _bot.Temperature.Value = 37;
                    var slot = EnsureItem(_bot.GetInventory(), "Water Gun");
                    _integrationCopy = slot.CopyId; _integrationSlot = slot.Slot;
                    _integrationUses = Uses(_bot.GetInventory(), slot.CopyId);
                    if (_integrationCase == "copy-compaction")
                    {
                        _bot.GetInventory().SlotStates.Insert(0, ItemSlotNetData.Empty);
                        _integrationSlot++;
                    }
                    if (_integrationCase is "active-fan-delay" or "active-fan-no-care")
                    {
                        _bot.IsFanActive.Value = true; _bot.FanSpeed.Value = 3;
                        _bot.Temperature.Value = _integrationCase == "active-fan-delay" ? 37 : 5;
                    }
                    // Deterministic fixture request identity is fixed before admission.
                    // This neither retries an accepted roll nor changes production odds.
                    var settings = AppBootstrapper.Instance.SessionRouter.LaunchContext.Solo;
                    settings.TryGetMiniGameSuccess(ItemId("Water Gun"), out float chance);
                    bool success = _integrationCase != "mini-failure";
                    ulong request = 1;
                    while ((new System.Random(unchecked(BotDecisionModel.Seed(settings.BotId, snapshot.Key)
                        ^ (int)(request * 2654435761UL))).NextDouble() < chance) != success)
                    {
                        if (++request > 1024) throw new InvalidOperationException("Fixture outcome not available within bounded request identities");
                    }
                    typeof(BotTurnController).GetField("_request", Hidden).SetValue(_integrationController, request - 1);
                    _report.checks.Add("controlled pre-admission request identity=" + request + ";expectedSuccess=" + success);
                    break;
            }
            _integrationHumanTemperature = _human.Temperature.Value;
            _integrationBotTemperature = _bot.Temperature.Value;
            _report.checks.Add("fixture state: case=" + _integrationCase + ";Prep=" + snapshot.Key
                + ";human=" + _integrationHumanTemperature + ";bot=" + _integrationBotTemperature);
            _integrationConfigured = true;
            MarkIntegrationVideo("controlled Prep configured");
        }

        void VerifyIntegrationDecision(ResolvedSoloSettings settings)
        {
            var trace = _integrationController.Trace.ToArray();
            Check(_integrationController.ReadyCount == 1 && _integrationController.RunCount == 1,
                "one graph run accepts exactly one Ready");
            Check(_botMiniGames == 0, "bot delay/outcome never starts human mini-game UI");
            Check(_integrationController.RuntimeGraph != null && _integrationController.RuntimeGraph != settings.Graph,
                "saved production graph runs through an isolated runtime instance");
            var queued = _bot.GetActionQueue().selectedAction;
            switch (_integrationCase)
            {
                case "crisis-heal": case "crisis-defense":
                    string care = _integrationCase == "crisis-heal" ? "Warm Tea" : "Windbreaker";
                    Check(trace.Any(x => x.Contains("score crisis=True")) && trace.Any(x => x.Contains("crisis copy="))
                        && !trace.Any(x => x.Contains("lethal copy=")) && queued.HasValue && queued.Value.ItemData.ItemName == care,
                        "actual crisis guard selects " + care + " before available lethal attack");
                    break;
                case "advanced-lethal":
                    Check(trace.Any(x => x.Contains("lethal copy=")) && queued.HasValue && queued.Value.ItemData.ItemName == "Fan",
                        "safe advanced graph selects the lethal item");
                    break;
                case "beginner-choice":
                    Check(settings.Judgement.MayMissLethal && trace.Any(x => x.Contains("odds copy="))
                        && !trace.Any(x => x.Contains("lethal copy=")),
                        "beginner graph uses weighted choice instead of mandatory lethal priority");
                    _beginnerAlternativeObserved |= queued.HasValue && queued.Value.ItemData.ItemName != "Fan";
                    _report.checks.Add("beginner selected=" + (queued.HasValue ? queued.Value.ItemData.ItemName : "none"));
                    break;
                case "no-care-ready": case "active-fan-no-care":
                    Check(trace.Any(x => x.Contains("crisis without eligible care")) && !queued.HasValue
                        && _integrationController.SelectionCount == 0 && !trace.Any(x => x.Contains("submit Pending")),
                        "crisis without a response ends with no-item Ready, not an attack");
                    if (_integrationCase == "active-fan-no-care")
                        Check(_bot.Temperature.Value > 0 && !_bot.IsFanActive.Value,
                            "real active-fan survival bound chooses Ready before a fatal item delay");
                    break;
                case "retry-exhaustion":
                    Check(trace.Count(x => x.Contains("legal candidates=0")) == settings.RetryBudget + 1
                        && trace.Count(x => x.Contains("fallback without new item")) == 1 && !queued.HasValue,
                        "real open authoritative window exhausts exactly the configured retry budget");
                    break;
                case "mini-failure":
                    Check(_integrationController.MiniGameFailureCount == 1 && !queued.HasValue
                        && _integrationController.SelectionCount == 0
                        && Uses(_bot.GetInventory(), _integrationCopy) == _integrationUses - 1,
                        "real bot mini-game failure consumes once, applies no item and then readies");
                    var adapter = _bot.GetBotCommands();
                    ulong operation = (ulong)ReadField(_integrationController, "_operation");
                    var failed = adapter.Poll(_integrationKey, operation);
                    uint fingerprint = _bot.GetInventory().CurrentFingerprint();
                    var repeated = adapter.BeginUse(_integrationKey, operation, _integrationCopy, 0);
                    Check(failed.Reason == PlayerActionReason.MiniGameFailed && repeated.Reason == failed.Reason
                        && adapter.Poll(_integrationKey, operation).Reason == failed.Reason
                        && _bot.GetInventory().CurrentFingerprint() == fingerprint
                        && _human.Temperature.Value == _integrationHumanTemperature
                        && _bot.Temperature.Value == _integrationBotTemperature,
                        "failure replay and polling neither reroll, debit again nor change temperature");
                    break;
                default:
                    Check(queued.HasValue && queued.Value.CopyId == _integrationCopy
                        && trace.Count(x => x.Contains("submit Pending")) == 1,
                        "one admitted operation queues the same physical copy");
                    if (_integrationCase == "copy-compaction")
                        Check(FindUsable(_bot.GetInventory(), "Water Gun").Slot == queued.Value.SlotIndex,
                            "actual command resolves the compacted copy's current slot");
                    if (_integrationCase == "active-fan-delay")
                        Check(_bot.Temperature.Value < _integrationBotTemperature && _bot.Temperature.Value > 0
                            && !_bot.IsFanActive.Value && !_human.IsReady.Value,
                            "real Prep fan ticks cool during the admitted delay and Ready stops the fan");
                    break;
            }
        }

        async Task VerifyIntegrationDeathCancellation()
        {
            var adapter = _bot.GetBotCommands();
            ulong operation = (ulong)ReadField(_integrationController, "_operation");
            uint before = _bot.GetInventory().CurrentFingerprint();
            int readies = _integrationController.ReadyCount;
            if (_integrationCase == "pending-owner-death") _bot.Temperature.Value = 0;
            else _human.Temperature.Value = 0;
            var result = adapter.Poll(_integrationKey, operation);
            Check(result.Status is PlayerActionStatus.Cancelled or PlayerActionStatus.Stale
                && !adapter.HasPending && !_bot.HasSelectedItem.Value
                && _bot.GetInventory().CurrentFingerprint() == before,
                "pending operation cancels on actual participant death without queue or debit");
            await Until(() => !_integrationController.IsRunning, 2, "production graph stops after participant death");
            Check(_integrationController.ReadyCount == readies
                && adapter.Poll(_integrationKey, operation).Status != PlayerActionStatus.Queued
                && _bot.GetInventory().CurrentFingerprint() == before,
                "dead participant cannot cause a fallback Ready, replay or second debit");
        }

        async Task ObserveIntegrationPresentation()
        {
            // Decision assertions above concern the first controlled Prep. The
            // optional video then observes ordinary combat and its real view ACK.
            // It is a controlled demonstration, not a natural-match certificate.
            int before = _combatResults.Count;
            MarkIntegrationVideo("normal combat requested");
            if (!_human.IsReady.Value) _human.PressReadyServerRpc();
            await Until(() => _combatResults.Count > before, 25, "normal combat presentation starts for recorded scenario");
            if (_integrationCase == "pending-session-exit")
                Check(_integrationSessions == 2 && _turn.GetPlayer(0) == _human,
                    "re-entry observes ordinary combat on the current session");
            MarkIntegrationVideo("normal combat received");
            await Task.Delay(7000);
            Check(_report.errors.Count == 0, "recorded ordinary presentation has no runtime error");
            MarkIntegrationVideo("normal presentation observed");
        }

        void MarkIntegrationVideo(string label)
        {
            if (!_integrationPresentation) return;
            string entry = Time.unscaledTimeAsDouble.ToString("F6", System.Globalization.CultureInfo.InvariantCulture)
                + "\t" + _integrationCase + "\t" + _integrationKey.Generation + "/" + _integrationKey.Round
                + "/" + _integrationKey.Turn + "/" + _integrationKey.Sequence + "\t" + label + "\n";
            File.AppendAllText(Path.Combine(Path.GetDirectoryName(_reportPath), "video-timeline.tsv"), entry);
        }

        async Task VerifyIntegrationDeadline()
        {
            var adapter = _bot.GetBotCommands();
            await Until(() => _turn.TryGetPrepInputSnapshot(out var s) && s.RemainingSeconds < 1,
                30, "actual preparation deadline approaches");
            var begin = adapter.BeginUse(_integrationKey, 900, _integrationCopy, 0);
            Check(begin.Status == PlayerActionStatus.Pending, "late but current request admits before the real deadline");
            uint fingerprint = _bot.GetInventory().CurrentFingerprint();
            await Until(() => adapter.Poll(_integrationKey, 900).Status != PlayerActionStatus.Pending,
                3, "actual deadline cancels pending work");
            var result = adapter.Poll(_integrationKey, 900);
            Check(result.Status is PlayerActionStatus.Cancelled or PlayerActionStatus.Stale
                && !_bot.HasSelectedItem.Value && _bot.GetInventory().CurrentFingerprint() == fingerprint,
                "closed real input window cannot queue or debit the late item");
        }
    }
}
#endif
