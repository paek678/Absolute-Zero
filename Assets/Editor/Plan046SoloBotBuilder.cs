using System;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.UI.LobbyUI;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// PLAN046 one-time authoring: the approved two-difficulty Solo bot. Creates the decision
// graph (spec decision order with built-in Conditional Guards), the configuration assets and
// the lobby profile list. Never overwrites an existing created asset; Validate() is read-only.
// Installed Behavior 1.0.16 grants this Editor assembly authoring access; player code uses only
// public execution APIs and the package pin stays unchanged.
public static class Plan046SoloBotBuilder
{
    public const string GraphPath = "Assets/Data/Solo/Graphs/SoloDuelDecision.asset";
    public const string KnowledgePath = "Assets/Data/Solo/Knowledge/DefaultItemKnowledge.asset";
    public const string StandardPolicyPath = "Assets/Data/Solo/ItemUse/StandardItemUsePolicy.asset";
    public const string BeginnerPath = "Assets/Data/Solo/Difficulties/Beginner.asset";
    public const string AdvancedPath = "Assets/Data/Solo/Difficulties/Advanced.asset";
    public const string BeginnerEncounterPath = "Assets/Data/Solo/Encounters/BeginnerSoloDuel.asset";
    public const string AdvancedEncounterPath = "Assets/Data/Solo/Encounters/AdvancedSoloDuel.asset";
    const string BotPath = "Assets/Data/Solo/Bots/DefaultBot.asset";
    const string BaselinePath = "Assets/Data/Solo/Difficulties/NeutralBaseline.asset";
    const string RulePath = "Assets/Data/GameModeRules/OneVsOneRule.asset";
    const string RegistryPath = "Assets/Data/Cosmetics/CosmeticRegistry.asset";
    const string LobbyPath = "Assets/Scenes/LobbyScene.unity";
    const string SoloScenePath = "Assets/Scenes/GameScene_Solo.unity";
    const int GraphNodeCount = 22;
    const float InstantDelay = 2, MiniGameDelay = 4, BeginnerExtraDelay = 2;

    // Spec item sheet in row order (ties prefer earlier rows): opponent value and mini-game tier.
    static readonly (string Path, float Value, BotMiniGameTier Tier)[] Sheet =
    {
        ("Assets/Data/Items/Basic/Fan.asset", 3, BotMiniGameTier.None),
        ("Assets/Data/Items/Basic/Windbreaker.asset", 3, BotMiniGameTier.None),
        ("Assets/Data/Items/Basic/WarmTea.asset", 7, BotMiniGameTier.None),
        ("Assets/Data/Items/Basic/Cat.asset", 4, BotMiniGameTier.None),
        ("Assets/Data/Items/Random/Special/Screwdriver.asset", 6, BotMiniGameTier.Hard),
        ("Assets/Data/Items/Random/Debuff/Samgyetang.asset", 4, BotMiniGameTier.None),
        ("Assets/Data/Items/Random/Sabotage/ClawMachine.asset", 6, BotMiniGameTier.Hard),
        ("Assets/Data/Items/Random/Attack/IceCream.asset", 5, BotMiniGameTier.None),
        ("Assets/Data/Items/Random/Attack/WaterGun.asset", 8, BotMiniGameTier.Hard),
        ("Assets/Data/Items/Random/Recovery/HotPack.asset", 12, BotMiniGameTier.Easy),
        ("Assets/Data/Items/Random/Sabotage/BlueTape.asset", 3, BotMiniGameTier.Normal),
        ("Assets/Data/Items/Random/Defense/Mask.asset", 4, BotMiniGameTier.None),
        ("Assets/Data/Items/Random/Attack/IcedAmericano.asset", 5, BotMiniGameTier.None),
        ("Assets/Data/Items/Random/Recovery/HotAmericano.asset", 5, BotMiniGameTier.None),
        ("Assets/Data/Items/Random/Attack/HandFan.asset", 4, BotMiniGameTier.None),
        ("Assets/Data/Items/Random/Recovery/Smartphone.asset", 6, BotMiniGameTier.Normal),
        ("Assets/Data/Items/Random/Buff/BuldakNoodles.asset", 16, BotMiniGameTier.Easy),
        ("Assets/Data/Items/Random/Buff/Soda.asset", 10, BotMiniGameTier.None),
        ("Assets/Data/Items/Random/Sabotage/RedCard.asset", 6, BotMiniGameTier.Normal),
        ("Assets/Data/Items/Random/Attack/HugTshirt.asset", 8, BotMiniGameTier.Hard),
    };

