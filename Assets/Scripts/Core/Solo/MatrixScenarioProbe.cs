#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using System.Text;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Solo
{
    // Explicit opt-in local validation. Fixtures never run in a release player.
    public sealed partial class MatrixScenarioProbe : MonoBehaviour
    {
        string _case, _role, _token;
        int _count, _winner, _victim, _roundSeed, _specialTurn, _completedRematches, _turn = -1;
        uint _settled, _visible, _vote;
        bool _seeded, _done, _acted, _ready, _sawGame, _voted, _disconnectRecovered;
        bool _visibleCapture, _visibleStartCaptured;
        bool _notificationPassed;
        bool _duelMiniRoundEndSeeded;
        float _phaseTime, _nextLog, _caseStart;
        NetworkManager _nm;
        int _captureSeat = -1;
        bool _reentryStarted;
        ItemSlotNetData[] _topUpBefore0, _topUpBefore1;
        MiniGameTicket _topUpTicket;
        bool _topUpMiniStarted, _topUpMiniSubmitted;
        uint _topUpSelectedCopyId;
        bool _topUpStaleSent, _topUpStaleChecked;
        float _topUpStaleAt;
        readonly float[] _inputAttemptStarted = new float[4];
        readonly bool[] _inputAttemptCancelled = new bool[4];
        static string Arg(string key, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnableAllocationDiagnostics()
        {
            if (Arg("--az-matrix", "").Length == 0 || Arg("--az-leak-diagnostics", "0") != "1") return;
            Unity.Collections.NativeLeakDetection.Mode = Unity.Collections.NativeLeakDetectionMode.EnabledWithStackTrace;
            Debug.Log("[MATRIX] Native allocation stack capture enabled (diagnostic run only)");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            string scenario = Arg("--az-matrix", "");
            if (scenario.Length == 0) return;
            var go = new GameObject("MatrixScenarioProbe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<MatrixScenarioProbe>();
            probe._case = scenario;
            probe._role = Arg("--az-role", "client");
            probe._token = Arg("--az-run", "matrix");
            probe._visibleCapture = Arg("--az-matrix-visible", "0") == "1";
            if (Arg("--az-manual-input", "0") == "1") Time.timeScale = 0.1f;
            probe._count = int.Parse(Arg("--az-count", "4"));
            probe._winner = int.Parse(Arg("--az-winner", "0"));
            probe._victim = scenario.StartsWith("disconnect") ? (Arg("--az-drop-seat", "3") == "2" ? 3 : 2)
                : (probe._winner + probe._count - 1) % probe._count;
        }

        void OnEnable()
        {
            CombatVFXManager.OnPresentationSettled += Settled;
            Application.logMessageReceived += Logged;
        }
        void OnDisable()
        {
            CombatVFXManager.OnPresentationSettled -= Settled;
            Application.logMessageReceived -= Logged;
            if (_nm != null)
            {
                _nm.OnClientDisconnectCallback -= Disconnected;
                _nm.OnClientStopped -= Stopped;
            }
        }
        void Disconnected(ulong client) => Debug.Log("[MATRIX] NET_DISCONNECT t=" + Time.realtimeSinceStartup.ToString("F1") + " client=" + client
            + " local=" + _nm.LocalClientId + " reason=" + _nm.DisconnectReason);
        void Stopped(bool host) => Debug.Log("[MATRIX] NET_STOP host=" + host + " scene=" + SceneManager.GetActiveScene().name);
        void Logged(string message, string stack, LogType type)
        {
            if (message.StartsWith("[NOTIFICATIONS] PASS")) _notificationPassed = true;
            if (_case == "input" && message.StartsWith("[INPUT] PASS")) Pass("INPUT_FLOW");
            const string prefix = "[MatchResult] Visible seq=";
            if (message.StartsWith(prefix)) uint.TryParse(message.Substring(prefix.Length).Split(' ')[0], out _visible);
        }
        PlayerState[] Players() => FindObjectsByType<PlayerState>(FindObjectsSortMode.None)
            .Where(p => p.IsSpawned && p.PlayerIndex >= 0).OrderBy(p => p.PlayerIndex).ToArray();
        string StateText()
        {
            var state = MatchCompositionRoot.Instance?.NetworkState;
            return "kills=" + (state == null ? "" : string.Join(",", Enumerable.Range(0, state.KillScores.Count).Select(i => state.KillScores[i])))
                + " seats=" + string.Join(";", Players().Select(p => p.PlayerIndex + ":"
                + p.Temperature.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ":" + p.CurrentLifeState.Value
                + ":" + string.Join(",", Enumerable.Range(0, p.GetInventory().SlotStates.Count).Select(i =>
                    p.GetInventory().SlotStates[i].ItemId + "/" + p.GetInventory().SlotStates[i].RemainingUses))));
        }
        void Settled(uint sequence)
        {
            _settled = sequence;
            Debug.Log("[MATRIX] CHECK seq=" + sequence + " " + StateText());
            CaptureVisible("settled-" + sequence);
        }
        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            yield return null;
            _nm = NetworkManager.Singleton;
            if (_nm == null) { Fail("NetworkManager missing"); yield break; }
            _nm.OnClientDisconnectCallback += Disconnected;
            _nm.OnClientStopped += Stopped;
            NetworkSessionCoordinator.Instance.SetMatchParameters(_count == 2 ? Core.Network.GameMode.OneVsOne : Core.Network.GameMode.Multi, _count);
            if (Arg("--az-relay-flow", "0") == "1")
            {
                yield return StartRelayMatrixFlow();
                yield break;
            }
            _nm.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", ushort.Parse(Arg("--az-port", "17849")), "127.0.0.1");
            Debug.Log("[MATRIX] TRANSPORT heartbeat=" + _nm.GetComponent<UnityTransport>().HeartbeatTimeoutMS
                + " timeout=" + _nm.GetComponent<UnityTransport>().DisconnectTimeoutMS);
            _nm.NetworkConfig.ConnectionApproval = true;
            _nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(_token);
            if (_role == "host")
            {
                _nm.ConnectionApprovalCallback = (request, response) =>
                {
                    response.Approved = Encoding.UTF8.GetString(request.Payload) == _token && _nm.ConnectedClientsIds.Count < _count;
                    response.CreatePlayerObject = false;
                    response.Pending = false;
                };
                if (!_nm.StartHost()) { Fail("Host start"); yield break; }
                NetworkSessionCoordinator.Instance?.DebugAdoptLocalNetworkSession();
                Debug.Log("[MATRIX] LISTENING");
                int expected = _case == "init-failure" ? _count - 1 : _count;
                while (_nm.ConnectedClientsIds.Count < expected) yield return null;
                _nm.SceneManager.LoadScene(_count == 2 ? "GameScene" : "GameScene_Multi", LoadSceneMode.Single);
            }
            else if (!_nm.StartClient()) Fail("Client start");
            else NetworkSessionCoordinator.Instance?.DebugAdoptLocalNetworkSession();
        }
        void Fail(string message)
        {
            if (_done) return;
            _done = true;
            Debug.LogError("[MATRIX] FAIL " + message);
            CaptureVisible("failure");
            Application.Quit(2);
        }
        void Pass(string message)
        {
            if (_done) return;
            _done = true;
            Debug.Log("[MATRIX] PASS " + message + " " + StateText());
            CaptureVisible("pass");
            StartCoroutine(ExitLater());
        }
        void CaptureVisible(string label)
        {
            if (!_visibleCapture) return;
            // Failed initialization can leave every client at seat -1. Keep their
            // evidence separate using the runner's unique per-process log name.
            string peer = _role == "host" ? "host" : _captureSeat >= 0 ? "seat" + _captureSeat
                : System.IO.Path.GetFileNameWithoutExtension(Application.consoleLogPath);
            string path = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.consoleLogPath) ?? Application.persistentDataPath,
                peer + "." + label + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("[MATRIX] CAPTURE " + path);
        }
        IEnumerator ExitLater()
        {
            if (Arg("--az-notification-check", "0") == "1")
            {
                float deadline = Time.realtimeSinceStartup + 30;
                while (!_notificationPassed && Time.realtimeSinceStartup < deadline) yield return null;
                if (!_notificationPassed)
                {
                    Debug.LogError("[MATRIX] FAIL notification lifetime gate did not finish");
                    Application.Quit(2);
                    yield break;
                }
            }
            yield return new WaitForSecondsRealtime(_role == "host" ? 5f : 2f);
            Application.Quit(0);
        }
        void Update()
        {
            if (_done) return;
            if (Time.realtimeSinceStartup > 300) { Fail("timeout"); return; }
            var root = MatchCompositionRoot.Instance;
            if (_case == "init-failure" && root?.ActiveConfig != null
                && _visibleCapture && !_visibleStartCaptured)
            {
                _visibleStartCaptured = true;
                CaptureVisible("start");
                return; // Let the framebuffer request complete before any pass capture.
            }
            if (_case == "init-failure" && root?.InitializationFailure != null)
            {
                if (root.InitializationFailure.Contains("waiting for players"))
                {
                    if (GameObject.Find("MatchInitializationFailure") != null) Pass("EXPECTED_INIT_FAILURE_UI");
                }
                else Fail(root.InitializationFailure);
                return;
            }
            if (_case == "host-exit" && _role != "host" && _sawGame && (_nm == null || !_nm.IsConnectedClient)
                && SceneManager.GetActiveScene().name == "LobbyScene")
            {
                if (_winner == 1 && Arg("--az-relay-flow", "0") == "1")
                {
                    if (!_reentryStarted)
                    {
                        _reentryStarted = true;
                        Debug.Log("[MATRIX] HOST_EXIT_RETURNED originalSeat=" + _captureSeat);
                        _role = _captureSeat == 1 ? "host" : "client";
                        _count = 3;
                        NetworkSessionCoordinator.Instance.SetMatchParameters(Core.Network.GameMode.Multi, 3);
                        StartCoroutine(StartRelayMatrixFlow());
                    }
                }
                else Pass("HOST_EXIT_RETURNED");
                return;
            }
            if (_nm == null || !_nm.IsConnectedClient || root?.ActiveConfig == null) return;
            var tm = TurnManager.Instance;
            var state = root.NetworkState;
            if (tm == null || state == null || !state.IsSpawned) return;
            var players = Players();
            var local = players.FirstOrDefault(p => p.IsOwner);
            if (local == null) return;
            _sawGame = true;
            if (_reentryStarted)
            {
                if (players.Length == 3 && tm.CurrentPhase.Value == TurnPhase.PrepPhase)
                    Pass("HOST_EXIT_REJOINED count=3");
                return;
            }
            _captureSeat = local.PlayerIndex;
            if (Arg("--az-relay-flow", "0") != "1")
                NetworkSessionCoordinator.Instance?.DebugAdoptLocalNetworkSession();
            if (_visibleCapture && !_visibleStartCaptured)
            {
                _visibleStartCaptured = true;
                CaptureVisible("start");
                // CaptureScreenshot is serviced at frame end. An instantaneous
                // case can otherwise overwrite this request with its pass shot.
                return;
            }
            if (Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 1;
                Debug.Log("[MATRIX] STATE local=" + local.PlayerIndex + " turn=" + tm.TurnNumber.Value + " phase=" + tm.CurrentPhase.Value
                    + " terminal=" + state.TerminalResult.Value.DecidingSequence + "/" + state.TerminalResult.Value.WinnerMask + "/" + state.TerminalResult.Value.Released);
            }
            if (_case == "item-delay")
            {
                RunDelayedItemRules(tm, players, local);
                return;
            }
            if (_case == "input")
            {
                if (_nm.IsServer && !_seeded && players.Length == _count && tm.CurrentPhase.Value == TurnPhase.PrepPhase)
                {
                    _seeded = true;
                    var items = ItemManager.Instance.GetAllItems();
                    short fan = (short)Array.FindIndex(items, i => i.ItemName == "Hand Fan");
                    short tea = (short)Array.FindIndex(items, i => i.ItemName == "Warm Tea");
                    short hug = (short)Array.FindIndex(items, i => i.ItemName == "Hug T-shirt");
                    foreach (var player in players)
                    {
                        var inv = player.GetInventory(); inv.SlotStates.Clear();
                        inv.GrantSpecificItem(fan); var first = inv.SlotStates[0]; inv.SlotStates.Clear();
                        inv.GrantSpecificItem(fan); inv.SlotStates.Insert(0, first);
                        inv.GrantSpecificItem(tea); inv.GrantSpecificItem(hug);
                        player.FanSpeed.Value = 0;
                    }
                    Debug.Log("[MATRIX] INPUT_FIXTURE");
                }
                if (_nm.IsServer && _seeded)
                    foreach (var player in players)
                    {
                        int seat = player.PlayerIndex;
                        if (_inputAttemptCancelled[seat] || player.DebugPendingMiniGameAttemptId == 0) continue;
                        if (_inputAttemptStarted[seat] == 0) _inputAttemptStarted[seat] = Time.unscaledTime;
                        if (Time.unscaledTime - _inputAttemptStarted[seat] < 1.25f) continue;
                        _inputAttemptCancelled[seat] = true;
                        uint before = player.GetInventory().CurrentFingerprint();
                        player.CancelPendingMiniGame();
                        if (before != player.GetInventory().CurrentFingerprint()) Fail("Input cancellation debited inventory");
                        Debug.Log("[INPUT] SERVER_CANCEL seat=" + seat);
                    }
                return;
            }
            if (_case == "init-failure" || (_case == "host-exit" && _winner < 2)) return;
            if (_case == "joint" || _case == "delayed")
            {
                // Preserve the existing scheduler's reverse pending-list order.
                byte expectedMask = (byte)(_case == "joint" ? 3 : 2);
                if (state.TerminalResult.Value.Released)
                {
                    if (_case == "joint" && (state.KillScores[0] != 5 || state.KillScores[1] != 5))
                    { Fail("joint group did not credit both fifth kills"); return; }
                    if (state.TerminalResult.Value.WinnerMask != expectedMask) { Fail("wrong scheduled winner mask"); return; }
                    if (_case == "delayed" && (state.KillScores[0] != 4 || players[2].CurrentLifeState.Value != LifeState.Alive))
                    { Fail("later due effect ran after a terminal winner"); return; }
                    if (_settled == state.TerminalResult.Value.DecidingSequence && _visible == _settled)
                        Pass("SCHEDULED_WIN mask=" + expectedMask);
                    return;
                }
                if (tm.CurrentPhase.Value != TurnPhase.PrepPhase || players.Length != _count) return;
                if (_caseStart == 0) _caseStart = Time.unscaledTime;
                if (_nm.IsServer && !_seeded)
                {
                    _seeded = true;
                    for (int i = 0; i < 4; i++) { state.ServerAddKill(0); state.ServerAddKill(1); }
                    if (_case == "joint")
                    {
                        root.Roster.SetLifeState(0, LifeState.Ghost);
                        root.Roster.SetLifeState(1, LifeState.Ghost);
                    }
                    else
                    {
                        tm.GetBuffSystem().Schedule(2, EffectType.TempChange, -100, 1, 0);
                        tm.GetBuffSystem().Schedule(3, EffectType.TempChange, -100, 1, 1);
                    }
                    Debug.Log("[MATRIX] SCHEDULED_FIXTURE " + _case);
                }
                if (_case == "joint")
                {
                    // Synthetic same-effect-group deaths exercise retained joint result/ACK/UI.
                    // Sequential Grudge RPCs must NOT be made simultaneous to satisfy this fixture.
                    if (_nm.IsServer && !_acted && Time.unscaledTime - _caseStart > 2)
                    {
                        _acted = tm.DebugResolveJointDeathsForValidation();
                        if (_acted) Debug.Log("[MATRIX] JOINT_SYNTHETIC_GROUP_COMMITTED");
                    }
                }
                else if (!_ready && Time.unscaledTime - _caseStart > 2)
                {
                    _ready = true;
                    ProbeInventoryCommands.Ready(local);
                }
                return;
            }
            if (_case == "idle" || _case == "disconnect-idle")
            {
                if (_caseStart == 0) _caseStart = Time.unscaledTime;
                int expected = _case == "disconnect-idle" ? _count - 1 : _count;
                if (Time.unscaledTime - _caseStart > (Arg("--az-manual-input", "0") == "1" ? 180 : 70) && players.Length == expected)
                    Pass(Arg("--az-manual-input", "0") == "1" ? "MANUAL_INPUT_SESSION_FINISHED" : "IDLE_CONNECTIONS_SURVIVE_70_SECONDS");
                return;
            }
            if (_case.StartsWith("minigame-"))
            {
                if (tm.CurrentPhase.Value != TurnPhase.PrepPhase || players.Length != _count) return;
                if (_caseStart == 0)
                {
                    _caseStart = Time.unscaledTime;
                    if (_nm.IsServer)
                    {
                        var inv = players.First(p => p.PlayerIndex == 1).GetInventory();
                        inv.SlotStates.Clear();
                        foreach (string name in new[] { "Fan", "Windbreaker", "Hug T-shirt" })
                            inv.GrantSpecificItem((short)Array.FindIndex(ItemManager.Instance.GetAllItems(), item => item.ItemName == name));
                    }
                    if (local.PlayerIndex == 1) StartCoroutine(CheckMiniGameInvalidation(local));
                }
                if (_nm.IsServer && !_seeded && Time.unscaledTime - _caseStart > 4)
                {
                    _seeded = true;
                    root.Roster.SetLifeState((byte)(_case == "minigame-actor" ? 1 : 3), LifeState.Ghost);
                    Debug.Log("[MATRIX] MINIGAME_LIFE_CHANGED");
                }
                if (local.PlayerIndex != 1 && Time.unscaledTime - _caseStart > 13) Pass("MINIGAME_OBSERVER");
                return;
            }
            if (_case == "multi-round")
            {
                if (_nm.IsServer && !_seeded && players.Length == _count && tm.CurrentPhase.Value == TurnPhase.PrepPhase)
                {
                    _seeded = true;
                    foreach (var player in players) if (player.PlayerIndex != 0) player.Temperature.Value = 0;
                    Debug.Log("[MATRIX] MULTI_ROUND_FIXTURE natural deaths on seats 1..3");
                }
                if (root.MatchManager.RoundNumber.Value >= 2 && tm.CurrentPhase.Value == TurnPhase.PrepPhase
                    && players.Length == _count && players.All(p => p.CurrentLifeState.Value == LifeState.Alive
                        && !p.HasSelectedItem.Value && !p.IsReady.Value && p.GetInventory().SlotStates.Count >= 4)
                    && state.GhostCooldowns.Count == 0 && Enumerable.Range(0, state.KillScores.Count).All(i => state.KillScores[i] == 0))
                    Pass("MULTI_ROUND_RESET local=" + local.PlayerIndex);
                return;
            }
            if (_case == "topup-transition")
            {
                if (players.Length != 4 || tm.CurrentPhase.Value != TurnPhase.PrepPhase) return;
                if (_caseStart == 0) _caseStart = Time.unscaledTime;
                if (_nm.IsServer && !_seeded)
                {
                    _seeded = true;
                    for (int seat = 0; seat < 2; seat++)
                    {
                        var inv = players[seat].GetInventory();
                        int target = _winner == 1 ? (seat == 0 ? 4 : 0) : (seat == 0 ? 3 : 1);
                        if (_winner == 5 && seat == 1)
                        {
                            while (inv.GetRandomSlotCount() > 0 && inv.SlotStates.Count > 4)
                                inv.SlotStates.RemoveAt(inv.SlotStates.Count - 1);
                            short hug = (short)Array.FindIndex(ItemManager.Instance.GetAllItems(),
                                item => item.ItemName == "Hug T-shirt");
                            if (hug < 0) { Fail("top-up minigame item missing"); return; }
                            inv.GrantSpecificItem(hug);
                        }
                        if (inv.GetRandomSlotCount() < target)
                            inv.FillRandomSlotsWithSeparateCopies(target, tm.GetDropTable());
                        while (inv.GetRandomSlotCount() > target && inv.SlotStates.Count > 4)
                            inv.SlotStates.RemoveAt(inv.SlotStates.Count - 1);
                        if (inv.GetRandomSlotCount() != target) { Fail("top-up fixture capacity"); return; }
                    }
                    _topUpBefore0 = Enumerable.Range(0, players[0].GetInventory().SlotStates.Count)
                        .Select(i => players[0].GetInventory().SlotStates[i]).ToArray();
                    _topUpBefore1 = Enumerable.Range(0, players[1].GetInventory().SlotStates.Count)
                        .Select(i => players[1].GetInventory().SlotStates[i]).ToArray();
                    if (_winner == 2) tm.DebugInjectTopUpFailureAfterFirst();
                    if (_winner == 4) tm.DebugInjectTopUpFailureAfterBothAttempts();
                    Debug.Log("[MATRIX] TOPUP_FIXTURE variant=" + _winner);
                }
                if (_winner == 5 && !_ready && local.PlayerIndex == 0
                    && Time.unscaledTime - _caseStart > 1.5f)
                {
                    _ready = true;
                    _topUpSelectedCopyId = local.GetInventory().SlotStates[1].CopyId;
                    ProbeInventoryCommands.SelectItem(local, 1);
                    ProbeInventoryCommands.Ready(local);
                    Debug.Log("[MATRIX] TOPUP_SELECTED_READY copy=" + _topUpSelectedCopyId);
                }
                if (_winner == 5 && !_topUpMiniStarted && local.PlayerIndex == 1
                    && local.GetInventory().SlotStates.Count > 4
                    && local.GetInventory().GetItemData(4)?.ItemName == "Hug T-shirt")
                {
                    _topUpMiniStarted = true;
                    StartCoroutine(CaptureTopUpMiniGame(local));
                }
                if ((_winner == 7 || _winner == 8) && !_ready && local.PlayerIndex == 1
                    && Time.unscaledTime - _caseStart > 1.5f)
                {
                    _ready = true;
                    ProbeInventoryCommands.SelectItem(local, 1);
                    Debug.Log("[MATRIX] TOPUP_PRESELECT_FOR_CANCEL");
                }
                if (_nm.IsServer && _seeded && !_acted
                    && Time.unscaledTime - _caseStart > (_winner == 5 || _winner == 7 || _winner == 8 ? 4.5f : 2f)
                    && (_winner != 5 || players[0].IsReady.Value)
                    && ((_winner != 7 && _winner != 8) || players[1].HasSelectedItem.Value))
                {
                    _acted = tm.DebugForceTwoGhostsForTopUp(2, 3);
                    if (_acted) Debug.Log("[MATRIX] TOPUP_TRANSITION_TRIGGERED");
                    if (_acted && _winner == 3)
                    {
                        var inventory = players[0].GetInventory();
                        var consumed = inventory.SlotStates[7];
                        consumed.RemainingUses = 42;
                        inventory.SlotStates[7] = consumed;
                        Debug.Log("[MATRIX] TOPUP_SAME_FRAME_POST_GRANT_CHANGE");
                    }
                }
                var grant = state.DeathmatchGrant.Value;
                if ((_winner == 8 || _winner == 9)
                    && local.PlayerIndex == 1
                    && grant.Stage == DeathmatchGrantStage.Completed
                    && state.DebugIsGrantDescriptorHeld
                    && state.InventoryReadModel?.CommittedGrantTransaction == grant.TransactionId
                    && state.IsDeathmatchGrantViewBlocked)
                {
                    if (!_topUpStaleSent)
                    {
                        _topUpStaleSent = true;
                        _topUpStaleAt = Time.unscaledTime;
                        const uint oldGrant = 0;
                        if (_winner == 8)
                        {
                            local.CancelSelectionServerRpc(state.GhostMatchEpoch.Value,
                                state.GhostRoundEpoch.Value, oldGrant);
                            local.PressReadyServerRpc(state.GhostMatchEpoch.Value,
                                state.GhostRoundEpoch.Value, oldGrant);
                        }
                        else
                        {
                            var slot = local.GetInventory().SlotStates[1];
                            local.SelectItemServerRpc(1, ActionIntent.NoTarget, slot.CopyId,
                                state.GhostMatchEpoch.Value, state.GhostRoundEpoch.Value, oldGrant);
                            local.PressReadyServerRpc(state.GhostMatchEpoch.Value,
                                state.GhostRoundEpoch.Value, oldGrant);
                        }
                        Debug.Log("[MATRIX] REVERSE_STALE_SENT variant=" + _winner
                            + " old=" + oldGrant + " completed=" + grant.TransactionId);
                    }
                    if (!_topUpStaleChecked && Time.unscaledTime - _topUpStaleAt > 0.5f)
                    {
                        bool invalid = _winner == 8
                            ? !local.HasSelectedItem.Value || local.IsReady.Value
                            : local.HasSelectedItem.Value || local.IsReady.Value;
                        if (invalid) { Fail("reverse-order stale command was accepted"); return; }
                        _topUpStaleChecked = true;
                        Debug.Log("[MATRIX] REVERSE_STALE_REJECTED variant=" + _winner);
                    }
                }
                if ((_winner == 6 || _winner == 7)
                    && grant.Stage == DeathmatchGrantStage.Completed
                    && state.IsDeathmatchGrantViewBlocked
                    && local.PlayerIndex == 1
                    && players[1].GetInventory().SlotStates.Count == 8
                    && state.InventoryReadModel != null
                    && state.InventoryReadModel.GetCount(1) < 8)
                {
                    if (!_topUpStaleSent)
                    {
                        _topUpStaleSent = true;
                        _topUpStaleAt = Time.unscaledTime;
                        uint oldGrant = state.InventoryReadModel.CommittedGrantTransaction;
                        if (_winner == 6)
                        {
                            var slot = local.GetInventory().SlotStates[1];
                            local.SelectItemServerRpc(1, ActionIntent.NoTarget, slot.CopyId,
                                state.GhostMatchEpoch.Value, state.GhostRoundEpoch.Value, oldGrant);
                            local.PressReadyServerRpc(state.GhostMatchEpoch.Value,
                                state.GhostRoundEpoch.Value, oldGrant);
                            Debug.Log("[MATRIX] STALE_GRANT_COMMAND_SENT old=" + oldGrant
                                + " completed=" + grant.TransactionId);
                        }
                        else
                        {
                            local.CancelSelectionServerRpc(state.GhostMatchEpoch.Value,
                                state.GhostRoundEpoch.Value, oldGrant);
                            Debug.Log("[MATRIX] STALE_GRANT_CANCEL_SENT old=" + oldGrant
                                + " completed=" + grant.TransactionId);
                        }
                    }
                    if (!_topUpStaleChecked && Time.unscaledTime - _topUpStaleAt > 0.5f)
                    {
                        if (_winner == 6 && (local.HasSelectedItem.Value || local.IsReady.Value)
                            || _winner == 7 && !local.HasSelectedItem.Value)
                        { Fail("stale grant command was accepted"); return; }
                        _topUpStaleChecked = true;
                        Debug.Log(_winner == 6
                            ? "[MATRIX] STALE_GRANT_COMMAND_REJECTED"
                            : "[MATRIX] STALE_GRANT_CANCEL_REJECTED");
                    }
                }
                if (_winner == 4)
                {
                    if (grant.Stage == DeathmatchGrantStage.Completed)
                    { Fail("unrecoverable top-up completed"); return; }
                    if (grant.Stage != DeathmatchGrantStage.Failed
                        || root.InitializationFailure == null
                        || state.InventoryReadModel?.IsFaulted != true
                        || !state.IsDeathmatchGrantViewBlocked) return;
                    if (tm.CurrentPhase.Value != TurnPhase.PrepPhase)
                    { Fail("top-up fault advanced the turn"); return; }
                    if (_nm.IsServer)
                    {
                        for (int seat = 0; seat < 2; seat++)
                        {
                            var before = seat == 0 ? _topUpBefore0 : _topUpBefore1;
                            var inv = players[seat].GetInventory();
                            if (before == null || inv.SlotStates.Count != before.Length
                                || Enumerable.Range(0, before.Length).Any(i => !before[i].Equals(inv.SlotStates[i])))
                            { Fail("top-up fault did not restore exact inventory"); return; }
                        }
                        if (!_ready)
                        {
                            _ready = true;
                            ProbeInventoryCommands.SelectItem(players[0], 0, 1);
                            ProbeInventoryCommands.Ready(players[0]);
                        }
                        if (players[0].HasSelectedItem.Value || players[0].IsReady.Value)
                        { Fail("top-up fault admitted a gameplay command"); return; }
                    }
                    if (_phaseTime == 0) _phaseTime = Time.unscaledTime;
                    if (Time.unscaledTime - _phaseTime > 2)
                        Pass("TOPUP_FAULT_LATCHED local=" + local.PlayerIndex);
                    return;
                }
                if (_winner == 2 && grant.Stage == DeathmatchGrantStage.Failed)
                {
                    Fail("automatic top-up recovery entered Failed state");
                    return;
                }
                if (grant.Stage != DeathmatchGrantStage.Completed || state.IsDeathmatchGrantViewBlocked)
                    return;
                if (players[2].CurrentLifeState.Value != LifeState.Ghost
                    || players[3].CurrentLifeState.Value != LifeState.Ghost) return;
                if (players[0].GetInventory().SlotStates.Count != 8
                    || players[1].GetInventory().SlotStates.Count != 8)
                { Fail("top-up slot count mismatch"); return; }
                if (_nm.IsServer)
                {
                    for (int seat = 0; seat < 2; seat++)
                    {
                        var before = seat == 0 ? _topUpBefore0 : _topUpBefore1;
                        var inv = players[seat].GetInventory();
                        if (before == null || inv.SlotStates.Count < before.Length
                            || Enumerable.Range(0, before.Length).Any(i => !before[i].Equals(inv.SlotStates[i])))
                        { Fail("top-up changed an existing slot"); return; }
                    }
                }
                if (_phaseTime == 0) _phaseTime = Time.unscaledTime;
                _ready = true;
                if (_winner == 0 && _nm.IsServer && !_disconnectRecovered
                    && Time.unscaledTime - _phaseTime > 1)
                {
                    var inventory = players[0].GetInventory();
                    var changed = inventory.SlotStates[7];
                    changed.RemainingUses = 42;
                    inventory.SlotStates[7] = changed;
                    _disconnectRecovered = true;
                    Debug.Log("[MATRIX] TOPUP_POST_GRANT_ITEM_CHANGED");
                }
                if (_winner == 0 && players[0].GetInventory().SlotStates[7].RemainingUses != 42)
                    return;
                if (_winner == 3 && (state.InventoryReadModel == null
                    || state.InventoryReadModel.CommittedGrantTransaction != grant.TransactionId
                    || state.InventoryReadModel.GetSlot(0, 7).RemainingUses != 42))
                    return;
                if ((_winner == 6 || _winner == 7 || _winner == 8 || _winner == 9)
                    && local.PlayerIndex == 1 && !_topUpStaleChecked)
                { Fail("stale grant ordering window was not exercised"); return; }
                if ((_winner == 7 || _winner == 8) && !players[1].HasSelectedItem.Value)
                { Fail("top-up lost a preserved selection"); return; }
                if (_winner == 5)
                {
                    if (!players[0].IsReady.Value || !players[0].HasSelectedItem.Value)
                    { Fail("top-up cleared selected Ready state"); return; }
                    if (local.PlayerIndex == 0 && local.GetInventory().SlotStates[1].CopyId != _topUpSelectedCopyId)
                    { Fail("top-up changed selected copy"); return; }
                    if (local.PlayerIndex == 1)
                    {
                        if (_topUpTicket.AttemptId == 0)
                        { Fail("top-up minigame ticket was not captured"); return; }
                        if (!_topUpMiniSubmitted)
                        {
                            _topUpMiniSubmitted = true;
                            local.SubmitMiniGameResultServerRpc(_topUpTicket.Slot, true,
                                _topUpTicket.MatchEpoch, _topUpTicket.RoundEpoch,
                                _topUpTicket.Turn, _topUpTicket.AttemptId, _topUpTicket.CopyId);
                            Debug.Log("[MATRIX] TOPUP_MINIGAME_RESULT_SUBMITTED");
                        }
                    }
                    if (!players[1].HasSelectedItem.Value) return;
                }
                if (Time.unscaledTime - _phaseTime > 2)
                    Pass("TOPUP_COHERENT local=" + local.PlayerIndex + " tx=" + grant.TransactionId);
                return;
            }
            if (_case == "rpc-guards")
            {
                if (_caseStart == 0)
                {
                    _caseStart = Time.unscaledTime;
                    if (_nm.IsServer) root.Roster.SetLifeState(3, LifeState.Ghost);
                    if (local.PlayerIndex == 1 || local.PlayerIndex == 3) StartCoroutine(CheckRpcGuards(local));
                }
                if (local.PlayerIndex != 1 && local.PlayerIndex != 3 && Time.unscaledTime - _caseStart > 12) Pass("RPC_OBSERVER");
                return;
            }
            if (_case == "inventory" || _case == "services" || _case == "temperature-env")
            {
                if (_nm.IsServer && !_seeded && players.Length == _count && tm.CurrentPhase.Value == TurnPhase.PrepPhase)
                {
                    _seeded = true;
                    try
                    {
                        if (_case == "inventory") CheckInventory(players);
                        else if (_case == "temperature-env") CheckTemperaturePolicies(players);
                        else CheckServices(players);
                        state.ServerAddKill(0);
                    }
                    catch (Exception error) { Fail(error.Message); }
                }
                if (state.KillScores[0] == 1) Pass(_case.ToUpperInvariant() + "_CHECKS local=" + local.PlayerIndex);
                return;
            }
            if (_count == 2 && HandleDuel(tm, players, local)) return;
            if (_nm.IsServer && !_seeded && tm.CurrentPhase.Value == TurnPhase.PrepPhase && players.Length == _count)
            {
                _seeded = true;
                if (_case == "special")
                {
                    var inv = players[0].GetInventory();
                    inv.SlotStates.Clear();
                    foreach (string name in new[] { "Cat", "Hug T-shirt", "Ice Cream", "Fan" })
                        inv.GrantSpecificItem((short)Array.FindIndex(ItemManager.Instance.GetAllItems(), item => item.ItemName == name));
                }
                else if (_count > 2)
                {
                    for (int i = 0; i < 4; i++) state.ServerAddKill(_winner);
                    players.First(p => p.PlayerIndex == _victim).Temperature.Value =
                        _case == "disconnect-prep" || _case == "disconnect-attack" ? 37
                            : _case == "ghost" ? 2 : 12;
                    if (_case == "ghost")
                    {
                        root.Roster.SetLifeState((byte)_winner, LifeState.Ghost);
                        players.First(p => p.PlayerIndex == _winner).GetInventory().SlotStates.Clear();
                    }
                }
                Debug.Log("[MATRIX] SEEDED case=" + _case + " winner=" + _winner + " victim=" + _victim);
            }
            if (_nm.IsServer && !_disconnectRecovered && (_case == "disconnect-prep" || _case == "disconnect-attack")
                && !root.Roster.IsConnected(byte.Parse(Arg("--az-drop-seat", "3"))))
            {
                _disconnectRecovered = true;
                var victim = players.FirstOrDefault(p => p.PlayerIndex == _victim);
                if (victim == null || victim.CurrentLifeState.Value != LifeState.Alive)
                { Fail("disconnect fixture victim died before recovery"); return; }
                // Start the finishing-attack fixture only after transport has detected the exit.
                victim.Temperature.Value = 12;
                Debug.Log("[MATRIX] DISCONNECT_RECOVERED_FINISHING_FIXTURE");
            }
            if (state.TerminalResult.Value.Released)
            {
                if (state.TerminalResult.Value.WinnerMask != (1 << _winner)) { Fail("wrong winner"); return; }
                if (_settled == state.TerminalResult.Value.DecidingSequence && _visible == _settled)
                    Pass("WIN local=" + local.PlayerIndex + " seq=" + _settled + " mask=" + state.TerminalResult.Value.WinnerMask);
                return;
            }
            if (_case == "special" && tm.TurnNumber.Value >= 4 && tm.CurrentPhase.Value == TurnPhase.PrepPhase)
            { Pass("SPECIAL_COMPLETED local=" + local.PlayerIndex); return; }
            if (tm.CurrentPhase.Value != TurnPhase.PrepPhase) { _turn = -1; return; }
            if (_case == "special" && _nm.IsServer && _specialTurn != tm.TurnNumber.Value)
            {
                _specialTurn = tm.TurnNumber.Value;
                string required = new[] { "Cat", "Hug T-shirt", "Ice Cream" }[Mathf.Min(_specialTurn - 1, 2)];
                var inv = players.First(p => p.PlayerIndex == 0).GetInventory();
                if (!Enumerable.Range(0, inv.SlotStates.Count).Any(i => inv.GetItemData(i)?.ItemName == required && inv.SlotStates[i].IsUsable))
                {
                    inv.GrantSpecificItem((short)Array.FindIndex(ItemManager.Instance.GetAllItems(), item => item.ItemName == required));
                    Debug.Log("[MATRIX] SPECIAL_FIXTURE_REPLENISH " + required);
                }
            }
            if (_turn != tm.TurnNumber.Value)
            {
                _turn = tm.TurnNumber.Value; _phaseTime = Time.unscaledTime; _acted = _ready = false;
                if (_nm.IsServer && ((_case == "win" && Arg("--az-network-stress", "0") == "1")
                    || (_disconnectRecovered && (_case == "disconnect-prep" || _case == "disconnect-attack"))))
                {
                    // Isolate delivery/replication from natural death and competing random winners.
                    var victim = players.FirstOrDefault(p => p.PlayerIndex == _victim);
                    if (victim != null && victim.CurrentLifeState.Value == LifeState.Alive)
                    {
                        victim.Temperature.Value = 1;
                        victim.IsFanActive.Value = false;
                        Debug.Log("[MATRIX] FINISHING_FIXTURE victim=1 fan=false");
                    }
                }
            }
            float elapsed = Time.unscaledTime - _phaseTime;
            if (_case == "ghost")
            {
                if (local.PlayerIndex == _winner && local.CurrentLifeState.Value == LifeState.Ghost && elapsed > 2 && !_acted)
                {
                    _acted = true;
                    var ghostState = MatchCompositionRoot.Instance?.NetworkState;
                    if (ghostState != null)
                        tm.UseGhostSkillRpc(0, (byte)_victim,
                            ghostState.GhostMatchEpoch.Value, ghostState.GhostRoundEpoch.Value,
                            tm.TurnNumber.Value, 0x80000002u);
                    Debug.Log("[MATRIX] GHOST_INTENT");
                }
                return;
            }
            if (local.CurrentLifeState.Value != LifeState.Alive) return;
            if (!_acted && elapsed > 2)
            {
                _acted = true;
                ProbeInventoryCommands.SelectItem(local, 254, 254);
                int defender = (_winner + 1) % _count;
                string item = local.PlayerIndex == defender && _turn == 1 ? "Windbreaker" : "Fan";
                if (_case == "win" && Arg("--az-network-stress", "0") == "1" && local.PlayerIndex != _winner)
                    item = "Windbreaker";
                byte target = (byte)(local.PlayerIndex == _winner ? _victim : _winner);
                if (_case == "special")
                {
                    item = local.PlayerIndex == 0 ? new[] { "Cat", "Hug T-shirt", "Ice Cream" }[Mathf.Min(_turn - 1, 2)] : "Fan";
                    target = (byte)(local.PlayerIndex == 0 ? 1 : (_count - 1));
                    if (local.PlayerIndex == _count - 1) target = 1;
                }
                if (_case == "duel-mini" && local.PlayerIndex == 0
                    && MatchCompositionRoot.Instance.MatchManager.RoundNumber.Value == 1)
                {
                    item = "Hug T-shirt";
                    target = 1;
                }
                Select(local, item, item == "Windbreaker" ? ActionIntent.NoTarget : target);
            }
            if (!_ready && elapsed > 4 + (local.PlayerIndex == _winner ? 0 : 0.3f))
            { _ready = true; ProbeInventoryCommands.Ready(local); }
        }
        void Select(PlayerState local, string item, byte target)
        {
            var inv = local.GetInventory();
            for (byte i = 0; i < inv.SlotStates.Count; i++)
                if (inv.GetItemData(i)?.ItemName == item && inv.SlotStates[i].IsUsable)
                {
                    if (inv.GetItemData(i).RequiresMiniGame)
                        StartCoroutine(CompleteMiniGame(local, i));
                    ProbeInventoryCommands.SelectItem(local, i, target);
                    Debug.Log("[MATRIX] INTENT " + item + " target=" + target); return;
                }
        }
        IEnumerator CompleteMiniGame(PlayerState local, byte slot)
        {
            // Simulated successful client result, not a test of the minigame's input UI.
            MiniGameTicket ticket = default;
            Action<MiniGameTicket> capture = started =>
            {
                if (started.Slot == slot) ticket = started;
            };
            local.OnMiniGameStart += capture;
            yield return new WaitForSecondsRealtime(0.45f);
            CaptureVisible("minigame-active");
            yield return new WaitForSecondsRealtime(0.3f);
            local.OnMiniGameStart -= capture;
            if (local != null && local.IsSpawned && ticket.AttemptId != 0)
                local.SubmitMiniGameResultServerRpc(slot, true, ticket.MatchEpoch,
                    ticket.RoundEpoch, ticket.Turn, ticket.AttemptId, ticket.CopyId);
        }
        IEnumerator CaptureTopUpMiniGame(PlayerState local)
        {
            Action<MiniGameTicket> capture = ticket => _topUpTicket = ticket;
            local.OnMiniGameStart += capture;
            ProbeInventoryCommands.SelectItem(local, 4, 0);
            float deadline = Time.unscaledTime + 3f;
            while (_topUpTicket.AttemptId == 0 && Time.unscaledTime < deadline)
                yield return null;
            local.OnMiniGameStart -= capture;
            if (_topUpTicket.AttemptId == 0)
                Fail("top-up minigame did not start");
            else
                Debug.Log("[MATRIX] TOPUP_MINIGAME_PENDING copy=" + _topUpTicket.CopyId);
        }
        IEnumerator CheckRpcGuards(PlayerState local)
        {
            yield return new WaitForSecondsRealtime(2);
            if (local.PlayerIndex == 3)
            {
                ProbeInventoryCommands.SelectItem(local, 0, 0);
                yield return new WaitForSecondsRealtime(1);
                if (local.HasSelectedItem.Value) { Fail("ghost actor selected ordinary attack"); yield break; }
                ProbeInventoryCommands.SelectItem(local, 1);
                yield return new WaitForSecondsRealtime(1);
                if (local.HasSelectedItem.Value) { Fail("ghost actor selected self defense"); yield break; }
                yield return new WaitForSecondsRealtime(5);
                Pass("GHOST_ACTOR_REJECTED");
                yield break;
            }
            foreach (byte target in new byte[] { 254, 1, 3 })
            {
                ProbeInventoryCommands.SelectItem(local, 0, target);
                yield return new WaitForSecondsRealtime(1);
                if (local.HasSelectedItem.Value) { Fail("server accepted invalid target " + target); yield break; }
                Debug.Log("[MATRIX] ASSERT invalid target rejected " + target);
            }
            ProbeInventoryCommands.SelectItem(local, 254, 0);
            yield return new WaitForSecondsRealtime(1);
            if (local.HasSelectedItem.Value) { Fail("server accepted invalid slot"); yield break; }
            ProbeInventoryCommands.SelectItem(local, 0, 0);
            yield return new WaitForSecondsRealtime(1);
            if (!local.HasSelectedItem.Value) { Fail("valid target rejected"); yield break; }
            var cancelView = MatchCompositionRoot.Instance?.NetworkState?.InventoryReadModel;
            local.CancelSelectionServerRpc(cancelView?.MatchEpoch ?? 0,
                cancelView?.RoundEpoch ?? 0, cancelView?.CommittedGrantTransaction ?? 0);
            yield return new WaitForSecondsRealtime(1);
            if (local.HasSelectedItem.Value) { Fail("valid selection did not cancel"); yield break; }
            Pass("RPC_GUARDS");
        }
        IEnumerator CheckMiniGameInvalidation(PlayerState local)
        {
            yield return new WaitForSecondsRealtime(2);
            var inv = local.GetInventory();
            if (inv.GetItemData(2)?.ItemName != "Hug T-shirt") { Fail("minigame fixture not ready"); yield break; }
            bool started = false;
            MiniGameTicket firstTicket = default;
            Action<MiniGameTicket> onStart = ticket => { started = true; firstTicket = ticket; };
            local.OnMiniGameStart += onStart;
            ProbeInventoryCommands.SelectItem(local, 2, 3);
            yield return new WaitForSecondsRealtime(1);
            local.OnMiniGameStart -= onStart;
            if (!started) { Fail("minigame did not start"); yield break; }
            byte uses = inv.SlotStates[2].RemainingUses;
            yield return new WaitForSecondsRealtime(2.5f);
            local.SubmitMiniGameResultServerRpc(2, _case != "minigame-failure",
                firstTicket.MatchEpoch, firstTicket.RoundEpoch, firstTicket.Turn,
                firstTicket.AttemptId, firstTicket.CopyId);
            yield return new WaitForSecondsRealtime(1);
            if (local.HasSelectedItem.Value || inv.SlotStates[2].RemainingUses != uses)
            { Fail("invalidated minigame queued or consumed item"); yield break; }
            local.SubmitMiniGameResultServerRpc(2, true, firstTicket.MatchEpoch,
                firstTicket.RoundEpoch, firstTicket.Turn, firstTicket.AttemptId, firstTicket.CopyId);
            yield return new WaitForSecondsRealtime(1);
            if (local.HasSelectedItem.Value) { Fail("duplicate minigame result accepted"); yield break; }
            if (_case != "minigame-actor")
            {
                MiniGameTicket retryTicket = default;
                Action<MiniGameTicket> onRetry = ticket => retryTicket = ticket;
                local.OnMiniGameStart += onRetry;
                ProbeInventoryCommands.SelectItem(local, 2, 0);
                yield return new WaitForSecondsRealtime(1);
                local.OnMiniGameStart -= onRetry;
                if (retryTicket.AttemptId != 0)
                    local.SubmitMiniGameResultServerRpc(2, true, retryTicket.MatchEpoch,
                        retryTicket.RoundEpoch, retryTicket.Turn,
                        retryTicket.AttemptId, retryTicket.CopyId);
                yield return new WaitForSecondsRealtime(1);
                if (!local.HasSelectedItem.Value) { Fail("valid minigame retry rejected"); yield break; }
            }
            // Keep the acting client connected until the observers have recorded their result.
            while (Time.unscaledTime - _caseStart < 14) yield return null;
            Pass("MINIGAME_INVALIDATION_AND_RETRY");
        }
        bool HandleDuel(TurnManager tm, PlayerState[] players, PlayerState local)
        {
            var match = MatchCompositionRoot.Instance.MatchManager;
            if (_case == "duel-mini" && match.RoundNumber.Value >= 2
                && tm.CurrentPhase.Value == TurnPhase.PrepPhase)
            {
                Pass("DUEL_MINIGAME_ROUND2 local=" + local.PlayerIndex);
                return true;
            }
            if (_case == "duel-mini" && _nm.IsServer
                && match.RoundNumber.Value == 1 && tm.TurnNumber.Value >= 2
                && tm.CurrentPhase.Value == TurnPhase.PrepPhase
                && !_duelMiniRoundEndSeeded)
            {
                _duelMiniRoundEndSeeded = true;
                players.First(p => p.PlayerIndex == 1).Temperature.Value = 0;
                Debug.Log("[MATRIX] DUEL_MINIGAME_ROUND_END_AFTER_ACTION");
            }
            if (match.CurrentMatchState.Value == Core.Network.MatchState.RematchVote && _vote != match.RematchVoteEpoch.Value)
            {
                _vote = match.RematchVoteEpoch.Value; _voted = true;
                match.SubmitRematchDecisionRpc(true, _vote);
                Debug.Log("[MATRIX] REMATCH_ACCEPT local=" + local.PlayerIndex);
            }
            if (_voted && match.RoundNumber.Value == 1 && match.P1RoundWins.Value == 0 && match.P2RoundWins.Value == 0
                && tm.CurrentPhase.Value == TurnPhase.PrepPhase)
            {
                _voted = false;
                _completedRematches++;
                _roundSeed = 0;
                if (players.Length != 2 || FindObjectsByType<TurnManager>(FindObjectsSortMode.None).Length != 1
                    || FindObjectsByType<MatchCompositionRoot>(FindObjectsSortMode.None).Length != 1)
                { Fail("duplicate or missing match owner after rematch"); return true; }
                Debug.Log("[MATRIX] REMATCH_RESET cycle=" + _completedRematches + " local=" + local.PlayerIndex);
                if (_completedRematches >= (_case == "duel-repeat" ? 3 : 1))
                { Pass("DUEL_REMATCH_RESET local=" + local.PlayerIndex + " cycles=" + _completedRematches); return true; }
            }
            if (_nm.IsServer && tm.CurrentPhase.Value == TurnPhase.PrepPhase && _roundSeed != match.RoundNumber.Value)
            {
                _roundSeed = match.RoundNumber.Value;
                players.First(p => p.PlayerIndex == 1).Temperature.Value = 6;
                if (_case == "duel-mini" && match.RoundNumber.Value == 1)
                {
                    players.First(p => p.PlayerIndex == 1).Temperature.Value = 37;
                    short hugId = (short)Array.FindIndex(ItemManager.Instance.GetAllItems(),
                        item => item != null && item.ItemName == "Hug T-shirt");
                    if (hugId < 0 || !players[0].GetInventory().GrantSpecificItem(hugId))
                    {
                        Fail("duel minigame item staging failed");
                        return true;
                    }
                }
                Debug.Log("[MATRIX] DUEL_ROUND_SEED round=" + _roundSeed);
            }
            return false;
        }
    }
}
#endif
