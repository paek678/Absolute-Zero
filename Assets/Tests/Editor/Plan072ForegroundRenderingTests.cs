using AbsoluteZero.Core.Common;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan072ForegroundRenderingTests
    {
        [Test]
        public void SharedMaterialPreservesDifferentTexturesAndTracksSpriteReplacement()
        {
            var first = new GameObject("First");
            var second = new GameObject("Second");
            var texA = new Texture2D(2, 2);
            var texB = new Texture2D(2, 2);
            var a = Sprite.Create(texA, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            var b = Sprite.Create(texB, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            try
            {
                var one = first.AddComponent<SpriteRenderer>(); one.sprite = a;
                var two = second.AddComponent<SpriteRenderer>(); two.sprite = b;
                WorldInteractionLayer.Apply(one);
                WorldInteractionLayer.Apply(two);
                Assert.That(one.sharedMaterial, Is.Not.Null);
                Assert.That(one.sharedMaterial, Is.SameAs(two.sharedMaterial));
                AssertTexture(one, texA); AssertTexture(two, texB);
                one.sprite = b;
                AssertTexture(one, texB);
                first.SetActive(false); one.sprite = a; first.SetActive(true);
                AssertTexture(one, texA); AssertTexture(two, texB);
            }
            finally
            {
                Object.DestroyImmediate(first); Object.DestroyImmediate(second);
                Object.DestroyImmediate(a); Object.DestroyImmediate(b);
                Object.DestroyImmediate(texA); Object.DestroyImmediate(texB);
            }
        }

        static void AssertTexture(SpriteRenderer renderer, Texture expected)
        {
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.That(properties.GetTexture("_MainTex"), Is.SameAs(expected));
        }
    }
}
