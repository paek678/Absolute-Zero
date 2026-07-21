using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    public class RedCardMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "찾아라!";

        const int Cols = 4;
        const int Rows = 4;
        const float SpacingX = 200f;
        const float SpacingY = 165f;

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
                PlayConfirmPop(cardRect.anchoredPosition, 170f);
                Finish(true);
            }
            else
            {
                face.color = new Color(0.9f, 0.3f, 0.25f);
                PlayConfirmPop(cardRect.anchoredPosition, 140f, new Color(1f, 0.3f, 0.25f, 0.7f));
                Finish(false);
            }
        }
    }
}
