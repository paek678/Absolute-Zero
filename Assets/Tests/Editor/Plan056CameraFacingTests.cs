using AbsoluteZero.Core.Combat;
using System.Linq;
using AbsoluteZero.Core.Network;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace AbsoluteZero.Tests
{
    public class Plan056CameraFacingTests
    {
        [TestCase(GameMode.Multi, true)]
        [TestCase(GameMode.None, false)]
        [TestCase(GameMode.OneVsOne, true)]
        [TestCase(GameMode.Solo, true)]
        public void AllGameplayModesAlignArtwork(GameMode mode, bool expected)
            => Assert.That(DuelSpriteFacing.UsesScreenAlignedArt(mode), Is.EqualTo(expected));

        [Test]
        public void AttachedLabelAndWorldCanvasFaceCameraAndRestoreWithParent()
        {
            var camera = new GameObject("camera", typeof(Camera));
            var actor = new GameObject("actor", typeof(SortingGroup));
            var label = new GameObject("uses", typeof(TextMesh));
            var canvas = new GameObject("attached ui", typeof(Canvas));
            label.transform.SetParent(actor.transform, false);
            canvas.transform.SetParent(actor.transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            try
            {
                actor.transform.SetPositionAndRotation(new Vector3(-7, 1, 4), Quaternion.Euler(0, 25, 12));
                label.transform.localPosition = new Vector3(0, -.6f, 0);
                canvas.transform.localPosition = new Vector3(0, 2, 0);
                var position = label.transform.position;
                var rotation = actor.transform.rotation;
                var facing = camera.AddComponent<DuelSpriteFacing>();
                facing.AddRoot(actor.transform);
                var view = Quaternion.Euler(30, -20, 0);
                facing.ApplyFacing(view);
                Assert.That(Quaternion.Angle(label.transform.rotation, view), Is.LessThan(.01));
                Assert.That(Quaternion.Angle(canvas.transform.rotation, view), Is.LessThan(.01));
                facing.Restore();
                Assert.That(Quaternion.Angle(actor.transform.rotation, rotation), Is.LessThan(.01));
                Assert.That(label.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(Vector3.Distance(label.transform.position, position), Is.LessThan(.001));
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(camera); }
        }

        [TestCase(GameMode.Multi, true)]
        [TestCase(GameMode.None, false)]
        [TestCase(GameMode.OneVsOne, true)]
        [TestCase(GameMode.Solo, true)]
        public void GameplayModesTurn(GameMode mode, bool expected)
            => Assert.That(DuelSpriteFacing.UsesTurningCamera(mode), Is.EqualTo(expected));

        [TestCase(GameMode.Multi, 3f)]
        [TestCase(GameMode.OneVsOne, 25f)]
        [TestCase(GameMode.Solo, 25f)]
        [TestCase(GameMode.None, 0f)]
        public void MultiUsesASmallerPanWithoutChangingDuel(GameMode mode, float expected)
            => Assert.That(DuelSpriteFacing.EnvironmentPanDegrees(mode), Is.EqualTo(expected));

        [Test]
        public void WholeArtworkTurnsOnceAndRestoresAnimationPose()
        {
            var camera = new GameObject("test camera", typeof(Camera));
            var actor = new GameObject("actor", typeof(SortingGroup));
            var arm = new GameObject("arm", typeof(SpriteRenderer));
            arm.transform.SetParent(actor.transform, false);
            try
            {
                var facing = camera.AddComponent<DuelSpriteFacing>();
                var root = DuelSpriteFacing.FindArtworkRoot(arm.GetComponent<SpriteRenderer>());
                Assert.That(root, Is.EqualTo(actor.transform));
                facing.AddRoot(arm.transform); facing.AddRoot(root); facing.AddRoot(arm.transform);
                actor.transform.rotation = Quaternion.Euler(8, 12, 3);
                arm.transform.localRotation = Quaternion.Euler(0, 0, 32);
                arm.transform.localPosition = new Vector3(.8f, 1.1f, -.1f);
                var before = actor.transform.rotation;
                var limb = arm.transform.localRotation;
                var pos = arm.transform.localPosition;
                facing.ApplyFacing(Quaternion.Euler(30, -25, 5));
                Assert.That(Quaternion.Angle(actor.transform.rotation, DuelSpriteFacing.ScreenAlignedRotation(Quaternion.Euler(30, -25, 5), before)), Is.LessThan(.01));
                Assert.That(Quaternion.Angle(arm.transform.localRotation, limb), Is.LessThan(.01));
                Assert.That(arm.transform.localPosition, Is.EqualTo(pos));
                facing.ApplyFacing(Quaternion.Euler(30, -25, 5)); // no cumulative drift
                facing.Restore(); facing.Restore();
                Assert.That(Quaternion.Angle(actor.transform.rotation, before), Is.LessThan(.01));
                actor.transform.rotation = Quaternion.Euler(0, 0, 18); // next animated pose is not frozen
                facing.ApplyFacing(Quaternion.Euler(-20, 25, 0));
                // EditMode does not dispatch MonoBehaviour disable callbacks automatically.
                typeof(DuelSpriteFacing).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic).Invoke(facing, null);
                Assert.That(Quaternion.Angle(actor.transform.rotation, Quaternion.Euler(0, 0, 18)), Is.LessThan(.01));
            }
            finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(camera); }
        }

        [TestCase(30, -25, 0)]
        [TestCase(-40, 80, 20)]
        [TestCase(0, 170, -15)]
        public void ImagePlaneIsParallelToScreenAtAnyCameraAngle(float pitch, float yaw, float roll)
        {
            var camera = Quaternion.Euler(pitch, yaw, roll);
            var art = Quaternion.Euler(15, 20, 32);
            var result = DuelSpriteFacing.ScreenAlignedRotation(camera, art);
            Assert.That(Vector3.Dot(result * Vector3.forward, camera * Vector3.forward), Is.GreaterThan(.99999f));
        }

        [Test]
        public void BottomHeightAndPositionSurvivePitchAndRestore()
        {
            var camera = new GameObject("camera", typeof(Camera));
            var item = new GameObject("item", typeof(SpriteRenderer));
            var texture = new Texture2D(16,16);
            var sprite = Sprite.Create(texture, new Rect(0,0,16,16), new Vector2(.5f,.5f), 8);
            try
            {
                var renderer = item.GetComponent<SpriteRenderer>(); renderer.sprite = sprite;
                item.transform.position = new Vector3(1,1,2);
                var position = item.transform.position;
                // SpriteRenderer's 3D culling bounds have depth; check the actual 2D plane.
                float bottom = sprite.vertices.Min(v => item.transform.TransformPoint(v).y);
                var facing = camera.AddComponent<DuelSpriteFacing>(); facing.AddRoot(item.transform);
                facing.ApplyFacing(Quaternion.Euler(40,25,0));
                Assert.That(sprite.vertices.Min(v => item.transform.TransformPoint(v).y), Is.EqualTo(bottom).Within(.001));
                facing.Restore();
                Assert.That(item.transform.position, Is.EqualTo(position));
            }
            finally { Object.DestroyImmediate(item); Object.DestroyImmediate(camera); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void FlatFloorAndCameraAttachedSpritesAreExcluded()
        {
            var root = new GameObject("test", typeof(Camera));
            var art = new GameObject("art", typeof(SpriteRenderer));
            try
            {
                art.transform.rotation = Quaternion.Euler(90, 0, 0);
                Assert.That(DuelSpriteFacing.FindArtworkRoot(art.GetComponent<SpriteRenderer>()), Is.Null);
                art.transform.rotation = Quaternion.identity;
                art.transform.SetParent(root.transform);
                Assert.That(DuelSpriteFacing.FindArtworkRoot(art.GetComponent<SpriteRenderer>()), Is.Null);
            }
            finally { Object.DestroyImmediate(art); Object.DestroyImmediate(root); }
        }
    }
}
