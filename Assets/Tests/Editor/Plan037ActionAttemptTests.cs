using System;
using System.Reflection;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037ActionAttemptTests
    {
        GameObject _object;
        PlayerBinding _binding;
        MiniGameAttemptTracker _tracker;
        MiniGameTicket Description(uint copy = 71) => new(2, MiniGameType.HitTargets, 4, 3, 8, 9, 10, 0, copy);

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Mini-game lifetime boundary");
            _object.SetActive(false);
            var network = _object.AddComponent<NetworkObject>();
            var state = _object.AddComponent<PlayerState>();
            _binding = new PlayerBinding(state, null, network);
            _tracker = new MiniGameAttemptTracker();
        }

        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(_object);
        MiniGameAttempt Begin(double now = 20, double prepEnd = 100)
            => _tracker.Begin(_binding, 3, Description(), 1, now, prepEnd);
        bool Take(MiniGameTicket t, out MiniGameAttempt a)
            => _tracker.TryTake(t.Slot, t.MatchEpoch, t.RoundEpoch, t.Turn, t.AttemptId, t.CopyId, out a);

        [TestCase("slot")]
        [TestCase("match")]
        [TestCase("round")]
        [TestCase("turn")]
        [TestCase("attempt")]
        [TestCase("copy")]
        public void StaleIdentityCannotClaimOrClearTheCurrentAttempt(string changed)
        {
            var t = Begin().Ticket;
            var forged = new MiniGameTicket((byte)(t.Slot + (changed == "slot" ? 1 : 0)), t.Type, t.TimeLimit, t.Goal,
                t.MatchEpoch + (changed == "match" ? 1u : 0u), t.RoundEpoch + (changed == "round" ? 1u : 0u),
                t.Turn + (changed == "turn" ? 1 : 0), t.AttemptId + (changed == "attempt" ? 1u : 0u),
                t.CopyId + (changed == "copy" ? 1u : 0u));
            Assert.That(Take(forged, out _), Is.False);
            Assert.That(Take(t, out var accepted), Is.True);
            Assert.That(accepted.Target, Is.EqualTo(1));
            Assert.That(_tracker.HasPending, Is.False);
            Assert.That(Take(t, out _), Is.False, "Duplicate completion cannot claim twice.");
        }

        [Test]
        public void CancelBeforeRetryKeepsIdentityMonotonicAndOldResponseHarmless()
        {
            var first = Begin().Ticket;
            Assert.That(_tracker.Cancel(out var cancelled), Is.True);
            Assert.That(cancelled.Ticket.CopyId, Is.EqualTo(first.CopyId));
            Assert.That(_tracker.Cancel(out _), Is.False);
            var next = Begin().Ticket;
            Assert.That(next.AttemptId, Is.GreaterThan(first.AttemptId));
            Assert.That(Take(first, out _), Is.False);
            Assert.That(Take(next, out _), Is.True);
        }

        [TestCase(100d, 24.5d)]
        [TestCase(22d, 22.5d)]
        public void DeadlineUsesEarlierItemOrPrepBudgetPlusExistingGrace(double prepEnd, double deadline)
        {
            var attempt = Begin(prepEnd: prepEnd);
            Assert.That(attempt.Deadline, Is.EqualTo(deadline));
            Assert.That(attempt.IsWithinDeadline(deadline), Is.True);
            Assert.That(attempt.IsWithinDeadline(deadline + 0.000001), Is.False);
            Assert.That(attempt.IsWithinDeadline(double.NaN), Is.False);
        }

        [TestCase(2)]
        [TestCase(9)]
        [TestCase(20)]
        public void ZeroEpochRoundRolloverUsesMonotonicAttemptIdentity(int previousTurn)
        {
            var previous = _tracker.Begin(_binding, 1,
                new MiniGameTicket(2, MiniGameType.HitTargets, 4, 3, 0, 0, previousTurn, 0, 71),
                1, 20, 100).Ticket;
            _tracker.Cancel(out _);
            var next = _tracker.Begin(_binding, 2,
                new MiniGameTicket(2, MiniGameType.HitTargets, 4, 3, 0, 0, 1, 0, 72),
                1, 30, 100).Ticket;

            Assert.That(next.AttemptId, Is.GreaterThan(previous.AttemptId));
            Assert.That(next.IsNewerThan(previous), Is.True,
                "Duel/Solo leave network epochs at zero while a new round resets Turn to one.");
            Assert.That(previous.IsNewerThan(next), Is.False,
                "A previous-round start cannot replace the new active mini-game.");
            Assert.That(Take(previous, out _), Is.False);
            Assert.That(Take(next, out _), Is.True);
        }

        [TestCase(0u, 0u)]
        [TestCase(8u, 9u)]
        public void HigherTurnCannotMakeDuplicateOrOlderAttemptNewer(uint match, uint round)
        {
            var current = new MiniGameTicket(2, MiniGameType.HitTargets, 4, 3, match, round, 1, 12, 71);
            var duplicate = new MiniGameTicket(2, MiniGameType.HitTargets, 4, 3, match, round, 9, 12, 71);
            var older = new MiniGameTicket(2, MiniGameType.HitTargets, 4, 3, match, round, 9, 11, 70);
            Assert.That(duplicate.IsNewerThan(current), Is.False);
            Assert.That(older.IsNewerThan(current), Is.False);
        }

        [Test]
        public void EpochChangesStillTakePriorityOverAttemptNumbers()
        {
            var oldRound = new MiniGameTicket(2, MiniGameType.HitTargets, 4, 3, 8, 8, 20, 100, 71);
            var current = new MiniGameTicket(2, MiniGameType.HitTargets, 4, 3, 8, 9, 1, 1, 72);
            var nextMatch = new MiniGameTicket(2, MiniGameType.HitTargets, 4, 3, 9, 1, 1, 1, 1);
            Assert.That(current.IsNewerThan(oldRound), Is.True);
            Assert.That(oldRound.IsNewerThan(current), Is.False);
            Assert.That(nextMatch.IsNewerThan(current), Is.True);
            Assert.That(current.IsNewerThan(nextMatch), Is.False);
        }

        [Test]
        public void ScopeIncludesBindingAndRoundEvenWhenNetworkEpochsAreUnchanged()
        {
            var attempt = Begin();
            Assert.That(attempt.IsCurrent(_binding, 3, 8, 9, 10), Is.True);
            Assert.That(attempt.IsCurrent(null, 3, 8, 9, 10), Is.False);
            Assert.That(attempt.IsCurrent(_binding, 4, 8, 9, 10), Is.False);
            Assert.That(attempt.IsCurrent(_binding, 3, 7, 9, 10), Is.False);
            Assert.That(attempt.IsCurrent(_binding, 3, 8, 8, 10), Is.False);
            Assert.That(attempt.IsCurrent(_binding, 3, 8, 9, 11), Is.False);
        }

        [Test]
        public void ConcurrentBeginAndInvalidCopyCannotReplacePendingAttempt()
        {
            Assert.Throws<ArgumentException>(() => _tracker.Begin(_binding, 3, Description(0), 1, 20, 100));
            var current = Begin();
            Assert.Throws<InvalidOperationException>(() => Begin());
            Assert.That(Take(current.Ticket, out _), Is.True);
        }

        [Test]
        public void ExhaustedIdentityCannotWrapBackToPreviouslyAcceptedAttempt()
        {
            typeof(MiniGameAttemptTracker).GetField("_nextId", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_tracker, uint.MaxValue - 1);
            var last = Begin();
            Assert.That(last.Ticket.AttemptId, Is.EqualTo(uint.MaxValue));
            Assert.That(Take(last.Ticket, out _), Is.True);
            Assert.Throws<InvalidOperationException>(() => Begin());
            Assert.Throws<InvalidOperationException>(() => Begin());
            Assert.That(_tracker.HasPending, Is.False);
        }
    }
}
