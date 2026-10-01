namespace AbsoluteZero.UI.Game.Bridge
{
    public readonly struct MatchResultNotice
    {
        public readonly ulong Revision;
        public readonly bool IsMatchEnd;
        public readonly MatchSnapshot Snapshot;

        internal MatchResultNotice(ulong revision, bool isMatchEnd, MatchSnapshot snapshot)
        {
            Revision = revision;
            IsMatchEnd = isMatchEnd;
            Snapshot = Copy(snapshot);
        }

        static MatchSnapshot Copy(MatchSnapshot value)
        {
            value.KillScores = value.KillScores == null ? null : (int[])value.KillScores.Clone();
            value.LifeStates = value.LifeStates == null ? null : (Core.Player.LifeState[])value.LifeStates.Clone();
            return value;
        }
    }

    // Local delivered-result memory, never a gameplay state or network writer.
    // Prep start distinguishes void/draw rounds which can reuse round/turn numbers.
    internal sealed class MatchResultBuffer
    {
        MatchResultNotice _latest;
        double _prepStart;
        bool _hasResult;
        ulong _revision;

        internal bool TryPublish(MatchSnapshot snapshot, bool matchEnd, double prepStart,
            out MatchResultNotice notice)
        {
            notice = default;
            if (_hasResult && _prepStart == prepStart
                && _latest.Snapshot.RoundNumber == snapshot.RoundNumber
                && _latest.Snapshot.TurnNumber == snapshot.TurnNumber
                && _latest.Snapshot.MultiDecidingSequence == snapshot.MultiDecidingSequence
                && (_latest.IsMatchEnd || !matchEnd)) return false;
            _prepStart = prepStart;
            _hasResult = true;
            _latest = new MatchResultNotice(++_revision, matchEnd, snapshot);
            return TryRead(out notice);
        }

        internal bool TryRead(out MatchResultNotice notice)
        {
            notice = _hasResult
                ? new MatchResultNotice(_latest.Revision, _latest.IsMatchEnd, _latest.Snapshot) : default;
            return _hasResult;
        }

        internal void Clear() => _hasResult = false;
    }
}
