using System;
using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Cosmetic
{
    public class CosmeticEquipState
    {
        public long Revision { get; private set; }

        readonly Dictionary<CosmeticPart, CosmeticItemSO> _equipped = new();

        public event Action OnEquipChanged;

        public CosmeticItemSO GetEquipped(CosmeticPart part)
        {
            return _equipped.TryGetValue(part, out var item) ? item : null;
        }

        public void Equip(CosmeticItemSO item)
        {
            if (item == null) return;
            if (GetEquipped(item.Part)?.Id == item.Id) return;
            _equipped[item.Part] = item;
            Changed();
        }

        public void Unequip(CosmeticPart part)
        {
            if (_equipped.Remove(part)) Changed();
        }

        void Changed()
        {
            Revision++;
            OnEquipChanged?.Invoke();
        }

        internal void ApplyValidated(CosmeticDto dto, CosmeticRegistrySO registry)
        {
            if (CosmeticCodec.SameIds(ToDto(), dto)) return;
            FromDto(dto, registry);
            Changed();
        }

        public CosmeticDto ToDto()
        {
            var dto = new CosmeticDto { v = 1 };
            if (_equipped.TryGetValue(CosmeticPart.Head, out var h)) dto.head = h.Id;
            if (_equipped.TryGetValue(CosmeticPart.Top, out var t)) dto.top = t.Id;
            if (_equipped.TryGetValue(CosmeticPart.Back, out var b)) dto.back = b.Id;
            if (_equipped.TryGetValue(CosmeticPart.Bottom, out var bt)) dto.bottom = bt.Id;
            if (_equipped.TryGetValue(CosmeticPart.Tail, out var tl)) dto.tail = tl.Id;
            return dto;
        }

        public void FromDto(CosmeticDto dto, CosmeticRegistrySO registry)
        {
            _equipped.Clear();
            if (dto == null || registry == null) return;

            TryLoadPart(dto.head, CosmeticPart.Head, registry);
            TryLoadPart(dto.top, CosmeticPart.Top, registry);
            TryLoadPart(dto.back, CosmeticPart.Back, registry);
            TryLoadPart(dto.bottom, CosmeticPart.Bottom, registry);
            TryLoadPart(dto.tail, CosmeticPart.Tail, registry);
        }

        void TryLoadPart(string id, CosmeticPart expectedPart, CosmeticRegistrySO registry)
        {
            if (string.IsNullOrEmpty(id)) return;
            var item = registry.GetById(id);
            if (item == null)
            {
                Debug.LogWarning($"[CosmeticEquipState] Id '{id}' not found in registry — skipping");
                return;
            }
            if (item.Part != expectedPart)
            {
                Debug.LogWarning($"[CosmeticEquipState] Id '{id}' Part mismatch: expected {expectedPart}, got {item.Part} — skipping");
                return;
            }
            _equipped[expectedPart] = item;
        }

    }
}
