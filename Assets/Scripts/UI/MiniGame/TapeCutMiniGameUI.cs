using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 청테이프 미니게임 (TimingCut) — 왼쪽 롤에서 테이프를 쭉 뽑아내는 연출 + 왕복 커서 타이밍 바 (5초).
    /// 테이프가 시간에 따라 점점 길어지고, 초록 영역 안에서 끊으면 성공. 배너: "끊어라!"
    /// </summary>
    public class TapeCutMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "끊어라!";

        const float BarHalf = 250f;
        const float CursorSpeed = 430f;
        const float GreenHalfWidth = 46f;

        static readonly Color TapeBlue = new(0.35f, 0.55f, 0.85f);
        static readonly Color TapeDark = new(0.25f, 0.4f, 0.7f);
        static readonly Color RollColor = new(0.28f, 0.48f, 0.78f);

        const float RollX = -280f;
        const float TapeY = 40f;
        const float TapeStartWidth = 40f;
        const float TapeMaxWidth = 480f;

        float _cursorT;
        float _pullT;
        float _greenCenterX;
        bool _resolved;
        RectTransform _cursor;
        RectTransform _tapeRect;
        Image _tapeImg;
        Image _sheenImg;
        RectTransform _rollRect;
        Image _greenZone;

        protected override void BuildContent(RectTransform content)
        {
            // 테이프 롤 (왼쪽 원형 — 바깥 원 + 안 구멍)
            var roll = CreateCircle(content, "Roll", new Vector2(RollX, TapeY), 90f, RollColor);
            roll.raycastTarget = false;
            _rollRect = roll.rectTransform;
            CreateCircle(roll.transform, "Hole", Vector2.zero, 28f, new Color(0.15f, 0.15f, 0.2f)).raycastTarget = false;
            // 롤 위 광택 (슬래시 라인)
            CreatePanel(roll.transform, "Sheen", new Vector2(8f, 12f), new Vector2(14f, 50f),
                new Color(1f, 1f, 1f, 0.15f)).raycastTarget = false;

            // 뽑아지는 테이프 (롤 오른쪽 끝에서 시작, 점점 오른쪽으로 길어짐)
            var tapeGO = new GameObject("Tape");
            tapeGO.transform.SetParent(content, false);
            _tapeRect = tapeGO.AddComponent<RectTransform>();
            _tapeRect.pivot = new Vector2(0f, 0.5f);
            _tapeRect.anchoredPosition = new Vector2(RollX + 45f, TapeY);
            _tapeRect.sizeDelta = new Vector2(TapeStartWidth, 36f);
            _tapeImg = tapeGO.AddComponent<Image>();
            _tapeImg.color = TapeBlue;
            _tapeImg.raycastTarget = false;

            // 테이프 위 광택 줄
            var sheenGO = new GameObject("TapeSheen");
            sheenGO.transform.SetParent(tapeGO.transform, false);
            var sheenRect = sheenGO.AddComponent<RectTransform>();
            sheenRect.anchorMin = new Vector2(0f, 0.65f);
            sheenRect.anchorMax = new Vector2(1f, 0.85f);
            sheenRect.offsetMin = new Vector2(4f, 0f);
            sheenRect.offsetMax = new Vector2(-4f, 0f);
            _sheenImg = sheenGO.AddComponent<Image>();
            _sheenImg.color = new Color(1f, 1f, 1f, 0.2f);
            _sheenImg.raycastTarget = false;

            // 테이프 끝 (찢어진 가장자리 — 삼각)
            var endGO = new GameObject("TapeEnd");
            endGO.transform.SetParent(tapeGO.transform, false);
            var endRect = endGO.AddComponent<RectTransform>();
            endRect.anchorMin = new Vector2(1f, 0f);
            endRect.anchorMax = new Vector2(1f, 1f);
            endRect.pivot = new Vector2(0f, 0.5f);
            endRect.sizeDelta = new Vector2(14f, 0f);
            endRect.anchoredPosition = Vector2.zero;
            var endImg = endGO.AddComponent<Image>();
            endImg.color = TapeDark;
            endImg.raycastTarget = false;

            // 타이밍 바 + 초록 영역 + 커서
            CreatePanel(content, "BarBg", new Vector2(0f, -150f), new Vector2(BarHalf * 2f + 20f, 26f),
                new Color(0f, 0f, 0f, 0.65f)).raycastTarget = false;

            _greenCenterX = Random.Range(-150f, 150f);
            _greenZone = CreatePanel(content, "GreenZone", new Vector2(_greenCenterX, -150f),
                new Vector2(GreenHalfWidth * 2f, 22f), new Color(0.25f, 0.8f, 0.35f));
            _greenZone.raycastTarget = false;

            var cursor = CreatePanel(content, "Cursor", new Vector2(-BarHalf, -150f), new Vector2(8f, 38f), Color.white);
            cursor.raycastTarget = false;
            _cursor = cursor.rectTransform;

            // 화면 탭
            var tapArea = CreatePanel(content, "TapArea", Vector2.zero, new Vector2(1920f, 1080f), new Color(0f, 0f, 0f, 0f));
            var button = tapArea.gameObject.AddComponent<Button>();
            button.targetGraphic = tapArea;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(OnTap);
        }

        protected override void OnTick(float dt)
        {
            if (_resolved) return;

            // 커서 왕복
            _cursorT += dt * CursorSpeed;
            var pos = _cursor.anchoredPosition;
            pos.x = Mathf.PingPong(_cursorT, BarHalf * 2f) - BarHalf;
            _cursor.anchoredPosition = pos;

            // 테이프가 롤에서 뽑아지는 연출 — 시간에 따라 폭이 늘어남
            _pullT += dt;
            float pullProgress = Mathf.Clamp01(_pullT / 4f);
            float tapeWidth = Mathf.Lerp(TapeStartWidth, TapeMaxWidth, pullProgress);
            _tapeRect.sizeDelta = new Vector2(tapeWidth, 36f);

            // 롤이 서서히 줄어드는 연출
            float rollScale = 1f - 0.25f * pullProgress;
            _rollRect.localScale = Vector3.one * rollScale;

            // 롤 회전 (뽑히는 느낌)
            _rollRect.localRotation = Quaternion.Euler(0f, 0f, -_pullT * 120f);
        }

        void OnTap()
        {
            if (_resolved) return;
            _resolved = true;

            var cursorImg = _cursor.GetComponent<Image>();
            bool inGreen = Mathf.Abs(_cursor.anchoredPosition.x - _greenCenterX) <= GreenHalfWidth;
            if (!inGreen)
            {
                cursorImg.color = new Color(0.9f, 0.3f, 0.25f);
                PlayConfirmPop(_cursor.anchoredPosition, 120f, new Color(1f, 0.3f, 0.25f, 0.7f));
                Finish(false);
                return;
            }

            cursorImg.color = new Color(0.45f, 1f, 0.5f);
            _greenZone.color = new Color(0.45f, 1f, 0.5f);
            PlayConfirmPop(_cursor.anchoredPosition, 150f);
            Finish(true);
        }
    }
}
