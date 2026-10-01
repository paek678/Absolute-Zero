using System;
using AbsoluteZero.Core.Item;
using Unity.Collections;
using Unity.Netcode;

namespace AbsoluteZero.Core.Match
{
    /// <summary>One retained presentation value for every configured Multi seat.</summary>
    public struct MultiInventorySnapshotNetData : INetworkSerializable,
        IEquatable<MultiInventorySnapshotNetData>
    {
        public const int MaxSeats = 4;
        public const int MaxSlots = 12;
        public const int TotalSlots = MaxSeats * MaxSlots;

        public uint MatchEpoch;
        public uint RoundEpoch;
        public uint Revision;
        public uint CommittedGrantTransaction;
        public byte SeatPresenceMask;
        public byte Faulted;
        public byte Count0;
        public byte Count1;
        public byte Count2;
        public byte Count3;
        public FixedList512Bytes<ItemSlotNetData> Slots;

        public bool HasSeat(int seat) => seat >= 0 && seat < MaxSeats
            && (SeatPresenceMask & (1 << seat)) != 0;

        public int GetCount(int seat) => seat switch
        {
            0 => Count0, 1 => Count1, 2 => Count2, 3 => Count3, _ => 0
        };

        public void SetCount(int seat, byte count)
        {
            switch (seat)
            {
                case 0: Count0 = count; break;
                case 1: Count1 = count; break;
                case 2: Count2 = count; break;
                case 3: Count3 = count; break;
                default: throw new ArgumentOutOfRangeException(nameof(seat));
            }
        }

        public ItemSlotNetData GetSlot(int seat, int index)
        {
            if (!HasSeat(seat) || index < 0 || index >= GetCount(seat)
                || Slots.Length != TotalSlots) return ItemSlotNetData.Empty;
            return Slots[seat * MaxSlots + index];
        }

        public bool IsValid(int requiredSeats)
        {
            if (MatchEpoch == 0 || RoundEpoch == 0 || Revision == 0
                || requiredSeats < 1 || requiredSeats > MaxSeats || Slots.Length != TotalSlots
                || Faulted > 1 || (SeatPresenceMask >> requiredSeats) != 0)
                return false;
            for (int seat = 0; seat < MaxSeats; seat++)
                if (GetCount(seat) > MaxSlots || (!HasSeat(seat) && GetCount(seat) != 0))
                    return false;
            return true;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MatchEpoch);
            serializer.SerializeValue(ref RoundEpoch);
            serializer.SerializeValue(ref Revision);
            serializer.SerializeValue(ref CommittedGrantTransaction);
            serializer.SerializeValue(ref SeatPresenceMask);
            serializer.SerializeValue(ref Faulted);
            serializer.SerializeValue(ref Count0);
            serializer.SerializeValue(ref Count1);
            serializer.SerializeValue(ref Count2);
            serializer.SerializeValue(ref Count3);
            if (serializer.IsReader) Slots.Clear();
            for (int i = 0; i < TotalSlots; i++)
            {
                // NGO can serialize the default NetworkVariable before the first
                // server publication. Encode empty slots until a full view exists.
                var slot = serializer.IsReader || Slots.Length != TotalSlots
                    ? ItemSlotNetData.Empty : Slots[i];
                slot.NetworkSerialize(serializer);
                if (serializer.IsReader) Slots.Add(slot);
            }
        }

        public bool Equals(MultiInventorySnapshotNetData other)
        {
            if (MatchEpoch != other.MatchEpoch || RoundEpoch != other.RoundEpoch
                || Revision != other.Revision
                || CommittedGrantTransaction != other.CommittedGrantTransaction
                || SeatPresenceMask != other.SeatPresenceMask || Faulted != other.Faulted
                || Count0 != other.Count0 || Count1 != other.Count1
                || Count2 != other.Count2 || Count3 != other.Count3
                || Slots.Length != other.Slots.Length) return false;
            for (int i = 0; i < Slots.Length; i++)
                if (!Slots[i].Equals(other.Slots[i])) return false;
            return true;
        }

        public override bool Equals(object obj)
            => obj is MultiInventorySnapshotNetData other && Equals(other);
        public override int GetHashCode()
            => HashCode.Combine(MatchEpoch, RoundEpoch, Revision,
                CommittedGrantTransaction, SeatPresenceMask, Faulted);
    }
}
