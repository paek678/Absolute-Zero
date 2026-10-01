#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Solo
{
    // Standalone development-player diagnostics; never installed without an explicit argument.
    public sealed class ResourceLifetimeProbe : MonoBehaviour
    {
        FieldInfo _fallback;
        MethodInfo _clearFallback;
        bool _releaseAtQuit, _manualInput;
        float _started;

        static string Arg(string name, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (Application.isEditor || Arg("--az-resource-probe").Length == 0) return;
            var go = new GameObject("ResourceLifetimeProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<ResourceLifetimeProbe>();
        }

        IEnumerator Start()
        {
            _started = Time.realtimeSinceStartup;
            _manualInput = Arg("--az-manual-input", "0") == "1";
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("UnityEngine.U2D.Animation.GpuDeformationSystem"))
                .FirstOrDefault(t => t != null);
            _fallback = type?.GetField("s_FallbackBuffer", BindingFlags.Static | BindingFlags.NonPublic);
            _clearFallback = type?.GetMethod("ClearFallbackBuffer", BindingFlags.Static | BindingFlags.NonPublic);
            _releaseAtQuit = Arg("--az-resource-probe") == "release-fallback-at-quit";
            Application.quitting += Quitting;
            yield return null;
            Debug.Log("[RESOURCE] source=" + (type?.Assembly.FullName ?? "unavailable")
                + " releaseAtQuit=" + _releaseAtQuit);
            bool standalone = Arg("--az-matrix").Length == 0 && Arg("--az-visual-flow").Length == 0;
            float duration = float.TryParse(Arg("--az-resource-seconds", "30"), out float value) ? value : 30f;
            while (true)
            {
                Sample("periodic");
                if (standalone && Time.realtimeSinceStartup - _started >= duration)
                {
                    Application.Quit();
                    yield break;
                }
                yield return new WaitForSecondsRealtime(15f);
            }
        }

        void Sample(string stage)
        {
            var materials = Resources.FindObjectsOfTypeAll<Material>();
            var boundMaterials = new System.Collections.Generic.HashSet<int>(
                Resources.FindObjectsOfTypeAll<Renderer>().SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null).Select(m => m.GetInstanceID()));
            Debug.Log("[RESOURCE] unboundParticleMaterials=" + string.Join(";", materials
                .Where(m => (m.name.StartsWith("HitEffectMat") || m.name.StartsWith("IceBreakEffectMat"))
                    && !boundMaterials.Contains(m.GetInstanceID()))
                .GroupBy(m => m.name).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count())));
            Debug.Log("[RESOURCE] materialCensus=" + string.Join(";", materials
                .GroupBy(m => m.name).OrderBy(g => g.Key).Select(g => g.Key + ":" + g.Count())));
            var buffer = _fallback?.GetValue(null) as ComputeBuffer;
            bool valid = buffer != null && buffer.IsValid();
            Debug.Log("[RESOURCE] sample=" + stage + " seconds=" + (Time.realtimeSinceStartup - _started).ToString("F1")
                + " scene=" + SceneManager.GetActiveScene().name
                + " allocated=" + Profiler.GetTotalAllocatedMemoryLong()
                + " reserved=" + Profiler.GetTotalReservedMemoryLong()
                + " managed=" + Profiler.GetMonoUsedSizeLong()
                + " objects=" + Resources.FindObjectsOfTypeAll<GameObject>().Length
                + " materials=" + materials.Length
                + " textures=" + Resources.FindObjectsOfTypeAll<Texture>().Length
                + " fallbackValid=" + valid + " fallbackId=" + (buffer?.GetHashCode() ?? 0)
                + " fallbackBytes=" + (valid ? buffer.count * buffer.stride : 0));
        }

        void Update()
        {
            if (!_manualInput) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
                Debug.Log("[RESOURCE] physical-Escape");
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                Debug.Log("[RESOURCE] physical-right-click");
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                Debug.Log("[RESOURCE] physical-left-click position=" + mouse.position.ReadValue());
        }

        void Quitting()
        {
            Sample("quitting");
            // A/B experiment only, after all gameplay stops. Not a production cleanup workaround.
            if (_releaseAtQuit && _clearFallback != null)
            {
                _clearFallback.Invoke(null, null);
                Sample("after-diagnostic-release");
            }
        }

        void OnDestroy() => Application.quitting -= Quitting;
    }
}
#endif
