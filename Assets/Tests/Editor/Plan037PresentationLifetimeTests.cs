using AbsoluteZero.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public class Plan037PresentationLifetimeTests
    {
        [Test]
        public void LateManagerCompletionCannotSettleReplacementOrUnlockItsInventory()
        {
            var owner = new GameObject("PresentationOwner");
            var vfx = owner.AddComponent<CombatVFXManager>();
            var camera = new GameObject("OwnedCamera");
            int unlocks = 0, settlements = 0;
            System.Action<uint> settled = _ => settlements++;
            CombatVFXManager.OnPresentationSettled += settled;
            try
            {
                camera.transform.position = Vector3.up;
                var a = vfx.BeginPresentation(10, null);
                vfx.CapturePresentationTransform(camera.transform);
                camera.transform.position = Vector3.right;
                var b = vfx.BeginPresentation(11, null);
                b.Resources.HoldInventory(() => { }, () => unlocks++);
                vfx.CapturePresentationTransform(camera.transform);
                camera.transform.position = Vector3.forward;
                var complete = typeof(CombatVFXManager).GetMethod("CompletePresentationSequence",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                complete.Invoke(vfx, new object[] { a, System.Enum.ToObject(complete.GetParameters()[1].ParameterType, 0) });
                Assert.That(b.CanContinue, Is.True);
                Assert.That(vfx.HasPendingPresentation(11), Is.True);
                Assert.That(camera.transform.position, Is.EqualTo(Vector3.forward));
                Assert.That(unlocks, Is.Zero); Assert.That(settlements, Is.Zero);
                vfx.ForceSettleMultiPresentation(11);
                vfx.ForceSettleMultiPresentation(11);
                Assert.That(camera.transform.position, Is.EqualTo(Vector3.up));
                Assert.That(unlocks, Is.EqualTo(1)); Assert.That(settlements, Is.EqualTo(1));
            }
            finally
            {
                CombatVFXManager.OnPresentationSettled -= settled;
                Object.DestroyImmediate(owner); Object.DestroyImmediate(camera);
            }
        }

        [Test]
        public void DisableHookAbandonsResourcesWithoutAcknowledgingAndNewExecutionCanStart()
        {
            var owner = new GameObject("DisablePresentationOwner");
            var vfx = owner.AddComponent<CombatVFXManager>();
            int settlements = 0;
            System.Action<uint> settled = _ => settlements++;
            CombatVFXManager.OnPresentationSettled += settled;
            try
            {
                var first = vfx.BeginPresentation(20, null);
                var temporary = first.Resources.Own(new GameObject("FeedSprite"));
                // Non-ExecuteAlways components do not receive Play lifecycle callbacks in EditMode.
                typeof(CombatVFXManager).GetMethod("OnDisable",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(vfx, null);
                Assert.That(first.Settled, Is.True);
                Assert.That(temporary == null, Is.True);
                Assert.That(settlements, Is.Zero);
                var second = vfx.BeginPresentation(21, null);
                Assert.That(second.CanContinue, Is.True);
            }
            finally { CombatVFXManager.OnPresentationSettled -= settled; Object.DestroyImmediate(owner); }
        }

        [Test]
        public void CanceledExecutionCannotResumeOrSettleTwice()
        {
            bool current = true, disposed = false;
            int steps = 0;
            var execution = new PresentationExecution(7, null, new PresentationResources(), () => current);
            System.Collections.IEnumerator Body()
            {
                try { steps++; yield return null; steps++; }
                finally { disposed = true; }
            }
            var routine = execution.Guard(Body());
            Assert.That(routine.MoveNext(), Is.True);
            current = false;
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(steps, Is.EqualTo(1)); Assert.That(disposed, Is.True);
            Assert.That(execution.TrySettle(), Is.True);
            Assert.That(execution.TrySettle(), Is.False);
        }

        [Test]
        public void ReplacedActorBindingStopsItemContinuationEvenWithinCurrentMatch()
        {
            bool actorCurrent = true;
            int impacts = 0;
            var execution = new PresentationExecution(8, null, new PresentationResources(), () => true);
            System.Collections.IEnumerator Body() { yield return null; impacts++; }
            var routine = execution.Guard(Body(), () => actorCurrent);
            Assert.That(routine.MoveNext(), Is.True);
            actorCurrent = false;
            Assert.That(routine.MoveNext(), Is.False);
            Assert.That(impacts, Is.Zero);
            execution.TrySettle();
        }

        [Test]
        public void QueuedBatchOwnsItsArrays()
        {
            var original = new CombatResolutionBatchNetData
            {
                Events = new CombatEventNetData[1], TempBefore = new[] { 37f }, TempAfter = new[] { 34f },
                MainItemIds = new short[] { 1 }, SubItemIds = new short[] { -1 }, ActionOrder = new[] { 0 }
            };
            var copy = CombatPresentationContext.CopyBatch(original);
            original.TempAfter[0] = 0; original.MainItemIds[0] = 9; original.ActionOrder[0] = 3;
            Assert.That(copy.TempAfter[0], Is.EqualTo(34));
            Assert.That(copy.MainItemIds[0], Is.EqualTo(1)); Assert.That(copy.ActionOrder[0], Is.Zero);
            Assert.That(copy.Events, Is.Not.SameAs(original.Events));
            Assert.That(copy.TempBefore, Is.Not.SameAs(original.TempBefore));
            Assert.That(copy.SubItemIds, Is.Not.SameAs(original.SubItemIds));
        }

        [Test]
        public void LateCameraLeaseCannotRestoreNewSequence_AndNewOwnerRestoresOriginal()
        {
            var camera = new GameObject("LeaseCamera");
            var ledger = new PresentationTransforms();
            try
            {
                camera.transform.position = Vector3.up;
                var a = ledger.Capture(camera.transform, new object());
                camera.transform.position = Vector3.right;
                var b = ledger.Capture(camera.transform, new object());
                camera.transform.position = Vector3.forward;
                a.Dispose();
                Assert.That(camera.transform.position, Is.EqualTo(Vector3.forward));
                b.Dispose(); b.Dispose();
                Assert.That(camera.transform.position, Is.EqualTo(Vector3.up));
            }
            finally { Object.DestroyImmediate(camera); }
        }

        [Test]
        public void CleanupOwnsExactObject_NotOtherObjectsWithSameName_AndUnlocksOnce()
        {
            var a = new PresentationResources(); var b = new PresentationResources();
            var first = a.Own(new GameObject("FeedSprite"));
            var second = b.Own(new GameObject("FeedSprite"));
            int locked = 0, unlocked = 0;
            a.HoldInventory(() => locked++, () => unlocked++);
            a.ReleaseInventory(); a.Dispose(); a.Dispose();
            Assert.That(first == null, Is.True);
            Assert.That(second != null, Is.True);
            Assert.That(locked, Is.EqualTo(1)); Assert.That(unlocked, Is.EqualTo(1));
            b.Dispose();
        }

        [Test]
        public void LateParticleReturnCannotReleaseReusedInstance()
        {
            var prefab = new GameObject("PoolSource");
            var pool = new PresentationParticlePool();
            try
            {
                var a = pool.Spawn(prefab, Vector3.zero);
                pool.ReleaseAll();
                var b = pool.Spawn(prefab, Vector3.one);
                Assert.That(b.Instance, Is.SameAs(a.Instance));
                pool.Release(a);
                Assert.That(b.Instance.activeSelf, Is.True);
                Assert.That(b.Instance.transform.position, Is.EqualTo(Vector3.one));
                pool.Release(b);
                Assert.That(b.Instance.activeSelf, Is.False);
                pool.Dispose(); pool.Dispose();
                Assert.That(b.Instance == null, Is.True);
                Assert.That(prefab != null, Is.True);
            }
            finally { pool.Dispose(); Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void InterruptedCatRestoresBorrowedViewBeforeInventoryRebuild()
        {
            var view = new GameObject("InventoryCat");
            var label = new GameObject("Label"); label.transform.SetParent(view.transform);
            var renderer = view.AddComponent<SpriteRenderer>();
            var collider = view.AddComponent<BoxCollider>();
            var resources = new PresentationResources();
            var original = view.transform.localPosition = Vector3.one;
            bool sawRestored = false;
            try
            {
                _ = new BorrowedItemPresentation(view, renderer, resources);
                resources.HoldInventory(() => { }, () => sawRestored =
                    view.transform.localPosition == original && collider.enabled && label.activeSelf);
                view.transform.localPosition = Vector3.right * 20;
                view.transform.localScale = Vector3.one * 2;
                renderer.sortingOrder = 90; renderer.color = Color.clear;
                collider.enabled = false; label.SetActive(false);
                resources.Dispose();
                Assert.That(sawRestored, Is.True);
                Assert.That(renderer.sortingOrder, Is.Zero);
                Assert.That(renderer.color, Is.EqualTo(Color.white));
                Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally { resources.Dispose(); Object.DestroyImmediate(view); }
        }
    }
}
