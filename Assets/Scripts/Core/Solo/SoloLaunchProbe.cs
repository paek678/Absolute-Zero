#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace AbsoluteZero.Core.Solo
{
    // Invokes the real serialized lobby button; never constructs a parallel app.
    public sealed class SoloLaunchProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope = "T07 shipped lobby button, dedicated scene, one real local connection, production views and explicit development bot driver";
            public List<string> checks = new();
            public List<string> captures = new();
            public List<string> errors = new();
            public string failure;
        }
        readonly Report _report = new();
        string _directory;
        int _combatCount;
        double _deadline;
        bool _finished;
        bool _btMode;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--plan036-solo-slice-run") < 0
                && Array.IndexOf(Environment.GetCommandLineArgs(), "--plan036-bt-run") < 0) return;
            new GameObject("Solo launch validation").AddComponent<SoloLaunchProbe>();
        }
        async void Start()
        {
            DontDestroyOnLoad(gameObject); Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs();
            _btMode = Array.IndexOf(args, "--plan036-bt-run") >= 0;
            if (_btMode) _report.scope = "T08 normal development lobby entry, real production BT; late binding/cancellation/empty-inventory stimuli are explicit test cases";
            int option = Array.IndexOf(args, "--plan036-output");
            _directory = option >= 0 ? Path.GetFullPath(args[option + 1]) : Path.Combine(Application.persistentDataPath, "solo-launch-probe");
            Directory.CreateDirectory(_directory);
            _deadline = Time.realtimeSinceStartupAsDouble + 130;
            Application.logMessageReceived += Log;
            TurnManager.OnCombatResult += Combat;
            try { await Run(); _report.passed = _report.errors.Count == 0; }
            catch (Exception error) { _report.failure = error.ToString(); }
            Finish();
        }
        async Task Run()
        {
            await Until(() => AppBootstrapper.Instance?.IsReady == true && GameObject.Find("MainUI/MainPanel/SoloBtn") != null, 15, "lobby readiness");
            await Task.Delay(400);
            Check(SceneManager.GetActiveScene().name == "LobbyScene" && UnityServices.State == ServicesInitializationState.Uninitialized, "cold real lobby needs no UGS authentication");
            var button = GameObject.Find("MainUI/MainPanel/SoloBtn").GetComponent<Button>();
            Check(button.interactable && button.gameObject.activeInHierarchy, "serialized Solo button is active and clickable");
            CheckButtonBounds(button);
            await Capture("01-lobby");
            button.onClick.Invoke();
            button.onClick.Invoke();
            var confirm = GameObject.Find("MainUI/SoloSelectionPanel/Body/StartBtn").GetComponent<Button>();
            Check(confirm.interactable && AppBootstrapper.Instance.SessionRouter.Current == null, "profile selection requires explicit Start without acquiring a session");
            await Capture("01b-solo-selection");
            confirm.onClick.Invoke(); confirm.onClick.Invoke();
            var app = AppBootstrapper.Instance;
            await Until(() => app.SessionRouter.Current != null, 5, "Solo ownership");
            long generation = app.SessionRouter.Current.Generation;
            Check(app.SessionRouter.IsSolo, "button acquires local Solo ownership");
            await Capture("02-loading");
            await Until(() => TurnManager.Instance != null && TurnManager.Instance.TryGetPrepInputSnapshot(out _), 45, "dedicated scene committed Prep");
            var turn = TurnManager.Instance;
            Check(SceneManager.GetActiveScene().path == "Assets/Scenes/GameScene_Solo.unity", "dedicated Solo scene is active, distinct from Multi");
            Check(app == AppBootstrapper.Instance && app.SessionRouter.Current.Generation == generation
                && FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length == 1
                && NetworkManager.Singleton.ConnectedClientsIds.Count == 1, "repeated clicks retain one app/host/generation/connection");
            var match = MatchCompositionRoot.Instance;
            Check(match.Registry.ReadyCount == 2 && match.ActiveConfig.Mode == GameMode.Solo, "two logical participants bind Solo with shared duel rules");
            var human = turn.GetPlayer(0); var bot = turn.GetPlayer(1);
            Check(human.NetworkObject.IsPlayerObject && !bot.NetworkObject.IsPlayerObject && bot.IsBotControlled, "human and ordinary server-owned bot have distinct roles");
            await Until(() => GameObject.Find("LoadingScreenCanvas") == null, 8, "loading dismissal");
            BotTurnController controller = _btMode ? FindAnyObjectByType<BotTurnController>() : null;
            if (_btMode)
            {
                await Until(() => bot.IsReady.Value, 6, "real graph selection and Ready");
                Check(controller != null && controller.RunCount == 1 && controller.SelectionCount == 1 && controller.ReadyCount == 1, "real visual BT ran once, delayed one selection and accepted Ready");
                Check(!app.SessionRouter.LaunchContext.Solo.IsValidationFixture && controller.RuntimeGraph != app.SessionRouter.LaunchContext.Solo.Graph,
                    "normal default uses isolated production graph and no fixture driver");
                Check(!FindAnyObjectByType<SoloDevelopmentDriver>().enabled, "scripted slice driver is disabled in normal launch");
            }
            await Capture("03-prep");
            var copy = human.GetInventory().SlotStates[0];
            human.SelectItemServerRpc(0, 1, copy.CopyId);
            Check(human.HasSelectedItem.Value, "real human selection accepts a legal attack");
            human.PressReadyServerRpc();
            await Until(() => _combatCount >= 1, 25, "real combat result");
            await Task.Delay(800);
            await Capture("04-combat");
            if (_btMode) controller.enabled = false;
            await Until(() => turn.CurrentPhase.Value == TurnPhase.PrepPhase && turn.TurnNumber.Value >= 2, 25, "presentation ACK and second Prep");
            Check(_combatCount == 1 && human.Temperature.Value < 37 && bot.Temperature.Value < 37, "production presentation finishes and both attacks apply exactly one combat");
            Check(UnityServices.State == ServicesInitializationState.Uninitialized, "local scene and real combat never initialize UGS");
            if (_btMode) await ExerciseGraphLifetime(controller, turn, human, bot);
            await app.SessionRouter.StopAsync();
            await Until(() => SceneManager.GetActiveScene().name == "LobbyScene" && app.SessionRouter.Current == null, 15, "explicit local exit");
            await Task.Delay(500);
            var restored = GameObject.Find("MainUI/MainPanel/SoloBtn")?.GetComponent<Button>();
            Check(restored != null && restored.interactable && !NetworkManager.Singleton.IsListening, "exit restores usable lobby and stops local network");
            await Capture("05-return");
        }
        async Task ExerciseGraphLifetime(BotTurnController controller, TurnManager turn, PlayerState human, PlayerState bot)
        {
            await Task.Delay(350);
            Check(controller.RunCount == 1 && turn.TryGetPrepInputSnapshot(out _), "disabled graph waits while committed second Prep advances");
            turn.TryGetPrepInputSnapshot(out var second);
            Check(second.RemainingSeconds < second.Deadline - second.StartTime, "late consumer sees elapsed Prep time");
            controller.enabled = true;
            await Until(() => controller.RunCount == 2 && bot.GetBotCommands().HasPending, 5, "late catch-up graph pending request");
            controller.OnPrep(second); controller.OnPrep(second);
            Check(controller.RunCount == 2, "duplicate current Prep notifications do not restart graph");
            uint fingerprint = bot.GetInventory().CurrentFingerprint();
            controller.enabled = false;
            await Task.Delay(1800);
            Check(!bot.GetBotCommands().HasPending && !bot.HasSelectedItem.Value && fingerprint == bot.GetInventory().CurrentFingerprint(),
                "disabling graph cancels pending request without late queue/effect/consumption");
            controller.enabled = true; await Task.Delay(100);
            Check(controller.RunCount == 2, "re-enable cannot resurrect canceled same-turn graph");
            controller.enabled = false;
            Check(bot.ServerPressBotReady(second.Key).Succeeded, "test cleanup Ready uses existing authoritative boundary");
            human.PressReadyServerRpc();
            await Until(() => _combatCount >= 2, 20, "second combat");
            await Until(() => turn.TryGetPrepInputSnapshot(out _) && turn.TurnNumber.Value >= 3, 25, "third committed Prep");
            // Deliberate development-only no-item stimulus, separate from natural
            // normal-entry evidence above. No production AI mutates inventory.
            bot.GetInventory().SlotStates.Clear();
            controller.enabled = true;
            await Until(() => bot.IsReady.Value, 5, "empty-inventory fallback");
            Check(controller.RunCount == 3 && controller.ReadyCount == 2 && !bot.HasSelectedItem.Value,
                "real graph exhausts bounded retry and falls back to legal Ready with no item");
            File.WriteAllLines(Path.Combine(_directory, "bot-trace.txt"), controller.Trace);
            await Capture("06-bt-fallback");
        }
        void CheckButtonBounds(Button button)
        {
            Rect Bounds(RectTransform rect)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var points = corners.Select(x => RectTransformUtility.WorldToScreenPoint(null, x)).ToArray();
                return Rect.MinMaxRect(points.Min(x => x.x), points.Min(x => x.y), points.Max(x => x.x), points.Max(x => x.y));
            }
            var bounds = Bounds((RectTransform)button.transform);
            Check(bounds.xMin >= 0 && bounds.yMin >= 0 && bounds.xMax <= Screen.width && bounds.yMax <= Screen.height, "Solo button fits current window " + Screen.width + "x" + Screen.height);
            foreach (var other in button.transform.parent.GetComponentsInChildren<Button>(false))
                if (other != button) Check(!bounds.Overlaps(Bounds((RectTransform)other.transform)), "Solo button does not overlap " + other.name);
        }
        Task Capture(string name)
        {
            var completion = new TaskCompletionSource<bool>(); StartCoroutine(CaptureFrame(name, completion)); return completion.Task;
        }
        IEnumerator CaptureFrame(string name, TaskCompletionSource<bool> completion)
        {
            yield return new WaitForEndOfFrame();
            try
            {
                string path = Path.Combine(_directory, name + ".png");
                WriteRender(path); _report.captures.Add(path);
                Check(true, "valid camera/canvas render " + name); completion.SetResult(true);
            }
            catch (Exception error) { completion.SetException(error); }
        }
        internal static void WriteRender(string path)
        {
            // Explicit visible-player validation captures the actual final frame,
            // including the URP camera stack and overlay UI, without re-rendering it.
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--plan036-framebuffer-captures") >= 0)
            {
                var frame = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    if (frame == null || frame.width <= 0 || frame.height <= 0)
                        throw new InvalidOperationException("Framebuffer capture unavailable: " + path);
                    var pixels = frame.GetPixels32();
                    if (pixels.Count(x => x.r + x.g + x.b > 30) <= pixels.Length / 100)
                        throw new InvalidOperationException("Black framebuffer capture: " + path);
                    File.WriteAllBytes(path, frame.EncodeToPNG());
                }
                finally { if (frame != null) Destroy(frame); }
                return;
            }
            // Hidden Windows players can have an occluded, black swap chain. Render
            // the actual camera plus live root canvases into a diagnostic target.
            var camera = Camera.main;
            bool temporaryCamera = camera == null;
            if (temporaryCamera) camera = new GameObject("Capture camera").AddComponent<Camera>();
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(x => x.isRootCanvas && x.isActiveAndEnabled && x.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var oldCameras = canvases.Select(x => x.worldCamera).ToArray();
            var oldDistances = canvases.Select(x => x.planeDistance).ToArray();
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                foreach (var canvas in canvases)
                { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + 0.5f; }
                Canvas.ForceUpdateCanvases();
                if (GraphicsSettings.currentRenderPipeline != null)
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                else { camera.targetTexture = target; camera.Render(); }
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
                var pixels = texture.GetPixels32();
                if (pixels.Count(x => x.r + x.g + x.b > 30) <= pixels.Length / 100)
                    throw new InvalidOperationException("Black or uniform diagnostic render: " + path);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                for (int i = 0; i < canvases.Length; i++)
                { canvases[i].renderMode = RenderMode.ScreenSpaceOverlay; canvases[i].worldCamera = oldCameras[i]; canvases[i].planeDistance = oldDistances[i]; }
                RenderTexture.ReleaseTemporary(target); Destroy(texture);
                if (temporaryCamera) Destroy(camera.gameObject);
            }
        }
        async Task Until(Func<bool> predicate, double seconds, string label)
        { double limit = Time.realtimeSinceStartupAsDouble + seconds; while (!predicate()) { if (Time.realtimeSinceStartupAsDouble > limit) throw new TimeoutException(label); await Task.Delay(25); } }
        void Check(bool pass, string label) { if (!pass) throw new InvalidOperationException(label); _report.checks.Add(label); }
        void Combat(CombatResultData result) => _combatCount++;
        void Log(string message, string stack, LogType type) { if (type == LogType.Error || type == LogType.Exception) _report.errors.Add(message); }
        void Update() { if (!_finished && _deadline > 0 && Time.realtimeSinceStartupAsDouble > _deadline) { _report.failure = "global timeout"; Finish(); } }
        void Finish()
        {
            if (_finished) return; _finished = true;
            Application.logMessageReceived -= Log; TurnManager.OnCombatResult -= Combat;
            File.WriteAllText(Path.Combine(_directory, "report.json"), JsonUtility.ToJson(_report, true));
            Application.Quit(_report.passed ? 0 : 2);
        }
    }
}
#endif
