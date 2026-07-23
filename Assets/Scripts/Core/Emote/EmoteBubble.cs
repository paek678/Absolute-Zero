using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.Core.Emote
{
    /// <summary>
    /// 도발 말풍선 — 상대(보낸 사람) 캐릭터 머리의 화면 좌표를 따라다니는 스크린공간 이모티콘.
    /// 크기는 화면 픽셀이라 조정이 직관적. 약 1초 페이드 인/아웃 후 소멸. (효과음 추후)
    /// 크기/위치 조정 노브: BubbleW/BubbleH, HeadWorldUp, ScreenUp.
    /// </summary>
    public class EmoteBubble : MonoBehaviour
    {
        // ── 조정 노브 (화면 픽셀 / 월드 오프셋) ──
        const float BubbleW = 190f;
        const float BubbleH = 210f;
        const float HeadWorldUp = 1.9f;   // 캐릭터 기준 머리 높이(월드)
        const float ScreenUp = 30f;       // 추가로 화면상 위로(픽셀)
        const float ScreenX = -30f;       // 화면상 좌우 보정(픽셀, 음수 = 왼쪽)

        Camera _cam;
        Transform _anchor;      // 따라갈 대상(visual root). null이면 _worldPos 고정
        Vector3 _worldPos;
        Canvas _canvas;
        RectTransform _area;    // 풀스크린 (스크린좌표 → 로컬 변환 기준)
        RectTransform _root;    // 이모지 컨텐츠
        CanvasGroup _cg;

        public static void Show(Transform anchor, Vector3 worldFallback, int emoteId)
        {
            var go = new GameObject("EmoteBubble");
            var b = go.AddComponent<EmoteBubble>();
            b._anchor = anchor;
            b._worldPos = worldFallback;
            b.Build(emoteId);
        }

        void Build(int id)
        {
            _cam = Camera.main;

            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;

            // 해상도 독립적 크기 (MPPM 등 다른 해상도에서도 동일하게 보이도록)
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var areaGO = new GameObject("Area");
            areaGO.transform.SetParent(_canvas.transform, false);
            _area = areaGO.AddComponent<RectTransform>();
            _area.anchorMin = Vector2.zero; _area.anchorMax = Vector2.one;
            _area.offsetMin = Vector2.zero; _area.offsetMax = Vector2.zero;

            var rootGO = new GameObject("Bubble");
            rootGO.transform.SetParent(_area, false);
            _root = rootGO.AddComponent<RectTransform>();
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);   // 캔버스 중앙 기준
            _root.pivot = new Vector2(0.5f, 0.1f);                         // 아래가 머리를 가리키도록
            _root.sizeDelta = new Vector2(BubbleW, BubbleH);
            _cg = rootGO.AddComponent<CanvasGroup>();
            _cg.alpha = 0f;

            // 말풍선 배경 없이 이모지(캐릭터+텍스트)만
            AddImage("Text", new Vector2(0f, BubbleH * 0.78f), new Vector2(BubbleW * 0.9f, 66f), EmoteCatalog.Text(id), Color.white, true);
            AddImage("Char", new Vector2(0f, BubbleH * 0.4f), new Vector2(BubbleW * 0.86f, BubbleW * 0.68f), EmoteCatalog.Char(id), Color.white, true);

            UpdateFollow();
            StartCoroutine(LifeRoutine());
        }

        void AddImage(string name, Vector2 pos, Vector2 size, Sprite sprite, Color color, bool preserve)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            if (sprite != null) { img.sprite = sprite; img.preserveAspect = preserve; }
            img.color = sprite != null ? color : new Color(1f, 1f, 1f, 0.2f);
            img.raycastTarget = false;
        }

        void LateUpdate() => UpdateFollow();

        void UpdateFollow()
        {
            // 플레이 중 스크립트 재컴파일(도메인 리로드)로 필드가 초기화된 잔존 버블 정리
            if (_root == null || _area == null || _canvas == null) { Destroy(gameObject); return; }
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Vector3 wp = (_anchor != null ? _anchor.position : _worldPos) + Vector3.up * HeadWorldUp;
            Vector3 sp = _cam.WorldToScreenPoint(wp);
            bool visible = sp.z > 0f;   // 카메라 뒤면 x/y 미러링 쓰레기값
            _canvas.enabled = visible;

            if (visible &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, new Vector2(sp.x, sp.y), null, out var lp))
                _root.anchoredPosition = lp + new Vector2(ScreenX, ScreenUp);
        }

        IEnumerator LifeRoutine()
        {
            const float inDur = 0.16f, hold = 0.55f, outDur = 0.3f;

            float t = 0f;
            while (t < inDur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / inDur);
                _cg.alpha = p;
                _root.localScale = Vector3.one * (0.55f + 0.45f * EaseOutBack(p));
                yield return null;
            }
            _cg.alpha = 1f;
            _root.localScale = Vector3.one;

            yield return new WaitForSeconds(hold);

            t = 0f;
            while (t < outDur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / outDur);
                _cg.alpha = 1f - p;
                yield return null;
            }
            Destroy(gameObject);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = 1.70158f + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }
    }
}
