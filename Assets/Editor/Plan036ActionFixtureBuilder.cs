using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.Validation.Actions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Plan036ActionFixtureBuilder
{
    const string Root = "Assets/Tests/Plan036Actions";
    public const string Menu = Root + "/ActionFixture.unity";
    public const string Match = Root + "/ActionMatch.unity";
    const string Encounter = Root + "/ActionEncounter.asset";
    const string SourceMatch = "Assets/Tests/Plan036Session/ParticipantMatchA.unity";
    const string SourceEncounter = "Assets/Tests/Plan036Session/ParticipantEncounter0.asset";
    const string Player = "Assets/Tests/Plan036Session/ParticipantPlayer.prefab";

    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before authoring the action fixture.");
        var previous = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(previous.path))
            throw new InvalidOperationException("Open a saved scene first; the fixture does not discard an unsaved scene.");
        string[] protectedPaths = { SourceMatch, SourceEncounter, Player,
            "Assets/Scenes/GameScene.unity", "Assets/Prefabs/Player.prefab",
            "Assets/Data/GameModeRules/OneVsOneRule.asset", "ProjectSettings/EditorBuildSettings.asset" };
        var before = protectedPaths.Select(Hash).ToArray();
        EnsureFolder(Root);
        var source = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(SourceEncounter);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Player);
        var cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
        if (source == null || prefab == null || cosmetics == null)
            throw new InvalidOperationException("T03 fixture assets and the current cosmetic registry are required.");

        var encounter = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Encounter);
        if (File.Exists(Encounter) && (encounter == null || encounter.ConfigId != "test.solo.actions"
            || encounter.GameplayScenePath != Match || encounter.SharedOneVsOneRule != source.SharedOneVsOneRule))
            throw new InvalidOperationException("Unrecognized data at the action encounter fixture path.");
        if (encounter == null)
        {
            encounter = UnityEngine.Object.Instantiate(source);
            var fields = new SerializedObject(encounter);
            fields.FindProperty("configId").stringValue = "test.solo.actions";
            fields.FindProperty("gameplayScenePath").stringValue = Match;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(encounter, Encounter);
        }

        try
        {
            if (!File.Exists(Match) && !AssetDatabase.CopyAsset(SourceMatch, Match))
                throw new IOException("Cannot copy the T03 minimal match fixture.");
            var match = EditorSceneManager.OpenScene(Match, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(match);
                var components = match.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                var composition = components.OfType<MatchCompositionRoot>().Single();
                var compositionFields = new SerializedObject(composition);
                compositionFields.FindProperty("allowHeadlessSoloValidation").boolValue = true;
                compositionFields.ApplyModifiedPropertiesWithoutUndo();
                var turn = components.OfType<TurnManager>().Single();
                var turnFields = new SerializedObject(turn);
                turnFields.FindProperty("prepDuration").floatValue = 30;
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
                if (newMenu)
                {
                    var spawner = new GameObject("Persistent Action Fixture Spawner").AddComponent<PlayerSpawnManager>();
                    var fields = new SerializedObject(spawner);
                    fields.FindProperty("playerPrefab").objectReferenceValue = prefab;
                    fields.ApplyModifiedPropertiesWithoutUndo();
                    var probe = new GameObject("T05 Action Probe").AddComponent<Plan036ActionProbe>();
                    probe.Encounter = encounter;
                    probe.Catalog = Plan036ConfigurationBuilder.ReadCurrentDuelCatalog();
                    probe.Cosmetics = cosmetics;
                    probe.PlayerPrefab = prefab;
                    probe.Spawner = spawner;
                    if (!EditorSceneManager.SaveScene(menu, Menu)) throw new IOException(Menu);
                }
                else
                {
                    var probe = menu.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Plan036ActionProbe>(true)).Single();
                    if (probe.Encounter != encounter || probe.PlayerPrefab != prefab || probe.Cosmetics != cosmetics
                        || probe.Spawner == null || !probe.Catalog.SequenceEqual(Plan036ConfigurationBuilder.ReadCurrentDuelCatalog()))
                        throw new InvalidOperationException("Existing action menu does not match the expected fixture.");
                }
            }
            finally { EditorSceneManager.CloseScene(menu, true); }
            AssetDatabase.SaveAssets();
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            for (int i = 0; i < protectedPaths.Length; i++)
                if (Hash(protectedPaths[i]) != before[i])
                    throw new InvalidOperationException("Fixture authoring changed a protected original: " + protectedPaths[i]);
        }
        return "T05 action fixture saved/resumed using a copy of the minimal T03 match and its existing player; original assets and production build list preserved.";
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
