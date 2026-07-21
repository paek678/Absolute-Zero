using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 안아줘요 티셔츠 미니게임 (HugCharacter) — 왕복 커서가 허그(초록) 존에 들어왔을 때 탭 (10초).
    /// 존 안에서 탭 = 성공(초록 팝), 밖에서 탭 = 즉시 실패(빨간 팝). 배너: "안아라!"
    /// (구 청테이프 타이밍 방식 이식 — 10초라 커서 속도 완화)
    /// </summary>
    public class HugCharacterMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "안아라!";

        const float BarHalf = 250f;
        const float CursorSpeed = 330f;      // 10초 → 5초짜리(430)보다 완화
        const float ZoneHalfWidth = 52f;     // 허그 존 살짝 넉넉하게

        static readonly Color HugPink = new(0.95f, 0.5f, 0.6f);

        float _cursorT;
        float _zoneCenterX;
        bool _resolved;
        RectTransform _cursor;
        Image _zone;

        protected override void BuildContent(RectTransform content)
        {
            // 허그 캐릭터 (중앙 하트 그레이박스 — 색감만)
            var heart = CreateCircle(content, "Heart", new Vector2(0f, 60f), 150f, HugPink);
            heart.raycastTarget = false;
            CreateCircle(heart.transform, "Inner", Vector2.zero, 90f, new Color(1f, 0.72f, 0.78f)).raycastTarget = false;

            // 타이밍 바 + 허그 존 + 커서
            CreatePanel(content, "BarBg", new Vector2(0f, -150f), new Vector2(BarHalf * 2f + 20f, 26f),
                new Color(0f, 0f, 0f, 0.65f)).raycastTarget = false;

            _zoneCenterX = Random.Range(-150f, 150f);
            _zone = CreatePanel(content, "HugZone", new Vector2(_zoneCenterX, -150f),
                new Vector2(ZoneHalfWidth * 2f, 22f), new Color(0.25f, 0.8f, 0.35f));
            _zone.raycastTarget = false;

            var cursor = CreatePanel(content, "Cursor", new Vector2(-BarHalf, -150f), new Vector2(8f, 38f), Color.white);
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
