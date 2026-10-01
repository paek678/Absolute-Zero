using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.Validation.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Plan036PresentationFixtureBuilder
{
    const string Root = "Assets/Tests/Plan036Presentation";
    public const string Menu = Root + "/PresentationFixture.unity";
    public const string Match = Root + "/PresentationMatch.unity";
    const string Encounter = Root + "/PresentationEncounter.asset";
    const string Bot = Root + "/PresentationBot.asset";
    const string SourceScene = "Assets/Scenes/GameScene.unity";
    const string Player = "Assets/Prefabs/Player.prefab";

    // Explicit authoring only: no production scene, prefab, SO or build-list edits.
    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before creating the T04 fixture.");
        var previous = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(previous.path))
            throw new InvalidOperationException("Open a saved scene before creating the additive T04 fixture; no unsaved scene will be discarded.");
        var protectedPaths = new[] { SourceScene, Player, "ProjectSettings/EditorBuildSettings.asset",
            Plan036ConfigurationBuilder.FixturePath, "Assets/Data/GameModeRules/OneVsOneRule.asset" };
        var before = protectedPaths.Select(Hash).ToArray();
        EnsureFolder(Root);
        var source = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.FixturePath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Player);
        var cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
        if (source == null || prefab == null || cosmetics == null || cosmetics.GetById("hat_01") == null)
            throw new InvalidOperationException("T01 fixture, production player and hat_01 cosmetic are required.");

        // A power loss or Editor error may leave the earlier authoring stages saved.
        // Resume only this named fixture and reject unrelated data at the same paths.
        var bot = AssetDatabase.LoadAssetAtPath<BotDefinitionSO>(Bot);
        if (File.Exists(Bot) && (bot == null || bot.ConfigId != "test.solo.presentation.bot"))
            throw new InvalidOperationException("Unrecognized asset at fixture bot path: " + Bot);
        if (bot == null)
        {
            bot = UnityEngine.Object.Instantiate(source.Bot);
            var botFields = new SerializedObject(bot);
            botFields.FindProperty("configId").stringValue = "test.solo.presentation.bot";
            botFields.FindProperty("cosmetics").FindPropertyRelative("Head").stringValue = "hat_01";
            botFields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(bot, Bot);
        }
        var encounter = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Encounter);
        if (File.Exists(Encounter) && (encounter == null || encounter.ConfigId != "test.solo.presentation.encounter"
            || encounter.Bot != bot || encounter.GameplayScenePath != Match))
            throw new InvalidOperationException("Unrecognized asset at fixture encounter path: " + Encounter);
        if (encounter == null)
        {
            encounter = UnityEngine.Object.Instantiate(source);
            var fields = new SerializedObject(encounter);
            fields.FindProperty("configId").stringValue = "test.solo.presentation.encounter";
            fields.FindProperty("gameplayScenePath").stringValue = Match;
            fields.FindProperty("bot").objectReferenceValue = bot;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(encounter, Encounter);
        }

        if (!File.Exists(Match) && !AssetDatabase.CopyAsset(SourceScene, Match))
            throw new IOException("Cannot copy the existing duel scene.");
        var match = EditorSceneManager.OpenScene(Match, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(match);
            var components = match.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var composition = components.OfType<MatchCompositionRoot>().Single();
            var compositionFields = new SerializedObject(composition);
            compositionFields.FindProperty("allowHeadlessSoloValidation").boolValue = false;
            compositionFields.ApplyModifiedPropertiesWithoutUndo();
            foreach (var preview in components.Where(x => x != null && x.GetType().Name == "AnimationTestRunner"))
                UnityEngine.Object.DestroyImmediate(preview);
            var turn = components.OfType<TurnManager>().Single();
            var turnFields = new SerializedObject(turn);
            turnFields.FindProperty("prepDuration").floatValue = 600;
            turnFields.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(match)) throw new IOException(Match);
        }
        finally { EditorSceneManager.CloseScene(match, true); }

        bool newMenu = !File.Exists(Menu);
        var menu = newMenu ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive)
            : EditorSceneManager.OpenScene(Menu, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(menu);
            if (!newMenu)
            {
                var existing = menu.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Plan036PresentationProbe>(true)).Single();
                if (existing.Encounter != encounter || existing.PlayerPrefab != prefab || existing.Cosmetics != cosmetics
                    || existing.Spawner == null)
                    throw new InvalidOperationException("Existing T04 fixture menu is not the expected authored configuration.");
            }
            else
            {
            var spawner = new GameObject("Persistent Presentation Spawner").AddComponent<PlayerSpawnManager>();
            var spawnFields = new SerializedObject(spawner);
            spawnFields.FindProperty("playerPrefab").objectReferenceValue = prefab;
            spawnFields.ApplyModifiedPropertiesWithoutUndo();
            var owner = new GameObject("T04 Presentation Probe");
            var probe = owner.AddComponent<Plan036PresentationProbe>();
            var profile = owner.AddComponent<CosmeticProfileService>();
            var profileFields = new SerializedObject(profile);
            profileFields.FindProperty("_registry").objectReferenceValue = cosmetics;
            profileFields.ApplyModifiedPropertiesWithoutUndo();
            probe.Encounter = encounter;
            probe.Catalog = Plan036ConfigurationBuilder.ReadCurrentDuelCatalog();
            probe.Cosmetics = cosmetics;
            probe.PlayerPrefab = prefab;
            probe.Spawner = spawner;
            if (!EditorSceneManager.SaveScene(menu, Menu)) throw new IOException(Menu);
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(menu, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
        AssetDatabase.SaveAssets();
        for (int i = 0; i < protectedPaths.Length; i++)
            if (Hash(protectedPaths[i]) != before[i])
                throw new InvalidOperationException("Fixture creation changed a protected original: " + protectedPaths[i]);
        return "T04 fixture created from a copy of the existing duel scene; original scene, player, rule, configuration and build list preserved.";
    }

    static string Hash(string path)
    {
        using var algorithm = SHA256.Create();
        return BitConverter.ToString(algorithm.ComputeHash(File.ReadAllBytes(path)));
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static string BuildPlayer()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Menu, Match },
            locationPathName = Path.Combine(Path.GetTempPath(), "AZVisual4P_Current", "Player", "AbsoluteZeroVisual4P.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.DetailedBuildReport
        });
        string result = $"{report.summary.result}; errors={report.summary.totalErrors}; warnings={report.summary.totalWarnings}";
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException(result);
        return result;
    }
}
