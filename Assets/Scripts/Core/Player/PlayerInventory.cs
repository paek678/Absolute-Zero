using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using Unity.Netcode;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace AbsoluteZero.Core.Player
{
    public class PlayerInventory : NetworkBehaviour
    {
        public NetworkList<ItemSlotNetData> SlotStates;

        const int MAX_SLOTS = 12;
        const int MAX_DUPLICATE_COPIES = 3;

        ItemDataSO[] _itemRegistry;
        short[] _basicItemIds;
        bool _isWindbreakerUnlimited = true;
        bool[] _thresholdGranted = new bool[3];
        bool _topUpCommitInProgress;
        public bool TopUpCommitInProgress => _topUpCommitInProgress;
        uint _nextCopyId;

        // Identical on server and clients; includes slot order, uses and copy identity.
        public uint CurrentFingerprint()
        {
            uint hash = FingerprintStart(SlotStates.Count);
            for (int i = 0; i < SlotStates.Count; i++)
                hash = FingerprintSlot(hash, SlotStates[i]);
            return hash;
        }

        static uint Fingerprint(ItemSlotNetData[] slots)
        {
            uint hash = FingerprintStart(slots.Length);
            for (int i = 0; i < slots.Length; i++)
                hash = FingerprintSlot(hash, slots[i]);
            return hash;
        }

        static uint FingerprintStart(int count) => unchecked(2166136261u ^ (uint)count);

        static uint FingerprintSlot(uint hash, ItemSlotNetData slot)
        {
            unchecked
            {
                hash = (hash ^ (ushort)slot.ItemId) * 16777619u;
                hash = (hash ^ slot.RemainingUses) * 16777619u;
                hash = (hash ^ slot.Flags) * 16777619u;
                return (hash ^ slot.CopyId) * 16777619u;
            }
        }

        public ItemSlotNetData AssignNewCopyId(ItemSlotNetData slot)
        {
            if (!IsServer) throw new System.InvalidOperationException("Only the server may allocate item copies");
            uint next = ++_nextCopyId;
            if (next == 0) throw new System.InvalidOperationException("Item copy identity exhausted");
            slot.CopyId = next;
            return slot;
        }

        void Awake()
        {
            SlotStates = new NetworkList<ItemSlotNetData>();
        }

        public void Initialize(ItemDataSO[] registry)
        {
            _itemRegistry = registry;
        }

        public bool IsRegistryReady => _itemRegistry != null && _itemRegistry.Length > 0;

        public ItemDataSO GetItemData(int slotIndex)
        {
            if (_itemRegistry == null) return null;
            if (slotIndex < 0 || slotIndex >= SlotStates.Count) return null;
            var slot = SlotStates[slotIndex];
            if (slot.IsEmpty || slot.ItemId < 0 || slot.ItemId >= _itemRegistry.Length)
                return null;
            return _itemRegistry[slot.ItemId];
        }

        public void InitializeBasicItems(short fanId, short windbreakerId, short warmTeaId, short catId)
        {
            if (!IsServer) return;

            _basicItemIds = new short[] { fanId, windbreakerId, warmTeaId, catId };

            SlotStates.Add(MakeSlot(fanId, _itemRegistry[fanId]));
            SlotStates.Add(MakeSlot(windbreakerId, _itemRegistry[windbreakerId]));
            SlotStates.Add(MakeSlot(warmTeaId, _itemRegistry[warmTeaId]));
            SlotStates.Add(MakeSlot(catId, _itemRegistry[catId]));
        }

        public int GetRandomSlotCount()
        {
            int count = 0;
            for (int i = 0; i < SlotStates.Count; i++)
            {
                if (SlotStates[i].IsEmpty) continue;
                if (IsBasicItem(SlotStates[i].ItemId)) continue;
                count++;
            }
            return count;
        }

        bool IsBasicItem(short itemId)
        {
            if (_basicItemIds == null) return false;
            for (int i = 0; i < _basicItemIds.Length; i++)
            {
                if (_basicItemIds[i] == itemId) return true;
            }
            return false;
        }

        public bool InitializeBasicItems(short fanId, short windbreakerId, short warmTeaId, short catId, bool isWindbreakerUnlimited)
        {
            if (!IsServer) return false;
            if (_itemRegistry == null || _itemRegistry.Length == 0) return false;
            if (SlotStates.Count > 0) return false;

            var ids = new short[] { fanId, windbreakerId, warmTeaId, catId };
            for (int i = 0; i < ids.Length; i++)
            {
                if (ids[i] < 0 || ids[i] >= _itemRegistry.Length || _itemRegistry[ids[i]] == null)
                    return false;
                for (int j = 0; j < i; j++)
                {
                    if (ids[j] == ids[i]) return false;
                }
            }

            _basicItemIds = ids;
            _isWindbreakerUnlimited = isWindbreakerUnlimited;

            SlotStates.Add(MakeSlot(fanId, _itemRegistry[fanId]));
            if (isWindbreakerUnlimited)
                SlotStates.Add(MakeSlot(windbreakerId, _itemRegistry[windbreakerId]));
            else
                SlotStates.Add(MakeSlotLimited(windbreakerId, _itemRegistry[windbreakerId], 1));
            SlotStates.Add(MakeSlot(warmTeaId, _itemRegistry[warmTeaId]));
            SlotStates.Add(MakeSlot(catId, _itemRegistry[catId]));
            return true;
        }

        public void GrantRandomItems(int count, ItemDropTable dropTable, int maxRandomItems)
        {
            if (!IsServer) return;
            if (count <= 0 || maxRandomItems <= 0) return;
            if (dropTable == null || dropTable.IsEmpty) return;

            int currentRandom = GetRandomSlotCount();
            int allowed = Mathf.Max(0, maxRandomItems - currentRandom);
            int toGrant = Mathf.Min(count, allowed);
            if (toGrant <= 0) return;

            GrantRandomItems(toGrant, dropTable);
        }

        public void ConsumeItem(byte slotIndex)
        {
            if (!IsServer) return;
            if (slotIndex >= SlotStates.Count) return;
            var slot = SlotStates[slotIndex];
            if (slot.IsUnlimited) return;
            if (slot.RemainingUses == 0) return;

            var item = GetItemData(slotIndex);
            string itemName = item != null ? item.ItemName : $"id={slot.ItemId}";
            byte before = slot.RemainingUses;
            slot.RemainingUses--;

            if (slot.RemainingUses <= 0)
            {
                slot.ItemId = -1;
                slot.RemainingUses = 0;
                Debug.Log($"[ITEM] ConsumeItem: '{itemName}' slot={slotIndex} uses {before}→0 (DESTROYED)");
            }
            else
            {
                Debug.Log($"[ITEM] ConsumeItem: '{itemName}' slot={slotIndex} uses {before}→{slot.RemainingUses}");
            }

            SlotStates[slotIndex] = slot;
        }

        public void CompactSlots()
        {
            if (!IsServer) return;
            for (int i = SlotStates.Count - 1; i >= 0; i--)
            {
                if (SlotStates[i].IsEmpty)
                {
                    GetComponent<PlayerState>()?.GetActionQueue()?.OnSlotRemoved((byte)i);
                    SlotStates.RemoveAt(i);
                }
            }
        }

        public int FillRandomSlotsWithSeparateCopies(int maxRandomItems, ItemDropTable dropTable)
        {
            if (!IsServer || maxRandomItems <= 0 || dropTable == null || dropTable.IsEmpty)
                return 0;

            int granted = 0;
            while (GetRandomSlotCount() < maxRandomItems)
            {
                int emptySlot = FindEmptySlot();
                if (emptySlot < 0 && SlotStates.Count >= MAX_SLOTS) break;

                var item = dropTable.Roll(candidate =>
                {
                    short candidateId = FindItemId(candidate);
                    return candidateId >= 0
                        && !IsBasicItem(candidateId)
                        && CountCopies(candidateId) < MAX_DUPLICATE_COPIES;
                });
                if (item == null) break;

                short itemId = FindItemId(item);
                var slot = MakeSlot(itemId, item);
                if (emptySlot >= 0)
                    SlotStates[emptySlot] = slot;
                else
                    SlotStates.Add(slot);
                granted++;
            }

            if (GetRandomSlotCount() < maxRandomItems)
                Debug.LogWarning($"[ITEM] Random top-up stopped at {GetRandomSlotCount()}/{maxRandomItems}: no legal slot or copy remains");
            return granted;
        }

        public sealed class RandomTopUpPlan
        {
            readonly PlayerInventory _owner;
            readonly ItemSlotNetData[] _before;
            readonly ItemSlotNetData[] _after;
            bool _applyStarted;
            int _appliedThrough;

            internal RandomTopUpPlan(PlayerInventory owner, ItemSlotNetData[] before,
                ItemSlotNetData[] after)
            {
                _owner = owner;
                _before = before;
                _after = after;
            }

            public int BeforeCount => _before.Length;
            public int ExpectedCount => _after.Length;
            public uint BeforeFingerprint => Fingerprint(_before);
            public uint ExpectedFingerprint => Fingerprint(_after);

            public bool CanApply()
            {
                if (_owner == null || !_owner.IsServer || _owner.SlotStates.Count != _before.Length)
                    return false;
                for (int i = 0; i < _before.Length; i++)
                    if (!_owner.SlotStates[i].Equals(_before[i])) return false;
                return true;
            }

            public bool TryApply()
            {
                if (!CanApply()) return false;
                _applyStarted = true;
                _appliedThrough = 0;
                _owner._topUpCommitInProgress = true;
                try
                {
                    for (int i = 0; i < _after.Length; i++)
                    {
                        if (i < _before.Length)
                        {
                            if (!_before[i].Equals(_after[i]))
                                _owner.SlotStates[i] = _after[i];
                        }
                        else
                            _owner.SlotStates.Add(_after[i]);
                        _appliedThrough = i + 1;
                    }
                    return true;
                }
                finally { _owner._topUpCommitInProgress = false; }
            }

            public void Restore()
            {
                if (!_applyStarted || _owner == null || !_owner.IsServer) return;
                _owner._topUpCommitInProgress = true;
                try
                {
                    int expectedCount = System.Math.Max(_before.Length, _appliedThrough);
                    if (_owner.SlotStates.Count != expectedCount)
                        throw new System.InvalidOperationException(
                            "Cannot restore a top-up after inventory length changed independently");
                    for (int i = 0; i < expectedCount; i++)
                    {
                        var expected = i < _appliedThrough ? _after[i] : _before[i];
                        if (!_owner.SlotStates[i].Equals(expected))
                            throw new System.InvalidOperationException(
                                $"Cannot restore a top-up after slot {i} changed independently");
                    }
                    while (_owner.SlotStates.Count > _before.Length)
                        _owner.SlotStates.RemoveAt(_owner.SlotStates.Count - 1);
                    for (int i = 0; i < _before.Length; i++)
                    {
                        if (i < _owner.SlotStates.Count) _owner.SlotStates[i] = _before[i];
                        else _owner.SlotStates.Add(_before[i]);
                    }
                    _applyStarted = false;
                    _appliedThrough = 0;
                }
                finally { _owner._topUpCommitInProgress = false; }
            }
        }

        public bool TryPrepareRandomTopUp(int maxRandomItems, ItemDropTable dropTable,
            out RandomTopUpPlan plan)
        {
            plan = null;
            if (!IsServer || !IsRegistryReady || maxRandomItems < 0 || maxRandomItems > MAX_SLOTS)
                return false;
            var before = new ItemSlotNetData[SlotStates.Count];
            var staged = new List<ItemSlotNetData>(SlotStates.Count);
            int randomCount = 0;
            for (int i = 0; i < SlotStates.Count; i++)
            {
                before[i] = SlotStates[i];
                staged.Add(before[i]);
                if (!before[i].IsEmpty && !IsBasicItem(before[i].ItemId)) randomCount++;
            }
            if (randomCount < maxRandomItems && (dropTable == null || dropTable.IsEmpty))
                return false;
            while (randomCount < maxRandomItems)
            {
                int destination = -1;
                for (int i = 0; i < staged.Count; i++)
                    if (staged[i].IsEmpty) { destination = i; break; }
                if (destination < 0 && staged.Count >= MAX_SLOTS) return false;

                var item = dropTable.Roll(candidate =>
                {
                    short candidateId = FindItemId(candidate);
                    if (candidateId < 0 || IsBasicItem(candidateId)) return false;
                    int copies = 0;
                    for (int i = 0; i < staged.Count; i++)
                        if (!staged[i].IsEmpty && staged[i].ItemId == candidateId) copies++;
                    return copies < MAX_DUPLICATE_COPIES;
                });
                if (item == null) return false;
                short itemId = FindItemId(item);
                var slot = MakeSlot(itemId, item);
                if (destination >= 0) staged[destination] = slot;
                else staged.Add(slot);
                randomCount++;
            }
            plan = new RandomTopUpPlan(this, before, staged.ToArray());
            return true;
        }

        public void GrantRandomItems(int count, ItemDropTable dropTable)
        {
            if (!IsServer) return;
            if (dropTable == null || dropTable.IsEmpty) return;

            Debug.Log($"[ITEM] GrantRandomItems: granting {count} items");
            int granted = 0;
            for (int i = 0; i < count; i++)
            {
                var item = RollExcludingOwned(dropTable);
                if (item == null) continue;

                short itemId = FindItemId(item);
                if (itemId < 0) continue;

                int existingSlot = FindSlotByItemId(itemId);
                if (existingSlot >= 0 && !SlotStates[existingSlot].IsUnlimited)
                {
                    var slot = SlotStates[existingSlot];
                    slot.RemainingUses = (byte)Mathf.Min(slot.RemainingUses + Mathf.Max(1, item.MaxUses), 254);
                    SlotStates[existingSlot] = slot;
                    granted++;
                    Debug.Log($"[ITEM] GrantRandomItems: STACKED '{item.ItemName}' on slot={existingSlot} (uses→{slot.RemainingUses})");
                    continue;
                }

                if (SlotStates.Count >= MAX_SLOTS)
                {
                    Debug.Log($"[ITEM] GrantRandomItems: inventory full ({MAX_SLOTS}) — stopped at {granted}/{count}");
                    return;
                }

                SlotStates.Add(MakeSlot(itemId, item));
                granted++;
                Debug.Log($"[ITEM] GrantRandomItems: slot={SlotStates.Count - 1} → '{item.ItemName}' (id={itemId}, cat={item.Category}, uses={item.MaxUses})");
            }
            Debug.Log($"[ITEM] GrantRandomItems: {granted}/{count} items granted");
        }

        public bool GrantSpecificItem(short itemId)
        {
            if (!IsServer) return false;
            if (_itemRegistry == null || itemId < 0 || itemId >= _itemRegistry.Length) return false;

            var item = _itemRegistry[itemId];

            if (!ItemAvailability.IsEnabled(item)) return false;

            int existingSlot = FindSlotByItemId(itemId);
            if (existingSlot >= 0 && !SlotStates[existingSlot].IsUnlimited)
            {
                var slot = SlotStates[existingSlot];
                slot.RemainingUses = (byte)Mathf.Min(slot.RemainingUses + Mathf.Max(1, item.MaxUses), 254);
                SlotStates[existingSlot] = slot;
                return true;
            }

            if (SlotStates.Count >= MAX_SLOTS) return false;
            SlotStates.Add(MakeSlot(itemId, item));
            return true;
        }

        ItemDataSO RollExcludingOwned(ItemDropTable dropTable)
        {
            const int MAX_ATTEMPTS = 20;
            for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                var item = dropTable.Roll();
                if (item == null) return null;

                short itemId = FindItemId(item);
                if (itemId < 0) return null;

                int existingSlot = FindSlotByItemId(itemId);
                if (existingSlot >= 0 && SlotStates[existingSlot].IsUnlimited)
                    continue;

                return item;
            }
            return null;
        }

        int FindSlotByItemId(short itemId)
        {
            for (int i = 0; i < SlotStates.Count; i++)
            {
                if (!SlotStates[i].IsEmpty && SlotStates[i].ItemId == itemId)
                    return i;
            }
            return -1;
        }

        public void RerollAllRandom(ItemDropTable dropTable)
        {
            if (!IsServer) return;
            if (dropTable == null || dropTable.IsEmpty) return;

            Debug.Log($"[ITEM] RerollAllRandom: rerolling Sub slots only");
            for (int i = 0; i < SlotStates.Count; i++)
            {
                if (SlotStates[i].IsEmpty || SlotStates[i].IsUnlimited) continue;
                var existingItem = GetItemData(i);
                if (!InventoryMutationCalculations.CanReroll(SlotStates[i], existingItem != null ? existingItem.SlotType : (ItemSlotType?)null)) continue;

                string oldName = GetItemData(i)?.ItemName ?? $"id={SlotStates[i].ItemId}";
                var newItem = dropTable.Roll();
                if (newItem == null) continue;

                short itemId = FindItemId(newItem);
                if (itemId < 0) continue;

                GetComponent<PlayerState>()?.GetActionQueue()?.InvalidateSlot((byte)i);
                SlotStates[i] = MakeSlot(itemId, newItem);
                Debug.Log($"[ITEM] RerollAllRandom: slot={i} '{oldName}' → '{newItem.ItemName}' (id={itemId})");
            }
        }

        public void StealRandomItem(PlayerInventory otherInventory)
        {
            if (!IsServer) return;

            var occupied = new System.Collections.Generic.List<int>();
            for (int i = 0; i < otherInventory.SlotStates.Count; i++)
            {
                if (InventoryMutationCalculations.IsFiniteCopy(otherInventory.SlotStates[i])
                    && ItemAvailability.IsEnabled(otherInventory.GetItemData(i)))
                    occupied.Add(i);
            }
            if (occupied.Count == 0)
            {
                Debug.Log($"[ITEM] StealRandomItem: target has no stealable items");
                return;
            }

            int targetSlot = occupied[UnityEngine.Random.Range(0, occupied.Count)];
            string stolenName = otherInventory.GetItemData(targetSlot)?.ItemName ?? $"id={otherInventory.SlotStates[targetSlot].ItemId}";
            short stolenItemId = otherInventory.SlotStates[targetSlot].ItemId;

            int existingSlot = FindSlotByItemId(stolenItemId);
            var placement = InventoryMutationCalculations.PlaceStolen(
                existingSlot >= 0 ? SlotStates[existingSlot] : null,
                otherInventory.SlotStates[targetSlot].RemainingUses, SlotStates.Count, MAX_SLOTS);
            if (placement.Destination == StealDestination.Stack)
            {
                var slot = SlotStates[existingSlot];
                slot.RemainingUses = placement.StackedUses;
                SlotStates[existingSlot] = slot;
                Debug.Log($"[ITEM] StealRandomItem: stole '{stolenName}' from opponent slot={targetSlot} → STACKED on slot={existingSlot} (uses→{slot.RemainingUses})");
            }
            else if (placement.Destination == StealDestination.None)
            {
                Debug.Log($"[ITEM] StealRandomItem: inventory full, cannot steal");
                return;
            }
            else
            {
                SlotStates.Add(AssignNewCopyId(otherInventory.SlotStates[targetSlot]));
                Debug.Log($"[ITEM] StealRandomItem: stole '{stolenName}' from opponent slot={targetSlot} → my slot={SlotStates.Count - 1}");
            }

            otherInventory.GetComponent<PlayerState>()?.GetActionQueue()?.InvalidateSlot((byte)targetSlot);
            otherInventory.SlotStates[targetSlot] = ItemSlotNetData.Empty;
        }

        public void ResetForNewRound()
        {
            if (!IsServer) return;

            for (int i = SlotStates.Count - 1; i >= 0; i--)
            {
                var item = GetItemData(i);
                if (item != null && item.Persistence == ItemPersistence.RandomConsumable)
                    SlotStates.RemoveAt(i);
            }

            if (_basicItemIds != null)
            {
                foreach (var basicId in _basicItemIds)
                {
                    if (basicId < 0 || basicId >= _itemRegistry.Length) continue;
                    var basicItem = _itemRegistry[basicId];

                    int existing = FindSlotByItemId(basicId);
                    if (existing >= 0)
                    {
                        var slot = SlotStates[existing];
                        byte uses = GetBasicItemUses(basicId, basicItem);
                        slot.RemainingUses = uses;
                        SlotStates[existing] = slot;
                    }
                    else
                    {
                        if (!_isWindbreakerUnlimited && _basicItemIds.Length > 1 && basicId == _basicItemIds[1])
                            SlotStates.Add(MakeSlotLimited(basicId, basicItem, 1));
                        else
                            SlotStates.Add(MakeSlot(basicId, basicItem));
                    }
                }
            }

            CompactSlots();
            _thresholdGranted = new bool[3];
        }

        public bool[] GetThresholdGranted() => _thresholdGranted;

        short FindItemId(ItemDataSO item)
        {
            if (_itemRegistry == null) return -1;
            for (short i = 0; i < _itemRegistry.Length; i++)
            {
                if (_itemRegistry[i] == item) return i;
            }
            return -1;
        }

        int FindEmptySlot()
        {
            for (int i = 0; i < SlotStates.Count; i++)
                if (SlotStates[i].IsEmpty)
                    return i;
            return -1;
        }

        int CountCopies(short itemId)
        {
            int count = 0;
            for (int i = 0; i < SlotStates.Count; i++)
                if (!SlotStates[i].IsEmpty && SlotStates[i].ItemId == itemId)
                    count++;
            return count;
        }

        ItemSlotNetData MakeSlot(short itemId, ItemDataSO item)
        {
            byte uses = item.MaxUses <= 0 ? (byte)255 : (byte)Mathf.Min(item.MaxUses, 254);
            return AssignNewCopyId(new ItemSlotNetData
            {
                ItemId = itemId,
                RemainingUses = uses,
                Flags = (byte)(item.SlotType == ItemSlotType.Sub ? 0b10 : 0)
            });
        }

        byte GetBasicItemUses(short basicId, ItemDataSO basicItem)
        {
            if (!_isWindbreakerUnlimited && _basicItemIds != null
                && _basicItemIds.Length > 1 && basicId == _basicItemIds[1])
                return 1;
            return basicItem.MaxUses <= 0 ? (byte)255 : (byte)Mathf.Min(basicItem.MaxUses, 254);
        }

        ItemSlotNetData MakeSlotLimited(short itemId, ItemDataSO item, byte uses)
        {
            return AssignNewCopyId(new ItemSlotNetData
            {
                ItemId = itemId,
                RemainingUses = uses,
                Flags = (byte)(item.SlotType == ItemSlotType.Sub ? 0b10 : 0)
            });
        }
    }
}
