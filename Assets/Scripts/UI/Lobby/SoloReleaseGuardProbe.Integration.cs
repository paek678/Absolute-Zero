#if PLAN036_RELEASE_GUARD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed partial class SoloReleaseGuardProbe
    {
        // Instrumented guard build only. Normal release resolver/coordinator are used;
        // approval is simulated on unsaved copies, never on production assets.
        async Task<string[]> ValidateIntegratedProfiles()
        {
            var checks = new List<string>();
            var ids = new[] { "solo.encounter.beginner", "solo.encounter.advanced" };
            SoloDuelDefinitionSO Current(string id) => Resources.FindObjectsOfTypeAll<SoloDuelDefinitionSO>()
                .Single(x => x.ConfigId == id);
            var original = ids.ToDictionary(id => id, id => Current(id).Difficulty.ProvisionalTuning);
            if (typeof(AppBootstrapper).GetMethod("ResolveDevelopmentSoloPort", BindingFlags.Static | BindingFlags.NonPublic) != null
                || typeof(AZLobbyUI).GetMethod("TrySelectEncounter") != null)
                throw new InvalidOperationException("Development port/selection seam leaked into ordinary guard build.");
            using var coordinator = new SoloSessionCoordinator(AppBootstrapper.Instance.SessionRouter,
                new NgoLocalHostRuntime(NetworkManager.Singleton), 18878);
            foreach (var id in ids)
            {
                var copies = new List<UnityEngine.Object>();
                try
                {
                    // A normal menu return unloads unused assets. Async local references
                    // are not asset leases; read this newly loaded lobby's definitions.
                    var source = Current(id);
                    var ui = FindObjectsByType<AZLobbyUI>(FindObjectsSortMode.None).Single(x => x.isActiveAndEnabled);
                    var context = (SoloConfigurationContext)typeof(AZLobbyUI)
                        .GetMethod("SoloContext", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, new object[] { source });
                    bool resolved = SoloSettingsResolver.TryResolve(source, context, out _, out var errors);
                    if (resolved
                        || !errors.Any(x => x.Contains("Provisional")))
                        throw new InvalidOperationException("Unapproved integrated profile guard mismatch: " + source.ConfigId
                            + ";resolved=" + resolved + ";errors=" + string.Join("; ", errors));

                    T Copy<T>(T asset) where T : UnityEngine.Object
                    { var clone = Instantiate(asset); copies.Add(clone); return clone; }
                    var encounter = Copy(source);
                    var bot = Copy(source.Bot);
                    var difficulty = Copy(source.Difficulty);
                    var policy = Copy(source.Bot.ItemUsePolicy);
                    var alternative = source.Difficulty.ItemUseOverride == null ? null
                        : source.Difficulty.ItemUseOverride == source.Bot.ItemUsePolicy ? policy : Copy(source.Difficulty.ItemUseOverride);
                    SetField(encounter, "bot", bot); SetField(encounter, "difficulty", difficulty);
                    SetField(bot, "itemUsePolicy", policy); SetField(difficulty, "itemUseOverride", alternative);
                    SetField(difficulty, "provisionalTuning", false); SetField(policy, "provisionalTuning", false);
                    if (alternative != null) SetField(alternative, "provisionalTuning", false);
                    if (!SoloSettingsResolver.TryResolve(encounter, context, out var approved, out errors)
                        || approved.ProvisionalTuning || approved.IsValidationFixture)
                        throw new InvalidOperationException("Copied approval path failed: " + string.Join("; ", errors));

                    var judgement = difficulty.Judgement;
                    var invalid = judgement; invalid.BestPickChance = float.NaN;
                    SetField(difficulty, "judgement", invalid);
                    var rejected = await coordinator.StartAsync(encounter, context);
                    if (!rejected.IsFailure || AppBootstrapper.Instance.SessionRouter.Current != null || NetworkManager.Singleton.IsListening)
                        throw new InvalidOperationException("Invalid copied profile started a release session.");
                    SetField(difficulty, "judgement", judgement);

                    var started = await coordinator.StartAsync(encounter, context);
                    if (started.IsFailure) throw new InvalidOperationException(started.ErrorMessage);
                    await WaitForOrdinaryBotReady();
                    var router = AppBootstrapper.Instance.SessionRouter;
                    var captured = router.LaunchContext.Solo;
                    if (captured.EncounterId != source.ConfigId || captured.DifficultyId != source.Difficulty.ConfigId
                        || captured.ProvisionalTuning || NetworkManager.Singleton.ConnectedClients.Count != 1
                        || new UnityServicesGateway().IsInitialized)
                        throw new InvalidOperationException("Ordinary identity/local ownership mismatch.");
                    if (source.ConfigId == "solo.encounter.advanced")
                    {
                        long generation = router.Current.Generation;
                        var replayed = await coordinator.RestartAsync();
                        if (replayed.IsFailure) throw new InvalidOperationException(replayed.ErrorMessage);
                        await WaitForOrdinaryBotReady();
                        if (router.Current.Generation <= generation || !ReferenceEquals(captured, router.LaunchContext.Solo))
                            throw new InvalidOperationException("Ordinary replay did not retain immutable settings/fresh generation.");
                        checks.Add("Advanced ordinary replay retains captured settings and has a new generation/one bot graph.");
                    }
                    await coordinator.StopAsync();
                    if (router.Current != null || NetworkManager.Singleton.IsListening || SceneManager.GetActiveScene().name != "LobbyScene")
                        throw new InvalidOperationException("Ordinary copied profile failed normal stop/lobby return.");
                    checks.Add(source.ConfigId + ": provisional rejected; invalid copy rejected; unsaved approved copy actually starts graph/Ready and returns normally.");
                }
                finally
                {
                    await coordinator.StopAsync();
                    foreach (var copy in copies) if (copy != null) Destroy(copy);
                }
            }
            if (ids.Select(Current).Any(x => x.Difficulty.ProvisionalTuning != original[x.ConfigId]
                || !x.Bot.ItemUsePolicy.ProvisionalTuning || !x.Difficulty.ItemUseOverride.ProvisionalTuning))
                throw new InvalidOperationException("Original production approval flags changed.");
            checks.Add("Production assets stay provisional; no persistent tuning promotion or guard bypass.");
            return checks.ToArray();
        }

        static void SetField(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(instance.GetType().Name, name);
            field.SetValue(instance, value);
        }

        static async Task WaitForOrdinaryBotReady()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 25;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                var controllers = FindObjectsByType<BotTurnController>(FindObjectsSortMode.None);
                if (controllers.Length == 1 && controllers[0].RunCount > 0 && controllers[0].SelectionCount > 0 && controllers[0].ReadyCount > 0
                    && FindObjectsByType<PlayerState>(FindObjectsSortMode.None).Count(x => x.IsSpawned) == 2)
                    return;
                await Task.Delay(20);
            }
            throw new TimeoutException("Ordinary accepted profile did not execute one real bot graph/Ready with two logical participants.");
        }
    }
}
#endif
