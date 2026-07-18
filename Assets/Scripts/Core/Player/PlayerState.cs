using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Player
{
    public class PlayerState : NetworkBehaviour
    {
        public readonly NetworkVariable<float> Temperature = new(
            37f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> FanSpeed = new(
            1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsReady = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsFanActive = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> SyncedPlayerIndex = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> HasSelectedItem = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsFanUpgraded = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> IsBasicBlocked = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly ActionQueue _actionQueue = new();
        PlayerInventory _inventory;

        public int PlayerIndex => SyncedPlayerIndex.Value;
        public ActionQueue GetActionQueue() => _actionQueue;

        public PlayerInventory GetInventory()
        {
            if (_inventory == null)
                _inventory = GetComponent<PlayerInventory>();
            return _inventory;
        }

        public void Initialize(int playerIndex, PlayerInventory inventory)
        {
            SyncedPlayerIndex.Value = playerIndex;
            _inventory = inventory;
        }

        public void ResetForNewTurn()
        {
            HasSelectedItem.Value = false;
            _actionQueue.Clear();
        }

        ItemContext BuildContext()
        {
            var tm = Turn.TurnManager.Instance;
            int myIndex = SyncedPlayerIndex.Value;
            var opponent = myIndex == 0 ? tm.GetPlayer(1) : tm.GetPlayer(0);
            return new ItemContext
            {
                User = this,
                Target = opponent,
                UserIndex = myIndex,
                TargetIndex = opponent.PlayerIndex,
                UserInventory = _inventory,
                TargetInventory = opponent.GetInventory(),
                AllModifiers = tm.GetModifiers(),
                TempSystem = tm.GetTempSystem(),
                BuffSystem = tm.GetBuffSystem(),
                DropTable = tm.GetDropTable(),
            };
        }

        [Rpc(SendTo.Server)]
        public void SelectItemServerRpc(byte slotIndex, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (Turn.TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase) return;
            if (IsReady.Value) return;
            if (slotIndex >= _inventory.SlotStates.Count) return;

            var slot = _inventory.SlotStates[slotIndex];
            if (!slot.IsUsable)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: slot {slotIndex} not usable");
                return;
            }

            var itemData = _inventory.GetItemData(slotIndex);
            if (itemData == null) return;

            var ctx = BuildContext();
            ctx.UserSlot = slot;
            ctx.SlotIndex = slotIndex;
            if (!itemData.CanUse(ctx))
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: CanUse false");
                return;
            }

            if (itemData.SlotType == ItemSlotType.Sub)
            {
                if (itemData.IsFreeAction)
                {
                    ExecuteFreeAction(slotIndex, itemData, ctx);
                    Turn.TurnManager.Instance.OnItemUsedClientRpc(
                        (byte)SyncedPlayerIndex.Value, slotIndex, (byte)itemData.Category, true);
                    return;
                }

                if (_actionQueue.hasUsedSub)
                {
                    Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: already used sub this turn");
                    return;
                }

                _actionQueue.SetSub(slotIndex, itemData);

                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Sub item queued: {itemData.ItemName} (executes at attack start)");

                Turn.TurnManager.Instance.OnItemUsedClientRpc(
                    (byte)SyncedPlayerIndex.Value, slotIndex, (byte)itemData.Category, true);
            }
            else
            {
                if (HasSelectedItem.Value)
                {
                    Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: already selected an item this turn");
                    return;
                }

                _actionQueue.SetSelected(slotIndex, itemData);
                HasSelectedItem.Value = true;

                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Main item selected: {itemData.ItemName} (queued for Attack)");

                Turn.TurnManager.Instance.OnItemUsedClientRpc(
                    (byte)SyncedPlayerIndex.Value, slotIndex, (byte)itemData.Category, false);
            }
        }

        void ExecuteFreeAction(byte slotIndex, ItemDataSO itemData, ItemContext ctx)
        {
            itemData.ExecuteEffect(ctx);
            _inventory.ConsumeItem(slotIndex);

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Free action executed: {itemData.ItemName}");

            if (ctx.UserModifiers.OpponentRevealed)
            {
                var opponent = ctx.Target;
                var oppQueue = opponent.GetActionQueue();
                short oppItemId = -1;
                if (oppQueue.selectedAction.HasValue)
                {
                    var oppInv = opponent.GetInventory();
                    byte oppSlot = oppQueue.selectedAction.Value.SlotIndex;
                    if (oppSlot < oppInv.SlotStates.Count)
                        oppItemId = oppInv.SlotStates[oppSlot].ItemId;
                }
                Turn.TurnManager.Instance.RevealOpponentItemClientRpc(
                    (byte)SyncedPlayerIndex.Value, oppItemId);
            }
        }

        [Rpc(SendTo.Server)]
        public void CancelSelectionServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (Turn.TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase) return;
            if (IsReady.Value) return;
            if (!HasSelectedItem.Value) return;

            _actionQueue.selectedAction = null;
            HasSelectedItem.Value = false;

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Selection cancelled");
        }

        [Rpc(SendTo.Server)]
        public void PressReadyServerRpc(RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (Turn.TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase) return;
            if (IsReady.Value) return;

            _actionQueue.SetReady(Time.time);
            IsReady.Value = true;
            IsFanActive.Value = false;

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Ready pressed (hasItem={HasSelectedItem.Value})");
        }
    }
}
