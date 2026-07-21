using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 레드카드 미니게임 (TapCard) — 겹치지 않게 그리드로 배치된 옐로카드들 사이에서 레드카드 1장을 찾아 탭 (5초).
    /// 레드카드 탭 = 성공(초록 팝), 옐로카드 탭 = 즉시 실패(빨간 팝), 시간 초과 = 실패. 배너: "찾아라!"
    /// </summary>
    public class RedCardMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "찾아라!";

        const int Cols = 4;
        const int Rows = 4;
        const float SpacingX = 200f;   // 카드 폭(120)보다 커서 겹치지 않음
        const float SpacingY = 165f;   // 카드 높이(150)보다 커서 겹치지 않음

        static readonly Color CardYellow = new(0.95f, 0.82f, 0.25f);
        static readonly Color CardRed = new(0.9f, 0.22f, 0.2f);
        static readonly Color CardBorder = new(0.15f, 0.15f, 0.2f);

        bool _resolved;

        protected override void BuildContent(RectTransform content)
        {
            int total = Cols * Rows;
            int redIndex = Random.Range(0, total);

            for (int i = 0; i < total; i++)
            {
                int c = i % Cols;
                int r = i / Cols;
                float x = (c - (Cols - 1) * 0.5f) * SpacingX;
                float y = ((Rows - 1) * 0.5f - r) * SpacingY;
                CreateCard(content, i, new Vector2(x, y), i == redIndex);
            }
        }

        void CreateCard(RectTransform content, int index, Vector2 pos, bool isRed)
        {
            // 어두운 테두리 판 + 카드 면 (겹침 없이 그리드 배치)
            var card = CreatePanel(content, $"Card{index}", pos, new Vector2(120f, 150f), CardBorder);
            var face = CreatePanel(card.transform, "Face", Vector2.zero, new Vector2(108f, 138f),
                isRed ? CardRed : CardYellow);
            face.raycastTarget = false;

            var button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = card;
            button.transition = Selectable.Transition.None;
            var cardRect = card.rectTransform;
            button.onClick.AddListener(() => OnCardTapped(isRed, cardRect, face));
        }

        void OnCardTapped(bool isRed, RectTransform cardRect, Image face)
        {
            if (_resolved) return;
            _resolved = true;

            if (isRed)
            {
                PlayConfirmPop(cardRect.anchoredPosition, 170f);   // 성취: 초록 팝
                Finish(true);
            }
            else
            {
                face.color = new Color(0.9f, 0.3f, 0.25f);
                PlayConfirmPop(cardRect.anchoredPosition, 140f, new Color(1f, 0.3f, 0.25f, 0.7f));   // 오답: 빨간 팝
                Finish(false);
            }
        }
    }
}
