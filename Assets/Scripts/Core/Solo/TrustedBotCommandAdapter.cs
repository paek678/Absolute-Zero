using System;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;

namespace AbsoluteZero.Core.Solo
{
    internal sealed class NgoBotActionClock : IBotActionClock
    {
        readonly NetworkManager _network;
        public NgoBotActionClock(NetworkManager network) => _network = network;
        public double Now => _network != null && _network.IsListening ? _network.ServerTime.Time : double.NaN;
    }

    // One adapter belongs to one exact live binding. Caller payloads never choose
    // an actor, a delay or a mini-game outcome. Re-entry creates a fresh adapter and invalidates the old.
    internal sealed class TrustedBotCommandAdapter : IBotItemUseBoundary, IDisposable
    {
        readonly PlayerState _actor;
        readonly PlayerBinding _binding;
        readonly PlayerIdentity _identity;
        readonly ResolvedSoloSettings _settings;
        readonly BotItemUseOperation _operation;
        bool _disposed;
        public bool HasPending => _operation.HasPending;
        internal TrustedBotCommandAdapter(PlayerState actor, ResolvedSoloSettings settings, IBotActionClock clock)
        {
            _actor = actor;
            _binding = actor.Binding;
            _identity = _binding.Identity;
            _settings = settings;
            _operation = new BotItemUseOperation(this, clock);
        }
        public BotUseResult BeginUse(PrepInputKey key, ulong requestId, uint copyId, byte targetSeat)
            => _operation.Begin(key, requestId, copyId, targetSeat);
        public BotUseResult Poll(PrepInputKey key, ulong operationId) => _operation.Poll(key, operationId);
        public BotUseResult CancelSelection(PrepInputKey key) => _operation.CancelSelection(key);
        public BotUseResult Ready(PrepInputKey key) => _operation.Ready(key);
        internal void Tick() => _operation.Tick();
        internal void AbortPending(PrepInputKey key) => _operation.AbortPending(key);

        public bool IsCurrent(PrepInputKey key)
        {
            var router = AppBootstrapper.Instance?.SessionRouter;
            var turn = TurnManager.Instance;
            return !_disposed && _actor != null && _actor.IsServer && _actor.IsSpawned
                && _actor.IsParticipantReady && _actor.IsBotControlled
                && ReferenceEquals(_actor.Binding, _binding) && _binding.Identity == _identity
                && _identity.Generation == key.Generation && router?.Current != null
                && router.IsCurrent(router.Current) && router.Current.Generation == key.Generation
                && ReferenceEquals(router.LaunchContext?.Solo, _settings)
                && turn != null && turn.TurnNumber.Value == key.Turn
                && AbsoluteZero.Core.Match.MatchCompositionRoot.Instance?.MatchManager?.RoundNumber.Value == key.Round;
        }
        public bool TryWindow(PrepInputKey key, out PrepInputSnapshot snapshot)
        {
            snapshot = null;
            return IsCurrent(key) && TurnManager.Instance.TryGetPrepInputSnapshot(out snapshot) && snapshot.Key == key;
        }
        PlayerActionResult IBotItemUseBoundary.Validate(PrepInputKey key, uint copy, byte target, out PlayerActionCandidate candidate)
            => _actor.ServerValidateBotItem(key, copy, target, out candidate);
        bool IBotItemUseBoundary.TryDelay(PlayerActionCandidate candidate, out double delay)
        {
            delay = 0;
            if (!TryCatalogIndex(candidate.ItemData, out short id) || !_settings.TryGetItemDelay(id, out float value)) return false;
            delay = value;
            return true;
        }
        bool IBotItemUseBoundary.RollSuccess(PrepInputKey key, ulong requestId, PlayerActionCandidate candidate)
        {
            if (!TryCatalogIndex(candidate.ItemData, out short id) || !_settings.TryGetMiniGameSuccess(id, out float chance)
                || chance >= 1) return true;
            if (chance <= 0) return false;
            // Private deterministic stream per Prep and request; gameplay RNG is never advanced.
            int seed = unchecked(BotDecisionModel.Seed(_settings.BotId, key) ^ (int)(requestId * 2654435761UL));
            return new System.Random(seed).NextDouble() < chance;
        }
        bool IBotItemUseBoundary.OthersReady(PrepInputKey key)
        {
            var registry = AbsoluteZero.Core.Match.MatchCompositionRoot.Instance?.Registry;
            if (registry == null || !IsCurrent(key)) return false;
            bool any = false;
            foreach (var player in registry.Players)
            {
                if (player == null || !player.IsValid || player.State == null || ReferenceEquals(player.State, _actor)) continue;
                if (!player.State.IsReady.Value) return false;
                any = true;
            }
            return any;
        }
        PlayerActionResult IBotItemUseBoundary.Queue(PrepInputKey key, PlayerActionCandidate candidate)
            => _actor.ServerQueueBotItem(key, candidate, this);
        PlayerActionResult IBotItemUseBoundary.FailMiniGame(PrepInputKey key, PlayerActionCandidate candidate)
            => _actor.ServerFailBotMiniGameItem(key, candidate, this);
        PlayerActionResult IBotItemUseBoundary.CancelSelection(PrepInputKey key) => _actor.ServerCancelBotSelection(key);
        PlayerActionResult IBotItemUseBoundary.Ready(PrepInputKey key) => _actor.ServerPressBotReady(key);
        public void Dispose() { _disposed = true; _operation.Dispose(); }

        bool TryCatalogIndex(ItemDataSO item, out short id)
        {
            for (int i = 0; i < _settings.Catalog.Count; i++)
                if (_settings.Catalog[i] == item) { id = (short)i; return true; }
            id = -1;
            return false;
        }
    }
}
