namespace AbsoluteZero.Core.Item
{
    // No RNG or inventory writes. Both server applicators keep their existing
    // draw position, copy identity, queue invalidation and transaction lifetime.
    internal static class InventoryMutationCalculations
    {
        internal const int MaxGeneratedCopies = 3;

        internal static bool CanGenerateCopy(short itemId, bool unlimited,
            System.Collections.Generic.IEnumerable<ItemSlotNetData> slots, int excludedSlot = -1)
        {
            if (itemId < 0 || slots == null) return false;
            int copies = 0, index = 0;
            foreach (var slot in slots)
            {
                bool excluded = index++ == excludedSlot;
                if (excluded || slot.IsEmpty || slot.ItemId != itemId) continue;
                if (slot.IsUnlimited) return false;
                copies++;
            }
            return unlimited ? copies == 0 : copies < MaxGeneratedCopies;
        }

        internal static bool CanReroll(ItemSlotNetData slot, ItemSlotType? type)
            => IsFiniteCopy(slot) && type == ItemSlotType.Sub;

        internal static bool IsFiniteCopy(ItemSlotNetData slot)
            => !slot.IsEmpty && !slot.IsUnlimited;

        internal static int SeparateCopySlot(int slotCount, int firstEmptySlot, int capacity)
        {
            if (firstEmptySlot >= 0 && firstEmptySlot < slotCount && firstEmptySlot < capacity)
                return firstEmptySlot;
            return slotCount >= 0 && slotCount < capacity ? slotCount : -1;
        }
    }
}
