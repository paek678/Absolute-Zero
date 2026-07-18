using AbsoluteZero.Core.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    public class BuldakMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "탭해라!";

        static readonly Color PotCold = new(0.4f, 0.4f, 0.45f);
        static readonly Color PotBoiling = new(0.95f, 0.3f, 0.15f);

        int _taps;
        Image _potIcon;
        Image _gaugeFill;
        TextMeshProUGUI _percentText;

        protected override void BuildContent(RectTransform content)
        {
            var (button, icon) = CreateIconTapTarget(content, "PotTap",
                new Vector2(0f, 0f), new Vector2(360f, 320f), new Vector2(230f, 200f),
                GameSprites.GetItemSprite("Buldak Noodles"), PotCold);
            _potIcon = icon;
            button.onClick.AddListener(OnTap);

            var gaugeBg = CreatePanel(content, "GaugeBg", new Vector2(0f, -195f), new Vector2(380f, 22f),
                new Color(0f, 0f, 0f, 0.6f));
            var fillGO = new GameObject("GaugeFill");
            fillGO.transform.SetParent(gaugeBg.transform, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);
            _gaugeFill = fillGO.AddComponent<Image>();
            _gaugeFill.type = Image.Type.Filled;
            _gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            _gaugeFill.fillAmount = 0f;
            _gaugeFill.color = new Color(1f, 0.45f, 0.1f);
            _gaugeFill.raycastTarget = false;

            _percentText = CreateText(content, "Percent", new Vector2(0f, -235f), new Vector2(200f, 34f), "0%", 24);
        }

        void OnTap()
        {
            _taps++;
            float fill = Mathf.Clamp01((float)_taps / Goal);
            _gaugeFill.fillAmount = fill;
            _percentText.text = $"{fill * 100f:F0}%";
            _potIcon.color = Color.Lerp(PotCold, PotBoiling, fill);

            if (_taps >= Goal)
                Finish(true);
        }
    }
}
