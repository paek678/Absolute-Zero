using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    public class CosmeticVisualController
    {
        readonly Dictionary<CosmeticPart, Transform> _partRoots = new();
        readonly OverlayApplier _overlayApplier = new();
        readonly SwapApplier _swapApplier = new();
        CosmeticAtlasRenderer _atlas;
        CosmeticView _view;

        public void BindCharacter(Transform root, CosmeticView view = CosmeticView.Character)
        {
            Clear();
            _partRoots.Clear();
            _view = view;
            _atlas = root.GetComponent<CosmeticAtlasRenderer>();
            if (_atlas == null) _atlas = root.gameObject.AddComponent<CosmeticAtlasRenderer>();
            if (view == CosmeticView.FirstPerson) return;
            SetPartRoot(CosmeticPart.Head, root.Find("body/head") ?? root.Find("head"));
            SetPartRoot(CosmeticPart.Top, root.Find("body"));
            SetPartRoot(CosmeticPart.Back, root.Find("body"));
            SetPartRoot(CosmeticPart.Bottom, root.Find("lowerbody"));
            SetPartRoot(CosmeticPart.Tail, root.Find("tail") ?? root.Find("lowerbody"));
        }

        void ApplyItem(CosmeticItemSO item, CosmeticPart part)
        {
            if (item.Atlas != null && _atlas != null) _atlas.Add(item.Atlas, _view);
            else if (_view == CosmeticView.Character && _partRoots.TryGetValue(part, out var root))
                GetApplier(item.Type).Apply(root, item, part);
        }

        public void SetPartRoot(CosmeticPart part, Transform root)
        {
            _partRoots[part] = root;
        }

        public void ApplyAll(CosmeticEquipState equipState)
        {
            Clear();
            if (equipState == null) return;

            foreach (CosmeticPart part in System.Enum.GetValues(typeof(CosmeticPart)))
            {
                var item = equipState.GetEquipped(part);
                if (item == null) continue;
                ApplyItem(item, part);
            }
        }

        public void ApplyFromDto(string json, CosmeticRegistrySO registry)
        {
            Clear();
            if (string.IsNullOrEmpty(json) || registry == null) return;

            CosmeticDto dto;
            try
            {
                dto = JsonUtility.FromJson<CosmeticDto>(json);
            }
            catch
            {
                return;
            }

            if (dto == null || dto.v != 1) return;

            ApplyPartFromDto(dto.head, CosmeticPart.Head, registry);
            ApplyPartFromDto(dto.top, CosmeticPart.Top, registry);
            ApplyPartFromDto(dto.back, CosmeticPart.Back, registry);
            ApplyPartFromDto(dto.bottom, CosmeticPart.Bottom, registry);
            ApplyPartFromDto(dto.tail, CosmeticPart.Tail, registry);
        }

        public void Clear()
        {
            if (_atlas != null) _atlas.Clear();
            foreach (var kvp in _partRoots)
            {
                _overlayApplier.Remove(kvp.Value, kvp.Key);
                _swapApplier.Remove(kvp.Value, kvp.Key);
            }
        }

        void ApplyPartFromDto(string id, CosmeticPart part, CosmeticRegistrySO registry)
        {
            if (string.IsNullOrEmpty(id)) return;

            var item = registry.GetById(id);
            if (item == null || item.Part != part) return;

            ApplyItem(item, part);
        }

        ICosmeticApplier GetApplier(CosmeticType type)
        {
            return type == CosmeticType.Swap ? _swapApplier : _overlayApplier;
        }
    }
}
