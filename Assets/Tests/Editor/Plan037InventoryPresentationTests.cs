using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player.Identity;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037InventoryPresentationTests
    {
        static ItemSlotNetData Slot(uint copy, byte uses = 2) => new() { ItemId = 3, CopyId = copy, RemainingUses = uses };

        [Test]
        public void DisplaySnapshot_PreservesCopyIdentityAfterSourceMutationAndCompaction()
        {
            var slots = new[] { Slot(21), Slot(22) };
            var first = new InventoryViewSnapshot(null, 4, 2, 7, slots);
            slots[0] = Slot(22, 1);
            var second = new InventoryViewSnapshot(null, 4, 2, 8, new[] { slots[0] });
            Assert.That(first[0].CopyId, Is.EqualTo(21));
            Assert.That(first.FindCopy(22), Is.EqualTo(1));
            Assert.That(second.FindCopy(22), Is.Zero);
            Assert.That(second.FindCopy(21), Is.EqualTo(-1));
            Assert.That(second.SameScope(first), Is.True);
            Assert.That(second.Revision, Is.EqualTo(8));
        }

        [Test]
        public void DisplaySnapshot_DoesNotReuseCopyAcrossRoundOrBinding()
        {
            var binding = new PlayerBinding(null, null, null);
            var original = new InventoryViewSnapshot(binding, 4, 2, 7, new[] { Slot(21) });
            Assert.That(new InventoryViewSnapshot(binding, 4, 3, 8, new[] { Slot(21) }).SameScope(original), Is.False);
            Assert.That(new InventoryViewSnapshot(new PlayerBinding(null, null, null), 4, 2, 8, new[] { Slot(21) }).SameScope(original), Is.False);
        }

        [Test]
        public void Reader_RejectsUnspawnedBindingInsteadOfShowingOldSlots()
        {
            Assert.That(new BoundInventoryReader(new PlayerBinding(null, null, null)).TryRead(out var result), Is.False);
            Assert.That(result, Is.Null);
        }

        sealed class View { public int Slot; public byte Uses; public bool Destroyed; }

        [Test]
        public void Reconciliation_ReusesDuplicateCopiesAcrossCompactionAndUpdatesUses()
        {
            var reconciler = new InventoryViewReconciler<View>();
            int created = 0, destroyed = 0;
            View[] Apply(uint round, params ItemSlotNetData[] slots) => reconciler.Reconcile(
                new InventoryViewSnapshot(null, 2, round, 1, slots),
                (i, s) => { created++; return new View(); },
                (v, i, s) => { v.Slot = i; v.Uses = s.RemainingUses; },
                v => { v.Destroyed = true; destroyed++; }, v => v != null && !v.Destroyed);
            var first = Apply(1, Slot(21), Slot(22));
            var second = Apply(1, Slot(22, 1));
            Assert.That(second[0], Is.SameAs(first[1]));
            Assert.That(second[0].Slot, Is.Zero);
            Assert.That(second[0].Uses, Is.EqualTo(1));
            Assert.That(first[0].Destroyed, Is.True);
            Assert.That(created, Is.EqualTo(2));
            Assert.That(destroyed, Is.EqualTo(1));
            var nextRound = Apply(2, Slot(22));
            Assert.That(nextRound[0], Is.Not.SameAs(second[0]));
            Assert.That(second[0].Destroyed, Is.True);
            Assert.That(created, Is.EqualTo(3));
        }

        [Test]
        public void Reconciliation_RejectsDuplicateIdentityBeforeDestroyingCurrentViews()
        {
            var reconciler = new InventoryViewReconciler<View>();
            int disposed = 0;
            View[] Apply(params ItemSlotNetData[] slots) => reconciler.Reconcile(
                new InventoryViewSnapshot(null, 2, 1, 1, slots), (i, s) => new View(),
                (v, i, s) => { }, v => disposed++, v => v != null);
            Assert.That(Apply(Slot(1)), Has.Length.EqualTo(1));
            Assert.That(Apply(Slot(1), Slot(1)), Is.Null);
            Assert.That(disposed, Is.Zero);
            reconciler.Clear(v => disposed++);
            reconciler.Clear(v => disposed++);
            Assert.That(disposed, Is.EqualTo(1));
        }

        [Test]
        public void Selection_HostAcceptanceCannotBeOverwrittenByDispatchCompletion()
        {
            var flow = new ItemSelectionInteraction(); var binding = new object();
            flow.Observe(binding, 1, 1, true, false, false);
            ulong token = flow.Submit(21);
            flow.Observe(binding, 1, 1, true, true, false);
            flow.DispatchFailed(token);
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.Confirmed));
            Assert.That(flow.CopyId, Is.EqualTo(21));
            flow.Reject(21); // A rejection cannot cancel an accepted server selection.
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.Confirmed));
        }

        [Test]
        public void Selection_OneInflightCopyAndScopeGenerationProtectRetry()
        {
            var flow = new ItemSelectionInteraction(); var binding = new object();
            flow.Observe(binding, 1, 1, true, false, false);
            ulong old = flow.Submit(21);
            Assert.That(flow.Submit(22), Is.Zero);
            flow.Reject(22);
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.AwaitingServer));
            flow.Reject(21);
            ulong retry = flow.Submit(22);
            flow.DispatchFailed(old);
            Assert.That(flow.Generation, Is.EqualTo(retry));
            Assert.That(flow.CopyId, Is.EqualTo(22));
            flow.Observe(binding, 1, 2, true, false, false);
            flow.DispatchFailed(retry);
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.Idle));
            Assert.That(flow.CopyId, Is.Zero);
        }

        [Test]
        public void Selection_CancelOnlyAim_ReadyAndPhaseAlwaysOverrideInput()
        {
            var flow = new ItemSelectionInteraction(); var binding = new object();
            flow.Observe(binding, 1, 1, true, false, false);
            Assert.That(flow.Aim(21), Is.True);
            flow.CancelAim();
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.Idle));
            flow.Submit(21); flow.CancelAim();
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.AwaitingServer));
            flow.Observe(binding, 1, 1, true, true, true);
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.ReadyLocked));
            Assert.That(flow.Aim(22), Is.False);
            flow.Observe(binding, 1, 1, false, true, true);
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.Idle));
            Assert.That(flow.Submit(22), Is.Zero);
        }

        [Test]
        public void Selection_ServerCancellationAndNewBindingDiscardOldCopy()
        {
            var flow = new ItemSelectionInteraction(); var binding = new object();
            flow.Observe(binding, 1, 1, true, false, false);
            flow.Submit(21); flow.Observe(binding, 1, 1, true, true, false);
            flow.Observe(binding, 1, 1, true, false, false);
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.Idle));
            flow.Aim(22);
            flow.Observe(new object(), 1, 1, true, false, false);
            Assert.That(flow.CopyId, Is.Zero);
            Assert.That(flow.Stage, Is.EqualTo(ItemSelectionStage.Idle));
        }
    }
}
