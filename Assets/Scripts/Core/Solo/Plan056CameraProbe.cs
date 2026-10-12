#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Turn;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbsoluteZero.Core.Solo
{
    // Explicit presentation stimulus only; never enabled in normal gameplay.
    public sealed class Plan056CameraProbe : MonoBehaviour
    {
        public static bool Completed { get; private set; }
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "--az-camera-check") >= 0;
        Camera _camera;
        Quaternion _start;
        bool _watching;
        float _maxAngle;
        float _maxPlaneError;
        int _rendered;
        bool _suite;
        bool _allPassed = true;
        int _observed;
        EnvironmentType _observingEnv;
        readonly EnvironmentType[] _environments = { EnvironmentType.None, EnvironmentType.SunnyDay,
            EnvironmentType.CoolBreeze, EnvironmentType.CicadaSong, EnvironmentType.Kids,
            EnvironmentType.Ambulance, EnvironmentType.SummerVacation, EnvironmentType.HeatWaveWarning };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!Requested) return;
            Completed = false;
            var go = new GameObject(nameof(Plan056CameraProbe)); DontDestroyOnLoad(go); go.AddComponent<Plan056CameraProbe>();
        }
        IEnumerator Start()
        {
            _suite = Array.IndexOf(Environment.GetCommandLineArgs(), "--az-camera-environment-suite") >= 0;
            while (TurnManager.Instance == null || !TurnManager.Instance.IsSpawned
                || TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase) yield return null;
            yield return new WaitForSecondsRealtime(1);
            _camera = Camera.main;
            TurnManager.OnEnvironmentAnnounced += Announced;
            RenderPipelineManager.beginCameraRendering += Rendered;
            if (TurnManager.Instance.IsServer)
            {
                // Isolate explicitly injected presentation cases from automatic turn announcements.
                // This opt-in suite ends with the probe process; normal gameplay never enters here.
                if (_suite) TurnManager.Instance.StopAllCoroutines();
                yield return new WaitForSecondsRealtime(3);
                foreach (var environment in _suite ? _environments : new[] { EnvironmentType.Kids })
                {
                    typeof(TurnManager).GetMethod("PublishEnvironment", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(TurnManager.Instance, new object[] { environment });
                    yield return new WaitForSecondsRealtime(4.2f);
                }
            }
        }
        void Announced(EnvironmentType env)
        {
            if (_watching || Completed) return;
            if (_suite && env != _environments[_observed]) return;
            _observingEnv = env;
            _maxAngle = _maxPlaneError = 0f; _rendered = 0;
            _start = _camera.transform.rotation; _watching = true;
            StartCoroutine(Observe());
        }
        void Rendered(ScriptableRenderContext context, Camera camera)
        {
            if (!_watching || camera != _camera) return;
            _rendered++;
            _maxAngle = Mathf.Max(_maxAngle, Quaternion.Angle(_start, camera.transform.rotation));
            if (DuelSpriteFacing.UsesScreenAlignedArt(MatchCompositionRoot.Instance.ActiveConfig.Mode))
            {
                var views = MatchViewBindings.ForScene(camera.gameObject.scene);
                for (int i = 0; i < 3; i++)
                {
                    var visual = views?.GetRemoteVisual(i);
                    if (visual == null || !visual.gameObject.activeInHierarchy) continue;
                    foreach (var sprite in visual.GetComponentsInChildren<SpriteRenderer>())
                        if (sprite.enabled && sprite.sprite != null) MeasurePlane(sprite.transform, camera);
                }
                foreach (var item in FindObjectsByType<ItemWorldView>(FindObjectsSortMode.None))
                    MeasurePlane(item.transform, camera);
                foreach (var rack in FindObjectsByType<AbsoluteZero.Core.Inventory.MultiPerspectiveLayout>(FindObjectsSortMode.None))
                    foreach (var sprite in rack.GetComponentsInChildren<SpriteRenderer>())
                    {
                        if (!sprite.enabled || sprite.sprite == null) continue;
                        if (sprite.gameObject.name.StartsWith("ItemPlace_"))
                        {
                            // Floor slot art must remain horizontal during camera pans.
                            float floorAngle = Vector3.Angle(sprite.transform.forward, Vector3.up);
                            _maxPlaneError = Mathf.Max(_maxPlaneError, Mathf.Min(floorAngle, 180f - floorAngle));
                        }
                        else MeasurePlane(sprite.transform, camera);
                    }
                foreach (var label in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
                    MeasurePlane(label.transform, camera);
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (canvas.renderMode == RenderMode.WorldSpace) MeasurePlane(canvas.transform, camera);
            }
        }
        void MeasurePlane(Transform art, Camera camera)
        {
            float angle = Vector3.Angle(art.forward, camera.transform.forward);
            _maxPlaneError = Mathf.Max(_maxPlaneError, Mathf.Min(angle, 180f - angle));
        }
        IEnumerator Observe()
        {
            yield return new WaitForSecondsRealtime(1.2f);
            string path = Path.ChangeExtension(Application.consoleLogPath,
                _suite ? $".camera-{_observingEnv}.png" : ".camera.png");
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSecondsRealtime(2.5f);
            bool character = _observingEnv == EnvironmentType.Kids || _observingEnv == EnvironmentType.Ambulance;
            float expected = character ? DuelSpriteFacing.EnvironmentPanDegrees(MatchCompositionRoot.Instance.ActiveConfig.Mode) : 0f;
            float restored = Quaternion.Angle(_start, _camera.transform.rotation);
            bool pass = _rendered > 0 && _maxPlaneError < .1f && Mathf.Abs(_maxAngle - expected) < .1f && restored < .01f;
            string result = $"mode={MatchCompositionRoot.Instance.ActiveConfig.Mode} environment={_observingEnv} rendered={_rendered} maxAngle={_maxAngle:F3} restored={restored:F3} planeError={_maxPlaneError:F3}";
            if (pass) Debug.Log("[CAMERA056] PASS " + result); else Debug.LogError("[CAMERA056] FAIL " + result);
            File.WriteAllText(Path.ChangeExtension(Application.consoleLogPath,
                _suite ? $".camera-{_observingEnv}-result.txt" : ".camera-result.txt"), (pass ? "PASS " : "FAIL ") + result);
            _allPassed &= pass;
            _observed++;
            _watching = false; Completed = !_suite || _observed == _environments.Length;
            if (_suite && Completed)
                File.WriteAllText(Path.ChangeExtension(Application.consoleLogPath, ".camera-result.txt"),
                    (_allPassed ? "PASS " : "FAIL ") + $"environmentCases={_observed}");
        }
        void OnDestroy()
        {
            TurnManager.OnEnvironmentAnnounced -= Announced;
            RenderPipelineManager.beginCameraRendering -= Rendered;
        }
    }
}
#endif
