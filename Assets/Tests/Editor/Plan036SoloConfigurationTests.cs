using System;
using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Solo.Configuration;
using NUnit.Framework;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AbsoluteZero.Tests
{
    public class Plan036SoloConfigurationTests
    {
        const string GraphPath = "Assets/Tests/Plan036Behavior/Plan036BehaviorFixture.asset";
        const string ScenePath = "Assets/Tests/Plan036Behavior/Plan036BehaviorFixture.unity";
        readonly List<Object> _created = new();
        SoloDuelDefinitionSO _encounter;
        BotDefinitionSO _bot;
        BotDifficultyProfileSO _difficulty;
        BotStatProfileSO _stats;
        BotItemUsePolicySO _policy;
        GameModeRuleSO _rule;
        BehaviorGraph _graph;
        CosmeticRegistrySO _cosmetics;
        ItemDataSO[] _catalog;

        [SetUp]
        public void SetUp()
        {
            _rule = AssetDatabase.LoadAssetAtPath<GameModeRuleSO>("Assets/Data/GameModeRules/OneVsOneRule.asset");
            _graph = AssetDatabase.LoadAllAssetsAtPath(GraphPath).OfType<BehaviorGraph>().Single();
            _cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            _catalog = new[]
            {
                AssetDatabase.LoadAssetAtPath<ItemDataSO>("Assets/Data/Items/Basic/Fan.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDataSO>("Assets/Data/Items/Basic/WarmTea.asset"),
                AssetDatabase.LoadAssetAtPath<ItemDataSO>("Assets/Data/Items/Random/Attack/WaterGun.asset")
            };
            Assert.That(_rule, Is.Not.Null); Assert.That(_catalog.All(item => item != null), Is.True);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
            _stats = Make<BotStatProfileSO>("test.stats");
            _policy = Make<BotItemUsePolicySO>("test.items"); Set(_policy, "defaultDelaySeconds", 0.5f);
            _difficulty = Make<BotDifficultyProfileSO>("test.difficulty");
            Set(_difficulty, "minimumThinkSeconds", 0.1f); Set(_difficulty, "maximumThinkSeconds", 0.2f);
            Set(_difficulty, "retryBudget", 2);
            _bot = Make<BotDefinitionSO>("test.bot");
            Set(_bot, "displayName", "Configuration test"); Set(_bot, "graph", _graph);
            Set(_bot, "baseStats", _stats); Set(_bot, "itemUsePolicy", _policy);
            _encounter = Make<SoloDuelDefinitionSO>("test.encounter");
            Set(_encounter, "readiness", (int)SoloConfigurationReadiness.ValidationFixture);
            Set(_encounter, "bot", _bot); Set(_encounter, "difficulty", _difficulty);
            Set(_encounter, "sharedOneVsOneRule", _rule); Set(_encounter, "gameplayScenePath", ScenePath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _created) Object.DestroyImmediate(value);
            _created.Clear();
        }

        [Test]
        public void NeutralFixtureInheritsLiveTemperatureFanAndExistingGrantOwner()
        {
            var settings = Resolve();
            Assert.That(settings.StartingTemperature, Is.EqualTo(TemperatureSystem.MAX_TEMP));
            Assert.That(settings.FanBaseline, Is.EqualTo(TemperatureSystem.DEFAULT_FAN_SPEED));
            Assert.That(settings.InheritOneVsOneGrants, Is.True);
            Assert.That(settings.SharedOneVsOneRule, Is.SameAs(_rule));
            Assert.That(settings.Graph, Is.SameAs(_graph));
            Assert.That(settings.RuleSnapshot.InitialRandomItems, Is.EqualTo(_rule.InitialRandomItems));
            Assert.That(settings.IsValidationFixture && settings.ProvisionalTuning, Is.True);
            Assert.That(settings.ConfigurationVersions.Count, Is.EqualTo(5));
            Assert.That(settings.ConfigurationVersions.All(stamp => stamp.Version == 1), Is.True);
            Assert.That(settings.TryGetItemDelay(0, out float delay), Is.True); Assert.That(delay, Is.EqualTo(0.5f));
            Assert.That(settings.TryGetItemDelay(-1, out _), Is.False);
            Assert.That(settings.TryGetItemDelay(3, out _), Is.False);
        }

        [Test]
        public void MissingOptionalStatProfileStillInheritsOneVsOne()
        {
            Set(_bot, "baseStats", null);
            Assert.That(Resolve().InheritOneVsOneGrants, Is.True);
        }

        [Test]
        public void DelayOverridesFollowItemReferenceWhenCatalogOrderChanges()
        {
            Overrides(_policy, (_catalog[0], 1.25f));
            ItemDataSO fan = _catalog[0];
            _catalog = new[] { _catalog[2], _catalog[1], fan };
            var settings = Resolve();
            Assert.That(settings.ItemDelaySeconds, Is.EqualTo(new[] { 0.5f, 0.5f, 1.25f }));
            Assert.That(settings.Catalog[2], Is.SameAs(fan));
        }

        [Test]
        public void DifficultyOverridesAreExplicitAndUnsetValuesInheritBotDefaults()
        {
            Set(_bot, "tacticalDefaults.Survival", 4f);
            Assert.That(Resolve().TacticalWeights.Survival, Is.EqualTo(4));
            var overridePolicy = Make<BotItemUsePolicySO>("test.override.items");
            Set(overridePolicy, "defaultDelaySeconds", 2f);
            Set(_difficulty, "itemUseOverride", overridePolicy);
            Set(_difficulty, "overrideTacticalWeights", true); Set(_difficulty, "tacticalWeights.Survival", 7f);
            var resolved = Resolve();
            Assert.That(resolved.ItemDelaySeconds, Is.EqualTo(new[] { 2f, 2f, 2f }));
            Assert.That(resolved.TacticalWeights.Survival, Is.EqualTo(7));
        }

        [Test]
        public void ResolutionDoesNotWriteAnySourceConfigurationRuleGraphOrItem()
        {
            Object[] sources = _created.Concat(new Object[] { _rule, _graph, _cosmetics }).Concat(_catalog).ToArray();
            string[] before = sources.Select(value => EditorJsonUtility.ToJson(value)).ToArray();
            Resolve(); Resolve();
            Assert.That(sources.Select(value => EditorJsonUtility.ToJson(value)).ToArray(), Is.EqualTo(before));
            Assert.That(_graph.IsRunning, Is.False);
        }

        [Test]
        public void SnapshotDefensivelyCopiesCatalogDelaysIdentityWeightsAndRuleScalars()
        {
            var ruleClone = Object.Instantiate(_rule); _created.Add(ruleClone); _rule = ruleClone;
            Set(_encounter, "sharedOneVsOneRule", _rule);
            Overrides(_policy, (_catalog[0], 1.25f));
            ItemDataSO originalItem = _catalog[0];
            var resolved = Resolve();
            _catalog[0] = _catalog[1];
            Set(_policy, "defaultDelaySeconds", 9f); Overrides(_policy, (_catalog[1], 8f));
            Set(_difficulty, "minimumThinkSeconds", 5f); Set(_difficulty, "retryBudget", 8);
            Set(_bot, "configId", "changed.bot"); Set(_bot, "tacticalDefaults.Survival", 12f);
            Set(_rule, "prepPhaseDuration", 99f);
            Assert.That(resolved.Catalog[0], Is.SameAs(originalItem));
            Assert.That(resolved.ItemDelaySeconds, Is.EqualTo(new[] { 1.25f, 0.5f, 0.5f }));
            Assert.That(resolved.MinimumThinkSeconds, Is.EqualTo(0.1f));
            Assert.That(resolved.RetryBudget, Is.EqualTo(2)); Assert.That(resolved.BotId, Is.EqualTo("test.bot"));
            Assert.That(resolved.TacticalWeights.Survival, Is.EqualTo(1));
            Assert.That(resolved.RuleSnapshot.PrepPhaseDuration, Is.EqualTo(20));
            Assert.Throws<NotSupportedException>(() => ((IList<float>)resolved.ItemDelaySeconds)[0] = 8f);
            Assert.Throws<NotSupportedException>(() => ((IList<ItemDataSO>)resolved.Catalog)[0] = _catalog[1]);
            Assert.Throws<NotSupportedException>(() => ((IList<SoloConfigurationStamp>)resolved.ConfigurationVersions).Clear());
        }

        [Test]
        public void DraftAndFixtureNeverPassNormalLaunchAndDraftCannotUseValidationBypass()
        {
            Assert.That(SoloSettingsResolver.TryResolve(_encounter, Context(), out var rejected, out _), Is.False);
            Assert.That(rejected, Is.Null);
            Set(_encounter, "readiness", (int)SoloConfigurationReadiness.Draft);
            Reject("Draft");
            Assert.That(SoloSettingsResolver.TryResolve(_encounter, Context(), out _, out _), Is.False);
        }

        [Test]
        public void ReadyStillRejectsProvisionalTuningAndUsesStrictSceneValidation()
        {
            Set(_encounter, "readiness", (int)SoloConfigurationReadiness.Ready);
            Assert.That(SoloSettingsResolver.TryResolve(_encounter, Context(), out _, out var errors), Is.False);
            Assert.That(errors.Any(error => error.Contains("Provisional")), Is.True);
            Set(_difficulty, "provisionalTuning", false); Set(_policy, "provisionalTuning", false);
            Assert.That(SoloSettingsResolver.TryResolve(_encounter, Context(), out _, out errors), Is.True, string.Join("; ", errors));
            var unavailable = new SoloConfigurationContext(_rule, _catalog, _cosmetics, _ => false);
            Assert.That(SoloSettingsResolver.TryResolve(_encounter, unavailable, out _, out errors), Is.False);
            Assert.That(errors.Any(error => error.Contains("not loadable")), Is.True);
        }

        [TestCase("bot")]
        [TestCase("difficulty")]
        [TestCase("sharedOneVsOneRule")]
        public void MissingRequiredEncounterReferencesAreRejected(string field)
        { Set(_encounter, field, null); Reject(); }

        [TestCase("graph")]
        [TestCase("itemUsePolicy")]
        public void MissingRequiredBotReferencesAreRejected(string field)
        { Set(_bot, field, null); Reject(); }

        [Test]
        public void CopiedRuleCannotReplaceSharedRuleReference()
        {
            var copy = Object.Instantiate(_rule); _created.Add(copy); Set(_encounter, "sharedOneVsOneRule", copy);
            Reject("shared OneVsOne");
        }

        [TestCase("")]
        [TestCase("GameScene")]
        [TestCase("Assets/Missing.unity")]
        [TestCase("Assets/../Scenes/GameScene.unity")]
        public void InvalidOrMissingSceneIsRejected(string path)
        { Set(_encounter, "gameplayScenePath", path); Reject("scene"); }

        [Test]
        public void DefaultScenePredicateUsesRuntimeBuildLoadability()
        {
            var context = new SoloConfigurationContext(_rule, _catalog, _cosmetics);
            Assert.That(context.IsSceneAvailable(ScenePath), Is.EqualTo(Application.CanStreamedLevelBeLoaded(ScenePath)));
        }

        [Test]
        public void CompiledGraphCheckRejectsAnEmptyGraphMissingRootAndPlaceholder()
        {
            Assert.That(SoloSettingsResolver.HasCompiledRoot(_graph), Is.True, "T00 graph must pass public property-bag traversal.");
            var empty = ScriptableObject.CreateInstance<BehaviorGraph>(); _created.Add(empty); Set(_bot, "graph", empty);
            Reject("compiled root");
            var missingRoot = Object.Instantiate(_graph); _created.Add(missingRoot);
            var so = new SerializedObject(missingRoot);
            so.FindProperty("Graphs").GetArrayElementAtIndex(0).FindPropertyRelative("Root").managedReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo(); Set(_bot, "graph", missingRoot); Reject("compiled root");
            var placeholder = Object.Instantiate(_graph); _created.Add(placeholder);
            Set(placeholder, "m_WasCompileWithPlaceholderNode", true); Set(_bot, "graph", placeholder); Reject("compiled root");
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDefaultOrOverrideDelayNeverFallsBackToInstantUse(float delay)
        {
            Set(_policy, "defaultDelaySeconds", delay); Reject("delay");
            Set(_policy, "defaultDelaySeconds", 0.5f); Overrides(_policy, (_catalog[0], delay)); Reject("delay");
        }

        [Test]
        public void DuplicateNullAndForeignItemOverridesAreRejected()
        {
            Overrides(_policy, (_catalog[0], 1f), (_catalog[0], 2f)); Reject("duplicate");
            Overrides(_policy, (null, 1f)); Reject("catalog");
            var foreign = Object.Instantiate(_catalog[0]); _created.Add(foreign);
            // Same name/data is not the same catalog asset.
            Overrides(_policy, (foreign, 1f)); Reject("catalog");
        }

        [Test]
        public void EmptyNullEntryAndAmbiguousCatalogAreRejected()
        {
            _catalog = Array.Empty<ItemDataSO>(); Reject("catalog");
            _catalog = new ItemDataSO[] { null }; Reject("null entry");
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>("Assets/Data/Items/Basic/Fan.asset");
            _catalog = new[] { item, item }; Reject("same item reference");
        }

        [TestCase("overrideStartingTemperature")]
        [TestCase("overrideFanBaseline")]
        [TestCase("overrideInitialItems")]
        public void EvenNeutralExplicitStatOverridesAreDisabled(string field)
        { Set(_stats, field, true); Reject("disabled"); }

        [Test]
        public void NonEmptyLoadoutCannotHideBehindDisabledFlag()
        {
            var so = new SerializedObject(_stats); var items = so.FindProperty("initialItems"); items.arraySize = 1;
            items.GetArrayElementAtIndex(0).objectReferenceValue = _catalog[0]; so.ApplyModifiedPropertiesWithoutUndo();
            Reject("disabled");
        }

        [TestCase("startingTemperature", float.NaN)]
        [TestCase("startingTemperature", float.PositiveInfinity)]
        [TestCase("startingTemperature", 0f)]
        [TestCase("startingTemperature", 38f)]
        [TestCase("fanBaseline", -1f)]
        [TestCase("fanBaseline", float.NaN)]
        public void InvalidStatValuesAreRejectedEvenWhenDormant(string field, float value)
        { Set(_stats, field, value); Reject(); }

        [TestCase("minimumThinkSeconds", -1f)]
        [TestCase("minimumThinkSeconds", float.NaN)]
        [TestCase("maximumThinkSeconds", float.PositiveInfinity)]
        [TestCase("maximumThinkSeconds", 0f)]
        [TestCase("decisionNoise", -0.1f)]
        [TestCase("decisionNoise", 1.1f)]
        [TestCase("decisionNoise", float.NaN)]
        [TestCase("readyReserveSeconds", -1f)]
        [TestCase("readyReserveSeconds", 20f)]
        [TestCase("readyReserveSeconds", float.PositiveInfinity)]
        [TestCase("tacticalWeights.General", float.NaN)]
        [TestCase("tacticalWeights.Survival", -1f)]
        public void InvalidDifficultyValuesAreRejected(string field, float value)
        { Set(_difficulty, field, value); Reject(); }

        [TestCase(-1)]
        [TestCase(33)]
        public void RetryBudgetIsBounded(int value)
        { Set(_difficulty, "retryBudget", value); Reject("Retry"); }

        [Test]
        public void UndefinedEnumsAndZeroTacticalWeightsAreRejected()
        {
            Set(_difficulty, "readyPolicy", 99); Reject("undefined"); Set(_difficulty, "readyPolicy", 0);
            Set(_encounter, "readiness", 99); Reject("undefined");
            Set(_encounter, "readiness", (int)SoloConfigurationReadiness.ValidationFixture);
            foreach (string field in new[] { "Survival", "Finish", "Counter", "Resource", "General" })
                Set(_bot, "tacticalDefaults." + field, 0f);
            Reject("positive total");
        }

        [TestCase("")]
        [TestCase("Not Stable")]
        [TestCase("test.items")]
        public void MissingMalformedAndDuplicateConfigurationIdsAreRejected(string id)
        { Set(_bot, "configId", id); Reject("ID"); }

        [Test]
        public void ConfigurationVersionsMustBePositive()
        { Set(_policy, "version", 0); Reject("version"); }

        [Test]
        public void CosmeticsMustBeRegisteredAndMatchTheirExistingPart()
        {
            Set(_bot, "cosmetics.Head", "hat_01"); Resolve();
            Set(_bot, "cosmetics.Head", "top_ref"); Reject("cosmetic");
            Set(_bot, "cosmetics.Head", "missing"); Reject("cosmetic");
        }

        public sealed class UnsupportedTestItem : ItemDataSO { }
        [Test]
        public void UnsupportedItemTypeFailsBeforeStartingItsBehaviorGraph()
        {
            var item = ScriptableObject.CreateInstance<UnsupportedTestItem>(); _created.Add(item);
            _catalog = _catalog.Concat(new ItemDataSO[] { item }).ToArray();
            Reject("unsupported effect");
        }
        [Test]
        public void RealDuelSceneCatalogResolvesWithoutEditingSceneOrBuildList()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
            try
            {
                _catalog = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ItemManager>(true))
                    .Single().GetAllItems();
                var resolved = Resolve();
                Assert.That(resolved.Catalog.Count, Is.EqualTo(_catalog.Length));
                Assert.That(resolved.Catalog.Count, Is.GreaterThan(0));
                Assert.That(resolved.Catalog, Is.EqualTo(_catalog));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        T Make<T>(string id) where T : SoloConfigurationSO
        {
            var asset = ScriptableObject.CreateInstance<T>(); _created.Add(asset); Set(asset, "configId", id); return asset;
        }
        SoloConfigurationContext Context() => new(_rule, _catalog, _cosmetics,
            path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null);
        ResolvedSoloSettings Resolve()
        {
            bool ok = SoloSettingsResolver.TryResolveForValidation(_encounter, Context(), out var settings, out var errors);
            Assert.That(ok, Is.True, string.Join("; ", errors)); return settings;
        }
        void Reject(string contains = null)
        {
            Assert.That(SoloSettingsResolver.TryResolveForValidation(_encounter, Context(), out var settings, out var errors), Is.False);
            Assert.That(settings, Is.Null); Assert.That(errors.Count, Is.GreaterThan(0));
            if (contains != null) Assert.That(errors.Any(error => error.Contains(contains)), Is.True, string.Join("; ", errors));
        }
        static void Set(Object target, string path, object value)
        {
            var so = new SerializedObject(target); var field = so.FindProperty(path);
            Assert.That(field, Is.Not.Null, path);
            if (field.propertyType == SerializedPropertyType.ObjectReference) field.objectReferenceValue = (Object)value;
            else if (value is float number) field.floatValue = number;
            else if (value is int integer) field.intValue = integer;
            else if (value is bool flag) field.boolValue = flag;
            else if (value is string text) field.stringValue = text;
            else throw new ArgumentException(path);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Overrides(BotItemUsePolicySO policy, params (ItemDataSO item, float delay)[] entries)
        {
            var so = new SerializedObject(policy); var array = so.FindProperty("overrides"); array.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                array.GetArrayElementAtIndex(i).FindPropertyRelative("Item").objectReferenceValue = entries[i].item;
                array.GetArrayElementAtIndex(i).FindPropertyRelative("DelaySeconds").floatValue = entries[i].delay;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
