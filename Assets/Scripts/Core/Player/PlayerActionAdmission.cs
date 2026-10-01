using System;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using UnityEngine;

namespace AbsoluteZero.Core.Player
{
    // Read-only, server-bound legality checks. PlayerState remains the authenticated
    // RPC facade and the only writer of queue/selection/consumption state.
    internal sealed class PlayerActionAdmission
    {
        readonly PlayerState _owner;
        internal PlayerActionAdmission(PlayerState owner)
            => _owner = owner ?? throw new ArgumentNullException(nameof(owner));

        internal bool TryResolveTargetSeat(ItemDataSO itemData, byte clientTarget, out byte resolvedTarget)
        {
            resolvedTarget = ActionIntent.NoTarget;
            if (!IsLiveActionParticipant(_owner.Binding))
                return false;
            var mode = itemData.GetTargetMode();

            if (mode == TargetMode.Self)
                return true;

            byte mySeat = (byte)Mathf.Max(0, _owner.SyncedPlayerIndex.Value);
            var mcr = MatchCompositionRoot.Instance;

            if (clientTarget != ActionIntent.NoTarget)
            {
                if (clientTarget == mySeat)
                {
                    Debug.Log($"[PlayerState P{mySeat}] Target rejected: cannot target self");
                    return false;
                }

                if (mcr == null || !mcr.Registry.TryGetByPlayerIndex(clientTarget, out var target)
                    || !IsEligibleItemTarget(target))
                {
                    Debug.Log($"[PlayerState P{mySeat}] Target rejected: seat {clientTarget} not registered");
                    return false;
                }

                resolvedTarget = clientTarget;
                return true;
            }

            if (mcr?.ActiveConfig != null && mcr.ActiveConfig.RequiredPlayerCount <= 2)
            {
                foreach (var p in mcr.Registry.Players)
                {
                    if (p.Identity.PlayerIndex != mySeat && IsEligibleItemTarget(p))
                    {
                        resolvedTarget = p.Identity.PlayerIndex;
                        return true;
                    }
                }
            }

            Debug.Log($"[PlayerState P{mySeat}] Target rejected: explicit target required for {mcr?.ActiveConfig?.RequiredPlayerCount ?? 0}-player mode");
            return false;
        }

        bool IsEligibleItemTarget(PlayerBinding target)
        {
            return IsLiveActionParticipant(target);
        }

        internal bool IsCurrentActionBinding(PlayerBinding binding)
        {
            var root = MatchCompositionRoot.Instance;
            if (root == null || root.ActiveConfig == null || !root.IsSessionCurrent
                || root.InitializationFailure != null || !ReferenceEquals(root.Registry, _owner.ActionRegistry)
                || binding == null || !binding.IsValid || !binding.HasIdentity || !binding.State.IsParticipantReady
                || binding.Identity.Generation != root.Generation
                || !root.Registry.TryGetByPlayerIndex(binding.Identity.PlayerIndex, out var current)
                || !ReferenceEquals(current, binding)
                || _owner.NetworkManager == null || !_owner.NetworkManager.IsListening || _owner.NetworkManager.ShutdownInProgress)
                return false;
            if (binding.Identity.ControllerKind == PlayerControllerKind.Human)
                return binding.Identity.ClientId.HasValue && binding.NetworkObject.IsPlayerObject
                    && binding.NetworkObject.OwnerClientId == binding.Identity.ClientId.Value
                    && _owner.NetworkManager.ConnectedClients.ContainsKey(binding.Identity.ClientId.Value);
            return root.ActiveConfig.Mode == GameMode.Solo
                && binding.NetworkObject.IsOwnedByServer && !binding.NetworkObject.IsPlayerObject
                && !binding.Identity.ClientId.HasValue
                && root.Roster?.IsParticipantAvailable(binding.Identity.PlayerIndex) == true;
        }

        internal bool IsLiveActionParticipant(PlayerBinding binding)
            => IsCurrentActionBinding(binding) && binding.State.CurrentLifeState.Value == LifeState.Alive
                // Duel deaths use temperature; they need not pass through Multi's
                // Ghost state transition before the round closes.
                && binding.State.Temperature.Value > 0f;

