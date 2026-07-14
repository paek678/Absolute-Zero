using System;
using Unity.Netcode;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// 인벤토리 슬롯 1칸의 동기화 데이터 (SYSTEM_ARCHITECTURE.md Section 1.2).
    /// PlayerInventory.SlotStates(NetworkList)에 담겨 서버→클라이언트 자동 동기화.
    /// </summary>
    public struct ItemSlotNetData : IEquatable<ItemSlotNetData>, INetworkSerializable
    {
        public short ItemId;          // -1 = empty, 그 외 = ItemRegistry 인덱스
        public byte RemainingUses;    // 0 = 소모됨/빈 슬롯, 255 = 무한 (Permanent)
        public byte Flags;            // bit 0: 청테이프 봉쇄, bit 1: Sub 타입

        // FIX-11: Empty 정적 프로퍼티 정의 (FindEmptyRandomSlot에서 사용)
        public static ItemSlotNetData Empty => new() { ItemId = -1, RemainingUses = 0, Flags = 0 };

        // FIX-03: 영구 아이템 체크 (RemainingUses == 255 = unlimited)
        public bool IsUnlimited => RemainingUses == 255;
        public bool IsEmpty => ItemId == -1;
        public bool IsUsable => !IsEmpty && (IsUnlimited || RemainingUses > 0) && !IsBlocked;
        public bool IsBlocked => (Flags & 1) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ItemId);
            serializer.SerializeValue(ref RemainingUses);
            serializer.SerializeValue(ref Flags);
        }

        public bool Equals(ItemSlotNetData other)
            => ItemId == other.ItemId && RemainingUses == other.RemainingUses && Flags == other.Flags;
    }
}
