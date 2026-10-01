using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Session;
using Unity.Netcode;

namespace AbsoluteZero.Core.Player.Identity
{
    // A read view over the current registry, not another owner of participant state.
    // Every use rechecks exact membership so a despawned/replaced human cannot keep
    // assigning the old seat to the next network object or match.
    public readonly struct LocalMatchPerspective
    {
        readonly IReadOnlyPlayerRegistry _registry;
        public PlayerBinding HumanBinding { get; }
        public byte HumanSeat => HumanBinding?.Identity.PlayerIndex ?? byte.MaxValue;

        LocalMatchPerspective(IReadOnlyPlayerRegistry registry, PlayerBinding human)
        { _registry = registry; HumanBinding = human; }

        public static bool TryResolve(IReadOnlyPlayerRegistry registry, ulong localClientId,
            out LocalMatchPerspective perspective)
        {
            perspective = default;
            if (registry == null || !registry.TryGetByClientId(localClientId, out var human)
                || !IsCurrentBinding(registry, human)
                || human.Identity.ControllerKind != PlayerControllerKind.Human
                || human.Identity.ClientId != localClientId) return false;
            perspective = new LocalMatchPerspective(registry, human);
            return true;
        }

        public static bool TryResolveCurrent(out LocalMatchPerspective perspective)
        {
            perspective = default;
            var nm = NetworkManager.Singleton;
            var match = MatchCompositionRoot.Instance;
            var router = AppBootstrapper.Instance?.SessionRouter;
            if (nm == null || !nm.IsListening || !nm.IsClient || nm.ShutdownInProgress
                || match == null || !match.IsSessionCurrent
                || (router?.Current != null && !router.IsCurrent(router.Current))) return false;
            return TryResolve(match.Registry, nm.LocalClientId, out perspective);
        }

        public static bool IsLocalHuman(PlayerState state)
            => state != null && TryResolveCurrent(out var perspective)
                && ReferenceEquals(perspective.HumanBinding.State, state);

        public bool IsHumanSeat(int seat)
            => IsCurrentBinding(_registry, HumanBinding) && HumanSeat == seat;

        public bool TryGetBinding(int seat, out PlayerBinding binding)
        {
            binding = null;
            if (seat < 0 || seat >= 4 || !IsCurrentBinding(_registry, HumanBinding)
                || !_registry.TryGetByPlayerIndex((byte)seat, out var current)
                || !IsCurrentBinding(_registry, current)
                || current.Identity.Generation != HumanBinding.Identity.Generation) return false;
            binding = current;
            return true;
        }

        public bool TryGetOpponentBinding(out PlayerBinding binding)
        {
            binding = null;
            if (!IsCurrentBinding(_registry, HumanBinding)) return false;
            foreach (var candidate in _registry.Players)
            {
                if (ReferenceEquals(candidate, HumanBinding) || !IsCurrentBinding(_registry, candidate)
                    || candidate.Identity.Generation != HumanBinding.Identity.Generation) continue;
                // The relative "opponent" alias belongs to duel presentation. Multi
                // callers must request a seat instead of selecting an arbitrary peer.
                if (binding != null) { binding = null; return false; }
                binding = candidate;
            }
            return binding != null;
        }

        static bool IsCurrentBinding(IReadOnlyPlayerRegistry registry, PlayerBinding binding)
            => registry != null && binding != null && binding.HasIdentity && binding.IsValid
                && registry.TryGetByPlayerIndex(binding.Identity.PlayerIndex, out var current)
                && ReferenceEquals(current, binding)
                && registry.TryGetByParticipantId(binding.Identity.ParticipantId, out var participant)
                && ReferenceEquals(participant, binding);
    }
}
