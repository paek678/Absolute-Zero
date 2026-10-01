using System;
using System.IO;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Validation.Solo;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Plan036SessionFixtureBuilder
{
    public const string Bootstrap = "Assets/Tests/Plan036Session/SessionFixture.unity";
    public const string Target = "Assets/Tests/Plan036Session/EmptyMatchFixture.unity";
    public const string Encounter = "Assets/Tests/Plan036Session/SessionEncounter.asset";

    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        foreach (var path in new[] { Bootstrap, Target, Encounter })
            if (File.Exists(path)) throw new InvalidOperationException("Fixture already exists: " + path);
        var source = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.FixturePath);
        var encounter = UnityEngine.Object.Instantiate(source);
        var so = new SerializedObject(encounter);
        so.FindProperty("configId").stringValue = "test.solo.session";
        so.FindProperty("gameplayScenePath").stringValue = Target;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(encounter, Encounter);
        var previous = SceneManager.GetActiveScene();
        foreach (var path in new[] { Bootstrap, Target })
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var camera = new GameObject("Fixture Camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.1f, 0.12f, 0.15f);
                if (path == Bootstrap)
                {
                    var go = new GameObject("Plan036 Session Probe");
                    SceneManager.MoveGameObjectToScene(go, scene);
                    var probe = go.AddComponent<Plan036SessionProbe>();
                    probe.Encounter = encounter;
                    probe.Catalog = Plan036ConfigurationBuilder.ReadCurrentDuelCatalog();
                    probe.Cosmetics = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset");
                }
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save " + path);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        AssetDatabase.SaveAssets();
        return "Created isolated bootstrap and empty match fixture; production build scene list unchanged.";
    }

    public static string BuildPlayer()
    {
        string path = Path.Combine(Path.GetTempPath(), "AZVisual4P_Current", "Player", "AbsoluteZeroVisual4P.exe");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { Bootstrap, Target }, locationPathName = path,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.DetailedBuildReport
        });
        var summary = report.summary;
        string result = $"{summary.result}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; path={path}";
        File.WriteAllText("output/validation/plan036/t02_20260928/fixture-build.txt", result);
        if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException(result);
        return result;
    }
}
