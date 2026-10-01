using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using Unity.Behavior;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Explicit cloned-profile fixtures, not natural matches or approved new balance.
public static class Plan036ExtensionValidation
{
    [Serializable] sealed class Report
    {
        public bool passed;
        public List<string> checks = new(), errors = new(), traces = new();
        public List<int> runtimeGraphCounts = new();
        public string failure;
    }
    public static bool Running { get; private set; }
    static readonly List<Object> Retained = new();
    const string Root = "output/validation/plan036/extended_20260929/";
    public static string Start(bool onlyUrgency = false)
    {
        if (Running || !EditorApplication.isPlaying) throw new InvalidOperationException("Enter Lobby Play Mode first");
        Running = true; Run(onlyUrgency); return "started";
    }
    static async void Run(bool onlyUrgency)
    {
        var report = new Report(); SoloSessionCoordinator session = null;
        void Log(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(text); }
        void Check(bool ok, string label)
        { if (!ok) throw new InvalidOperationException(label); report.checks.Add(label); }
        Application.logMessageReceived += Log;
        try
        {
            await Until(() => AppBootstrapper.Instance?.IsReady == true, 15, "app ready");
            var app = AppBootstrapper.Instance; var manager = NetworkManager.Singleton;
            var source = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.DraftPath);
            Retained.Add(source);
            string before = EditorJsonUtility.ToJson(source.Difficulty);
            var catalog = Plan036ConfigurationBuilder.ReadCurrentDuelCatalog();
            var context = new SoloConfigurationContext(source.SharedOneVsOneRule, catalog,
                AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset"));
            session = new SoloSessionCoordinator(app.SessionRouter, new NgoLocalHostRuntime(manager), 18778);
            for (int scenario = 0; scenario < (onlyUrgency ? 1 : 12); scenario++)
            {
                var encounter = Clone(source); var difficulty = Clone(source.Difficulty); var botConfig = Clone(source.Bot);
                Set(encounter, "difficulty", difficulty); Set(encounter, "bot", botConfig);
                Set(difficulty, "minimumThinkSeconds", scenario == 0 ? 8f : .1f);
                Set(difficulty, "maximumThinkSeconds", scenario == 0 ? 8f : .1f);
                Set(difficulty, "readyPolicy", (int)(scenario == 2 ? BotReadyPolicy.NearDeadline : BotReadyPolicy.AfterSelection));
                if (scenario == 1)
                {
                    Set(difficulty, "overrideTacticalWeights", true);
                    foreach (string branch in new[] { "Survival", "Finish", "Counter", "Resource", "General" })
                        Set(difficulty, "tacticalWeights." + branch, branch == "General" ? 1f : 0f);
                    Set(difficulty, "minimumThinkSeconds", 1f); Set(difficulty, "maximumThinkSeconds", 1f);
                }
                if (scenario == 3)
                {
                    var policy = Clone(source.Bot.ItemUsePolicy);
                    Set(policy, "configId", "fixture.long-items"); Set(policy, "defaultDelaySeconds", 60f);
                    var fields = new SerializedObject(policy); fields.FindProperty("overrides").arraySize = 0; fields.ApplyModifiedPropertiesWithoutUndo();
                    Set(botConfig, "itemUsePolicy", policy); Set(difficulty, "retryBudget", 0);
                }
                Check((await session.StartDevelopmentAsync(encounter, context)).IsSuccess, "scenario " + scenario + " launches");
                await Until(() => TurnManager.Instance != null && TurnManager.Instance.TryGetPrepInputSnapshot(out _), 35, "Prep");
                var turn = TurnManager.Instance; var bot = turn.GetPlayer(1);
                var controller = Object.FindAnyObjectByType<BotTurnController>();
                if (scenario == 0)
                {
                    await Until(() => controller.Trace.Any(x => x.Contains("think=8.000")), 5, "long thought starts");
                    bot.Temperature.Value = 4; // Deliberate publicly visible urgency stimulus.
                }
                if (scenario == 1)
                {
                    // Replace only the fixture inventory during thinking; stale CopyId must replan.
                    await Until(() => controller.Trace.Any(x => x.Contains("think=1.000")), 5, "utility thought starts");
                    var inventory = bot.GetInventory(); inventory.SlotStates.Clear();
                    Check(inventory.GrantSpecificItem((short)Array.FindIndex(catalog, x => x.ItemName == "Red Card")), "fixture grants only Red Card");
                }
                await Until(() => bot.IsReady.Value, 25, "Ready");
                report.traces.AddRange(controller.Trace.Select(x => "scenario=" + scenario + " " + x));
                var selected = bot.GetActionQueue().selectedAction;
                if (scenario == 0) Check(selected.HasValue && selected.Value.ItemData is RecoveryItemDataSO, "urgent thought refreshes survival choice before submission");
                if (scenario == 1) Check(selected.HasValue && selected.Value.ItemData.ItemName == "Red Card", "real graph replans removed copy and queues neutralize");
                if (scenario == 2)
                {
                    turn.TryGetPrepInputSnapshot(out var window);
                    Check(manager.ServerTime.Time >= window.Deadline - .7, "near-deadline profile override honored");
                }
                Check(controller.RunCount == 1 && controller.ReadyCount == 1 && controller.SelectionCount == (scenario == 3 ? 0 : 1), "scenario " + scenario + " bounded single decision/Ready");
                if (scenario == 3) Check(!selected.HasValue && controller.Trace.Any(x => x.Contains("fallback")), "unaffordable delay and zero retries use legal fallback");
                await session.StopAsync(); await Task.Delay(200);
                Check(controller == null && turn == null && bot == null && app.SessionRouter.Current == null && !manager.IsListening
                    && Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length == 1
                    && Object.FindObjectsByType<BotTurnController>(FindObjectsSortMode.None).Length == 0
                    && Object.FindObjectsByType<BehaviorGraphAgent>(FindObjectsSortMode.None).Length == 0
                    && manager.GetComponentsInChildren<Unity.Netcode.Transports.UTP.UnityTransport>(true).Length == 1,
                    "scenario " + scenario + " releases graph/players/transport and retains one manager");
                report.runtimeGraphCounts.Add(Resources.FindObjectsOfTypeAll<BehaviorGraph>().Count(x => !EditorUtility.IsPersistent(x)));
            }
            if (!onlyUrgency) Check(report.runtimeGraphCounts.Skip(3).Max() <= report.runtimeGraphCounts[3] + 1, "runtime graph instances do not accumulate over repeated sessions");
            Check(EditorJsonUtility.ToJson(source.Difficulty) == before, "authored difficulty unchanged");
            report.passed = report.errors.Count == 0;
        }
        catch (Exception error) { report.failure = error.ToString(); }
        finally
        {
            try { if (AppBootstrapper.Instance?.SessionRouter.Current != null) await AppBootstrapper.Instance.SessionRouter.StopAsync(); }
            catch (Exception error) { report.errors.Add("cleanup: " + error); report.passed = false; }
            session?.Dispose();
            foreach (var obj in Retained) if (obj != null && !EditorUtility.IsPersistent(obj)) Object.Destroy(obj);
            Retained.Clear(); Application.logMessageReceived -= Log;
            Directory.CreateDirectory(Root); File.WriteAllText(Root + (onlyUrgency ? "urgency-before.json" : "profile-lifecycle.json"), JsonUtility.ToJson(report, true));
            Running = false;
        }
    }
    static T Clone<T>(T source) where T : Object { var clone = Object.Instantiate(source); Retained.Add(clone); return clone; }
    static void Set(Object obj, string name, object value)
    {
        var data = new SerializedObject(obj); var field = data.FindProperty(name);
        if (value is Object asset) field.objectReferenceValue = asset;
        else if (value is float number) field.floatValue = number;
        else if (value is int integer) field.intValue = integer;
        else if (value is bool flag) field.boolValue = flag;
        else field.stringValue = (string)value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static async Task Until(Func<bool> predicate, double seconds, string label)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (!predicate()) { if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(label); await Task.Delay(20); }
    }
}
