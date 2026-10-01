using System;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Installed Behavior 1.0.16 grants this Editor assembly authoring access. Player
// code uses only public execution APIs; package pin remains unchanged.
public static class Plan036BotGraphBuilder
{
    public const string GraphPath = "Assets/Data/Solo/Graphs/SoloDuelDecision.asset";
    public static string CreateAndBind()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Preserve the current stopped scene first.");
        var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(GraphPath);
        if (graph == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data/Solo/Graphs")) AssetDatabase.CreateFolder("Assets/Data/Solo", "Graphs");
            graph = ScriptableObject.CreateInstance<BehaviorAuthoringGraph>(); graph.name = "Solo Duel Decision";
            AssetDatabase.CreateAsset(graph, GraphPath); graph.ValidateAsset();
            var start = graph.Nodes.OfType<StartNodeModel>().Single(); start.Repeat = false; start.Position = new Vector2(2500, 0);
            NodeModel Node(Type type, float x, float y)
            {
                var info = Unity.Behavior.NodeRegistry.GetInfo(type) ?? throw new InvalidOperationException("Node not registered: " + type);
                return graph.CreateNode(info.ModelType, new Vector2(x, y), null, new object[] { info });
            }
            void Link(NodeModel parent, NodeModel child)
            {
                if (!parent.TryDefaultOutputPortModel(out var output) || !child.TryDefaultInputPortModel(out var input))
                    throw new InvalidOperationException("Missing default graph ports.");
                graph.ConnectEdge(output, input);
            }
            var root = Node(typeof(SelectorComposite), 2500, 160); Link(start, root);
            var retry = Node(typeof(SoloRetry), 2000, 320); Link(root, retry);
            var fallback = Node(typeof(SoloFallback), 5000, 320); Link(root, fallback);
            var sequence = Node(typeof(SequenceComposite), 2000, 480); Link(retry, sequence);
            var types = new[] { typeof(SoloObserve), typeof(SoloCandidates), typeof(SelectorComposite), typeof(SoloThinking),
                typeof(SoloSubmitUse), typeof(SoloAwaitUse), typeof(SoloChooseReady), typeof(SoloWaitReady), typeof(SoloReady) };
            NodeModel tactics = null;
            for (int i = 0; i < types.Length; i++)
            {
                var node = Node(types[i], i * 500, 660); Link(sequence, node);
                if (i == 2) tactics = node;
            }
            var strategies = new[] { typeof(SoloSurvival), typeof(SoloFinish), typeof(SoloCounter), typeof(SoloResource), typeof(SoloGeneral) };
            for (int i = 0; i < strategies.Length; i++) Link(tactics, Node(strategies[i], i * 400, 880));
            GraphAssetProcessor.EnsureBlackboardGraphOwnerVariable(graph.Blackboard);
            graph.Blackboard.Variables.Add(new TypedVariableModel<int> { Name = "Round" });
            graph.Blackboard.Variables.Add(new TypedVariableModel<int> { Name = "Turn" });
            graph.Blackboard.Variables.Add(new TypedVariableModel<float> { Name = "Own temperature" });
            graph.Blackboard.Variables.Add(new TypedVariableModel<float> { Name = "Opponent temperature" });
            graph.Blackboard.Variables.Add(new TypedVariableModel<float> { Name = "Remaining seconds" });
            graph.Blackboard.Variables.Add(new TypedVariableModel<string> { Name = "Selected copy" });
            ((BehaviorBlackboardAuthoringAsset)graph.Blackboard).RebuildAndSave();
            graph.SetAssetDirty(); graph.ValidateAsset(); graph.BuildRuntimeGraph(); graph.SaveAsset(); AssetDatabase.SaveAssets();
        }
        var runtime = AssetDatabase.LoadAllAssetsAtPath(GraphPath).OfType<BehaviorGraph>().Single();
        if (!SoloSettingsResolver.HasCompiledRoot(runtime) || SerializationUtility.HasManagedReferencesWithMissingTypes(runtime))
            throw new InvalidOperationException("Production graph did not compile.");
        var encounter = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.DraftPath);
        Modify(encounter.Bot, x => x.FindProperty("graph").objectReferenceValue = runtime);
        Modify(encounter.Bot.ItemUsePolicy, x =>
        {
            x.FindProperty("provisionalTuning").boolValue = true;
            x.FindProperty("defaultDelaySeconds").floatValue = 0.75f;
            var catalog = Plan036ConfigurationBuilder.ReadCurrentDuelCatalog();
            var items = x.FindProperty("overrides"); items.arraySize = catalog.Length;
            for (int i = 0; i < catalog.Length; i++)
            {
                items.GetArrayElementAtIndex(i).FindPropertyRelative("Item").objectReferenceValue = catalog[i];
                items.GetArrayElementAtIndex(i).FindPropertyRelative("DelaySeconds").floatValue = catalog[i].RequiresMiniGame ? 1.5f : 0.75f;
            }
        });
        Modify(encounter.Difficulty, x =>
        {
            x.FindProperty("provisionalTuning").boolValue = true;
            x.FindProperty("minimumThinkSeconds").floatValue = 0.2f;
            x.FindProperty("maximumThinkSeconds").floatValue = 0.45f;
            x.FindProperty("readyReserveSeconds").floatValue = 0.25f;
            x.FindProperty("retryBudget").intValue = 2;
        });
        Modify(encounter, x => x.FindProperty("readiness").intValue = (int)SoloConfigurationReadiness.Ready);
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.OpenScene(Plan036SoloSceneBuilder.SoloScene, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            if (!scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<BotTurnController>(true)).Any())
                new GameObject("Solo Bot Decision Controller").AddComponent<BotTurnController>();
            EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous); }
        AssetDatabase.SaveAssets();
        return Validate();
    }
    public static string Validate()
    {
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
        var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(GraphPath);
        var encounter = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.DraftPath);
        var context = new SoloConfigurationContext(encounter.SharedOneVsOneRule, Plan036ConfigurationBuilder.ReadCurrentDuelCatalog(),
            AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset"),
            path => EditorBuildSettings.scenes.Any(x => x.enabled && x.path == path));
        if (!SoloSettingsResolver.TryResolveForDevelopment(encounter, context, out var settings, out var errors))
            throw new InvalidOperationException(string.Join("; ", errors));
        if (graph.Nodes.Count != 19 || graph.Nodes.OfType<StartNodeModel>().Single().Repeat || settings.IsValidationFixture)
            throw new InvalidOperationException("Production editable graph structure/readiness mismatch.");
        return "PASS: 19 editable nodes, isolated production graph, strict default encounter, dedicated scene, 21 explicit provisional item delays.";
    }
    static void Modify(UnityEngine.Object asset, System.Action<SerializedObject> edit)
    { var fields = new SerializedObject(asset); edit(fields); fields.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset); }
}
