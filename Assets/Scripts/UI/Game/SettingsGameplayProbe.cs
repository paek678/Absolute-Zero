#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AbsoluteZero.Core.Audio;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Presenters;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.UI.Game
{
    // Explicit presentation-only fixture. Network/turn state is never written.
    public sealed class SettingsGameplayProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed; public int peer, gameplayViews;
            public float master; public bool shake; public string failure;
            public List<string> checks = new(), scenes = new();
        }
        readonly Report _report = new();
        readonly HashSet<int> _seen = new();
        LocalSettingsRuntime _runtime;
        LocalSettingsSnapshot _expected;
        string _path;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Application.isEditor) return;
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "--az-settings-probe");
            if (at < 0 || at + 1 >= args.Length || !int.TryParse(args[at + 1], out int peer) || peer < 0 || peer > 3) return;
            var go = new GameObject(nameof(SettingsGameplayProbe)); DontDestroyOnLoad(go);
            var probe = go.AddComponent<SettingsGameplayProbe>(); probe._report.peer = peer;
        }
        void Start()
        {
            _runtime = LocalSettingsRuntime.Instance; _expected = _runtime.Service.Current;
            _report.master = _expected.Master; _report.shake = _expected.Shake;
            _path = Path.ChangeExtension(Application.consoleLogPath, ".settings.json");
            try { Check(Mathf.Approximately(_expected.Master, _report.peer / 3f) && _expected.Shake == (_report.peer % 2 == 1), "isolated per-process settings loaded"); }
            catch (Exception error) { Fail(error); }
        }
        void Update()
        {
            if (_runtime == null || _report.failure != null) return;
            try
            {
                CheckSilent(_runtime == LocalSettingsRuntime.Instance && _runtime.Service.Current.Same(_expected), "settings changed during scene flow");
                string scene = SceneManager.GetActiveScene().name;
                if (_report.scenes.Count == 0 || _report.scenes[^1] != scene) { _report.scenes.Add(scene); Save(); }
                var audio = GameAudioManager.Instance;
                if (audio != null)
                    foreach (var source in audio.GetComponentsInChildren<AudioSource>())
                    {
                        float basis = source.name switch { "Audio_BGM" => .35f, "Audio_SFX" => .7f, "Audio_UI" => .5f,
                            "Audio_ENV" => .6f, "Audio_FanLoop" => .15f, "Audio_Clock" => .5f, _ => -1 };
                        CheckSilent(basis >= 0 && Mathf.Approximately(source.volume,
                            basis * (source.name == "Audio_BGM" ? _expected.Bgm : _expected.Sfx) * _expected.Master), "audio settings drift: " + source.name);
                    }
                var hud = FindAnyObjectByType<MatchHudPresenter>();
                if (hud == null || _seen.Contains(hud.GetInstanceID()) || !(bool)Get(hud, "_initialized")) return;
                var bridge = (IGameDataBridge)Get(hud, "_bridge");
                if (bridge.CurrentMatch.CurrentPhase != TurnPhase.PrepPhase) return;
                VerifyHud(hud, bridge);
                _seen.Add(hud.GetInstanceID()); _report.gameplayViews++;
                _report.passed = true; Save();
                Debug.Log("[SETTINGS_GAMEPLAY] PASS peer=" + _report.peer + " scene=" + scene + " checks=" + _report.checks.Count);
            }
            catch (Exception error) { Fail(error); }
        }
        void VerifyHud(MatchHudPresenter hud, IGameDataBridge bridge)
        {
            var service = _runtime.Service;
            var alarm = (GameObject)Get(hud, "_timeAlarmObj");
            var alarmRect = alarm.GetComponent<RectTransform>();
            var timerRect = (RectTransform)Get(hud, "_timerContainerRT");
            var originalRandom = UnityEngine.Random.state;
            bool originalSummer = (bool)Get(hud, "_summerVacShaking");
            Call(hud, "ClearShakeOffsets");
            Vector2 alarmBase = alarmRect.anchoredPosition, timerBase = timerRect.anchoredPosition;
            try
            {
                var example = bridge.CurrentMatch; example.RemainingTime = 3;
                service.SetShake(false); Call(hud, "UpdateTimerDisplay", example);
                var timerText = (TextMeshProUGUI)Get(hud, "_timerText");
                var clock = GameAudioManager.Instance.GetComponentsInChildren<AudioSource>().Single(s => s.name == "Audio_Clock");
                Check(alarm.activeSelf && timerText.text == "3" && timerText.color == Color.red && clock.isPlaying,
                    "shake off retains alarm, red countdown and running clock sound");
                Call(hud, "LateUpdate");
                Check(alarmRect.anchoredPosition == alarmBase && timerRect.anchoredPosition == timerBase, "shake off keeps HUD still");
                var table = new ItemDropTable(ItemManager.Instance.GetAllItems());
                Check(!table.IsEmpty, "real item drop table available");
                var before = UnityEngine.Random.state;
                var expectedDrop = table.Roll(); var afterDrop = UnityEngine.Random.state;
                UnityEngine.Random.state = before;
                service.SetShake(true); Set(hud, "_summerVacShaking", true);
                for (int i = 0; i < 240; i++) { Set(hud, "_nextAlarmJitter", 0f); Call(hud, "LateUpdate"); }
                Check(UnityEngine.Random.state.Equals(before), "240 real HUD alarm/summer updates preserve game RNG state");
                Check(table.Roll() == expectedDrop && UnityEngine.Random.state.Equals(afterDrop), "real weighted drop identical with decorative updates interleaved");
                Check(alarmRect.anchoredPosition != alarmBase, "enabled alarm shake moves");
                service.SetShake(false);
                Check(alarmRect.anchoredPosition == alarmBase && timerRect.anchoredPosition == timerBase, "turning shake off immediately restores both offsets");
                service.SetShake(true); Set(hud, "_nextAlarmJitter", 0f); Call(hud, "LateUpdate");
                hud.enabled = false;
                Check(alarmRect.anchoredPosition == alarmBase && timerRect.anchoredPosition == timerBase, "disabling HUD clears owned offsets");
                hud.enabled = true;
                Set(hud, "_nextAlarmJitter", 0f); Call(hud, "LateUpdate");
                alarmRect.anchoredPosition = alarmBase + new Vector2(23, 17);
                service.SetShake(false);
                Check(alarmRect.anchoredPosition == alarmBase + new Vector2(23, 17), "shake cleanup preserves external layout move");
                alarmRect.anchoredPosition = alarmBase;
            }
            finally
            {
                UnityEngine.Random.state = originalRandom;
                hud.enabled = true; Call(hud, "ClearShakeOffsets");
                Set(hud, "_summerVacShaking", originalSummer);
                service.SetShake(_expected.Shake);
                Call(hud, "UpdateTimerDisplay", bridge.CurrentMatch);
            }
        }
        static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
        static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
        void Check(bool value, string message) { CheckSilent(value, message); _report.checks.Add(message); }
        static void CheckSilent(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        void Save() { if (_path != null) File.WriteAllText(_path, JsonUtility.ToJson(_report, true)); }
        void Fail(Exception error) { _report.failure = error.ToString(); _report.passed = false; Save(); Debug.LogError("[SETTINGS_GAMEPLAY] FAIL " + error); Application.Quit(2); }
        void OnApplicationQuit() { if (_report.failure == null) Save(); }
    }
}
#endif
