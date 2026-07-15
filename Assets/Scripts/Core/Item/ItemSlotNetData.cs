using System;
using Unity.Netcode;

namespace AbsoluteZero.Core.Item
{
    public struct ItemSlotNetData : IEquatable<ItemSlotNetData>, INetworkSerializable
    {
        public short ItemId;
        public byte RemainingUses;
        public byte Flags;

        public bool IsUnlimited => RemainingUses == 255;
        public bool IsEmpty => ItemId < 0;
        public bool IsUsable => !IsEmpty && (IsUnlimited || RemainingUses > 0);

        public static ItemSlotNetData Empty => new() { ItemId = -1, RemainingUses = 0, Flags = 0 };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref RemainingUses);
            serializer.SerializeValue(ref Flags);
        }

        public bool Equals(ItemSlotNetData other)
        {
            return ItemId == other.ItemId
                && RemainingUses == other.RemainingUses
                && Flags == other.Flags;
        }

        public override bool Equals(object obj) => obj is ItemSlotNetData other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ItemId, RemainingUses, Flags);
    }
}
