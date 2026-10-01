using System;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Solo.Configuration;

namespace AbsoluteZero.Core.Session
{
    public sealed class MatchLaunchContext
    {
        public long Generation { get; }
        public GameMode Mode { get; }
        public int RequiredSeats { get; }
        public ResolvedSoloSettings Solo { get; }

        public MatchLaunchContext(long generation, GameMode mode, int requiredSeats, ResolvedSoloSettings solo = null)
        {
            if (generation <= 0 || requiredSeats < 2 || requiredSeats > 4) throw new ArgumentOutOfRangeException();
            if (mode == GameMode.Solo && (solo == null || requiredSeats != 2))
                throw new ArgumentException("Solo requires validated settings and two logical seats.");
            Generation = generation; Mode = mode; RequiredSeats = requiredSeats; Solo = solo;
        }
    }
}
