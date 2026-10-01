using System.Collections.Generic;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;

namespace AbsoluteZero.Core.Turn
{
    // Synchronous batch work only. Root owns the initialization latch/generation;
    // TurnManager owns waits, failure routing, grants and phase advancement.
    internal static class TurnParticipantSetup
    {
        internal static bool AreViewsReady(PlayerBinding[] bindings)
        {
            foreach (var binding in bindings)
                if (!binding.IsValid || !binding.State.TryGetComponent<AZPlayerVisual>(out var visual)
                    || !visual.IsPresentationReady) return false;
            return true;
        }

        internal static void AssignPending(PlayerRegistry registry,
            IReadOnlyList<MatchParticipantDescriptor> participants)
        {
            foreach (var pending in new List<PlayerBinding>(registry.EnumeratePending()))
            {
                if (!pending.IsValid) continue;
                foreach (var participant in participants)
                {
                    bool matches = pending.State.Participant != null
                        ? pending.State.Participant.ParticipantId == participant.ParticipantId
                        : participant.ControllerKind == PlayerControllerKind.Human
                            && participant.ClientId == pending.NetworkObject.OwnerClientId;
                    if (matches) { pending.State.AssignParticipant(participant); break; }
                }
            }
        }

        internal static void ValidateBatch(PlayerBinding[] ready, IGameModeRule rule, int requiredCount)
        {
            if (rule == null || ready == null || ready.Length != requiredCount)
                throw new System.InvalidOperationException("Rule or expected participant set is missing");
            // Validate the complete batch before any grant or round mutation.
            for (int i = 0; i < ready.Length; i++)
                if (!ready[i].IsValid || ready[i].Inventory == null || !ready[i].Inventory.IsSpawned
                    || ready[i].State.PlayerIndex != i || ready[i].Inventory.SlotStates.Count != 0)
                    throw new System.InvalidOperationException($"Seat {i} inventory/identity is not ready for initial grant");
        }

        internal static void BindStates(PlayerBinding[] ready, PlayerState[] players, ITurnContext turn)
        {
            for (int i = 0; i < ready.Length; i++)
            {
                players[i] = ready[i].State;
                players[i].Initialize(i, ready[i].Inventory);
                players[i].BindTurnContext(turn);
            }
        }
    }
}
