#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.UI.Game.Build;
using AbsoluteZero.UI.Game.Presenters;
using UnityEngine;

namespace AbsoluteZero.UI.Game
{
    // Opt-in view lifetime injection. Never changes authoritative gameplay state.
    public sealed class NotificationLifetimeProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public List<string> checks = new();
            public List<string> captures = new();
            public string failure;
        }
        readonly Report _report = new();
        GameDataBridge _bridge;
        GameUIManager _ui;
        string _folder, _peer;
        int _duplicates, _visibleCount;
        void OnEnable() => Application.logMessageReceived += Logged;
        void OnDisable() => Application.logMessageReceived -= Logged;
        void Logged(string text, string stack, LogType type)
        { if (text.StartsWith("[MatchResult] Visible seq=")) _visibleCount++; }
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        IEnumerator Start()
        {
            _folder = Path.GetDirectoryName(Application.consoleLogPath);
            _peer = Path.GetFileNameWithoutExtension(Application.consoleLogPath);
            var root = GetComponent<GameUIRoot>();
            _bridge = root.Bridge as GameDataBridge;
            _ui = root.UIManager;
            float deadline = Time.realtimeSinceStartup + 80;
            while (_bridge.CurrentMatch.CurrentPhase != TurnPhase.PrepPhase
                || _bridge.LocalSeatIndex == byte.MaxValue)
            {
                if (Time.realtimeSinceStartup > deadline) { Fail("Prep binding timeout"); yield break; }
                yield return null;
            }
            for (int i = 0; i < 2; i++)
            {
                _bridge.enabled = false;
                yield return null;
                if (!Check(!_bridge.TryGetLatestResult(out _), "disabled view has no deliverable result")) yield break;
                _bridge.enabled = true;
                yield return null; yield return null;
                if (!Check(_bridge.LocalSeatIndex != byte.MaxValue
                    && _bridge.CurrentMatch.CurrentPhase == TurnManager.Instance.CurrentPhase.Value,
                    "rebound view reads current phase and local seat")) yield break;
            }
            Capture("prep-rebound");

            deadline = Time.realtimeSinceStartup + 180;
            MatchResultNotice result;
            while (!_bridge.TryGetLatestResult(out result) || !result.IsMatchEnd)
            {
                if (Time.realtimeSinceStartup > deadline) { Fail("Terminal result timeout"); yield break; }
                yield return null;
            }
            yield return new WaitForSecondsRealtime(2.1f);
            if (!Check(_visibleCount == 1, "original result renders exactly once")) yield break;
            Capture("original-terminal");
            _bridge.OnMatchEnd += Duplicate;
            Call(_bridge, "ReadCurrentMatchValues");
            Set(_bridge, "_matchEndTimer", 1f);
            Call(_bridge, "ProcessMatchEnd");
            if (!Check(_duplicates == 0, "duplicate retained terminal does not emit another edge")) yield break;

            var old = (RoundResultPresenter)Get(_ui, "_roundResultPresenter");
            old.Dispose();
            var refs = (GameHudRefs)Get(_ui, "_refs");
            var late = new RoundResultPresenter(_bridge, root.Commands, refs, _ui);
            Set(_ui, "_roundResultPresenter", late);
            if (!Check((ulong)Get(late, "_resultRevision") == result.Revision,
                "late presenter reconciles already delivered terminal")) yield break;
            yield return new WaitForSecondsRealtime(2.1f);
            if (!Check(refs.CinematicOverlay.gameObject.activeInHierarchy
                && refs.LobbyButton.gameObject.activeInHierarchy && _visibleCount == 2,
                "late presenter renders terminal once with lobby action")) yield break;
            Capture("late-terminal");

            _ui.enabled = false;
            _bridge.enabled = false;
            yield return null;
            if (!Check(!(bool)Get(late, "_attached"), "disabled presenter detaches")) yield break;
            _bridge.enabled = true;
            _ui.enabled = true;
            yield return new WaitForSecondsRealtime(2.1f);
            if (!Check((bool)Get(late, "_attached") && refs.CinematicOverlay.gameObject.activeInHierarchy
                && refs.LobbyButton.gameObject.activeInHierarchy && _duplicates == 0 && _visibleCount == 3,
                "re-enabled presenter restores terminal without duplicate bridge delivery")) yield break;
            Capture("reenabled-terminal");
            _bridge.OnMatchEnd -= Duplicate;
            _report.passed = true;
            Save();
            Debug.Log("[NOTIFICATIONS] PASS " + _peer + " checks=" + _report.checks.Count);
        }

        void Duplicate(MatchSnapshot _) => _duplicates++;
        void OnDestroy() { if (_bridge != null) _bridge.OnMatchEnd -= Duplicate; }
        bool Check(bool condition, string text)
        {
            if (!condition) { Fail(text); return false; }
            _report.checks.Add(text); return true;
        }
        void Fail(string message)
        {
            _report.failure = message; Save();
            Debug.LogError("[NOTIFICATIONS] FAIL " + message);
            Application.Quit(2);
        }
        void Capture(string label)
        {
            string path = Path.Combine(_folder, _peer + ".notification-" + label + ".png");
            ScreenCapture.CaptureScreenshot(path);
            _report.captures.Add(path);
        }
        void Save() => File.WriteAllText(Path.Combine(_folder, _peer + ".notifications.json"), JsonUtility.ToJson(_report, true));
        static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
        static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
        static object Call(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    }
}
#endif
