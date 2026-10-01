#if UNITY_EDITOR || DEVELOPMENT_BUILD
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Solo
{
    internal static class ProbeInventoryCommands
    {
        public static void SelectItem(PlayerState player, byte slot,
            byte target = ActionIntent.NoTarget)
        {
            var inventory = player.GetInventory();
            uint copy = inventory != null && slot < inventory.SlotStates.Count
                ? inventory.SlotStates[slot].CopyId : 0;
            var state = MatchCompositionRoot.Instance?.NetworkState;
            player.SelectItemServerRpc(slot, target, copy,
                state?.GhostMatchEpoch.Value ?? 0, state?.GhostRoundEpoch.Value ?? 0,
                state?.InventoryReadModel?.CommittedGrantTransaction ?? 0);
        }

        public static void Ready(PlayerState player)
        {
            var state = MatchCompositionRoot.Instance?.NetworkState;
            var grant = state?.DeathmatchGrant.Value ?? default;
            player.PressReadyServerRpc(state?.GhostMatchEpoch.Value ?? 0,
                state?.GhostRoundEpoch.Value ?? 0,
                grant.Stage == DeathmatchGrantStage.Completed ? grant.TransactionId : 0);
        }
    }
}
#endif
