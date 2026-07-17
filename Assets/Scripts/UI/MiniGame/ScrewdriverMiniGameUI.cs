using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 십자드라이버 미니게임 (TightenScrews) — 나사 3개가 나란히 배치되고,
    /// 활성 나사를 시계방향으로 Goal회전(에셋: 1바퀴) 드래그해 조이면
    /// 초록 테두리가 켜지며 다음 나사로 넘어간다 (기획: 7초). 배너: "조여라!"
    /// </summary>
    public class ScrewdriverMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "조여라!";

        const int SCREW_COUNT = 3;

        static readonly Color BorderPending = new(0.15f, 0.15f, 0.2f, 0.9f);   // 대기
        static readonly Color BorderActive = new(1f, 0.85f, 0.3f, 0.95f);      // 현재 조이는 나사
        static readonly Color BorderDone = new(0.25f, 0.85f, 0.35f, 1f);       // 완료 (초록 테두리)
        static readonly Color ScrewColor = new(0.65f, 0.65f, 0.7f);
        static readonly Color SlotColor = new(0.25f, 0.25f, 0.3f);

        int _currentScrew;
        float _accumDegrees;
        float _prevPointerAngle;
        bool _dragging;

        readonly Image[] _borders = new Image[SCREW_COUNT];
        readonly RectTransform[] _screwRects = new RectTransform[SCREW_COUNT];

        float TargetDegrees => Goal * 360f;

        protected override void BuildContent(RectTransform content)
        {
            // 나사 3개 나란히 — 테두리(상태 표시) + 나사 머리(십자 홈, 회전 시각화)
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

            // 드래그 입력면 (전체 화면 — 관대한 판정: 어디서든 활성 나사 중심 기준 원 드래그)
            var surfaceGO = new GameObject("DragSurface");
            surfaceGO.transform.SetParent(content, false);
            var surfRect = surfaceGO.AddComponent<RectTransform>();
            surfRect.anchorMin = Vector2.zero;
            surfRect.anchorMax = Vector2.one;
            surfRect.offsetMin = new Vector2(-500f, -300f);
            surfRect.offsetMax = new Vector2(500f, 300f);
            var surfImg = surfaceGO.AddComponent<Image>();
            surfImg.color = new Color(0f, 0f, 0f, 0f);   // 투명이어도 raycastTarget 동작
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

            // 시계방향 = 각도 감소 (음수 delta)만 누적, 반시계는 무시
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
            _borders[_currentScrew].color = BorderDone;   // 완료 = 초록 테두리
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
            // ⚠ 반드시 "회전하지 않는" 테두리 rect 기준으로 측정할 것.
            // 나사 rect 기준으로 재면 나사가 돌아간 만큼 로컬 좌표계도 같이 돌아가
            // 측정 각도가 상쇄되어 회전이 누적되지 않는다 (실제 발생했던 버그).
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _borders[_currentScrew].rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 local))
            {
                angle = 0f;
                return false;
            }
            if (local.sqrMagnitude < 100f)   // 중심 10px 이내 — 각도 불안정 구간 무시
            {
                angle = 0f;
                return false;
            }
            angle = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>uGUI 포인터 이벤트 중계 (raycast 타깃 = 투명 풀스크린 이미지)</summary>
        class DragRelay : MonoBehaviour, IPointerDownHandler, IDragHandler
        {
            public ScrewdriverMiniGameUI Owner;
            public void OnPointerDown(PointerEventData eventData) => Owner?.HandlePointerDown(eventData);
            public void OnDrag(PointerEventData eventData) => Owner?.HandleDrag(eventData);
        }
    }
}
