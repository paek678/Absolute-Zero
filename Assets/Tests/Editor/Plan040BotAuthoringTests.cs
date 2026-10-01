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
using Object = UnityEngine.Object;

namespace AbsoluteZero.Tests
{
    public class Plan040BotAuthoringTests
    {
        SoloDuelDefinitionSO _source, _encounter;
        BotDefinitionSO _bot;
        BotDifficultyProfileSO _difficulty;
        BotStatProfileSO _stats;
        BotItemUsePolicySO _policy;
        ItemDataSO[] _catalog;
        readonly List<Object> _copies = new();

        [OneTimeSetUp]
        public void ReadSavedInputs()
        {
            _source = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>("Assets/Data/Solo/Encounters/DefaultSoloDuel.asset");
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/LobbyScene.unity");
            try
            {
                var ui = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<AbsoluteZero.UI.LobbyUI.AZLobbyUI>(true)).Single();
                using var data = new SerializedObject(ui);
                var items = data.FindProperty("_soloCatalog");
                _catalog = Enumerable.Range(0, items.arraySize).Select(i => items.GetArrayElementAtIndex(i).objectReferenceValue as ItemDataSO).ToArray();
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        T Copy<T>(T source) where T : Object { var result = Object.Instantiate(source); _copies.Add(result); return result; }
        static void Edit(Object source, string field, Action<SerializedProperty> edit)
        { using var data = new SerializedObject(source); edit(data.FindProperty(field)); data.ApplyModifiedPropertiesWithoutUndo(); }

        [SetUp]
        public void CopyInputs()
        {
            _encounter = Copy(_source); _bot = Copy(_source.Bot); _difficulty = Copy(_source.Difficulty);
            _stats = Copy(_source.Bot.BaseStats); _policy = Copy(_source.Bot.ItemUsePolicy);
            Edit(_encounter, "bot", p => p.objectReferenceValue = _bot);
            Edit(_encounter, "difficulty", p => p.objectReferenceValue = _difficulty);
            Edit(_bot, "baseStats", p => p.objectReferenceValue = _stats);
            Edit(_bot, "itemUsePolicy", p => p.objectReferenceValue = _policy);
        }

        [TearDown]
        public void DestroyCopies() { foreach (var copy in _copies) Object.DestroyImmediate(copy); _copies.Clear(); }

        SoloConfigurationContext Context(ItemDataSO[] catalog = null, Func<string, bool> sceneCheck = null)
            => new(_source.SharedOneVsOneRule, catalog ?? _catalog,
                AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset"),
                sceneCheck ?? (path => EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path) && AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null));

        [Test]
        public void DefaultPreviewEqualsLaunchSnapshotAndDoesNotMutateSources()
        {
            var sources = new List<Object>(_catalog) { _source, _source.Bot, _source.Bot.Graph, _source.Bot.BaseStats,
                _source.Bot.ItemUsePolicy, _source.Difficulty, _source.SharedOneVsOneRule };
            var before = sources.ToDictionary(o => o, o => EditorJsonUtility.ToJson(o));
            Assert.That(SoloSettingsResolver.TryResolveForDevelopment(_source, Context(), out var launch, out var errors), Is.True, string.Join(";", errors));
            var preview = SoloAuthoringPreview.Inspect(_source, Context());
            Assert.That(preview.Development.CanLaunch, Is.True);
            Assert.That(preview.Release.CanLaunch, Is.False);
            Assert.That(preview.Fixture.CanLaunch, Is.False);
            Assert.That(preview.PreviewSettings.StartingTemperature, Is.EqualTo(launch.StartingTemperature));
            Assert.That(preview.PreviewSettings.FanBaseline, Is.EqualTo(launch.FanBaseline));
            Assert.That(preview.PreviewSettings.InheritOneVsOneGrants, Is.True);
            CollectionAssert.AreEqual(launch.ItemDelaySeconds, preview.PreviewSettings.ItemDelaySeconds);
            CollectionAssert.AreEqual(launch.ConfigurationVersions.Select(s => s.Id + ":" + s.Version),
                preview.PreviewSettings.ConfigurationVersions.Select(s => s.Id + ":" + s.Version));
            Assert.That(preview.ItemTiming.Count, Is.EqualTo(_catalog.Length));
            foreach (var source in sources) Assert.That(EditorJsonUtility.ToJson(source), Is.EqualTo(before[source]), source.name);
        }

