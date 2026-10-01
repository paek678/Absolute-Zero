#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AbsoluteZero.Core.Solo
{
    // Explicit development probe: real public online coordinator/UGS/Relay, then
    // the real serialized Solo button on the SAME persistent application.
    public sealed class SoloModeTransitionProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public int peer;
            public string scope = "Cold Solo -> real Relay 1v1 -> Solo -> Relay 3P -> Relay 4P; failed online and local shutdown recovery";
            public List<string> checks = new(), errors = new(), expectedErrors = new();
            public string failure;
        }
        readonly Report _report = new();
        AppBootstrapper _app;
        NetworkManager _manager;
        NetworkTransport _onlineTransport;
        string _directory;
        bool _expectedFailure;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--plan036-mode-transitions") >= 0)
                new GameObject("Solo mode transition validation").AddComponent<SoloModeTransitionProbe>();
        }
        static string Arg(string key, string fallback)
        { var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback; }
        async void Start()
        {
            DontDestroyOnLoad(gameObject); Application.runInBackground = true;
            _directory = Path.GetFullPath(Arg("--plan036-output", "."));
            _report.peer = int.Parse(Arg("--plan036-peer", "0")); Directory.CreateDirectory(_directory);
            Application.logMessageReceived += Log;
            try
            {
                await Until(() => AppBootstrapper.Instance?.IsReady == true && Button() != null, 20, "local app/menu");
                _app = AppBootstrapper.Instance; _manager = NetworkManager.Singleton; _onlineTransport = _manager.NetworkConfig.NetworkTransport;
                Check(UnityServices.State == ServicesInitializationState.Uninitialized, "cold boot does not authenticate");
                if (_report.peer == 0) await Host(); else await Client();
                _report.passed = _report.errors.Count == 0;
            }
            catch (Exception error) { _report.failure = error.ToString(); File.WriteAllText(Path.Combine(_directory, "abort"), "peer " + _report.peer + ": " + error.Message); }
            finally
            {
                try { if (_app?.SessionRouter.Current != null) await _app.SessionRouter.StopAsync(); }
                catch (Exception error) { _report.errors.Add("Final cleanup: " + error); _report.passed = false; }
                Application.logMessageReceived -= Log;
                File.WriteAllText(Path.Combine(_directory, "peer" + _report.peer + "-report.json"), JsonUtility.ToJson(_report, true));
                Application.Quit(_report.passed ? 0 : 2);
            }
        }
        async Task Host()
        {
            await Solo("cold", false);
            Check(UnityServices.State == ServicesInitializationState.Uninitialized, "cold real Solo graph makes no UGS initialization");
            for (int count = 2; count <= 4; count++)
            {
                var coordinator = NetworkSessionCoordinator.Instance;
                coordinator.SetMatchParameters(count == 2 ? GameMode.OneVsOne : GameMode.Multi, count);
                var create = await Timed(coordinator.CreateLobbyAsync("AZ_P036_" + DateTime.UtcNow.ToString("HHmmss") + "_" + count), 55, "create lobby");
                Check(create.IsSuccess, "create real " + count + "P lobby: " + create.ErrorMessage);
                Write(count + "-code", coordinator.CurrentLobby.LobbyCode);
                await Until(() => coordinator.CurrentLobby?.Players?.Count == count, 80, "lobby population " + count);
                var start = await Timed(coordinator.StartMatchAsHostAsync(), 90, "Relay host start");
                Check(start.IsSuccess, "Relay host entry " + count + ": " + start.ErrorMessage);
                await CheckOnline(count);
                for (int peer = 1; peer < count; peer++) await Exists(count + "-ready-" + peer, 45);
                await Task.Delay(500);
                await _app.SessionRouter.StopAsync();
                await CheckMenu("host leaves " + count + "P");
                Write(count + "-left", "host stopped");
                for (int peer = 1; peer < count; peer++) await Exists(count + "-left-" + peer, 45);
                // All peers share the same PC, so local-only Solo instances run
                // sequentially while each retains its own persistent application.
                await Solo("after-online-" + count, count == 4);
                if (count == 2)
                {
                    Write("client-solo-go", "1"); await Exists("client-solo-done", 60);
                }
            }
            _expectedFailure = true;
            var invalid = await Timed(NetworkSessionCoordinator.Instance.JoinGameAsync("!invalid!"), 45, "deliberate invalid online join");
            _expectedFailure = false;
            Check(invalid.IsFailure, "invalid online entry reports failure");
            await _app.SessionRouter.StopAsync();
            await CheckMenu("failed online entry cleanup");
            await Solo("after-failed-online", false);
            Write("done", "passed");
        }
        async Task Client()
        {
            for (int count = 2; count <= 4; count++)
            {
                if (_report.peer >= count) continue;
                await Exists(count + "-code", 240);
                var coordinator = NetworkSessionCoordinator.Instance;
                coordinator.SetMatchParameters(count == 2 ? GameMode.OneVsOne : GameMode.Multi, count);
                var join = await Timed(coordinator.JoinGameAsync(File.ReadAllText(Path.Combine(_directory, count + "-code")).Trim()), 100, "Relay join");
                Check(join.IsSuccess, "real client joined " + count + "P: " + join.ErrorMessage);
                await CheckOnline(count); Write(count + "-ready-" + _report.peer, "ready");
                // Host stop must itself trigger the normal remote cleanup route.
                await Until(() => _app.SessionRouter.Current == null && SceneManager.GetActiveScene().name == "LobbyScene", 60, "host-departure automatic client recovery");
                await CheckMenu("remote host departure " + count + "P");
                Write(count + "-left-" + _report.peer, "left");
                if (count == 2)
                {
                    await Exists("client-solo-go", 60);
                    await Solo("after-online-client", false);
                    Write("client-solo-done", "passed");
                }
            }
            await Exists("done", 160);
        }
        async Task CheckOnline(int count)
        {
            await Until(() => MatchCompositionRoot.Instance?.Registry?.ReadyCount == count
                && TurnManager.Instance != null && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase, 50, "online presentation/participants");
            var root = MatchCompositionRoot.Instance;
            Check(root.ActiveConfig.Mode == (count == 2 ? GameMode.OneVsOne : GameMode.Multi), count + "P rules/mode correct");
            Check(root.Registry.Players.All(x => x.IsValid && !x.State.IsBotControlled), count + "P has only human roles");
            var managers = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None);
            Check(_app == AppBootstrapper.Instance, count + "P retains original application");
            Check(_manager == NetworkManager.Singleton, count + "P retains original NetworkManager singleton");
            Check(managers.Length == 1, count + "P has one manager: " + string.Join(",", managers.Select(x => x.name + "@" + x.gameObject.scene.name + "#" + x.GetInstanceID() + ":listening=" + x.IsListening)));
            Check(_app.SessionRouter.LaunchContext?.Solo == null && FindAnyObjectByType<BotTurnController>() == null, count + "P contains no Solo settings or controller");
            Check(ReferenceEquals(_manager.NetworkConfig.NetworkTransport, _onlineTransport)
                && ((UnityTransport)_onlineTransport).Protocol == UnityTransport.ProtocolType.RelayUnityTransport, count + "P uses original actual Relay transport");
        }
        async Task Solo(string label, bool unexpectedStop)
        {
            await CheckMenu(label + " before Solo");
            Button().onClick.Invoke(); Button()?.onClick.Invoke();
            var confirm = GameObject.Find("MainUI/SoloSelectionPanel/Body/StartBtn").GetComponent<Button>();
            Check(confirm.interactable && _app.SessionRouter.Current == null, label + " profile preview remains local without ownership");
            confirm.onClick.Invoke(); confirm.onClick.Invoke();
            await Until(() => _app.SessionRouter.IsSolo && TurnManager.Instance != null
                && TurnManager.Instance.TryGetPrepInputSnapshot(out _), 40, label + " Solo Prep");
            var controller = FindAnyObjectByType<BotTurnController>();
            await Until(() => TurnManager.Instance.GetPlayer(1).IsReady.Value, 10, label + " real bot Ready");
            Check(controller != null && controller.RunCount == 1 && controller.SelectionCount == 1, label + " real BT works once");
            Check(_app == AppBootstrapper.Instance && _manager == NetworkManager.Singleton
                && MatchCompositionRoot.Instance.Registry.ReadyCount == 2 && _manager.ConnectedClientsIds.Count == 1, label + " local composition restored");
            Check(!ReferenceEquals(_manager.NetworkConfig.NetworkTransport, _onlineTransport)
                && ((UnityTransport)_manager.NetworkConfig.NetworkTransport).Protocol == UnityTransport.ProtocolType.UnityTransport, label + " local transport replaces Relay only for this session");
            Check(NetworkSessionCoordinator.Instance.CurrentLobby == null && !NetworkSessionCoordinator.Instance.HasOnlineOwnership, label + " no retained online lobby/lease");
            if (unexpectedStop)
            {
                _manager.Shutdown();
                await Until(() => _app.SessionRouter.Current == null && SceneManager.GetActiveScene().name == "LobbyScene", 20, "unexpected local shutdown recovery");
                Check(!string.IsNullOrEmpty(_app.SoloSession.LastError), "unexpected local stop is reported");
            }
            else await _app.SessionRouter.StopAsync();
            await CheckMenu(label + " after Solo");
        }
        async Task CheckMenu(string label)
        {
            await Until(() => SceneManager.GetActiveScene().name == "LobbyScene" && Button()?.interactable == true
                && _app.SessionRouter.Current == null && !_manager.IsListening && !_manager.ShutdownInProgress, 20, label);
            Check(ReferenceEquals(_manager.NetworkConfig.NetworkTransport, _onlineTransport)
                && FindAnyObjectByType<BotTurnController>() == null, label + " transport/controller cleanup");
        }
        static Button Button() => GameObject.Find("MainUI/MainPanel/SoloBtn")?.GetComponent<Button>();
        void Write(string name, string value)
        { string path = Path.Combine(_directory, name); File.WriteAllText(path + ".tmp", value); File.Move(path + ".tmp", path); }
        Task Exists(string name, double seconds) => Until(() => File.Exists(Path.Combine(_directory, name)), seconds, name);
        async Task Until(Func<bool> predicate, double seconds, string name)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (!predicate())
            {
                if (File.Exists(Path.Combine(_directory, "abort"))) throw new InvalidOperationException("peer failure: " + File.ReadAllText(Path.Combine(_directory, "abort")));
                if (Time.realtimeSinceStartupAsDouble >= end) throw new TimeoutException(name);
                await Task.Delay(25);
            }
        }
        async Task<Result<Unit>> Timed(Task<Result<Unit>> task, int seconds, string label)
        { if (await Task.WhenAny(task, Task.Delay(seconds * 1000)) != task) throw new TimeoutException(label); return await task; }
        void Check(bool passed, string label)
        { if (!passed) throw new InvalidOperationException(label); _report.checks.Add(label); Debug.Log("[SoloTransition] " + label); }
        void Log(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception) (_expectedFailure ? _report.expectedErrors : _report.errors).Add(text); }
    }
}
#endif
