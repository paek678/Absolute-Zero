using System;
using System.Reflection;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AbsoluteZero.Tests
{
    public sealed class Plan044ContactPresentationTests
    {
        [Test]
        public void IdleEndTriggerCannotImmediatelyCancelRemoteHugInAuthoredController()
        {
            var root = new GameObject("Contact animator regression");
            try
            {
                var animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/MainFolder/Animations/playerA.controller");
                Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
                animator.Rebind();
                animator.Update(0f);
                var visual = root.AddComponent<AZPlayerVisual>();
                typeof(AZPlayerVisual).GetField("_animator", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(visual, animator);
                visual.ReturnToIdle();
                animator.Update(0f);
                ContactItemChoreography.StartRemoteHug(visual);
                float sampledHugTime = 0f;
                for (int i = 0; i < 60; i++)
                {
                    animator.Update(0.02f);
                    foreach (var info in animator.GetCurrentAnimatorClipInfo(0))
                        if (info.clip.name == "playerA_hug" && info.weight > 0.5f)
                            sampledHugTime = Mathf.Max(sampledHugTime, animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
                }
                Assert.That(sampledHugTime, Is.GreaterThanOrEqualTo(0.9f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContactForegroundRestoresGroupingWithoutChangingPartOrderAndLateDisposeIsHarmless(bool authoredGroup)
        {
            var root = new GameObject("Contact foreground");
            var arm = new GameObject("Arm");
            arm.transform.SetParent(root.transform);
            try
            {
                var renderer = arm.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 5;
                if (authoredGroup) root.AddComponent<SortingGroup>().sortingOrder = 7;
                var resources = new PresentationResources();
                var first = new PresentationSortingLease(root.transform, 80);
                resources.OnRelease(first.Dispose);
                var group = root.GetComponent<SortingGroup>();
                Assert.That(group.enabled, Is.True);
                Assert.That(group.sortingOrder, Is.EqualTo(80));
                Assert.That(renderer.sortingOrder, Is.EqualTo(5));

                resources.Dispose();
                Assert.That(group.enabled, Is.EqualTo(authoredGroup));
                Assert.That(group.sortingOrder, Is.EqualTo(authoredGroup ? 7 : 0));
                using (var second = new PresentationSortingLease(root.transform, 90))
                {
                    first.Dispose();
                    resources.Dispose();
                    Assert.That(group.enabled, Is.True);
                    Assert.That(group.sortingOrder, Is.EqualTo(90));
                    Assert.That(root.GetComponents<SortingGroup>().Length, Is.EqualTo(1));
                }
                Assert.That(group.enabled, Is.EqualTo(authoredGroup));
                Assert.That(group.sortingOrder, Is.EqualTo(authoredGroup ? 7 : 0));
                Assert.That(renderer.sortingOrder, Is.EqualTo(5));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void NormalCompletionRestoresCameraAndAuthoredFpsBeforeUnlockAndSettlement()
        {
            using var fixture = new PoseFixture();
            int unlocks = 0, settlements = 0;
            bool restoredAtUnlock = false, restoredAtSettlement = false;
            Action<uint> settled = sequence =>
            {
                if (sequence != 4401) return;
                settlements++;
                restoredAtSettlement = fixture.HasOriginalPose;
            };
            CombatVFXManager.OnPresentationSettled += settled;
            try
            {
                var execution = fixture.Vfx.BeginPresentation(4401, null);
                fixture.Capture();
                execution.Resources.HoldInventory(() => { }, () =>
                {
                    unlocks++;
                    restoredAtUnlock = fixture.HasOriginalPose;
                });
                fixture.MoveToContact(1);

                CompleteNormally(fixture.Vfx, execution);
                CompleteNormally(fixture.Vfx, execution);

                Assert.That(execution.Settled, Is.True);
                fixture.AssertOriginalPose();
                Assert.That(restoredAtUnlock, Is.True, "Views must be restored before inventory rebuild.");
                Assert.That(restoredAtSettlement, Is.True, "The next action must observe the restored view.");
                Assert.That(unlocks, Is.EqualTo(1));
                Assert.That(settlements, Is.EqualTo(1));
            }
            finally { CombatVFXManager.OnPresentationSettled -= settled; }
        }

        [Test]
        public void ReplacedPresentationAndItsLateLeasesCannotMoveTheNewCameraOrFps()
        {
            using var fixture = new PoseFixture();
            var first = fixture.Vfx.BeginPresentation(4402, null);
            var firstCamera = fixture.Vfx.CapturePresentationTransform(fixture.Camera.transform);
            var firstFps = fixture.Vfx.CapturePresentationTransform(fixture.Fps.transform);
            fixture.MoveToContact(1);

            var second = fixture.Vfx.BeginPresentation(4403, null);
            Assert.That(first.Settled, Is.True);
            fixture.AssertOriginalPose();
            fixture.Capture();
            fixture.MoveToContact(2);
            var nextCamera = new WorldPose(fixture.Camera.transform);
            var nextFps = new WorldPose(fixture.Fps.transform);

            CompleteNormally(fixture.Vfx, first);
            firstCamera.Dispose();
            firstFps.Dispose();
            first.Resources.Dispose();

            nextCamera.AssertMatches(fixture.Camera.transform, "replacement camera");
            nextFps.AssertMatches(fixture.Fps.transform, "replacement FPS");
            Assert.That(second.CanContinue, Is.True);
            Assert.That(fixture.Vfx.HasPendingPresentation(4403), Is.True);

            CompleteNormally(fixture.Vfx, second);
            fixture.AssertOriginalPose();
        }

        [Test]
        public void ForcedSettlementRestoresBothPosesOnceAndOldDisposeCannotUndoLaterPlacement()
        {
            using var fixture = new PoseFixture();
            int settlements = 0, unlocks = 0;
            Action<uint> settled = sequence => { if (sequence == 4404) settlements++; };
            CombatVFXManager.OnPresentationSettled += settled;
            try
            {
                var execution = fixture.Vfx.BeginPresentation(4404, null);
                var cameraLease = fixture.Vfx.CapturePresentationTransform(fixture.Camera.transform);
                var fpsLease = fixture.Vfx.CapturePresentationTransform(fixture.Fps.transform);
                execution.Resources.HoldInventory(() => { }, () => unlocks++);
                fixture.MoveToContact(1);

                fixture.Vfx.ForceSettleMultiPresentation(4404);
                fixture.Vfx.ForceSettleMultiPresentation(4404);

                fixture.AssertOriginalPose();
                Assert.That(execution.CanContinue, Is.False);
                Assert.That(settlements, Is.EqualTo(1));
                Assert.That(unlocks, Is.EqualTo(1));

                fixture.MoveToContact(3);
                var laterCamera = new WorldPose(fixture.Camera.transform);
                var laterFps = new WorldPose(fixture.Fps.transform);
                cameraLease.Dispose();
                fpsLease.Dispose();
                execution.Resources.Dispose();
                laterCamera.AssertMatches(fixture.Camera.transform, "camera after completed lease");
                laterFps.AssertMatches(fixture.Fps.transform, "FPS after completed lease");
            }
            finally { CombatVFXManager.OnPresentationSettled -= settled; }
        }

        [Test]
        public void DisabledPresentationRestoresBothPosesWithoutSettlementAndCanRestart()
        {
            using var fixture = new PoseFixture();
            int settlements = 0;
            Action<uint> settled = _ => settlements++;
            CombatVFXManager.OnPresentationSettled += settled;
            try
            {
                var interrupted = fixture.Vfx.BeginPresentation(4405, null);
                fixture.Capture();
                fixture.MoveToContact(1);

                // EditMode does not invoke this non-ExecuteAlways component's Play lifecycle.
                typeof(CombatVFXManager).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(fixture.Vfx, null);

                fixture.AssertOriginalPose();
                Assert.That(interrupted.Settled, Is.True);
                Assert.That(settlements, Is.Zero, "Abandoning a session must not announce normal completion.");

                var restarted = fixture.Vfx.BeginPresentation(4406, null);
                fixture.Capture();
                fixture.MoveToContact(2);
                var currentCamera = new WorldPose(fixture.Camera.transform);
                var currentFps = new WorldPose(fixture.Fps.transform);
                CompleteNormally(fixture.Vfx, interrupted);
                currentCamera.AssertMatches(fixture.Camera.transform, "restarted camera");
                currentFps.AssertMatches(fixture.Fps.transform, "restarted FPS");
                Assert.That(restarted.CanContinue, Is.True);
                Assert.That(settlements, Is.Zero);

                CompleteNormally(fixture.Vfx, restarted);
                fixture.AssertOriginalPose();
                Assert.That(settlements, Is.EqualTo(1));
            }
            finally { CombatVFXManager.OnPresentationSettled -= settled; }
        }

        [TestCase("normal")]
        [TestCase("forced")]
        [TestCase("disabled")]
        public void CameraChildFallbackFpsRestoresWorldAndLocalPoseWhenCameraRestoresFirst(string ending)
        {
            var owner = new GameObject("FallbackPresentationOwner");
            var camera = new GameObject("FallbackCamera");
            var fps = new GameObject("FallbackFps");
            try
            {
                var vfx = owner.AddComponent<CombatVFXManager>();
                camera.transform.SetPositionAndRotation(new Vector3(-2, 4, -5), Quaternion.Euler(16, 25, 0));
                fps.transform.SetParent(camera.transform, false);
                fps.transform.localPosition = new Vector3(-0.4f, -1.1f, 2);
                fps.transform.localRotation = Quaternion.Euler(5, 0, 3);
                var cameraStart = new WorldPose(camera.transform);
                var fpsStart = new WorldPose(fps.transform);
                var fpsLocalPosition = fps.transform.localPosition;
                var fpsLocalRotation = fps.transform.localRotation;

                var execution = vfx.BeginPresentation(4407, null);
                // Resource cleanup is LIFO: restore the parent camera before the child world pose.
                vfx.CapturePresentationTransform(fps.transform);
                vfx.CapturePresentationTransform(camera.transform);
                camera.transform.SetPositionAndRotation(new Vector3(3, 2, 4), Quaternion.Euler(9, -35, 0));
                fps.transform.position += Vector3.right;
                fps.transform.rotation = Quaternion.Euler(20, -15, 4);

                if (ending == "normal") CompleteNormally(vfx, execution);
                else if (ending == "forced") vfx.ForceSettleMultiPresentation(4407);
                else typeof(CombatVFXManager).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(vfx, null);

                cameraStart.AssertMatches(camera.transform, "fallback camera");
                fpsStart.AssertMatches(fps.transform, "fallback FPS world pose");
                Assert.That(Vector3.Distance(fps.transform.localPosition, fpsLocalPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(fps.transform.localRotation, fpsLocalRotation), Is.LessThan(0.1f));
                Assert.That(fps.transform.parent, Is.SameAs(camera.transform));
                Assert.That(execution.Settled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(camera);
            }
        }

        [TestCase(1.5f, 1.1666666f, 1.35f, 1.85f)]
        [TestCase(1.5f, 2.5f, 0.3f, 2.5f)]
        [TestCase(4f, 1.1666666f, 1.35f, 4f)]
        public void FeedingDurationAllowsReceiverToFinishAndRespectsLongerGiverOrConfiguration(
            float configured, float giver, float receiver, float expected)
        {
            Assert.That(ContactPresentationTiming.FeedDuration(configured, giver, receiver),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [TestCase(1.5f, 1.0166667f, 3.0166667f)]
        [TestCase(1.5f, 2.1f, 4.1f)]
        [TestCase(4f, 1.0166667f, 4f)]
        [TestCase(0f, 0f, 2.5f)]
        public void HugDurationAllowsApproachContactGestureAndReturnWithoutCuttingTheClip(
            float configured, float gesture, float expected)
        {
            Assert.That(ContactPresentationTiming.HugDuration(configured, gesture),
                Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void CurrentContactClipsCompleteGestureAndRecoveryWithinMultiBarrierAllowance()
        {
            var hug = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/FPS/FPS_hug.anim");
            var giver = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/FPS/FPS_feed.anim");
            var receiver = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Resources/FPS/FPS_useItem.anim");
            Assert.That(hug, Is.Not.Null);
            Assert.That(giver, Is.Not.Null);
            Assert.That(receiver, Is.Not.Null);

            float hugDuration = ContactPresentationTiming.HugDuration(1.5f, hug.length);
            float feedDuration = ContactPresentationTiming.FeedDuration(1.5f, giver.length, receiver.length);

            var batch = new CombatResolutionBatchNetData
            {
                SeatCount = 2, Events = Array.Empty<CombatEventNetData>(), MainItemIds = new short[] { 0, -1 }
            };
            float emptyBudget = MultiPresentationSchedule.Build(batch, _ => 1.5f, _ => false).BarrierBudgetSeconds;
            batch.Events = new[] { new CombatEventNetData
            {
                ActorSeat = 0, TargetSeat = 1, EventType = (byte)CombatEventType.MainEffect
            } };
            batch.EventCount = 1;
            float actionAllowance = MultiPresentationSchedule.Build(batch, _ => 1.5f, _ => false).BarrierBudgetSeconds
                - emptyBudget;
            Assert.That(hugDuration, Is.GreaterThanOrEqualTo(3.01f).And.LessThanOrEqualTo(actionAllowance),
                "The full jump, two-hand hug and return must fit the action slot plus its barrier margin.");
            Assert.That(feedDuration, Is.GreaterThanOrEqualTo(1.849f).And.LessThanOrEqualTo(3f),
                "The receiver's full use animation follows the handoff and must not be cut at 1.5 seconds.");
        }

        static void CompleteNormally(CombatVFXManager owner, PresentationExecution execution)
        {
            var method = typeof(CombatVFXManager).GetMethod("CompletePresentationSequence",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(owner, new[] { (object)execution,
                Enum.ToObject(method.GetParameters()[1].ParameterType, 0) });
        }

        readonly struct WorldPose
        {
            readonly Vector3 _position;
            readonly Quaternion _rotation;
            public WorldPose(Transform target)
            { _position = target.position; _rotation = target.rotation; }
            public bool Matches(Transform target) => Vector3.Distance(target.position, _position) < 0.0001f
                && Quaternion.Angle(target.rotation, _rotation) < 0.1f;
            public void AssertMatches(Transform target, string label)
            {
                Assert.That(Vector3.Distance(target.position, _position), Is.LessThan(0.0001f), label + " position");
                Assert.That(Quaternion.Angle(target.rotation, _rotation), Is.LessThan(0.1f), label + " rotation");
            }
        }

        sealed class PoseFixture : IDisposable
        {
            readonly GameObject _owner = new("ContactPresentationOwner");
            readonly GameObject _fpsAnchor = new("AuthoredFpsAnchor");
            readonly WorldPose _cameraPose, _fpsPose;
            public readonly GameObject Camera = new("ContactCamera");
            public readonly GameObject Fps = new("AuthoredFpsRoot");
            public readonly CombatVFXManager Vfx;
            public bool HasOriginalPose => _cameraPose.Matches(Camera.transform) && _fpsPose.Matches(Fps.transform);

            public PoseFixture()
            {
                Vfx = _owner.AddComponent<CombatVFXManager>();
                Camera.transform.SetPositionAndRotation(new Vector3(1, 3.5f, -4), Quaternion.Euler(16, 12, 0));
                _fpsAnchor.transform.SetPositionAndRotation(new Vector3(-3, -1, -2), Quaternion.Euler(0, 18, 3));
                Fps.transform.SetParent(_fpsAnchor.transform, false);
                Fps.transform.localPosition = new Vector3(-0.39f, 0.01f, -3.5f);
                Fps.transform.localRotation = Quaternion.Euler(30, 0, 0);
                _cameraPose = new WorldPose(Camera.transform);
                _fpsPose = new WorldPose(Fps.transform);
            }

            public void Capture()
            {
                Vfx.CapturePresentationTransform(Camera.transform);
                Vfx.CapturePresentationTransform(Fps.transform);
            }

            public void MoveToContact(int step)
            {
                Camera.transform.SetPositionAndRotation(new Vector3(2 * step, 2, 1 + step),
                    Quaternion.Euler(6, 25 * step, 2));
                Fps.transform.SetPositionAndRotation(new Vector3(2 * step, 1.2f, 2 + step),
                    Quaternion.Euler(20, 25 * step, -3));
            }

            public void AssertOriginalPose()
            {
                _cameraPose.AssertMatches(Camera.transform, "original camera");
                _fpsPose.AssertMatches(Fps.transform, "original FPS");
                Assert.That(Fps.transform.parent, Is.SameAs(_fpsAnchor.transform), "Authored hierarchy must survive presentation.");
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_owner);
                Object.DestroyImmediate(Camera);
                Object.DestroyImmediate(_fpsAnchor);
            }
        }
    }
}
