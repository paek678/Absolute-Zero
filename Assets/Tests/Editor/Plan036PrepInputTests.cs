using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public sealed class Plan036PrepInputTests
    {
        static PrepInputKey Key => new(5, 2, 3, 8);

        [Test]
        public void IncompleteWindowRejectsAndCommitCopiesBaseline()
        {
            var window = new PrepInputWindow();
            Assert.That(window.TryRead(Key, 10, out _), Is.False);
            float[] baseline = { 31, 27 };
            var committed = window.Commit(Key, 10, 20, baseline);
            baseline[0] = 0;
            Assert.That(committed.TemperaturesAtStart[0], Is.EqualTo(31));
            Assert.Throws<NotSupportedException>(() => ((IList<float>)committed.TemperaturesAtStart)[0] = 0);
            Assert.That(window.TryRead(Key, 10, out var current), Is.True);
            Assert.That(current.Key, Is.EqualTo(Key));
            Assert.That(current.RemainingSeconds, Is.EqualTo(20));
        }

        [TestCase(9.9, false, 0)]
        [TestCase(10, true, 20)]
        [TestCase(25, true, 5)]
        [TestCase(29.999, true, 0.001)]
        [TestCase(30, false, 0)]
        [TestCase(31, false, 0)]
        [TestCase(double.NaN, false, 0)]
        [TestCase(double.PositiveInfinity, false, 0)]
        public void QueryUsesStrictDeadlineAndClampedLiveTime(double now, bool accepted, double remaining)
        {
            var window = new PrepInputWindow();
            window.Commit(Key, 10, 20, new[] {37f, 30f});
            Assert.That(window.TryRead(Key, now, out var snapshot), Is.EqualTo(accepted));
            if (accepted) Assert.That(snapshot.RemainingSeconds, Is.EqualTo(remaining).Within(0.00001));
        }

        [Test]
        public void CloseImmediatelyRejectsEvenBeforeDeadlineAndFreshTurnRejectsHeldKeys()
        {
            var window = new PrepInputWindow();
            window.Commit(Key, 10, 20, new[] {37f});
            window.Close();
            Assert.That(window.TryRead(Key, 11, out _), Is.False);
            var next = new PrepInputKey(5, 2, 4, 9);
            window.Commit(next, 12, 20, new[] {29f});
            Assert.That(window.TryRead(Key, 12, out _), Is.False);
            Assert.That(window.TryRead(next, 12, out var snapshot), Is.True);
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(20));
            Assert.That(snapshot.TemperaturesAtStart[0], Is.EqualTo(29));
        }

        [Test]
        public void EveryIdentityDomainParticipatesInValidation()
        {
            var window = new PrepInputWindow();
            window.Commit(Key, 10, 20, new[] {37f});
            foreach (var stale in new[] {new PrepInputKey(6,2,3,8),new PrepInputKey(5,3,3,8),
                new PrepInputKey(5,2,4,8),new PrepInputKey(5,2,3,9)})
                Assert.That(window.TryRead(stale, 11, out _), Is.False);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void FailedCommitCannotLeavePriorWindowAccepting(double duration)
        {
            var window = new PrepInputWindow();
            window.Commit(Key, 10, 20, new[] {37f});
            Assert.Throws<ArgumentException>(() => window.Commit(Key, 10, duration, new[] {37f}));
            Assert.That(window.TryRead(Key, 11, out _), Is.False);
        }
    }
}
