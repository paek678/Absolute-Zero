using System.Threading.Tasks;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using AbsoluteZero.UI.MiniGame;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.UI.Game.Bridge
{
    public sealed class LocalPlayerCommandAdapter : ILocalPlayerCommands
    {
        readonly IReadOnlyPlayerRegistry _registry;
        PlayerState _localPlayer;
        bool _disposed;

        public LocalPlayerCommandAdapter(IReadOnlyPlayerRegistry registry)
        {
            _registry = registry ?? throw new System.ArgumentNullException(nameof(registry));
            _registry.Registered += OnRegistered;
            _registry.Unregistered += OnUnregistered;
            RebindLocal();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _registry.Registered -= OnRegistered;
            _registry.Unregistered -= OnUnregistered;
            _localPlayer = null;
        }

        void OnRegistered(PlayerBinding _) => RebindLocal();
        void OnUnregistered(PlayerIdentity _) => RebindLocal();

        void RebindLocal()
        {
            _localPlayer = null;
            var match = MatchCompositionRoot.Instance;
            if (_disposed || match == null || !ReferenceEquals(match.Registry, _registry)) return;

            if (LocalMatchPerspective.TryResolveCurrent(out var perspective))
                _localPlayer = perspective.HumanBinding.State;
        }

        // A queued UI callback can arrive after a despawn or scene transition.
        // Re-resolve the human at submission time instead of retaining owner authority.
        bool HasCurrentLocalHuman()
        {
            RebindLocal();
            return _localPlayer != null;
        }

        public bool TrySelectItem(byte slotIndex, uint displayedCopyId)
        {
            return TrySelectItemWithTarget(slotIndex, Core.Player.ActionIntent.NoTarget, displayedCopyId);
        }

        public bool TrySelectItemWithTarget(byte slotIndex, byte targetSeat, uint displayedCopyId)
        {
            if (MatchCompositionRoot.Instance?.NetworkState?.IsDeathmatchGrantViewBlocked == true)
                return false;
            if (!HasCurrentLocalHuman())
            {
                Debug.LogWarning("[CommandAdapter] TrySelectItem: no local player bound");
                return false;
            }

            if (_localPlayer.IsReady.Value) return false;
            if (MiniGameHub.IsRunning) return false;
            if (_localPlayer.HasSelectedItem.Value) return false;

            var state = MatchCompositionRoot.Instance?.NetworkState;
            var view = state?.InventoryReadModel;
            if (MatchCompositionRoot.Instance?.ActiveConfig?.Mode == GameMode.Multi
                && (displayedCopyId == 0 || view == null || !view.HasView)) return false;
            _localPlayer.SelectItemServerRpc(slotIndex, targetSeat, displayedCopyId,
                view?.MatchEpoch ?? 0, view?.RoundEpoch ?? 0,
                view?.CommittedGrantTransaction ?? 0);
            return true;
        }

        public bool TryCancelSelection()
        {
            if (MatchCompositionRoot.Instance?.NetworkState?.IsDeathmatchGrantViewBlocked == true)
                return false;
            if (!HasCurrentLocalHuman() || _localPlayer.IsReady.Value || !_localPlayer.HasSelectedItem.Value)
                return false;

            var view = MatchCompositionRoot.Instance?.NetworkState?.InventoryReadModel;
            _localPlayer.CancelSelectionServerRpc(view?.MatchEpoch ?? 0,
                view?.RoundEpoch ?? 0, view?.CommittedGrantTransaction ?? 0);
            return true;
        }

        public void PressReady()
        {
            if (MatchCompositionRoot.Instance?.NetworkState?.IsDeathmatchGrantViewBlocked == true)
                return;
            if (!HasCurrentLocalHuman())
            {
                Debug.LogWarning("[CommandAdapter] PressReady: no local player bound");
                return;
            }
            var state = MatchCompositionRoot.Instance?.NetworkState;
            var view = state?.InventoryReadModel;
            _localPlayer.PressReadyServerRpc(view?.MatchEpoch ?? 0,
                view?.RoundEpoch ?? 0, view?.CommittedGrantTransaction ?? 0);
        }

        public void UseGhostSkill(byte skillIndex, byte targetSeat, uint requestId)
        {
            if (!HasCurrentLocalHuman()) return;
            if (MatchCompositionRoot.Instance?.NetworkState?.IsDeathmatchGrantViewBlocked == true)
                return;
            var tm = Object.FindAnyObjectByType<Core.Turn.TurnManager>();
            var nState = MatchCompositionRoot.Instance?.NetworkState;
            if (tm == null || nState == null)
            {
                Debug.LogWarning("[CommandAdapter] UseGhostSkill: match state not found");
                return;
            }
            tm.UseGhostSkillRpc(skillIndex, targetSeat, nState.GhostMatchEpoch.Value,
                nState.GhostRoundEpoch.Value, tm.TurnNumber.Value, requestId);
        }

        public async Task LeaveMatchAsync()
        {
            var match = MatchCompositionRoot.Instance;
            if (_disposed || match == null || !match.IsSessionCurrent
                || !ReferenceEquals(match.Registry, _registry)) return;
            var router = AppBootstrapper.Instance?.SessionRouter;
            if (router?.Current != null)
            {
                try { await router.StopAsync(); }
                catch (System.Exception error) { Debug.LogException(error); }
                return;
            }
            var coordinator = Object.FindAnyObjectByType<NetworkSessionCoordinator>();
            if (coordinator == null)
            {
                Debug.LogError("[CommandAdapter] LeaveMatchAsync: NetworkSessionCoordinator not found");
                return;
            }

            try
            {
                await coordinator.LeaveAsync();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
            }
        }

        bool _soloReplayPending;
        public async Task<bool> ReplaySoloAsync()
        {
            var match = MatchCompositionRoot.Instance;
            var app = AppBootstrapper.Instance;
            if (_soloReplayPending || !HasCurrentLocalHuman() || match?.ActiveConfig?.Mode != GameMode.Solo
                || match.MatchManager.CurrentMatchState.Value != MatchState.MatchComplete || app?.SoloSession == null)
                return false;
            _soloReplayPending = true;
            try
            {
                var result = await app.SoloSession.RestartAsync();
                if (result.IsFailure) Debug.LogWarning("[CommandAdapter] Solo replay failed: " + result.ErrorMessage);
                return result.IsSuccess;
            }
            finally { _soloReplayPending = false; }
        }

        public void SubmitRematchDecision(bool accept, uint voteEpoch)
        {
            if (!HasCurrentLocalHuman()) return;
            var mcr = MatchCompositionRoot.Instance;
            if (mcr == null || mcr.MatchManager == null)
            {
                Debug.LogWarning("[CommandAdapter] SubmitRematchDecision: MatchManager not available");
                return;
            }
            mcr.MatchManager.SubmitRematchDecisionRpc(accept, voteEpoch);
        }
    }
}
