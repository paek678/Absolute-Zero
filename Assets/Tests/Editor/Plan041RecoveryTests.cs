using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan041RecoveryTests
    {
        [Test]
        public void EveryAuthoredHatSliceIsRegisteredExactlyOnceWithAValidHeadBinding()
        {
            const string path = "Assets/Art/gameGem/MainFolder/Sprite/Customizing/hat.png";
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            var hats = registry.GetByPart(CosmeticPart.Head).Where(i => i.Id.StartsWith("hat_")).ToArray();
            Assert.That(hats.Select(i => i.Id).Distinct().Count(), Is.EqualTo(hats.Length));
            Assert.That(hats.All(i => i.Id.Length <= 8 && i.Type == CosmeticType.Overlay), Is.True);
            CollectionAssert.AreEquivalent(sprites, hats.Select(i => i.Sprite).ToArray());
            foreach (var hat in hats)
            {
                Assert.That(hat.Atlas, Is.Not.Null, hat.Id);
                Assert.That(hat.Atlas.Bindings.Count, Is.EqualTo(1), hat.Id);
                var binding = hat.Atlas.Bindings[0];
                Assert.That(binding.Replacement, Is.SameAs(hat.Sprite), hat.Id);
                Assert.That(binding.RendererPath, Is.EqualTo("body/head"));
                Assert.That(binding.Overlay && binding.SortOffset > 0 && binding.LocalScale.x > 0, Is.True);
            }
        }

        [TestCase("top_01")] [TestCase("top_02")] [TestCase("top_03")]
        [TestCase("top_04")] [TestCase("top_05")] [TestCase("top_06")] [TestCase("top_07")]
        public void IncomingTopMapsEveryAuthoredBodyAndArmPoseAndCanBeUnequipped(string id)
        {
            var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            var item = registry.GetById(id);
            Assert.That(item, Is.Not.Null, id);
            Assert.That(item.Part, Is.EqualTo(CosmeticPart.Top));
            Assert.That(item.Atlas.Bindings.Count, Is.EqualTo(32));
            var root = new GameObject("Cosmetic pose test");
            try
            {
                var body = new GameObject("body"); body.transform.SetParent(root.transform);
                body.AddComponent<SpriteRenderer>();
                foreach (var name in new[] { "arm1", "arm2" })
                {
                    var arm = new GameObject(name); arm.transform.SetParent(body.transform); arm.AddComponent<SpriteRenderer>();
                }
                var renderer = root.AddComponent<CosmeticAtlasRenderer>();
                foreach (var binding in item.Atlas.Bindings)
                {
                    Assert.That(binding.View, Is.EqualTo(CosmeticView.Character));
                    Assert.That(binding.Overlay, Is.False);
                    Assert.That(binding.Source, Is.Not.Null); Assert.That(binding.Replacement, Is.Not.Null);
                    var target = root.transform.Find(binding.RendererPath).GetComponent<SpriteRenderer>();
                    target.sprite = binding.Source;
                    renderer.Add(item.Atlas, CosmeticView.Character); renderer.ApplyFrame();
                    Assert.That(target.sprite, Is.SameAs(binding.Replacement), binding.Note);
                    renderer.Clear();
                    Assert.That(target.sprite, Is.SameAs(binding.Source), binding.Note);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test] public void IncomingTopsDoNotReplaceUnrelatedPartsAndHaveUniqueWireIds()
        {
            var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            var tops = Enumerable.Range(1, 7).Select(i => registry.GetById("top_" + i.ToString("D2"))).ToArray();
            Assert.That(tops.All(t => t != null), Is.True);
            Assert.That(tops.Select(t => t.Id).Distinct().Count(), Is.EqualTo(7));
            Assert.That(tops.All(t => t.Id.Length <= 8), Is.True);
            foreach (var t in tops)
                Assert.That(t.Atlas.Bindings.All(b => b.RendererPath == "body" || b.RendererPath == "body/arm1" || b.RendererPath == "body/arm2"), Is.True);
        }
    }
}
