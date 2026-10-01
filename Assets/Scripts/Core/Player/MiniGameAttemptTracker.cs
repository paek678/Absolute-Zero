using System;
using AbsoluteZero.Core.Player.Identity;

namespace AbsoluteZero.Core.Player
{
    internal readonly struct MiniGameAttempt
    {
        internal readonly PlayerBinding Binding;
        internal readonly int Round;
        internal readonly MiniGameTicket Ticket;
        internal readonly byte Target;
        internal readonly double Deadline;

        internal MiniGameAttempt(PlayerBinding binding, int round, MiniGameTicket ticket,
            byte target, double deadline)
        { Binding = binding; Round = round; Ticket = ticket; Target = target; Deadline = deadline; }

        internal bool IsCurrent(PlayerBinding binding, int round, uint matchEpoch, uint roundEpoch, int turn)
            => ReferenceEquals(Binding, binding) && Round == round
                && Ticket.MatchEpoch == matchEpoch && Ticket.RoundEpoch == roundEpoch && Ticket.Turn == turn;

        // The existing grace and inclusive deadline are unchanged.
        internal bool IsWithinDeadline(double now) => now <= Deadline;
    }

    // One pending attempt per bound PlayerState. Taking or cancelling clears it
    // before any callback/queue/inventory mutation; a stale result never clears it.
    internal sealed class MiniGameAttemptTracker
    {
        const double GraceSeconds = 0.5;
        uint _nextId;
        MiniGameAttempt? _pending;
        internal MiniGameAttempt? Pending => _pending;
        internal bool HasPending => _pending.HasValue;

        internal MiniGameAttempt Begin(PlayerBinding binding, int round, MiniGameTicket description,
            byte target, double now, double prepEnd)
        {
            if (binding == null) throw new ArgumentNullException(nameof(binding));
            if (description.CopyId == 0) throw new ArgumentException("A physical item copy is required.");
            if (HasPending) throw new InvalidOperationException("A mini-game is already pending.");
            if (_nextId == uint.MaxValue) throw new InvalidOperationException("Mini-game attempt identity exhausted.");
            var ticket = new MiniGameTicket(description.Slot, description.Type, description.TimeLimit,
                description.Goal, description.MatchEpoch, description.RoundEpoch, description.Turn,
                ++_nextId, description.CopyId);
            var attempt = new MiniGameAttempt(binding, round, ticket, target,
                Math.Min(now + ticket.TimeLimit, prepEnd) + GraceSeconds);
            _pending = attempt;
            return attempt;
        }

        internal bool TryTake(byte slot, uint matchEpoch, uint roundEpoch, int turn,
            uint attemptId, uint copyId, out MiniGameAttempt attempt)
        {
            attempt = default;
            if (!_pending.HasValue || attemptId == 0 || copyId == 0) return false;
            var ticket = _pending.Value.Ticket;
            if (ticket.Slot != slot || ticket.AttemptId != attemptId || ticket.CopyId != copyId
                || ticket.MatchEpoch != matchEpoch || ticket.RoundEpoch != roundEpoch || ticket.Turn != turn)
                return false;
            return Cancel(out attempt);
        }

        internal bool Cancel(out MiniGameAttempt attempt)
        {
            attempt = _pending.GetValueOrDefault();
            bool existed = _pending.HasValue;
            _pending = null;
            return existed;
        }
    }
}
