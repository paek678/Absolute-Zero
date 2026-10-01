using System;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Match
{
    public enum DeathmatchGrantOutcome : byte { NotEligible, Granted, Faulted }

    /// <summary>Owns the one synchronous, two-recipient top-up transaction for a Multi round.</summary>
    public sealed class DeathmatchGrantCoordinator
    {
        readonly Action<string> _failSession;
        uint _nextTransactionId;
        bool _granted;
        bool _faulted;
        bool _inProgress;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        int _failAfterFirstAttempts;
        public void DebugFailAfterFirstAttempts(int attempts)
            => _failAfterFirstAttempts = Math.Max(0, attempts);
#endif
        public bool IsAdmissionClosed => _inProgress || _faulted;
        public bool IsFaulted => _faulted;

        public DeathmatchGrantCoordinator(Action<string> failSession)
        {
            _failSession = failSession;
        }

        public void BeginRound()
        {
            if (_inProgress) throw new InvalidOperationException("Cannot reset an active top-up");
            _granted = false;
            // A technical session fault cannot be cleared by a round transition.
        }

        public DeathmatchGrantOutcome TryGrant(MatchRoster roster, PlayerState[] players,
            ItemManager items, IGameModeRule rule, MatchNetworkState network, bool terminal)
        {
            if (_faulted) return DeathmatchGrantOutcome.Faulted;
            if (_granted || _inProgress || terminal || roster == null
                || roster.CountAliveForRoundEnd() != 2)
                return DeathmatchGrantOutcome.NotEligible;

            if (rule == null || rule.DeathmatchGrantCount <= 0)
                return Fault("Deathmatch grant rule is unavailable", network, default);

            if (network == null || !network.IsSpawned || !network.IsServer)
                return Fault("MatchNetworkState unavailable", null, default);

            if (_nextTransactionId == uint.MaxValue)
                return Fault("Top-up transaction identity exhausted", network, default);
            uint transaction = ++_nextTransactionId;
            uint round = network.GhostRoundEpoch.Value;
            uint match = network.GhostMatchEpoch.Value;

            var survivors = new PlayerState[2];
            var survivorSeats = new byte[2];
            int found = 0;
            for (byte seat = 0; seat < roster.RequiredPlayerCount; seat++)
            {
                if (!roster.CountsAsAliveForRoundEnd(seat)) continue;
                var player = players != null && seat < players.Length ? players[seat] : null;
                if (player == null || !player.IsSpawned || player.PlayerIndex != seat
                    || player.CurrentLifeState.Value != LifeState.Alive || player.GetInventory() == null)
                    return Fault($"Survivor seat {seat} has no matching inventory", network,
                        new DeathmatchGrantNetData { TransactionId = transaction, RoundEpoch = round });
                survivorSeats[found] = seat;
                survivors[found++] = player;
            }
            if (found != 2)
                return Fault("Roster survivor count changed", network,
                    new DeathmatchGrantNetData { TransactionId = transaction, RoundEpoch = round });

            DeathmatchGrantNetData Descriptor(DeathmatchGrantStage stage)
            {
                var first = survivors[0] != null ? survivors[0].GetInventory() : null;
                var second = survivors[1] != null ? survivors[1].GetInventory() : null;
                return new DeathmatchGrantNetData
                {
                    TransactionId = transaction, RoundEpoch = round, Stage = stage,
                    FirstSeat = survivorSeats[0],
                    SecondSeat = survivorSeats[1],
                    FirstCount = (byte)(first != null ? first.SlotStates.Count : 0),
                    SecondCount = (byte)(second != null ? second.SlotStates.Count : 0),
                    FirstFingerprint = first != null ? first.CurrentFingerprint() : 0,
                    SecondFingerprint = second != null ? second.CurrentFingerprint() : 0
                };
            }

            if (items == null)
                return Fault("ItemManager unavailable", network, Descriptor(DeathmatchGrantStage.Failed));
            var plans = new PlayerInventory.RandomTopUpPlan[2];
            for (int i = 0; i < 2; i++)
                if (!items.TryPrepareDeathmatchItems(survivors[i].GetInventory(), rule, out plans[i]))
                    return Fault($"Cannot prepare survivor seat {survivors[i].PlayerIndex}",
                        network, Descriptor(DeathmatchGrantStage.Failed));

            _inProgress = true;
            try
            {
                network.ServerPublishDeathmatchGrant(Descriptor(DeathmatchGrantStage.Pending));
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        if (!ContextValid(roster, survivors, network, match, round, terminal)
                            || !plans[0].CanApply() || !plans[1].CanApply())
                            throw new InvalidOperationException("Top-up context changed before commit");
                        if (!plans[0].TryApply())
                            throw new InvalidOperationException("First recipient changed during commit");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        if (_failAfterFirstAttempts > 0)
                        {
                            _failAfterFirstAttempts--;
                            throw new InvalidOperationException("Injected failure after first recipient");
                        }
#endif
                        if (!ContextValid(roster, survivors, network, match, round, terminal)
                            || !plans[1].TryApply())
                            throw new InvalidOperationException("Second recipient changed during commit");
                        network.ServerPublishDeathmatchGrant(Descriptor(DeathmatchGrantStage.Completed));
                        _granted = true;
                        return DeathmatchGrantOutcome.Granted;
                    }
                    catch (Exception error)
                    {
                        try
                        {
                            plans[1].Restore();
                            plans[0].Restore();
                            if (!plans[0].CanApply() || !plans[1].CanApply())
                                throw new InvalidOperationException("Restored inventories do not match the captured state");
                        }
                        catch (Exception restoreError)
                        {
                            Debug.LogException(restoreError);
                            return Fault("Deathmatch inventory recovery failed", network,
                                Descriptor(DeathmatchGrantStage.Failed));
                        }
                        if (attempt == 0 && ContextValid(roster, survivors, network, match, round, terminal))
                        {
                            Debug.LogWarning($"[DeathmatchGrant] Retrying the same staged copies: {error.Message}");
                            continue;
                        }
                        return Fault($"Deathmatch top-up failed: {error.Message}", network,
                            Descriptor(DeathmatchGrantStage.Failed));
                    }
                }
                return Fault("Top-up retry exhausted", network, Descriptor(DeathmatchGrantStage.Failed));
            }
            finally { _inProgress = false; }
        }

        static bool ContextValid(MatchRoster roster, PlayerState[] survivors,
            MatchNetworkState network, uint match, uint round, bool terminal)
            => !terminal && network != null && network.IsSpawned && network.IsServer
                && survivors[0] != null && survivors[0].IsSpawned
                && survivors[1] != null && survivors[1].IsSpawned
                && survivors[0].GetInventory() != null && survivors[1].GetInventory() != null
                && network.GhostMatchEpoch.Value == match
                && network.GhostRoundEpoch.Value == round
                && network.TerminalResult.Value.WinnerMask == 0
                && roster.CountAliveForRoundEnd() == 2
                && roster.CountsAsAliveForRoundEnd((byte)survivors[0].PlayerIndex)
                && roster.CountsAsAliveForRoundEnd((byte)survivors[1].PlayerIndex);

        DeathmatchGrantOutcome Fault(string reason, MatchNetworkState network,
            DeathmatchGrantNetData descriptor)
        {
            _faulted = true;
            if (network != null && descriptor.TransactionId != 0)
            {
                descriptor.Stage = DeathmatchGrantStage.Failed;
                try { network.ServerPublishDeathmatchGrant(descriptor); }
                catch (Exception error) { Debug.LogException(error); }
            }
            _failSession?.Invoke(reason);
            return DeathmatchGrantOutcome.Faulted;
        }
    }
}
