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

        readonly ActionQueue _actionQueue = new();
        PlayerInventory _inventory;

        // ═══ 미니게임 (Q11: 서버는 시작/종료만 판정, 게임플레이·판정은 클라이언트) ═══
        const float MINIGAME_GRACE_SEC = 0.5f;   // 결과 RPC 왕복 지연 유예
        int _pendingMiniGameSlot = -1;            // 서버 전용: 진행 중인 미니게임 슬롯 (-1 = 없음)
        double _pendingMiniGameDeadline;          // 서버 전용: ServerTime 기준 결과 제출 마감

        /// <summary>클라(Owner) 전용 — 서버가 미니게임 시작을 승인했을 때 발화 (slot, type, timeLimit, goal)</summary>
        public event System.Action<byte, MiniGameType, float, int> OnMiniGameStart;

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
            _pendingMiniGameSlot = -1;   // 지난 턴에 끝내지 못한 미니게임 대기 정리
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
            if (_pendingMiniGameSlot >= 0)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: mini-game in progress");
                return;
            }
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

            // 슬롯 타입별 선점 검사 — 미니게임 시작 전에 걸러야 헛플레이를 막는다
            if (itemData.SlotType == ItemSlotType.Sub && _actionQueue.hasUsedSub)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: already used sub this turn");
                return;
            }
            if (itemData.SlotType != ItemSlotType.Sub && HasSelectedItem.Value)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: already selected an item this turn");
                return;
            }

            // ═══ 미니게임 아이템: 큐잉 대신 시작 승인 → 클라가 플레이 → 결과 RPC로 종료 판정 ═══
            if (itemData.RequiresMiniGame)
            {
                var tm = Turn.TurnManager.Instance;
                double now = NetworkManager.ServerTime.Time;
                double prepEnd = tm.PrepStartServerTime.Value + tm.PrepDuration.Value;
                _pendingMiniGameSlot = slotIndex;
                // 마감 = min(제한시간, 남은 준비시간) + 유예 — 준비 종료가 마스터 (Q11)
                _pendingMiniGameDeadline = System.Math.Min(now + itemData.MiniGameTimeLimit, prepEnd) + MINIGAME_GRACE_SEC;

                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game START: {itemData.ItemName} " +
                          $"({itemData.MiniGameType}, {itemData.MiniGameTimeLimit}s, goal={itemData.MiniGameGoal})");

                StartMiniGameClientRpc(slotIndex, (byte)itemData.MiniGameType,
                                       itemData.MiniGameTimeLimit, itemData.MiniGameGoal);
                return;
            }

            ServerQueueItem(slotIndex, itemData);
        }

        /// <summary>검증 통과한 아이템을 큐에 넣는다 — 일반 선택과 미니게임 성공이 공유하는 경로 (서버 전용)</summary>
        void ServerQueueItem(byte slotIndex, ItemDataSO itemData)
        {
            if (itemData.SlotType == ItemSlotType.Sub)
            {
                if (_actionQueue.hasUsedSub)
                {
                    Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Queue rejected: already used sub this turn");
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
                    Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Queue rejected: already selected an item this turn");
                    return;
                }

                _actionQueue.SetSelected(slotIndex, itemData);
                HasSelectedItem.Value = true;

                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Main item selected: {itemData.ItemName} (queued for Attack)");

                Turn.TurnManager.Instance.OnItemUsedClientRpc(
                    (byte)SyncedPlayerIndex.Value, slotIndex, (byte)itemData.Category, false);
            }
        }

        /// <summary>서버 → 해당 클라(Owner)에게만 미니게임 시작 통지</summary>
        [Rpc(SendTo.Owner)]
        void StartMiniGameClientRpc(byte slotIndex, byte miniGameType, float timeLimit, int goal)
        {
            OnMiniGameStart?.Invoke(slotIndex, (MiniGameType)miniGameType, timeLimit, goal);
        }

        /// <summary>클라 판정 결과 제출 — 서버는 마감/페이즈/Ready만 검증 (Q11). 실패 시 아이템 미소모 (Q12)</summary>
        [Rpc(SendTo.Server)]
        public void SubmitMiniGameResultServerRpc(byte slotIndex, bool success, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (_pendingMiniGameSlot != slotIndex)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game result rejected: no pending game for slot {slotIndex}");
                return;
            }
            _pendingMiniGameSlot = -1;   // 결과는 1회만 처리 (성공/실패/거부 무관)

            if (Turn.TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game result rejected: prep phase already over");
                return;
            }
            if (IsReady.Value) return;
            if (NetworkManager.ServerTime.Time > _pendingMiniGameDeadline)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game result rejected: past deadline");
                return;
            }

            if (!success)
            {
                // 실패 = 선택 취소만. 아이템 유지 → 재선택으로 재도전 가능 (Q12)
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game FAILED: slot {slotIndex} (item kept)");
                return;
            }

            // 성공 → 미니게임 사이 상태가 변했을 수 있으므로 재검증 후 큐잉
            if (slotIndex >= _inventory.SlotStates.Count) return;
            var slot = _inventory.SlotStates[slotIndex];
            if (!slot.IsUsable) return;

            var itemData = _inventory.GetItemData(slotIndex);
            if (itemData == null) return;

            var ctx = BuildContext();
            ctx.UserSlot = slot;
            ctx.SlotIndex = slotIndex;
            if (!itemData.CanUse(ctx)) return;

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game SUCCESS: {itemData.ItemName} → queueing");
            ServerQueueItem(slotIndex, itemData);
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

            _pendingMiniGameSlot = -1;   // 미니게임 중 Ready 강행 시 대기 정리 (늦은 결과는 IsReady로도 거부됨)
            _actionQueue.SetReady(Time.time);
            IsReady.Value = true;
            IsFanActive.Value = false;

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Ready pressed (hasItem={HasSelectedItem.Value})");
        }
    }
}
