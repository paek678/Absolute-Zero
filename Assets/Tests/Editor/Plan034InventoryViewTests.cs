using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace AbsoluteZero.Tests
{
    public sealed class Plan034InventoryViewTests
    {
        static MultiInventorySnapshotNetData View(uint revision, uint grant, byte uses)
        {
            var view = new MultiInventorySnapshotNetData
            {
                MatchEpoch = 7, RoundEpoch = 2, Revision = revision,
                CommittedGrantTransaction = grant, SeatPresenceMask = 0x0f
            };
            view.SetCount(0, 1);
            view.SetCount(1, 1);
            for (int i = 0; i < MultiInventorySnapshotNetData.TotalSlots; i++)
                view.Slots.Add(ItemSlotNetData.Empty);
            view.Slots[0] = new ItemSlotNetData
                { ItemId = 12, RemainingUses = uses, CopyId = 101 };
            view.Slots[12] = new ItemSlotNetData
                { ItemId = 13, RemainingUses = 2, CopyId = 205 };
            return view;
        }

        [Test]
        public void CompleteView_RoundTripsThroughInstalledNgoSerializer()
        {
            var original = View(3, 8, 1);
            using var writer = new FastBufferWriter(640, Allocator.Temp);
            writer.WriteNetworkSerializable(original);
            Assert.That(writer.Length, Is.LessThan(512));

            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out MultiInventorySnapshotNetData restored);
            Assert.That(restored.IsValid(4), Is.True);
            Assert.That(restored.Equals(original), Is.True);
            Assert.That(restored.GetSlot(1, 0).CopyId, Is.EqualTo(205));
        }

        [Test]
        public void DefaultNetworkVariable_SerializesSafelyBeforeFirstPublication()
        {
            using var writer = new FastBufferWriter(640, Allocator.Temp);
            writer.WriteNetworkSerializable(default(MultiInventorySnapshotNetData));
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out MultiInventorySnapshotNetData restored);
            Assert.That(restored.IsValid(4), Is.False);
            Assert.That(restored.Slots.Length, Is.EqualTo(MultiInventorySnapshotNetData.TotalSlots));
        }

        [Test]
        public void SkippedIntermediateGrant_AdoptsLatestInventoryAndWatermark()
        {
            var model = new MultiInventoryReadModel();
            int notifications = 0;
            model.Changed += () => notifications++;
            Assert.That(model.Adopt(View(1, 0, 2), 4), Is.True);
            Assert.That(model.HasCommittedGrant(2, 8), Is.False);
            // The grant was consumed before a send. Revision 2 was never observed.
            Assert.That(model.Adopt(View(3, 8, 1), 4), Is.True);
            Assert.That(model.HasCommittedGrant(2, 8), Is.True);
            Assert.That(model.HasCommittedGrant(3, 8), Is.False);
            Assert.That(model.CommittedGrantTransaction, Is.EqualTo(8));
            Assert.That(model.GetSlot(0, 0).RemainingUses, Is.EqualTo(1));
            Assert.That(model.Adopt(View(2, 8, 2), 4), Is.False);
            Assert.That(notifications, Is.EqualTo(2));
        }

        [Test]
        public void GrantDescriptorBeforeView_BlocksUntilWholeCommittedEnvelopeArrives()
        {
            var model = new MultiInventoryReadModel();
            Assert.That(model.Adopt(View(1, 0, 2), 4), Is.True);
            var grant = new DeathmatchGrantNetData
            {
                RoundEpoch = 2, TransactionId = 8, Stage = DeathmatchGrantStage.Pending
            };
            Assert.That(model.IsBlockedForGrant(7, 2, grant), Is.True);
            grant.Stage = DeathmatchGrantStage.Completed;
            Assert.That(model.IsBlockedForGrant(7, 2, grant), Is.True);
            Assert.That(model.GetSlot(0, 0).RemainingUses, Is.EqualTo(2));

            Assert.That(model.Adopt(View(3, 8, 1), 4), Is.True);
            Assert.That(model.IsBlockedForGrant(7, 2, grant), Is.False);
            Assert.That(model.GetSlot(0, 0).RemainingUses, Is.EqualTo(1));
        }

        [Test]
        public void WholeViewBeforeDescriptor_StaysCoherentAndPendingStillBlocks()
        {
            var model = new MultiInventoryReadModel();
            Assert.That(model.Adopt(View(1, 0, 2), 4), Is.True);
            var grant = new DeathmatchGrantNetData
            {
                RoundEpoch = 2, TransactionId = 8, Stage = DeathmatchGrantStage.Pending
            };
            // The old complete view remains visible while the new whole view is pending.
            Assert.That(model.GetSlot(0, 0).RemainingUses, Is.EqualTo(2));
            Assert.That(model.IsBlockedForGrant(7, 2, grant), Is.True);

            Assert.That(model.Adopt(View(3, 8, 1), 4), Is.True);
            Assert.That(model.IsBlockedForGrant(7, 2, grant), Is.True);
            grant.Stage = DeathmatchGrantStage.Completed;
            Assert.That(model.IsBlockedForGrant(7, 2, grant), Is.False);
            Assert.That(model.GetSlot(0, 0).RemainingUses, Is.EqualTo(1));
        }

        [Test]
        public void WrongSessionAndIncompleteEnvelope_AreRejected()
        {
            var model = new MultiInventoryReadModel();
            Assert.That(model.Adopt(View(3, 8, 1), 4), Is.True);
            var oldRound = View(9, 9, 2);
            oldRound.RoundEpoch = 1;
            Assert.That(model.Adopt(oldRound, 4), Is.False);
            var malformed = View(4, 8, 1);
            malformed.SetCount(1, 13);
            Assert.That(model.Adopt(malformed, 4), Is.False);
            Assert.That(model.GetSlot(1, 0).CopyId, Is.EqualTo(205));
        }

        [Test]
        public void QueuedCopyIdentity_SurvivesCompactionAndClearsWhenRemoved()
        {
            var queue = new ActionQueue();
            queue.SetSelected(2, null, copyId: 205);
            queue.OnSlotRemoved(0);
            Assert.That(queue.selectedAction.Value.SlotIndex, Is.EqualTo(1));
            Assert.That(queue.selectedAction.Value.CopyId, Is.EqualTo(205));
            queue.OnSlotRemoved(1);
            Assert.That(queue.selectedAction.HasValue, Is.False);
        }
    }
}
