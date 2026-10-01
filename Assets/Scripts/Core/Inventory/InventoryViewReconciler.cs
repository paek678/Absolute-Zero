using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Item;

namespace AbsoluteZero.Core.Inventory
{
    // Identity-based reconciliation. No pooling: a removed copy is destroyed, never reassigned.
    internal sealed class InventoryViewReconciler<T> where T : class
    {
        sealed class Entry { public short Item; public T View; }
        readonly Dictionary<uint, Entry> _entries = new();
        readonly HashSet<uint> _present = new();
        readonly List<uint> _removed = new();
        InventoryViewSnapshot _scope;

        public T[] Reconcile(InventoryViewSnapshot snapshot, Func<int, ItemSlotNetData, T> create,
            Action<T, int, ItemSlotNetData> update, Action<T> destroy, Func<T, bool> alive)
        {
            _present.Clear();
            for (int i = 0; i < snapshot.Count; i++)
                if (!snapshot[i].IsEmpty && (snapshot[i].CopyId == 0 || !_present.Add(snapshot[i].CopyId)))
                    return null; // Caller retains its full-rebuild compatibility path for malformed legacy input.
            if (!snapshot.SameScope(_scope)) Clear(destroy);
            _scope = snapshot;
            _removed.Clear();
            foreach (var pair in _entries) if (!_present.Contains(pair.Key)) _removed.Add(pair.Key);
            foreach (var key in _removed) { destroy(_entries[key].View); _entries.Remove(key); }
            var views = new T[snapshot.Count];
            for (int i = 0; i < snapshot.Count; i++)
            {
                var slot = snapshot[i];
                if (slot.IsEmpty) continue;
                if (_entries.TryGetValue(slot.CopyId, out var old) && (old.Item != slot.ItemId || !alive(old.View)))
                { destroy(old.View); _entries.Remove(slot.CopyId); old = null; }
                if (old == null)
                {
                    var view = create(i, slot);
                    if (!alive(view)) continue;
                    old = new Entry { Item = slot.ItemId, View = view };
                    _entries.Add(slot.CopyId, old);
                }
                update(old.View, i, slot);
                views[i] = old.View;
            }
            return views;
        }

        public void Clear(Action<T> destroy)
        {
            foreach (var entry in _entries.Values) destroy(entry.View);
            _entries.Clear(); _scope = null;
        }
    }
}
