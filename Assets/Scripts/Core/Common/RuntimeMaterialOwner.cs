using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    // Owns only the copies created here, never the imported/shared source materials.
    public sealed class RuntimeMaterialOwner : MonoBehaviour, System.IDisposable
    {
        readonly List<Material> _owned = new();

        public Material Clone(Material source)
        {
            if (source == null) return null;
            var copy = new Material(source) { name = source.name + " (VFX runtime)" };
            _owned.Add(copy);
            return copy;
        }

        void OnDestroy() => Dispose();

        public void Dispose()
        {
            foreach (var material in _owned)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            _owned.Clear();
        }
    }
}