    public static string Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        foreach (string path in new[] { GraphPath, KnowledgePath, StandardPolicyPath, BeginnerPath, AdvancedPath, BeginnerEncounterPath, AdvancedEncounterPath })
            if (AssetDatabase.LoadMainAssetAtPath(path) != null || System.IO.File.Exists(path))
                throw new InvalidOperationException("Refusing to overwrite " + path + "; use Validate() for existing assets.");
        var rule = AssetDatabase.LoadAssetAtPath<GameModeRuleSO>(RulePath);
        var bot = AssetDatabase.LoadAssetAtPath<BotDefinitionSO>(BotPath);
        var baseline = AssetDatabase.LoadAssetAtPath<BotDifficultyProfileSO>(BaselinePath);
        if (rule == null || bot == null || baseline == null) throw new InvalidOperationException("Shared rule, default bot and baseline difficulty must exist.");
        var catalog = Plan036ConfigurationBuilder.ReadCurrentDuelCatalog();

        var runtime = CreateGraph();
        var knowledge = Create<BotItemKnowledgeSO>(KnowledgePath, "solo.knowledge.default", so =>
        {
            var entries = so.FindProperty("entries"); entries.arraySize = Sheet.Length;
            for (int i = 0; i < Sheet.Length; i++)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(Sheet[i].Path);
                if (item == null || !catalog.Contains(item)) throw new InvalidOperationException("Sheet item missing from catalog: " + Sheet[i].Path);
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("Item").objectReferenceValue = item;
                entry.FindPropertyRelative("OpponentValue").floatValue = Sheet[i].Value;
                entry.FindPropertyRelative("MiniGameTier").enumValueIndex = (int)Sheet[i].Tier;
            }
        });
        // Approved delays: 2 s after an instant item, 4 s after a mini-game item; beginner adds 2 s.
        var standard = Create<BotItemUsePolicySO>(StandardPolicyPath, "solo.items.standard", so =>
        {
            so.FindProperty("provisionalTuning").boolValue = true;
            so.FindProperty("defaultDelaySeconds").floatValue = InstantDelay;
            var games = catalog.Where(item => item.RequiresMiniGame && ItemAvailability.IsEnabled(item)).ToArray();
            var overrides = so.FindProperty("overrides"); overrides.arraySize = games.Length;
            for (int i = 0; i < games.Length; i++)
            {
                overrides.GetArrayElementAtIndex(i).FindPropertyRelative("Item").objectReferenceValue = games[i];
                overrides.GetArrayElementAtIndex(i).FindPropertyRelative("DelaySeconds").floatValue = MiniGameDelay;
            }
        });
        var beginner = Create<BotDifficultyProfileSO>(BeginnerPath, "solo.difficulty.beginner",
            so => Difficulty(so, BotJudgementSettings.Beginner, BeginnerExtraDelay, standard));
        var advanced = Create<BotDifficultyProfileSO>(AdvancedPath, "solo.difficulty.advanced",
            so => Difficulty(so, BotJudgementSettings.Advanced, 0, standard));
        Modify(bot, so =>
        {
            so.FindProperty("graph").objectReferenceValue = runtime;
            so.FindProperty("itemKnowledge").objectReferenceValue = knowledge;
        });
        // Development baseline keeps the approved judgement without random rolls so fixtures stay repeatable.
        var repeatable = BotJudgementSettings.Advanced;
        repeatable.EasyMiniGameSuccess = repeatable.NormalMiniGameSuccess = repeatable.HardMiniGameSuccess = 1;
        repeatable.BestPickChance = 1; repeatable.SecondPickChance = 0;
        Modify(baseline, so => Judgement(so.FindProperty("judgement"), repeatable));
        var beginnerEncounter = Create<SoloDuelDefinitionSO>(BeginnerEncounterPath, "solo.encounter.beginner", so => Encounter(so, bot, beginner, rule));
        var advancedEncounter = Create<SoloDuelDefinitionSO>(AdvancedEncounterPath, "solo.encounter.advanced", so => Encounter(so, bot, advanced, rule));
        AssetDatabase.SaveAssets();
        BindLobby(beginnerEncounter, advancedEncounter);
        return Validate();
    }

    public static string Validate()
    {
        AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceSynchronousImport);
        var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(GraphPath);
        var runtime = AssetDatabase.LoadAllAssetsAtPath(GraphPath).OfType<BehaviorGraph>().SingleOrDefault();
        if (graph == null || runtime == null || !SoloSettingsResolver.HasCompiledRoot(runtime)
            || SerializationUtility.HasManagedReferencesWithMissingTypes(runtime))
            throw new InvalidOperationException("Decision graph did not compile.");
        if (graph.Nodes.Count != GraphNodeCount || graph.Nodes.OfType<StartNodeModel>().Single().Repeat)
            throw new InvalidOperationException($"Decision graph structure mismatch: {graph.Nodes.Count} nodes.");
        var guards = graph.Nodes.OfType<ConditionalGuardNodeModel>().ToArray();
        if (guards.Length != 2 || !guards.Any(g => Guards(g, typeof(SoloInCrisis))) || !guards.Any(g => Guards(g, typeof(SoloMustFinish))))
            throw new InvalidOperationException("Decision graph must guard crisis and lethal branches.");
        string order = string.Join(">", ChildrenByX(graph, graph.Nodes.OfType<NodeModel>()
            .Single(n => n is BehaviorGraphNodeModel b && b.NodeType?.Type == typeof(SequenceComposite) && ChildrenByX(graph, n).Length == 10)));
        const string expected = "SoloObserve>SoloCandidates>SoloScore>SelectorComposite>SoloThinking>SoloSubmitUse>SoloAwaitUse>SoloChooseReady>SoloWaitReady>SoloReady";
        if (order != expected) throw new InvalidOperationException("Decision step order mismatch: " + order);

        var catalog = Plan036ConfigurationBuilder.ReadCurrentDuelCatalog();
        var registry = AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>(RegistryPath);
        foreach (string path in new[] { BeginnerEncounterPath, AdvancedEncounterPath, Plan036ConfigurationBuilder.DraftPath })
        {
            var encounter = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(path);
            var context = new SoloConfigurationContext(encounter.SharedOneVsOneRule, catalog, registry,
                scene => EditorBuildSettings.scenes.Any(x => x.enabled && x.path == scene));
            if (!SoloSettingsResolver.TryResolveForDevelopment(encounter, context, out var settings, out var errors))
                throw new InvalidOperationException(path + ": " + string.Join("; ", errors));
            if (settings.IsValidationFixture || settings.Graph != runtime) throw new InvalidOperationException(path + " must use the production graph.");
            for (short i = 0; i < catalog.Length; i++)
                if (ItemAvailability.IsEnabled(catalog[i]) && !settings.KnowsItem(i))
                    throw new InvalidOperationException(path + " has no item knowledge for " + catalog[i].name);
        }
        var profiles = ReadLobbyProfiles();
        if (profiles.Length != 2 || profiles[0] != AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(BeginnerEncounterPath)
            || profiles[1] != AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(AdvancedEncounterPath))
            throw new InvalidOperationException("LobbyScene Solo profiles must list 초급 then 상급.");
        return $"PASS: {GraphNodeCount}-node graph with crisis/lethal guards, beginner/advanced/baseline resolve, " +
            $"{catalog.Count(ItemAvailability.IsEnabled)} enabled items known, lobby lists 초급/상급.";
    }

    static BehaviorGraph CreateGraph()
    {
        var graph = ScriptableObject.CreateInstance<BehaviorAuthoringGraph>(); graph.name = "Solo Duel Decision";
        AssetDatabase.CreateAsset(graph, GraphPath); graph.ValidateAsset();
        var start = graph.Nodes.OfType<StartNodeModel>().Single(); start.Repeat = false; start.Position = new Vector2(3000, 0);
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
        NodeModel Guard(Type condition, float x, float y)
        {
            var guard = (ConditionalGuardNodeModel)Node(typeof(ConditionalGuardAction), x, y);
            guard.ConditionModels.Add(new ConditionModel(guard, (Condition)Activator.CreateInstance(condition),
                ConditionUtility.GetInfoForConditionType(condition)));
            guard.RequiresAllConditionsTrue = true;
            return guard;
        }
        // Siblings execute in ascending X (GraphAssetProcessor.GetSortedConnections).
        var root = Node(typeof(SelectorComposite), 3000, 160); Link(start, root);
        var retry = Node(typeof(SoloRetry), 2400, 320); Link(root, retry);
        var fallback = Node(typeof(SoloFallback), 6000, 320); Link(root, fallback);
        var sequence = Node(typeof(SequenceComposite), 2400, 480); Link(retry, sequence);
        var steps = new[] { typeof(SoloObserve), typeof(SoloCandidates), typeof(SoloScore), typeof(SelectorComposite),
            typeof(SoloThinking), typeof(SoloSubmitUse), typeof(SoloAwaitUse), typeof(SoloChooseReady), typeof(SoloWaitReady), typeof(SoloReady) };
        NodeModel picks = null;
        for (int i = 0; i < steps.Length; i++)
        {
            var node = Node(steps[i], i * 600, 660); Link(sequence, node);
            if (steps[i] == typeof(SelectorComposite)) picks = node;
        }
        var crisis = Node(typeof(SequenceComposite), 1400, 880); Link(picks, crisis);
        Link(crisis, Guard(typeof(SoloInCrisis), 1250, 1100)); Link(crisis, Node(typeof(SoloPickCrisis), 1550, 1100));
        var lethal = Node(typeof(SequenceComposite), 1900, 880); Link(picks, lethal);
        Link(lethal, Guard(typeof(SoloMustFinish), 1750, 1100)); Link(lethal, Node(typeof(SoloPickLethal), 2050, 1100));
        Link(picks, Node(typeof(SoloPickWeighted), 2400, 880));

        GraphAssetProcessor.EnsureBlackboardGraphOwnerVariable(graph.Blackboard);
        graph.Blackboard.Variables.Add(new TypedVariableModel<int> { Name = "Round" });
        graph.Blackboard.Variables.Add(new TypedVariableModel<int> { Name = "Turn" });
        graph.Blackboard.Variables.Add(new TypedVariableModel<float> { Name = "Own temperature" });
        graph.Blackboard.Variables.Add(new TypedVariableModel<float> { Name = "Opponent temperature" });
        graph.Blackboard.Variables.Add(new TypedVariableModel<float> { Name = "Remaining seconds" });
        graph.Blackboard.Variables.Add(new TypedVariableModel<bool> { Name = "In crisis" });
        graph.Blackboard.Variables.Add(new TypedVariableModel<bool> { Name = "Lethal available" });
        graph.Blackboard.Variables.Add(new TypedVariableModel<string> { Name = "Selected item" });
        graph.Blackboard.Variables.Add(new TypedVariableModel<string> { Name = "Selected copy" });
        ((BehaviorBlackboardAuthoringAsset)graph.Blackboard).RebuildAndSave();
        graph.SetAssetDirty(); graph.ValidateAsset(); graph.BuildRuntimeGraph(); graph.SaveAsset(); AssetDatabase.SaveAssets();
        var runtime = AssetDatabase.LoadAllAssetsAtPath(GraphPath).OfType<BehaviorGraph>().Single();
        if (!SoloSettingsResolver.HasCompiledRoot(runtime) || SerializationUtility.HasManagedReferencesWithMissingTypes(runtime))
            throw new InvalidOperationException("Production graph did not compile.");
        return runtime;
    }

    static bool Guards(ConditionalGuardNodeModel guard, Type condition)
    {
        var id = ConditionUtility.GetInfoForConditionType(condition).TypeID;
        return guard.ConditionModels.Count == 1 && guard.ConditionModels[0].ConditionTypeID.Equals(id);
    }

    static string[] ChildrenByX(BehaviorAuthoringGraph graph, NodeModel parent)
        => !parent.TryDefaultOutputPortModel(out var output) ? Array.Empty<string>()
            : output.Connections.Select(port => port.NodeModel).OrderBy(node => node.Position.x)
                .Select(node => node is BehaviorGraphNodeModel b && b.NodeType?.Type != null ? b.NodeType.Type.Name : node.GetType().Name).ToArray();

    static void Difficulty(SerializedObject so, BotJudgementSettings judgement, float extraDelay, BotItemUsePolicySO policy)
    {
        so.FindProperty("provisionalTuning").boolValue = true;
        so.FindProperty("minimumThinkSeconds").floatValue = 0;
        so.FindProperty("maximumThinkSeconds").floatValue = 0;
        so.FindProperty("readyPolicy").enumValueIndex = (int)BotReadyPolicy.AfterSelection;
        so.FindProperty("readyReserveSeconds").floatValue = .25f;
        so.FindProperty("extraItemDelaySeconds").floatValue = extraDelay;
        so.FindProperty("retryBudget").intValue = 2;
        so.FindProperty("itemUseOverride").objectReferenceValue = policy;
        Judgement(so.FindProperty("judgement"), judgement);
    }

    static void Judgement(SerializedProperty p, BotJudgementSettings j)
    {
        p.FindPropertyRelative("EasyMiniGameSuccess").floatValue = j.EasyMiniGameSuccess;
        p.FindPropertyRelative("NormalMiniGameSuccess").floatValue = j.NormalMiniGameSuccess;
        p.FindPropertyRelative("HardMiniGameSuccess").floatValue = j.HardMiniGameSuccess;
        p.FindPropertyRelative("BestPickChance").floatValue = j.BestPickChance;
        p.FindPropertyRelative("SecondPickChance").floatValue = j.SecondPickChance;
        p.FindPropertyRelative("LookaheadFactor").floatValue = j.LookaheadFactor;
        p.FindPropertyRelative("OpponentBlockChance").floatValue = j.OpponentBlockChance;
        p.FindPropertyRelative("OpponentAttackChance").floatValue = j.OpponentAttackChance;
        p.FindPropertyRelative("KillBonus").floatValue = j.KillBonus;
        p.FindPropertyRelative("CrisisCareMultiplier").floatValue = j.CrisisCareMultiplier;
        p.FindPropertyRelative("ComfortCareMultiplier").floatValue = j.ComfortCareMultiplier;
        p.FindPropertyRelative("ComfortMargin").floatValue = j.ComfortMargin;
        p.FindPropertyRelative("MayMissLethal").boolValue = j.MayMissLethal;
        p.FindPropertyRelative("MayMissCrisis").boolValue = j.MayMissCrisis;
    }

    static void Encounter(SerializedObject so, BotDefinitionSO bot, BotDifficultyProfileSO difficulty, GameModeRuleSO rule)
    {
        so.FindProperty("readiness").intValue = (int)SoloConfigurationReadiness.Ready;
        so.FindProperty("bot").objectReferenceValue = bot;
        so.FindProperty("difficulty").objectReferenceValue = difficulty;
        so.FindProperty("sharedOneVsOneRule").objectReferenceValue = rule;
        so.FindProperty("gameplayScenePath").stringValue = SoloScenePath;
    }

    static void BindLobby(SoloDuelDefinitionSO beginner, SoloDuelDefinitionSO advanced)
    {
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(LobbyPath);
        bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
        else if (scene.isDirty) throw new InvalidOperationException("Save or discard LobbyScene changes first.");
        try
        {
            var ui = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AZLobbyUI>(true)).Single();
            var data = new SerializedObject(ui);
            var profiles = data.FindProperty("_soloProfiles");
            profiles.arraySize = 2;
            Profile(profiles.GetArrayElementAtIndex(0), "초급",
                "처음 하는 분께 맞는 상대입니다. 아이템을 쓰는 데 시간이 더 걸리고, 가끔 마무리를 놓칩니다.", beginner);
            Profile(profiles.GetArrayElementAtIndex(1), "상급",
                "빠르게 판단하고, 처치할 수 있으면 놓치지 않는 상대입니다.", advanced);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (opened) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        }
    }

    static void Profile(SerializedProperty entry, string title, string description, SoloDuelDefinitionSO encounter)
    {
        entry.FindPropertyRelative("Title").stringValue = title;
        entry.FindPropertyRelative("Description").stringValue = description;
        entry.FindPropertyRelative("Encounter").objectReferenceValue = encounter;
    }

    static SoloDuelDefinitionSO[] ReadLobbyProfiles()
    {
        var scene = EditorSceneManager.OpenPreviewScene(LobbyPath);
        try
        {
            var ui = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AZLobbyUI>(true)).Single();
            var profiles = new SerializedObject(ui).FindProperty("_soloProfiles");
            return Enumerable.Range(0, profiles.arraySize)
                .Select(i => profiles.GetArrayElementAtIndex(i).FindPropertyRelative("Encounter").objectReferenceValue as SoloDuelDefinitionSO)
                .ToArray();
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    static T Create<T>(string path, string id, Action<SerializedObject> configure) where T : SoloConfigurationSO
    {
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        var asset = ScriptableObject.CreateInstance<T>();
        var so = new SerializedObject(asset);
        so.FindProperty("configId").stringValue = id;
        configure(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssetIfDirty(asset);
        return asset;
    }

    static void Modify(UnityEngine.Object asset, Action<SerializedObject> edit)
    { var fields = new SerializedObject(asset); edit(fields); fields.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset); }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
