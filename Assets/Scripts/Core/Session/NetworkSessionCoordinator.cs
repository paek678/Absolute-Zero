using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Network;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

using LobbyPlayer = Unity.Services.Lobbies.Models.Player;

namespace AbsoluteZero.Core.Session
{
    public enum SessionState
    {
        Offline,
        Initializing,
        Ready,
        Connecting,
        LoadingGame,
        InGame,
        Disconnecting,
        Failed
    }

    public enum SessionOperation
    {
        None,
        CreatingLobby,
        JoiningLobby,
        WaitingRelayCode,
        AllocatingRelay,
        JoiningRelay,
        StartingHost,
        StartingClient,
        WaitingForPlayers
    }

    public class NetworkSessionCoordinator : MonoBehaviour
    {
        public static NetworkSessionCoordinator Instance { get; private set; }

        [SerializeField] float relayCodeTimeoutSeconds = 30f;
        [SerializeField] float gameEntryTimeoutSeconds = 60f;
        [SerializeField] int maxPlayers = 2;

        SessionState _state = SessionState.Offline;
        SessionOperation _operation = SessionOperation.None;
        uint _operationGeneration;
        string _lastError;
        bool _isHostRole;
        Lobby _currentLobby;
        LobbySessionWork _lobbyWork;
        MatchSessionLease _lobbyWorkLease;
        internal LobbySessionWork LobbyWork => EnsureLobbyWork();

        GameMode _selectedMode = GameMode.OneVsOne;
        int _selectedPlayerCount = 2;

        SessionParticipantTable _participantTable;

        IUnityServicesGateway _services;
        ILobbyGateway _lobbyGateway;
        IRelayGateway _relayGateway;
        INetworkRuntime _networkRuntime;
        ISceneTransitionService _sceneTransition;
        MatchSessionLease _lease;
        Task<Result<Unit>> _servicesTask;
        Task<Result<Unit>> _initializationTask;
        MatchSessionLease _initializationLease;

        public MatchSessionRouter Router { get; set; }
        MatchSessionRouter SessionRouter => Router ?? AppBootstrapper.Instance?.SessionRouter;
        public MatchSessionLease OnlineLease => _lease;
        public bool HasOnlineOwnership => IsCurrentSession(_lease);
        public bool OwnsOnlineSession => _lease != null && SessionRouter != null && SessionRouter.Owns(_lease);
        public bool IsCurrentSession(MatchSessionLease lease)
            => this != null && lease != null && ReferenceEquals(lease, _lease)
                && SessionRouter != null && SessionRouter.IsCurrent(lease);

        public SessionState State => _state;
        public SessionOperation Operation => _operation;
        public string LastError => _lastError;
        public bool IsHostRole => _isHostRole;
        public Lobby CurrentLobby => _currentLobby;
        public GameMode SelectedMode => _selectedMode;
        public int SelectedPlayerCount => _selectedPlayerCount;

        public SessionParticipantTable ParticipantTable => _participantTable;

        public event Action<SessionState, SessionOperation> OnStateChanged;
        public event Action<string> OnError;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(this);
                return;
            }

