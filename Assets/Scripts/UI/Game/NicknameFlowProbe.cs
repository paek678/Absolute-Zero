#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.UI.Game.Bridge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game
{
    // Explicit end-to-end UI test. Saved user nickname is restored in the same
    // synchronous block as submission; only the test process keeps its test name.
    public sealed class NicknameFlowProbe : MonoBehaviour
    {
        string _folder, _role, _name;
        int _count;
        float _deadline;
        bool _done;
        static string Arg(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--az-nickname-flow") < 0) return;
            var go = new GameObject(nameof(NicknameFlowProbe)); DontDestroyOnLoad(go);
            go.AddComponent<NicknameFlowProbe>();
        }
        void Update()
        {
            if (!_done && Time.realtimeSinceStartup > _deadline && _deadline > 0) Fail("timeout");
        }
        void Fail(string message)
        {
            if (_done) return; _done = true;
            Debug.LogError("[NICKNAME] FAIL " + message);
            File.WriteAllText(Path.Combine(_folder, _role + ".failed"), message);
            Application.Quit(1);
        }
        bool Check(bool value, string message)
        {
            if (!value) Fail(message); else Debug.Log("[NICKNAME] PASS " + message);
            return value;
        }
        void Click(string path) => GameObject.Find(path).GetComponent<Button>().onClick.Invoke();
        string PathFor(string suffix) => Path.Combine(_folder, _role + suffix);
        string[] Names() => Enumerable.Range(0, _count).Select(i => "이름검증" + i).ToArray();
        IEnumerator Start()
        {
            _folder = Arg("--az-nickname-flow", ""); Directory.CreateDirectory(_folder);
            _role = Arg("--az-name-role", "0"); _name = "이름검증" + _role;
            _count = int.Parse(Arg("--az-name-count", "4"));
            _deadline = Time.realtimeSinceStartup + 180;
            while (AppBootstrapper.Instance?.IsReady != true || CosmeticProfileService.Instance == null) yield return null;
            yield return null; yield return null;
            var input = GameObject.Find("MainUI/MainPanel/NicknameBar").GetComponent<TMP_InputField>();
            bool had = PlayerPrefs.HasKey("player_nickname"); string saved = PlayerPrefs.GetString("player_nickname", "");
            try
            {
                var previous = CosmeticProfileService.Instance.Nickname;
                input.text = "a";
                input.onEndEdit.Invoke(input.text);
                Click("MainUI/MainPanel/" + (_count == 1 ? "SoloBtn" : "ArenaBtn"));
                if (!Check(input.gameObject.activeInHierarchy && input.text == "a"
                    && CosmeticProfileService.Instance.Nickname == previous, "invalid input keeps edit text and prevents stale-name navigation")) yield break;
                input.text = _name;
                // Deliberately omit onEndEdit: navigation must commit the visible text.
                Click("MainUI/MainPanel/" + (_count == 1 ? "SoloBtn" : "ArenaBtn"));
                if (!Check(CosmeticProfileService.Instance.Nickname == _name && PlayerPrefs.GetString("player_nickname") == _name,
                    "main input committed before navigation")) yield break;
            }
            finally
            {
                if (had) PlayerPrefs.SetString("player_nickname", saved); else PlayerPrefs.DeleteKey("player_nickname");
                PlayerPrefs.Save();
                File.WriteAllText(PathFor(".prefs-restored"), "restored");
            }
            var coordinator = NetworkSessionCoordinator.Instance;
            if (_count == 1)
            {
                // Use the actual serialized Solo selection start button.
                var bindings = FindFirstObjectByType<AbsoluteZero.UI.LobbyUI.SoloSelectionBindings>();
                bindings.StartButton.onClick.Invoke();
            }
            else
            {
                Click("MainUI/ModeSelectPanel/PanelBG/" + (_count == 2 ? "OneVsOneBtn" : "Multi4Btn"));
                string codePath = Path.Combine(_folder, "code.tmpdata");
                if (_role == "0")
                {
                    Click("MainUI/RoomPanel/PanelBG/CreateBtn");
                    while (coordinator.CurrentLobby == null && !_done) yield return null;
                    if (_done) yield break;
                    File.WriteAllText(codePath, coordinator.CurrentLobby.LobbyCode);
                }
                else
                {
                    while (!File.Exists(codePath) && !_done) yield return null;
                    if (_done) yield break;
                    GameObject.Find("MainUI/RoomPanel/PanelBG/JoinInput").GetComponent<TMP_InputField>().text = File.ReadAllText(codePath);
                    Click("MainUI/RoomPanel/PanelBG/JoinBtn");
                }
                while (!_done)
                {
                    var lobby = coordinator.CurrentLobby;
                    var labels = GameObject.Find("MainUI/RoomPanel/PanelBG/PlayerList")?.GetComponentsInChildren<TMP_Text>();
                    if (lobby?.Players?.Count == _count && Names().All(n => lobby.Players.Any(p =>
                            p.Data != null && p.Data.TryGetValue("PlayerName", out var v) && v.Value == n))
                        && labels != null && Names().All(n => labels.Any(t => t.text.StartsWith(n)))) break;
                    yield return null;
                }
                if (_done) yield break;
                Check(true, "UGS roster and visible room labels contain every submitted nickname");
                SoloLaunchProbe.WriteRender(PathFor(".room.png"));
                File.WriteAllText(PathFor(".room-ok"), "passed");
                if (_role == "0")
                {
                    while (!Enumerable.Range(0, _count).All(i => File.Exists(Path.Combine(_folder, i + ".room-ok")))) yield return null;
                    Click("MainUI/RoomPanel/PanelBG/StartBtn");
                }
            }
            PlayerState[] players = null;
            while (!_done)
            {
                players = FindObjectsByType<PlayerState>(FindObjectsSortMode.None).Where(p => p.IsSpawned && p.IsParticipantReady).ToArray();
                var bridge = FindFirstObjectByType<GameDataBridge>();
                var expected = _count == 1 ? 2 : _count;
                if (players.Length == expected && players.Any(p => p.DisplayNameText == _name)
                    && (_count == 1 || Names().All(n => players.Any(p => p.DisplayNameText == n)))
                    && bridge != null && bridge.CurrentMatch.CurrentPhase == AbsoluteZero.Core.Common.TurnPhase.PrepPhase
                    && players.All(p => bridge.TryGetSeat((byte)p.PlayerIndex, out var s) && s.DisplayName == p.DisplayNameText
                        && GameObject.Find("P" + p.PlayerIndex + "Name")?.GetComponent<TMP_Text>()?.text == p.DisplayNameText)) break;
                yield return null;
            }
            if (_done) yield break;
            var loading = AbsoluteZero.UI.Loading.LoadingScreenManager.Instance;
            var showing = typeof(AbsoluteZero.UI.Loading.LoadingScreenManager).GetField("_showing",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            while (loading != null && (bool)showing.GetValue(loading) && !_done) yield return null;
            if (_done) yield break;
            yield return new WaitForSecondsRealtime(2);
            while (Plan056CameraProbe.Requested && !Plan056CameraProbe.Completed && !_done) yield return null;
            while (Plan063DeathTimingProbe.Requested && !Plan063DeathTimingProbe.Completed && !_done) yield return null;
            Check(true, "network state, bridge and visible battle banners agree");
            string roster = string.Join(";", players.OrderBy(p => p.PlayerIndex).Select(p => p.PlayerIndex + ":" + p.DisplayNameText));
            File.WriteAllText(PathFor(".game-ok"), roster);
            SoloLaunchProbe.WriteRender(PathFor(".game.png"));
            Debug.Log("[NICKNAME] " + roster);
            if (_role == "0" || _count == 1)
            {
                while (!Enumerable.Range(0, _count).All(i => File.Exists(Path.Combine(_folder, i + ".game-ok")))) yield return null;
                yield return new WaitForSecondsRealtime(1);
                if (_count > 1)
                {
                    var leave = coordinator.LeaveAsync();
                    while (!leave.IsCompleted) yield return null;
                }
            }
            else yield return new WaitForSecondsRealtime(3);
            _done = true; Application.Quit(0);
        }
    }
}
#endif
