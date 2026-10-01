using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

using LobbyPlayer = Unity.Services.Lobbies.Models.Player;

namespace AbsoluteZero.Core.Network
{
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        [Header("Lobby Settings")]
        [SerializeField] private int maxPlayers = 2;
        [SerializeField] private float heartbeatInterval = 15f;
        [SerializeField] private float lobbyPollInterval = 2f;

        private Lobby currentLobby;
        private float heartbeatTimer;
        private float pollTimer;
        private bool isHost;
        private bool isGameSessionActive;

        private bool _heartbeatInFlight;
        private bool _pollInFlight;
        private uint _operationGeneration;

        public event Action<Lobby> OnLobbyCreated;
        public event Action<Lobby> OnLobbyJoined;
        public event Action<Lobby> OnLobbyUpdated;
        public event Action OnLobbyLeft;
        public event Action<string> OnError;

        private void FireError(string error) => OnError?.Invoke(error);

        public Lobby CurrentLobby => currentLobby;
        public bool IsHost => isHost;
        public bool IsInLobby => currentLobby != null;
        public bool IsGameSessionActive => isGameSessionActive;
        public uint OperationGeneration => _operationGeneration;
        internal float HeartbeatInterval => heartbeatInterval;
        internal float PollInterval => lobbyPollInterval;
        public string PlayerId => UnityServices.State == ServicesInitializationState.Initialized
            ? AuthenticationService.Instance.PlayerId : null;

