using AbsoluteZero.Core.Match;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public sealed class MultiRoundLimitTests
    {
        [TestCase(1, 2, 0)]
        [TestCase(2, 2, 4)]
        [TestCase(3, 2, 4)]
        [TestCase(2, 3, 0)]
        [TestCase(3, 3, 4)]
        [TestCase(4, 3, 4)]
        [TestCase(3, 0, 0)]
        public void CapOnlyAppliesAtConfiguredCompletedRound(int round, int cap, int expected)
            => Assert.That(MultiVictoryRules.FindRoundLimitWinners(round, cap, new[] { 1, 0, 2, 0 }, 15), Is.EqualTo((byte)expected));
        [Test] public void AllTiedLeadersShareVictory()
            => Assert.That(MultiVictoryRules.FindRoundLimitWinners(2, 2, new[] { 2, 1, 2, 0 }, 15), Is.EqualTo((byte)5));
        [Test] public void DepartedLeaderCannotWinAndGhostCanRemainEligible()
            => Assert.That(MultiVictoryRules.FindRoundLimitWinners(2, 2, new[] { 5, 1, 2, 0 }, 14), Is.EqualTo((byte)4));
        [Test] public void ZeroKillsRemainJointVictory()
            => Assert.That(MultiVictoryRules.FindRoundLimitWinners(1, 1, new[] { 0, 0, 0, 0 }, 15), Is.EqualTo((byte)15));
        [Test] public void NoEligibleSeatsDoesNotPublishTerminal()
            => Assert.That(MultiVictoryRules.FindRoundLimitWinners(2, 2, new[] { 3, 2 }, 0), Is.Zero);
        [Test] public void MissingScoresDoNotPublishTerminal()
            => Assert.That(MultiVictoryRules.FindRoundLimitWinners(2, 2, null, 15), Is.Zero);
        [Test] public void ThirdKillCrossesThresholdAndRepeatingItDoesNotWinAgain()
        {
            Assert.That(MultiVictoryRules.FindThresholdCrossings(new[] { 1, 2, 0, 1 }, new[] { 1, 3, 0, 1 }, 3), Is.EqualTo((byte)2));
            Assert.That(MultiVictoryRules.FindThresholdCrossings(new[] { 1, 3, 0, 1 }, new[] { 1, 3, 0, 1 }, 3), Is.Zero);
        }
    }
}
