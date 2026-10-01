using System;
using System.IO;
using System.Linq;
using AbsoluteZero.Validation.Behavior;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Behavior 1.0.16 explicitly grants Assembly-CSharp-Editor friend access.
// Keep this tooling fixture outside asmdefs; internal authoring APIs never enter player code.
public static class Plan036BehaviorFixtureBuilder
{
    public const string GraphPath = "Assets/Tests/Plan036Behavior/Plan036BehaviorFixture.asset";
    public const string ScenePath = "Assets/Tests/Plan036Behavior/Plan036BehaviorFixture.unity";

    public static string Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        if (AssetDatabase.LoadMainAssetAtPath(GraphPath) != null || File.Exists(ScenePath))
            throw new InvalidOperationException("Fixture already exists; use ValidateSavedFixture instead of overwriting.");

        var graph = ScriptableObject.CreateInstance<BehaviorAuthoringGraph>();
        graph.name = "Plan036BehaviorFixture";
        AssetDatabase.CreateAsset(graph, GraphPath);
        graph.ValidateAsset();
        var start = graph.Nodes.OfType<StartNodeModel>().Single();
        start.Position = Vector2.zero;
        start.Repeat = false;
        var info = NodeRegistry.GetInfo(typeof(Plan036LifecycleAction));
        if (info == null) throw new InvalidOperationException("Custom node has not been registered.");
        var action = graph.CreateNode(info.ModelType, new Vector2(0, 180), null, new object[] { info });
        if (!start.TryDefaultOutputPortModel(out var output) || !action.TryDefaultInputPortModel(out var input))
            throw new InvalidOperationException("Expected graph ports are missing.");
        graph.ConnectEdge(output, input);
        GraphAssetProcessor.EnsureBlackboardGraphOwnerVariable(graph.Blackboard);
        ((BehaviorBlackboardAuthoringAsset)graph.Blackboard).RebuildAndSave();
        graph.SetAssetDirty();
        graph.ValidateAsset();
        var runtime = graph.BuildRuntimeGraph();
        if (runtime == null || runtime.RootGraph?.Root == null)
            throw new InvalidOperationException("Runtime graph was not generated.");
        EditorUtility.SetDirty(runtime);
        graph.SaveAsset();
        AssetDatabase.SaveAssets();

        Scene previous = SceneManager.GetActiveScene();
        Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            var owner = new GameObject("Plan036 Behavior tooling fixture");
            SceneManager.MoveGameObjectToScene(owner, fixture);
            owner.AddComponent<Plan036BehaviorProbe>().SourceGraph = runtime;
            var camera = new GameObject("Fixture Camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, fixture);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.13f);
            if (!EditorSceneManager.SaveScene(fixture, ScenePath)) throw new IOException("Could not save fixture scene.");
        }
        finally
        {
            EditorSceneManager.CloseScene(fixture, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
        return ValidateSavedFixture();
    }

    public static string ValidateSavedFixture()
    {
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(GraphPath);
        if (graph == null || graph.Nodes.Count != 2 || graph.Nodes.OfType<StartNodeModel>().Single().Repeat)
            throw new InvalidOperationException("Saved authoring graph did not preserve two nodes and one-shot start.");
        var runtime = AssetDatabase.LoadAllAssetsAtPath(GraphPath).OfType<BehaviorGraph>().Single();
        if (runtime.RootGraph?.Root == null || SerializationUtility.HasManagedReferencesWithMissingTypes(runtime))
            throw new InvalidOperationException("Saved runtime graph has missing nodes or types.");
        Scene previous = SceneManager.GetActiveScene();
        Scene fixture = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var probes = fixture.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Plan036BehaviorProbe>()).ToArray();
            if (probes.Length != 1 || probes[0].SourceGraph != runtime)
                throw new InvalidOperationException("Saved scene did not preserve its graph reference.");
        }
        finally
        {
            EditorSceneManager.CloseScene(fixture, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
        return "PASS: authoring nodes, runtime managed references and reopened scene binding; graph="
            + AssetDatabase.AssetPathToGUID(GraphPath);
    }

    public static string BuildPlayer()
    {
        string output = Path.Combine(Path.GetTempPath(), "AZPlan036BehaviorFixture", "Player", "Plan036BehaviorFixture.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath }, locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development | BuildOptions.DetailedBuildReport
        });
        var summary = report.summary;
        string result = $"{summary.result}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; path={output}";
        File.WriteAllText("output/validation/plan036/t00_20260928/behavior-build.txt", result);
        if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException(result);
        return result;
    }
}
