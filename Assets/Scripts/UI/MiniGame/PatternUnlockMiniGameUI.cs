using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AbsoluteZero.UI.MiniGame
{
    /// <summary>
    /// 스마트폰 미니게임 (PatternUnlock) — 상단에 제시된 패턴(ㄱ/ㄴ/Z 랜덤)을 3×3 점에 드래그로 그리기 (5초).
    /// 정답 순서대로 연결 = 성공, 틀린 점 연결 = 즉시 실패, 손을 떼면 처음부터 (시간 내 재시도).
    /// 성취 피드백: 점 연결마다 팝, 완성 시 경로 전체 초록 + 확인 팝, 틀리면 빨간 팝. 배너: "그려라!"
    /// </summary>
    public class PatternUnlockMiniGameUI : MiniGameUIBase
    {
        protected override string BannerText => "그려라!";

        // 인덱스: 0=좌상단, 행 우선 (0 1 2 / 3 4 5 / 6 7 8)
        static readonly int[][] Patterns =
        {
            new[] { 0, 1, 2, 5, 8 },            // ㄱ
            new[] { 0, 3, 6, 7, 8 },            // ㄴ
            new[] { 0, 1, 2, 4, 6, 7, 8 },      // Z
        };

        const float GridSpacing = 110f;
        const float SnapRadius = 46f;

        static readonly Color DotIdle = new(0.5f, 0.5f, 0.55f);
        static readonly Color DotConnected = new(0.3f, 0.85f, 0.95f);
        static readonly Color DotWrong = new(0.9f, 0.3f, 0.25f);
        static readonly Color LineColor = new(0.3f, 0.85f, 0.95f, 0.9f);
        static readonly Color SuccessGreen = new(0.35f, 0.95f, 0.45f);

        int[] _answer;
        RectTransform _grid;
        readonly Image[] _dots = new Image[9];
        readonly bool[] _connected = new bool[9];
        readonly List<GameObject> _lines = new();
        int _progress;

        protected override void BuildContent(RectTransform content)
        {
            _answer = Patterns[Random.Range(0, Patterns.Length)];

            // 정답 프리뷰 (상단 미니 그리드 — 작게 그려서 보여줌)
            BuildPreview(content);

            // 본 그리드
            var gridGO = new GameObject("Grid");
            gridGO.transform.SetParent(content, false);
            _grid = gridGO.AddComponent<RectTransform>();
            _grid.anchoredPosition = new Vector2(0f, -60f);
            _grid.sizeDelta = new Vector2(GridSpacing * 2f + 60f, GridSpacing * 2f + 60f);

            for (int i = 0; i < 9; i++)
            {
                var dot = CreateCircle(_grid, $"Dot{i}", DotPosition(i, GridSpacing), 36f, DotIdle);
                dot.raycastTarget = false;
                _dots[i] = dot;
            }

            // 드래그 입력면 (풀스크린)
            var surfaceGO = new GameObject("DragSurface");
            surfaceGO.transform.SetParent(content, false);
            var surfRect = surfaceGO.AddComponent<RectTransform>();
            surfRect.anchoredPosition = Vector2.zero;
            surfRect.sizeDelta = new Vector2(1920f, 1080f);
            var surfImg = surfaceGO.AddComponent<Image>();
            surfImg.color = new Color(0f, 0f, 0f, 0f);
            var relay = surfaceGO.AddComponent<DragRelay>();
            relay.Owner = this;
        }

        void BuildPreview(RectTransform content)
        {
            const float miniSpacing = 34f;
            var previewGO = new GameObject("Preview");
            previewGO.transform.SetParent(content, false);
            var preview = previewGO.AddComponent<RectTransform>();
            preview.anchoredPosition = new Vector2(0f, 210f);
            preview.sizeDelta = new Vector2(miniSpacing * 2f + 24f, miniSpacing * 2f + 24f);

            // 정답 선 먼저 (점 아래 레이어)
            for (int i = 1; i < _answer.Length; i++)
                DrawLine(preview, DotPosition(_answer[i - 1], miniSpacing), DotPosition(_answer[i], miniSpacing), 5f,
                    new Color(1f, 0.95f, 0.75f, 0.95f));

            for (int i = 0; i < 9; i++)
                CreateCircle(preview, $"MiniDot{i}", DotPosition(i, miniSpacing), 11f,
                    new Color(0.85f, 0.85f, 0.9f)).raycastTarget = false;
        }

        static Vector2 DotPosition(int index, float spacing)
        {
            int row = index / 3;
            int col = index % 3;
            return new Vector2((col - 1) * spacing, (1 - row) * spacing);
        }

        /// <summary>그리드 로컬 좌표 → Content 좌표 (확인 팝 위치용)</summary>
        Vector2 ContentPos(int dotIndex) => _grid.anchoredPosition + DotPosition(dotIndex, GridSpacing);

        void HandlePointer(PointerEventData eventData)
        {
            if (Finished) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _grid, eventData.position, eventData.pressEventCamera, out Vector2 local))
                return;

            for (int i = 0; i < 9; i++)
            {
                if ((DotPosition(i, GridSpacing) - local).sqrMagnitude <= SnapRadius * SnapRadius)
                {
                    TryConnect(i);
                    return;
                }
            }
        }

        void TryConnect(int index)
        {
            if (_connected[index]) return;   // 이미 연결된 점 위 재통과 — 무시

            if (index == _answer[_progress])
            {
                _connected[index] = true;
                _dots[index].color = DotConnected;
                PlayConfirmPop(ContentPos(index), 80f, new Color(LineColor.r, LineColor.g, LineColor.b, 0.6f));   // 점 연결 성취 팝

                if (_progress > 0)
                {
                    var line = DrawLine(_grid, DotPosition(_answer[_progress - 1], GridSpacing),
                        DotPosition(index, GridSpacing), 9f, LineColor);
                    _lines.Add(line.gameObject);
                }
                _progress++;

                if (_progress >= _answer.Length)
                {
                    // 성공 순간: 경로 전체 초록 확정 + 큰 확인 팝
                    foreach (int d in _answer) _dots[d].color = SuccessGreen;
                    foreach (var line in _lines)
                        if (line != null) line.GetComponent<Image>().color = SuccessGreen;
                    PlayConfirmPop(_grid.anchoredPosition, 380f);
                    Finish(true);
                }
            }
            else
            {
                _dots[index].color = DotWrong;
                PlayConfirmPop(ContentPos(index), 90f, new Color(1f, 0.3f, 0.25f, 0.7f));
                Finish(false);   // 틀린 점 연결 = 즉시 실패 (기획 표)
            }
        }

        void ResetChain()
        {
            if (Finished || _progress == 0) return;
            for (int i = 0; i < 9; i++)
            {
                _connected[i] = false;
                _dots[i].color = DotIdle;
            }
            foreach (var line in _lines)
                if (line != null) Destroy(line);
            _lines.Clear();
            _progress = 0;
        }

        Image DrawLine(RectTransform parent, Vector2 from, Vector2 to, float thickness, Color color)
        {
            var go = new GameObject("Line");
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            var delta = to - from;
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.sizeDelta = new Vector2(delta.magnitude, thickness);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        class DragRelay : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public PatternUnlockMiniGameUI Owner;
            public void OnPointerDown(PointerEventData eventData) => Owner?.HandlePointer(eventData);
            public void OnDrag(PointerEventData eventData) => Owner?.HandlePointer(eventData);
            public void OnPointerUp(PointerEventData eventData) => Owner?.ResetChain();
        }
    }
}
