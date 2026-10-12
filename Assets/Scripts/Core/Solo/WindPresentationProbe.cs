#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Solo
{
    // Opt-in player-build rendering evidence. Never runs in normal gameplay.
    public sealed class WindPresentationProbe : MonoBehaviour
    {
        bool _observing;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--az-wind-check") < 0) return;
            var go = new GameObject(nameof(WindPresentationProbe));
            DontDestroyOnLoad(go);
            go.AddComponent<WindPresentationProbe>();
        }
        IEnumerator Start()
        {
            while (TurnManager.Instance == null || !TurnManager.Instance.IsSpawned)
                yield return null;
            TurnManager.OnEnvironmentAnnounced += Announced;
            yield return new WaitForSecondsRealtime(5);
            if (TurnManager.Instance.IsServer)
                typeof(TurnManager).GetMethod("PublishEnvironment", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(TurnManager.Instance, new object[] { EnvironmentType.CoolBreeze });
        }
        void Announced(EnvironmentType type)
        {
            if (type != EnvironmentType.CoolBreeze || _observing) return;
            _observing = true;
            StartCoroutine(Capture());
        }
        IEnumerator Capture()
        {
            string prefix = Path.ChangeExtension(Application.consoleLogPath, ".wind");
            yield return new WaitForSecondsRealtime(2);
            for (int i = 0; i < 4; i++)
            {
                ScreenCapture.CaptureScreenshot(prefix + "-" + i + ".png");
                yield return new WaitForSecondsRealtime(3);
            }
            var wind = GameObject.Find("WindVFX");
            if (wind == null) { File.WriteAllText(prefix + ".txt", "FAIL missing wind"); yield break; }
            var ps = wind.GetComponent<ParticleSystem>();
            var renderer = wind.GetComponent<ParticleSystemRenderer>();
            var mat = renderer.trailMaterial;
            File.WriteAllText(prefix + ".txt", "shader=" + mat.shader.name + "\nsupported=" + mat.shader.isSupported
                + "\nkeywords=" + string.Join(",", mat.shaderKeywords) + "\nqueue=" + mat.renderQueue
                + "\ncolor=" + mat.color + "\nparticles=" + ps.particleCount + "\nbounds=" + renderer.bounds
                + "\nscale=" + ps.transform.lossyScale);
            // Same-frame render toggle verifies that observed geometry belongs to wind.
            yield return new WaitForEndOfFrame();
            renderer.enabled = false;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(prefix + "-disabled.png");
            yield return new WaitForSecondsRealtime(1);
            renderer.enabled = true;
            var coolerField = typeof(EnvironmentVFXManager).GetField("_coolerObj", BindingFlags.Instance | BindingFlags.NonPublic);
            var cooler = (GameObject)coolerField.GetValue(EnvironmentVFXManager.Instance);
            bool coolerActive = cooler != null && cooler.activeSelf;
            File.AppendAllText(prefix + ".txt", "\ncamera=" + Camera.main.transform.position
                + "\ncoolerActive=" + coolerActive + "\ncooler=" + (cooler != null ? cooler.transform.position.ToString() : "missing"));
            if (cooler != null) cooler.SetActive(false);
            yield return new WaitForSecondsRealtime(1);
            ScreenCapture.CaptureScreenshot(prefix + "-cooler-disabled.png");
            yield return new WaitForSecondsRealtime(1);
            if (cooler != null) cooler.SetActive(coolerActive);
            var manager = EnvironmentVFXManager.Instance;
            var announce = typeof(EnvironmentVFXManager).GetMethod("OnEnvironmentAnnounced", BindingFlags.Instance | BindingFlags.NonPublic);
            announce.Invoke(manager, new object[] { EnvironmentType.None });
            bool reset = !wind.activeSelf && (cooler == null || !cooler.activeSelf);
            announce.Invoke(manager, new object[] { EnvironmentType.CoolBreeze });
            bool multi = MatchCompositionRoot.Instance?.ActiveConfig?.Mode == GameMode.Multi;
            bool replay = wind.activeSelf && cooler != null && cooler.activeSelf == !multi;
            File.AppendAllText(prefix + ".txt", "\nreset=" + reset + "\nreplay=" + replay + "\nmode=" + MatchCompositionRoot.Instance?.ActiveConfig?.Mode);
            Debug.Log("[WIND] Capture completed: " + prefix);
        }
        void OnDestroy() => TurnManager.OnEnvironmentAnnounced -= Announced;
    }
}
#endif
