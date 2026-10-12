#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.UI.Game
{
    // Opt-in observation only: does not change match state or item transforms.
    public sealed class VisualPolishProbe : MonoBehaviour
    {
        readonly Dictionary<int, Quaternion> _rotations = new();
        float _nextFloor;
        int _capture;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--az-polish-check") < 0) return;
            var root = new GameObject("VisualPolishProbe");
            DontDestroyOnLoad(root);
            root.AddComponent<VisualPolishProbe>();
        }
        void OnEnable() => DuelSpriteFacing.ArtworkAligned += Floor;
        void OnDisable() => DuelSpriteFacing.ArtworkAligned -= Floor;
        void Floor(Camera camera)
        {
            if (Time.unscaledTime < _nextFloor) return;
            _nextFloor = Time.unscaledTime + 2;
            int count = 0, failed = 0;
            float clearance = float.MaxValue;
            foreach (var contact in FindObjectsByType<InventoryFloorContact>(FindObjectsSortMode.None))
            {
                var art = contact.Art;
                if (art == null || !art.enabled || art.sprite == null || !art.gameObject.activeInHierarchy) continue;
                float min = float.MaxValue;
                foreach (var v in art.sprite.vertices) min = Mathf.Min(min, art.transform.TransformPoint(v).y);
                clearance = Mathf.Min(clearance, min - contact.FloorY);
                count++;
                if (min < contact.FloorY + .039f) failed++;
            }
            if (count > 0) Debug.Log($"[POLISH] FLOOR count={count} failed={failed} clearance={clearance:F4}");
        }
        IEnumerator Start()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(.2f);
                foreach (var fan in FindObjectsByType<FanBladeSpinner>(FindObjectsSortMode.None))
                {
                    var blades = (Transform)typeof(FanBladeSpinner).GetField("blades", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fan);
                    var state = (PlayerState)typeof(FanBladeSpinner).GetMethod("GetPlayerState", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(fan, null);
                    if (blades == null || state == null) continue;
                    int id = fan.GetInstanceID();
                    if (_rotations.TryGetValue(id, out var before) && Time.unscaledTime < 45)
                        Debug.Log($"[POLISH] FAN seat={state.PlayerIndex} active={state.IsFanActive.Value} ready={state.IsReady.Value} delta={Quaternion.Angle(before, blades.localRotation):F2}");
                    _rotations[id] = blades.localRotation;
                }
                if (Time.unscaledTime >= 15 + _capture * 15 && _capture < 5)
                {
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Application.consoleLogPath), $"polish-{++_capture:00}.png"));
                }
            }
        }
    }
}
#endif
