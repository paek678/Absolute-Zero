#if PLAN036_RELEASE_GUARD
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AbsoluteZero.UI.LobbyUI
{
    // Included only by the explicitly instrumented non-development guard build.
    // The production button and resolver compile without DEVELOPMENT_BUILD.
    public sealed class SoloReleaseGuardProbe : MonoBehaviour
    {
        [Serializable] sealed class Report { public bool passed; public string failure; public string[] checks; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--plan036-release-guard") >= 0)
                new GameObject("Release guard validation").AddComponent<SoloReleaseGuardProbe>();
        }
        async void Start()
        {
            var report = new Report();
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (AppBootstrapper.Instance?.IsReady != true)
                { if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("menu"); await Task.Delay(20); }
                if (Debug.isDebugBuild || typeof(SoloSessionCoordinator).GetMethod("StartDevelopmentAsync") != null)
                    throw new InvalidOperationException("Release probe must compile without the development entry.");
                string[] diagnosticTypes = {
                    "AbsoluteZero.Core.Solo.MatrixScenarioProbe", "AbsoluteZero.Core.Solo.VisualFourPlayerProbe",
                    "AbsoluteZero.Core.Solo.CosmeticNetworkProbe", "AbsoluteZero.Core.Solo.MultiScenarioProbe",
                    "AbsoluteZero.Core.Solo.ResourceLifetimeProbe", "AbsoluteZero.Core.Solo.SoloLaunchProbe",
                    "AbsoluteZero.Core.Solo.SoloMatchProbe", "AbsoluteZero.Core.Solo.SoloModeTransitionProbe",
                    "AbsoluteZero.UI.LobbyUI.ClosetValidationProbe", "AbsoluteZero.UI.LobbyUI.ClosetLayoutValidationProbe",
                    "AbsoluteZero.UI.LobbyUI.SettingsValidationProbe", "AbsoluteZero.UI.Game.SettingsGameplayProbe",
                    "AbsoluteZero.UI.LobbyUI.SoloSelectionValidationProbe"
                };
                foreach (var name in diagnosticTypes)
                    if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType(name, false) != null))
                        throw new InvalidOperationException("Development diagnostic leaked into non-development build: " + name);
                await Task.Delay(500);
                var encounter = Resources.FindObjectsOfTypeAll<SoloDuelDefinitionSO>().Single(x => x.ConfigId == "solo.encounter.default");
                // This UI-only diagnostic does not add a production UI -> Behavior
                // assembly dependency merely to inspect the retained graph.
                var graph = typeof(BotDefinitionSO).GetProperty("Graph").GetValue(encounter.Bot);
                bool graphValid = (bool)typeof(SoloSettingsResolver).GetMethod("HasCompiledRoot").Invoke(null, new[] { graph });
                if (!graphValid) throw new InvalidOperationException("Release graph lost compiled root/types");
                var button = GameObject.Find("MainUI/MainPanel/SoloBtn").GetComponent<Button>(); button.onClick.Invoke();
                await Task.Delay(500);
                bool message = FindObjectsByType<TextMeshProUGUI>(FindObjectsSortMode.None).Any(x => x.text.Contains("Provisional tuning"));
                var confirm = GameObject.Find("MainUI/SoloSelectionPanel/Body/StartBtn").GetComponent<Button>();
                if (confirm.interactable) throw new InvalidOperationException("Unapproved profile became selectable for release launch.");
                confirm.onClick.Invoke();
                GameObject.Find("MainUI/SoloSelectionPanel/Body/BackBtn").GetComponent<Button>().onClick.Invoke();
                await Task.Delay(100);
                if (!message || !button.interactable || AppBootstrapper.Instance.SessionRouter.Current != null
                    || NetworkManager.Singleton.IsListening || new UnityServicesGateway().IsInitialized
                    || NetworkSessionCoordinator.Instance.State != SessionState.Offline
                    || SceneManager.GetActiveScene().name != "LobbyScene") throw new InvalidOperationException("Provisional release launch was not safely rejected with usable menu.");
                report.passed = true;
                report.checks = new[] { "Non-development compilation; development launch API absent", diagnosticTypes.Length + " development diagnostic types absent even with diagnostic CLI flags", "Production graph compiled root/types retained",
                    "Real Solo button rejects unapproved provisional tuning", "Menu restored; no session/network/UGS start" };
            }
            catch (Exception error) { report.failure = error.ToString(); }
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "--plan036-output");
            string directory = i >= 0 ? args[i + 1] : Application.persistentDataPath; Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "release-guard.json"), JsonUtility.ToJson(report, true));
            Application.Quit(report.passed ? 0 : 2);
        }
    }
}
#endif
