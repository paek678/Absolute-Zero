#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Validation.Solo
{
    public sealed class Plan036SessionProbe : MonoBehaviour
    {
        public SoloDuelDefinitionSO Encounter;
        public ItemDataSO[] Catalog;
        public CosmeticRegistrySO Cosmetics;
        static Plan036SessionProbe _instance;
        const string BootstrapScene = "Assets/Tests/Plan036Session/SessionFixture.unity";
        readonly Report _report = new();
        bool _expectedTransportError;
        bool _finished;
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
            _deadline = Time.realtimeSinceStartupAsDouble + 100;
            Application.logMessageReceived += OnLog;
            try { await Run(); _report.passed = _report.errors.Count == 0; }
            catch (Exception error) { _report.failure = error.ToString(); }
            Finish();
        }

        async Task Run()
        {
            var root = new GameObject("T02 local application fixture");
            DontDestroyOnLoad(root);
            var nm = root.AddComponent<NetworkManager>();
            nm.NetworkConfig = new NetworkConfig();
            var original = root.AddComponent<UnityTransport>();
            nm.NetworkConfig.NetworkTransport = original;
            nm.NetworkConfig.EnableSceneManagement = true;
            var online = root.AddComponent<NetworkSessionCoordinator>();
            var bootstrap = root.AddComponent<AppBootstrapper>();
            await Until(() => bootstrap.IsReady, 5, "local bootstrap");
            Check(online.State == SessionState.Offline && UnityServices.State == ServicesInitializationState.Uninitialized,
                "cold local readiness makes no UGS initialization request");
            var context = new SoloConfigurationContext(Encounter.SharedOneVsOneRule, Catalog, Cosmetics);
            Check(SoloSettingsResolver.TryResolveForValidation(Encounter, context, out var settings, out var errors),
                "player resolves real compiled graph and catalog: " + string.Join("; ", errors));
            var empty = ScriptableObject.CreateInstance<Unity.Behavior.BehaviorGraph>();
            Check(!SoloSettingsResolver.HasCompiledRoot(empty), "player rejects empty graph");
            Destroy(empty);
            string originalJson = JsonUtility.ToJson(original);
            var router = bootstrap.SessionRouter;
            var runtime = new NgoLocalHostRuntime(nm);
            var solo = new SoloSessionCoordinator(router, runtime, 18777, ReturnToFixture);

            var start = solo.StartValidationAsync(Encounter, context);
            var duplicate = await solo.StartValidationAsync(Encounter, context);
            Check(duplicate.IsFailure, "duplicate Solo launch is rejected");
            Check((await start).IsSuccess, "local host and target scene start");
            Check(nm.IsHost && nm.ConnectedClientsIds.Count == 1 && nm.SpawnManager.GetLocalPlayerObject() == null,
                "one actual connection and no automatic player spawn");
            Check(runtime.ActiveTransport.ConnectionData.Address == "127.0.0.1"
                && runtime.ActiveTransport.ConnectionData.ServerListenAddress == "127.0.0.1",
                "both transport endpoints are loopback");
            await VerifyRemoteRejection(nm);
            Check(UnityServices.State == ServicesInitializationState.Uninitialized && online.State == SessionState.Offline,
                "Solo start does not initialize or update online services");
            var firstLease = solo.Lease;
            await Task.WhenAll(solo.StopAsync(), solo.StopAsync());
            Check(!nm.IsListening && !nm.ShutdownInProgress && !runtime.HasSnapshot && router.Current == null,
                "duplicate stop completes one shutdown and releases owner");
            Check(ReferenceEquals(nm.NetworkConfig.NetworkTransport, original) && originalJson == JsonUtility.ToJson(original)
                && !nm.NetworkConfig.ConnectionApproval && nm.ConnectionApprovalCallback == null,
                "online transport and approval settings restored unchanged");

            AsyncOperation held = null;
            NetworkSceneManager heldManager = null;
            NetworkSceneManager.OnLoadDelegateHandler hold = (client, scene, mode, operation) =>
            {
                if (client != nm.LocalClientId) return;
                held = operation;
                held.allowSceneActivation = false;
            };
            Action holdOnStart = () => { heldManager = nm.SceneManager; heldManager.OnLoad += hold; };
            nm.OnServerStarted += holdOnStart;
            var heldStart = solo.StartValidationAsync(Encounter, context);
            await Until(() => held != null, 5, "captured pending scene load");
            var heldStop = solo.StopAsync();
            await Task.Delay(150);
            Check(!heldStop.IsCompleted && router.IsStopping && router.Owns(solo.Lease) && !router.IsCurrent(solo.Lease),
                "stop retains invalidated owner while native scene load is pending");
            held.allowSceneActivation = true;
            await heldStop;
            var cancelledStart = await heldStart;
            nm.OnServerStarted -= holdOnStart;
            if (heldManager != null) heldManager.OnLoad -= hold;
            await Task.Delay(100);
            Check(cancelledStart.ErrorCode == OperationErrorCode.Cancelled && SceneManager.GetActiveScene().path == BootstrapScene,
                "late scene load settles before return; old start cannot revive session");

            using (var blocker = new UdpClient(AddressFamily.InterNetwork))
            {
                blocker.Client.ExclusiveAddressUse = true;
                blocker.Client.Bind(new IPEndPoint(IPAddress.Loopback, 18778));
                var blocked = new SoloSessionCoordinator(router, new NgoLocalHostRuntime(nm), 18778, ReturnToFixture);
                _expectedTransportError = true;
                var failed = await blocked.StartValidationAsync(Encounter, context);
                await Task.Delay(100);
                _expectedTransportError = false;
                Check(failed.IsFailure && !nm.IsListening && router.Current == null
                    && ReferenceEquals(nm.NetworkConfig.NetworkTransport, original),
                    "occupied port fails and restores local configuration without releasing a live host");
                blocked.Dispose();
            }

            Check((await solo.StartValidationAsync(Encounter, context)).IsSuccess, "start succeeds after occupied-port rollback");
            Check(!router.End(firstLease) && router.IsCurrent(solo.Lease), "old cleanup cannot release restarted session");
            await solo.StopAsync();
            Check(router.Current == null && SceneManager.GetActiveScene().path == BootstrapScene && !nm.IsListening,
                "final fixture exit has no live host or stale scene");
            await VerifyOnlineLoadCancellation(nm, original, online, router);
            Check((await solo.StartValidationAsync(Encounter, context)).IsSuccess,
                "Solo can start after cancelled online scene entry on the same NetworkManager");
            await solo.StopAsync();
            solo.Dispose();
            Destroy(root);
            await Task.Delay(50);
        }

        async Task VerifyOnlineLoadCancellation(NetworkManager nm, UnityTransport original,
            NetworkSessionCoordinator online, MatchSessionRouter router)
        {
            // Adopt the existing direct-UTP validation route; production online entry is
            // exercised separately through the real Relay/Lobby smoke scenario.
            online.DebugSetSceneTransitionForValidation(new SceneTransitionService(BootstrapScene));
            original.SetConnectionData(true, "127.0.0.1", 18779, "127.0.0.1");
            Check(nm.StartHost(), "online lifecycle fixture starts existing UTP transport");
            online.DebugAdoptLocalNetworkSession();
            await Task.Delay(50);
            AsyncOperation held = null;
            var sceneManager = nm.SceneManager;
            NetworkSceneManager.OnLoadDelegateHandler hold = (client, scene, mode, operation) =>
            {
                if (client != nm.LocalClientId) return;
                held = operation;
                held.allowSceneActivation = false;
            };
            sceneManager.OnLoad += hold;
            Check(sceneManager.LoadScene(Encounter.GameplayScenePath, LoadSceneMode.Single) == SceneEventProgressStatus.Started,
                "online lifecycle fixture begins native scene load");
            await Until(() => held != null, 5, "online pending scene load");
            var lease = online.OnlineLease;
            var stopping = online.LeaveAsync();
            await Task.Delay(150);
            Check(!stopping.IsCompleted && router.IsStopping && router.Owns(lease),
                "online cancellation retains owner until its native scene load settles");
            held.allowSceneActivation = true;
            await stopping;
            sceneManager.OnLoad -= hold;
            await Task.Delay(100);
            Check(router.Current == null && !nm.IsListening && SceneManager.GetActiveScene().path == BootstrapScene,
                "cancelled online entry cannot activate a late gameplay scene after release");
        }

        async Task VerifyRemoteRejection(NetworkManager host)
        {
            // A real second UTP endpoint performs NGO admission; it is not counted as
            // an accepted participant or a substitute for the later bot object.
            var go = new GameObject("T02 rejected endpoint");
            DontDestroyOnLoad(go);
            var client = go.AddComponent<NetworkManager>();
            client.NetworkConfig = new NetworkConfig();
            var transport = go.AddComponent<UnityTransport>();
            client.NetworkConfig.NetworkTransport = transport;
            client.NetworkConfig.ConnectionApproval = true;
            client.NetworkConfig.EnableSceneManagement = true;
            transport.SetConnectionData(true, "127.0.0.1", 18777, "127.0.0.1");
            bool stopped = false;
            client.OnClientStopped += _ => stopped = true;
            Check(client.StartClient(), "additional endpoint attempts an actual NGO connection");
            await Until(() => stopped, 8, "remote admission rejection");
            Check(host.ConnectedClientsIds.Count == 1 && !client.IsConnectedClient
                && client.DisconnectReason.Contains("does not accept"), "additional client is rejected by Solo approval");
            if (client.IsListening) client.Shutdown();
            await Until(() => !client.ShutdownInProgress, 5, "rejected endpoint shutdown");
            Destroy(go);
            await Task.Delay(50);
        }

        static async Task ReturnToFixture()
        {
            if (SceneManager.GetActiveScene().path == BootstrapScene) return;
            var operation = SceneManager.LoadSceneAsync(BootstrapScene);
            while (!operation.isDone) await Task.Delay(20);
        }

        static async Task Until(Func<bool> condition, double timeout, string operation)
        {
            double end = Time.realtimeSinceStartupAsDouble + timeout;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble >= end) throw new TimeoutException(operation);
                await Task.Delay(20);
            }
        }

        void Check(bool passed, string label)
        {
            if (!passed) throw new InvalidOperationException(label);
            _report.checks.Add(label);
            Debug.Log("[PLAN036 T02] PASS " + label);
        }
        void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && !_expectedTransportError)
                _report.errors.Add(message + "\n" + stack);
        }
        void Update()
        {
            if (!_finished && _deadline > 0 && Time.realtimeSinceStartupAsDouble > _deadline)
            { _report.failure = "Fixture watchdog timeout"; Finish(); }
        }
        void Finish()
        {
            if (_finished) return;
            _finished = true;
            Application.logMessageReceived -= OnLog;
            string path = Path.Combine(Application.persistentDataPath, "plan036-session.json");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--plan036-report") path = args[i + 1];
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(_report, true));
            Debug.Log("[PLAN036 T02] " + (_report.passed ? "PASS " : "FAIL ") + path);
            if (!Application.isEditor) Application.Quit(_report.passed ? 0 : 1);
        }
        void OnDestroy() { if (_instance == this) _instance = null; Application.logMessageReceived -= OnLog; }
    }
}
#endif
