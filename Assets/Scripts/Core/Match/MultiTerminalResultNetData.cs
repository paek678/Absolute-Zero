using System;
using Unity.Netcode;

namespace AbsoluteZero.Core.Match
{
    public static class MultiVictoryRules
    {
        // Round cap is evaluated only after a completed round. Ghosts retain their
        // earned score; departed seats are excluded by the caller's eligibility mask.
        public static byte FindRoundLimitWinners(int completedRound, int maxRounds,
            int[] scores, byte eligibleSeats)
        {
            if (maxRounds <= 0 || completedRound < maxRounds || scores == null) return 0;
            int best = int.MinValue;
            byte winners = 0;
            for (int seat = 0; seat < Math.Min(scores.Length, 8); seat++)
            {
                byte bit = (byte)(1 << seat);
                if ((eligibleSeats & bit) == 0 || scores[seat] < best) continue;
                if (scores[seat] > best) { best = scores[seat]; winners = 0; }
                winners |= bit;
            }
            return winners;
        }

        public static byte FindThresholdCrossings(int[] scoresBefore, int[] scoresAfter,
            int killsToWin)
        {
            if (scoresBefore == null || scoresAfter == null || killsToWin <= 0)
                return 0;

            int count = Math.Min(Math.Min(scoresBefore.Length, scoresAfter.Length), 8);
            byte winnerMask = 0;
            for (int seat = 0; seat < count; seat++)
            {
                if (scoresBefore[seat] < killsToWin && scoresAfter[seat] >= killsToWin)
                    winnerMask |= (byte)(1 << seat);
            }
            return winnerMask;
        }
    }

    public struct MultiTerminalResultNetData : INetworkSerializable, IEquatable<MultiTerminalResultNetData>
    {
        public uint DecidingSequence;
        public MultiMatchOutcome Outcome;
        public byte WinnerMask;
        public bool Released;

        public bool IsValid => DecidingSequence != 0
            && Outcome != MultiMatchOutcome.InProgress
            && WinnerMask != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref DecidingSequence);
            serializer.SerializeValue(ref Outcome);
            serializer.SerializeValue(ref WinnerMask);
            serializer.SerializeValue(ref Released);
        }

        public bool Equals(MultiTerminalResultNetData other)
            => DecidingSequence == other.DecidingSequence
                && Outcome == other.Outcome
                && WinnerMask == other.WinnerMask
                && Released == other.Released;

        public override bool Equals(object obj)
            => obj is MultiTerminalResultNetData other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(DecidingSequence, (byte)Outcome, WinnerMask, Released);
    }
}
