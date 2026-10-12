using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    // Explicit per-renderer texture prevents shared overlay materials from
    // reusing another sprite's texture in Editor/URP batching. Track swaps too.
    [DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
    public sealed class ForegroundSpriteTexture : MonoBehaviour
    {
        SpriteRenderer _renderer;
        MaterialPropertyBlock _properties;
        static readonly int MainTexture = Shader.PropertyToID("_MainTex");

        void OnEnable()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.RegisterSpriteChangeCallback(Refresh);
            Refresh(_renderer);
        }

        public void Refresh(SpriteRenderer renderer)
        {
            if (renderer == null || renderer.sprite == null) return;
            _properties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_properties);
            _properties.SetTexture(MainTexture, renderer.sprite.texture);
            renderer.SetPropertyBlock(_properties);
        }

        void OnDisable()
        {
            if (_renderer != null) _renderer.UnregisterSpriteChangeCallback(Refresh);
        }
    }
}
