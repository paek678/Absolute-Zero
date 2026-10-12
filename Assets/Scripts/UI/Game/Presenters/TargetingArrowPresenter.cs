using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game.Presenters
{
    /// <summary>Local aim feedback only. Target identity and input remain owned by GameUIManager.</summary>
    public sealed class TargetingArrowPresenter : MonoBehaviour
    {
        Canvas _canvas;
        RectTransform _canvasRect;
        Graphic _graphic;
        Vector2 _size = new(46, 54);
        readonly List<SpriteRenderer> _renderers = new();
        readonly Vector3[] _corners = new Vector3[4];
        public bool IsVisible => isActiveAndEnabled && _graphic != null && _graphic.gameObject.activeInHierarchy;

        public void Initialize(Sprite sprite = null, float size = 64f)
        {
            if (_canvas != null) return;
            var root = new GameObject("TargetingArrowCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            _canvas = root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = -10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            _canvasRect = root.GetComponent<RectTransform>();
            var go = new GameObject("TargetMarker", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
            rt.pivot = new Vector2(.5f, 0);
            if (sprite != null)
            {
                var icon = go.AddComponent<Image>();
                icon.sprite = sprite; icon.preserveAspect = true;
                _graphic = icon; _size = Vector2.one * size;
            }
            else _graphic = go.AddComponent<TargetingArrowGraphic>();
            rt.sizeDelta = _size;
            _graphic.raycastTarget = false;
            Hide();
        }

        public static bool TryProjectVisible(Camera camera, Vector3 world, out Vector2 screen)
        {
            screen = default;
            if (camera == null) return false;
            Vector3 point = camera.WorldToScreenPoint(world);
            if (point.z <= camera.nearClipPlane || point.z >= camera.farClipPlane
                || !camera.pixelRect.Contains(new Vector2(point.x, point.y))) return false;
            screen = point;
            return true;
        }

        public bool Show(Camera camera, Transform target, RectTransform healthBar)
        {
            Initialize();
            if (!isActiveAndEnabled || target == null || !target.gameObject.activeInHierarchy
                || !TryProjectVisible(camera, target.position + Vector3.up, out var head))
            { Hide(); return false; }

            // List overload reuses storage, including after appearance children are replaced.
            target.GetComponentsInChildren(false, _renderers);
            float top = head.y;
            foreach (var renderer in _renderers)
            {
                if (!renderer.enabled || renderer.sprite == null) continue;
                var name = renderer.name;
                if (Excluded(name)) continue;
                var bounds = renderer.bounds;
                if (_graphic is Image && name == "head") head.x = camera.WorldToScreenPoint(bounds.center).x;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var screen = camera.WorldToScreenPoint(corner);
                    if (screen.z > camera.nearClipPlane) top = Mathf.Max(top, screen.y);
                }
            }
            if (healthBar != null && healthBar.gameObject.activeInHierarchy)
            {
                healthBar.GetWorldCorners(_corners);
                foreach (var corner in _corners)
                {
                    var screen = camera.WorldToScreenPoint(corner);
                    if (screen.z > camera.nearClipPlane) top = Mathf.Max(top, screen.y);
                }
            }
            float scale = Mathf.Sqrt(Screen.width / 1920f * (Screen.height / 1080f));
            head.y = top + 10 * scale;
            var viewport = camera.pixelRect;
            if (head.y + _size.y * scale > viewport.yMax || head.x - _size.x * .5f * scale < viewport.xMin
                || head.x + _size.x * .5f * scale > viewport.xMax)
            { Hide(); return false; }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, head, null, out var local))
            { Hide(); return false; }
            _graphic.rectTransform.anchoredPosition = local;
            _graphic.gameObject.SetActive(true);
            return true;
        }

        static bool Excluded(string name) => Contains(name, "item") || Contains(name, "fan")
            || Contains(name, "effect") || Contains(name, "outline") || Contains(name, "freeze")
            || Contains(name, "ghost") || Contains(name, "arrow") || Contains(name, "indicator");
        static bool Contains(string value, string word) => value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
        public void Hide() { if (_graphic != null) _graphic.gameObject.SetActive(false); }
        public void ShowAtScreen(Vector2 screen)
        {
            if (_canvasRect == null || !isActiveAndEnabled) { Hide(); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out var local);
            _graphic.rectTransform.anchoredPosition = local;
            _graphic.gameObject.SetActive(true);
        }
        public Rect ScreenRect
        {
            get
            {
                if (_graphic == null) return default;
                _graphic.rectTransform.GetWorldCorners(_corners);
                return Rect.MinMaxRect(_corners[0].x, _corners[0].y, _corners[2].x, _corners[2].y);
            }
        }
        void OnDisable() => Hide();
    }

    [RequireComponent(typeof(CanvasRenderer))]
    sealed class TargetingArrowGraphic : MaskableGraphic
    {
        static readonly Color32 Outline = new(53, 20, 19, 255);
        static readonly Color32 Fill = new(232, 59, 51, 255);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Quad(vh, -8, 24, 8, 54, Outline);
            Triangle(vh, new(-23, 26), new(23, 26), Vector2.zero, Outline);
            Quad(vh, -5, 21, 5, 51, Fill);
            Triangle(vh, new(-17, 23), new(17, 23), new(0, 5), Fill);
        }
        static void Quad(VertexHelper vh, float left, float bottom, float right, float top, Color32 color)
        {
            Triangle(vh, new(left,bottom), new(left,top), new(right,top), color);
            Triangle(vh, new(left,bottom), new(right,top), new(right,bottom), color);
        }
        static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color32 color)
        {
            int start = vh.currentVertCount;
            vh.AddVert(a,color,Vector2.zero); vh.AddVert(b,color,Vector2.zero); vh.AddVert(c,color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);
        }
    }
}
