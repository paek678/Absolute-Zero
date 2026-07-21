using AbsoluteZero.Core.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 핫팩 미니게임 (TapRepeat) — 핫팩 아이콘을 제한시간 내 Goal회 연타 (기획 확정: 5초 / 10회).
    /// 연타할수록 색이 달아오르고, 목표 달성 순간 초록 확인 팝. 배너: "탭해라!"
    /// </summary>
    public class HotPackMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "탭해라!";

        static readonly Color ColdColor = new(0.55f, 0.55f, 0.6f);
        static readonly Color HotColor = new(1f, 0.25f, 0.15f);

        int _taps;
        TextMeshProUGUI _countText;
        Image _packIcon;

        protected override void BuildContent(RectTransform content)
        {
            // 아이콘이 곧 탭 대상 — 히트박스는 아이콘보다 넉넉하게
            var (button, icon) = CreateIconTapTarget(content, "PackTap",
                new Vector2(0f, -20f), new Vector2(400f, 400f), new Vector2(250f, 250f),
                GameSprites.GetItemSprite("Hot Pack"), ColdColor);
            _packIcon = icon;
            button.onClick.AddListener(OnTap);

            _countText = CreateText(content, "Count", new Vector2(0f, -245f), new Vector2(320f, 48f), $"0 / {Goal}", 34);
        }

        void OnTap()
        {
            if (Finished) return;

            _taps++;
            float heat = Mathf.Clamp01((float)_taps / Goal);
            _countText.text = $"{_taps} / {Goal}";
            _countText.color = Color.Lerp(Color.white, HotColor, heat);
            _packIcon.color = Color.Lerp(ColdColor, HotColor, heat);

            if (_taps >= Goal)
            {
                PlayConfirmPop(new Vector2(0f, -20f), 340f);   // 목표 달성 순간의 초록 확인 피드백
                Finish(true);
            }
        }
    }
}
