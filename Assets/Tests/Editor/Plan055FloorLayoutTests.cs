using System.Linq;
using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Match;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public class Plan055FloorLayoutTests
    {
        [TestCase(-4.4f, 1.5f)] [TestCase(0f, 5.4f)] [TestCase(4.4f, 1.5f)] [TestCase(0f, .8f)]
        public void RackRowsLieOnFloorAndFaceCenter(float x, float z)
        {
            var origin = new Vector3(x, .85f, z);
            var center = MultiPerspectiveLayout.IceboxCenter;
            var left = MultiItemFacing.RackPosition(origin, center, -1, 0);
            var right = MultiItemFacing.RackPosition(origin, center, 1, 0);
            var front = MultiItemFacing.RackPosition(origin, center, 0, 1);
            var inward = Vector3.ProjectOnPlane(center - origin, Vector3.up).normalized;
            Assert.That(left.y, Is.EqualTo(origin.y).Within(.0001));
            Assert.That(right.y, Is.EqualTo(origin.y).Within(.0001));
            Assert.That(Mathf.Abs(Vector3.Dot((right-left).normalized, inward)), Is.LessThan(.0001));
            Assert.That(Vector3.Dot((front-origin).normalized, inward), Is.GreaterThan(.9999));
        }

        [Test]
        public void FloorClearanceIncludesHoverExpansionAndIsIdempotent()
        {
            var go = new GameObject("Rotated card"); var texture = new Texture2D(16, 16);
            var sprite = Sprite.Create(texture, new Rect(0,0,16,16), new Vector2(.5f,.5f), 8);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sprite;
                go.transform.rotation = Quaternion.Euler(22,0,70);
                MultiItemFacing.ClearFloor(go.transform, sr, 0, 1.3f);
                var saved = go.transform.position;
                MultiItemFacing.ClearFloor(go.transform, sr, 0, 1.3f);
                Assert.That(go.transform.position, Is.EqualTo(saved));
                go.transform.localScale = Vector3.one * 1.3f;
                foreach (var vertex in sprite.vertices)
                    Assert.That(go.transform.TransformPoint(vertex).y, Is.GreaterThanOrEqualTo(.039f));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void MultiLowerBodyMeetsFloorWithoutMovingBonesAndDebugFragmentIsHidden()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene_Multi.unity");
            try
            {
                var b = scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MatchViewBindings>(true)).Single();
                var map = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Maps/PavilionNight/PavilionNight.prefab").GetComponent<MapCharacterLayout>();
                for(int i=0;i<3;i++)
                {
                    var root=b.GetRemoteVisual(i);var lower=root.Find("lowerbody").GetComponent<SpriteRenderer>();var local=lower.transform.localPosition;
                    var anchor=map.GetAnchor(MapLayoutMode.Multi, MapAnchorRole.RemoteVisual,i);anchor.ApplyVisualPose(root);
                    float bottom=lower.sprite.vertices.Min(v=>lower.transform.TransformPoint(v).y);
                    Assert.That(bottom,Is.EqualTo(anchor.transform.position.y+.025f).Within(.0001));
                    Assert.That(lower.transform.localPosition,Is.EqualTo(local));
                    var first=root.position;anchor.ApplyVisualPose(root);Assert.That(Vector3.Distance(root.position,first),Is.LessThan(.0001));
                }
                Assert.That(b.GetRemoteVisual(1).Find("Test").GetComponent<SpriteRenderer>().enabled,Is.False);
                Assert.That(b.GetRemoteVisual(1).Find("Test").gameObject.activeSelf,Is.False);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
