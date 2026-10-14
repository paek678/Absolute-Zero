#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Solo
{
    // Explicit opt-in development-only coordinator regression probe.
    // No gameplay state, rules, RNG, transport implementation or deadlines are changed.
    public sealed class RelayRegressionProbe : MonoBehaviour
    {
        const string OutputRoot = "C:/Users/paek6/AbsoluteZero-SoloIntegration/output/relay-fixes-20261014";
        string _folder;
        double _deadline;
        bool _ready, _ending;
        int _inFlight, _acquireInFlight;
        float _nextRead;
        NetworkSessionCoordinator _coordinator;
        MatchSessionRouter _router;
        readonly HashSet<string> _seen = new();
        readonly HashSet<MatchSessionLease> _owned = new();
        readonly Dictionary<string, PendingWrite> _pendingWrites = new(StringComparer.Ordinal);
        readonly List<string> _failedWrites = new();
        bool _exitRequested;
        int _exitCode;
        const double WriteRetrySeconds = .05, WriteTimeoutSeconds = 5;
        sealed class PendingWrite
        {
            public string Name, Payload, LastError;
            public double FirstQueuedAt, RetryAt;
        }

        static string Arg(string key, string fallback = "")
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string folder = Arg("--az-relay-regression");
            if (folder.Length == 0) return;
            string full = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string allowed = Path.GetFullPath(OutputRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(allowed, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Relay regression output must remain inside the dedicated evidence root.");
            if (File.Exists(Path.Combine(full, "state.json")))
                throw new InvalidOperationException("Prior probe evidence exists; use a new attempt/peer folder.");
            Directory.CreateDirectory(full);
            Directory.CreateDirectory(Path.Combine(full, "commands"));
            Directory.CreateDirectory(Path.Combine(full, "responses"));
            var go = new GameObject("RelayRegressionProbe");
            DontDestroyOnLoad(go);
            var probe = go.AddComponent<RelayRegressionProbe>();
            probe._folder = full;
            probe._deadline = Time.realtimeSinceStartupAsDouble + 600;
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (AppBootstrapper.Instance?.IsReady != true || NetworkSessionCoordinator.Instance == null)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                { Fatal("Bootstrap did not become ready"); yield break; }
                yield return null;
            }
            _coordinator = NetworkSessionCoordinator.Instance;
            _router = AppBootstrapper.Instance.SessionRouter;
            if (_router.Current != null || _router.IsStopping || _coordinator.CurrentLobby != null
                || NetworkManager.Singleton?.IsListening == true)
            { Fatal("Probe requires a fresh session owner"); yield break; }
            _ready = true;
            Write("ready.json", Snapshot());
            Save();
            Debug.Log("[RELAY-REGRESSION] READY");
        }

        void Update()
        {
            if (!_ready || _ending || Time.unscaledTime < _nextRead) return;
            _nextRead = Time.unscaledTime + .2f;
            if (Time.realtimeSinceStartupAsDouble >= _deadline)
            { Fatal("Probe lifetime exceeded600 seconds"); return; }
            if (_coordinator == null || !ReferenceEquals(NetworkSessionCoordinator.Instance, _coordinator)
                || !ReferenceEquals(AppBootstrapper.Instance?.SessionRouter, _router))
            { Fatal("Session owner changed"); return; }
            // Create/join acquires its lease after asynchronous authentication.
            // Remember that exact lease while join is still waiting so normal cancellation is owned.
            if (_acquireInFlight > 0) RememberLease();
            foreach (string path in Directory.GetFiles(Path.Combine(_folder, "commands"), "*.json").OrderBy(x => x))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                if (!_seen.Add(id)) continue;
                try { Execute(id, JObject.Parse(File.ReadAllText(path))); }
                catch (Exception error) { WriteResponse(id, false, "ProbeCommandError", error.ToString(), null); }
            }
            Save();
        }

        void LateUpdate()
        {
            // Retry between frames. Never sleep, spin, touch gameplay time, or
            // drop command responses just because an evidence reader holds a file.
            FlushPendingWrites();
            if (_exitRequested && _pendingWrites.Count == 0)
                Application.Quit(_exitCode);
        }

        async void Execute(string id, JObject command)
        {
            string action = (string)command["action"];
            bool parallelSafe = action == "snapshot" || action == "leave";
            if (_inFlight > 0 && !parallelSafe)
            { WriteResponse(id, false, "ProbeBusy", "Only leave/snapshot may run while join waits for host", null); return; }
            _inFlight++;
            JObject before = Snapshot();
            bool acquiring = false;
            try
            {
                if (action == "snapshot") WriteResponse(id, true, null, null, before);
                else if (action == "create")
                {
                    Select(command);
                    acquiring = true; _acquireInFlight++;
                    var task = _coordinator.CreateLobbyAsync("AZ_Regression_" + Path.GetFileName(_folder));
                    RememberLease();
                    var result = await task;
                    RememberLease();
                    WriteResponse(id, result.IsSuccess, result.ErrorCode.ToString(), result.ErrorMessage, before);
                }
                else if (action == "join")
                {
                    Select(command);
                    acquiring = true; _acquireInFlight++;
                    var task = _coordinator.JoinGameAsync((string)command["code"]);
                    RememberLease();
                    var result = await task;
                    RememberLease();
                    WriteResponse(id, result.IsSuccess, result.ErrorCode.ToString(), result.ErrorMessage, before);
                }
                else if (action == "start")
                {
                    RequireOwned();
                    var result = await _coordinator.StartMatchAsHostAsync();
                    WriteResponse(id, result.IsSuccess, result.ErrorCode.ToString(), result.ErrorMessage, before);
                }
                else if (action == "leave")
                {
                    if (_router.Current != null) RequireOwned();
                    await _coordinator.LeaveAsync();
                    WriteResponse(id, true, null, null, before);
                }
                else if (action == "memberships")
                {
                    if (UnityServices.State != ServicesInitializationState.Initialized || !AuthenticationService.Instance.IsSignedIn)
                        throw new InvalidOperationException("Membership inspection requires this process's authenticated player");
                    var ids = await LobbyService.Instance.GetJoinedLobbiesAsync();
                    WriteResponse(id, true, null, null, before, new JObject { ["joinedLobbyIds"] = new JArray(ids) });
                }
                else if (action == "inspect_lobby")
                {
                    RequireOwned();
                    string lobbyId = _coordinator.CurrentLobby?.Id;
                    if (string.IsNullOrEmpty(lobbyId)) throw new InvalidOperationException("No owned lobby to inspect");
                    var lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);
                    WriteResponse(id, true, null, null, before, new JObject {
                        ["lobbyId"] = lobby.Id, ["memberIds"] = new JArray(lobby.Players.Select(p => p.Id)),
                        ["maxPlayers"] = lobby.MaxPlayers, ["isLocked"] = lobby.IsLocked });
                }
                else if (action == "quit")
                {
                    if (_router.Current != null) { RequireOwned(); await _coordinator.LeaveAsync(); }
                    WriteResponse(id, true, null, null, before);
                    RequestExit(0);
                    Save();
                }
                else throw new InvalidOperationException("Unsupported command: " + action);
            }
            catch (Exception error) { WriteResponse(id, false, "ProbeException", error.ToString(), before); }
            finally { if (acquiring) _acquireInFlight--; _inFlight--; Save(); }
        }

        void Select(JObject command)
        {
            var nm = NetworkManager.Singleton;
            bool ownedReadyRetry = _coordinator.State == SessionState.Ready
                && _coordinator.Operation == SessionOperation.None && _router.Current != null
                && _owned.Contains(_router.Current) && ReferenceEquals(_router.Current, _coordinator.OnlineLease)
                && _router.LaunchContext == null && _coordinator.CurrentLobby == null
                && nm != null && !nm.IsListening && !nm.ShutdownInProgress;
            if ((_router.Current != null && !ownedReadyRetry) || _router.IsStopping
                || _coordinator.CurrentLobby != null || _router.LaunchContext != null
                || (nm != null && (nm.IsListening || nm.ShutdownInProgress)))
                throw new InvalidOperationException("Create/join requires released ownership or this probe's Ready online retry lease");
            if (!Enum.TryParse((string)command["mode"], out GameMode mode)
                || (mode != GameMode.OneVsOne && mode != GameMode.Multi))
                throw new InvalidOperationException("Expected OneVsOne or Multi");
            int count = (int)command["count"];
            if ((mode == GameMode.OneVsOne && count != 2) || (mode == GameMode.Multi && count != 3 && count != 4))
                throw new InvalidOperationException("Invalid requested player count");
            _coordinator.SetMatchParameters(mode, count);
        }

        void RememberLease()
        {
            var lease = _coordinator.OnlineLease;
            if (lease != null && ReferenceEquals(_router.Current, lease)) _owned.Add(lease);
        }

        void RequireOwned()
        {
            if (_router.Current == null || !_owned.Contains(_router.Current)
                || !ReferenceEquals(_router.Current, _coordinator.OnlineLease))
                throw new InvalidOperationException("Refusing to mutate a lease this probe did not acquire");
        }

        JObject Snapshot()
        {
            var nm = NetworkManager.Singleton;
            var root = MatchCompositionRoot.Instance;
            var turn = TurnManager.Instance;
            string playerId = null;
            if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
                playerId = AuthenticationService.Instance.PlayerId;
            var lobby = _coordinator?.CurrentLobby;
            var roster = new JArray();
            if (root?.Roster != null)
                foreach (var pair in root.Roster.Seats)
                    roster.Add(new JObject { ["seat"] = pair.Key, ["participantId"] = pair.Value.ParticipantId,
                        ["clientId"] = pair.Value.ClientId?.ToString(), ["state"] = pair.Value.State.ToString() });
            var players = new JArray();
            foreach (var p in FindObjectsByType<PlayerState>(FindObjectsSortMode.None))
                if (p != null && p.IsSpawned)
                    players.Add(new JObject { ["seat"] = p.PlayerIndex, ["ownerClientId"] = p.OwnerClientId.ToString(),
                        ["isOwner"] = p.IsOwner, ["readyParticipant"] = p.IsParticipantReady });
            return new JObject {
                ["utc"] = DateTime.UtcNow.ToString("o"), ["time"] = Time.realtimeSinceStartupAsDouble,
                ["scene"] = SceneManager.GetActiveScene().name, ["ready"] = _ready,
                ["state"] = _coordinator?.State.ToString(), ["operation"] = _coordinator?.Operation.ToString(),
                ["error"] = _coordinator?.LastError, ["playerId"] = playerId,
                ["selectedMode"] = _coordinator?.SelectedMode.ToString(), ["selectedCount"] = _coordinator?.SelectedPlayerCount,
                ["leasePresent"] = _router?.Current != null, ["leaseMode"] = _router?.Current?.Mode.ToString(),
                ["leaseOwnedByProbe"] = _router?.Current != null && _owned.Contains(_router.Current)
                    && ReferenceEquals(_router.Current, _coordinator?.OnlineLease),
                ["launchContextPresent"] = _router?.LaunchContext != null,
                ["routerStopping"] = _router?.IsStopping ?? false,
                ["lobbyId"] = lobby?.Id, ["lobbyCode"] = lobby?.LobbyCode,
                ["memberIds"] = lobby == null ? new JArray() : new JArray(lobby.Players.Select(p => p.Id)),
                ["networkListening"] = nm != null && nm.IsListening,
                ["shutdownInProgress"] = nm != null && nm.ShutdownInProgress,
                ["connectedClient"] = nm != null && nm.IsConnectedClient,
                ["isServer"] = nm != null && nm.IsServer, ["localClientId"] = nm?.LocalClientId.ToString(),
                ["serverConnections"] = nm != null && nm.IsServer ? nm.ConnectedClientsIds.Count : 0,
                ["matchMode"] = root?.ActiveConfig?.Mode.ToString(),
                ["matchCount"] = root?.ActiveConfig?.RequiredPlayerCount,
                ["phase"] = turn != null && turn.IsSpawned ? turn.CurrentPhase.Value.ToString() : null,
                ["initializationFailure"] = root?.InitializationFailure,
                ["roster"] = roster, ["players"] = players, ["inFlight"] = _inFlight, ["ending"] = _ending,
                ["pendingEvidenceWrites"] = _pendingWrites.Count, ["failedEvidenceWrites"] = new JArray(_failedWrites)
            };
        }

        void WriteResponse(string id, bool success, string errorCode, string error, JObject before, JObject extra = null)
        {
            Write(Path.Combine("responses", id + ".json"), new JObject {
                ["commandId"] = id, ["success"] = success, ["errorCode"] = errorCode, ["error"] = error,
                ["before"] = before, ["after"] = Snapshot(), ["extra"] = extra });
        }

        void Save() { if (_folder != null) Write("state.json", Snapshot()); }
        void Write(string name, JToken value)
        {
            string payload = value.ToString(Formatting.Indented);
            if (_pendingWrites.TryGetValue(name, out var pending))
            {
                // State is observational and may coalesce. Responses/ready/failure
                // are distinct immutable records and must survive until committed.
                if (name == "state.json") { pending.Payload = payload; return; }
                if (pending.Payload == payload) return;
                EvidenceWriteFailed(name, "Conflicting pending critical evidence payload");
                return;
            }
            _pendingWrites.Add(name, new PendingWrite {
                Name = name, Payload = payload, FirstQueuedAt = Time.realtimeSinceStartupAsDouble });
        }

        void FlushPendingWrites()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (var pending in _pendingWrites.Values.ToArray())
            {
                if (now < pending.RetryAt) continue;
                string path = Path.Combine(_folder, pending.Name), temporary = path + ".tmp";
                try
                {
                    File.WriteAllText(temporary, pending.Payload, new System.Text.UTF8Encoding(false));
                    // Atomic replacement avoids an observable Delete/Move gap.
                    // Readers without FILE_SHARE_DELETE may still block Replace;
                    // keep the exact payload queued and retry after a later frame.
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                    _pendingWrites.Remove(pending.Name);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    pending.LastError = error.GetType().Name + ": " + error.Message;
                    if (now - pending.FirstQueuedAt >= WriteTimeoutSeconds)
                    {
                        _pendingWrites.Remove(pending.Name);
                        // Preserve any .tmp payload as recovery evidence. A critical
                        // response that cannot commit makes the probe fail visibly.
                        EvidenceWriteFailed(pending.Name, pending.LastError);
                    }
                    else pending.RetryAt = now + WriteRetrySeconds;
                }
            }
        }

        void EvidenceWriteFailed(string name, string error)
        {
            if (!_failedWrites.Contains(name)) _failedWrites.Add(name);
            RequestExit(2);
            Debug.LogError("[RELAY-REGRESSION] EVIDENCE_WRITE_FAILED " + name + " " + error);
            if (name != "probe-write-failure.json" && !_pendingWrites.ContainsKey("probe-write-failure.json"))
                Write("probe-write-failure.json", new JObject {
                    ["error"] = error, ["failedFile"] = name, ["uncommittedPayload"] = name + ".tmp",
                    ["retryBoundSeconds"] = WriteTimeoutSeconds, ["failedFiles"] = new JArray(_failedWrites) });
        }

        void RequestExit(int code)
        {
            _ending = true;
            _exitRequested = true;
            _exitCode = Math.Max(_exitCode, code);
        }
        void Fatal(string message)
        {
            RequestExit(2);
            Write("probe-failure.json", new JObject { ["error"] = message, ["state"] = Snapshot() });
            Debug.LogError("[RELAY-REGRESSION] FAIL " + message);
        }
    }
}
#endif
