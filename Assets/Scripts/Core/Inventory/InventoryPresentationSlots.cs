using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Item;

namespace AbsoluteZero.Core.Inventory
{
    // Display-only copy -> anchor mapping. Network slot indices remain input identities.
    internal sealed class InventoryPresentationSlots
    {
        internal const int Capacity = MultiInventorySnapshotNetData.MaxSlots;
        internal const int RandomCapacity = 8;
        readonly Dictionary<uint, int> _slots = new();
        readonly HashSet<uint> _present = new();
        readonly List<uint> _removed = new();
        readonly List<ItemSlotNetData> _pending = new();
        readonly bool[] _used = new bool[Capacity];
        InventoryViewSnapshot _scope;

        public void Refresh(InventoryViewSnapshot snapshot, Func<short, bool> isRandom)
        {
            if (!snapshot.SameScope(_scope)) Clear();
            _scope = snapshot;
            _present.Clear();
            for (int i = 0; i < snapshot.Count; i++)
                if (!snapshot[i].IsEmpty) _present.Add(snapshot[i].CopyId);
            _removed.Clear();
            foreach (var pair in _slots)
                if (!_present.Contains(pair.Key)) _removed.Add(pair.Key);
            foreach (var copy in _removed) _slots.Remove(copy);
            Array.Clear(_used, 0, _used.Length);
            foreach (var slot in _slots.Values) _used[slot] = true;
            _pending.Clear();
            for (int i = 0; i < snapshot.Count; i++)
            {
                var item = snapshot[i];
                if (!item.IsEmpty && item.CopyId != 0 && !_slots.ContainsKey(item.CopyId)) _pending.Add(item);
            }
            // Refilled network holes may precede older copies. Allocation identity
            // preserves arrival order even when several grants arrive in one snapshot.
            _pending.Sort((a, b) => a.CopyId.CompareTo(b.CopyId));

            // Reserve the authored basic-item region first on initial binding.
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < _pending.Count; i++)
                {
                    var item = _pending[i];
                    if (_slots.ContainsKey(item.CopyId)) continue;
                    bool random = isRandom(item.ItemId);
                    if (random != (pass == 1)) continue;
                    int slot = FirstFree(random ? 0 : RandomCapacity, random ? RandomCapacity : Capacity);
                    // A legal steal can use spare capacity outside its normal region.
                    if (slot < 0) slot = FirstFree(0, Capacity);
                    if (slot < 0) continue;
                    _slots.Add(item.CopyId, slot);
                    _used[slot] = true;
                }
        }

        int FirstFree(int first, int end)
        {
            for (int i = first; i < end; i++) if (!_used[i]) return i;
            return -1;
        }

        public int Get(uint copy) => _slots.TryGetValue(copy, out var slot) ? slot : -1;
        public void Clear() { _slots.Clear(); _scope = null; }
    }
}