        bool IsCurrentLobbyRequest(uint generation, string lobbyId)
            => this != null && generation == _operationGeneration && lobbyId != null
                && currentLobby?.Id == lobbyId && Session.AppBootstrapper.Instance?.SessionRouter.IsSolo != true;
        public int MaxPlayers
        {
            get => maxPlayers;
            set => maxPlayers = Mathf.Clamp(value, 2, 8);
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // Online initialization is owned by NetworkSessionCoordinator and starts on demand.

        private void Update()
        {
            // The online lease owns scheduling. Retain the legacy-only route until
            // its serialized/debug callers have been audited in R12.
            if (Session.NetworkSessionCoordinator.Instance?.OwnsOnlineSession == true) return;
            HandleHeartbeat();
            HandleLobbyPoll();
        }

        private void OnApplicationQuit()
        {
            _ = LeaveLobbyAsync();
        }

        #region Initialization

        private async Task InitializeServices()
        {
            try
            {
                var options = new InitializationOptions();

#if UNITY_EDITOR
                try
                {
                    var clonesManagerType = System.Type.GetType("ParrelSync.ClonesManager, ParrelSync");
                    if (clonesManagerType != null)
                    {
                        var isCloneMethod = clonesManagerType.GetMethod("IsClone", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                        var getArgumentMethod = clonesManagerType.GetMethod("GetArgument", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                        if (isCloneMethod != null && (bool)isCloneMethod.Invoke(null, null))
                        {
                            string customArgument = getArgumentMethod?.Invoke(null, null) as string ?? "";
                            string profile = string.IsNullOrEmpty(customArgument) ? "clone" : customArgument;
                            options.SetProfile(profile);
                            Debug.Log($"[LobbyManager] ParrelSync clone detected - Profile: {profile}");
                        }
                    }
                }
                catch (System.Exception) { }
#endif

                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync(options);
                    Debug.Log("[LobbyManager] Unity Services initialized");
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[LobbyManager] Signed in anonymously - PlayerId: {PlayerId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[LobbyManager] Service initialization failed: {e.Message}");
                OnError?.Invoke($"Service initialization failed: {e.Message}");
            }
        }

        #endregion

        #region Lobby Creation

        public async Task<Lobby> CreateLobbyAsync(string lobbyName, bool isPrivate = false)
        {
            _operationGeneration++;
            return await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                if (currentLobby != null)
                    await LeaveLobbyAsync();

                var options = new CreateLobbyOptions
                {
                    IsPrivate = isPrivate,
                    Player = CreatePlayerData(),
                    Data = new Dictionary<string, DataObject>
                    {
                        { "GameMode", new DataObject(DataObject.VisibilityOptions.Public, "TurnBattle") },
                        { "HostReady", new DataObject(DataObject.VisibilityOptions.Public, "false") },
                        { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, "") },
                        { "GameStarted", new DataObject(DataObject.VisibilityOptions.Member, "false") }
                    }
                };

                currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
                isHost = true;
                SetGameSessionActive(false);

                Debug.Log($"[LobbyManager] Lobby created - Name: {lobbyName}, Code: {currentLobby.LobbyCode}, MaxPlayers: {maxPlayers}");
                OnLobbyCreated?.Invoke(currentLobby);

                return currentLobby;
            }, "Create lobby", FireError);
        }

        #endregion

        #region Lobby Join

        public async Task<Lobby> JoinLobbyByCodeAsync(string lobbyCode)
        {
            _operationGeneration++;
            return await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                if (currentLobby != null)
                    await LeaveLobbyAsync();

                var options = new JoinLobbyByCodeOptions
                {
                    Player = CreatePlayerData()
                };

                currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
                isHost = false;
                SetGameSessionActive(false);

                Debug.Log($"[LobbyManager] Joined lobby by code - Code: {lobbyCode}");
                OnLobbyJoined?.Invoke(currentLobby);

                return currentLobby;
            }, "Join by code", FireError);
        }

        public async Task<Lobby> JoinLobbyByIdAsync(string lobbyId)
        {
            _operationGeneration++;
            return await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                if (currentLobby != null)
                    await LeaveLobbyAsync();

                var options = new JoinLobbyByIdOptions
                {
                    Player = CreatePlayerData()
                };

                currentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
                isHost = false;
                SetGameSessionActive(false);

                Debug.Log($"[LobbyManager] Joined lobby by ID - ID: {lobbyId}");
                OnLobbyJoined?.Invoke(currentLobby);

                return currentLobby;
            }, "Join by ID", FireError);
        }

        public async Task<Lobby> QuickJoinAsync()
        {
            _operationGeneration++;
            return await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                if (currentLobby != null)
                    await LeaveLobbyAsync();

                var options = new QuickJoinLobbyOptions
                {
                    Player = CreatePlayerData()
                };

                currentLobby = await LobbyService.Instance.QuickJoinLobbyAsync(options);
                isHost = false;
                SetGameSessionActive(false);

                Debug.Log($"[LobbyManager] Quick join succeeded - LobbyId: {currentLobby.Id}");
                OnLobbyJoined?.Invoke(currentLobby);

                return currentLobby;
            }, "Quick join", FireError);
        }

        #endregion

        #region Lobby Query

        public async Task<List<Lobby>> QueryLobbiesAsync()
        {
            return await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                var options = new QueryLobbiesOptions
                {
                    Count = 20,
                    Filters = new List<QueryFilter>
                    {
                        new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                    },
                    Order = new List<QueryOrder>
                    {
                        new QueryOrder(false, QueryOrder.FieldOptions.Created)
                    }
                };

                var response = await LobbyService.Instance.QueryLobbiesAsync(options);
                Debug.Log($"[LobbyManager] Lobby query completed - Found: {response.Results.Count}");

                return response.Results;
            }, "Query lobbies", FireError) ?? new List<Lobby>();
        }

        #endregion

        #region Lobby Leave

        public async Task LeaveLobbyAsync()
        {
            if (currentLobby == null) return;
            uint generation = ++_operationGeneration;
            string lobbyId = currentLobby.Id;

            try
            {
                if (isHost)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(currentLobby.Id);
                    Debug.Log("[LobbyManager] Lobby deleted");
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(currentLobby.Id, PlayerId);
                    Debug.Log("[LobbyManager] Left lobby");
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"[LobbyManager] Leave lobby failed: {e.Message}");
            }
            finally
            {
                if (IsCurrentLobbyRequest(generation, lobbyId))
                {
                    currentLobby = null;
                    isHost = false;
                    SetGameSessionActive(false);
                    OnLobbyLeft?.Invoke();
                }
            }
        }

        #endregion

        #region Player Data

        private LobbyPlayer CreatePlayerData()
        {
            return new LobbyPlayer
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, $"Player_{PlayerId?.Substring(0, 6) ?? "Unknown"}") },
                    { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "false") },
                    { "LastAction", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "") }
                }
            };
        }

        public async Task SendActionAsync(string actionMessage)
        {
            if (currentLobby == null) return;

            uint generation = _operationGeneration;
            string lobbyId = currentLobby.Id;
            var snapshotOwner = Session.NetworkSessionCoordinator.Instance?.LobbyWork;
            long metadataEpoch = snapshotOwner?.MetadataEpoch ?? 0;
            string playerId = PlayerId;
            await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                string actionWithTime = $"{DateTime.Now.Ticks}|{actionMessage}";

                var options = new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "LastAction", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, actionWithTime) }
                    }
                };

                var updated = await LobbyService.Instance.UpdatePlayerAsync(lobbyId, playerId, options);
                if (!IsCurrentLobbyRequest(generation, lobbyId)) return;
                AcceptUpdatedLobby(updated, snapshotOwner, metadataEpoch);
                Debug.Log($"[LobbyManager] Action sent: {actionMessage}");
            }, "Send action", error => { if (IsCurrentLobbyRequest(generation, lobbyId)) FireError(error); });
        }

        public async Task SetPlayerReadyAsync(bool isReady)
        {
            if (currentLobby == null) return;

            uint generation = _operationGeneration;
            string lobbyId = currentLobby.Id;
            var snapshotOwner = Session.NetworkSessionCoordinator.Instance?.LobbyWork;
            long metadataEpoch = snapshotOwner?.MetadataEpoch ?? 0;
            string playerId = PlayerId;
            await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                var options = new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, isReady.ToString().ToLower()) }
                    }
                };

                var updated = await LobbyService.Instance.UpdatePlayerAsync(lobbyId, playerId, options);
                if (!IsCurrentLobbyRequest(generation, lobbyId)) return;
                AcceptUpdatedLobby(updated, snapshotOwner, metadataEpoch);
                Debug.Log($"[LobbyManager] Ready state updated: {isReady}");
            }, "Ready update", error => { if (IsCurrentLobbyRequest(generation, lobbyId)) FireError(error); });
        }

        public async Task SetPlayerNameAsync(string playerName)
        {
            if (currentLobby == null) return;

            uint generation = _operationGeneration;
            string lobbyId = currentLobby.Id;
            var snapshotOwner = Session.NetworkSessionCoordinator.Instance?.LobbyWork;
            long metadataEpoch = snapshotOwner?.MetadataEpoch ?? 0;
            string playerId = PlayerId;
            await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                var options = new UpdatePlayerOptions
                {
                    Data = new Dictionary<string, PlayerDataObject>
                    {
                        { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, playerName) }
                    }
                };

                var updated = await LobbyService.Instance.UpdatePlayerAsync(lobbyId, playerId, options);
                if (!IsCurrentLobbyRequest(generation, lobbyId)) return;
                AcceptUpdatedLobby(updated, snapshotOwner, metadataEpoch);
                Debug.Log($"[LobbyManager] Player name updated: {playerName}");
            }, "Set player name", error => { if (IsCurrentLobbyRequest(generation, lobbyId)) FireError(error); });
        }

        #endregion

        #region Relay Integration

        public async Task SetRelayJoinCodeAsync(string relayJoinCode)
        {
            if (currentLobby == null || !isHost) return;

            uint generation = _operationGeneration;
            string lobbyId = currentLobby.Id;
            var snapshotOwner = Session.NetworkSessionCoordinator.Instance?.LobbyWork;
            long metadataEpoch = snapshotOwner?.MetadataEpoch ?? 0;
            await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                var options = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                    }
                };

                var updated = await LobbyService.Instance.UpdateLobbyAsync(lobbyId, options);
                if (!IsCurrentLobbyRequest(generation, lobbyId)) return;
                AcceptUpdatedLobby(updated, snapshotOwner, metadataEpoch);
                Debug.Log($"[LobbyManager] Relay Join Code set: {relayJoinCode}");
            }, "Save Relay join code", error => { if (IsCurrentLobbyRequest(generation, lobbyId)) FireError(error); });
        }

        public string GetRelayJoinCode()
        {
            if (currentLobby?.Data == null) return null;

            if (currentLobby.Data.TryGetValue("RelayJoinCode", out var data))
            {
                return string.IsNullOrEmpty(data.Value) ? null : data.Value;
            }
            return null;
        }

        public async Task SetGameStartedAsync(bool started)
        {
            if (currentLobby == null || !isHost) return;

            uint generation = _operationGeneration;
            string lobbyId = currentLobby.Id;
            var snapshotOwner = Session.NetworkSessionCoordinator.Instance?.LobbyWork;
            long metadataEpoch = snapshotOwner?.MetadataEpoch ?? 0;
            await LobbyServiceHelper.ExecuteAsync(async () =>
            {
                var options = new UpdateLobbyOptions
                {
                    Data = new Dictionary<string, DataObject>
                    {
                        { "GameStarted", new DataObject(DataObject.VisibilityOptions.Member, started.ToString().ToLower()) }
                    }
                };

                var updated = await LobbyService.Instance.UpdateLobbyAsync(lobbyId, options);
                if (!IsCurrentLobbyRequest(generation, lobbyId)) return;
                AcceptUpdatedLobby(updated, snapshotOwner, metadataEpoch);
                SetGameSessionActive(IsGameStarted());
                Debug.Log($"[LobbyManager] Game started flag updated: {started}");
            }, "Set game started", error => { if (IsCurrentLobbyRequest(generation, lobbyId)) FireError(error); });
        }

        public bool IsGameStarted()
        {
            if (currentLobby?.Data == null) return false;

            if (currentLobby.Data.TryGetValue("GameStarted", out var data))
            {
                return data.Value == "true";
            }
            return false;
        }

        public void ForceCleanup()
        {
            _operationGeneration++;
            currentLobby = null;
            isHost = false;
            isGameSessionActive = false;
            OnLobbyLeft?.Invoke();
        }

        bool AcceptUpdatedLobby(Lobby snapshot, Session.LobbySessionWork owner, long epoch)
        {
            var coordinator = Session.NetworkSessionCoordinator.Instance;
            if (owner != null) return coordinator != null && coordinator.AcceptLobbyResponse(owner, snapshot, epoch);
            // A legacy request cannot replace a snapshot now owned by an online lease.
            if (coordinator?.OwnsOnlineSession == true) return false;
            if (snapshot == null || snapshot.Id != currentLobby?.Id || snapshot.Version <= currentLobby.Version)
                return false;
            currentLobby = snapshot;
            return true;
        }

        internal void SyncFromCoordinator(Lobby lobby, bool isHostRole)
        {
            if (currentLobby?.Id != lobby?.Id || lobby == null) _operationGeneration++;
            currentLobby = lobby;
            isHost = isHostRole;
        }

        internal void FireCreatedEvent() => OnLobbyCreated?.Invoke(currentLobby);
        internal void FireJoinedEvent() => OnLobbyJoined?.Invoke(currentLobby);
        internal void FireUpdatedEvent() => OnLobbyUpdated?.Invoke(currentLobby);
        internal void FireLeftEvent() => OnLobbyLeft?.Invoke();

        public void SetGameSessionActive(bool active)
        {
            isGameSessionActive = active;
            heartbeatTimer = heartbeatInterval;
            pollTimer = lobbyPollInterval;
            Debug.Log($"[LobbyManager] Game session active: {isGameSessionActive}");
        }

        #endregion

        #region Heartbeat & Polling

        private void HandleHeartbeat()
        {
            if (isGameSessionActive) return;
            if (!isHost || currentLobby == null) return;

            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                heartbeatTimer = heartbeatInterval;
                _ = SendHeartbeatAsync();
            }
        }

        private async Task SendHeartbeatAsync()
        {
            if (_heartbeatInFlight) return;
            _heartbeatInFlight = true;
            try
            {
                string lobbyId = currentLobby?.Id;
                if (lobbyId == null) return;

                await LobbyServiceHelper.ExecuteAsync(async () =>
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
                    Debug.Log("[LobbyManager] Heartbeat sent");
                }, "Heartbeat");
            }
            finally
            {
                _heartbeatInFlight = false;
            }
        }

        private void HandleLobbyPoll()
        {
            if (isGameSessionActive) return;
            if (currentLobby == null) return;

            pollTimer -= Time.deltaTime;
            if (pollTimer <= 0f)
            {
                pollTimer = lobbyPollInterval;
                _ = PollLobbyAsync();
            }
        }

        private async Task PollLobbyAsync()
        {
            if (_pollInFlight) return;
            _pollInFlight = true;
            string lobbyId = currentLobby?.Id;
            uint gen = _operationGeneration;
            try
            {
                if (lobbyId == null) return;

                var lobby = await LobbyService.Instance.GetLobbyAsync(lobbyId);

                if (!IsCurrentLobbyRequest(gen, lobbyId)) return;
                if (AcceptUpdatedLobby(lobby, null, 0))
                    OnLobbyUpdated?.Invoke(currentLobby);
            }
            catch (LobbyServiceException e)
            {
                if (!IsCurrentLobbyRequest(gen, lobbyId)) return;
                if (e.Reason == LobbyExceptionReason.LobbyNotFound
                    || e.Reason == LobbyExceptionReason.LobbyConflict)
                {
                    Debug.Log($"[LobbyManager] Lobby gone ({e.Reason}). Returning to main.");
                    currentLobby = null;
                    isHost = false;
                    OnLobbyLeft?.Invoke();
                }
                else
                {
                    Debug.LogWarning($"[LobbyManager] Lobby polling failed: {e.Message}");
                }
            }
            catch (Exception e)
            {
                if (!IsCurrentLobbyRequest(gen, lobbyId)) return;
                Debug.LogWarning($"[LobbyManager] Unexpected poll error: {e.Message}");
                if (currentLobby != null)
                {
                    currentLobby = null;
                    isHost = false;
                    OnLobbyLeft?.Invoke();
                }
            }
            finally
            {
                _pollInFlight = false;
            }
        }

        #endregion

        #region Debug

        public void PrintLobbyInfo()
        {
            if (currentLobby == null)
            {
                Debug.Log("[LobbyManager] No active lobby");
                return;
            }

            Debug.Log("========== Lobby Info ==========");
            Debug.Log($"Name: {currentLobby.Name}");
            Debug.Log($"ID: {currentLobby.Id}");
            Debug.Log($"Code: {currentLobby.LobbyCode}");
            Debug.Log($"Players: {currentLobby.Players.Count}/{currentLobby.MaxPlayers}");
            Debug.Log($"IsPrivate: {currentLobby.IsPrivate}");
            Debug.Log($"HostId: {currentLobby.HostId}");

            foreach (var player in currentLobby.Players)
            {
                var playerName = player.Data != null && player.Data.ContainsKey("PlayerName")
                    ? player.Data["PlayerName"].Value
                    : "Unknown";
                var isReady = player.Data != null && player.Data.ContainsKey("IsReady")
                    ? player.Data["IsReady"].Value
                    : "false";
                var isHostPlayer = player.Id == currentLobby.HostId ? " [HOST]" : "";

                Debug.Log($"  - {playerName} (Ready: {isReady}){isHostPlayer}");
            }
            Debug.Log("================================");
        }

        #endregion
    }
}