            _services = new UnityServicesGateway();
            _lobbyGateway = new LobbyGateway();
            _relayGateway = new RelayGateway();
            _networkRuntime = new NgoNetworkRuntime();
            _sceneTransition = new SceneTransitionService("LobbyScene");
        }

        // Online services start only in an explicit online command.

        void OnEnable()
        {
            TrySubscribeNgoCallbacks();
            TrySubscribeLobbyPoll();
        }

        void OnDisable()
        {
            DisposeLobbyWork();
            if (_lobbyPollSubscribed)
            {
                if (_subscribedLobbyManager != null)
                {
                    _subscribedLobbyManager.OnLobbyUpdated -= OnLobbyPolled;
                    _subscribedLobbyManager.OnLobbyLeft -= OnLobbyLost;
                }
                _subscribedLobbyManager = null;
                _lobbyPollSubscribed = false;
            }

            if (_ngoCallbacksSubscribed)
            {
                if (_subscribedNetworkManager != null)
                    _subscribedNetworkManager.OnClientStopped -= _networkStoppedHandler;
                _subscribedNetworkManager = null;
                _subscribedNetworkLease = null;
                _networkStoppedHandler = null;
                _ngoCallbacksSubscribed = false;
            }
        }

        void OnDestroy()
        {
            DisposeLobbyWork();
            _operationGeneration++;
            UnregisterConnectionApproval();
            UnregisterSceneLoadCallback();
            if (Instance == this) Instance = null;
        }

        bool _lobbyPollSubscribed;
        LobbyManager _subscribedLobbyManager;

        void TrySubscribeLobbyPoll()
        {
            var lobbyMgr = LobbyManager.Instance;
            if (_lobbyPollSubscribed && _subscribedLobbyManager == lobbyMgr) return;
            if (_subscribedLobbyManager != null)
            {
                _subscribedLobbyManager.OnLobbyUpdated -= OnLobbyPolled;
                _subscribedLobbyManager.OnLobbyLeft -= OnLobbyLost;
            }
            _lobbyPollSubscribed = false;
            _subscribedLobbyManager = null;
            if (lobbyMgr == null) return;
            lobbyMgr.OnLobbyUpdated += OnLobbyPolled;
            lobbyMgr.OnLobbyLeft += OnLobbyLost;
            _subscribedLobbyManager = lobbyMgr;
            _lobbyPollSubscribed = true;
        }

        void OnLobbyLost()
        {
            if (HasOnlineOwnership && _currentLobby != null) ObserveUnexpectedLeave();
        }

        void OnLobbyPolled(Lobby lobby)
        {
            if (!HasOnlineOwnership) return;
            if (_currentLobby == null || lobby == null) return;
            if (_currentLobby.Id != lobby.Id) return;
            var work = EnsureLobbyWork();
            work?.TryAccept(lobby, work.MetadataEpoch);
        }

        void Update()
        {
            var work = EnsureLobbyWork();
            work?.Tick(Time.deltaTime, _isHostRole, LobbyManager.Instance?.IsGameSessionActive == true);
        }

        LobbySessionWork EnsureLobbyWork()
        {
            if (!isActiveAndEnabled || !HasOnlineOwnership || _currentLobby == null)
            { DisposeLobbyWork(); return null; }
            if (_lobbyWork != null && ReferenceEquals(_lobbyWorkLease, _lease) &&
                _lobbyWork.Current.Id == _currentLobby.Id) return _lobbyWork;
            DisposeLobbyWork();
            var lease = _lease;
            var manager = LobbyManager.Instance;
            string playerId = _services.PlayerId;
            string lobbyId = _currentLobby.Id;
            LobbySessionWork work = null;
            work = new LobbySessionWork(_lobbyGateway, _currentLobby, playerId, lease,
                () => this != null && isActiveAndEnabled && IsCurrentSession(lease) &&
                    _services.PlayerId == playerId && _currentLobby?.Id == lobbyId && ReferenceEquals(_lobbyWork, work),
                lobby =>
                {
                    _currentLobby = lobby;
                    var currentManager = LobbyManager.Instance;
                    currentManager?.SyncFromCoordinator(lobby, _isHostRole);
                    currentManager?.FireUpdatedEvent();
                }, OnLobbyLost, message => Debug.LogWarning("[SessionCoordinator] Lobby: " + message),
                manager != null ? manager.HeartbeatInterval : 15f,
                manager != null ? manager.PollInterval : 2f);
            _lobbyWork = work;
            _lobbyWorkLease = lease;
            return work;
        }

        void DisposeLobbyWork()
        {
            _lobbyWork?.Dispose();
            _lobbyWork = null;
            _lobbyWorkLease = null;
        }

        internal bool AcceptLobbyResponse(LobbySessionWork owner, Lobby lobby, long capturedEpoch)
            => ReferenceEquals(owner, EnsureLobbyWork()) && owner != null && owner.TryAccept(lobby, capturedEpoch);

        public Task<CosmeticPublicationResult> PublishCosmeticsAsync(string dto, long equipmentRevision)
        {
            var work = EnsureLobbyWork();
            return work != null ? work.PublishAsync(dto, equipmentRevision)
                : Task.FromResult(new CosmeticPublicationResult(CosmeticPublicationStatus.NotApplicable,
                    equipmentRevision, _lease));
        }

        bool _ngoCallbacksSubscribed;
        Unity.Netcode.NetworkManager _subscribedNetworkManager;
        MatchSessionLease _subscribedNetworkLease;
        Action<bool> _networkStoppedHandler;

        void TrySubscribeNgoCallbacks()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (_ngoCallbacksSubscribed && _subscribedNetworkManager == nm && ReferenceEquals(_subscribedNetworkLease, _lease)) return;
            if (_subscribedNetworkManager != null)
                _subscribedNetworkManager.OnClientStopped -= _networkStoppedHandler;
            _ngoCallbacksSubscribed = false;
            _subscribedNetworkManager = null;
            _subscribedNetworkLease = null;
            _networkStoppedHandler = null;
            if (nm == null) return;
            _subscribedNetworkManager = nm;
            var lease = _lease;
            _subscribedNetworkLease = lease;
            _networkStoppedHandler = wasHost =>
            {
                if (nm == Unity.Netcode.NetworkManager.Singleton && IsCurrentSession(lease)) OnNetworkStopped(wasHost);
            };
            nm.OnClientStopped += _networkStoppedHandler;
            _ngoCallbacksSubscribed = true;
        }

        void OnNetworkStopped(bool wasHost)
        {
            if (!HasOnlineOwnership) return;
            if (_state == SessionState.Disconnecting) return;
            // The pending entry operation owns cleanup and reports its failure.
            if (_state == SessionState.LoadingGame) return;

            if (_state != SessionState.InGame && _state != SessionState.LoadingGame &&
                _state != SessionState.Connecting)
                return;

            Debug.Log("[SessionCoordinator] External network stop detected — leaving session");
            ObserveUnexpectedLeave();
        }

        async void ObserveUnexpectedLeave()
        {
            try { await LeaveAsync(); }
            catch (Exception error) { Debug.LogException(error); }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugSetSceneTransitionForValidation(ISceneTransitionService sceneTransition)
        {
            if (OwnsOnlineSession) throw new InvalidOperationException("Cannot replace scene navigation during a session");
            _sceneTransition = sceneTransition ?? throw new ArgumentNullException(nameof(sceneTransition));
        }

        // Direct-UTP fixtures opt into the same lifecycle without changing release admission.
        public void DebugAdoptLocalNetworkSession()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (_currentLobby == null && nm != null && nm.IsListening && TryAcquireOnlineLease())
            {
                TrySubscribeSceneLoadCallback();
                SetState(SessionState.InGame);
            }
        }
