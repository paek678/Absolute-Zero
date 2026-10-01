using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AbsoluteZero.Core.Turn
{
    public readonly struct PrepInputKey : IEquatable<PrepInputKey>
    {
        public readonly long Generation;
        public readonly int Round;
        public readonly int Turn;
        public readonly ulong Sequence;

        public PrepInputKey(long generation, int round, int turn, ulong sequence)
        { Generation = generation; Round = round; Turn = turn; Sequence = sequence; }
        public bool Equals(PrepInputKey other) => Generation == other.Generation
            && Round == other.Round && Turn == other.Turn && Sequence == other.Sequence;
        public override bool Equals(object value) => value is PrepInputKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Generation, Round, Turn, Sequence);
        public static bool operator ==(PrepInputKey a, PrepInputKey b) => a.Equals(b);
        public static bool operator !=(PrepInputKey a, PrepInputKey b) => !a.Equals(b);
    }

    // A committed read model. A query refreshes remaining time without changing the
    // turn identity or exposing the mutable temperature baseline owned by TurnManager.
    public sealed class PrepInputSnapshot
    {
        public PrepInputKey Key { get; }
        public double StartTime { get; }
        public double Deadline { get; }
        public double RemainingSeconds { get; }
        public IReadOnlyList<float> TemperaturesAtStart { get; }

        internal PrepInputSnapshot(PrepInputKey key, double start, double deadline,
            double remaining, IReadOnlyList<float> temperatures)
        { Key = key; StartTime = start; Deadline = deadline; RemainingSeconds = remaining; TemperaturesAtStart = temperatures; }

        internal PrepInputSnapshot At(double now) => new(Key, StartTime, Deadline,
            Math.Max(0, Math.Min(Deadline - StartTime, Deadline - now)), TemperaturesAtStart);
    }

    // No phase progression or gameplay clock owner here. TurnManager commits/closes
    // this window; callers must also verify the current live session and phase.
    public sealed class PrepInputWindow
    {
        PrepInputSnapshot _committed;
        bool _open;

        public PrepInputSnapshot Commit(PrepInputKey key, double start, double duration, float[] temperatures)
        {
            Close();
            if (key.Generation < 0 || key.Round <= 0 || key.Turn <= 0 || key.Sequence == 0
                || double.IsNaN(start) || double.IsInfinity(start)
                || double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0
                || double.IsInfinity(start + duration) || temperatures == null || temperatures.Length == 0)
                throw new ArgumentException("Prep requires a complete identity, finite time and temperature baseline.");
            _committed = new PrepInputSnapshot(key, start, start + duration, duration,
                new ReadOnlyCollection<float>((float[])temperatures.Clone()));
            _open = true;
            return _committed;
        }

        public bool TryRead(PrepInputKey currentKey, double now, out PrepInputSnapshot snapshot)
        {
            snapshot = null;
            if (!_open || _committed == null || currentKey != _committed.Key
                || double.IsNaN(now) || double.IsInfinity(now)
                || now < _committed.StartTime || now >= _committed.Deadline) return false;
            snapshot = _committed.At(now);
            return true;
        }

        public void Close() { _open = false; _committed = null; }
    }
}
