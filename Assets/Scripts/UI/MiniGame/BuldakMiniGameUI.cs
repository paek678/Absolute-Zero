using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 불닭볶음면 미니게임 (BoilWater) — 냄비 아이콘을 연타해 물 끓이기 게이지 100% (기획: 10초).
    /// 차오를수록 색이 붉어지고, 목표 달성 순간 게이지 초록 확정 + 확인 팝. 배너: "탭해라!"
    /// </summary>
    public class BuldakMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "탭해라!";

        static readonly Color PotCold = new(0.4f, 0.4f, 0.45f);
        static readonly Color PotBoiling = new(0.95f, 0.3f, 0.15f);
        static readonly Color GaugeCool = new(1f, 0.65f, 0.2f);
        static readonly Color GaugeHot = new(1f, 0.25f, 0.1f);

        int _taps;
        Image _potIcon;
        Image _gaugeFill;
        TextMeshProUGUI _percentText;

        protected override void BuildContent(RectTransform content)
        {
            // 냄비 아이콘이 곧 탭 대상
            var (button, icon) = CreateIconTapTarget(content, "PotTap",
                new Vector2(0f, 0f), new Vector2(380f, 340f), new Vector2(1150f, 1000f),
                ItemIcon, PotCold);
            _potIcon = icon;
            button.onClick.AddListener(OnTap);

            // 끓임 게이지
            CreatePanel(content, "GaugeBg", new Vector2(0f, -195f), new Vector2(400f, 24f),
                new Color(0f, 0f, 0f, 0.65f)).raycastTarget = false;
            var fillGO = new GameObject("GaugeFill");
            fillGO.transform.SetParent(content, false);
            var fillRect = fillGO.AddComponent<RectTransform>();
            fillRect.anchoredPosition = new Vector2(0f, -195f);
            fillRect.sizeDelta = new Vector2(396f, 20f);
            _gaugeFill = fillGO.AddComponent<Image>();
            _gaugeFill.type = Image.Type.Filled;
            _gaugeFill.fillMethod = Image.FillMethod.Horizontal;
            _gaugeFill.fillAmount = 0f;
            _gaugeFill.color = GaugeCool;
            _gaugeFill.raycastTarget = false;

            _percentText = CreateText(content, "Percent", new Vector2(0f, -240f), new Vector2(220f, 38f), "0%", 26);
        }

        void OnTap()
        {
            if (Finished) return;

            _taps++;
            float fill = Mathf.Clamp01((float)_taps / Goal);
            _gaugeFill.fillAmount = fill;
            _gaugeFill.color = Color.Lerp(GaugeCool, GaugeHot, fill);
            _percentText.text = $"{fill * 100f:F0}%";
            _potIcon.color = Color.Lerp(PotCold, PotBoiling, fill);

            if (_taps >= Goal)
            {
                _gaugeFill.color = new Color(0.35f, 0.95f, 0.45f);   // 게이지도 초록으로 확정 표시
                PlayConfirmPop(Vector2.zero, 320f);
                Finish(true);
            }
        }
    }
}
