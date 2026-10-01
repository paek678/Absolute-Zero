using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Explicit Play Mode lifecycle stimuli; never installed in a player or scene.
public static class Plan036ExitValidation
{
    [Serializable] sealed class Report
    {
        public bool passed;
        public List<string> checks = new(), errors = new();
        public string failure;
    }
    public static bool Running { get; private set; }
    static SoloDuelDefinitionSO _encounter;
    public static string Start()
    {
        if (Running || !EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode in LobbyScene first.");
        Running = true; Run(); return "started";
    }
    static async void Run()
    {
        var report = new Report(); SoloSessionCoordinator session = null;
        void Log(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception) report.errors.Add(text); }
        void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); report.checks.Add(label); }
        Application.logMessageReceived += Log;
        try
        {
            await Until(() => AppBootstrapper.Instance?.IsReady == true, 15, "app ready");
            var app = AppBootstrapper.Instance; var manager = NetworkManager.Singleton;
            _encounter = AssetDatabase.LoadAssetAtPath<SoloDuelDefinitionSO>(Plan036ConfigurationBuilder.DraftPath);
            var context = new SoloConfigurationContext(_encounter.SharedOneVsOneRule, Plan036ConfigurationBuilder.ReadCurrentDuelCatalog(),
                AssetDatabase.LoadAssetAtPath<CosmeticRegistrySO>("Assets/Data/Cosmetics/CosmeticRegistry.asset"));
            session = new SoloSessionCoordinator(app.SessionRouter, new NgoLocalHostRuntime(manager), 18778);
            var stages = new[] { TurnPhase.PrepPhase, TurnPhase.AttackPhase, TurnPhase.ResolutionPhase, TurnPhase.RoundOver };
            foreach (var stage in stages)
            {
                Check((await session.StartDevelopmentAsync(_encounter, context)).IsSuccess, stage + " entry");
                await Until(() => TurnManager.Instance != null && TurnManager.Instance.TryGetPrepInputSnapshot(out _), 35, "committed Prep");
                var turn = TurnManager.Instance; var bot = turn.GetPlayer(1); var human = turn.GetPlayer(0);
                var controller = UnityEngine.Object.FindAnyObjectByType<BotTurnController>();
                if (stage == TurnPhase.PrepPhase)
                {
                    await Until(() => controller.Trace.Any(x => x.Contains("submit Pending")), 5, "pending real graph item");
                    Check(!bot.IsReady.Value && controller.SelectionCount == 0, "exit while real graph item delay is pending");
                }
                else
                {
                    if (stage == TurnPhase.RoundOver) bot.Temperature.Value = 0; // Labeled death-boundary fixture only.
                    else
                    {
                        var copy = human.GetInventory().SlotStates[0];
                        human.SelectItemServerRpc(0, 1, copy.CopyId); human.PressReadyServerRpc();
                    }
                    await Until(() => turn.CurrentPhase.Value == stage, 30, "reach " + stage);
                    Check(turn.CurrentPhase.Value == stage, "exit reached " + stage);
                }
                await app.SessionRouter.StopAsync();
                await Task.Delay(1200);
                Check(app.SessionRouter.Current == null && !manager.IsListening && !manager.ShutdownInProgress
                    && controller == null && bot == null && human == null && turn == null
                    && UnityEngine.Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length == 1
                    && SceneManager.GetActiveScene().name == "LobbyScene", stage + " fully cleaned without late work");
            }
            Check((await session.StartDevelopmentAsync(_encounter, context)).IsSuccess, "fresh entry after all phase exits");
            await Until(() => TurnManager.Instance != null && TurnManager.Instance.GetPlayer(1)?.IsReady.Value == true, 35, "fresh graph Ready");
            var fresh = UnityEngine.Object.FindAnyObjectByType<BotTurnController>();
            Check(fresh.RunCount == 1 && fresh.SelectionCount == 1 && fresh.ReadyCount == 1, "new generation starts exactly one unaffected graph");
            await app.SessionRouter.StopAsync();
            report.passed = report.errors.Count == 0;
        }
        catch (Exception error) { report.failure = error.ToString(); }
        finally
        {
            try { if (AppBootstrapper.Instance?.SessionRouter.Current != null) await AppBootstrapper.Instance.SessionRouter.StopAsync(); }
            catch (Exception error) { report.errors.Add("cleanup: " + error); report.passed = false; }
            session?.Dispose(); _encounter = null;
            Application.logMessageReceived -= Log;
            Directory.CreateDirectory("output/validation/plan036/t10_20260929");
            File.WriteAllText("output/validation/plan036/t10_20260929/editor-phase-exits.json", JsonUtility.ToJson(report, true));
            Running = false;
        }
    }
    static async Task Until(Func<bool> predicate, double seconds, string label)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + seconds;
        while (!predicate())
        { if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException(label); await Task.Delay(20); }
    }
}
