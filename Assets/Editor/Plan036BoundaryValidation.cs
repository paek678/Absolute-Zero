using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit Editor Play Mode fixture. Changes only unsaved configuration clones
// and labeled live test stimuli; the production scene and SOs remain untouched.
public static class Plan036BoundaryValidation
{
    [Serializable] sealed class Report
    {
        public bool passed;
        public List<string> checks = new(), traces = new(), errors = new();
        public string failure;
    }
    public static bool Running { get; private set; }
    public static string LastResult { get; private set; }
    // Unity's unused-asset sweep does not scan a suspended async stack. Keep the
    // fixture's sources/clones rooted until all scene transitions have finished.
    static readonly List<UnityEngine.Object> Retained = new();
    public static string Start()
    {
        if (Running || !EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode in LobbyScene first.");
        Running = true; Run(); return "started";
    }
    static async void Run()
    {
        var report = new Report(); var clones = new List<UnityEngine.Object>();
        SoloSessionCoordinator session = null;
        int oldFps = Application.targetFrameRate;
        void Log(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(text); }
        void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); report.checks.Add(label); Debug.Log("[SoloBoundary] " + label); }
        Application.logMessageReceived += Log;
        try
        {
            await Until(() => AppBootstrapper.Instance?.IsReady == true, 15, "app ready");
            var app = AppBootstrapper.Instance;
            session = new SoloSessionCoordinator(app.SessionRouter, new NgoLocalHostRuntime(NetworkManager.Singleton), 18778);
            var original = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.DraftPath);
            Retained.Add(original);
            string before = EditorJsonUtility.ToJson(original), beforeDifficulty = EditorJsonUtility.ToJson(original.Difficulty);
            var context = new SoloConfigurationContext(original.SharedOneVsOneRule, Plan036ConfigurationBuilder.ReadCurrentDuelCatalog(),
                AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset"));
            var missing = await session.StartDevelopmentAsync(null, context);
            Check(missing.IsFailure && app.SessionRouter.Current == null && !NetworkManager.Singleton.IsListening, "missing encounter rejected without starting network");
            var wrongScene = UnityEngine.Object.Instantiate(original); clones.Add(wrongScene);
            var wrongFields = new SerializedObject(wrongScene); wrongFields.FindProperty("gameplayScenePath").stringValue = "Assets/Scenes/MissingSolo.unity"; wrongFields.ApplyModifiedPropertiesWithoutUndo();
            var invalidScene = await session.StartDevelopmentAsync(wrongScene, context);
            Check(invalidScene.IsFailure && app.SessionRouter.Current == null, "missing scene rejected before host or loading UI");
            var graphBot = UnityEngine.Object.Instantiate(original.Bot); clones.Add(graphBot);
            var graphFields = new SerializedObject(graphBot); graphFields.FindProperty("graph").objectReferenceValue = null; graphFields.ApplyModifiedPropertiesWithoutUndo();
            var graphEncounter = UnityEngine.Object.Instantiate(original); clones.Add(graphEncounter);
            var graphEncounterFields = new SerializedObject(graphEncounter); graphEncounterFields.FindProperty("bot").objectReferenceValue = graphBot; graphEncounterFields.ApplyModifiedPropertiesWithoutUndo();
            var invalidGraph = await session.StartDevelopmentAsync(graphEncounter, context);
            Check(invalidGraph.IsFailure && app.SessionRouter.Current == null, "missing graph rejected before host or loading UI");
            for (int scenario = 0; scenario < 7; scenario++)
            {
                var encounter = UnityEngine.Object.Instantiate(original); clones.Add(encounter);
                var difficulty = UnityEngine.Object.Instantiate(original.Difficulty); clones.Add(difficulty);
                Retained.Add(encounter); Retained.Add(difficulty);
                var fields = new SerializedObject(difficulty);
                fields.FindProperty("minimumThinkSeconds").floatValue = scenario == 2 || scenario == 6 ? 8 : 0;
                fields.FindProperty("maximumThinkSeconds").floatValue = scenario == 2 || scenario == 6 ? 8 : 0;
                fields.FindProperty("readyPolicy").intValue = (int)(scenario == 1 ? BotReadyPolicy.NearDeadline : BotReadyPolicy.AfterSelection);
                fields.ApplyModifiedPropertiesWithoutUndo();
                fields = new SerializedObject(encounter); fields.FindProperty("difficulty").objectReferenceValue = difficulty; fields.ApplyModifiedPropertiesWithoutUndo();
                Application.targetFrameRate = scenario == 3 ? 5 : oldFps;
                var start = await session.StartDevelopmentAsync(encounter, context);
                Check(start.IsSuccess, "scenario " + scenario + " dedicated Solo entry: " + start.ErrorMessage);
                await Until(() => TurnManager.Instance != null && TurnManager.Instance.TryGetPrepInputSnapshot(out _), 35, "committed first Prep");
                var turn = TurnManager.Instance; var bot = turn.GetPlayer(1); var human = turn.GetPlayer(0);
                var controller = UnityEngine.Object.FindAnyObjectByType<BotTurnController>();
                turn.TryGetPrepInputSnapshot(out var first);
                if (scenario == 6)
                {
                    await Until(() => controller.Trace.Any(x => x.Contains("think=8.000")), 5, "exit during thinking");
                    Check(!bot.HasSelectedItem.Value, "exit fixture stops during optional thinking before selection");
                    report.traces.AddRange(controller.Trace.Select(x => "scenario=6 " + x));
                    await app.SessionRouter.StopAsync(); await Task.Delay(1200);
                    Check(controller == null && !NetworkManager.Singleton.IsListening && app.SessionRouter.Current == null
                        && UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length == 1,
                        "exit during thinking removes controller and leaves one idle manager without late work");
                    continue;
                }
                if (scenario == 4 || scenario == 5)
                {
                    // Explicit result fixtures. Unlike the standalone natural
                    // corpus, these deliberately place actors at the death boundary.
                    var match = MatchCompositionRoot.Instance.MatchManager;
                    bot.Temperature.Value = 0;
                    if (scenario == 5) human.Temperature.Value = 0;
                    await Until(() => turn.TryGetPrepInputSnapshot(out var next) && next.Key != first.Key
                        && match.RoundNumber.Value == (scenario == 4 ? 2 : 1), 30, "injected death to next preparation");
                    Check(match.P1RoundWins.Value == (scenario == 4 ? 1 : 0) && match.P2RoundWins.Value == 0,
                        scenario == 4 ? "human round win counted once" : "simultaneous death replays the same 1v1 round without awarding a win");
                    turn.TryGetPrepInputSnapshot(out var reset);
                    Check(reset.TemperaturesAtStart.All(x => Math.Abs(x - 37) < .001), "win/draw round uses neutral reset before next Prep");
                    if (scenario == 4)
                    {
                        bot.Temperature.Value = 0;
                        await Until(() => match.CurrentMatchState.Value == MatchState.MatchComplete, 30, "human two-win match result");
                        Check(match.P1RoundWins.Value == 2 && match.RematchVoteEpoch.Value == 0, "human Bo3 win completes without online vote");
                    }
                    report.traces.AddRange(controller.Trace.Select(x => "scenario=" + scenario + " " + x));
                    await app.SessionRouter.StopAsync();
                    Check(app.SessionRouter.Current == null && controller == null, "injected result fixture fully cleaned up");
                    continue;
                }
                if (scenario == 2)
                {
                    await Until(() => controller.Trace.Any(x => x.Contains("think=8.000")), 5, "long optional thinking");
                    Check(!bot.HasSelectedItem.Value && !controller.Trace.Any(x => x.Contains("submit Pending")), "thinking has not queued or started item use");
                    bot.Temperature.Value = 4; // Deliberate urgency stimulus, not a balance/profile change.
                    await Until(() => controller.Trace.Any(x => x.Contains("submit Pending")) || bot.IsReady.Value, 1, "urgent cooling interrupts optional thinking");
                    Check(true, "live low temperature preempts an already-running optional think delay");
                }
                if (scenario == 1)
                {
                    await Until(() => controller.SelectionCount == 1, 5, "near-deadline item queued");
                    Check(!bot.IsReady.Value, "near-deadline policy does not immediately Ready");
                }
                await Until(() => bot.IsReady.Value, 25, "normal graph Ready");
                Check(controller.RunCount == 1 && controller.SelectionCount == 1 && controller.ReadyCount == 1,
                    "scenario " + scenario + " exactly one selection and Ready");
                Check(controller.RuntimeGraph != original.Bot.Graph && !original.Bot.Graph.IsRunning, "runtime graph isolated from source asset");
                if (scenario == 1)
                    Check(NetworkManager.Singleton.ServerTime.Time >= first.Deadline - .7
                        && NetworkManager.Singleton.ServerTime.Time <= first.Deadline + .5, "near-deadline Ready uses committed server deadline");
                if (scenario == 0)
                {
                    var slot = human.GetInventory().SlotStates[0]; human.SelectItemServerRpc(0, 1, slot.CopyId); human.PressReadyServerRpc();
                    await Until(() => turn.TryGetPrepInputSnapshot(out var next) && next.Key != first.Key && bot.IsReady.Value, 40, "zero-think second Prep");
                    turn.TryGetPrepInputSnapshot(out var second);
                    Check(controller.RunCount == 2 && controller.SelectionCount == 2 && controller.ReadyCount == 2,
                        "zero-think second turn starts once after committed setup");
                    Check(second.Key.Turn == 2 && second.StartTime > first.StartTime && second.RemainingSeconds > 0
                        && Math.Abs(second.RemainingSeconds - Math.Max(0, second.Deadline - NetworkManager.Singleton.ServerTime.Time)) < .1,
                        "second-turn observation has new key/start and current time instead of prior exhausted timer");
                    Check(controller.Trace.Count(x => x.Contains("think=0.000")) == 2, "both real graph turns use zero optional thinking");
                }
                report.traces.AddRange(controller.Trace.Select(x => "scenario=" + scenario + " " + x));
                await app.SessionRouter.StopAsync();
                Check(app.SessionRouter.Current == null && !NetworkManager.Singleton.IsListening && controller == null
                    && SceneManager.GetActiveScene().name == "LobbyScene", "scenario " + scenario + " clean stop");
            }
            Check(EditorJsonUtility.ToJson(original) == before && EditorJsonUtility.ToJson(original.Difficulty) == beforeDifficulty,
                "original encounter/difficulty unchanged by boundary fixtures");
            report.passed = report.errors.Count == 0;
        }
        catch (Exception error) { report.failure = error.ToString(); }
        finally
        {
            Application.targetFrameRate = oldFps;
            try { if (AppBootstrapper.Instance?.SessionRouter.Current != null) await AppBootstrapper.Instance.SessionRouter.StopAsync(); }
            catch (Exception error) { report.errors.Add("cleanup: " + error); report.passed = false; }
            foreach (var clone in clones) if (clone != null) UnityEngine.Object.Destroy(clone);
            Retained.Clear();
            session?.Dispose();
            Application.logMessageReceived -= Log;
            LastResult = JsonUtility.ToJson(report, true);
            Directory.CreateDirectory("output/validation/plan036/t10_20260929");
            File.WriteAllText("output/validation/plan036/t10_20260929/editor-boundary.json", LastResult); Running = false;
        }
    }
    static async Task Until(Func<bool> predicate, double seconds, string label)
    {
        double end = Time.realtimeSinceStartupAsDouble + seconds;
        while (!predicate())
        { if (Time.realtimeSinceStartupAsDouble >= end) throw new TimeoutException(label); await Task.Delay(20); }
    }
}
