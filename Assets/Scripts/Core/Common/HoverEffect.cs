using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    [RequireComponent(typeof(Collider))]
    public class HoverEffect : MonoBehaviour
    {
        public float HoverScale = 1.15f;
        public float ScaleSpeed = 12f;
        public float OutlineExpand = 1.12f;
        public Color HoverOutlineColor = new(1f, 0.9f, 0.3f);
        public Color SelectedOutlineColor = new(0.3f, 1f, 0.45f);

        SpriteRenderer _outlineRenderer;
        Vector3 _baseScale;
        bool _isHovered;
        bool _isSelected;
        bool _initialized;

        public bool IsHovered => _isHovered;

        public void Initialize()
        {
            _baseScale = transform.localScale;

            var mainSprite = GetComponentInChildren<SpriteRenderer>();
            if (mainSprite != null)
                CreateOutline(mainSprite);

            _initialized = true;
        }

        void Start()
        {
            if (!_initialized) Initialize();
        }

        public void SetHovered(bool hovered)
        {
            _isHovered = hovered;
            if (_isSelected || _outlineRenderer == null) return;
            _outlineRenderer.color = HoverOutlineColor;
            _outlineRenderer.enabled = hovered;
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (_outlineRenderer == null) return;

            if (selected)
            {
                _outlineRenderer.color = SelectedOutlineColor;
                _outlineRenderer.enabled = true;
            }
            else
            {
                _outlineRenderer.color = HoverOutlineColor;
                _outlineRenderer.enabled = _isHovered;
            }
        }

        void Update()
        {
            if (_baseScale.x <= 0f) return;
            float target = _isHovered ? HoverScale : 1f;
            float ratio = transform.localScale.x / _baseScale.x;
            float lerped = Mathf.Lerp(ratio, target, Time.deltaTime * ScaleSpeed);
            transform.localScale = _baseScale * lerped;
        }

        void CreateOutline(SpriteRenderer source)
        {
            var outlineGO = new GameObject("HoverOutline");
            outlineGO.transform.SetParent(transform, false);
            outlineGO.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            outlineGO.transform.localScale = Vector3.one * OutlineExpand;
            _outlineRenderer = outlineGO.AddComponent<SpriteRenderer>();
            _outlineRenderer.sprite = source.sprite;
            _outlineRenderer.color = HoverOutlineColor;
            _outlineRenderer.sortingOrder = source.sortingOrder - 1;
            _outlineRenderer.enabled = false;
        }
    }
}
