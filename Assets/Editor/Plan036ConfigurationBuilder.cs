using System;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Solo.Configuration;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Additive T01 authoring helper. Never rewrites existing assets, scenes or build settings.
public static class Plan036ConfigurationBuilder
{
    public const string DraftPath = "Assets/Data/Solo/Encounters/DefaultSoloDuel.asset";
    public const string FixturePath = "Assets/Tests/Plan036Behavior/Configuration/FixtureSoloDuel.asset";
    const string RulePath = "Assets/Data/GameModeRules/OneVsOneRule.asset";
    const string RegistryPath = "Assets/Data/Cosmetics/CosmeticRegistry.asset";
    const string DraftStats = "Assets/Data/Solo/Stats/NeutralInherited.asset";
    const string DraftPolicy = "Assets/Data/Solo/ItemUse/DefaultItemUsePolicy.asset";
    const string DraftBot = "Assets/Data/Solo/Bots/DefaultBot.asset";
    const string DraftDifficulty = "Assets/Data/Solo/Difficulties/NeutralBaseline.asset";
    const string FixtureRoot = "Assets/Tests/Plan036Behavior/Configuration/";
    static readonly string[] Paths = { DraftStats, DraftPolicy, DraftBot, DraftDifficulty, DraftPath,
        FixtureRoot + "FixtureStats.asset", FixtureRoot + "FixtureItemUse.asset", FixtureRoot + "FixtureBot.asset",
        FixtureRoot + "FixtureDifficulty.asset", FixturePath };

    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        foreach (string path in Paths)
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
                throw new InvalidOperationException("Refusing to overwrite " + path + "; use ValidateSavedAssets for existing configurations.");
        var rule = AssetDatabase.LoadAssetAtPath<GameModeRuleSO>(RulePath);
        var graph = AssetDatabase.LoadAllAssetsAtPath(Plan036BehaviorFixtureBuilder.GraphPath).OfType<BehaviorGraph>().SingleOrDefault();
        if (rule == null || !SoloSettingsResolver.HasCompiledRoot(graph) || !SceneExists(Plan036BehaviorFixtureBuilder.ScenePath))
            throw new InvalidOperationException("T00's real graph/scene and shared 1v1 rule must exist first.");
        var catalog = ReadCurrentDuelCatalog();
        if (catalog.Length == 0) throw new InvalidOperationException("The existing duel item catalog is empty.");

        var stats = Create<BotStatProfileSO>(DraftStats, "solo.stats.neutral", null);
        var policy = Create<BotItemUsePolicySO>(DraftPolicy, "solo.items.default", null);
        var bot = Create<BotDefinitionSO>(DraftBot, "solo.bot.default", so =>
        {
            so.FindProperty("displayName").stringValue = "기본 봇";
            Set(so, "baseStats", stats); Set(so, "itemUsePolicy", policy);
            // No test graph masquerades as the future production bot graph.
        });
        var difficulty = Create<BotDifficultyProfileSO>(DraftDifficulty, "solo.difficulty.neutral", null);
        Create<SoloDuelDefinitionSO>(DraftPath, "solo.encounter.default", so =>
        {
            Set(so, "bot", bot); Set(so, "difficulty", difficulty); Set(so, "sharedOneVsOneRule", rule);
            so.FindProperty("gameplayScenePath").stringValue = "Assets/Scenes/GameScene_Solo.unity";
        });

        var fixtureStats = Create<BotStatProfileSO>(Paths[5], "test.solo.stats", null);
        var fixturePolicy = Create<BotItemUsePolicySO>(Paths[6], "test.solo.items", so =>
        {
            // Provisional fixture numbers, deliberately separate from unset draft balance.
            so.FindProperty("defaultDelaySeconds").floatValue = 0.5f;
            var entries = so.FindProperty("overrides"); entries.arraySize = 1;
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("Item").objectReferenceValue = catalog[0];
            entries.GetArrayElementAtIndex(0).FindPropertyRelative("DelaySeconds").floatValue = 0.75f;
        });
        var fixtureBot = Create<BotDefinitionSO>(Paths[7], "test.solo.bot", so =>
        {
            so.FindProperty("displayName").stringValue = "T01 validation bot";
            Set(so, "baseStats", fixtureStats); Set(so, "itemUsePolicy", fixturePolicy); Set(so, "graph", graph);
        });
        var fixtureDifficulty = Create<BotDifficultyProfileSO>(Paths[8], "test.solo.difficulty", so =>
        {
            so.FindProperty("minimumThinkSeconds").floatValue = 0.1f;
            so.FindProperty("maximumThinkSeconds").floatValue = 0.2f;
            so.FindProperty("retryBudget").intValue = 2;
        });
        Create<SoloDuelDefinitionSO>(FixturePath, "test.solo.encounter", so =>
        {
            so.FindProperty("readiness").intValue = (int)SoloConfigurationReadiness.ValidationFixture;
            Set(so, "bot", fixtureBot); Set(so, "difficulty", fixtureDifficulty); Set(so, "sharedOneVsOneRule", rule);
            so.FindProperty("gameplayScenePath").stringValue = Plan036BehaviorFixtureBuilder.ScenePath;
        });
        return ValidateSavedAssets();
    }

    public static string ValidateSavedAssets()
    {
        foreach (string path in Paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var catalog = ReadCurrentDuelCatalog();
        var context = new SoloConfigurationContext(AssetDatabase.LoadAssetAtPath<GameModeRuleSO>(RulePath), catalog,
            AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>(RegistryPath), SceneExists);
        var fixture = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(FixturePath);
        if (!SoloSettingsResolver.TryResolveForValidation(fixture, context, out var resolved, out var fixtureErrors))
            throw new InvalidOperationException("Fixture invalid: " + string.Join("; ", fixtureErrors));
        if (SoloSettingsResolver.TryResolve(fixture, context, out _, out _))
            throw new InvalidOperationException("Validation fixture was accepted as a normal encounter.");
        var draft = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(DraftPath);
        if (draft != null && draft.Readiness == SoloConfigurationReadiness.Ready)
            return Plan036BotGraphBuilder.Validate();
        if (draft == null || draft.Readiness != SoloConfigurationReadiness.Draft
            || SoloSettingsResolver.TryResolveForValidation(draft, context, out _, out var draftErrors)
            || !draftErrors.Any(error => error.Contains("Draft")))
            throw new InvalidOperationException("Default encounter must remain an explicitly nonlaunchable draft.");
        return "PASS: draft rejected; explicit fixture resolves real T00 graph/scene; current catalog="
            + catalog.Length + "; provisional item delay=" + resolved.ItemDelaySeconds[0] + ".";
    }

    static T Create<T>(string path, string id, Action<SerializedObject> configure) where T : SoloConfigurationSO
    {
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        var asset = ScriptableObject.CreateInstance<T>();
        var so = new SerializedObject(asset);
        so.FindProperty("configId").stringValue = id;
        configure?.Invoke(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static void Set(SerializedObject so, string field, UnityEngine.Object value) => so.FindProperty(field).objectReferenceValue = value;
    static bool SceneExists(string path) => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;

    public static ItemDataSO[] ReadCurrentDuelCatalog()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/GameScene.unity");
        try
        {
            var manager = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<ItemManager>(true)).Single();
            return (ItemDataSO[])manager.GetAllItems().Clone();
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