        [Test]
        public void FixtureCannotAppearAsNormalOrShippingReady()
        {
            var fixture = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>("Assets/Tests/Plan036Behavior/Configuration/FixtureSoloDuel.asset");
            var preview = SoloAuthoringPreview.Inspect(fixture, Context(sceneCheck: path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null));
            Assert.That(preview.Fixture.CanLaunch, Is.True, string.Join(";", preview.Fixture.Errors));
            Assert.That(preview.Development.CanLaunch || preview.Release.CanLaunch, Is.False);
            Assert.That(preview.PreviewSettings.IsValidationFixture, Is.True);
        }

        [TestCase("overrideStartingTemperature")]
        [TestCase("overrideFanBaseline")]
        [TestCase("overrideInitialItems")]
        public void UnapprovedStartOverrideIsBlockedInEveryRoute(string flag)
        {
            Edit(_stats, flag, p => p.boolValue = true);
            AssertBlocked("custom stats/loadouts");
        }

        [Test]
        public void InitialItemsWithoutOverrideFlagStillCannotBypassGuard()
        {
            Edit(_stats, "initialItems", p => { p.arraySize = 1; p.GetArrayElementAtIndex(0).objectReferenceValue = _catalog[0]; });
            AssertBlocked("custom stats/loadouts");
        }

        [TestCase("graph")]
        [TestCase("itemUsePolicy")]
        public void RequiredGraphOrPolicyCannotBeMissing(string field)
        {
            Edit(_bot, field, p => p.objectReferenceValue = null);
            AssertBlocked(field == "graph" ? "compiled root" : "item-use policy");
        }

        [Test]
        public void NullAndDuplicateCatalogReferencesRemainBlocked()
        {
            var broken = (ItemDataSO[])_catalog.Clone(); broken[0] = null;
            Assert.That(SoloAuthoringPreview.Inspect(_encounter, Context(broken)).Development.CanLaunch, Is.False);
            broken[0] = broken[1];
            Assert.That(SoloAuthoringPreview.Inspect(_encounter, Context(broken)).Development.Errors.Any(s => s.Contains("same item reference twice")), Is.True);
        }

