using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Solo.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public class Plan036SoloConfigurationAssetTests
    {
        const string FixturePath = "Assets/Tests/Plan036Behavior/Configuration/FixtureSoloDuel.asset";
        const string DraftPath = "Assets/Data/Solo/Encounters/DefaultSoloDuel.asset";

        static SoloConfigurationContext Context(ItemDataSO[] catalog)
            => new(AssetDatabase.LoadAssetAtPath<GameModeRuleSO>("Assets/Data/GameModeRules/OneVsOneRule.asset"),
                catalog, AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset"),
                path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null);

        static ItemDataSO[] ReadCatalog()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
            try
            {
                return (ItemDataSO[])scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ItemManager>(true))
                    .Single().GetAllItems().Clone();
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SavedDefaultUsesProductionGraphAndDevelopmentTuningWithoutReleaseApproval()
        {
            var draft = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(DraftPath);
            Assert.NotNull(draft);
            Assert.AreEqual(SoloConfigurationReadiness.Ready, draft.Readiness);
            Assert.IsFalse(SoloSettingsResolver.TryResolve(draft, Context(ReadCatalog()), out _, out var errors));
            Assert.IsTrue(errors.Any(e => e.Contains("Provisional")));
            Assert.IsFalse(SoloSettingsResolver.TryResolveForValidation(draft, Context(ReadCatalog()), out _, out _));
            Assert.IsTrue(SoloSettingsResolver.TryResolveForDevelopment(draft, Context(ReadCatalog()), out var resolved, out errors), string.Join("; ", errors));
            Assert.IsFalse(resolved.IsValidationFixture);
            Assert.IsTrue(resolved.ProvisionalTuning);
            Assert.IsTrue(AssetDatabase.GetAssetPath(draft.Bot.Graph).StartsWith("Assets/Data/Solo/Graphs/"));
            Assert.IsTrue(SoloSettingsResolver.HasCompiledRoot(draft.Bot.Graph));
            var fixture = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(FixturePath);
            Assert.IsFalse(SoloSettingsResolver.TryResolveForDevelopment(fixture, Context(ReadCatalog()), out _, out _));
        }

        [Test]
        public void SavedFixtureResolvesCurrentCatalogWithoutChangingAnyReferencedSource()
        {
            var fixture = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(FixturePath);
            Assert.NotNull(fixture);
            var catalog = ReadCatalog();
            var sources = new List<UnityEngine.Object>(catalog)
            {
                fixture, fixture.Bot, fixture.Bot.BaseStats, fixture.Bot.ItemUsePolicy,
                fixture.Difficulty, fixture.SharedOneVsOneRule, fixture.Bot.Graph
            };
            var before = sources.ToDictionary(source => source, source => EditorJsonUtility.ToJson(source));
            Assert.IsTrue(SoloSettingsResolver.TryResolveForValidation(fixture, Context(catalog),
                out var resolved, out var errors), string.Join("; ", errors));
            Assert.AreEqual(catalog.Length, resolved.ItemDelaySeconds.Count);
            Assert.AreSame(fixture.SharedOneVsOneRule, resolved.SharedOneVsOneRule);
            Assert.AreEqual(37f, resolved.StartingTemperature);
            Assert.AreEqual(1f, resolved.FanBaseline);
            Assert.IsTrue(resolved.InheritOneVsOneGrants);
            Assert.IsFalse(SoloSettingsResolver.TryResolve(fixture, Context(catalog), out _, out _));
            foreach (var source in sources)
                Assert.AreEqual(before[source], EditorJsonUtility.ToJson(source), source.name);
        }

        [Test]
        public void SavedItemReferenceOverrideFollowsAChangedCatalogOrder()
        {
            var fixture = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(FixturePath);
            var catalog = ReadCatalog();
            Array.Reverse(catalog);
            Assert.IsTrue(SoloSettingsResolver.TryResolveForValidation(fixture, Context(catalog),
                out var resolved, out var errors), string.Join("; ", errors));
            for (short i = 0; i < catalog.Length; i++)
            {
                var match = fixture.Bot.ItemUsePolicy.Overrides.Where(entry => entry.Item == catalog[i]).ToArray();
                float expected = match.Length == 0 ? fixture.Bot.ItemUsePolicy.DefaultDelaySeconds : match.Single().DelaySeconds;
                Assert.IsTrue(resolved.TryGetItemDelay(i, out float actual));
                Assert.AreEqual(expected, actual, catalog[i].name);
            }
            Assert.IsFalse(resolved.TryGetItemDelay(-1, out _));
            Assert.IsFalse(resolved.TryGetItemDelay((short)catalog.Length, out _));
        }
    }
}
