using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 안아줘요 미니게임 (HugCharacter) — 좌우로 왕복하는 커서를 초록 존에 맞춰 탭하는 타이밍 게임.
    /// 존 안에서 탭 = 성공(초록 팝), 밖에서 탭 = 실패(빨강 팝). 배너: "안아라!"
    /// (하트/포커스 블러 연출 없음 — 순수 타이밍 맞추기)
    /// </summary>
    public class HugCharacterMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "안아라!";

        const float BarHalf = 250f;
        const float CursorSpeed = 330f;
        const float ZoneHalfWidth = 52f;

        float _cursorT;
        float _zoneCenterX;
        bool _resolved;
        RectTransform _cursor;
        Image _zone;

        protected override void BuildContent(RectTransform content)
        {
            CreatePanel(content, "BarBg", new Vector2(0f, -20f), new Vector2(BarHalf * 2f + 20f, 26f),
                new Color(0f, 0f, 0f, 0.65f)).raycastTarget = false;

            _zoneCenterX = Random.Range(-150f, 150f);
            _zone = CreatePanel(content, "HugZone", new Vector2(_zoneCenterX, -20f),
                new Vector2(ZoneHalfWidth * 2f, 22f), new Color(0.25f, 0.8f, 0.35f));
            _zone.raycastTarget = false;

            var cursor = CreatePanel(content, "Cursor", new Vector2(-BarHalf, -20f), new Vector2(8f, 38f), Color.white);
            cursor.raycastTarget = false;
            _cursor = cursor.rectTransform;

            var tapArea = CreatePanel(content, "TapArea", Vector2.zero, new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0f));
            var button = tapArea.gameObject.AddComponent<Button>();
            button.targetGraphic = tapArea;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(OnTap);
        }

        protected override void OnTick(float dt)
        {
            if (_resolved) return;

            _cursorT += dt * CursorSpeed;
            var pos = _cursor.anchoredPosition;
            pos.x = Mathf.PingPong(_cursorT, BarHalf * 2f) - BarHalf;
            _cursor.anchoredPosition = pos;
        }

        void OnTap()
        {
            if (_resolved) return;
            _resolved = true;

            var cursorImg = _cursor.GetComponent<Image>();
            bool inZone = Mathf.Abs(_cursor.anchoredPosition.x - _zoneCenterX) <= ZoneHalfWidth;
            if (!inZone)
            {
                cursorImg.color = new Color(0.9f, 0.3f, 0.25f);
                PlayConfirmPop(_cursor.anchoredPosition, 120f, new Color(1f, 0.3f, 0.25f, 0.7f));
                Finish(false);
                return;
            }

            cursorImg.color = new Color(0.45f, 1f, 0.5f);
            _zone.color = new Color(0.45f, 1f, 0.5f);
            PlayConfirmPop(_cursor.anchoredPosition, 150f);
            Finish(true);
        }
    }
}
