#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbsoluteZero.Core.Solo
{
    // Explicit opt-in visual timing check using the real server death presentation.
    public sealed class Plan063DeathTimingProbe : MonoBehaviour
    {
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(), "--az-death-timing-check") >= 0;
        public static bool Completed { get; private set; }
        AZPlayerVisual _visual;
        Transform _root;
        ParticleSystem _ice;
        float _breakAt = -1f;
        bool _frozenCaptured;
        bool _bodyAtBreak;
        bool _bodyDuringGap;
        float _deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!Requested) return;
            Completed = false;
            var go = new GameObject(nameof(Plan063DeathTimingProbe));
            DontDestroyOnLoad(go); go.AddComponent<Plan063DeathTimingProbe>();
        }

        IEnumerator Start()
        {
            _deadline = Time.realtimeSinceStartup + 100;
            while (TurnManager.Instance == null || !TurnManager.Instance.IsSpawned
                || TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase) yield return null;
            yield return new WaitForSecondsRealtime(3);
            _visual = FindObjectsByType<PlayerState>(FindObjectsSortMode.None)
                .First(p => p.PlayerIndex == 1).GetComponent<AZPlayerVisual>();
            _root = _visual.GetVisualRoot();
            if (_root == null)
            {
                while (!_visual.IsGhost && !Completed) yield return null;
                if (!Completed) Finish(true, "local victim uses FPS; remote body checked by other peers");
                yield break;
            }
            _ice = _root.Find("IceBreakEffect")?.GetComponent<ParticleSystem>();
            RenderPipelineManager.beginCameraRendering += Observe;
            if (TurnManager.Instance.IsServer)
            {
                yield return new WaitForSecondsRealtime(2);
                if (!TurnManager.Instance.DebugForceGhostForVisual(1)) Finish(false, "server stimulus rejected");
            }
        }

        void Update()
        {
            if (!Completed && _deadline > 0 && Time.realtimeSinceStartup > _deadline) Finish(false, "timed out");
        }

        void Observe(ScriptableRenderContext context, Camera camera)
        {
            if (Completed || camera != Camera.main || _root == null) return;
            var freeze = _root.Find("freezeice");
            var ghost = _root.Find("GhostPlaceholder");
            bool body = _root.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.sprite != null
                && r.color.a > .01f && r.transform != freeze
                && (ghost == null || !r.transform.IsChildOf(ghost)) && r.name != "item");
            if (!_frozenCaptured && _visual.IsDead && freeze != null && freeze.gameObject.activeInHierarchy)
            {
                _frozenCaptured = true; Capture("frozen");
            }
            if (_breakAt < 0 && _visual.IsDead && _ice != null && _ice.isPlaying)
            {
                _breakAt = Time.realtimeSinceStartup;
                _bodyAtBreak = body;
                Capture("break-hidden");
            }
            if (_breakAt < 0) return;
            if (ghost == null || !ghost.gameObject.activeInHierarchy) { _bodyDuringGap |= body; return; }
            float delay = Time.realtimeSinceStartup - _breakAt;
            Capture("ghost");
            Finish(!_bodyAtBreak && !_bodyDuringGap && delay >= .48f && delay < .8f,
                $"bodyAtBreak={_bodyAtBreak} bodyDuringGap={_bodyDuringGap} revealDelay={delay:F3}s");
        }

        void Capture(string stage) => ScreenCapture.CaptureScreenshot(Path.ChangeExtension(Application.consoleLogPath, ".death-" + stage + ".png"));
        void Finish(bool pass, string detail)
        {
            Completed = true;
            string result = (pass ? "PASS " : "FAIL ") + detail;
            File.WriteAllText(Path.ChangeExtension(Application.consoleLogPath, ".death-result.txt"), result);
            if (pass) Debug.Log("[DEATH063] " + result); else Debug.LogError("[DEATH063] " + result);
        }
        void OnDestroy() => RenderPipelineManager.beginCameraRendering -= Observe;
    }
}
#endif
