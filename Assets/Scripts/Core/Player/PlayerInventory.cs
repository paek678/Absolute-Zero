using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Player
{
    public class PlayerInventory : NetworkBehaviour
    {
        public NetworkList<ItemSlotNetData> SlotStates;

        const int BASIC_SLOT_COUNT = 4;
        const int MAX_RANDOM_SLOTS = 8;
        const int MAX_SLOTS = BASIC_SLOT_COUNT + MAX_RANDOM_SLOTS;

        ItemDataSO[] _itemRegistry;
        bool[] _thresholdGranted = new bool[3];

        void Awake()
        {
            SlotStates = new NetworkList<ItemSlotNetData>();
        }

        public void Initialize(ItemDataSO[] registry)
        {
            _itemRegistry = registry;
        }

        public ItemDataSO GetItemData(int slotIndex)
        {
            var slot = SlotStates[slotIndex];
            if (slot.IsEmpty || slot.ItemId < 0 || slot.ItemId >= _itemRegistry.Length)
                return null;
            return _itemRegistry[slot.ItemId];
        }

        public void InitializeBasicItems(short fanId, short windbreakerId, short warmTeaId, short catId)
        {
            if (!IsServer) return;

            SlotStates.Add(MakeSlot(fanId, _itemRegistry[fanId]));
            SlotStates.Add(MakeSlot(windbreakerId, _itemRegistry[windbreakerId]));
            SlotStates.Add(MakeSlot(warmTeaId, _itemRegistry[warmTeaId]));
            SlotStates.Add(MakeSlot(catId, _itemRegistry[catId]));
        }

        public void ConsumeItem(byte slotIndex)
        {
            if (!IsServer) return;
            var slot = SlotStates[slotIndex];
            if (slot.IsUnlimited) return;
            if (slot.RemainingUses == 0) return;

            slot.RemainingUses--;

            if (slot.RemainingUses <= 0)
            {
                var item = GetItemData(slotIndex);
                if (item != null && item.Persistence == ItemPersistence.RandomConsumable)
                    slot.ItemId = -1;
                slot.RemainingUses = 0;
            }

            SlotStates[slotIndex] = slot;
        }

        public void GrantRandomItems(int count, ItemDropTable dropTable)
        {
            if (!IsServer) return;
            if (dropTable == null || dropTable.IsEmpty) return;

            for (int i = 0; i < count; i++)
            {
                int emptySlot = FindEmptyRandomSlot();
                if (emptySlot < 0) return;

                var item = dropTable.Roll();
                if (item == null) continue;

                short itemId = FindItemId(item);
                if (itemId < 0) continue;

                SlotStates[emptySlot] = MakeSlot(itemId, item);
            }
        }

        public void RerollAllRandom(ItemDropTable dropTable)
        {
            if (!IsServer) return;
            if (dropTable == null || dropTable.IsEmpty) return;

            for (int i = BASIC_SLOT_COUNT; i < SlotStates.Count; i++)
            {
                if (SlotStates[i].IsEmpty) continue;

                var newItem = dropTable.Roll();
                if (newItem == null) continue;

                short itemId = FindItemId(newItem);
                if (itemId < 0) continue;

                SlotStates[i] = MakeSlot(itemId, newItem);
            }
        }

        public void StealRandomItem(PlayerInventory otherInventory)
        {
            if (!IsServer) return;

            var occupied = new System.Collections.Generic.List<int>();
            for (int i = BASIC_SLOT_COUNT; i < otherInventory.SlotStates.Count; i++)
            {
                if (!otherInventory.SlotStates[i].IsEmpty) occupied.Add(i);
            }
            if (occupied.Count == 0) return;

            int targetSlot = occupied[UnityEngine.Random.Range(0, occupied.Count)];
            int myEmptySlot = FindEmptyRandomSlot();
            if (myEmptySlot < 0) return;

            SlotStates[myEmptySlot] = otherInventory.SlotStates[targetSlot];
            otherInventory.SlotStates[targetSlot] = ItemSlotNetData.Empty;
        }

        public void ResetForNewRound()
        {
            if (!IsServer) return;

            for (int i = 0; i < BASIC_SLOT_COUNT && i < SlotStates.Count; i++)
            {
                var slot = SlotStates[i];
                if (slot.IsEmpty || slot.IsUnlimited) continue;

                var item = GetItemData(i);
                if (item != null && item.Persistence == ItemPersistence.BasicConsumable)
                {
                    slot.RemainingUses = (byte)Mathf.Max(1, item.MaxUses);
                    SlotStates[i] = slot;
                }
            }

            for (int i = BASIC_SLOT_COUNT; i < SlotStates.Count; i++)
            {
                SlotStates[i] = ItemSlotNetData.Empty;
            }

            _thresholdGranted = new bool[3];
        }

        public bool[] GetThresholdGranted() => _thresholdGranted;

        int FindEmptyRandomSlot()
        {
            while (SlotStates.Count < MAX_SLOTS)
            {
                SlotStates.Add(ItemSlotNetData.Empty);
            }

            for (int i = BASIC_SLOT_COUNT; i < SlotStates.Count; i++)
            {
                if (SlotStates[i].IsEmpty) return i;
            }
            return -1;
        }

        short FindItemId(ItemDataSO item)
        {
            if (_itemRegistry == null) return -1;
            for (short i = 0; i < _itemRegistry.Length; i++)
            {
                if (_itemRegistry[i] == item) return i;
            }
            return -1;
        }

        ItemSlotNetData MakeSlot(short itemId, ItemDataSO item)
        {
            byte uses = item.MaxUses <= 0 ? (byte)255 : (byte)Mathf.Min(item.MaxUses, 254);
            return new ItemSlotNetData
            {
                ItemId = itemId,
                RemainingUses = uses,
                Flags = (byte)(item.SlotType == ItemSlotType.Sub ? 0b10 : 0)
            };
        }
    }
}
