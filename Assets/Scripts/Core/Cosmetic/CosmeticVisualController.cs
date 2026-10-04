using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    public class CosmeticVisualController
    {
        readonly Dictionary<CosmeticPart, Transform> _partRoots = new();
        CosmeticAtlasRenderer _atlas;
        Transform _root;
        CosmeticView _view;
        CharacterAppearanceSnapshot _base;
        string _applied;
        bool _suspended;
        public Action Synchronize { get; set; }
        public Func<bool> IsCurrent { get; set; }
        public Action<AppearanceApplyReport> Applied { get; set; }
        public AppearanceApplyReport LastReport { get; private set; }

        public void BindCharacter(Transform root, CosmeticView view = CosmeticView.Character, CharacterAppearanceSnapshot baseAppearance = null)
        {
            Clear(); _partRoots.Clear();
            _root = root; _view = view; _base = baseAppearance;
            if (root == null) return;
            _atlas = root.GetComponent<CosmeticAtlasRenderer>();
            if (_atlas == null) _atlas = root.gameObject.AddComponent<CosmeticAtlasRenderer>();
            _atlas.Claim(this);
            if (view == CosmeticView.FirstPerson) return;
            SetPartRoot(CosmeticPart.Head, root.Find("body/head") ?? root.Find("head"));
            SetPartRoot(CosmeticPart.Top, root.Find("body"));
            SetPartRoot(CosmeticPart.Back, root.Find("body"));
            SetPartRoot(CosmeticPart.Bottom, root.Find("lowerbody"));
            SetPartRoot(CosmeticPart.Tail, root.Find("tail") ?? root.Find("lowerbody"));
        }
        public void SetPartRoot(CosmeticPart part, Transform root) => _partRoots[part] = root;
        public void ApplyAll(CosmeticEquipState state)
        {
            var items = new List<CosmeticItemSO>();
            if (state != null) foreach (CosmeticPart part in Enum.GetValues(typeof(CosmeticPart)))
            { var item = state.GetEquipped(part); if (item != null) items.Add(item); }
            Apply(items, null); // Compatibility entry without a registry; no canonical dedup key.
        }
        public AppearanceApplyReport ApplyFromDto(string json, CosmeticRegistrySO registry)
        {
            if (string.IsNullOrEmpty(json))
            {
                if (_applied != null && Current()) return Report(true, "pending", null, json, false);
                return Apply(new List<CosmeticItemSO>(), null); // Pending NV still displays the base profile.
            }
            if (!CosmeticCodec.TryParse(json, out var dto) || !CosmeticCodec.TryEncode(dto, registry, out var canonical))
                return Report(false, "input", "Invalid cosmetic snapshot.", json, false);
            var items = new List<CosmeticItemSO>();
            var snapshot = new CosmeticSnapshot(dto, 0);
            foreach (CosmeticPart part in Enum.GetValues(typeof(CosmeticPart)))
            { var id = snapshot.Get(part); if (!string.IsNullOrEmpty(id)) items.Add(registry.GetById(id)); }
            return Apply(items, canonical);
        }
        public AppearanceApplyReport ResetEquipment() => Apply(new List<CosmeticItemSO>(), null);
        bool Current() => !_suspended && _root != null && _atlas != null && _atlas.IsOwnedBy(this)
            && (IsCurrent == null || IsCurrent());
        AppearanceApplyReport Apply(List<CosmeticItemSO> items, string canonical)
        {
            if (!Current() || _atlas == null) return Report(false, "identity", "Inactive/stale appearance target.", canonical, false);
            if (canonical != null && canonical == _applied)
            {
                try { Synchronize?.Invoke(); _atlas.ApplyFrame(); return Report(true, "unchanged", null, canonical, false); }
                catch (Exception e) { return Report(false, "policy", e.Message, canonical, false); }
            }
            var bindings = new List<CosmeticAtlasBinding>(); _base?.AppendTo(bindings);
            var equipment = new List<CosmeticAtlasBinding>();
            try
            {
                foreach (var item in items)
                {
                    if (item.Atlas != null)
                    {
                        if (item.Atlas.Bindings == null || (item.Atlas.Bindings.Count == 0 && !IsLegacyPlaceholder(item.Id)))
                            throw new InvalidOperationException(item.Id + ": Empty atlas.");
                        foreach (var b in item.Atlas.Bindings)
                        {
                            if (b != null && b.Replacement == null && IsLegacyPlaceholder(item.Id)) continue;
                            if (!AppearanceBindingValidator.CheckData(b, out var error)) throw new InvalidOperationException(item.Id + ": " + error);
                            equipment.Add(b.Copy());
                        }
                    }
                    else if (item.Sprite == null && IsLegacyPlaceholder(item.Id)) continue;
                    else if (_view == CosmeticView.Character)
                    {
                        if (!_partRoots.TryGetValue(item.Part, out var root) || root == null) throw new InvalidOperationException("Missing part: " + item.Part);
                        var renderer = root.GetComponent<SpriteRenderer>();
                        equipment.Add(new CosmeticAtlasBinding { RendererPath = RelativePath(root), Source = item.Type == CosmeticType.Swap ? _atlas.Original(renderer) : null,
                            Replacement = item.Sprite, Overlay = item.Type == CosmeticType.Overlay, SortOffset = item.SortOrderOffset });
                    }
                }
                if (!AppearanceBindingValidator.CheckConflicts(equipment, out var conflict)) throw new InvalidOperationException(conflict);
                // Equipment overrides base using ORIGINAL pose keys, not chained replacements.
                foreach (var b in equipment)
                {
                    if (!b.Overlay) bindings.RemoveAll(x => !x.Overlay && x.View == b.View && x.RendererPath == b.RendererPath && x.Source == b.Source);
                    bindings.Add(b);
                }
            }
            catch (Exception e) { return Report(false, "prepare", e.Message, canonical, false); }
            if (!_atlas.TryReplace(bindings, _view, Synchronize, Current, out var failure, out var restored))
                return Report(false, "commit", failure, canonical, restored);
            _applied = canonical;
            return Report(true, "committed", null, canonical, false);
        }
        static bool IsLegacyPlaceholder(string id) => id is "head_ref" or "top_ref" or "back_ref" or "low_ref" or "tail_ref" or "cape_01";
        string RelativePath(Transform target)
        {
            var parts = new List<string>();
            while (target != null && target != _root) { parts.Add(target.name); target = target.parent; }
            if (target != _root) throw new InvalidOperationException("Part is outside the visual root.");
            parts.Reverse(); return string.Join("/", parts);
        }
        AppearanceApplyReport Report(bool success, string stage, string error, string requested, bool restored)
        {
            var report = new AppearanceApplyReport { Success = success, Stage = stage, Error = error,
                Target = _root != null ? _root.name : "(released)", Profile = _base?.Id, ProfileVersion = _base?.Version ?? 0,
                RequestedIds = requested, AppliedIds = _applied, LayerCount = _atlas != null ? _atlas.LayerCount : 0, Restored = restored };
            LastReport = report;
            // Diagnostic consumers cannot invalidate a successful render transaction.
            try { Applied?.Invoke(report); } catch (Exception e) { Debug.LogWarning("[AppearanceReport] " + e.Message); }
            return report;
        }
        public void Suspend() { _suspended = true; if (_atlas != null && _atlas.IsOwnedBy(this)) _atlas.Suspend(); }
        public void Resume()
        {
            if (_root == null || _atlas == null || !_atlas.IsOwnedBy(this) || (IsCurrent != null && !IsCurrent())) { Clear(); return; }
            _suspended = false; _atlas?.Resume(); Synchronize?.Invoke(); _atlas?.ApplyFrame();
        }
        public void Clear()
        {
            if (_atlas != null) _atlas.Release(this);
            _atlas = null; _root = null; _base = null; _applied = null; _suspended = false;
            _partRoots.Clear(); Synchronize = null; IsCurrent = null; Applied = null;
        }
    }
}
