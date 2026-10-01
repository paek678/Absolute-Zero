using System;

namespace AbsoluteZero.Core.Match
{
    /// <summary>Server-owned ghost skill availability. Seats are match seats, not client IDs.</summary>
    public sealed class GhostSkillLedger
    {
        int[] _grudgeAvailableTurn = Array.Empty<int>();
        byte _validSeatsMask;

        public uint MatchEpoch { get; private set; }
        public uint RoundEpoch { get; private set; }
        public int Turn { get; private set; }
        public byte PossessionSpentMask { get; private set; }
        public byte PossessedMask { get; private set; }
        public byte GhostDamageMask { get; private set; }

        public void BeginMatch(uint matchEpoch, int seatCount)
        {
            if (matchEpoch == 0 || seatCount < 1 || seatCount > 8)
                throw new ArgumentOutOfRangeException(nameof(seatCount));
            MatchEpoch = matchEpoch;
            RoundEpoch = 0;
            PossessionSpentMask = 0;
            _grudgeAvailableTurn = new int[seatCount];
            _validSeatsMask = (byte)((1 << seatCount) - 1);
            BeginRound();
        }

        public void BeginRound()
        {
            if (MatchEpoch == 0) throw new InvalidOperationException("Ghost match is not initialized");
            RoundEpoch++;
            if (RoundEpoch == 0) throw new InvalidOperationException("Ghost round epoch exhausted");
            Array.Clear(_grudgeAvailableTurn, 0, _grudgeAvailableTurn.Length);
            Turn = 0;
            PossessedMask = 0;
            GhostDamageMask = 0;
        }

        public void BeginTurn(int turn)
        {
            if (turn <= Turn) throw new ArgumentOutOfRangeException(nameof(turn));
            Turn = turn;
            PossessedMask = 0;
            GhostDamageMask = 0;
        }

        public bool CanUseGrudge(byte actor, byte target)
            => IsValidSeat(actor) && IsValidSeat(target) && actor != target && Turn > 0
                && Turn >= _grudgeAvailableTurn[actor] && (GhostDamageMask & (1 << target)) == 0;

        public bool TryCommitGrudge(byte actor, byte target)
        {
            if (!CanUseGrudge(actor, target)) return false;
            _grudgeAvailableTurn[actor] = checked(Turn + 2);
            GhostDamageMask |= (byte)(1 << target);
            return true;
        }

        public int GetGrudgeAvailableTurn(byte actor)
            => IsValidSeat(actor) ? _grudgeAvailableTurn[actor] : int.MaxValue;

        public bool CanUsePossession(byte actor, byte target)
            => IsValidSeat(actor) && IsValidSeat(target) && actor != target && Turn > 0
                && (PossessionSpentMask & (1 << actor)) == 0
                && (PossessedMask & (1 << target)) == 0;

        public bool TryCommitPossession(byte actor, byte target)
        {
            if (!CanUsePossession(actor, target)) return false;
            PossessionSpentMask |= (byte)(1 << actor);
            PossessedMask |= (byte)(1 << target);
            return true;
        }

        public bool IsPossessed(byte target)
            => IsValidSeat(target) && (PossessedMask & (1 << target)) != 0;

        bool IsValidSeat(byte seat)
            => seat < _grudgeAvailableTurn.Length && (_validSeatsMask & (1 << seat)) != 0;
    }
}
