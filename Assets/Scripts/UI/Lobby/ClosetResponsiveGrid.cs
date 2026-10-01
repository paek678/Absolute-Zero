using UnityEngine;

namespace AbsoluteZero.UI.LobbyUI
{
    [RequireComponent(typeof(UnityEngine.UI.GridLayoutGroup))]
    public sealed class ClosetResponsiveGrid : MonoBehaviour
    {
        [Min(80)] public float MinimumCellPixels = 140;
        [Range(1, 5)] public int MaximumColumns = 3;
        [Range(.5f, 1.5f)] public float HeightToWidth = .88f;
        UnityEngine.UI.GridLayoutGroup _grid;
        RectTransform _rect;
        Canvas _canvas;
        float _lastWidth = -1, _lastScale = -1;
        void LateUpdate() => Refresh();
        public void Refresh()
        {
            if (_grid == null) _grid = GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (_rect == null) _rect = (RectTransform)transform;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            float width = _rect.rect.width - _grid.padding.horizontal;
            float scale = _canvas != null ? _canvas.scaleFactor : 1;
            if (width <= 0 || (Mathf.Approximately(width, _lastWidth) && Mathf.Approximately(scale, _lastScale))) return;
            _lastWidth = width; _lastScale = scale;
            int columns = Mathf.Clamp(Mathf.FloorToInt((width + _grid.spacing.x) * scale
                / (MinimumCellPixels + _grid.spacing.x * scale)), 1, MaximumColumns);
            _grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            _grid.constraintCount = columns;
            float cellWidth = Mathf.Max(1, (width - (columns - 1) * _grid.spacing.x) / columns);
            _grid.cellSize = new Vector2(cellWidth, cellWidth * HeightToWidth);
        }
    }
}
