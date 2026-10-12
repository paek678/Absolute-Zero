using AbsoluteZero.Core.Inventory;
using AbsoluteZero.UI.LobbyUI;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan070VisualPolishTests
    {
        [TestCase("1")]
        [TestCase("3")]
        [TestCase("∞")]
        public void WorldItemLabelsNeverDisplayUseCounts(string uses)
        {
            var go = new GameObject("Item label test");
            try
            {
                var view = go.AddComponent<AbsoluteZero.Core.Item.ItemWorldView>();
                view.Initialize(0, "Fan", Color.white);
                view.UpdateDisplay("Fan", uses, true);
                foreach (var label in go.GetComponentsInChildren<TextMesh>(true))
                    Assert.That(label.text, Is.EqualTo(AbsoluteZero.Core.Common.GameplayDebugDisplay.ShowItemNames ? "Fan" : ""));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(0f, 0f, 1f)]
        [TestCase(25f, 90f, 1.2f)]
        [TestCase(-25f, -90f, 1.4f)]
        [TestCase(3f, 180f, 1.2f)]
        public void RotatedAndHoveredArtworkStaysAboveRaisedFloor(float yaw, float roll, float scale)
        {
            var root = new GameObject("FloorTest");
            var texture = new Texture2D(100, 200);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 100, 200), new Vector2(.3f, .7f), 100);
            try
            {
                var artObject = new GameObject("Art");
                artObject.transform.SetParent(root.transform);
                artObject.transform.localPosition = new Vector3(.1f, -.15f, 0);
                var art = artObject.AddComponent<SpriteRenderer>(); art.sprite = sprite;
                root.transform.position = new Vector3(5, .3f, 8);
                root.transform.rotation = Quaternion.Euler(25, yaw, roll);
                root.transform.localScale = Vector3.one * scale;
                MultiItemFacing.ClearFloor(root.transform, art, .6f);
                foreach (var vertex in sprite.vertices)
                    Assert.That(art.transform.TransformPoint(vertex).y, Is.GreaterThanOrEqualTo(.6399f));
                Assert.That(root.transform.position.x, Is.EqualTo(5));
                Assert.That(root.transform.position.z, Is.EqualTo(8));
                var once = root.transform.position;
                MultiItemFacing.ClearFloor(root.transform, art, .6f);
                Assert.That(Vector3.Distance(once, root.transform.position), Is.LessThan(.0001f));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [TestCase(2, true)] [TestCase(4, true)] [TestCase(2, false)] [TestCase(4, false)]
        public void RoomStackFitsInsidePanelWithoutOverlapping(int seats, bool joined)
        {
            var root = new GameObject("Room", typeof(RectTransform));
            try
            {
                var panel = new GameObject("PanelBG", typeof(RectTransform)); panel.transform.SetParent(root.transform);
                string[] names = { "ModeLabel", "Title", "LobbyCode", "PlayerList", "LogScroll", "StatusText", "StartBtn", "BackLink" };
                foreach (var name in names) new GameObject(name, typeof(RectTransform)).transform.SetParent(panel.transform);
                CasualLobbySkin.RoomLayout(root.transform, seats, joined);
                float bottom = ((RectTransform)panel.transform).rect.yMin;
                float previousBottom = ((RectTransform)panel.transform).rect.yMax;
                foreach (var name in names)
                {
                    var rect = (RectTransform)panel.transform.Find(name);
                    float top = rect.anchoredPosition.y + rect.rect.yMax;
                    float nextBottom = rect.anchoredPosition.y + rect.rect.yMin;
                    Assert.That(top, Is.LessThanOrEqualTo(previousBottom), name + " overlap");
                    Assert.That(nextBottom, Is.GreaterThan(bottom), name + " outside panel");
                    previousBottom = nextBottom;
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