        [Test]
        public void DisabledItemKeepsStableCatalogIndexButIsNotShownAsPlayable()
        {
            var preview = SoloAuthoringPreview.Inspect(_encounter, Context());
            int disabled = Array.FindIndex(_catalog, item => !ItemAvailability.IsEnabled(item));
            Assert.That(disabled, Is.GreaterThanOrEqualTo(0));
            Assert.That(preview.ItemTiming[disabled].Enabled, Is.False);
            Assert.That(preview.ItemTiming[disabled].CatalogIndex, Is.EqualTo(disabled));
            Assert.That(preview.ItemTiming[disabled].NeedsTimingReview, Is.False);
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidItemDelayIsAnErrorRatherThanATimingWarning(float delay)
        {
            Edit(_policy, "defaultDelaySeconds", p => p.floatValue = delay);
            AssertBlocked("default delay");
        }

        [Test]
        public void NominalTimeOverflowWarnsButPreservesRuntimeThinkingClamp()
        {
            Edit(_difficulty, "minimumThinkSeconds", p => p.floatValue = 50);
            Edit(_difficulty, "maximumThinkSeconds", p => p.floatValue = 60);
            var preview = SoloAuthoringPreview.Inspect(_encounter, Context());
            Assert.That(preview.Development.CanLaunch, Is.True);
            Assert.That(preview.ItemTiming.Where(r => r.Enabled).All(r => r.NeedsTimingReview), Is.True);
            Assert.That(preview.Warnings.Any(w => w.Contains("shorten thinking")), Is.True);
            Assert.That(_difficulty.MinimumThinkSeconds, Is.EqualTo(50), "Preview must not retune data.");
        }

        [Test]
        public void ReserveAtDeadlineIsRejected()
        {
            Edit(_difficulty, "readyReserveSeconds", p => p.floatValue = _source.SharedOneVsOneRule.PrepPhaseDuration);
            AssertBlocked("Ready reserve");
        }

        [Test]
        public void DuplicateItemOverridesAreRejected()
        {
            Edit(_policy, "overrides", p => { p.arraySize = 2; for (int i = 0; i < 2; i++) {
                p.GetArrayElementAtIndex(i).FindPropertyRelative("Item").objectReferenceValue = _catalog[0];
                p.GetArrayElementAtIndex(i).FindPropertyRelative("DelaySeconds").floatValue = 1; } });
            AssertBlocked("duplicate overrides");
        }

        [Test]
        public void EffectivePolicyOverrideIsPreviewedRatherThanTheBasePolicy()
        {
            var policy = Copy(_policy);
            Edit(policy, "configId", p => p.stringValue = "test.preview.override");
            Edit(policy, "overrides", p => p.arraySize = 0);
            Edit(policy, "defaultDelaySeconds", p => p.floatValue = 2.75f);
            Edit(_difficulty, "itemUseOverride", p => p.objectReferenceValue = policy);
            var preview = SoloAuthoringPreview.Inspect(_encounter, Context());
            Assert.That(preview.Development.CanLaunch, Is.True, string.Join(";", preview.Development.Errors));
            Assert.That(preview.ItemTiming.All(r => r.Delay == 2.75f), Is.True);
        }

        [Test]
        public void LibraryAmbiguityDistinguishesDuplicateAssetsFromSharedReferences()
        {
            var copy = Copy(_difficulty);
            Assert.That(SoloAuthoringPreview.FindDuplicateIds(new[] { _difficulty, _difficulty }), Is.Empty);
            Assert.That(SoloAuthoringPreview.FindDuplicateIds(new[] { _difficulty, copy }).Count, Is.EqualTo(1));
            Assert.That(SoloAuthoringPreview.FindDuplicateIds(new SoloConfigurationSO[] { null }).Count, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void IdentityDraftAndUnbuiltSceneRemainInvalid(int invalidCase)
        {
            if (invalidCase == 0) { Edit(_difficulty, "configId", p => p.stringValue = _encounter.ConfigId); AssertBlocked("share ID"); }
            if (invalidCase == 1) { Edit(_encounter, "readiness", p => p.enumValueIndex = (int)SoloConfigurationReadiness.Draft); AssertBlocked("Draft"); }
            if (invalidCase == 2) Assert.That(SoloAuthoringPreview.Inspect(_encounter, Context(sceneCheck: _ => false)).Development.CanLaunch, Is.False);
        }

        [Test]
        public void NullInputsProduceFailuresWithoutGuessingAProfile()
        {
            var preview = SoloAuthoringPreview.Inspect(null, null);
            Assert.That(preview.Development.CanLaunch || preview.Release.CanLaunch || preview.Fixture.CanLaunch, Is.False);
            Assert.That(preview.PreviewSettings, Is.Null);
            Assert.That(preview.ItemTiming, Is.Empty);
        }

        void AssertBlocked(string reason)
        {
            var report = SoloAuthoringPreview.Inspect(_encounter, Context());
            Assert.That(report.Development.CanLaunch || report.Release.CanLaunch || report.Fixture.CanLaunch, Is.False);
            Assert.That(report.Development.Errors.Any(e => e.Contains(reason)), Is.True, string.Join(";", report.Development.Errors));
            Assert.That(report.ItemTiming, Is.Empty);
        }
    }
}
