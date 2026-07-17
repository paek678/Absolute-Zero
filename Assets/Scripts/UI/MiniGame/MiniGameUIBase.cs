using System;
using System.Collections;
using AbsoluteZero.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 미니게임 UI 공통 베이스 (미니멀 리페이스 — PLAN_010).
    /// 프레임: 옅은 딤(하위 UI 입력 차단) + 상단 얇은 타이머 라인 + 중앙 콘텐츠.
    /// 시작 시 와리오웨어식 배너: [윗선 → 문구 → 아랫선] 순서로 왼쪽에서 고속 슬라이드 인
    /// → 잠깐 유지 → 같은 순서로 오른쪽으로 퇴장. 입력은 시작부터 살아있음 (배너는 연출일 뿐).
    /// 판정은 전부 클라이언트, 서버는 시작/종료만 검증 (Q11). 시간 초과 = 실패.
    /// </summary>
    public abstract class MiniGameUIBase : MonoBehaviour
    {
        /// <summary>(slotIndex, success) — 종료 시 1회 발화 후 자신을 파괴</summary>
        public event Action<byte, bool> OnFinished;

        protected byte SlotIndex { get; private set; }
        protected int Goal { get; private set; }
        protected RectTransform Content { get; private set; }

        float _timeLimit;
        float _elapsed;
        bool _finished;
        Image _timerFill;

        protected abstract string BannerText { get; }
        protected abstract void BuildContent(RectTransform content);
        protected virtual void OnTick(float dt) { }

        public void Begin(byte slotIndex, float timeLimit, int goal, Transform canvasRoot)
        {
            SlotIndex = slotIndex;
            Goal = Mathf.Max(1, goal);
            _timeLimit = Mathf.Max(0.5f, timeLimit);
            BuildFrame(canvasRoot);
            BuildContent(Content);
            StartCoroutine(BannerRoutine(BannerText));
        }

        void Update()
        {
            if (_finished || Content == null) return;

            _elapsed += Time.deltaTime;
            if (_timerFill != null)
                _timerFill.fillAmount = Mathf.Max(0f, 1f - _elapsed / _timeLimit);

            if (_elapsed >= _timeLimit)
            {
                Finish(false);   // 시간 초과 = 실패
                return;
            }

            OnTick(Time.deltaTime);
        }

        protected void Finish(bool success)
        {
            if (_finished) return;
            _finished = true;
            OnFinished?.Invoke(SlotIndex, success);
            Destroy(gameObject);
        }

        /// <summary>페이즈 전환 등 외부 강제 종료 — 실패 처리 (서버 타이머가 마스터)</summary>
        public void ForceCancel() => Finish(false);

        void BuildFrame(Transform canvasRoot)
        {
            var rootRect = gameObject.AddComponent<RectTransform>();
            transform.SetParent(canvasRoot, false);
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            // 옅은 딤 — 시야 집중 + 미니게임 중 하위 UI 클릭 차단
            var dim = CreatePanel(rootRect, "Dim", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.35f));
            var dimRect = dim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            // 상단 얇은 타이머 라인 (풀폭)
            var lineBg = new GameObject("TimerLineBg");
            lineBg.transform.SetParent(rootRect, false);
            var lineBgRect = lineBg.AddComponent<RectTransform>();
            lineBgRect.anchorMin = new Vector2(0f, 1f);
            lineBgRect.anchorMax = new Vector2(1f, 1f);
            lineBgRect.pivot = new Vector2(0.5f, 1f);
            lineBgRect.offsetMin = new Vector2(0f, -10f);
            lineBgRect.offsetMax = new Vector2(0f, 0f);
            var lineBgImg = lineBg.AddComponent<Image>();
            lineBgImg.color = new Color(0f, 0f, 0f, 0.6f);
            lineBgImg.raycastTarget = false;

            var fillGO = new GameObject("TimerLineFill");
            fillGO.transform.SetParent(lineBg.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            _timerFill = fillGO.AddComponent<Image>();
            _timerFill.type = Image.Type.Filled;
            _timerFill.fillMethod = Image.FillMethod.Horizontal;
            _timerFill.fillAmount = 1f;
            _timerFill.color = new Color(1f, 0.85f, 0.3f);
            _timerFill.raycastTarget = false;

            // 중앙 콘텐츠 컨테이너
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(rootRect, false);
            Content = contentGO.AddComponent<RectTransform>();
            Content.anchoredPosition = Vector2.zero;
            Content.sizeDelta = new Vector2(900, 700);
        }

        // ═══ 와리오웨어식 배너: 윗선 → 문구 → 아랫선 슬라이드 인/아웃 ═══

        IEnumerator BannerRoutine(string text)
        {
            const float offX = 1400f;      // 화면 밖 시작/종료 x
            const float slideDur = 0.13f;  // 요소당 슬라이드 시간
            const float stagger = 0.07f;   // 요소 간 시차
            const float hold = 0.5f;

            var bannerGO = new GameObject("Banner");
            bannerGO.transform.SetParent(Content, false);
            var bannerRect = bannerGO.AddComponent<RectTransform>();
            bannerRect.anchoredPosition = new Vector2(0f, 40f);
            bannerRect.sizeDelta = Vector2.zero;

            var lineColor = new Color(1f, 0.95f, 0.75f);
            var topLine = CreatePanel(bannerRect, "TopLine", new Vector2(-offX, 72f), new Vector2(560, 8), lineColor);
            var label = CreateText(bannerRect, "Text", new Vector2(-offX, 0f), new Vector2(800, 110), text, 76);
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            var bottomLine = CreatePanel(bannerRect, "BottomLine", new Vector2(-offX, -72f), new Vector2(560, 8), lineColor);

            topLine.raycastTarget = false;
            label.raycastTarget = false;
            bottomLine.raycastTarget = false;

            var rects = new[]
            {
                topLine.GetComponent<RectTransform>(),
                label.GetComponent<RectTransform>(),
                bottomLine.GetComponent<RectTransform>(),
            };

            // 슬라이드 인 (왼쪽 → 중앙, 순차)
            yield return StartCoroutine(SlideAll(rects, -offX, 0f, slideDur, stagger));
            yield return new WaitForSeconds(hold);
            // 슬라이드 아웃 (중앙 → 오른쪽, 같은 순서)
            yield return StartCoroutine(SlideAll(rects, 0f, offX, slideDur, stagger));

            if (bannerGO != null) Destroy(bannerGO);
        }

        IEnumerator SlideAll(RectTransform[] rects, float fromX, float toX, float duration, float stagger)
        {
            float total = duration + stagger * (rects.Length - 1);
            float t = 0f;
            while (t < total)
            {
                t += Time.deltaTime;
                for (int i = 0; i < rects.Length; i++)
                {
                    if (rects[i] == null) continue;
                    float local = Mathf.Clamp01((t - stagger * i) / duration);
                    float eased = local * local * (3f - 2f * local);   // smoothstep — 빠르게 붙는 느낌
                    var pos = rects[i].anchoredPosition;
                    pos.x = Mathf.Lerp(fromX, toX, eased);
                    rects[i].anchoredPosition = pos;
                }
                yield return null;
            }
        }

        // ═══ 공용 빌더 ═══

        protected static TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            UiFont.Apply(tmp);   // 한글 라벨 □ 깨짐 방지
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        protected static Image CreatePanel(Transform parent, string name,
            Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>아이콘 탭 대상 생성 — 히트박스(투명, 넉넉함) + 시각 아이콘(자식). 반환: (버튼, 아이콘 이미지)</summary>
        protected static (Button button, Image icon) CreateIconTapTarget(Transform parent, string name,
            Vector2 pos, Vector2 hitSize, Vector2 iconSize, Sprite iconSprite, Color iconColor)
        {
            // 히트박스: 아이콘보다 넓게 — 연타 미스 방지 (와리오웨어식 관대한 판정)
            var hit = CreatePanel(parent, name, pos, hitSize, new Color(0f, 0f, 0f, 0f));
            var button = hit.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(hit.transform, false);
            var iconRect = iconGO.AddComponent<RectTransform>();
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = iconSize;
            var icon = iconGO.AddComponent<Image>();
            if (iconSprite != null)
            {
                icon.sprite = iconSprite;
                icon.preserveAspect = true;
            }
            icon.color = iconColor;
            icon.raycastTarget = false;

            return (button, icon);
        }
    }
}
