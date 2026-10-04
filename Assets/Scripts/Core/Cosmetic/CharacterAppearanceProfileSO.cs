using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    [CreateAssetMenu(menuName = "AbsoluteZero/Character Appearance Profile")]
    public sealed class CharacterAppearanceProfileSO : ScriptableObject
    {
        public int Version = 1;
        public List<CosmeticAtlasSO> Atlases = new();
        public bool TrySnapshot(out CharacterAppearanceSnapshot snapshot, out string error)
        {
            snapshot = null;
            var bindings = new List<CosmeticAtlasBinding>();
            if (Version < 1 || Atlases == null || Atlases.Count == 0)
            { error = "Appearance requires a version and atlas."; return false; }
            foreach (var atlas in Atlases)
            {
                if (atlas == null || atlas.Bindings == null || atlas.Bindings.Count == 0)
                { error = "Missing appearance atlas bindings."; return false; }
                foreach (var binding in atlas.Bindings)
                {
                    if (!AppearanceBindingValidator.CheckData(binding, out error)) return false;
                    bindings.Add(binding.Copy());
                }
            }
            if (!AppearanceBindingValidator.CheckConflicts(bindings, out error)) return false;
            snapshot = new CharacterAppearanceSnapshot(name, Version, bindings);
            return true;
        }
    }
    // Collections/scalars are frozen at launch; only sprite handles are shared.
    public sealed class CharacterAppearanceSnapshot
    {
        readonly CosmeticAtlasBinding[] _bindings;
        public string Id { get; }
        public int Version { get; }
        internal CharacterAppearanceSnapshot(string id, int version, List<CosmeticAtlasBinding> bindings)
        { Id = id; Version = version; _bindings = bindings.ConvertAll(b => b.Copy()).ToArray(); }
        internal void AppendTo(List<CosmeticAtlasBinding> destination)
        { foreach (var binding in _bindings) destination.Add(binding.Copy()); }
    }
    public static class AppearanceBindingValidator
    {
        public static bool CheckData(CosmeticAtlasBinding b, out string error)
        {
            error = null;
            if (b == null || b.RendererPath == null || b.Replacement == null || (!b.Overlay && b.Source == null)
                || !Enum.IsDefined(typeof(CosmeticView), b.View))
                error = "Missing/invalid binding: " + b?.RendererPath;
            else if (!Finite(b.LocalPosition) || !Finite(b.LocalScale) || !float.IsFinite(b.Rotation))
                error = "Non-finite transform: " + b.RendererPath;
            return error == null;
        }
        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        public static bool CheckConflicts(IEnumerable<CosmeticAtlasBinding> bindings, out string error)
        {
            var keys = new Dictionary<(CosmeticView, string, Sprite), Sprite>();
            foreach (var b in bindings)
            {
                if (!CheckData(b, out error)) return false;
                if (b.Overlay) continue;
                var key = (b.View, b.RendererPath, b.Source);
                if (keys.TryGetValue(key, out var previous) && previous != b.Replacement)
                { error = "Conflicting pose: " + b.RendererPath; return false; }
                keys[key] = b.Replacement;
            }
            error = null; return true;
        }
    }
    public sealed class AppearanceApplyReport
    {
        public bool Success { get; internal set; }
        public string Stage { get; internal set; }
        public string Error { get; internal set; }
        public string Target { get; internal set; }
        public string Profile { get; internal set; }
        public int ProfileVersion { get; internal set; }
        public string RequestedIds { get; internal set; }
        public string AppliedIds { get; internal set; }
        public int LayerCount { get; internal set; }
        public bool Restored { get; internal set; }
    }
}
