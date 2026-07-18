using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    public class ItemManager : NetworkBehaviour
    {
        public static ItemManager Instance { get; private set; }

        [Header("Item Registry")]
        [SerializeField] ItemDataSO[] allItems;

        [Header("Basic Item IDs (index in allItems)")]
        [SerializeField] short fanItemId = 0;
        [SerializeField] short windbreakerItemId = 1;
        [SerializeField] short warmTeaItemId = 2;
        [SerializeField] short catItemId = 3;

        ItemDropTable _dropTable;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            Instance = this;

            if (allItems == null || allItems.Length == 0)
            {
                Debug.LogError("[ItemManager] allItems array is empty! Assign SO assets in Inspector.");
                return;
            }

            if (IsServer)
                _dropTable = new ItemDropTable(allItems);

            Debug.Log($"[ItemManager] {allItems.Length} items loaded from SO assets");
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        public void InitializePlayerInventory(PlayerInventory inventory)
        {
            if (!IsServer) return;

            inventory.Initialize(allItems);
            inventory.InitializeBasicItems(fanItemId, windbreakerItemId, warmTeaItemId, catItemId);

            if (_dropTable != null)
                inventory.GrantRandomItems(4, _dropTable);
        }

        public void InitializeClientRegistry(PlayerInventory inventory)
        {
            if (allItems != null)
                inventory.Initialize(allItems);
        }

        public ItemDropTable GetDropTable() => _dropTable;
        public ItemDataSO[] GetAllItems() => allItems;

        public ItemDataSO GetItemData(short itemId)
        {
            if (itemId < 0 || itemId >= allItems.Length) return null;
            return allItems[itemId];
        }
    }
}
