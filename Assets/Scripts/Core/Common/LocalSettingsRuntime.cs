using System;
using System.Collections;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    // Owns only platform presentation preferences for this application lifetime.
    public sealed class LocalSettingsRuntime : MonoBehaviour
    {
        public static LocalSettingsRuntime Instance { get; private set; }
        public LocalSettingsService Service { get; private set; }
        public bool ScreenPending { get; private set; }
        public string ScreenError { get; private set; }
        public event Action ScreenChanged;
        bool _wanted, _persist;
        int _request;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatic() => Instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Instance == null) new GameObject(nameof(LocalSettingsRuntime)).AddComponent<LocalSettingsRuntime>();
        }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            string prefix = "";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Explicit development fixture uses the normal service with isolated keys.
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--plan040-settings");
            if (at >= 0 && at + 3 < args.Length && Guid.TryParseExact(args[at + 3], "N", out _))
                prefix = "plan040_" + args[at + 3] + "_";
            int probeAt = Array.IndexOf(args, "--az-settings-probe");
            if (probeAt >= 0 && probeAt + 1 < args.Length && int.TryParse(args[probeAt + 1], out int peer) && peer is >= 0 and < 4)
            {
                Service = new LocalSettingsService(new ProbeStore(peer));
                return;
            }
#endif
            Service = new LocalSettingsService(new PlayerPrefsSettingsStore(prefix));
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        sealed class ProbeStore : ILocalSettingsStore
        {
            LocalSettingsSnapshot _value;
            public ProbeStore(int peer) => _value = new LocalSettingsSnapshot(peer / 3f, .25f, .75f, peer % 2 == 1);
            public LocalSettingsSnapshot Load() => _value;
            public bool Save(LocalSettingsSnapshot value, out string error) { _value = value; error = null; return true; }
        }
#endif
        void Start()
        {
            if (Service.Current.Fullscreen >= 0 && !Application.isEditor)
                RequestFullscreen(Service.Current.Fullscreen == 1, false);
        }
        public void RequestFullscreen(bool value) => RequestFullscreen(value, true);
        void RequestFullscreen(bool value, bool persist)
        {
            _wanted = value; _persist = persist; _request++;
            if (ScreenPending) return; // Keep the latest request while the OS applies the current one.
            ScreenPending = true; ScreenError = null;
            StartCoroutine(ApplyScreen());
        }
        IEnumerator ApplyScreen()
        {
            while (true)
            {
                int request = _request;
                bool wanted = _wanted, persist = _persist;
                if (Application.isEditor) ScreenError = "전체 화면 변경은 실행 파일에서 확인할 수 있습니다";
                else
                {
                    // Preserve render resolution and monitor refresh rate. Avoid exclusive mode.
                    Screen.SetResolution(Screen.width, Screen.height,
                        wanted ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed, Screen.currentResolution.refreshRateRatio);
                    yield return null; yield return null;
                    double deadline = Time.realtimeSinceStartupAsDouble + 3;
                    while (Screen.fullScreen != wanted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    ScreenError = Screen.fullScreen == wanted ? null : "화면 모드를 바꾸지 못했습니다. 다시 시도해 주세요";
                    if (request == _request && persist && ScreenError == null) Service.SetFullscreen(Screen.fullScreen);
                }
                if (request != _request) continue;
                ScreenPending = false; ScreenChanged?.Invoke(); yield break;
            }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