        internal PlayerActionResult CheckSharedActionWindow(bool requireAlive = true)
        {
            if (!_owner.IsServer || !_owner.IsSpawned) return PlayerActionResult.Reject(PlayerActionReason.NotServer);
            if (!_owner.IsParticipantReady) return PlayerActionResult.Stale(PlayerActionReason.ActorNotReady);
            if (!IsCurrentActionBinding(_owner.Binding)) return PlayerActionResult.Stale(PlayerActionReason.StaleSession);
            if (TurnManager.Instance?.IsDeathmatchGrantInProgress == true
                || _owner.ActionTurnContext == null || _owner.ActionTurnContext.Phase != TurnPhase.PrepPhase)
                return PlayerActionResult.Reject(PlayerActionReason.InputClosed);
            if (_owner.IsReady.Value) return PlayerActionResult.Reject(PlayerActionReason.AlreadyReady);
            if (requireAlive && !IsLiveActionParticipant(_owner.Binding))
                return PlayerActionResult.Reject(PlayerActionReason.ActorUnavailable);
            return new PlayerActionResult(PlayerActionStatus.Validated);
        }

        internal PlayerActionResult CheckBotCommand(PrepInputKey key)
        {
            if (!_owner.IsServer || !_owner.IsSpawned) return PlayerActionResult.Reject(PlayerActionReason.NotServer);
            if (!_owner.IsBotControlled) return PlayerActionResult.Reject(PlayerActionReason.WrongController);
            var root = MatchCompositionRoot.Instance;
            var router = AppBootstrapper.Instance?.SessionRouter;
            var tm = TurnManager.Instance;
            if (root?.ActiveConfig?.Mode != GameMode.Solo || router?.Current == null
                || router.Current.Mode != GameMode.Solo || !router.IsCurrent(router.Current)
                || !IsCurrentActionBinding(_owner.Binding) || !ReferenceEquals(_owner.ActionTurnContext, tm))
                return PlayerActionResult.Stale(PlayerActionReason.StaleSession);
            if (key.Generation != root.Generation || key.Round != root.MatchManager?.RoundNumber.Value
                || tm == null || key.Turn != tm.TurnNumber.Value)
                return PlayerActionResult.Stale(PlayerActionReason.StaleCandidate);
            if (!tm.TryGetPrepInputSnapshot(out var snapshot))
                return PlayerActionResult.Reject(PlayerActionReason.InputClosed);
            if (snapshot.Key != key) return PlayerActionResult.Stale(PlayerActionReason.StaleCandidate);
            return CheckSharedActionWindow();
        }

        internal int FindItemCopy(uint copyId)
        {
            if (copyId == 0 || _owner.GetInventory()?.SlotStates == null) return -1;
            for (int i = 0; i < _owner.GetInventory().SlotStates.Count; i++)
                if (!_owner.GetInventory().SlotStates[i].IsEmpty && _owner.GetInventory().SlotStates[i].CopyId == copyId)
                    return i;
            return -1;
        }

        internal PlayerActionResult ValidateItemCandidate(byte slotIndex, uint expectedCopyId, byte requestedTarget,
            PrepInputKey? prepKey, out PlayerActionCandidate candidate)
        {
            candidate = default;
            var window = prepKey.HasValue ? CheckBotCommand(prepKey.Value) : CheckSharedActionWindow();
            if (!window.Succeeded) return window;
            if (_owner.HasPendingMiniGame) return PlayerActionResult.Reject(PlayerActionReason.MiniGamePending);
            if (_owner.HasSelectedItem.Value) return PlayerActionResult.Reject(PlayerActionReason.AlreadySelected);
            if (_owner.GetInventory()?.SlotStates == null || !_owner.GetInventory().IsRegistryReady)
                return PlayerActionResult.Reject(PlayerActionReason.ItemUnavailable);
            if (expectedCopyId != 0)
            {
                int actual = FindItemCopy(expectedCopyId);
                if (actual < 0) return PlayerActionResult.Reject(PlayerActionReason.CopyMissing);
                slotIndex = (byte)actual;
            }
            if (slotIndex >= _owner.GetInventory().SlotStates.Count)
                return PlayerActionResult.Reject(PlayerActionReason.InvalidSlot);
            var slot = _owner.GetInventory().SlotStates[slotIndex];
            var item = _owner.GetInventory().GetItemData(slotIndex);
            if (!slot.IsUsable || !ItemAvailability.IsEnabled(item))
                return PlayerActionResult.Reject(PlayerActionReason.ItemUnavailable);
            if (!TryResolveTargetSeat(item, requestedTarget, out var target))
                return PlayerActionResult.Reject(PlayerActionReason.InvalidTarget);
            var context = _owner.BuildContext(target);
            context.UserSlot = slot;
            context.SlotIndex = slotIndex;
            if (!item.CanUse(context)) return PlayerActionResult.Reject(PlayerActionReason.CannotUse);
            candidate = new PlayerActionCandidate(_owner.Binding, slotIndex, slot.CopyId, target, item, prepKey);
            return new PlayerActionResult(PlayerActionStatus.Validated);
        }

    }
}
