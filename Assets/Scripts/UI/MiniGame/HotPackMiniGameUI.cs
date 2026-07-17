using AbsoluteZero.Core.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 핫팩 미니게임 (TapRepeat) — 핫팩 아이콘을 제한시간 내 Goal회 연타 (기획 확정: 5초 / 10회).
    /// 연타할수록 아이콘이 붉게 달아오르며 커진다. 배너: "탭해라!"
    /// </summary>
    public class HotPackMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "탭해라!";

        static readonly Color ColdColor = new(0.55f, 0.55f, 0.6f);
        static readonly Color HotColor = new(1f, 0.25f, 0.15f);

        int _taps;
        TextMeshProUGUI _countText;
        Image _packIcon;
        RectTransform _packRect;

        protected override void BuildContent(RectTransform content)
        {
            // 아이콘이 곧 탭 대상 — 히트박스는 아이콘보다 넉넉하게
            var (button, icon) = CreateIconTapTarget(content, "PackTap",
                new Vector2(0f, -20f), new Vector2(380f, 380f), new Vector2(260f, 260f),
                GameSprites.GetItemSprite("Hot Pack"), ColdColor);
            _packIcon = icon;
            _packRect = icon.GetComponent<RectTransform>();
            button.onClick.AddListener(OnTap);

            _countText = CreateText(content, "Count", new Vector2(0f, -235f), new Vector2(300f, 44f), $"0 / {Goal}", 32);
        }

        void OnTap()
        {
            _taps++;
            _countText.text = $"{_taps} / {Goal}";

            float t = Mathf.Clamp01((float)_taps / Goal);
            _packIcon.color = Color.Lerp(ColdColor, HotColor, t);
            _packRect.localScale = Vector3.one * (1f + 0.2f * t);   // 달아오를수록 커짐

            if (_taps >= Goal)
                Finish(true);
        }
    }
}
