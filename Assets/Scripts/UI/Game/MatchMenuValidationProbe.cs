#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Turn;
using AbsoluteZero.UI.MiniGame;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AbsoluteZero.UI.Game
{
    // Opt-in development test. Actual InputSystem/UI input; explicit controlled readiness.
    public sealed class MatchMenuValidationProbe : MonoBehaviour
    {
        [Serializable] sealed class Report
        {
            public bool passed;
            public int seat;
            public string scenario;
            public List<string> checks = new(), views = new(), captures = new();
            public string failure;
        }
        readonly Report _report = new();
        Keyboard _keyboard;
        Mouse _mouse;
        InputSettings _original, _input;
        readonly List<InputDevice> _physical = new();
        string _output;
        static string Arg(string key, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (Arg("--plan043-menu-case").Length > 0)
                new GameObject("Match menu validation").AddComponent<MatchMenuValidationProbe>();
        }
        async void Start()
        {
            DontDestroyOnLoad(gameObject); Application.runInBackground = true;
            _output = Path.ChangeExtension(Application.consoleLogPath, ".menu.json");
            _report.scenario = Arg("--plan043-menu-case");
            try { await Run(); _report.passed = true; }
            catch (Exception e) { _report.failure = e.ToString(); Debug.LogError("[MENU] FAIL " + e.Message); }
            File.WriteAllText(_output, JsonUtility.ToJson(_report, true));
            Debug.Log("[MENU] " + (_report.passed ? "PASS" : "FAILED") + " checks=" + _report.checks.Count);
            await Task.Delay(500);
            Application.Quit(_report.passed ? 0 : 2);
        }
        PlayerState[] Players() => FindObjectsByType<PlayerState>(FindObjectsSortMode.None)
            .Where(p => p.IsSpawned && p.IsParticipantReady).OrderBy(p => p.PlayerIndex).ToArray();
        async Task Run()
        {
            _original = InputSystem.settings; _input = Instantiate(_original);
            _input.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings = _input;
            foreach (var d in InputSystem.devices) if (d.enabled && (d is Mouse || d is Keyboard)) _physical.Add(d);
            foreach (var d in _physical) InputSystem.DisableDevice(d);
            _keyboard = InputSystem.AddDevice<Keyboard>(); _mouse = InputSystem.AddDevice<Mouse>();
            _keyboard.MakeCurrent(); _mouse.MakeCurrent();
            bool solo = _report.scenario == "solo";
            if (solo)
            {
                await Until(() => GameObject.Find("MainUI/MainPanel/SoloBtn") != null, "lobby Solo button");
                GameObject.Find("MainUI/MainPanel/SoloBtn").GetComponent<Button>().onClick.Invoke();
                await Task.Delay(100);
                GameObject.Find("MainUI/SoloSelectionPanel/Body/StartBtn").GetComponent<Button>().onClick.Invoke();
            }
            int count = solo ? 2 : int.Parse(Arg("--az-count", "4"));
            await Until(() => GameUIRoot.Instance?.Commands != null && Players().Length == count
                && Players().All(p => p.GetComponent<AZPlayerVisual>().IsPresentationReady)
                && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase
                && IceboxController.Instance?.IsAnimating != true
                && GameObject.Find("LoadingScreenCanvas") == null, "all participants and gameplay views ready", 65);
            var local = Players().First(p => p.IsOwner && !p.IsBotControlled);
            _report.seat = local.PlayerIndex;
            var menu = GameUIRoot.Instance.GetComponent<MatchMenu>();
            Check(menu != null, "menu exists in this mode");
            ValidateSeating(local.PlayerIndex, count > 2);
            await Capture("seating");
            float scale = Time.timeScale, before = Time.time;
            int remaining = TurnManager.Instance.RemainingTime.Value;
            await PressKey(Key.Escape);
            Check(menu.IsOpen && LocalGameplayInput.IsBlocked, "Escape opens input-blocking menu");
            Check(!GameUIRoot.Instance.Commands.TrySelectItem(0, local.GetInventory().SlotStates[0].CopyId), "menu blocks item submission");
            if (!local.IsReady.Value)
            {
                await PressKey(Key.Space);
                Check(!local.IsReady.Value, "menu blocks Ready key");
            }
            await Task.Delay(2200);
            Check(Time.timeScale == scale && Time.time - before > 2f, "menu does not pause scaled game time");
            Check(TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase
                || TurnManager.Instance.RemainingTime.Value < remaining, "preparation clock continues");
            await Capture("menu");
            await Click(menu.transform.Find("MatchMenuCanvas/InputBlocker/Dialog/Continue").GetComponent<Button>());
            Check(!menu.IsOpen && !LocalGameplayInput.IsBlocked, "Continue button closes menu without input leak");
            await PressKey(Key.Escape); await PressKey(Key.Escape);
            Check(!menu.IsOpen, "repeated Escape closes menu");
            await Barrier("menu-ready", solo ? new[] { 0 } : Enumerable.Range(0, count).ToArray());
            int departing = solo ? 0 : int.Parse(Arg("--plan043-exit-seat", "1"));
            if (_report.scenario == "attack")
            {
                GameUIRoot.Instance.Commands.PressReady();
                await Until(() => TurnManager.Instance.CurrentPhase.Value == TurnPhase.AttackPhase, "attack before leaving");
            }
            if (_report.scenario == "ghost")
            {
                if (NetworkManager.Singleton.IsServer) MatchCompositionRoot.Instance.Roster.SetLifeState((byte)departing, LifeState.Ghost);
                await Until(() => Players().Any(p => p.PlayerIndex == departing && p.CurrentLifeState.Value == LifeState.Ghost), "ghost departure setup");
            }
            if (_report.scenario == "mini")
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    short hug = (short)Array.FindIndex(ItemManager.Instance.GetAllItems(), i => i.ItemName == "Hug T-shirt");
                    Check(hug >= 0, "mini-game fixture item exists");
                    var inventory = Players().First(p => p.PlayerIndex == departing).GetInventory();
                    inventory.SlotStates.Clear(); // Deterministic fixture; normal grants may start with four full slots.
                    Check(inventory.GrantSpecificItem(hug), "mini-game fixture grant accepted");
                }
                string started = Path.Combine(Path.GetDirectoryName(Application.consoleLogPath), "mini-start.ready");
                if (local.PlayerIndex == departing)
                {
                    int slot = -1;
                    await Until(() =>
                    {
                        var inventory = local.GetInventory();
                        for (int i = 0; i < inventory.SlotStates.Count; i++)
                            if (ItemManager.Instance.GetItemData(inventory.SlotStates[i].ItemId)?.ItemName == "Hug T-shirt") { slot = i; return true; }
                        return false;
                    }, "mini-game item replicated");
                    Check(GameUIRoot.Instance.Commands.TrySelectItemWithTarget((byte)slot, (byte)(departing == 0 ? 1 : 0),
                        local.GetInventory().SlotStates[slot].CopyId), "mini-game selection sent");
                    await Until(() => MiniGameHub.IsRunning, "mini-game active before menu");
                    File.WriteAllText(started, "active");
                }
                else await Until(() => File.Exists(started), "remote mini-game started");
            }
            if (_report.scenario == "result")
            {
                var match = MatchCompositionRoot.Instance;
                if (NetworkManager.Singleton.IsServer)
                {
                    for (int i = 0; i < 4; i++) match.NetworkState.ServerAddKill(0);
                    match.Roster.SetLifeState(0, LifeState.Ghost);
                    Players().First(p => p.PlayerIndex == 1).Temperature.Value = 2;
                    GameUIRoot.Instance.Commands.UseGhostSkill(0, 1, 0x80004301u);
                }
                await Until(() => match.NetworkState.TerminalResult.Value.Released, "natural ghost kill releases match result");
                await Until(() => FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .Any(b => b.name == "LobbyBtn" && b.interactable), "match result buttons visible");
                await Capture("result-before-menu");
            }
            if (local.PlayerIndex == departing)
            {
                await PressKey(Key.Escape); Check(menu.IsOpen, "open at departure boundary");
                if (_report.scenario == "result") await Capture("menu-over-result");
                if (_report.scenario == "mini")
                {
                    Check(MiniGameHub.IsRunning, "menu coexists with running mini-game");
                    await Capture("menu-over-minigame");
                }
                await Click(menu.transform.Find("MatchMenuCanvas/InputBlocker/Dialog/Leave").GetComponent<Button>());
                await Returned(); return;
            }
            if (count == 4 && departing != 0)
            {
                await Until(() => Players().Length == 3, "4 to 3 despawn replicated");
                await Task.Delay(1000);
                Check(SceneManager.GetActiveScene().name == "GameScene_Multi" && NetworkManager.Singleton.IsConnectedClient,
                    "remaining three stay in match");
                var views = MatchCompositionRoot.Instance.ViewBindings;
                int empty = AZPlayerVisual.GetRemoteVisualSlot(departing, local.PlayerIndex);
                Check(views.GetRemoteVisual(empty)?.gameObject.activeSelf == false, "departed character including gear hidden");
                ValidateSeating(local.PlayerIndex, true);
                await Capture("after-departure");
                int turn = TurnManager.Instance.TurnNumber.Value;
                if (_report.scenario == "attack")
                    await Until(() => TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase, "interrupted attack completes");
                else
                {
                    GameUIRoot.Instance.Commands.PressReady();
                    await Until(() => TurnManager.Instance.TurnNumber.Value > turn
                        && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase, "three-player combat and next turn", 50);
                }
                Check(NetworkManager.Singleton.IsConnectedClient, "three-player progress keeps connection");
                await Barrier("three-continue", Enumerable.Range(0, count).Where(s => s != departing).ToArray());
                if (_report.scenario == "round")
                {
                    int round = MatchCompositionRoot.Instance.MatchManager.RoundNumber.Value;
                    if (NetworkManager.Singleton.IsServer)
                        foreach (var player in Players())
                            if (player.PlayerIndex != 0) player.Temperature.Value = 0; // Natural death/round reset path.
                    await Until(() => MatchCompositionRoot.Instance.MatchManager.RoundNumber.Value > round
                        && TurnManager.Instance.CurrentPhase.Value == TurnPhase.PrepPhase
                        && Players().All(p => p.CurrentLifeState.Value == LifeState.Alive), "next round revives only remaining players", 50);
                    Check(Players().Length == 3 && views.GetRemoteVisual(empty)?.gameObject.activeSelf == false,
                        "departed seat stays absent after round reset");
                    if (NetworkManager.Singleton.IsServer)
                        Check(!MatchCompositionRoot.Instance.Roster.IsActive((byte)departing), "round reset keeps departed roster inactive");
                    ValidateSeating(local.PlayerIndex, true);
                    await Capture("next-round");
                    await Barrier("round-continue", Enumerable.Range(0, count).Where(s => s != departing).ToArray());
                }
                int second = departing == 1 ? 2 : 1;
                if (local.PlayerIndex == second)
                {
                    await PressKey(Key.Escape);
                    await Click(menu.transform.Find("MatchMenuCanvas/InputBlocker/Dialog/Leave").GetComponent<Button>());
                }
            }
            await Returned();
        }
        void ValidateSeating(int local, bool multi)
        {
            var views = MatchCompositionRoot.Instance.ViewBindings;
            foreach (var p in Players())
            {
                if (p.PlayerIndex == local) continue;
                int slot = multi ? (p.PlayerIndex - local + 4) % 4 - 1 : 0;
                var root = p.GetComponent<AZPlayerVisual>().GetVisualRoot();
                Check(root != null && root == views.GetRemoteVisual(slot), "seat " + p.PlayerIndex + " binds expected slot " + slot);
                Check(root.GetComponent<PlayerSeatMarker>()?.Player == p, "target identity agrees with visual");
                _report.views.Add($"local={local} remote={p.PlayerIndex} slot={slot} position={root.position}");
            }
        }
        async Task Returned()
        {
            await Until(() => SceneManager.GetActiveScene().name == "LobbyScene"
                && AppBootstrapper.Instance?.SessionRouter.Current == null && !NetworkManager.Singleton.IsListening,
                "returned to lobby with released session", 25);
            Check(GameObject.Find("MainUI/MainPanel/SoloBtn")?.GetComponent<Button>().interactable == true, "lobby usable");
            await Task.Delay(150);
            Check(!LocalGameplayInput.IsBlocked, "modal lock released after scene exit");
            await Capture("returned");
        }
        async Task Barrier(string stage, int[] seats)
        {
            string folder = Path.GetDirectoryName(Application.consoleLogPath);
            File.WriteAllText(Path.Combine(folder, stage + "-" + _report.seat + ".ready"), "observed");
            await Until(() => seats.All(s => File.Exists(Path.Combine(folder, stage + "-" + s + ".ready"))), stage);
        }
        async Task PressKey(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key)); await Task.Delay(100);
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState()); await Task.Delay(100);
        }
        async Task Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }); await Task.Delay(100);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); await Task.Delay(100);
            InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }); await Task.Delay(150);
        }
        async Task Capture(string name)
        {
            await Task.Delay(100);
            string path = Path.ChangeExtension(Application.consoleLogPath, "." + name + ".png");
            SoloLaunchProbe.WriteRender(path); _report.captures.Add(path);
        }
        async Task Until(Func<bool> test, string label, float seconds = 25)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!test() && Time.realtimeSinceStartup < end) await Task.Delay(40);
            Check(test(), label);
        }
        void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label);
            _report.checks.Add(label);
        }
        void OnDestroy()
        {
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
            foreach (var d in _physical) if (d.added) InputSystem.EnableDevice(d);
            if (_input != null) { if (InputSystem.settings == _input) InputSystem.settings = _original; Destroy(_input); }
        }
    }
}
#endif
