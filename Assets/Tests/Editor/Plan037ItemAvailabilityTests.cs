using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037ItemAvailabilityTests
    {
        readonly List<Object> _objects = new();
        UnityEngine.Random.State _random;

        [SetUp] public void SetUp() => _random = UnityEngine.Random.state;
        [TearDown] public void TearDown()
        {
            UnityEngine.Random.state = _random;
            foreach (var value in _objects) Object.DestroyImmediate(value);
            _objects.Clear();
        }

        T Create<T>() where T : ScriptableObject
        {
            var item = ScriptableObject.CreateInstance<T>(); _objects.Add(item); return item;
        }

        [Test]
        public void DisabledEffectIsIndependentOfDisplayNameFreeActionAndDropWeight()
        {
            var disabled = Create<SpecialItemDataSO>();
            disabled.SpecialEffect = SpecialEffectType.RevealOpponent;
            disabled.ItemName = "Renamed item"; disabled.DropWeight = 100000; disabled.IsFreeAction = false;
            var ordinary = Create<AttackItemDataSO>(); ordinary.ItemName = "TarotCard";
            Assert.IsFalse(ItemAvailability.IsEnabled(null));
            Assert.IsFalse(ItemAvailability.IsEnabled(disabled));
            Assert.IsFalse(disabled.CanUse(null), "Availability rejects before live context is accessed.");
            Assert.IsTrue(ItemAvailability.IsEnabled(ordinary), "Display text is not identity.");
        }

        [Test]
        public void EmptyOrDisabledOnlyTableDoesNotDrawRandomOrBecomeEnabledByPredicate()
        {
            var disabled = Create<SpecialItemDataSO>();
            disabled.SpecialEffect = SpecialEffectType.RevealOpponent; disabled.DropWeight = 100;
            var table = new ItemDropTable(new ItemDataSO[] { null, disabled }, _ => true);
            Assert.IsTrue(table.IsEmpty);
            UnityEngine.Random.InitState(3701);
            var state = UnityEngine.Random.state;
            Assert.IsNull(table.Roll()); Assert.IsNull(table.Roll(_ => true));
            Assert.AreEqual(state, UnityEngine.Random.state);
            Assert.IsTrue(new ItemDropTable(null).IsEmpty);
        }

        [Test]
        public void ExclusionPreservesEnabledOrderWeightsAndRandomDrawCount()
        {
            var first = Create<AttackItemDataSO>(); first.DropWeight = 2;
            var second = Create<AttackItemDataSO>(); second.DropWeight = 7;
            var disabled = Create<SpecialItemDataSO>();
            disabled.SpecialEffect = SpecialEffectType.RevealOpponent; disabled.DropWeight = 500;
            var registry = new ItemDataSO[] { first, disabled, second };
            var before = registry.ToArray();
            var expected = new ItemDropTable(new ItemDataSO[] { first, second });
            var actual = new ItemDropTable(registry);
            UnityEngine.Random.InitState(3701);
            var draws = Enumerable.Range(0, 128).Select(_ => expected.Roll()).ToArray();
            var final = UnityEngine.Random.state;
            UnityEngine.Random.InitState(3701);
            CollectionAssert.AreEqual(draws, Enumerable.Range(0, 128).Select(_ => actual.Roll()).ToArray());
            Assert.AreEqual(final, UnityEngine.Random.state);
            CollectionAssert.AreEqual(before, registry, "Constructing a playable table must not reindex the catalog.");
            Assert.AreEqual(2, first.DropWeight); Assert.AreEqual(7, second.DropWeight);
        }

        [TestCase("Assets/Scenes/LobbyScene.unity")]
        [TestCase("Assets/Scenes/GameScene.unity")]
        [TestCase("Assets/Scenes/GameScene_Multi.unity")]
        [TestCase("Assets/Scenes/GameScene_Solo.unity")]
        public void SavedCatalogRetainsAllIdentitiesAndExactlyTwentyPlayableItems(string path)
        {
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var managers = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<ItemManager>(true)).ToArray();
                if (path != "Assets/Scenes/LobbyScene.unity")
                    Assert.AreEqual(1, managers.Length, "Gameplay scenes require exactly one item registry.");
                else Assert.LessOrEqual(managers.Length, 1);
                // Lobby has the composition catalog rather than a spawned ItemManager.
                ItemDataSO[] catalog;
                if (managers.Length == 1) catalog = managers[0].GetAllItems();
                else
                {
                    var yaml = System.IO.File.ReadAllText(path);
                    var ids = AssetDatabase.FindAssets("t:ItemDataSO", new[] { "Assets/Data/Items" });
                    Assert.AreEqual(21, ids.Length);
                    foreach (var id in ids) StringAssert.Contains("guid: " + id, yaml);
                    catalog = ids.Select(id => AssetDatabase.LoadAssetAtPath<ItemDataSO>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
                }
                Assert.AreEqual(21, catalog.Length);
                Assert.AreEqual(20, catalog.Count(ItemAvailability.IsEnabled));
                Assert.AreEqual(21, catalog.Distinct().Count());
                var inactive = catalog.Single(item => !ItemAvailability.IsEnabled(item));
                if (managers.Length == 1) Assert.AreSame(inactive, catalog[17], "Preserve the existing wire item ID.");
                Assert.AreEqual("e00d878160acb6b4eb4a02f7d8f9a279", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(inactive)));
                Assert.IsNull(new ItemDropTable(catalog).Roll(item => ReferenceEquals(item, inactive)));
                Assert.IsFalse(scene.isDirty);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
