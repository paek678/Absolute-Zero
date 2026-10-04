using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using NUnit.Framework;

namespace AbsoluteZero.Tests.Editor
{
    public class Plan043DepartureTests
    {
        [TestCase(GameMode.OneVsOne, 2, false)]
        [TestCase(GameMode.OneVsOne, 1, true)]
        [TestCase(GameMode.Multi, 4, false)]
        [TestCase(GameMode.Multi, 3, false)]
        [TestCase(GameMode.Multi, 2, true)]
        [TestCase(GameMode.Multi, 1, true)]
        [TestCase(GameMode.Solo, 1, false)]
        public void DepartureUsesConnectedParticipantsNotLivingPlayers(GameMode mode, int connected, bool ends)
            => Assert.That(MatchDepartureController.MustEnd(mode, connected), Is.EqualTo(ends));

        [Test]
        public void CircularSeatsHaveSameNeighboursAndOppositesForEveryViewer()
        {
            // Fixed map: P1 south, P2 west, P3 north, P4 east.
            int[,] expected = { { -1, 0, 1, 2 }, { 2, -1, 0, 1 }, { 1, 2, -1, 0 }, { 0, 1, 2, -1 } };
            for (int local = 0; local < 4; local++)
            for (int remote = 0; remote < 4; remote++)
            {
                int slot = AZPlayerVisual.GetRemoteVisualSlot(remote, local);
                Assert.That(slot, Is.EqualTo(expected[local, remote]), $"local={local}, remote={remote}");
                if (slot == 1) Assert.That(AZPlayerVisual.GetRemoteVisualSlot(local, remote), Is.EqualTo(1));
                if (slot == 0) Assert.That(AZPlayerVisual.GetRemoteVisualSlot(local, remote), Is.EqualTo(2));
            }
        }

        [Test]
        public void DuelAndSoloAlwaysBindTheirSingleOppositeVisual()
        {
            Assert.That(AZPlayerVisual.GetRemoteVisualSlot(1, 0, false), Is.Zero);
            Assert.That(AZPlayerVisual.GetRemoteVisualSlot(0, 1, false), Is.Zero);
            Assert.That(AZPlayerVisual.GetRemoteVisualSlot(2, 0, false), Is.EqualTo(-1));
        }

        [TestCase(LifeState.Alive)]
        [TestCase(LifeState.Ghost)]
        public void DepartedSeatCannotReviveParticipateOrHoldTheRoundOpen(LifeState previousLife)
        {
            using var roster = new MatchRoster(4);
            var participants = new MatchParticipantDescriptor[4];
            for (byte i = 0; i < 4; i++)
                participants[i] = new MatchParticipantDescriptor("p" + i, i, PlayerControllerKind.Human, (ulong)i, 1);
            Assert.That(roster.Hydrate(participants), Is.True);
            roster.CaptureOfflineState(2, new SeatRuntimeState { SeatIndex = 2, CurrentLifeState = previousLife });
            roster.MarkTimedOut(2);
            Assert.That(roster.ConnectedCount, Is.EqualTo(3));
            Assert.That(roster.IsActive(2), Is.False, "round reset iterates only active seats");
            Assert.That(roster.CountsAsAliveForRoundEnd(2), Is.False);
            Assert.That(roster.IsTurnEligible(2), Is.False);
            Assert.That(roster.IsGhostEligible(2), Is.False);
            Assert.That(roster.IsAutoReady(2), Is.False);
            Assert.That(roster.BindReconnect(2, 42), Is.False);
        }
    }
}
