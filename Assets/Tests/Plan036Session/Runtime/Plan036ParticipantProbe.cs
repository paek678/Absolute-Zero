#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Validation.Solo
{
    public sealed class Plan036ParticipantProbe : MonoBehaviour
    {
        public SoloDuelDefinitionSO[] Encounters;
        public ItemDataSO[] Catalog;
        public CosmeticRegistrySO Cosmetics;
        public GameObject PlayerPrefab;
        public PlayerSpawnManager Spawner;
        const string Menu = "Assets/Tests/Plan036Session/ParticipantFixture.unity";
        static Plan036ParticipantProbe _instance;
        readonly Report _report = new();
        bool _finished;
        bool _expectGrantFailure;
        bool _sawGrantFailure;
        readonly string _grantFailureMessage = "Injected initial grant failure after seat 0: " + new string('한', 200);
        double _deadline;

        [Serializable] sealed class Report
        {
            public bool passed;
            public List<string> checks = new();
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
            _deadline = Time.realtimeSinceStartupAsDouble + 150;
            Application.logMessageReceived += OnLog;
            try { await Run(); _report.passed = _report.errors.Count == 0; }
            catch (Exception error) { _report.failure = error.ToString(); }
            Finish();
        }

        async Task Run()
        {
            var root = new GameObject("T03 application fixture");
            DontDestroyOnLoad(root);
            var nm = root.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig { EnableSceneManagement = true };
            var transport = root.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.PlayerPrefab = PlayerPrefab;
            nm.AddNetworkPrefab(PlayerPrefab);
            var online = root.AddComponent<NetworkSessionCoordinator>();
            var app = root.AddComponent<AppBootstrapper>();
            await Until(() => app.IsReady, 5, "local application readiness");
            var router = app.SessionRouter;
            var solo = new SoloSessionCoordinator(router, new NgoLocalHostRuntime(nm), 18780, ReturnToMenu);
            int spawnerId = Spawner.GetInstanceID();
            NetworkSceneManager previousSceneManager = null;
            PlayerBinding[] previousBindings = null;
            Action<ulong, string, LoadSceneMode> oldSceneCallback = null;
            for (int scenario = 0; scenario < 3; scenario++)
            {
                int current = scenario;
                var encounter = Encounters[scenario % 2];
                var context = new SoloConfigurationContext(encounter.SharedOneVsOneRule, Catalog, Cosmetics);
                Spawner.DebugReverseSoloSpawnOrder = scenario == 2;
                TurnManager.DebugBeforeReadyHandoff = mcr =>
                {
                    Check(mcr.Registry.ReadyCount == 2 && mcr.WritableRegistry.PendingCount == 0,
                        $"case {current}: both bindings promoted before discovery consumes them");
                    if (current == 1)
                    {
                        mcr.Registry.TryGetByPlayerIndex(1, out var bot);
                        mcr.WritableRegistry.Unregister(bot);
                        Check(mcr.WritableRegistry.RegisterPending(bot), "mixed case returns exact bot binding to pending");
                        Check(mcr.Registry.ReadyCount == 1 && mcr.WritableRegistry.PendingCount == 1,
                            "mixed case enters handoff with one ready and one pending binding");
                    }
                };
                AsyncOperation held = null;
                string heldName = null;
                NetworkSceneManager heldManager = null;
                NetworkSceneManager.OnLoadDelegateHandler hold = (client, name, mode, operation) =>
                {
                    if (client != nm.LocalClientId) return;
                    held = operation;
                    heldName = name;
                    operation.allowSceneActivation = false;
                };
                Action holdOnStart = () => { heldManager = nm.SceneManager; heldManager.OnLoad += hold; };
                if (scenario == 1) nm.OnServerStarted += holdOnStart;
                var startTask = solo.StartValidationAsync(encounter, context);
                if (scenario == 1)
                {
                    await Until(() => held != null, 5, "held scene for stale callback");
                    Check(Spawner.DebugIsSceneLoadPending, "new scene load closes the spawner gate");
                    oldSceneCallback?.Invoke(nm.LocalClientId, heldName, LoadSceneMode.Single);
                    Check(Spawner.DebugIsSceneLoadPending, "old same-name callback cannot open the new pending scene gate");
                    held.allowSceneActivation = true;
                }
                var start = await startTask;
                nm.OnServerStarted -= holdOnStart;
                if (heldManager != null) heldManager.OnLoad -= hold;
                Check(start.IsSuccess, $"case {scenario}: production Solo coordinator starts: {start.ErrorMessage}");
                await Until(() => MatchCompositionRoot.Instance?.InitializationState == MatchInitializationState.Complete,
                    35, "two participant initialization");
                TurnManager.DebugBeforeReadyHandoff = null;
                var mcr = MatchCompositionRoot.Instance;
                var turn = TurnManager.Instance;
                var mm = mcr.MatchManager;
                Check(mcr.Registry is PlayerRegistry registry
                    && registry.TryCaptureReadyBindings(mcr.Participants, out _, out _), "exact expected ready set is capturable");
                Check(mcr.WritableRegistry.TryCaptureReadyBindings(mcr.Participants, out var bindings, out string bindingError),
                    "ready bindings validate: " + bindingError);
                Check(nm.ConnectedClientsIds.Count == 1 && bindings.Length == 2
                    && bindings[0].NetworkObject.NetworkObjectId != bindings[1].NetworkObject.NetworkObjectId
                    && bindings[0].NetworkObject.OwnerClientId == bindings[1].NetworkObject.OwnerClientId,
                    "two distinct logical players use one real connection and shared NGO owner");
                Check(bindings[0].NetworkObject.IsPlayerObject && !bindings[1].NetworkObject.IsPlayerObject
                    && bindings[0].Identity.ClientId == nm.LocalClientId && !bindings[1].Identity.ClientId.HasValue
                    && mcr.Registry.TryGetByClientId(nm.LocalClientId, out var human) && ReferenceEquals(human, bindings[0]),
                    "human alone is the client player; ordinary server-owned bot has no fake client ID");
                Check(mcr.ActiveConfig.Mode == GameMode.Solo && mcr.ActiveConfig.RequiredPlayerCount == 2
                    && ReferenceEquals(mcr.ActiveConfig.Rule, encounter.SharedOneVsOneRule)
                    && encounter.SharedOneVsOneRule.TargetMode == GameMode.OneVsOne,
                    "Solo binds the unchanged shared 1v1 rule asset");
                Check(!mcr.Roster.IsConnected(1) && mcr.Roster.IsParticipantAvailable(1)
                    && mcr.Roster.IsTurnEligible(1) && !mcr.Roster.IsAutoReady(1),
                    "live bot is available and turn eligible without being connected or auto-ready");
                Check(turn.GetPlayer(0) == bindings[0].State && turn.GetPlayer(1) == bindings[1].State
                    && mcr.InitializationCount == 1 && mm.RoundNumber.Value == 1 && turn.TurnNumber.Value == 1
                    && mm.MatchParticipantCount == 2 && mm.HumanConnectionCount == 1,
                    "ready handoff binds both contexts and begins one round and one Prep");
                Check(bindings.All(b => b.Inventory.IsRegistryReady && b.Inventory.SlotStates.Count >= 5
                    && b.State.Temperature.Value == 37 && b.State.FanSpeed.Value == 1),
                    "both participants receive existing 1v1 basics/random grants and neutral stats");
                mcr.Roster.SetTemperature(1, 36f);
                Check(bindings[1].State.Temperature.Value == 36f
                    && mcr.Roster.GetInventorySlots(1).Length == bindings[1].Inventory.SlotStates.Count,
                    "roster reads and writes the live bot rather than a disconnected snapshot");
                mcr.Roster.SetTemperature(1, 37f);
                var fingerprints = bindings.Select(b => b.Inventory.CurrentFingerprint()).ToArray();
                foreach (var binding in bindings) binding.State.AssignParticipant(binding.State.Participant);
                turn.DebugRetryInitialDiscovery();
                await Task.Delay(50);
                Check(mcr.InitializationCount == 1 && mm.RoundNumber.Value == 1 && turn.TurnNumber.Value == 1
                    && bindings.Select(b => b.Inventory.CurrentFingerprint()).SequenceEqual(fingerprints),
                    "duplicate identity/discovery notifications do not repeat grants or round start");
                var markers = FindObjectsByType<PlayerSpawnPoint3D>(FindObjectsSortMode.None).OrderBy(x => x.Order).ToArray();
                Check(markers.Length == 2 && bindings.All(b => Vector3.Distance(b.State.transform.position,
                    markers[b.Identity.PlayerIndex].transform.position) < 0.001f),
                    "both positions use markers from the current A/B scene");
                Check(Spawner.GetInstanceID() == spawnerId && (previousSceneManager == null
                    || !ReferenceEquals(previousSceneManager, nm.SceneManager)),
                    "same persistent spawner tracks newly created NGO scene manager");
                if (scenario == 2)
                    Check(bindings[1].NetworkObject.NetworkObjectId < bindings[0].NetworkObject.NetworkObjectId,
                        "bot-first spawn preserves human seat 0 and bot seat 1");
                if (previousBindings != null)
                {
                    foreach (var old in previousBindings) mcr.WritableRegistry.Unregister(old);
                    oldSceneCallback?.Invoke(nm.LocalClientId, Encounters[(scenario + 1) % 2].GameplayScenePath, LoadSceneMode.Single);
                    Check(mcr.Registry.ReadyCount == 2 && mcr.InitializationCount == 1,
                        "old despawn/scene callbacks cannot remove or reinitialize current participants");
                }
                previousBindings = bindings;
                previousSceneManager = nm.SceneManager;
                oldSceneCallback = Spawner.DebugCaptureSceneLoadCallback();
                await solo.StopAsync();
                Check(!nm.IsListening && router.Current == null && previousBindings.All(b => !b.IsValid)
                    && MatchCompositionRoot.Instance == null, "shutdown clears old participants and match composition");
                if (scenario == 0)
                    await VerifyOnlineSpawnerCycle(nm, transport, online, router, encounter.GameplayScenePath, spawnerId);
            }

            // Inject after a real grant, then verify the failed match cannot enter Prep.
            TurnManager.DebugFailAfterFirstInitialGrant = true;
            TurnManager.DebugInitialGrantFailureMessage = _grantFailureMessage;
            _expectGrantFailure = true;
            var failureStart = await solo.StartValidationAsync(Encounters[0],
                new SoloConfigurationContext(Encounters[0].SharedOneVsOneRule, Catalog, Cosmetics));
            await Until(() => _sawGrantFailure && router.Current == null && !nm.IsListening, 40, "partial grant rollback");
            _expectGrantFailure = false;
            Check(_sawGrantFailure && MatchCompositionRoot.Instance == null, "partial grant failure rolls back all match objects");
            Check(UnityServices.State == ServicesInitializationState.Uninitialized,
                "all local participant/transition fixtures leave UGS uninitialized");
            solo.Dispose();
            Destroy(root);
            Destroy(Spawner.gameObject);
        }

        async Task VerifyOnlineSpawnerCycle(NetworkManager nm, UnityTransport transport, NetworkSessionCoordinator online,
            MatchSessionRouter router, string scene, int spawnerId)
        {
            // Host-side scene/spawn lifecycle only. External 2/3/4-player scenarios
            // exercise full online participants separately with the existing harness.
            online.DebugSetSceneTransitionForValidation(new SceneTransitionService(Menu));
            transport.SetConnectionData(true, "127.0.0.1", 18781, "127.0.0.1");
            nm.NetworkConfig.PlayerPrefab = null;
            Check(nm.StartHost(), "online direct-UTP lifecycle starts after Solo shutdown");
            online.DebugAdoptLocalNetworkSession();
            Check(nm.SceneManager.LoadScene(scene, LoadSceneMode.Single) == SceneEventProgressStatus.Started,
                "online scene begins on persistent spawner");
            await Until(() => Spawner.GetPlayerForClient(nm.LocalClientId) != null, 15, "online host spawn");
            var host = Spawner.GetPlayerForClient(nm.LocalClientId);
            var marker = FindObjectsByType<PlayerSpawnPoint3D>(FindObjectsSortMode.None).OrderBy(x => x.Order).First();
            Check(Spawner.GetInstanceID() == spawnerId && host.IsPlayerObject
                && Vector3.Distance(host.transform.position, marker.transform.position) < 0.001f,
                "online host spawns once at current marker after Solo; same persistent manager");
            await online.LeaveAsync();
            Check(router.Current == null && !nm.IsListening && SceneManager.GetActiveScene().path == Menu,
                "online host lifecycle exits before next Solo start");
            nm.NetworkConfig.PlayerPrefab = PlayerPrefab;
        }

        static async Task ReturnToMenu()
        {
            if (SceneManager.GetActiveScene().path == Menu) return;
            var op = SceneManager.LoadSceneAsync(Menu);
            while (!op.isDone) await Task.Delay(20);
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
            Debug.Log("[PLAN036 T03] PASS " + label);
        }
        void OnLog(string message, string stack, LogType type)
        {
            if (_expectGrantFailure && type == LogType.Error && message == "[MatchInitialization] " + _grantFailureMessage)
            {
                _sawGrantFailure = true;
                var mcr = MatchCompositionRoot.Instance;
                var turn = TurnManager.Instance;
                if (mcr.InitializationState != MatchInitializationState.Failed || mcr.InitializationCount != 0
                    || mcr.MatchManager.RoundNumber.Value != 0 || turn.CurrentPhase.Value != TurnPhase.WaitingForPlayers)
                    _report.errors.Add("Partial grant failure entered a round or did not latch failed state");
                if (mcr.InitializationFailure != _grantFailureMessage || mcr.NetworkState.InitializationError.Value.Length > 125)
                    _report.errors.Add("Long UTF-8 failure was not preserved locally and safely bounded for replication");
                var before = turn.GetPlayer(0).GetInventory().CurrentFingerprint();
                turn.DebugRetryInitialDiscovery();
                if (before != turn.GetPlayer(0).GetInventory().CurrentFingerprint() || mcr.TryBeginInitialization())
                    _report.errors.Add("Partial failure was retried in the same session");
                return;
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _report.errors.Add(message + "\n" + stack);
        }
        void Update()
        {
            if (!_finished && _deadline > 0 && Time.realtimeSinceStartupAsDouble > _deadline)
            { _report.failure = "Participant fixture watchdog timeout"; Finish(); }
        }
        void Finish()
        {
            if (_finished) return;
            _finished = true;
            Application.logMessageReceived -= OnLog;
            TurnManager.DebugBeforeReadyHandoff = null;
            TurnManager.DebugFailAfterFirstInitialGrant = false;
            TurnManager.DebugInitialGrantFailureMessage = null;
            string path = Path.Combine(Application.persistentDataPath, "plan036-participants.json");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--plan036-report") path = args[i + 1];
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(_report, true));
            if (!Application.isEditor) Application.Quit(_report.passed ? 0 : 1);
        }
        void OnDestroy() { if (_instance == this) _instance = null; Application.logMessageReceived -= OnLog; }
    }
}
#endif
