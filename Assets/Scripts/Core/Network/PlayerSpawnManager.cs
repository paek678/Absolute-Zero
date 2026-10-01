using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Maps;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Network
{
    public class PlayerSpawnManager : MonoBehaviour, IDisconnectHandler
    {
        public static PlayerSpawnManager Instance { get; private set; }

        [Header("=== Player Prefab ===")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private bool spawnPlayerCharacter = true;

        [Header("=== Spawn Points ===")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool useSceneSpawnPointMarkers = true;
        [SerializeField] private bool includeInactiveSceneSpawnMarkers = false;
        [SerializeField] private float defaultSpawnRadius = 5f;
        [SerializeField] private float topDownFallbackY = 1f;

        readonly Dictionary<ulong, NetworkObject> spawnedPlayers = new();
        readonly Dictionary<string, NetworkObject> spawnedParticipants = new(StringComparer.Ordinal);
        readonly HashSet<ulong> pendingSpawnClients = new();
        readonly List<Transform> resolvedSpawnPoints = new();
        NetworkManager _subscribedManager;
        NetworkSceneManager _subscribedSceneManager;
        MatchSessionRouter _router;
        MatchSessionLease _lease;
        long _bindingVersion;
        Scene _preparedScene;
        bool _sceneReady;
        bool _localLoadPending;
        string _loadingScene;
        string _mapLayoutError;
        Action<ulong> _connected, _disconnected;
        Action _serverStarted;
        Action _routerChanged;
        Action<bool> _stopped;
        NetworkSceneManager.OnLoadDelegateHandler _sceneLoading;
        NetworkSceneManager.OnLoadCompleteDelegateHandler _sceneLoaded;
        DisconnectDispatcher _dispatcher;
        bool _directDisconnect;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public bool DebugReverseSoloSpawnOrder { get; set; }
        public bool DebugIsSceneLoadPending => _localLoadPending;
        public Action<ulong, string, LoadSceneMode> DebugCaptureSceneLoadCallback()
        {
            var callback = _sceneLoaded;
            return (clientId, scene, mode) => callback?.Invoke(clientId, scene, mode);
        }
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start() => RefreshSubscriptions();

        void Update()
        {
            RefreshSubscriptions();
            TrySpawnPendingClients();
        }

        void RefreshSubscriptions()
        {
            var nm = NetworkManager.Singleton;
            var sceneManager = nm != null ? nm.SceneManager : null;
            var router = AppBootstrapper.Instance?.SessionRouter;
            var lease = router?.Current;
            if (ReferenceEquals(nm, _subscribedManager) && ReferenceEquals(sceneManager, _subscribedSceneManager)
                && ReferenceEquals(router, _router) && ReferenceEquals(lease, _lease))
            {
                RefreshDisconnectRoute();
                return;
            }

            // Existing direct-UTP validation adopts an already-running online session.
            // This is the same network lifetime, not another match to spawn again.
            bool adoption = ReferenceEquals(nm, _subscribedManager) && nm != null && nm.IsListening
                && ReferenceEquals(sceneManager, _subscribedSceneManager) && _lease == null
                && lease != null && lease.Mode != GameMode.Solo;
            DetachCallbacks();
            if (!adoption) ClearSessionRecords();
            else CloseSceneGate();
            _subscribedManager = nm;
            _subscribedSceneManager = sceneManager;
            _router = router;
            _lease = lease;
            long version = ++_bindingVersion;
            if (router != null)
            {
                _routerChanged = () =>
                {
                    if (this == null || !ReferenceEquals(router, _router)
                        || !ReferenceEquals(router, AppBootstrapper.Instance?.SessionRouter)) return;
                    if (router.IsStopping) CloseSceneGate();
                    // TryBegin raises Changed before StartHost. Rebind now so its
                    // synchronous OnServerStarted/OnLoad cannot fall between Updates.
                    RefreshSubscriptions();
                };
                router.Changed += _routerChanged;
            }
            if (nm == null) return;

            _connected = clientId =>
            {
                if (!CanAct(nm, version)) return;
                pendingSpawnClients.Add(clientId);
                TrySpawnPendingClients();
            };
            _disconnected = clientId =>
            {
                if (CanAct(nm, version)) OnPlayerDisconnected(clientId);
            };
            _serverStarted = () =>
            {
                if (!IsExactBinding(nm, version)) return;
                RefreshSubscriptions();
                TrySpawnPendingClients();
            };
            _stopped = wasHost =>
            {
                if (IsExactBinding(nm, version)) ClearSessionRecords();
            };
            nm.OnClientConnectedCallback += _connected;
            nm.OnServerStarted += _serverStarted;
            nm.OnServerStopped += _stopped;
            nm.OnClientStopped += _stopped;

            if (sceneManager != null)
            {
                _sceneLoading = (clientId, sceneName, mode, operation) =>
                {
                    if (!CanAct(nm, version) || !ReferenceEquals(sceneManager, _subscribedSceneManager)
                        || clientId != nm.LocalClientId) return;
                    _localLoadPending = true;
                    _loadingScene = sceneName;
                    CloseSceneGate();
                };
                _sceneLoaded = (clientId, sceneName, mode) =>
                {
                    if (!CanAct(nm, version) || !ReferenceEquals(sceneManager, _subscribedSceneManager)
                        || clientId != nm.LocalClientId) return;
                    if (_localLoadPending && !string.Equals(_loadingScene, sceneName, StringComparison.Ordinal)) return;
                    _localLoadPending = false;
                    _loadingScene = null;
                    TrySpawnPendingClients();
                };
                sceneManager.OnLoad += _sceneLoading;
                sceneManager.OnLoadComplete += _sceneLoaded;
            }
            RefreshDisconnectRoute();
        }

        bool IsExactBinding(NetworkManager nm, long version)
            => this != null && ReferenceEquals(nm, _subscribedManager) && version == _bindingVersion
                && ReferenceEquals(nm, NetworkManager.Singleton)
                && ReferenceEquals(_router, AppBootstrapper.Instance?.SessionRouter)
                && ReferenceEquals(_lease, _router?.Current);

        bool CanAct(NetworkManager nm, long version)
            => IsExactBinding(nm, version) && nm != null && nm.IsServer && nm.IsListening
                && ReferenceEquals(nm.SceneManager, _subscribedSceneManager)
                && !nm.ShutdownInProgress && (_lease == null || _router.IsCurrent(_lease));

        bool IsSolo => _lease?.Mode == GameMode.Solo || MatchCompositionRoot.Instance?.ActiveConfig?.Mode == GameMode.Solo;

        void RefreshDisconnectRoute()
        {
            var dispatcher = DisconnectDispatcher.Instance;
            if (!ReferenceEquals(dispatcher, _dispatcher))
            {
                if (_dispatcher != null) _dispatcher.ClearDefaultIfCurrent(this);
                _dispatcher = dispatcher;
                if (_dispatcher != null) _dispatcher.SetDefaultHandler(this);
            }
            bool direct = _dispatcher == null && _subscribedManager != null && _disconnected != null;
            if (direct == _directDisconnect) return;
            if (_subscribedManager != null)
            {
                if (direct) _subscribedManager.OnClientDisconnectCallback += _disconnected;
                else _subscribedManager.OnClientDisconnectCallback -= _disconnected;
            }
            _directDisconnect = direct;
        }

        void DetachCallbacks()
        {
            // Unsubscribe from the actual publisher, even if Singleton already changed.
            if (_router != null) _router.Changed -= _routerChanged;
            _routerChanged = null;
            var nm = _subscribedManager;
            if (!ReferenceEquals(nm, null))
            {
                nm.OnClientConnectedCallback -= _connected;
                nm.OnServerStarted -= _serverStarted;
                nm.OnServerStopped -= _stopped;
                nm.OnClientStopped -= _stopped;
                if (_directDisconnect) nm.OnClientDisconnectCallback -= _disconnected;
            }
            if (_subscribedSceneManager != null)
            {
                _subscribedSceneManager.OnLoad -= _sceneLoading;
                _subscribedSceneManager.OnLoadComplete -= _sceneLoaded;
            }
            _connected = _disconnected = null;
            _serverStarted = null;
            _stopped = null;
            _sceneLoading = null;
            _sceneLoaded = null;
            _directDisconnect = false;
            _subscribedManager = null;
            _subscribedSceneManager = null;
        }

        void OnDestroy()
        {
            ++_bindingVersion;
            DetachCallbacks();
            if (_dispatcher != null) _dispatcher.ClearDefaultIfCurrent(this);
            ClearSessionRecords();
            if (Instance == this) Instance = null;
        }

        void CloseSceneGate()
        {
            _sceneReady = false;
            _preparedScene = default;
            resolvedSpawnPoints.Clear();
            _mapLayoutError = null;
        }

        void ClearSessionRecords()
        {
            pendingSpawnClients.Clear();
            spawnedPlayers.Clear();
            spawnedParticipants.Clear();
            _localLoadPending = false;
            _loadingScene = null;
            CloseSceneGate();
        }

        public bool TryPrepareMatchScene(long generation, out string error)
        {
            RefreshSubscriptions();
            error = null;
            var mcr = MatchCompositionRoot.Instance;
            if (!CanAct(_subscribedManager, _bindingVersion) || mcr == null || !mcr.IsSessionCurrent)
            { error = "The current server match is not available."; return false; }
            if (generation != mcr.Generation || (IsSolo && (_lease == null || _lease.Generation != generation)))
            { error = "The match belongs to a different session generation."; return false; }
            Scene scene = mcr.gameObject.scene;
            if (_localLoadPending || !scene.IsValid() || !scene.isLoaded || scene != SceneManager.GetActiveScene())
            { error = "The current gameplay scene is still loading."; return false; }
            if (_subscribedSceneManager == null || !_subscribedSceneManager.GetSynchronizedScenes().Contains(scene))
            { error = "The gameplay scene has not completed network synchronization."; return false; }
            if (!_sceneReady || _preparedScene != scene)
            {
                ResolveSpawnPoints(scene);
                if (_mapLayoutError != null) { error = _mapLayoutError; return false; }
                _preparedScene = scene;
                _sceneReady = true;
            }
            return true;
        }

        public bool IsSpawnSceneReady(long generation)
            => TryPrepareMatchScene(generation, out _);

        void QueueExistingClients()
        {
            foreach (var client in _subscribedManager.ConnectedClientsList)
                if (!spawnedPlayers.TryGetValue(client.ClientId, out var obj) || obj == null || !obj.IsSpawned)
                    pendingSpawnClients.Add(client.ClientId);
        }

        void TrySpawnPendingClients()
        {
            if (!CanAct(_subscribedManager, _bindingVersion) || IsSolo) return;
            var mcr = MatchCompositionRoot.Instance;
            if (mcr == null || !TryPrepareMatchScene(mcr.Generation, out _)) return;
            var roster = mcr.Roster;
            if (roster != null && !roster.RosterReady) return;
            QueueExistingClients();
            foreach (ulong clientId in new List<ulong>(pendingSpawnClients))
            {
                if (!_subscribedManager.ConnectedClients.ContainsKey(clientId))
                { pendingSpawnClients.Remove(clientId); continue; }
                if (SpawnPlayerForClient(clientId, roster)) pendingSpawnClients.Remove(clientId);
            }
        }

        bool SpawnPlayerForClient(ulong clientId, MatchRoster roster)
        {
            if (!spawnPlayerCharacter || playerPrefab == null) return false;
            if (spawnedPlayers.TryGetValue(clientId, out var existing) && existing != null && existing.IsSpawned) return true;
            if (_subscribedManager.ConnectedClients[clientId].PlayerObject != null)
            {
                spawnedPlayers[clientId] = _subscribedManager.ConnectedClients[clientId].PlayerObject;
                return true;
            }
            byte seat = 0;
            bool hasSeat = roster != null && roster.TryGetSeatByClientId(clientId, out seat);
            var position = hasSeat ? GetSpawnPositionBySeat(seat) : GetSpawnPositionLegacy(clientId);
            var instance = Instantiate(playerPrefab, position, Quaternion.identity);
            SceneManager.MoveGameObjectToScene(instance, _preparedScene);
            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("[PlayerSpawnManager] Player prefab is missing NetworkObject.");
                Destroy(instance);
                return false;
            }
            networkObject.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            spawnedPlayers[clientId] = networkObject;
            return true;
        }

        public bool TrySpawnSoloParticipants(IReadOnlyList<MatchParticipantDescriptor> participants, out string error)
        {
            error = null;
            RefreshSubscriptions();
            var mcr = MatchCompositionRoot.Instance;
            if (!IsSolo || mcr == null || mcr.ActiveConfig?.Mode != GameMode.Solo || mcr.Roster == null || !mcr.Roster.RosterReady)
            { error = "Solo participants require the current ready Solo roster."; return false; }
            if (!TryPrepareMatchScene(mcr.Generation, out error)) return false;
            if (!spawnPlayerCharacter || playerPrefab == null || playerPrefab.GetComponent<NetworkObject>() == null
                || playerPrefab.GetComponent<PlayerState>() == null || playerPrefab.GetComponent<PlayerInventory>() == null)
            { error = "The Solo player prefab requires NetworkObject, PlayerState and PlayerInventory."; return false; }
            if (!MatchParticipantDescriptor.TryValidateSet(participants, mcr.ActiveConfig.RequiredPlayerCount, out var bySeat, out error))
                return false;
            int humans = 0;
            foreach (var participant in bySeat)
            {
                if (participant.Generation != mcr.Generation || !ContainsDescriptor(mcr.Participants, participant))
                { error = "A participant does not match the current Solo roster."; return false; }
                if (participant.Seat >= resolvedSpawnPoints.Count || resolvedSpawnPoints[participant.Seat] == null)
                { error = $"The current scene has no spawn marker for seat {participant.Seat}."; return false; }
                if (participant.ControllerKind == PlayerControllerKind.Human)
                {
                    humans++;
                    if (!participant.ClientId.HasValue || participant.ClientId.Value != _subscribedManager.LocalClientId
                        || !_subscribedManager.ConnectedClients.ContainsKey(participant.ClientId.Value))
                    { error = "The Solo human must use the connected local host client."; return false; }
                    var player = _subscribedManager.ConnectedClients[participant.ClientId.Value].PlayerObject;
                    if (player != null && (!spawnedParticipants.TryGetValue(participant.ParticipantId, out var tracked) || tracked != player))
                    { error = "The Solo host already owns another player object."; return false; }
                }
            }
            if (humans != 1 || bySeat.Length != 2)
            { error = "Solo requires one human and one bot."; return false; }

            for (int index = 0; index < bySeat.Length; index++)
            {
                int order = index;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DebugReverseSoloSpawnOrder) order = bySeat.Length - index - 1;
#endif
                var participant = bySeat[order];
                if (spawnedParticipants.TryGetValue(participant.ParticipantId, out var existing) && existing != null && existing.IsSpawned)
                    continue;
                GameObject instance = null;
                try
                {
                    if (!CanAct(_subscribedManager, _bindingVersion))
                    { error = "Solo session stopped during participant creation."; return false; }
                    instance = Instantiate(playerPrefab, GetSpawnPositionBySeat(participant.Seat), Quaternion.identity);
                    SceneManager.MoveGameObjectToScene(instance, _preparedScene);
                    var state = instance.GetComponent<PlayerState>();
                    var networkObject = instance.GetComponent<NetworkObject>();
                    state.ConfigureParticipant(participant);
                    if (participant.ControllerKind == PlayerControllerKind.Human)
                        networkObject.SpawnAsPlayerObject(participant.ClientId.Value, destroyWithScene: true);
                    else networkObject.Spawn(destroyWithScene: true);
                    spawnedParticipants[participant.ParticipantId] = networkObject;
                    if (participant.ClientId.HasValue) spawnedPlayers[participant.ClientId.Value] = networkObject;
                    if (mcr.InitializationFailure != null)
                    { error = mcr.InitializationFailure; return false; }
                }
                catch (Exception exception)
                {
                    // Spawned objects remain owned by this session for the caller's NGO rollback.
                    if (instance != null && !instance.GetComponent<NetworkObject>().IsSpawned) Destroy(instance);
                    error = "Solo participant creation failed: " + exception.Message;
                    return false;
                }
            }
            return true;
        }

        static bool ContainsDescriptor(IReadOnlyList<MatchParticipantDescriptor> participants, MatchParticipantDescriptor expected)
        {
            foreach (var descriptor in participants) if (expected.Equals(descriptor)) return true;
            return false;
        }

        public void OnPlayerDisconnected(ulong clientId)
        {
            if (!CanAct(_subscribedManager, _bindingVersion)) return;
            // Multi's dispatcher calls its roster first, preserving the ghost snapshot before despawn.
            pendingSpawnClients.Remove(clientId);
            if (!spawnedPlayers.TryGetValue(clientId, out var obj)) return;
            spawnedPlayers.Remove(clientId);
            if (obj != null && obj.IsSpawned) obj.Despawn();
        }

        public Vector3 GetSpawnPositionBySeat(byte seatIndex)
        {
            if (seatIndex < resolvedSpawnPoints.Count && resolvedSpawnPoints[seatIndex] != null)
                return resolvedSpawnPoints[seatIndex].position;
            return GetFallbackSpawn(seatIndex);
        }

        Vector3 GetSpawnPositionLegacy(ulong clientId)
        {
            if (resolvedSpawnPoints.Count > 0)
            {
                var point = resolvedSpawnPoints[(int)(clientId % (ulong)resolvedSpawnPoints.Count)];
                if (point != null) return point.position;
            }
            return GetFallbackSpawn(clientId);
        }

        public void RefreshResolvedSpawnPoints()
        {
            CloseSceneGate();
            var mcr = MatchCompositionRoot.Instance;
            if (mcr != null) TryPrepareMatchScene(mcr.Generation, out _);
        }

        void ResolveSpawnPoints(Scene scene)
        {
            resolvedSpawnPoints.Clear();
            _mapLayoutError = null;
            if (MapCharacterLayout.TryFind(scene, out var layout))
            {
                var mode = MatchCompositionRoot.Instance?.ActiveConfig?.Mode ?? GameMode.OneVsOne;
                if (layout.TryGetSpawnAnchors(MapCharacterLayout.ModeFor(mode), out var anchors, out _mapLayoutError))
                    resolvedSpawnPoints.AddRange(anchors);
                return;
            }
            var unique = new HashSet<Transform>();
            if (spawnPoints != null)
                foreach (var point in spawnPoints)
                    if (point != null && point.gameObject.scene == scene && unique.Add(point)) resolvedSpawnPoints.Add(point);
            if (!useSceneSpawnPointMarkers) return;
            var markers = new List<PlayerSpawnPoint3D>();
            foreach (var root in scene.GetRootGameObjects())
                markers.AddRange(root.GetComponentsInChildren<PlayerSpawnPoint3D>(includeInactiveSceneSpawnMarkers));
            markers.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (var marker in markers)
                if (marker != null && (includeInactiveSceneSpawnMarkers || marker.gameObject.activeInHierarchy)
                    && unique.Add(marker.transform)) resolvedSpawnPoints.Add(marker.transform);
        }

        Vector3 GetFallbackSpawn(ulong clientId)
        {
            float angle = (clientId + 1) * 137.5f * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * defaultSpawnRadius, topDownFallbackY, Mathf.Sin(angle) * defaultSpawnRadius);
        }

        public NetworkObject GetPlayerForClient(ulong clientId)
            => spawnedPlayers.TryGetValue(clientId, out var player) ? player : null;
        public IReadOnlyDictionary<ulong, NetworkObject> GetAllPlayers() => spawnedPlayers;
        public Vector3 GetRespawnPosition(ulong clientId)
        {
            var roster = MatchCompositionRoot.Instance?.Roster;
            return roster != null && roster.TryGetSeatByClientId(clientId, out byte seat)
                ? GetSpawnPositionBySeat(seat) : GetSpawnPositionLegacy(clientId);
        }
    }
}
