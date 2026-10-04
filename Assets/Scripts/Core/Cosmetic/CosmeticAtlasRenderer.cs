using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    [DefaultExecutionOrder(10000)]
    public sealed class CosmeticAtlasRenderer : MonoBehaviour
    {
        sealed class Target
        {
            public SpriteRenderer Renderer;
            public Sprite Source, Applied;
            public readonly Dictionary<Sprite, Sprite> Replacements = new();
        }
        sealed class Layer
        {
            public Target Target;
            public SpriteRenderer Renderer;
            public CosmeticAtlasBinding Binding;
        }
        sealed class Configuration
        {
            public readonly Dictionary<SpriteRenderer, Target> Targets = new();
            public readonly List<Layer> Layers = new();
        }
        Configuration _current = new();
        bool _suspended;
        object _owner;
        public bool IsOwnedBy(object owner) => ReferenceEquals(_owner, owner);
        public void Claim(object owner)
        {
            if (ReferenceEquals(_owner, owner)) return;
            Clear(); Reactivated = null; _owner = owner;
        }
        public void Release(object owner)
        {
            if (!IsOwnedBy(owner)) return;
            Clear(); Reactivated = null; _owner = null;
        }
        public int LayerCount => _current.Layers.Count;
        public bool LayersVisible { get; set; } = true;
        public Action Reactivated { get; set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Action<string> DebugFault;
#endif
        void Checkpoint(string name)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugFault?.Invoke(name);
#endif
        }
        public Sprite Original(SpriteRenderer renderer)
        {
            if (renderer == null) return null;
            return _current.Targets.TryGetValue(renderer, out var t) && renderer.sprite == t.Applied ? t.Source : renderer.sprite;
        }
        Target GetTarget(Configuration config, SpriteRenderer renderer)
        {
            if (!config.Targets.TryGetValue(renderer, out var target))
            {
                target = new Target { Renderer = renderer, Source = Original(renderer), Applied = renderer.sprite };
                config.Targets.Add(renderer, target);
            }
            return target;
        }
        void AddBinding(Configuration config, CosmeticAtlasBinding b)
        {
            var anchor = b.RendererPath.Length == 0 ? transform : transform.Find(b.RendererPath);
            var renderer = anchor != null ? anchor.GetComponent<SpriteRenderer>() : null;
            if (renderer == null) throw new InvalidOperationException("Missing Renderer: " + b.RendererPath);
            var target = GetTarget(config, renderer);
            if (!b.Overlay) { target.Replacements[b.Source] = b.Replacement; return; }
            var go = new GameObject("_cosmetic_atlas_layer");
            go.SetActive(false);
            go.transform.SetParent(anchor, false);
            go.layer = anchor.gameObject.layer;
            go.transform.localPosition = b.LocalPosition;
            go.transform.localScale = b.LocalScale;
            go.transform.localRotation = Quaternion.Euler(0, 0, b.Rotation);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.enabled = false; sr.sharedMaterial = renderer.sharedMaterial; sr.sprite = b.Replacement;
            config.Layers.Add(new Layer { Target = target, Renderer = sr, Binding = b.Copy() });
        }
        // Legacy authoring entry; production uses the full transaction.
        public void Add(CosmeticAtlasSO atlas, CosmeticView view)
        {
            if (atlas == null) return;
            foreach (var b in atlas.Bindings)
            {
                if (b == null || b.View != view || !AppearanceBindingValidator.CheckData(b, out _)) continue;
                var t = b.RendererPath.Length == 0 ? transform : transform.Find(b.RendererPath);
                if (t == null || t.GetComponent<SpriteRenderer>() == null) continue;
                AddBinding(_current, b);
            }
            SetActive(_current, true); ApplyFrame();
        }
        public bool TryReplace(IReadOnlyList<CosmeticAtlasBinding> bindings, CosmeticView view,
            Action synchronize, Func<bool> isCurrent, out string error, out bool restored)
        {
            error = null; restored = false;
            var candidate = new Configuration(); var previous = _current; bool swapped = false;
            try
            {
                if (_suspended || (isCurrent != null && !isCurrent())) throw new InvalidOperationException("Inactive/stale appearance target.");
                foreach (var b in bindings)
                {
                    if (!AppearanceBindingValidator.CheckData(b, out error)) throw new InvalidOperationException(error);
                    if (b.View == view) AddBinding(candidate, b);
                }
                if (isCurrent != null && !isCurrent()) throw new InvalidOperationException("Stale appearance target.");
                CaptureAndRestore(previous); SetActive(previous, false);
                swapped = true; Checkpoint("hidden");
                _current = candidate;
                // Enter the owner's cache while renderers are still hidden.
                SetActive(candidate, true); Checkpoint("cache"); synchronize?.Invoke();
                Checkpoint("policy"); ApplyFrame();
                if (isCurrent != null && !isCurrent()) throw new InvalidOperationException("Appearance target changed during commit.");
            }
            catch (Exception ex)
            {
                error = ex.Message;
                if (swapped)
                {
                    CaptureAndRestore(candidate); SetActive(candidate, false); _current = previous;
                    if (isCurrent == null || isCurrent())
                    {
                        try { SetActive(previous, true); synchronize?.Invoke(); ApplyFrame(); restored = true; }
                        catch (Exception recovery) { error += " / recovery: " + recovery.Message; Clear(); }
                    }
                    else Clear();
                }
                DisposeLayers(candidate); return false;
            }
            DisposeLayers(previous); return true;
        }
        public void ApplyFrame()
        {
            if (_suspended) return;
            foreach (var t in _current.Targets.Values)
            {
                var r = t.Renderer; if (r == null) continue;
                if (r.sprite != t.Applied) t.Source = r.sprite;
                t.Applied = t.Source != null && t.Replacements.TryGetValue(t.Source, out var mapped) ? mapped : t.Source;
                r.sprite = t.Applied;
            }
            foreach (var l in _current.Layers)
            {
                var r = l.Renderer; var source = l.Target.Renderer; if (r == null || source == null) continue;
                var b = l.Binding;
                r.enabled = LayersVisible && source.enabled && (b.Source == null || b.Source == l.Target.Source);
                r.color = source.color; r.flipX = source.flipX; r.flipY = source.flipY;
                r.sharedMaterial = source.sharedMaterial;
                r.sortingLayerID = source.sortingLayerID; r.sortingOrder = source.sortingOrder + b.SortOffset;
                if (b.MirrorPositionWithFlipX) r.transform.localPosition = new Vector3(b.LocalPosition.x * (source.flipX ? -1 : 1), b.LocalPosition.y, b.LocalPosition.z);
            }
        }
        static void SetActive(Configuration c, bool active)
        { foreach (var l in c.Layers) if (l.Renderer != null) l.Renderer.gameObject.SetActive(active); }
        static void CaptureAndRestore(Configuration c)
        {
            foreach (var t in c.Targets.Values)
            {
                if (t.Renderer == null) continue;
                if (t.Renderer.sprite != t.Applied) t.Source = t.Renderer.sprite;
                t.Renderer.sprite = t.Source; t.Applied = t.Source;
            }
        }
        static void DisposeLayers(Configuration c)
        {
            foreach (var l in c.Layers) if (l.Renderer != null)
            {
                l.Renderer.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(l.Renderer.gameObject); else DestroyImmediate(l.Renderer.gameObject);
            }
            c.Layers.Clear();
        }
        public void Suspend() { _suspended = true; SetActive(_current, false); }
        public void Resume() { _suspended = false; SetActive(_current, true); ApplyFrame(); }
        public void Clear()
        { CaptureAndRestore(_current); DisposeLayers(_current); _current = new Configuration(); _suspended = false; }
        void LateUpdate() => ApplyFrame();
        void OnDisable() => Suspend();
        void OnEnable()
        {
            if (!_suspended) return;
            Resume(); Reactivated?.Invoke();
        }
        void OnDestroy() => Clear();
    }
}