#endif

        #region State Machine

        void SetState(SessionState newState, SessionOperation newOp = SessionOperation.None)
        {
            if (_state == newState && _operation == newOp) return;
            Debug.Log($"[SessionCoordinator] {_state}/{_operation} -> {newState}/{newOp}");
            _state = newState;
            _operation = newOp;
            OnStateChanged?.Invoke(_state, _operation);
        }

        void SetError(string message)
        {
            _lastError = message;
            OnError?.Invoke(message);
        }

        OperationScope BeginOperation()
        {
            _lastError = null;
            _operationGeneration++;
            var lease = _lease;
            uint captured = _operationGeneration;
            return new OperationScope(() => IsCurrentSession(lease) ? _operationGeneration : unchecked(captured + 1));
        }

        #endregion

        #region Public Commands

        bool TryAcquireOnlineLease()
        {
            var router = SessionRouter;
            if (router == null || _selectedMode == GameMode.Solo) return false;
            if (IsCurrentSession(_lease)) return true;
            MatchSessionLease lease = null;
            if (!router.TryBegin(_selectedMode, () => LeaveOwnedAsync(lease), out lease)) return false;
            _lease = lease;
            _operationGeneration++;
            TrySubscribeNgoCallbacks();
            return true;
        }

        public Task<Result<Unit>> EnsureInitializedAsync()
        {
            if (!TryAcquireOnlineLease())
                return Task.FromResult(Result<Unit>.Failure(OperationErrorCode.InvalidState, "Another session owns the network, or local bootstrap is unavailable"));
            if (_services.IsInitialized && _services.IsSignedIn)
            {
                TrySubscribeNgoCallbacks();
                TrySubscribeLobbyPoll();
                if (_state == SessionState.Offline || _state == SessionState.Failed) SetState(SessionState.Ready);
                return Task.FromResult(Result<Unit>.Success(Unit.Value));
            }
            if (_initializationTask != null && !_initializationTask.IsCompleted && ReferenceEquals(_initializationLease, _lease))
                return _initializationTask;
            _initializationLease = _lease;
            return _initializationTask = InitializeOwnedAsync(_lease, _operationGeneration);
        }

        async Task<Result<Unit>> InitializeOwnedAsync(MatchSessionLease lease, uint generation)
        {
            SetState(SessionState.Initializing);
            Result<Unit> result;
            try
            {
                // A canceled application operation must not start a second uncancelable SDK flight.
                if (_servicesTask == null || _servicesTask.IsCompleted)
                    _servicesTask = _services.InitializeAndSignInAsync(DetectParrelSyncProfile());
                result = await _servicesTask;
            }
            catch (Exception e) { result = Result<Unit>.Failure(OperationErrorCode.AuthenticationFailed, e.Message); }
            if (!IsCurrentSession(lease) || generation != _operationGeneration)
                return Result<Unit>.Failure(OperationErrorCode.Cancelled, "Online initialization was superseded");
            if (result.IsFailure)
            {
                SetError(result.ErrorMessage);
                SetState(SessionState.Failed);
                SessionRouter.End(lease);
                return result;
            }
            TrySubscribeNgoCallbacks();
            TrySubscribeLobbyPoll();
            SetState(SessionState.Ready);
            return result;
        }

        public async Task InitializeAsync() { await EnsureInitializedAsync(); }
        public async Task RetryInitializeAsync() { if (_state == SessionState.Failed) await EnsureInitializedAsync(); }

        public void SetMatchParameters(GameMode mode, int playerCount)
        {
            if (mode == GameMode.Solo || SessionRouter?.IsSolo == true || _currentLobby != null
                || _state == SessionState.Initializing || _state == SessionState.Connecting
                || _state == SessionState.LoadingGame || _state == SessionState.InGame || _state == SessionState.Disconnecting) return;
            if (HasOnlineOwnership && _lease.Mode != mode)
            {
                SessionRouter.End(_lease);
                _lease = null;
                _operationGeneration++;
            }
            _selectedMode = mode;
            _selectedPlayerCount = Mathf.Clamp(playerCount, 2, 4);
            Debug.Log($"[SessionCoordinator] Match params set — Mode={_selectedMode}, Players={_selectedPlayerCount}");
        }

        public async Task<Result<Unit>> CreateLobbyAsync(string lobbyName = null)
        {
            var initialization = EnsureInitializedAsync();
            var lease = _lease;
            var ready = await initialization;
            if (ready.IsFailure) return ready;
            if (!IsCurrentSession(lease)) return Result<Unit>.Failure(OperationErrorCode.Cancelled, "Online command was superseded");
            if (_state != SessionState.Ready || _currentLobby != null)
                return Result<Unit>.Failure(OperationErrorCode.InvalidState, $"Cannot create lobby from state {_state}");

            var scope = BeginOperation();
            _isHostRole = true;
            lobbyName ??= $"AZ_{UnityEngine.Random.Range(1000, 9999)}";

            SetState(SessionState.Connecting, SessionOperation.CreatingLobby);
            int lobbySlots = _selectedPlayerCount;
            var createResult = await _lobbyGateway.CreateAsync(lobbyName, lobbySlots, new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = CreatePlayerData(),
                Data = new Dictionary<string, DataObject>
                {
                    { "GameMode", new DataObject(DataObject.VisibilityOptions.Public, _selectedMode.ToString()) },
                    { "PlayerCount", new DataObject(DataObject.VisibilityOptions.Public, lobbySlots.ToString()) },
                    { "HostReady", new DataObject(DataObject.VisibilityOptions.Public, "false") },
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, "") },
                    { "GameStarted", new DataObject(DataObject.VisibilityOptions.Member, "false") }
                }
            });

            if (scope.IsStale)
            {
                if (createResult.IsSuccess) ObserveRemoteCleanup(_lobbyGateway.DeleteAsync(createResult.Value.Id));
                return Result<Unit>.Failure(OperationErrorCode.Cancelled, "Lobby creation was superseded");
            }

            if (createResult.IsFailure)
            {
                _isHostRole = false;
                return FailAndRecover(createResult.ErrorCode, createResult.ErrorMessage);
            }

            _currentLobby = createResult.Value;
            SyncLobbyManager(_currentLobby, true);
            LobbyManager.Instance?.FireCreatedEvent();

            SetState(SessionState.Ready);
            return Result<Unit>.Success(Unit.Value);
        }

        public async Task<Result<Unit>> StartMatchAsHostAsync()
        {
            var initialization = EnsureInitializedAsync();
            var lease = _lease;
            var ready = await initialization;
            if (ready.IsFailure) return ready;
            if (!IsCurrentSession(lease)) return Result<Unit>.Failure(OperationErrorCode.Cancelled, "Online command was superseded");
            if (_state != SessionState.Ready || _currentLobby == null || !_isHostRole)
                return Result<Unit>.Failure(OperationErrorCode.InvalidState, "No active lobby or not host");

            TrySubscribeNgoCallbacks();
            var scope = BeginOperation();
            string lobbyId = _currentLobby.Id;
            scope.PushCompensation(() => CleanupLobby(lobbyId, scope));

            // 1) Allocate relay — maxConnections = joining clients (playerCount - 1)
            SetState(SessionState.Connecting, SessionOperation.AllocatingRelay);
            int relayConnections = _selectedPlayerCount - 1;

            var relayResult = await _relayGateway.AllocateAsync(relayConnections);
            if (scope.IsStale) return await scope.CancelWithCompensation();
            if (relayResult.IsFailure) return await RecoverOperationFailure(scope, relayResult.ErrorCode, relayResult.ErrorMessage);

            // 2) Start host — register ConnectionApproval first
            SetState(SessionState.Connecting, SessionOperation.StartingHost);
            RegisterConnectionApproval();
            scope.PushCompensation(() => { if (!scope.IsStale) { UnregisterConnectionApproval(); _networkRuntime.Shutdown(); } return Task.CompletedTask; });

            var hostResult = _networkRuntime.StartHost(relayResult.Value.ServerData);
            if (scope.IsStale) return await scope.CancelWithCompensation();
            if (hostResult.IsFailure) return await RecoverOperationFailure(scope, hostResult.ErrorCode, hostResult.ErrorMessage);
            TrySubscribeSceneLoadCallback();

            // 3) Publish relay code to lobby
            var lobbyWork = EnsureLobbyWork();
            long metadataEpoch = lobbyWork?.MetadataEpoch ?? 0;
            var updateResult = await _lobbyGateway.UpdateAsync(lobbyId, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayResult.Value.JoinCode) },
                    { "GameStarted", new DataObject(DataObject.VisibilityOptions.Member, "true") }
                }
            });

            if (scope.IsStale) return await scope.CancelWithCompensation();
            if (updateResult.IsFailure)
                return await RecoverOperationFailure(scope, updateResult.ErrorCode, updateResult.ErrorMessage);
            if (updateResult.Value == null)
                return await RecoverOperationFailure(scope, OperationErrorCode.Unexpected, "Missing relay publication snapshot");
            AcceptLobbyResponse(lobbyWork, updateResult.Value, metadataEpoch);

            // 4) Load game scene — branch by mode
            SetState(SessionState.LoadingGame);
            LobbyManager.Instance?.SetGameSessionActive(true);

            string sceneName = _selectedMode == GameMode.Multi ? "GameScene_Multi" : "GameScene";
            var sceneResult = _networkRuntime.LoadNetworkScene(sceneName);
            if (sceneResult.IsFailure)
            {
                LobbyManager.Instance?.SetGameSessionActive(false);
                return await RecoverOperationFailure(scope, sceneResult.ErrorCode, sceneResult.ErrorMessage);
            }

            return await CompleteGameEntryAsync(scope, sceneName);
        }

        public async Task<Result<Unit>> JoinGameAsync(string lobbyCode)
        {
            var initialization = EnsureInitializedAsync();
            var lease = _lease;
            var ready = await initialization;
            if (ready.IsFailure) return ready;
            if (!IsCurrentSession(lease)) return Result<Unit>.Failure(OperationErrorCode.Cancelled, "Online command was superseded");
            if (_state != SessionState.Ready || _currentLobby != null)
                return Result<Unit>.Failure(OperationErrorCode.InvalidState, $"Cannot join from state {_state}");

            TrySubscribeNgoCallbacks();
            var scope = BeginOperation();
            _isHostRole = false;

            // 1) Join lobby by code
            SetState(SessionState.Connecting, SessionOperation.JoiningLobby);
            var joinResult = await _lobbyGateway.JoinByCodeAsync(lobbyCode, new JoinLobbyByCodeOptions
            {
                Player = CreatePlayerData()
            });

            if (scope.IsStale)
            {
                if (joinResult.IsSuccess) scope.PushCompensation(() => LeaveLobbyCleanup(joinResult.Value.Id, scope));
                return await scope.CancelWithCompensation();
            }
            if (joinResult.IsFailure) return FailAndRecover(joinResult.ErrorCode, joinResult.ErrorMessage);

            _currentLobby = joinResult.Value;
            SyncLobbyManager(_currentLobby, false);
            LobbyManager.Instance?.FireJoinedEvent();

            string joinedLobbyId = _currentLobby.Id;
            scope.PushCompensation(() => LeaveLobbyCleanup(joinedLobbyId, scope));

            // 2) Wait for relay code
            SetState(SessionState.Connecting, SessionOperation.WaitingRelayCode);
            var relayCodeResult = await WaitForRelayCodeAsync(scope);
            if (scope.IsStale) return await scope.CancelWithCompensation();
            if (relayCodeResult.IsFailure) return await RecoverOperationFailure(scope, relayCodeResult.ErrorCode, relayCodeResult.ErrorMessage);

            // 3) Join relay
            SetState(SessionState.Connecting, SessionOperation.JoiningRelay);
            var relayResult = await _relayGateway.JoinAsync(relayCodeResult.Value);
            if (scope.IsStale) return await scope.CancelWithCompensation();
            if (relayResult.IsFailure) return await RecoverOperationFailure(scope, relayResult.ErrorCode, relayResult.ErrorMessage);

            // 4) Start client
            SetState(SessionState.Connecting, SessionOperation.StartingClient);
            scope.PushCompensation(() => { if (!scope.IsStale) _networkRuntime.Shutdown(); return Task.CompletedTask; });

            var clientResult = _networkRuntime.StartClient(relayResult.Value.ServerData);
            if (clientResult.IsFailure) return await RecoverOperationFailure(scope, clientResult.ErrorCode, clientResult.ErrorMessage);
            TrySubscribeSceneLoadCallback();

            // 5) Scene will be loaded by host via NGO SceneManager
            SetState(SessionState.LoadingGame);
            LobbyManager.Instance?.SetGameSessionActive(true);

            string sceneName = _selectedMode == GameMode.Multi ? "GameScene_Multi" : "GameScene";
            return await CompleteGameEntryAsync(scope, sceneName);
        }

        async Task<Result<Unit>> CompleteGameEntryAsync(OperationScope scope, string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + gameEntryTimeoutSeconds;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            while (this != null && !scope.IsStale)
            {
                if (nm == null || !nm.IsListening)
                {
                    string reason = nm != null ? nm.DisconnectReason : null;
                    string message = string.IsNullOrEmpty(reason)
                        ? "Game connection closed before scene synchronization completed" : reason;
                    SetError(message);
                    await LeaveAsync();
                    return Result<Unit>.Failure(OperationErrorCode.NetworkStartFailed, message);
                }

                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(sceneName);
                // NGO sets IsConnectedClient after initial scene synchronization.
                if (nm.IsConnectedClient && scene.IsValid() && scene.isLoaded &&
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene() == scene)
                {
                    SetState(SessionState.InGame);
                    return Result<Unit>.Success(Unit.Value);
                }

                if (Time.realtimeSinceStartup >= deadline)
                {
                    const string message = "Timed out waiting for game scene synchronization";
                    SetError(message);
                    await LeaveAsync();
                    return Result<Unit>.Failure(OperationErrorCode.Timeout, message);
                }
                await Task.Delay(100);
            }
            // A newer leave/join owns the session; never shut it down here.
            return Result<Unit>.Failure(OperationErrorCode.Cancelled, "Game entry was cancelled");
        }

        public Task LeaveAsync()
        {
            return OwnsOnlineSession ? SessionRouter.StopAsync() : Task.CompletedTask;
        }

        async Task LeaveOwnedAsync(MatchSessionLease lease)
        {
            var router = SessionRouter;
            if (router == null || !router.Owns(lease)) return;

            // Capture the old session before callbacks or a remote await can change ownership.
            var oldLobby = _currentLobby;
            bool oldHost = _isHostRole;
            string oldPlayerId = _services.PlayerId;
            uint generation = ++_operationGeneration;
            DisposeLobbyWork();
            SetState(SessionState.Disconnecting);
            // NGO can invoke this while its shutdown stack is still unwinding.
            await Task.Yield();
            if (this == null || generation != _operationGeneration || !router.Owns(lease)) return;

            // NGO shutdown does not cancel Unity's native scene operation. Keep the
            // invalidated owner until late activation settles, then choose the menu route.
            float sceneDeadline = Time.realtimeSinceStartup + 35f;
            while (_nativeSceneLoad != null && !_nativeSceneLoad.isDone)
            {
                if (Time.realtimeSinceStartup >= sceneDeadline)
                    throw new TimeoutException("Online scene load is still pending; session ownership is retained");
                await Task.Delay(20);
            }
            bool returnToLobby = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "LobbyScene";
            UnregisterSceneLoadCallback();
            _nativeSceneLoad = null;

            // Do not release the lease or restore a scene while NGO is still stopping.
            UnregisterConnectionApproval();
            _networkRuntime.Shutdown();
            if (_networkRuntime is NgoNetworkRuntime)
            {
                var nm = Unity.Netcode.NetworkManager.Singleton;
                float deadline = Time.realtimeSinceStartup + 5f;
                while (nm != null && (nm.IsListening || nm.ShutdownInProgress))
                {
                    if (Time.realtimeSinceStartup >= deadline)
                        throw new TimeoutException("Online network shutdown did not complete; session ownership is retained");
                    await Task.Yield();
                }
            }

            try
            {
                RelayManager.Instance?.ClearState();
                var lobbyManager = LobbyManager.Instance;
                if (lobbyManager != null)
                {
                    lobbyManager.SyncFromCoordinator(null, false);
                    lobbyManager.SetGameSessionActive(false);
                }
                if (oldLobby != null)
                {
                    var cleanup = oldHost ? _lobbyGateway.DeleteAsync(oldLobby.Id)
                        : _lobbyGateway.RemovePlayerAsync(oldLobby.Id, oldPlayerId);
                    if (await Task.WhenAny(cleanup, Task.Delay(3000)) == cleanup)
                        await cleanup;
                    else
                    {
                        Debug.LogWarning("[SessionCoordinator] Remote cleanup exceeded 3s; completing local recovery");
                        ObserveRemoteCleanup(cleanup);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SessionCoordinator] Leave cleanup error: {e.Message}");
            }
            finally
            {
                // A destroyed coordinator or a newer operation owns all subsequent state.
                if (this != null && generation == _operationGeneration && router.Owns(lease))
                {
                    _currentLobby = null;
                    _isHostRole = false;
                    _selectedMode = GameMode.OneVsOne;
                    _selectedPlayerCount = 2;
                    _participantTable?.Clear();
                    _participantTable = null;
                    var lobbyMgr = LobbyManager.Instance;
                    if (lobbyMgr != null)
                    {
                        lobbyMgr.SyncFromCoordinator(null, false);
                        lobbyMgr.SetGameSessionActive(false);
                        lobbyMgr.FireLeftEvent();
                    }
                    try
                    {
                        if (returnToLobby)
                        {
                            if (_sceneTransition is IAsyncSceneTransitionService asyncScenes)
                                await asyncScenes.LoadTitleSceneAsync();
                            else
                                _sceneTransition.LoadTitleScene();
                        }
                        if (this != null && generation == _operationGeneration && router.Owns(lease))
                            SetState(_services.IsInitialized && _services.IsSignedIn
                                ? SessionState.Ready : SessionState.Offline);
                    }
                    catch (Exception error)
                    {
                        if (this != null && generation == _operationGeneration && router.Owns(lease))
                        {
                            _lastError = error.Message;
                            SetState(SessionState.Failed);
                            OnError?.Invoke(_lastError);
                        }
                        throw;
                    }
                }
            }
        }

        static async void ObserveRemoteCleanup(Task<Result<Unit>> cleanup)
        {
            // This continuation can only observe the captured old request, never mutate session state.
            try { await cleanup; }
            catch (Exception error) { Debug.LogWarning("[SessionCoordinator] Late cleanup: " + error.Message); }
        }

        #endregion

        #region Internal Helpers

        Unity.Netcode.NetworkSceneManager _sceneEvents;
        MatchSessionLease _sceneLease;
        Unity.Netcode.NetworkSceneManager.OnLoadDelegateHandler _sceneLoadHandler;
        AsyncOperation _nativeSceneLoad;

        void TrySubscribeSceneLoadCallback()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            var events = nm != null ? nm.SceneManager : null;
            if (events == null || !HasOnlineOwnership) return;
            if (ReferenceEquals(events, _sceneEvents) && ReferenceEquals(_sceneLease, _lease)) return;
            UnregisterSceneLoadCallback();
            var lease = _lease;
            _sceneEvents = events;
            _sceneLease = lease;
            _sceneLoadHandler = (client, scene, mode, operation) =>
            {
                if (nm == Unity.Netcode.NetworkManager.Singleton && ReferenceEquals(nm.SceneManager, events)
                    && SessionRouter != null && SessionRouter.Owns(lease) && client == nm.LocalClientId)
                    _nativeSceneLoad = operation;
            };
            events.OnLoad += _sceneLoadHandler;
        }

        void UnregisterSceneLoadCallback()
        {
            if (_sceneEvents != null) _sceneEvents.OnLoad -= _sceneLoadHandler;
            _sceneEvents = null;
            _sceneLease = null;
            _sceneLoadHandler = null;
        }

        Unity.Netcode.NetworkManager _approvalManager;
        Action<Unity.Netcode.NetworkManager.ConnectionApprovalRequest, Unity.Netcode.NetworkManager.ConnectionApprovalResponse> _approvalHandler;

        void RegisterConnectionApproval()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null) return;
            UnregisterConnectionApproval();
            var lease = _lease;
            _approvalManager = nm;
            _approvalHandler = (request, response) =>
            {
                if (nm != Unity.Netcode.NetworkManager.Singleton || !IsCurrentSession(lease))
                {
                    response.Approved = false;
                    response.CreatePlayerObject = false;
                    response.Reason = "Session is no longer accepting connections";
                    return;
                }
                ApproveConnection(request, response);
            };
            nm.ConnectionApprovalCallback += _approvalHandler;
            nm.NetworkConfig.ConnectionApproval = true;
        }

        void UnregisterConnectionApproval()
        {
            if (_approvalManager != null) _approvalManager.ConnectionApprovalCallback -= _approvalHandler;
            _approvalManager = null;
            _approvalHandler = null;
        }

        void ApproveConnection(
            Unity.Netcode.NetworkManager.ConnectionApprovalRequest request,
            Unity.Netcode.NetworkManager.ConnectionApprovalResponse response)
        {
            if (!HasOnlineOwnership)
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Reason = "Session is no longer accepting connections";
                return;
            }
            var nm = Unity.Netcode.NetworkManager.Singleton;
            int connected = nm != null ? nm.ConnectedClientsIds.Count : 0;

            if (connected >= _selectedPlayerCount)
            {
                response.Approved = false;
                response.Reason = "Lobby full";
                Debug.Log($"[SessionCoordinator] Connection denied — {connected}/{_selectedPlayerCount}");
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = false;
        }

        async Task<Result<Unit>> RecoverOperationFailure(OperationScope scope, OperationErrorCode code, string message)
        {
            await scope.RunCompensations();
            if (scope.IsStale) return Result<Unit>.Failure(OperationErrorCode.Cancelled, "Operation superseded during cleanup");
            return FailAndRecover(code, message);
        }

        Result<Unit> FailAndRecover(OperationErrorCode code, string message)
        {
            SetError(message);
            SetState(SessionState.Ready);
            return Result<Unit>.Failure(code, message);
        }

        public SessionParticipantTable BuildParticipantTable(IReadOnlyList<ulong> connectedClientIds, int requiredCount)
        {
            if (_participantTable != null && _participantTable.Count >= requiredCount)
            {
                Debug.Log($"[SessionCoordinator] ParticipantTable already populated ({_participantTable.Count} entries) — reusing");
                return _participantTable;
            }

            _participantTable = new SessionParticipantTable();
            var sorted = new List<ulong>(connectedClientIds);
            sorted.Sort();

            byte seat = 0;
            foreach (var clientId in sorted)
            {
                if (seat >= requiredCount) break;
                string pid = clientId.ToString();
                _participantTable.Register(pid, "match");
                _participantTable.TryValidateAndBind(pid, "match", clientId, out _);
                seat++;
            }

            Debug.Log($"[SessionCoordinator] ParticipantTable built — {seat} seats from {connectedClientIds.Count} clients");
            return _participantTable;
        }

        void SyncLobbyManager(Lobby lobby, bool isHostRole)
        {
            if (lobby == null) DisposeLobbyWork();
            else EnsureLobbyWork();
            var lobbyMgr = LobbyManager.Instance;
            if (lobbyMgr == null) return;
            lobbyMgr.SyncFromCoordinator(lobby, isHostRole);
        }

        async Task CleanupLobby(string lobbyId, OperationScope scope)
        {
            try { await _lobbyGateway.DeleteAsync(lobbyId); }
            catch (Exception e) { Debug.LogWarning($"[SessionCoordinator] Lobby cleanup failed: {e.Message}"); }
            if (scope.IsStale) return;
            _currentLobby = null;
            _isHostRole = false;
            SyncLobbyManager(null, false);
        }

        async Task LeaveLobbyCleanup(string lobbyId, OperationScope scope)
        {
            try { await _lobbyGateway.RemovePlayerAsync(lobbyId, _services.PlayerId); }
            catch (Exception e) { Debug.LogWarning($"[SessionCoordinator] Lobby leave failed: {e.Message}"); }
            if (scope.IsStale) return;
            _currentLobby = null;
            SyncLobbyManager(null, false);
        }

        async Task<Result<string>> WaitForRelayCodeAsync(OperationScope scope)
        {
            var tcs = new TaskCompletionSource<string>();
            string expectedLobbyId = _currentLobby?.Id;

            void OnLobbyUpdated(Lobby lobby)
            {
                if (!scope.IsStale && lobby?.Id == expectedLobbyId && lobby?.Data != null &&
                    lobby.Data.TryGetValue("RelayJoinCode", out var data) &&
                    !string.IsNullOrEmpty(data.Value))
                {
                    tcs.TrySetResult(data.Value);
                }
            }

            var lobbyMgr = LobbyManager.Instance;
            if (lobbyMgr == null)
                return Result<string>.Failure(OperationErrorCode.InvalidState, "LobbyManager not available");

            lobbyMgr.OnLobbyUpdated += OnLobbyUpdated;
            try
            {
                var current = lobbyMgr.CurrentLobby;
                if (!scope.IsStale && current?.Id == expectedLobbyId && current?.Data != null &&
                    current.Data.TryGetValue("RelayJoinCode", out var existing) &&
                    !string.IsNullOrEmpty(existing.Value))
                {
                    return Result<string>.Success(existing.Value);
                }

                float deadline = Time.realtimeSinceStartup + relayCodeTimeoutSeconds;
                while (!tcs.Task.IsCompleted && !scope.IsStale && Time.realtimeSinceStartup < deadline)
                    await Task.Delay(50);

                if (scope.IsStale)
                    return Result<string>.Failure(OperationErrorCode.Cancelled, "Superseded");

                if (!tcs.Task.IsCompleted)
                    return Result<string>.Failure(OperationErrorCode.Timeout, "Relay code not received in time");

                return Result<string>.Success(tcs.Task.Result);
            }
            finally
            {
                lobbyMgr.OnLobbyUpdated -= OnLobbyUpdated;
            }
        }

        LobbyPlayer CreatePlayerData()
        {
            string playerId = _services.PlayerId;
            string shortId = playerId?.Length >= 6 ? playerId[..6] : (playerId ?? "Unknown");

            var profileService = CosmeticProfileService.Instance;
            string nickname = profileService != null ? profileService.Nickname : "";
            if (string.IsNullOrEmpty(nickname))
                nickname = $"Player_{shortId}";

            string cosmeticDto = profileService != null ? (profileService.GetCompactDto() ?? "") : "";

            return new LobbyPlayer
            {
                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, nickname) },
                    { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "false") },
                    { "LastAction", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "") },
                    { "CosmeticData", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, cosmeticDto) }
                }
            };
        }

        static string DetectParrelSyncProfile()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var commandLine = Environment.GetCommandLineArgs();
            int profileIndex = Array.IndexOf(commandLine, "--az-services-profile");
            if (profileIndex >= 0 && profileIndex + 1 < commandLine.Length &&
                !string.IsNullOrWhiteSpace(commandLine[profileIndex + 1]))
            {
                string commandLineProfile = commandLine[profileIndex + 1].Trim();
                Debug.Log($"[SessionCoordinator] Services profile override: {commandLineProfile}");
                return commandLineProfile;
            }
#endif
#if UNITY_EDITOR
            try
            {
                var clonesManagerType = Type.GetType("ParrelSync.ClonesManager, ParrelSync");
                if (clonesManagerType != null)
                {
                    var isCloneMethod = clonesManagerType.GetMethod("IsClone",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var getArgumentMethod = clonesManagerType.GetMethod("GetArgument",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

                    if (isCloneMethod != null && (bool)isCloneMethod.Invoke(null, null))
                    {
                        string customArgument = getArgumentMethod?.Invoke(null, null) as string ?? "";
                        string profile = string.IsNullOrEmpty(customArgument) ? "clone" : customArgument;
                        Debug.Log($"[SessionCoordinator] ParrelSync clone detected - Profile: {profile}");
                        return profile;
                    }
                }
            }
            catch (Exception) { }
#endif
            return null;
        }

        #endregion
    }
}
