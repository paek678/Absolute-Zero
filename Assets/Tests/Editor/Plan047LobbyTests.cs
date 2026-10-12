using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.UI.LobbyUI;
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan047LobbyTests
    {
        [Test] public void EquipReplacesOnlyVisualAndRetainsScaleAndCleanup()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Closet/ClosetPreviewVisual.prefab");
            var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            var go = new GameObject("Fallback");
            var fallback = go.AddComponent<SpriteRenderer>();
            fallback.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/18.png");
            var state = new CosmeticEquipState();
            var equipment = new CosmeticEquipmentService(state, registry, null);
            var originalHead = prefab.transform.Find("body/head").GetComponent<SpriteRenderer>().sprite;
            var face = AssetDatabase.LoadAllAssetsAtPath("Assets/MainFolder/Sprite/player_heal.png").OfType<Sprite>()
                .Single(s => s.name == "player_heal_3");
            var presenter = new LobbyCharacterPresenter(prefab.transform, registry, fallback, equipment, state, new Rect(0, 0, 1, 1), face);
            try
            {
                Assert.That(presenter.VisualRoot, Is.Not.Null);
                Assert.That(fallback.enabled, Is.False);
                var scale = presenter.VisualRoot.transform.localScale;
                var first = presenter.VisualRoot;
                Assert.That(equipment.Set(CosmeticPart.Head, "hat_01").Success, Is.True);
                Assert.That(first == null, Is.True);
                Assert.That(presenter.VisualRoot.transform.localScale, Is.EqualTo(scale));
                Assert.That(presenter.VisualRoot.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true), Is.Empty);
                Assert.That(presenter.VisualRoot.GetComponentsInChildren<Animator>(true), Is.Empty);
                Assert.That(equipment.Set(CosmeticPart.Head, "").Success, Is.True);
                foreach (var hat in registry.GetByPart(CosmeticPart.Head))
                {
                    Assert.That(equipment.Set(CosmeticPart.Head, hat.Id).Success, Is.True);
                    presenter.VisualRoot.GetComponent<CosmeticAtlasRenderer>().ApplyFrame();
                    Assert.That(presenter.VisualRoot.transform.Find("body/head").GetComponent<SpriteRenderer>().sprite, Is.SameAs(face));
                }
                Assert.That(prefab.transform.Find("body/head").GetComponent<SpriteRenderer>().sprite, Is.SameAs(originalHead));
            }
            finally { presenter.Dispose(); Object.DestroyImmediate(go); }
            Assert.That(presenter.VisualRoot, Is.Null);
        }
    }
}
