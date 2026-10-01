using System;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Match
{
    public sealed class GhostSkillService : IDisposable
    {
        public const byte SKILL_GRUDGE = 0;
        public const byte SKILL_POSSESSION = 1;
        public const float GRUDGE_DAMAGE = 3f;
        public const byte GRUDGE_COOLDOWN_TURNS = 2;

        readonly GhostSkillLedger _ledger = new();
        bool _disposed;

        public GhostSkillLedger Ledger => _ledger;

        // Preserve the composition-root constructor shape while moving mutable skill
        // state out of PlayerModifiers. Existing fan/recovery modifiers remain unrelated.
        public GhostSkillService(MatchRoster roster, PlayerModifiers[] modifiers) { }

        public void BeginMatch(MatchNetworkState netState, int seatCount)
        {
            if (_disposed || netState == null || netState.GhostMatchEpoch.Value == 0) return;
            _ledger.BeginMatch(netState.GhostMatchEpoch.Value, seatCount);
            netState.ServerPublishGhostLedger(_ledger);
        }

        public void BeginRound(MatchNetworkState netState)
        {
            if (_disposed || netState == null || _ledger.MatchEpoch == 0) return;
            _ledger.BeginRound();
            netState.ServerPublishGhostLedger(_ledger);
        }

        public void BeginTurn(MatchNetworkState netState, int turn)
        {
            if (_disposed || netState == null || _ledger.MatchEpoch == 0) return;
            _ledger.BeginTurn(turn);
            netState.ServerPublishGhostLedger(_ledger);
        }

        public bool TryUseGrudge(byte ghostSeat, byte targetSeat,
            MatchNetworkState netState, AuthoritativeDeathService deathService,
            PlayerState[] players, ISeatStateAccessor roster)
        {
            if (_disposed || netState == null || deathService == null || players == null
                || roster == null || targetSeat >= players.Length || players[targetSeat] == null
                || !roster.IsConnected(targetSeat) || roster.GetLifeState(targetSeat) != LifeState.Alive
                || !_ledger.CanUseGrudge(ghostSeat, targetSeat)) return false;

            float before = players[targetSeat].Temperature.Value;
            float after = Mathf.Max(TemperatureSystem.MIN_TEMP, before - GRUDGE_DAMAGE);
            if (!_ledger.TryCommitGrudge(ghostSeat, targetSeat)) return false;
            players[targetSeat].Temperature.Value = after;
            netState.ServerSetCooldown(ghostSeat, SKILL_GRUDGE, GRUDGE_COOLDOWN_TURNS);
            netState.ServerPublishGhostLedger(_ledger);

            if (after <= TemperatureSystem.MIN_TEMP)
            {
                deathService.TryKill(targetSeat, DamageSource.Create(ghostSeat, DamageOrigin.GhostFrost));
                deathService.FlushDeathQueue();
            }
            Debug.Log($"[Ghost] Grudge P{ghostSeat} → P{targetSeat}: {before:F1}→{after:F1}°");
            return true;
        }

        public bool TryUsePossession(byte ghostSeat, byte targetSeat,
            MatchNetworkState netState, ISeatStateAccessor roster)
        {
            if (_disposed || netState == null || roster == null
                || !roster.IsConnected(targetSeat) || roster.GetLifeState(targetSeat) != LifeState.Alive
                || !_ledger.TryCommitPossession(ghostSeat, targetSeat)) return false;
            netState.ServerPublishGhostLedger(_ledger);
            Debug.Log($"[Ghost] Possession P{ghostSeat} → P{targetSeat} for turn {_ledger.Turn}");
            return true;
        }

        public void Dispose() => _disposed = true;
    }
}
