using System;
using System.Collections;
using AbsoluteZero.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    public abstract class MiniGameUIBase : MonoBehaviour
    {
        public event Action<byte, bool> OnFinished;

        protected byte SlotIndex { get; private set; }
        protected int Goal { get; private set; }
        protected RectTransform Content { get; private set; }
        protected bool Finished => _finished;
        protected Sprite ItemIcon { get; private set; }

        static readonly Color TimerFresh = new(1f, 0.85f, 0.3f);
        static readonly Color TimerDanger = new(1f, 0.3f, 0.2f);
        static readonly WaitForSeconds _waitResultHold = new(0.45f);
        static readonly WaitForSeconds _waitBannerHold = new(0.55f);

        float _timeLimit;
        float _elapsed;
        bool _finished;
        Image _timerFill;
        RectTransform _root;

        protected abstract string BannerText { get; }
        protected abstract void BuildContent(RectTransform content);
        protected virtual void OnTick(float dt) { }

        public void Begin(byte slotIndex, float timeLimit, int goal, Transform canvasRoot, Sprite itemIcon = null)
        {
            SlotIndex = slotIndex;
            Goal = Mathf.Max(1, goal);
            _timeLimit = Mathf.Max(0.5f, timeLimit);
            ItemIcon = itemIcon;
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
                if (remain < 1.5f)
                    color.a = 0.55f + 0.45f * Mathf.PingPong(Time.time * 6f, 1f);
                _timerFill.color = color;
            }

            if (_elapsed >= _timeLimit)
            {
                Finish(false);
                return;
            }

            OnTick(Time.deltaTime);
        }

        protected void Finish(bool success)
        {
            if (_finished) return;
            _finished = true;
            OnFinished?.Invoke(SlotIndex, success);
            StartCoroutine(ResultOutroRoutine(success));
        }

        public void ForceCancel() => Finish(false);

        IEnumerator ResultOutroRoutine(bool success)
        {
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

            var labelRect = label.GetComponent<RectTransform>();
            const float popDur = 0.16f;
            float t = 0f;
            while (t < popDur)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / popDur);
                float scale = Mathf.Lerp(0.4f, 1.12f, p);
                labelRect.localScale = Vector3.one * scale;
                yield return null;
            }
            labelRect.localScale = Vector3.one;

            yield return _waitResultHold;

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

            var dim = CreatePanel(_root, "Dim", Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.4f));
            var dimRect = dim.GetComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

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

            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(_root, false);
            Content = contentGO.AddComponent<RectTransform>();
            Content.anchoredPosition = Vector2.zero;
            Content.sizeDelta = new Vector2(900, 700);
        }

        IEnumerator BannerRoutine(string text)
        {
            const float offX = 1500f;
            const float slideDur = 0.13f;
            const float stagger = 0.06f;

            var bannerGO = new GameObject("Banner");
            bannerGO.transform.SetParent(_root, false);
            var bannerRect = bannerGO.AddComponent<RectTransform>();
            bannerRect.anchoredPosition = new Vector2(0f, 40f);
            bannerRect.sizeDelta = Vector2.zero;

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
            yield return _waitBannerHold;
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
                    float eased = local * local * (3f - 2f * local);
                    var pos = rects[i].anchoredPosition;
                    pos.x = Mathf.Lerp(fromX, toX, eased);
                    rects[i].anchoredPosition = pos;
                }
                yield return null;
            }
        }

        protected static TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            UiFont.Apply(tmp);
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

        protected static Image CreateCircle(Transform parent, string name,
            Vector2 pos, float diameter, Color color)
        {
            var img = CreatePanel(parent, name, pos, new Vector2(diameter, diameter), color);
            img.sprite = MiniGameArt.Circle();
            return img;
        }

        static readonly Color ConfirmGreen = new(0.35f, 0.95f, 0.45f, 0.85f);

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
