using System;

namespace AbsoluteZero.Core.Item
{
    internal enum StealDestination { None, Stack, Append }

    internal readonly struct StealPlacement
    {
        internal readonly StealDestination Destination;
        internal readonly byte StackedUses;
        internal StealPlacement(StealDestination destination, byte stackedUses = 0)
        { Destination = destination; StackedUses = stackedUses; }
    }

    // No RNG or inventory writes. Both server applicators keep their existing
    // draw position, copy identity, queue invalidation and transaction lifetime.
    internal static class InventoryMutationCalculations
    {
        internal static bool CanReroll(ItemSlotNetData slot, ItemSlotType? type)
            => IsFiniteCopy(slot) && type == ItemSlotType.Sub;

        internal static bool IsFiniteCopy(ItemSlotNetData slot)
            => !slot.IsEmpty && !slot.IsUnlimited;

        internal static StealPlacement PlaceStolen(ItemSlotNetData? existing, byte incomingUses,
            int destinationCount, int capacity)
        {
            if (existing.HasValue && !existing.Value.IsUnlimited)
                return new StealPlacement(StealDestination.Stack,
                    (byte)Math.Min(existing.Value.RemainingUses + incomingUses, 254));
            return new StealPlacement(destinationCount >= capacity ? StealDestination.None : StealDestination.Append);
        }
    }
}
