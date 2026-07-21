using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    public class ScrewdriverMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "조여라!";

        const int SCREW_COUNT = 3;

        static readonly Color BorderPending = new(0.15f, 0.15f, 0.2f, 0.9f);
        static readonly Color BorderActive = new(1f, 0.85f, 0.3f, 0.95f);
        static readonly Color BorderDone = new(0.25f, 0.85f, 0.35f, 1f);
        static readonly Color ScrewColor = new(0.65f, 0.65f, 0.7f);
        static readonly Color SlotColor = new(0.25f, 0.25f, 0.3f);

        int _currentScrew;
        float _accumDegrees;
        float _prevPointerAngle;
        bool _dragging;

        readonly Image[] _borders = new Image[SCREW_COUNT];
        readonly RectTransform[] _screwRects = new RectTransform[SCREW_COUNT];

        float TargetDegrees => Goal * 360f;

        static float ScrewX(int index) => -190f + index * 190f;

        protected override void BuildContent(RectTransform content)
        {
            for (int i = 0; i < SCREW_COUNT; i++)
            {
                float x = -190f + i * 190f;

                _borders[i] = CreatePanel(content, $"Border{i}", new Vector2(x, 0f),
                    new Vector2(168f, 168f), BorderPending);
                _borders[i].raycastTarget = false;

                var screwGO = new GameObject($"Screw{i}");
                screwGO.transform.SetParent(content, false);
                var screwRect = screwGO.AddComponent<RectTransform>();
                screwRect.anchoredPosition = new Vector2(x, 0f);
                screwRect.sizeDelta = new Vector2(140f, 140f);
                var screwImg = screwGO.AddComponent<Image>();
                screwImg.color = ScrewColor;
                screwImg.raycastTarget = false;

                CreatePanel(screwRect, "SlotV", Vector2.zero, new Vector2(20f, 108f), SlotColor).raycastTarget = false;
                CreatePanel(screwRect, "SlotH", Vector2.zero, new Vector2(108f, 20f), SlotColor).raycastTarget = false;

                _screwRects[i] = screwRect;
            }

            SetActiveBorder(0);

            var surfaceGO = new GameObject("DragSurface");
            surfaceGO.transform.SetParent(content, false);
            var surfRect = surfaceGO.AddComponent<RectTransform>();
            surfRect.anchorMin = Vector2.zero;
            surfRect.anchorMax = Vector2.one;
            surfRect.offsetMin = new Vector2(-500f, -300f);
            surfRect.offsetMax = new Vector2(500f, 300f);
            var surfImg = surfaceGO.AddComponent<Image>();
            surfImg.color = new Color(0f, 0f, 0f, 0f);
            var relay = surfaceGO.AddComponent<DragRelay>();
            relay.Owner = this;
        }

        void SetActiveBorder(int index)
        {
            for (int i = 0; i < SCREW_COUNT; i++)
            {
                if (i < index) _borders[i].color = BorderDone;
                else if (i == index) _borders[i].color = BorderActive;
                else _borders[i].color = BorderPending;
            }
        }

        void HandlePointerDown(PointerEventData eventData)
        {
            _dragging = TryGetPointerAngle(eventData, out _prevPointerAngle);
        }

        void HandleDrag(PointerEventData eventData)
        {
            if (!_dragging || _currentScrew >= SCREW_COUNT) return;
            if (!TryGetPointerAngle(eventData, out float angle)) return;

            float delta = Mathf.DeltaAngle(_prevPointerAngle, angle);
            _prevPointerAngle = angle;

            if (delta < 0f)
            {
                _accumDegrees += -delta;
                _screwRects[_currentScrew].localRotation = Quaternion.Euler(0f, 0f, -_accumDegrees);

                if (_accumDegrees >= TargetDegrees)
                    CompleteCurrentScrew();
            }
        }

        void CompleteCurrentScrew()
        {
            // 나사별 완료 순간 피드백: 초록 테두리 + 초록 확인 팝
            _borders[_currentScrew].color = BorderDone;
            PlayConfirmPop(new Vector2(ScrewX(_currentScrew), 0f), 210f);
            _currentScrew++;

            if (_currentScrew >= SCREW_COUNT)
            {
                Finish(true);
                return;
            }

            _accumDegrees = 0f;
            _dragging = false;
            SetActiveBorder(_currentScrew);
        }

        bool TryGetPointerAngle(PointerEventData eventData, out float angle)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _borders[_currentScrew].rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                angle = 0f;
                return false;
            }
            if (local.sqrMagnitude < 100f)
            {
                angle = 0f;
                return false;
            }
            angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            return true;
        }

        class DragRelay : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            public ScrewdriverMiniGameUI Owner;
            public void OnPointerDown(PointerEventData eventData) => Owner?.HandlePointerDown(eventData);
            public void OnDrag(PointerEventData eventData) => Owner?.HandleDrag(eventData);
        }
    }
}
