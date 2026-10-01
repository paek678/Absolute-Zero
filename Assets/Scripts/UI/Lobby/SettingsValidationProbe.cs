#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed class SettingsValidationProbe : MonoBehaviour
    {
        [Serializable] sealed class Report { public bool passed; public string failure; public List<string> checks = new(), errors = new(); }
        readonly Report _report = new();
        string _directory, _mode, _prefix;
        GameObject _privatePanel, _shakeObject;
        LobbySettingsView _privateView;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--plan040-settings");
            if (at < 0 || at + 3 >= args.Length || !Guid.TryParseExact(args[at + 3], "N", out _)) return;
            var probe = new GameObject(nameof(SettingsValidationProbe)).AddComponent<SettingsValidationProbe>();
            probe._mode = args[at + 1]; probe._directory = args[at + 2]; probe._prefix = "plan040_" + args[at + 3] + "_";
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.logMessageReceived += Log;
            Directory.CreateDirectory(_directory);
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object yielded = null;
                try
                {
                    var current = stack.Peek();
                    if (!current.MoveNext()) { stack.Pop(); continue; }
                    yielded = current.Current;
                    if (yielded is IEnumerator child) { stack.Push(child); continue; }
                }
                catch (Exception error) { _report.failure = error.ToString(); break; }
                yield return yielded;
            }
            _privateView?.Dispose();
            if (_privatePanel != null) Destroy(_privatePanel);
            if (_shakeObject != null) Destroy(_shakeObject);
            if (_mode == "read")
            {
                foreach (string key in new[] { "master_volume", "bgm_volume", "sfx_volume", "screen_shake", "fullscreen" }) PlayerPrefs.DeleteKey(_prefix + key);
                PlayerPrefs.Save();
            }
            Application.logMessageReceived -= Log;
            _report.passed = _report.failure == null && _report.errors.Count == 0;
            File.WriteAllText(Path.Combine(_directory, _mode + ".json"), JsonUtility.ToJson(_report, true));
            Application.Quit(_report.passed ? 0 : 2);
        }
        IEnumerator Run()
        {
            yield return Until(() => LocalSettingsRuntime.Instance != null && GameAudioManager.Instance != null
                && FindAnyObjectByType<AZLobbyUI>() != null && FindObjectsByType<Button>(FindObjectsSortMode.None).Any(b => b.name == "SettingsBtn"), "Main menu");
            yield return null;
            var runtime = LocalSettingsRuntime.Instance; var service = runtime.Service;
            if (_mode == "read")
                Check(service.Current.Master == .5f && service.Current.Bgm == .25f && service.Current.Sfx == .75f
                    && !service.Current.Shake && service.Current.Fullscreen == 0, "fresh process restores saved settings");
            FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "SettingsBtn").onClick.Invoke();
            yield return null;
            var bindings = FindObjectsByType<SettingsPanelBindings>(FindObjectsSortMode.None).Single();
            Check(bindings.IsComplete && bindings.gameObject.activeInHierarchy, "real menu opens complete Settings panel");
            float listener = AudioListener.volume;
            var sources = GameAudioManager.Instance.GetComponentsInChildren<AudioSource>();
            Check(sources.Length == 6, "all six managed audio sources present");
            foreach (float master in new[] { 0f, .5f, 1f })
            {
                bindings.Master.value = master; bindings.Bgm.value = .5f; bindings.Sfx.value = .5f;
                yield return null;
                foreach (var source in sources)
                {
                    float baseline = source.name switch { "Audio_BGM" => .35f, "Audio_SFX" => .7f, "Audio_UI" => .5f,
                        "Audio_ENV" => .6f, "Audio_FanLoop" => .15f, "Audio_Clock" => .5f, _ => -1 };
                    Check(Mathf.Approximately(source.volume, baseline * .5f * master), source.name + " single multiplier " + master);
                }
            }
            Check(AudioListener.volume == listener, "listener volume is not multiplied again");
            var audio = GameAudioManager.Instance;
            audio.PlayBGM(); audio.StartFanLoop(); audio.PlayClockTick(); audio.PlayButtonClick(); audio.PlayFreeze();
            audio.PlayEnvironment(AbsoluteZero.Core.Item.EnvironmentType.CicadaSong);
            yield return null;
            foreach (string name in new[] { "Audio_BGM", "Audio_FanLoop", "Audio_Clock", "Audio_ENV" })
                Check(sources.Single(s => s.name == name).isPlaying, name + " keeps playback with settings control");
            service.SetVolumes(0, .5f, .5f);
            Check(sources.All(s => s.volume == 0), "master mute affects every managed source");
            service.SetVolumes(1, .5f, .5f); audio.StopClockTick(); audio.StopEnvironment(); audio.StopFanLoop();

            for (int i = 0; i < 20; i++)
            {
                bindings.Close.onClick.Invoke(); yield return null;
                Check(!bindings.gameObject.activeSelf, "close cycle " + i);
                FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "SettingsBtn").onClick.Invoke(); yield return null;
                Check(bindings.gameObject.activeSelf, "open cycle " + i);
            }
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(800,600) })
            {
                Screen.SetResolution(size.x,size.y,false); yield return new WaitForSecondsRealtime(.3f);
                Check(Screen.width == size.x && Screen.height == size.y, "resolution " + size);
                yield return Capture("size-" + size.x + "x" + size.y);
            }
            Screen.SetResolution(1280,720,false); yield return new WaitForSecondsRealtime(.3f);
            int width=Screen.width, height=Screen.height;
            var refresh = Screen.currentResolution.refreshRateRatio;
            bindings.Fullscreen.isOn = true;
            yield return Until(() => !runtime.ScreenPending, "fullscreen apply");
            Check(Screen.fullScreen && runtime.ScreenError == null, "fullscreen true confirmed by standalone");
            Check(Screen.width == width && Screen.height == height, "fullscreen preserves render resolution");
            Check(Screen.currentResolution.refreshRateRatio.Equals(refresh), "fullscreen preserves monitor refresh");
            bindings.Fullscreen.isOn = false;
            yield return Until(() => !runtime.ScreenPending, "windowed apply");
            Check(!Screen.fullScreen && runtime.ScreenError == null, "windowed confirmed by standalone");
            runtime.RequestFullscreen(true); runtime.RequestFullscreen(false);
            yield return Until(() => !runtime.ScreenPending, "coalesced screen mode");
            Check(!Screen.fullScreen && service.Current.Fullscreen == 0, "rapid requests end at latest saved actual mode");

            _shakeObject = new GameObject("SettingsShakeFixture");
            _shakeObject.transform.position = new Vector3(3,4,5);
            var shake = _shakeObject.AddComponent<CameraShake>();
            Check(CameraShake.Instance == shake, "isolated camera shake fixture owns no gameplay camera");
            service.SetShake(true);
            service.SetShake(false);
            var idleRandom = UnityEngine.Random.state; yield return null; yield return null;
            _report.checks.Add("diagnostic: random changed across idle frames with shake disabled=" + !UnityEngine.Random.state.Equals(idleRandom));
            service.SetShake(true);
            var random = UnityEngine.Random.state;
            var tick = typeof(CameraShake).GetMethod("LateUpdate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < 60; i++) { shake.Shake(3,.2f); tick.Invoke(shake,null); }
            Check(UnityEngine.Random.state.Equals(random), "60 actual shake updates do not consume game random state");
            yield return null; yield return null;
            Check(_shakeObject.transform.position != new Vector3(3,4,5), "enabled shake animates");
            service.SetShake(false);
            Check(_shakeObject.transform.position == new Vector3(3,4,5), "disable shake immediately removes owned offset");
            service.SetShake(true); shake.Shake(2,.2f); yield return null;
            _shakeObject.transform.position = new Vector3(10,20,30); service.SetShake(false);
            Check(_shakeObject.transform.position == new Vector3(10,20,30), "settings does not overwrite an external camera move");
            Destroy(_shakeObject); yield return null;

            bindings.Close.onClick.Invoke(); yield return null;
            _privatePanel = Instantiate(bindings.gameObject, bindings.transform.parent); _privatePanel.SetActive(false);
            var local = _privatePanel.GetComponent<SettingsPanelBindings>();
            foreach (var slider in _privatePanel.GetComponentsInChildren<Slider>(true)) slider.onValueChanged.RemoveAllListeners();
            foreach (var toggle in _privatePanel.GetComponentsInChildren<Toggle>(true)) toggle.onValueChanged.RemoveAllListeners();
            foreach (var button in _privatePanel.GetComponentsInChildren<Button>(true)) button.onClick.RemoveAllListeners();
            var fault = new FaultStore(); var isolated = new LocalSettingsService(fault);
            _privateView = new LobbySettingsView(_privatePanel, isolated); _privateView.SetVisible(true);
            local.Master.value = .2f;
            Check(isolated.HasUnsaved && local.Retry.gameObject.activeSelf && local.Status.text.Contains("저장하지"), "save failure remains visible and retryable");
            fault.Fail = false; local.Retry.onClick.Invoke();
            Check(!isolated.HasUnsaved && !local.Retry.gameObject.activeSelf, "explicit retry clears save error");
            int writes = fault.Saves;
            _privateView.SetVisible(false); _privateView.SetVisible(true);
            Check(writes == fault.Saves, "opening view does not write preferences");
            _privateView.Dispose(); local.Master.value = .3f;
            Check(writes == fault.Saves, "disposed view listeners no longer write");
            Destroy(_privatePanel); yield return null;
            service.SetVolumes(.5f,.25f,.75f); service.SetShake(false); service.SetFullscreen(false);
            Check(service.Save(), "final settings saved for next process");
            Check(!new AbsoluteZero.Core.Session.UnityServicesGateway().IsInitialized, "settings do not initialize UGS");
        }
        sealed class FaultStore : ILocalSettingsStore
        {
            public bool Fail = true; public int Saves;
            public LocalSettingsSnapshot Load() => new(1);
            public bool Save(LocalSettingsSnapshot value, out string error) { Saves++; error = Fail ? "injected" : null; return !Fail; }
        }
        IEnumerator Until(Func<bool> ready, string label)
        { double end = Time.realtimeSinceStartupAsDouble + 15; while (!ready()) { if (Time.realtimeSinceStartupAsDouble > end) throw new TimeoutException(label); yield return null; } }
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try { Check(texture != null && texture.width == Screen.width, "actual framebuffer " + name); File.WriteAllBytes(Path.Combine(_directory,_mode + "-" + name + ".png"), texture.EncodeToPNG()); }
            finally { if (texture != null) Destroy(texture); }
        }
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); _report.checks.Add(message); }
        void Log(string message, string stack, LogType type) { if (type is LogType.Error or LogType.Exception) _report.errors.Add(message); }
    }
}
#endif
