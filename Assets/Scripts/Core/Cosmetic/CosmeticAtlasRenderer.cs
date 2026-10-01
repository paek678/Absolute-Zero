using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    // Animator/SpriteResolver selects the original pose first; only its final artwork is remapped.
    // No animator hashes, transforms, imported SpriteLibraries or gameplay state are modified.
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
            public Sprite Pose;
        }
        readonly Dictionary<SpriteRenderer, Target> _targets = new();
        readonly List<Layer> _layers = new();

        public void Add(CosmeticAtlasSO atlas, CosmeticView view)
        {
            if (atlas == null) return;
            foreach (var binding in atlas.Bindings)
            {
                if (binding == null || binding.View != view || binding.Replacement == null
                    || string.IsNullOrEmpty(binding.RendererPath)) continue;
                var anchor = transform.Find(binding.RendererPath);
                var renderer = anchor != null ? anchor.GetComponent<SpriteRenderer>() : null;
                if (renderer == null) continue;
                if (!_targets.TryGetValue(renderer, out var target))
                {
                    target = new Target { Renderer = renderer, Source = renderer.sprite, Applied = renderer.sprite };
                    _targets.Add(renderer, target);
                }
                if (!binding.Overlay)
                {
                    if (binding.Source != null) target.Replacements[binding.Source] = binding.Replacement;
                    continue;
                }
                var go = new GameObject("_cosmetic_atlas_layer");
                go.transform.SetParent(anchor, false);
                go.transform.localPosition = binding.LocalPosition;
                go.transform.localScale = binding.LocalScale;
                go.transform.localRotation = Quaternion.Euler(0, 0, binding.Rotation);
                var layer = go.AddComponent<SpriteRenderer>();
                layer.sharedMaterial = renderer.sharedMaterial;
                layer.sprite = binding.Replacement;
                layer.sortingLayerID = renderer.sortingLayerID;
                layer.sortingOrder = renderer.sortingOrder + binding.SortOffset;
                _layers.Add(new Layer { Target = target, Renderer = layer, Pose = binding.Source });
            }
            ApplyFrame();
        }

        public void ApplyFrame()
        {
            foreach (var target in _targets.Values)
            {
                var renderer = target.Renderer;
                if (renderer == null) continue;
                if (renderer.sprite != target.Applied) target.Source = renderer.sprite;
                target.Applied = target.Source != null && target.Replacements.TryGetValue(target.Source, out var sprite)
                    ? sprite : target.Source;
                renderer.sprite = target.Applied;
            }
            foreach (var layer in _layers)
            {
                if (layer.Renderer == null || layer.Target.Renderer == null) continue;
                var source = layer.Target.Renderer;
                layer.Renderer.enabled = source.enabled && (layer.Pose == null || layer.Pose == layer.Target.Source);
                layer.Renderer.color = source.color;
                layer.Renderer.flipX = source.flipX;
                layer.Renderer.flipY = source.flipY;
            }
        }

        public void Clear()
        {
            foreach (var layer in _layers)
                if (layer.Renderer != null)
                {
                    layer.Renderer.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(layer.Renderer.gameObject);
                    else DestroyImmediate(layer.Renderer.gameObject);
                }
            _layers.Clear();
            foreach (var target in _targets.Values)
                if (target.Renderer != null && target.Renderer.sprite == target.Applied)
                    target.Renderer.sprite = target.Source;
            _targets.Clear();
        }
        void LateUpdate() => ApplyFrame();
        void OnDestroy() => Clear();
    }
}
