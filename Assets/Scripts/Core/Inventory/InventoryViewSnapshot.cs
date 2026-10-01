using System;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player.Identity;

namespace AbsoluteZero.Core.Inventory
{
    // A display snapshot, never a writable inventory or another replication stream.
    internal sealed class InventoryViewSnapshot
    {
        readonly ItemSlotNetData[] _slots;
        public PlayerBinding Binding { get; }
        public uint MatchEpoch { get; }
        public uint RoundEpoch { get; }
        public uint Revision { get; }
        public bool Selected { get; }
        public bool BasicBlocked { get; }
        public int Count => _slots.Length;
        public ItemSlotNetData this[int index] => index >= 0 && index < Count ? _slots[index] : ItemSlotNetData.Empty;

        internal InventoryViewSnapshot(PlayerBinding binding, uint match, uint round, uint revision,
            ItemSlotNetData[] slots, bool selected = false, bool basicBlocked = false)
        {
            Binding = binding; MatchEpoch = match; RoundEpoch = round; Revision = revision;
            _slots = (ItemSlotNetData[])slots.Clone(); Selected = selected; BasicBlocked = basicBlocked;
        }

        public int FindCopy(uint copyId)
        {
            if (copyId == 0) return -1;
            for (int i = 0; i < Count; i++) if (_slots[i].CopyId == copyId && !_slots[i].IsEmpty) return i;
            return -1;
        }

        public bool SameScope(InventoryViewSnapshot other) => other != null
            && ReferenceEquals(Binding, other.Binding) && MatchEpoch == other.MatchEpoch && RoundEpoch == other.RoundEpoch;
    }

    internal sealed class BoundInventoryReader
    {
        readonly PlayerBinding _binding;
        readonly MultiInventoryReadModel _multi;
        uint _duelRevision;
        public BoundInventoryReader(PlayerBinding binding, MultiInventoryReadModel multi = null)
        { _binding = binding; _multi = multi; }

        public bool TryRead(out InventoryViewSnapshot snapshot)
        {
            snapshot = null;
            if (_binding?.IsValid != true || !LocalMatchPerspective.TryResolveCurrent(out var perspective)
                || !perspective.TryGetBinding(_binding.Identity.PlayerIndex, out var current)
                || !ReferenceEquals(current, _binding)) return false;
            var state = _binding.State;
            int seat = _binding.Identity.PlayerIndex;
            if (_multi != null && (!_multi.HasSeat(seat) || _multi.IsFaulted)) return false;
            int count = _multi != null ? _multi.GetCount(seat) : _binding.Inventory.SlotStates.Count;
            var slots = new ItemSlotNetData[count];
            for (int i = 0; i < count; i++)
                slots[i] = _multi != null ? _multi.GetSlot(seat, i) : _binding.Inventory.SlotStates[i];
            // No yields/callbacks while copying: Multi metadata and every slot use one adopted envelope.
            // Selection flags are independent replicated state, not invented envelope metadata.
            snapshot = new InventoryViewSnapshot(_binding, _multi?.MatchEpoch ?? 0, _multi?.RoundEpoch ?? 0,
                _multi?.Revision ?? ++_duelRevision, slots, state.HasSelectedItem.Value, state.IsBasicBlocked.Value);
            return true;
        }
    }
}
