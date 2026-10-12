using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Common
{
    /// <summary>Presentation-only skin, applied explicitly by UI owners.</summary>
    public static class CasualUiSkin
    {
        public static readonly Color Ink = new(.23f, .19f, .17f);
        public static readonly Color Muted = new(.43f, .39f, .35f);
        public static readonly Color Cream = Color.white;
        public static readonly Color Mint = new(.73f, .91f, .85f);
        public static readonly Color Sky = new(.78f, .89f, .98f);
        public static readonly Color Butter = new(1f, .94f, .75f);
        public static readonly Color Peach = new(1f, .79f, .70f);
        public static readonly Color Lavender = new(.86f, .82f, .96f);
        static Sprite _panel, _pill, _card;
        static TMP_FontAsset _font;
        public static Sprite Panel => _panel != null ? _panel : _panel = Resources.Load<Sprite>("UI/Casual/Panel");
        public static Sprite Pill => _pill != null ? _pill : _pill = Resources.Load<Sprite>("UI/Casual/Pill");
        public static Sprite Card => _card != null ? _card : _card = Resources.Load<Sprite>("UI/Casual/Card");
        public static TMP_FontAsset Font => _font != null ? _font : _font = Resources.Load<TMP_FontAsset>("UI/Fonts/GmarketBanner");

        public static void Surface(Image image, Color color, Sprite sprite = null, float density = 3f)
        {
            if (image == null) return;
            image.sprite = sprite != null ? sprite : Panel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = density;
            image.color = color;
            // Keep the owner's raycast, masking and RectTransform contract.
        }

        public static void Label(TMP_Text text, Color? color = null)
        {
            if (text == null) return;
            if (Font != null) text.font = Font;
            text.fontStyle = FontStyles.Normal; // Native Bold; no synthetic dilation.
            text.color = color ?? Ink;
        }

        public static void Labels(Transform root, Color? color = null)
        {
            if (root == null) return;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) Label(text, color);
        }

        public static void Button(Button button, Color tint, bool card = false)
        {
            if (button == null) return;
            var image = button.targetGraphic as Image;
            if (image == null) image = button.GetComponent<Image>();
            Surface(image, Color.white, card ? Card : Pill, card ? 4f : 8f);
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = tint;
            colors.highlightedColor = Color.Lerp(tint, Color.white, .35f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = Color.Lerp(tint, new Color(.65f, .69f, .66f), .25f);
            colors.disabledColor = new Color(.78f, .78f, .76f, .7f);
            colors.colorMultiplier = 1f; colors.fadeDuration = .1f;
            button.colors = colors;
            Labels(button.transform);
        }

        public static void At(Transform root, string path, Color tint, Sprite sprite = null, float density = 3f)
        {
            var target = root != null ? root.Find(path) : null;
            if (target != null) Surface(target.GetComponent<Image>(), tint, sprite, density);
        }

        public static void Slider(Slider slider)
        {
            if (slider == null) return;
            At(slider.transform, "Background", Cream, Pill, 24f);
            if (slider.fillRect != null) Surface(slider.fillRect.GetComponent<Image>(), Mint, Pill, 24f);
            if (slider.handleRect != null)
            {
                var handle = slider.handleRect;
                // Slider drives the thumb's anchors again whenever its value changes.
                // Give its travel area zero height, so that driven stretch still yields a circle.
                if (handle.parent is RectTransform area && area.name == "HandleSlideArea")
                {
                    area.anchorMin = new Vector2(area.anchorMin.x, .5f);
                    area.anchorMax = new Vector2(area.anchorMax.x, .5f);
                    area.sizeDelta = new Vector2(area.sizeDelta.x, 0);
                }
                handle.sizeDelta = new Vector2(36, 36);
                Surface(handle.GetComponent<Image>(), Butter, Pill, 10f);
            }
        }

        public static void Toggle(Toggle toggle)
        {
            if (toggle == null || toggle.targetGraphic is not Image background) return;
            background.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 76);
            background.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 40);
            Surface(background, toggle.isOn ? Mint : new Color(.82f, .81f, .78f), Pill, 12);
            // Retain the original toggle graphic/binding; a separate thumb is visible in both states.
            if (toggle.graphic != null) toggle.graphic.enabled = false;
            var thumb = background.transform.Find("CasualThumb") as RectTransform;
            if (thumb == null)
            {
                var go = new GameObject("CasualThumb", typeof(RectTransform), typeof(Image));
                thumb = go.GetComponent<RectTransform>(); thumb.SetParent(background.transform, false);
            }
            thumb.anchorMin = thumb.anchorMax = new Vector2(.5f, .5f);
            thumb.sizeDelta = new Vector2(30, 30); thumb.anchoredPosition = new Vector2(toggle.isOn ? 16 : -16, 0);
            var image = thumb.GetComponent<Image>(); Surface(image, Cream, Pill, 14); image.raycastTarget = false;
        }
    }
}
