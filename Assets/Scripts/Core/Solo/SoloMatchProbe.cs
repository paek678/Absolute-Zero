#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AbsoluteZero.Core.Solo
{
    // Normal default encounter + real graph. Scripted human policies select
    // owned items through ordinary commands, exercising real cooling/round flow.
    // No temperature, winner, inventory, round or phase is set by this probe.
    public sealed class SoloMatchProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope = "Natural neutral Solo Bo3 using normal rules and the production BT; exact match/replay counts and seed are recorded separately";
            public List<string> checks = new(), captures = new(), errors = new();
            public int combatCount, roundsOpened, matchesCompleted, replays;
            public int seed, turnsOpened;
            public int matchTimeoutSeconds;
            public string humanPolicy;
            public List<string> humanItems = new();
            public double elapsedSeconds;
            public List<string> configurations = new();
            public string failure;
        }
        readonly Report _report = new();
        string _directory;
        bool _finished, _drive;
        double _deadline;
        PrepInputKey _lastKey;
        long _generation;
        int _lastRound;
        int _cycles = 4;
        double _startedAt;
        System.Random _humanRandom;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--plan036-full-match") >= 0)
                new GameObject("Solo full match validation").AddComponent<SoloMatchProbe>();
        }
        async void Start()
        {
            DontDestroyOnLoad(gameObject); Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "--plan036-output");
            _directory = index >= 0 ? Path.GetFullPath(args[index + 1]) : Path.Combine(Application.persistentDataPath, "solo-match");
            Directory.CreateDirectory(_directory);
            _startedAt = Time.realtimeSinceStartupAsDouble;
            int seedIndex = Array.IndexOf(args, "--plan036-seed");
            _report.seed = seedIndex >= 0 ? int.Parse(args[seedIndex + 1]) : 3609;
            int policyIndex = Array.IndexOf(args, "--plan036-human-policy");
            _report.humanPolicy = policyIndex >= 0 ? args[policyIndex + 1] : "late-fan";
            int timeoutIndex = Array.IndexOf(args, "--plan036-match-timeout");
            _report.matchTimeoutSeconds = timeoutIndex >= 0 ? Mathf.Clamp(int.Parse(args[timeoutIndex + 1]), 30, 600) : 300;
            _humanRandom = new System.Random(_report.seed);
            if (Array.IndexOf(args, "--plan036-corpus") >= 0) _cycles = 1;
            _deadline = _startedAt + _cycles * _report.matchTimeoutSeconds + 120;
            Application.logMessageReceived += Log; TurnManager.OnCombatResult += Combat;
            try { await Run(); _report.passed = _report.errors.Count == 0; }
            catch (Exception error) { _report.failure = error.ToString(); }
            Finish();
        }
        async Task Run()
        {
            await Until(() => AppBootstrapper.Instance?.IsReady == true && Button("SoloBtn") != null, 15, "main lobby");
            var app = AppBootstrapper.Instance;
            string cosmetics = CosmeticProfileService.Instance?.GetCompactDto();
            string nickname = CosmeticProfileService.Instance?.Nickname;
            Check(UnityServices.State == ServicesInitializationState.Uninitialized, "fresh lobby starts without UGS");
            bool captureFramebuffer = Array.IndexOf(Environment.GetCommandLineArgs(), "--plan036-framebuffer-captures") >= 0;
            if (captureFramebuffer) await Capture("initial-lobby");
            UnityEngine.Random.InitState(_report.seed);
            Button("SoloBtn").onClick.Invoke();
            var confirm = GameObject.Find("MainUI/SoloSelectionPanel/Body/StartBtn").GetComponent<Button>();
            Check(confirm.interactable && app.SessionRouter.Current == null, "Solo profile preview waits for explicit Start");
            if (captureFramebuffer) await Capture("solo-profile-selection");
            confirm.onClick.Invoke();
            AbsoluteZero.Core.Solo.Configuration.ResolvedSoloSettings firstSettings = null;
            for (int cycle = 0; cycle < _cycles; cycle++)
            {
                await Until(() => app.SessionRouter.Current != null && app.SessionRouter.Current.Generation > _generation
                    && TurnManager.Instance != null && TurnManager.Instance.TryGetPrepInputSnapshot(out _), 40, "new match Prep");
                _generation = app.SessionRouter.Current.Generation; _lastRound = 0;
                if (cycle == 0) firstSettings = app.SessionRouter.LaunchContext.Solo;
                else Check(ReferenceEquals(firstSettings, app.SessionRouter.LaunchContext.Solo), "replay " + cycle + " retains captured profile and versions");
                // TurnManager clears static events on destruction, so subscribe to
                // each new match lifetime rather than trusting the first subscription.
                TurnManager.OnCombatResult -= Combat; TurnManager.OnCombatResult += Combat;
                foreach (var stamp in app.SessionRouter.LaunchContext.Solo.ConfigurationVersions)
                    _report.configurations.Add(stamp.Kind + ":" + stamp.Id + "@" + stamp.Version);
                var match = MatchCompositionRoot.Instance; var manager = match.MatchManager;
                var bot = TurnManager.Instance.GetPlayer(1); var human = TurnManager.Instance.GetPlayer(0);
                var controller = FindAnyObjectByType<BotTurnController>();
                Check(manager.RoundNumber.Value == 1 && manager.P1RoundWins.Value == 0 && manager.P2RoundWins.Value == 0,
                    "cycle " + cycle + " starts with cleared round wins");
                Check(TurnManager.Instance.TryGetPrepInputSnapshot(out var start)
                    && start.TemperaturesAtStart.All(x => Math.Abs(x - 37) < .001), "cycle " + cycle + " neutral temperature reset");
                Check(match.Registry.ReadyCount == 2 && NetworkManager.Singleton.ConnectedClientsIds.Count == 1
                    && FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length == 1
                    && FindObjectsByType<BotTurnController>(FindObjectsSortMode.None).Length == 1, "cycle " + cycle + " clean two-seat/one-connection/one-controller composition");
                Check(cosmetics == CosmeticProfileService.Instance?.GetCompactDto() && nickname == CosmeticProfileService.Instance?.Nickname,
                    "cycle " + cycle + " preserves local cosmetics and nickname");
                await Until(() => human.CosmeticSubmission is CosmeticSubmissionStatus.Accepted or CosmeticSubmissionStatus.AcceptedLate
                    && !bot.CosmeticDataNV.Value.IsEmpty, 12, "human and bot appearance accepted");
                Check(CosmeticCodec.TryParse(cosmetics, out var expectedHuman)
                    && CosmeticCodec.TryParse(human.CosmeticDataNV.Value.ToString(), out var acceptedHuman)
                    && CosmeticCodec.SameIds(expectedHuman, acceptedHuman), "cycle " + cycle + " human appearance matches frozen profile");
                Check(AbsoluteZero.Core.Player.PlayerState.TrySerializeBotCosmetics(app.SessionRouter.LaunchContext.Solo.Cosmetics, out var expectedBot)
                    && bot.CosmeticDataNV.Value.Equals(expectedBot), "cycle " + cycle + " bot appearance uses resolved bot config");
                if (captureFramebuffer)
                {
                    await Until(() => !FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                        .Any(canvas => canvas.name == "LoadingScreenCanvas" && canvas.isActiveAndEnabled),
                        10, "loading overlay dismissed before gameplay capture");
                    await Capture("cycle" + cycle + "-prep");
                }
                _drive = true;
                await Until(() => manager != null && manager.CurrentMatchState.Value == MatchState.MatchComplete, _report.matchTimeoutSeconds, "natural Bo3 completion");
                _drive = false; _report.matchesCompleted++;
                Check(manager.P1RoundWins.Value == 2 || manager.P2RoundWins.Value == 2, "cycle " + cycle + " completes by two real round wins");
                Check(manager.RoundNumber.Value >= 2 && controller.RunCount >= 2 && controller.SelectionCount >= 2,
                    "cycle " + cycle + " graph and normal combat persist across rounds");
                await Until(() => Button("RematchBtn")?.interactable == true && Button("LobbyBtn")?.interactable == true, 10, "Solo result buttons");
                await Capture("cycle" + cycle + "-result");
                if (cycle == 0 && _cycles > 1)
                {
                    int runs = controller.RunCount;
                    for (int second = 0; second < 31; second++)
                    {
                        await Task.Delay(1000);
                        Check(manager != null && manager.CurrentMatchState.Value == MatchState.MatchComplete
                            && manager.RematchVoteEpoch.Value == 0 && SceneManager.GetActiveScene().name == "GameScene_Solo",
                            "idle result second " + (second + 1) + " retains Solo without vote/auto-return");
                    }
                    Check(!controller.IsRunning && controller.RunCount == runs, "completed match has no active/new bot run");
                    await Capture("cycle0-result-after31seconds");
                }
                File.WriteAllLines(Path.Combine(_directory, "cycle" + cycle + "-bot-trace.txt"), controller.Trace);
                if (cycle < _cycles - 1)
                {
                    var replay = Button("RematchBtn"); replay.onClick.Invoke(); replay.onClick.Invoke();
                    await Until(() => manager == null && bot == null && human == null, 20, "old match teardown");
                    _report.replays++;
                    Check(app == AppBootstrapper.Instance, "replay " + (cycle + 1) + " preserves only persistent app");
                }
                else
                {
                    Button("LobbyBtn").onClick.Invoke();
                    await Until(() => SceneManager.GetActiveScene().name == "LobbyScene" && app.SessionRouter.Current == null, 20, "explicit result exit");
                    await Until(() => Button("SoloBtn")?.interactable == true, 5, "restored menu");
                    Check(!NetworkManager.Singleton.IsListening && FindObjectsByType<BotTurnController>(FindObjectsSortMode.None).Length == 0,
                        "final exit stops network and removes graph controller");
                    await Capture("final-lobby");
                }
            }
            Check(UnityServices.State == ServicesInitializationState.Uninitialized, "all natural matches and replays never initialize UGS");
        }
        void Update()
        {
            if (_finished) return;
            if (_deadline > 0 && Time.realtimeSinceStartupAsDouble > _deadline) { _report.failure = "global timeout"; Finish(); return; }
            if (!_drive || TurnManager.Instance == null || !TurnManager.Instance.TryGetPrepInputSnapshot(out var snapshot)) return;
            var turn = TurnManager.Instance; var human = turn.GetPlayer(0);
            if (human == null || human.IsReady.Value || human.Temperature.Value <= 0) return;
            if (_lastKey != snapshot.Key)
            {
                _lastKey = snapshot.Key;
                _report.turnsOpened++;
                if (_report.turnsOpened > 200) { _report.failure = "bounded-turn watchdog"; Finish(); return; }
                if (_lastRound != snapshot.Key.Round)
                { _lastRound = snapshot.Key.Round; _report.roundsOpened++; Debug.Log("[SoloMatchProbe] generation=" + _generation + " round=" + _lastRound); }
                var inventory = human.GetInventory();
                var available = new List<byte>();
                for (byte slot = 0; slot < inventory.SlotStates.Count; slot++)
                {
                    var item = inventory.GetItemData(slot);
                    if (item == null || !inventory.SlotStates[slot].IsUsable || item.RequiresMiniGame) continue;
                    if (_report.humanPolicy == "mixed" || item.ItemName == "Fan") available.Add(slot);
                }
                if (available.Count > 0)
                {
                    byte slot = available[_humanRandom.Next(available.Count)];
                    var item = inventory.GetItemData(slot);
                    byte target = item.GetTargetMode() == TargetMode.Self ? (byte)0 : (byte)1;
                    human.SelectItemServerRpc(slot, target, inventory.SlotStates[slot].CopyId);
                    _report.humanItems.Add(item.ItemName + ":" + (human.HasSelectedItem.Value ? "queued" : "rejected"));
                }
            }
            if (snapshot.RemainingSeconds < 0.3 || human.Temperature.Value <= 3
                || (_report.humanPolicy == "mixed" && NetworkManager.Singleton.ServerTime.Time >= snapshot.StartTime + 2)) human.PressReadyServerRpc();
        }
        static Button Button(string name) => FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(x => x.name == name && x.gameObject.activeInHierarchy);
        async Task Until(Func<bool> condition, double seconds, string label)
        { double deadline = Time.realtimeSinceStartupAsDouble + seconds; while (!condition()) { if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(label); await Task.Delay(25); } }
        Task Capture(string name) { var source = new TaskCompletionSource<bool>(); StartCoroutine(Frame(name, source)); return source.Task; }
        IEnumerator Frame(string name, TaskCompletionSource<bool> source)
        {
            yield return new WaitForEndOfFrame();
            try { string path = Path.Combine(_directory, name + ".png"); SoloLaunchProbe.WriteRender(path); _report.captures.Add(path); source.SetResult(true); }
            catch (Exception error) { source.SetException(error); }
        }
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); _report.checks.Add(message); }
        void Combat(CombatResultData result) => _report.combatCount++;
        void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) _report.errors.Add(message); }
        void Finish()
        {
            if (_finished) return; _finished = true; _drive = false;
            Application.logMessageReceived -= Log; TurnManager.OnCombatResult -= Combat;
            _report.elapsedSeconds = Time.realtimeSinceStartupAsDouble - _startedAt;
            File.WriteAllText(Path.Combine(_directory, "report.json"), JsonUtility.ToJson(_report, true));
            Application.Quit(_report.passed ? 0 : 2);
        }
    }
}
#endif
