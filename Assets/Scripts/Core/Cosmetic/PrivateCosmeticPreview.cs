using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbsoluteZero.Core.Cosmetic
{
    /// <summary>Copies only an idle visual hierarchy; never instantiates a network/player prefab.</summary>
    public sealed class PrivateCosmeticPreview : IDisposable
    {
        readonly CosmeticRegistrySO _registry;
        readonly CosmeticVisualController _visual = new();
        readonly int _layer;
        public GameObject Root { get; private set; }
        public PrivateCosmeticPreview(Transform sourceVisual, CosmeticRegistrySO registry, int previewLayer)
        {
            if (sourceVisual == null || registry == null) throw new ArgumentNullException();
            if (previewLayer < 0 || previewLayer > 31) throw new ArgumentOutOfRangeException(nameof(previewLayer));
            _registry = registry; _layer = previewLayer;
            Root = new GameObject("PrivateCosmeticPreview");
            Root.SetActive(false);
            CopyVisual(sourceVisual, Root.transform);
            _visual.BindCharacter(Root.transform);
            Isolate();
        }
        void CopyVisual(Transform source, Transform target)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
            if (source.TryGetComponent<SpriteRenderer>(out var sr))
            {
                var copy = target.gameObject.AddComponent<SpriteRenderer>();
                copy.sprite = sr.sprite; copy.sharedMaterial = sr.sharedMaterial;
                copy.color = sr.color; copy.flipX = sr.flipX; copy.flipY = sr.flipY;
                copy.sortingLayerID = sr.sortingLayerID; copy.sortingOrder = sr.sortingOrder;
                copy.drawMode = sr.drawMode; copy.size = sr.size; copy.spriteSortPoint = sr.spriteSortPoint;
                copy.maskInteraction = sr.maskInteraction; copy.enabled = sr.enabled;
            }
            if (source.TryGetComponent<SortingGroup>(out var group))
            {
                var copy = target.gameObject.AddComponent<SortingGroup>();
                copy.sortingLayerID = group.sortingLayerID; copy.sortingOrder = group.sortingOrder;
                copy.sortAtRoot = group.sortAtRoot; copy.enabled = group.enabled;
            }
            foreach (Transform child in source)
            {
                var copy = new GameObject(child.name);
                copy.transform.SetParent(target, false);
                CopyVisual(child, copy.transform);
                copy.SetActive(child.gameObject.activeSelf);
            }
        }
        public bool Apply(CosmeticSnapshot snapshot)
        {
            if (Root == null || snapshot == null || !CosmeticCodec.TryEncode(snapshot.ToDto(), _registry, out var json)) return false;
            _visual.ApplyFromDto(json, _registry);
            Isolate(); // New atlas/overlay children inherit the private camera layer explicitly.
            return true;
        }
        public void Isolate()
        {
            if (Root == null) return;
            foreach (var child in Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = _layer;
        }
        public void Dispose()
        {
            if (Root == null) return;
            Root.SetActive(false);
            _visual.Clear();
            if (Application.isPlaying) UnityEngine.Object.Destroy(Root);
            else UnityEngine.Object.DestroyImmediate(Root);
            Root = null;
        }
    }
}
