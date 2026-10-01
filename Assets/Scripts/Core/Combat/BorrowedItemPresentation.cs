using System;
using AbsoluteZero.Core.Common;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    // The cat temporarily borrows an inventory view; cancellation must restore that exact view.
    internal sealed class BorrowedItemPresentation : IDisposable
    {
        readonly Transform _transform;
        readonly Vector3 _position, _scale;
        readonly Quaternion _rotation;
        readonly SpriteRenderer _renderer;
        readonly Sprite _sprite;
        readonly Material _material;
        readonly Color _color;
        readonly int _sortingOrder;
        readonly HoverEffect _hover;
        readonly Collider _collider;
        readonly bool _hoverEnabled, _colliderEnabled;
        readonly GameObject[] _children;
        readonly bool[] _active;
        bool _disposed;

        public BorrowedItemPresentation(GameObject view, SpriteRenderer renderer, PresentationResources resources)
        {
            _transform = view.transform;
            _position = _transform.localPosition; _scale = _transform.localScale; _rotation = _transform.localRotation;
            _renderer = renderer; _sprite = renderer.sprite; _material = renderer.sharedMaterial;
            _color = renderer.color; _sortingOrder = renderer.sortingOrder;
            _hover = view.GetComponent<HoverEffect>(); _collider = view.GetComponent<Collider>();
            _hoverEnabled = _hover != null && _hover.enabled;
            _colliderEnabled = _collider != null && _collider.enabled;
            var children = view.GetComponentsInChildren<Transform>(true);
            _children = new GameObject[children.Length]; _active = new bool[children.Length];
            for (int i = 0; i < children.Length; i++)
            { _children[i] = children[i].gameObject; _active[i] = _children[i].gameObject.activeSelf; }
            resources.OnRelease(Dispose);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_transform != null)
            { _transform.localPosition = _position; _transform.localScale = _scale; _transform.localRotation = _rotation; }
            if (_renderer != null)
            { _renderer.sprite = _sprite; _renderer.sharedMaterial = _material; _renderer.color = _color; _renderer.sortingOrder = _sortingOrder; }
            if (_hover != null) _hover.enabled = _hoverEnabled;
            if (_collider != null) _collider.enabled = _colliderEnabled;
            for (int i = 0; i < _children.Length; i++)
                if (_children[i] != null) _children[i].SetActive(_active[i]);
        }
    }
}
