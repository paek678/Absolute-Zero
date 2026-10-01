using System;
using System.IO;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.Validation.Solo;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Plan036ParticipantFixtureBuilder
{
    const string Root = "Assets/Tests/Plan036Session/";
    public const string Menu = Root + "ParticipantFixture.unity";
    static readonly string[] Matches = { Root + "ParticipantMatchA.unity", Root + "ParticipantMatchB.unity" };
    internal const string Prefab = Root + "ParticipantPlayer.prefab";

    // NGO auto-registers imported NetworkObject prefabs. This fixture registers its
    // prefab explicitly at runtime and must never become a production dependency.
    public static void IsolateFixturePrefab()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(
            Unity.Netcode.Editor.Configuration.NetworkPrefabProcessor.DefaultNetworkPrefabsPath);
        if (list == null) return;
        bool changed = false;
        for (int i = list.PrefabList.Count - 1; i >= 0; i--)
        {
            var entry = list.PrefabList[i];
            if (AssetDatabase.GetAssetPath(entry.Prefab) != Prefab) continue;
            list.Remove(entry);
            changed = true;
        }
        if (!changed) return;
        EditorUtility.SetDirty(list);
        AssetDatabase.SaveAssetIfDirty(list);
    }

    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first");
        if (File.Exists(Menu) || File.Exists(Prefab)) throw new InvalidOperationException("Participant fixtures already exist");
        var previous = SceneManager.GetActiveScene();
        var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(temp);
        var go = new GameObject("ParticipantPlayer");
        go.AddComponent<NetworkObject>();
        go.AddComponent<PlayerInventory>();
        go.AddComponent<PlayerState>();
        var player = PrefabUtility.SaveAsPrefabAsset(go, Prefab);
        UnityEngine.Object.DestroyImmediate(go);
        EditorSceneManager.CloseScene(temp, true);
        var source = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.FixturePath);
        var encounters = new SoloDuelDefinitionSO[2];
        var catalog = Plan036ConfigurationBuilder.ReadCurrentDuelCatalog();
        for (int i = 0; i < 2; i++)
        {
            var encounter = UnityEngine.Object.Instantiate(source);
            var serialized = new SerializedObject(encounter);
            serialized.FindProperty("configId").stringValue = "test.solo.participants." + i;
            serialized.FindProperty("gameplayScenePath").stringValue = Matches[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(encounter, Root + "ParticipantEncounter" + i + ".asset");
            encounters[i] = encounter;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var services = new GameObject("Match Services");
                services.AddComponent<NetworkObject>();
                services.AddComponent<MatchNetworkState>();
                // Deliberately precedes ItemManager: discovery must await its initialization.
                services.AddComponent<TurnManager>();
                var items = services.AddComponent<ItemManager>();
                services.AddComponent<MatchManager>();
                var itemFields = new SerializedObject(items);
                var itemArray = itemFields.FindProperty("allItems");
                itemArray.arraySize = catalog.Length;
                for (int j = 0; j < catalog.Length; j++) itemArray.GetArrayElementAtIndex(j).objectReferenceValue = catalog[j];
                itemFields.ApplyModifiedPropertiesWithoutUndo();
                var composition = new GameObject("Match Composition").AddComponent<MatchCompositionRoot>();
                var fields = new SerializedObject(composition);
                fields.FindProperty("allowHeadlessSoloValidation").boolValue = true;
                fields.FindProperty("gameModeRules").arraySize = 2;
                fields.FindProperty("gameModeRules").GetArrayElementAtIndex(0).objectReferenceValue = source.SharedOneVsOneRule;
                fields.FindProperty("gameModeRules").GetArrayElementAtIndex(1).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<GameModeRuleSO>("Assets/Data/GameModeRules/MultiRule.asset");
                fields.ApplyModifiedPropertiesWithoutUndo();
                for (int seat = 0; seat < 2; seat++)
                {
                    var marker = new GameObject("Seat " + seat).AddComponent<PlayerSpawnPoint3D>();
                    marker.transform.position = new Vector3(100 * i + 10 * seat, 1 + i, 20 + i);
                    var markerFields = new SerializedObject(marker);
                    markerFields.FindProperty("order").intValue = seat;
                    markerFields.ApplyModifiedPropertiesWithoutUndo();
                }
                if (!EditorSceneManager.SaveScene(scene, Matches[i])) throw new IOException(Matches[i]);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        var menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(menu);
        try
        {
            var spawner = new GameObject("Persistent Player Spawner").AddComponent<PlayerSpawnManager>();
            var fields = new SerializedObject(spawner);
            fields.FindProperty("playerPrefab").objectReferenceValue = player;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var probe = new GameObject("Participant Probe").AddComponent<Plan036ParticipantProbe>();
            probe.Encounters = encounters;
            probe.Catalog = catalog;
            probe.PlayerPrefab = player;
            probe.Spawner = spawner;
            probe.Cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
            if (!EditorSceneManager.SaveScene(menu, Menu)) throw new IOException(Menu);
        }
        finally { EditorSceneManager.CloseScene(menu, true); }
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        IsolateFixturePrefab();
        AssetDatabase.SaveAssets();
        return "Participant prefab, two marker-distinct match scenes and isolated menu saved; production build scenes unchanged.";
    }

    public static string BuildPlayer()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Menu, Matches[0], Matches[1] },
            locationPathName = Path.Combine(Path.GetTempPath(), "AZVisual4P_Current", "Player", "AbsoluteZeroVisual4P.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.DetailedBuildReport
        });
        var s = report.summary;
        string result = $"{s.result}; errors={s.totalErrors}; warnings={s.totalWarnings}";
        if (s.result != BuildResult.Succeeded) throw new InvalidOperationException(result);
        return result;
    }
}

[InitializeOnLoad]
public sealed class Plan036ParticipantFixtureIsolation : AssetPostprocessor, IPreprocessBuildWithReport
{
    static Plan036ParticipantFixtureIsolation()
    {
        EditorApplication.delayCall += Plan036ParticipantFixtureBuilder.IsolateFixturePrefab;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                Plan036ParticipantFixtureBuilder.IsolateFixturePrefab();
        };
    }

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report) => Plan036ParticipantFixtureBuilder.IsolateFixturePrefab();

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (Array.IndexOf(imported, Plan036ParticipantFixtureBuilder.Prefab) < 0 &&
            Array.IndexOf(imported, Unity.Netcode.Editor.Configuration.NetworkPrefabProcessor.DefaultNetworkPrefabsPath) < 0 &&
            Array.IndexOf(moved, Plan036ParticipantFixtureBuilder.Prefab) < 0) return;
        // Run after NGO's own asset postprocessor, outside the import callback.
        EditorApplication.delayCall -= Plan036ParticipantFixtureBuilder.IsolateFixturePrefab;
        EditorApplication.delayCall += Plan036ParticipantFixtureBuilder.IsolateFixturePrefab;
    }
}
