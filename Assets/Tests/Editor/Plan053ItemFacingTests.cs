using AbsoluteZero.Core.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan053ItemFacingTests
    {
        [TestCase(-4.4f, .85f, 1.5f)]
        [TestCase(0, .85f, 5.4f)]
        [TestCase(4.4f, .85f, 1.5f)]
        [TestCase(-2.7f, .3f, .8f)]
        [TestCase(2.7f, .3f, -.5f)]
        public void AllSeatSpriteTopsFaceBoxAndKeepTheirVisiblePlane(float x, float y, float z)
        {
            var position = new Vector3(x, y, z);
            var view = Quaternion.Euler(22, 0, 0);
            var rotation = MultiItemFacing.TowardCenter(position, MultiPerspectiveLayout.IceboxCenter, view);
            var projected = Vector3.ProjectOnPlane(MultiPerspectiveLayout.IceboxCenter - position, view * Vector3.forward).normalized;
            Assert.That(Vector3.Dot(rotation * Vector3.up, projected), Is.GreaterThan(.9999f));
            Assert.That(Vector3.Dot(rotation * Vector3.forward, view * Vector3.forward), Is.GreaterThan(.9999f));
        }
    }
}
