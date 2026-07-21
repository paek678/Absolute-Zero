using System;
using System.Collections;
using AbsoluteZero.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 미니게임 UI 공통 베이스.
    /// 프레임: 옅은 딤(하위 UI 입력 차단) + 상단 타이머 라인(시간에 따라 노랑→빨강, 막판 펄스) + 중앙 콘텐츠.
    /// 시작: 와리오웨어식 배너 — 어두운 띠/윗선/문구/아랫선이 왼쪽에서 순차 슬라이드 인 → 유지 → 오른쪽 퇴장.
    /// 종료: 결과는 서버에 즉시 제출(OnFinished — 마감 준수)하고, "성공!/실패…" 아웃트로 연출 후 닫힘.
    /// 판정은 전부 클라이언트, 서버는 시작/종료만 검증 (Q11). 시간 초과 = 실패.
    /// </summary>
    public abstract class MiniGameUIBase : MonoBehaviour
    {
        /// <summary>(slotIndex, success) — 판정 확정 즉시 1회 발화 (아웃트로 연출과 무관하게 먼저 제출)</summary>
        public event Action<byte, bool> OnFinished;

        protected byte SlotIndex { get; private set; }
        protected int Goal { get; private set; }
        protected RectTransform Content { get; private set; }
        protected bool Finished => _finished;

        static readonly Color TimerFresh = new(1f, 0.85f, 0.3f);
        static readonly Color TimerDanger = new(1f, 0.3f, 0.2f);

        float _timeLimit;
        float _elapsed;
        bool _finished;
        Image _timerFill;
        RectTransform _root;

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
            float remain = Mathf.Max(0f, _timeLimit - _elapsed);
            if (_timerFill != null)
            {
                _timerFill.fillAmount = remain / _timeLimit;
                var color = Color.Lerp(TimerDanger, TimerFresh, remain / _timeLimit);
                if (remain < 1.5f)   // 막판 펄스 — 긴박감
                    color.a = 0.55f + 0.45f * Mathf.PingPong(Time.time * 6f, 1f);
                _timerFill.color = color;
            }

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
            OnFinished?.Invoke(SlotIndex, success);          // 서버 제출은 즉시 (마감 준수)
            StartCoroutine(ResultOutroRoutine(success));     // 연출 후 닫힘
        }

        /// <summary>페이즈 전환 등 외부 강제 종료 — 실패 처리 (서버 타이머가 마스터)</summary>
        public void ForceCancel() => Finish(false);

        // ═══ 결과 아웃트로: 성공!/실패… 팝 후 페이드 ═══

        IEnumerator ResultOutroRoutine(bool success)
        {
            // 잔여 입력 차단
            var blocker = CreatePanel(_root, "InputBlocker", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f));
            var blockerRect = blocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;

            var band = CreatePanel(_root, "ResultBand", new Vector2(0f, 20f), new Vector2(4000f, 170f),
                new Color(0f, 0f, 0f, 0.55f));
            band.raycastTarget = false;

            var label = CreateText(_root, "ResultText", new Vector2(0f, 20f), new Vector2(800f, 140f),
                success ? "성공!" : "실패…", 84);
            label.fontStyle = FontStyles.Bold;
            label.color = success ? new Color(0.45f, 1f, 0.5f) : new Color(1f, 0.4f, 0.35f);
            label.raycastTarget = false;

            // 팝 인 (오버슈트)
            var labelRect = label.GetComponent<RectTransform>();
            const float popDur = 0.16f;
            float t = 0f;
            while (t < popDur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / popDur);
                float scale = Mathf.Lerp(0.4f, 1.12f, p);   // 살짝 오버슈트했다가
                labelRect.localScale = Vector3.one * scale;
                yield return null;
            }
            labelRect.localScale = Vector3.one;

            yield return new WaitForSeconds(0.45f);

            // 빠른 페이드 아웃
            const float fadeDur = 0.18f;
            t = 0f;
            var bandColor = band.color;
            var labelColor = label.color;
            while (t < fadeDur)
            {
                t += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(t / fadeDur);
                band.color = new Color(bandColor.r, bandColor.g, bandColor.b, bandColor.a * a);
                label.color = new Color(labelColor.r, labelColor.g, labelColor.b, a);
                yield return null;
            }

            Destroy(gameObject);
        }

        void BuildFrame(Transform canvasRoot)
        {
            _root = gameObject.AddComponent<RectTransform>();
            transform.SetParent(canvasRoot, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            // 옅은 딤 — 시야 집중 + 미니게임 중 하위 UI 클릭 차단
            var dim = CreatePanel(_root, "Dim", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.4f));
            var dimRect = dim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            // 상단 타이머 라인 (풀폭)
            var lineBg = new GameObject("TimerLineBg");
            lineBg.transform.SetParent(_root, false);
            var lineBgRect = lineBg.AddComponent<RectTransform>();
            lineBgRect.anchorMin = new Vector2(0f, 1f);
            lineBgRect.anchorMax = new Vector2(1f, 1f);
            lineBgRect.pivot = new Vector2(0.5f, 1f);
            lineBgRect.offsetMin = new Vector2(0f, -12f);
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
            _timerFill.color = TimerFresh;
            _timerFill.raycastTarget = false;

            // 중앙 콘텐츠 컨테이너
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(_root, false);
            Content = contentGO.AddComponent<RectTransform>();
            Content.anchoredPosition = Vector2.zero;
            Content.sizeDelta = new Vector2(900, 700);
        }

        // ═══ 와리오웨어식 배너: 어두운 띠 → 윗선 → 문구 → 아랫선 슬라이드 인/아웃 ═══

        IEnumerator BannerRoutine(string text)
        {
            const float offX = 1500f;
            const float slideDur = 0.13f;
            const float stagger = 0.06f;
            const float hold = 0.55f;

            var bannerGO = new GameObject("Banner");
            bannerGO.transform.SetParent(_root, false);   // 콘텐츠(과녁 등) 위에 그려지도록 root 직속 + 마지막 sibling
            var bannerRect = bannerGO.AddComponent<RectTransform>();
            bannerRect.anchoredPosition = new Vector2(0f, 40f);
            bannerRect.sizeDelta = Vector2.zero;

            // 가독성 띠 — 밝고 어지러운 콘텐츠(물총 과녁 등) 위에서도 문구가 읽히게
            var band = CreatePanel(bannerRect, "Band", new Vector2(-offX, 0f), new Vector2(4000f, 210f),
                new Color(0f, 0f, 0f, 0.6f));
            var lineColor = new Color(1f, 0.95f, 0.75f);
            var topLine = CreatePanel(bannerRect, "TopLine", new Vector2(-offX, 78f), new Vector2(620, 8), lineColor);
            var label = CreateText(bannerRect, "Text", new Vector2(-offX, 0f), new Vector2(900, 120), text, 82);
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            var bottomLine = CreatePanel(bannerRect, "BottomLine", new Vector2(-offX, -78f), new Vector2(620, 8), lineColor);

            band.raycastTarget = false;
            topLine.raycastTarget = false;
            label.raycastTarget = false;
            bottomLine.raycastTarget = false;

            var rects = new[]
            {
                band.GetComponent<RectTransform>(),
                topLine.GetComponent<RectTransform>(),
                label.GetComponent<RectTransform>(),
                bottomLine.GetComponent<RectTransform>(),
            };

            yield return StartCoroutine(SlideAll(rects, -offX, 0f, slideDur, stagger));
            yield return new WaitForSeconds(hold);
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

        /// <summary>원형 이미지 (절차 생성 원 스프라이트 + 틴트)</summary>
        protected static Image CreateCircle(Transform parent, string name,
            Vector2 pos, float diameter, Color color)
        {
            var img = CreatePanel(parent, name, pos, new Vector2(diameter, diameter), color);
            img.sprite = MiniGameArt.Circle();
            return img;
        }

        static readonly Color ConfirmGreen = new(0.35f, 0.95f, 0.45f, 0.85f);

        /// <summary>
        /// 성공 확인 팝 — 드라이버의 초록 테두리처럼 "됐다"를 그 자리에서 보여주는 공통 피드백.
        /// 지정 위치에서 초록(기본) 원이 퍼지며 사라진다. 모든 게임의 목표 달성 순간에 사용.
        /// </summary>
        protected void PlayConfirmPop(Vector2 pos, float diameter = 140f, Color? color = null)
        {
            if (Content == null) return;
            StartCoroutine(ConfirmPopRoutine(pos, diameter, color ?? ConfirmGreen));
        }

        IEnumerator ConfirmPopRoutine(Vector2 pos, float diameter, Color color)
        {
            var ring = CreateCircle(Content, "ConfirmPop", pos, diameter, color);
            ring.raycastTarget = false;
            var rect = ring.rectTransform;

            const float dur = 0.3f;
            float t = 0f;
            while (t < dur && ring != null)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / dur);
                rect.localScale = Vector3.one * (0.55f + 0.85f * p);
                ring.color = new Color(color.r, color.g, color.b, color.a * (1f - p));
                yield return null;
            }
            if (ring != null) Destroy(ring.gameObject);
        }

        /// <summary>아이콘 탭 대상 생성 — 히트박스(투명, 넉넉함) + 시각 아이콘(자식). 반환: (버튼, 아이콘 이미지)</summary>
        protected static (Button button, Image icon) CreateIconTapTarget(Transform parent, string name,
            Vector2 pos, Vector2 hitSize, Vector2 iconSize, Sprite iconSprite, Color iconColor)
        {
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
