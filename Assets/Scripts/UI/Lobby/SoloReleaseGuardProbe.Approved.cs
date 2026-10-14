#if PLAN036_RELEASE_GUARD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    public sealed partial class SoloReleaseGuardProbe
    {
        // Explicit current-build approval check. These are registered source assets,
        // the actual menu events and the unchanged AppBootstrapper coordinator/port.
        async Task<string[]> ValidateApprovedSourceMenu()
        {
            var checks = new List<string>();
            foreach (var id in new[] { "solo.encounter.beginner", "solo.encounter.advanced" })
            {
                var ui = FindObjectsByType<AZLobbyUI>(FindObjectsSortMode.None).Single(x => x.isActiveAndEnabled);
                var profiles = (SoloLobbyProfile[])typeof(AZLobbyUI).GetField("_soloProfiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                var matches = Enumerable.Range(0, profiles.Length).Where(i => profiles[i].Encounter != null && profiles[i].Encounter.ConfigId == id).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Source menu ID missing/ambiguous: " + id);
                var source = profiles[matches[0]].Encounter;
                var expectedDifficultyId = source.Difficulty.ConfigId;
                var context = (SoloConfigurationContext)typeof(AZLobbyUI).GetMethod("SoloContext", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ui, new object[] { source });
                if (!SoloSettingsResolver.TryResolve(source, context, out var resolved, out var errors)
                    || resolved.ProvisionalTuning || resolved.IsValidationFixture)
                    throw new InvalidOperationException("Approved source still blocked: " + id + ";" + string.Join(";", errors));

                var invalidSource = Instantiate(source);
                var invalidDifficulty = Instantiate(source.Difficulty);
                try
                {
                    SetField(invalidSource, "difficulty", invalidDifficulty);
                    var judgement = invalidDifficulty.Judgement;
                    judgement.BestPickChance = float.NaN;
                    SetField(invalidDifficulty, "judgement", judgement);
                    var rejection = await AppBootstrapper.Instance.SoloSession.StartAsync(invalidSource, context);
                    if (!rejection.IsFailure || AppBootstrapper.Instance.SessionRouter.Current != null || NetworkManager.Singleton.IsListening)
                        throw new InvalidOperationException("Invalid approved-profile copy bypassed the ordinary guard");
                    checks.Add(id + ": invalid copy still rejected before session admission");
                }
                finally { Destroy(invalidDifficulty); Destroy(invalidSource); }

                GameObject.Find("MainUI/MainPanel/SoloBtn").GetComponent<Button>().onClick.Invoke();
                var bindings = (SoloSelectionBindings)typeof(AZLobbyUI).GetField("_soloSelection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);
                var option = bindings.Content.GetComponentsInChildren<Button>(false).Single(x => x.name == "Profile" + matches[0]);
                if (!option.interactable) throw new InvalidOperationException("Approved menu option disabled");
                option.onClick.Invoke();
                if (!bindings.StartButton.interactable || bindings.Details.text.Contains("개발 조정 중"))
                    throw new InvalidOperationException("Approved source menu remains disabled/provisional");
                bindings.StartButton.onClick.Invoke();
                try
                {
                    await WaitForOrdinaryBotReady();
                    var app = AppBootstrapper.Instance;
                    var actual = app.SessionRouter.LaunchContext.Solo;
                    // Solo owns a child loopback transport; the manager's attached
                    // transport is the preserved online transport, not the live host.
                    var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport as UnityTransport;
                    Debug.Log("[ApprovedSolo] encounter=" + actual.EncounterId + ";difficulty=" + actual.DifficultyId
                        + ";provisional=" + actual.ProvisionalTuning + ";fixture=" + actual.IsValidationFixture
                        + ";port=" + (transport == null ? 0 : transport.ConnectionData.Port)
                        + ";humanConnections=" + NetworkManager.Singleton.ConnectedClients.Count
                        + ";UGS=" + new UnityServicesGateway().IsInitialized);
                    if (actual.EncounterId != id || actual.DifficultyId != expectedDifficultyId
                        || actual.ProvisionalTuning || actual.IsValidationFixture || transport == null || transport.ConnectionData.Port != 18777
                        || NetworkManager.Singleton.ConnectedClients.Count != 1 || new UnityServicesGateway().IsInitialized)
                        throw new InvalidOperationException("Approved source identity/default local transport mismatch");
                    var bot = FindObjectsByType<BotTurnController>(FindObjectsSortMode.None).Single();
                    checks.Add(id + ": actual menu StartAsync -> source " + actual.DifficultyId
                        + "; default port18777; one bot graph; run=" + bot.RunCount + "; select=" + bot.SelectionCount + "; ready=" + bot.ReadyCount);
                    if (id == "solo.encounter.advanced")
                    {
                        long generation = app.SessionRouter.Current.Generation;
                        var replay = await app.SoloSession.RestartAsync();
                        if (replay.IsFailure) throw new InvalidOperationException(replay.ErrorMessage);
                        await WaitForOrdinaryBotReady();
                        if (app.SessionRouter.Current.Generation <= generation || !ReferenceEquals(actual, app.SessionRouter.LaunchContext.Solo))
                            throw new InvalidOperationException("Approved source replay lost immutable settings/new generation");
                        checks.Add("Advanced approved-source replay: new generation, captured settings retained and one graph/Ready");
                    }
                }
                finally { await AppBootstrapper.Instance.SoloSession.StopAsync(); }
                if (AppBootstrapper.Instance.SessionRouter.Current != null || NetworkManager.Singleton.IsListening
                    || SceneManager.GetActiveScene().name != "LobbyScene")
                    throw new InvalidOperationException("Approved source failed normal stop/lobby return");
                checks.Add(id + ": normal StopAsync returns usable lobby with no lease/network");
                await Task.Delay(300);
            }
            checks.Add("Actual source approval used; no development entry, unsaved approval clone, alternate coordinator, transport override or UGS connection");
            return checks.ToArray();
        }
    }
}
#endif
