using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.Core.Combat
{
    /// <summary>
    /// 화면 전체 VFX 오버레이 — 피격 시 서리(background_VFX1), 회복 시 온기(background_VFX2) 프레임 플래시.
    /// 로컬 플레이어 기준으로만 호출 (CombatVFXManager에서 내가 맞을 때/회복할 때). 페이드 인→아웃.
    /// 지연 생성 싱글턴 — 첫 접근 시 자기 캔버스+오버레이를 만든다. (게임 씬 한정, DDOL 아님)
    /// </summary>
    public class ScreenVFXManager : MonoBehaviour
    {
        static ScreenVFXManager _instance;
        public static ScreenVFXManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("ScreenVFXManager");
                    _instance = go.AddComponent<ScreenVFXManager>();
                }
                return _instance;
            }
        }

        [SerializeField] float peakAlpha = 0.9f;
        [SerializeField] float fadeInDuration = 0.1f;
        [SerializeField] float holdDuration = 0.08f;
        [SerializeField] float fadeOutDuration = 0.35f;

        RawImage _frost;   // 피격 (서리)
        RawImage _warm;    // 회복 (온기)
        Coroutine _flash;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildOverlay();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void BuildOverlay()
        {
            var canvasGO = new GameObject("ScreenVFXCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;   // 미니게임(50) 등보다 위, 화면 최상단 프레임

            _frost = CreateFullScreen(canvasGO.transform, "Frost", "background_VFX1");
            _warm = CreateFullScreen(canvasGO.transform, "Warm", "background_VFX2");
        }

        static RawImage CreateFullScreen(Transform parent, string name, string resourceTex)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var ri = go.AddComponent<RawImage>();
            ri.texture = Resources.Load<Texture2D>(resourceTex);
            ri.raycastTarget = false;
            ri.color = new Color(1f, 1f, 1f, 0f);   // 평소 투명
            return ri;
        }

        /// <summary>피격(온도 하락) — 서리 프레임 플래시</summary>
        public void PlayHitVFX() => Flash(_frost);

        /// <summary>회복(온도 상승) — 온기 프레임 플래시</summary>
        public void PlayRecoveryVFX() => Flash(_warm);

        void Flash(RawImage target)
        {
            if (target == null) return;
            if (_flash != null) StopCoroutine(_flash);
            _flash = StartCoroutine(FlashRoutine(target));
        }

        IEnumerator FlashRoutine(RawImage target)
        {
            // 다른 오버레이는 즉시 숨김 (서리/온기 겹침 방지)
            if (_frost != null && _frost != target) _frost.color = new Color(1f, 1f, 1f, 0f);
            if (_warm != null && _warm != target) _warm.color = new Color(1f, 1f, 1f, 0f);

            float t = 0f;
            while (t < fadeInDuration)
            {
                t += Time.deltaTime;
                SetAlpha(target, Mathf.Lerp(0f, peakAlpha, t / fadeInDuration));
                yield return null;
            }
            SetAlpha(target, peakAlpha);

            if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

            t = 0f;
            while (t < fadeOutDuration)
            {
                t += Time.deltaTime;
                SetAlpha(target, Mathf.Lerp(peakAlpha, 0f, t / fadeOutDuration));
                yield return null;
            }
            SetAlpha(target, 0f);
            _flash = null;
        }

        static void SetAlpha(RawImage ri, float a)
        {
            if (ri != null) ri.color = new Color(1f, 1f, 1f, a);
        }
    }
}
