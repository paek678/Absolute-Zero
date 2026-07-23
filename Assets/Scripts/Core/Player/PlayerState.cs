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

        // 기본 영구 아이템(부채/바람막이) 연속 사용 방지 — 사용 시 다음 턴 영구 아이템 잠금
        public readonly NetworkVariable<bool> IsPermanentLocked = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        readonly ActionQueue _actionQueue = new();
        PlayerInventory _inventory;

        const float MINIGAME_GRACE_SEC = 0.5f;
        int _pendingMiniGameSlot = -1;
        double _pendingMiniGameDeadline;

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
            _pendingMiniGameSlot = -1;
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

            if (HasSelectedItem.Value)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] SelectItem rejected: already selected an item this turn");
                return;
            }

            if (itemData.RequiresMiniGame)
            {
                var tm = Turn.TurnManager.Instance;
                double now = NetworkManager.ServerTime.Time;
                double prepEnd = tm.PrepStartServerTime.Value + tm.PrepDuration.Value;
                _pendingMiniGameSlot = slotIndex;
                _pendingMiniGameDeadline = System.Math.Min(now + itemData.MiniGameTimeLimit, prepEnd) + MINIGAME_GRACE_SEC;

                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game START: {itemData.ItemName} " +
                          $"({itemData.MiniGameType}, {itemData.MiniGameTimeLimit}s, goal={itemData.MiniGameGoal})");

                StartMiniGameClientRpc(slotIndex, (byte)itemData.MiniGameType,
                                       itemData.MiniGameTimeLimit, itemData.MiniGameGoal);
                return;
            }

            ServerQueueItem(slotIndex, itemData);
        }

        void ServerQueueItem(byte slotIndex, ItemDataSO itemData)
        {
            if (HasSelectedItem.Value)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Queue rejected: already selected an item this turn");
                return;
            }

            _actionQueue.SetSelected(slotIndex, itemData);
            HasSelectedItem.Value = true;

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Item selected: {itemData.ItemName} (queued for Attack)");

            Turn.TurnManager.Instance.OnItemUsedClientRpc(
                (byte)SyncedPlayerIndex.Value, slotIndex, (byte)itemData.Category, false);
        }

        [Rpc(SendTo.Owner)]
        void StartMiniGameClientRpc(byte slotIndex, byte miniGameType, float timeLimit, int goal)
        {
            OnMiniGameStart?.Invoke(slotIndex, (MiniGameType)miniGameType, timeLimit, goal);
        }

        [Rpc(SendTo.Server)]
        public void SubmitMiniGameResultServerRpc(byte slotIndex, bool success, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            if (_pendingMiniGameSlot != slotIndex)
            {
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game result rejected: no pending game for slot {slotIndex}");
                return;
            }
            _pendingMiniGameSlot = -1;

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
                Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Mini-game FAILED: slot {slotIndex} — consuming 1 use");
                _inventory.ConsumeItem(slotIndex);
                _inventory.CompactSlots();
                return;
            }

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

            _pendingMiniGameSlot = -1;
            _actionQueue.SetReady(Time.time);
            IsReady.Value = true;
            IsFanActive.Value = false;

            Debug.Log($"[PlayerState P{SyncedPlayerIndex.Value}] Ready pressed (hasItem={HasSelectedItem.Value})");
        }

        // ─── 도발 이모티콘 ──────────────────────────────────────

        double _lastEmoteServerTime = -100.0;
        /// <summary>서버 기준 마지막 도발 발동 시각 (공격 시작 지연 판정용).</summary>
        public double LastEmoteServerTime => _lastEmoteServerTime;

        [Rpc(SendTo.Server)]
        public void SendEmoteServerRpc(byte emoteId, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            var tm = Turn.TurnManager.Instance;
            if (tm == null || tm.CurrentPhase.Value != TurnPhase.PrepPhase) return;   // 프렙에서만
            if (emoteId >= Emote.EmoteCatalog.Count) return;

            _lastEmoteServerTime = NetworkManager.ServerTime.Time;
            ShowEmoteClientRpc(emoteId);
        }

        [Rpc(SendTo.Everyone)]
        void ShowEmoteClientRpc(byte emoteId)
        {
            // 보낸 사람 자신의 화면엔 월드 버블 생략 — 로컬 플레이어는 1인칭이라 유효한 월드 앵커가 없고
            // (netTransform 폴백은 클라 로컬 좌석과 불일치해 오배치됨), 이미 로컬 확인 팝으로 대체됨.
            if (IsOwner) return;

            // 상대 화면: enemy visual 루트에만 앵커 (없으면 표시 생략, 폴백 금지)
            var visual = GetComponent<AZPlayerVisual>();
            Transform root = visual != null ? visual.GetVisualRoot() : null;
            if (root == null) return;

            Emote.EmoteBubble.Show(root, root.position, emoteId);
        }
    }
}
