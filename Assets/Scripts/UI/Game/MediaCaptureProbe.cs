#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.UI.LobbyUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game
{
    // Explicit recording fixture. Uses shipped UI/presentation and never saves an outfit.
    public sealed class MediaCaptureProbe : MonoBehaviour
    {
        string _mode, _folder;
        static string Arg(string key, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (Application.isEditor || Arg("--az-media").Length == 0) return;
            var go = new GameObject(nameof(MediaCaptureProbe)); DontDestroyOnLoad(go);
            go.AddComponent<MediaCaptureProbe>();
        }
        async void Start()
        {
            _mode = Arg("--az-media"); _folder = Path.GetFullPath(Arg("--az-media-output"));
            Directory.CreateDirectory(_folder); Application.runInBackground = true;
            try
            {
                if (_mode == "timed")
                {
                    await Task.Delay(90000); Mark("COMPLETE"); Application.Quit(0); return;
                }
                await Until(() => AppBootstrapper.Instance?.IsReady == true && GameObject.Find("MainUI/MainPanel") != null, "lobby");
                await Task.Delay(2500);
                if (_mode == "environment") await Environments();
                else if (_mode == "closet") await Closet();
                else if (_mode.StartsWith("join-")) await Join();
                else throw new InvalidOperationException("Unknown media mode");
                Mark("COMPLETE"); await Task.Delay(1500); Application.Quit(0);
            }
            catch (Exception error) { Debug.LogError("[MEDIA] " + error); Mark("FAILED " + error.Message); Application.Quit(2); }
        }
        async Task Until(Func<bool> condition, string label, int seconds = 60)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException(label);
                await Task.Delay(100);
            }
        }
        void Click(string path)
        {
            var button = GameObject.Find(path)?.GetComponent<Button>();
            if (button == null || !button.interactable) throw new InvalidOperationException("Unavailable button " + path);
            button.onClick.Invoke(); Mark("CLICK " + path);
        }
        void Mark(string label)
        {
            string line = Time.realtimeSinceStartupAsDouble.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + label;
            File.AppendAllText(Path.Combine(_folder, _mode + ".timeline.txt"), line + "\n"); Debug.Log("[MEDIA] " + line);
        }
        async Task Environments()
        {
            Click("MainUI/MainPanel/SoloBtn"); await Task.Delay(1000);
            Click("MainUI/SoloSelectionPanel/Body/StartBtn");
            await Until(() => TurnManager.Instance != null && TurnManager.Instance.IsSpawned
                && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase, "solo prep");
            await Task.Delay(1800);
            foreach (var env in new[] { EnvironmentType.SunnyDay, EnvironmentType.Kids, EnvironmentType.HeatWaveWarning })
            {
                var turn = TurnManager.Instance;
                if (!turn.IsServer) throw new InvalidOperationException("Environment capture requires local authority");
                turn.ActiveEnvironment.Value = env;
                typeof(TurnManager).GetMethod("PublishEnvironment", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(turn, new object[] { env });
                Mark("ENV " + env);
                await Task.Delay(1700);
                ScreenCapture.CaptureScreenshot(Path.Combine(_folder, env + ".png"));
                await Task.Delay(2600);
                ScreenCapture.CaptureScreenshot(Path.Combine(_folder, env + "-wide.png"));
                await Task.Delay(700);
            }
        }
        sealed class MemoryStore : ICosmeticStore
        {
            string _json = "";
            public string Read() => _json;
            public void Write(string json) => _json = json;
        }
        async Task Closet()
        {
            var profile = CosmeticProfileService.Instance;
            var equipment = new CosmeticEquipmentService(profile.EquipState, profile.Registry, new MemoryStore());
            typeof(CosmeticProfileService).GetField("<Equipment>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(profile, equipment);
            equipment.Apply(new CosmeticDto());
            Click("MainUI/MainPanel/ClosetBtn"); await Task.Delay(1200);
            var view = FindFirstObjectByType<ClosetViewBindings>();
            Mark("OUTFITS_BEGIN");
            for (int n = 0; n < 8; n++)
            {
                foreach (var part in new[] { CosmeticPart.Head, CosmeticPart.Top, CosmeticPart.Back, CosmeticPart.Tail })
                {
                    var items = equipment.GetItems(part).Where(item => item != null
                        && item.DisplayName.IndexOf("Template", StringComparison.OrdinalIgnoreCase) < 0).ToArray();
                    if (items.Length == 0) continue;
                    view.Tabs[(int)part].onClick.Invoke(); await Task.Delay(90);
                    var item = items[n % items.Length];
                    var cell = view.Content.Find("Item_" + item.Id).GetComponent<ClosetItemCellBindings>();
                    cell.Button.onClick.Invoke(); await Task.Delay(90);
                    if (view.Equip.interactable) view.Equip.onClick.Invoke();
                }
                Mark("OUTFIT " + n); await Task.Delay(900);
                ScreenCapture.CaptureScreenshot(Path.Combine(_folder, "outfit-" + n + ".png"));
            }
            Mark("OUTFITS_END"); await Task.Delay(600); view.Close.onClick.Invoke(); await Task.Delay(1500);
        }
        async Task Join()
        {
            bool host = _mode == "join-host";
            string codePath = Arg("--az-media-code");
            string nickname = host ? "말랑눈꽃" : "구름친구";
            // Ephemeral capture identity; avoid writing the user's persistent nickname.
            typeof(CosmeticProfileService).GetField("_nickname", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(CosmeticProfileService.Instance, nickname);
            GameObject.Find("MainUI/MainPanel/NicknameBar").GetComponent<TMP_InputField>().SetTextWithoutNotify(nickname);
            Mark("JOIN_BEGIN"); await Task.Delay(1200);
            bool hadNickname = PlayerPrefs.HasKey("player_nickname");
            string savedNickname = PlayerPrefs.GetString("player_nickname", "");
            Click("MainUI/MainPanel/ArenaBtn");
            // Navigation commits its field. Restore immediately; keep only the in-memory demo identity.
            if (hadNickname) PlayerPrefs.SetString("player_nickname", savedNickname);
            else PlayerPrefs.DeleteKey("player_nickname");
            PlayerPrefs.Save();
            await Task.Delay(1800);
            Click("MainUI/ModeSelectPanel/PanelBG/OneVsOneBtn"); await Task.Delay(1800);
            if (host)
            {
                Click("MainUI/RoomPanel/PanelBG/CreateBtn");
                await Until(() => LobbyManager.Instance?.CurrentLobby != null, "create lobby");
                File.WriteAllText(codePath, LobbyManager.Instance.CurrentLobby.LobbyCode);
                await Until(() => LobbyManager.Instance.CurrentLobby?.Players?.Count == 2, "second player", 110);
                await Task.Delay(4000); Click("MainUI/RoomPanel/PanelBG/StartBtn");
            }
            else
            {
                await Until(() => File.Exists(codePath), "room code");
                string code = File.ReadAllText(codePath).Trim();
                var input = GameObject.Find("MainUI/RoomPanel/PanelBG/JoinInput").GetComponent<TMP_InputField>();
                input.ActivateInputField(); input.text = "";
                foreach (char c in code) { input.text += c; await Task.Delay(250); }
                input.DeactivateInputField(); await Task.Delay(1000);
                Click("MainUI/RoomPanel/PanelBG/JoinBtn");
                await Until(() => LobbyManager.Instance?.CurrentLobby != null, "join lobby"); Mark("JOINED");
            }
            await Until(() => TurnManager.Instance != null && TurnManager.Instance.IsSpawned
                && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase, "game started", 110);
            Mark("IN_GAME"); await Task.Delay(6000);
        }
    }
}
#endif
