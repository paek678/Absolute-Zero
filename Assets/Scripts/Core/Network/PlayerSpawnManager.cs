using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Network
{
    public class PlayerSpawnManager : MonoBehaviour
    {
        public static PlayerSpawnManager Instance { get; private set; }

        [Header("=== Player Prefab ===")]
        [SerializeField] private GameObject playerPrefab;

        [Header("=== Spawn Points ===")]
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private bool useSceneSpawnPointMarkers = true;
        [SerializeField] private bool includeInactiveSceneSpawnMarkers = false;
        [SerializeField] private float defaultSpawnRadius = 5f;
        [SerializeField] private float topDownFallbackY = 1f;

        [Header("=== Spawn Timing ===")]
        [SerializeField] private bool spawnOnlyInGameScene = true;
        [SerializeField] private string gameSceneName = "GameScene";

        private readonly Dictionary<ulong, NetworkObject> spawnedPlayers = new Dictionary<ulong, NetworkObject>();
        private readonly HashSet<ulong> pendingSpawnClients = new HashSet<ulong>();
        private readonly List<Transform> resolvedSpawnPoints = new List<Transform>();
        private bool networkCallbacksSubscribed;
        private bool sceneCallbacksSubscribed;

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

        private void Start()
        {
            RefreshResolvedSpawnPoints();
            TryRegisterCallbacks();
            QueueExistingClients();
            TrySpawnPendingClients();
        }

        private void Update()
        {
            if (!networkCallbacksSubscribed || !sceneCallbacksSubscribed)
                TryRegisterCallbacks();

            if (pendingSpawnClients.Count > 0)
                TrySpawnPendingClients();
        }

        private void TryRegisterCallbacks()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null) return;

            if (!networkCallbacksSubscribed)
            {
                networkManager.OnClientConnectedCallback += OnClientConnectedCallback;
                networkManager.OnClientDisconnectCallback += OnClientDisconnectCallback;
                networkCallbacksSubscribed = true;
            }

            if (!sceneCallbacksSubscribed && networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnLoadComplete += OnSceneLoadComplete;
                sceneCallbacksSubscribed = true;
            }
        }

        private void OnDestroy()
        {
            UnregisterCallbacks();
            if (Instance == this) Instance = null;
        }

        private void UnregisterCallbacks()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                networkCallbacksSubscribed = false;
                sceneCallbacksSubscribed = false;
                return;
            }

            if (networkCallbacksSubscribed)
            {
                networkManager.OnClientConnectedCallback -= OnClientConnectedCallback;
                networkManager.OnClientDisconnectCallback -= OnClientDisconnectCallback;
                networkCallbacksSubscribed = false;
            }

            if (sceneCallbacksSubscribed && networkManager.SceneManager != null)
            {
                networkManager.SceneManager.OnLoadComplete -= OnSceneLoadComplete;
                sceneCallbacksSubscribed = false;
            }
        }

        private bool ShouldSpawnInCurrentScene()
        {
            if (!spawnOnlyInGameScene) return true;
            return SceneManager.GetActiveScene().name == gameSceneName;
        }

        private void QueueExistingClients()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer) return;

            foreach (var client in networkManager.ConnectedClientsList)
            {
                if (!spawnedPlayers.ContainsKey(client.ClientId))
                    pendingSpawnClients.Add(client.ClientId);
            }
        }

        private void TrySpawnPendingClients()
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer) return;
            if (!ShouldSpawnInCurrentScene()) return;

            QueueExistingClients();
            if (pendingSpawnClients.Count == 0) return;

            List<ulong> spawnQueue = new List<ulong>(pendingSpawnClients);
            foreach (ulong clientId in spawnQueue)
            {
                if (!networkManager.ConnectedClients.ContainsKey(clientId))
                {
                    pendingSpawnClients.Remove(clientId);
                    continue;
                }

                if (spawnedPlayers.ContainsKey(clientId))
                {
                    pendingSpawnClients.Remove(clientId);
                    continue;
                }

                SpawnPlayerForClient(clientId);
                pendingSpawnClients.Remove(clientId);
            }
        }

        private void OnClientConnectedCallback(ulong clientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            pendingSpawnClients.Add(clientId);
            Debug.Log($"[PlayerSpawnManager] Client {clientId} connected, queued for spawn.");
            TrySpawnPendingClients();
        }

        private void OnClientDisconnectCallback(ulong clientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            pendingSpawnClients.Remove(clientId);
            Debug.Log($"[PlayerSpawnManager] Client {clientId} disconnected, despawning player...");
            DespawnPlayerForClient(clientId);
        }

        private void OnSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            var networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsServer) return;
            if (spawnOnlyInGameScene && sceneName != gameSceneName) return;
            if (clientId != networkManager.LocalClientId) return;

            RefreshResolvedSpawnPoints();
            TrySpawnPendingClients();
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawnManager] Player prefab is not assigned.");
                return;
            }

            if (spawnedPlayers.ContainsKey(clientId))
            {
                Debug.LogWarning($"[PlayerSpawnManager] Player already spawned for client {clientId}");
                return;
            }

            Vector3 spawnPosition = GetSpawnPosition(clientId);
            Quaternion spawnRotation = Quaternion.identity;

            GameObject playerInstance = Instantiate(playerPrefab, spawnPosition, spawnRotation);

            NetworkObject networkObject = playerInstance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("[PlayerSpawnManager] Player prefab is missing NetworkObject.");
                Destroy(playerInstance);
                return;
            }

            networkObject.SpawnAsPlayerObject(clientId);
            spawnedPlayers[clientId] = networkObject;

            Debug.Log($"[PlayerSpawnManager] Player spawned for client {clientId} at {spawnPosition}");
        }

        private void DespawnPlayerForClient(ulong clientId)
        {
            if (spawnedPlayers.TryGetValue(clientId, out NetworkObject networkObject))
            {
                if (networkObject != null && networkObject.IsSpawned)
                {
                    networkObject.Despawn();
                    Destroy(networkObject.gameObject);
                }

                spawnedPlayers.Remove(clientId);
                Debug.Log($"[PlayerSpawnManager] Player despawned for client {clientId}");
            }
        }

        private Vector3 GetSpawnPosition(ulong clientId)
        {
            if (resolvedSpawnPoints.Count > 0)
            {
                int index = (int)(clientId % (ulong)resolvedSpawnPoints.Count);
                return resolvedSpawnPoints[index].position;
            }

            return GetFallbackSpawn(clientId);
        }

        public void RefreshResolvedSpawnPoints()
        {
            resolvedSpawnPoints.Clear();
            HashSet<Transform> unique = new HashSet<Transform>();

            if (spawnPoints != null)
            {
                foreach (Transform point in spawnPoints)
                {
                    if (point == null) continue;
                    if (unique.Add(point))
                        resolvedSpawnPoints.Add(point);
                }
            }

            if (useSceneSpawnPointMarkers)
            {
                PlayerSpawnPoint3D[] markers = FindObjectsByType<PlayerSpawnPoint3D>(
                    includeInactiveSceneSpawnMarkers ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None
                );
                if (markers != null && markers.Length > 0)
                {
                    System.Array.Sort(markers, (a, b) => a.Order.CompareTo(b.Order));
                    foreach (PlayerSpawnPoint3D marker in markers)
                    {
                        if (marker == null) continue;
                        Transform markerTransform = marker.transform;
                        if (unique.Add(markerTransform))
                            resolvedSpawnPoints.Add(markerTransform);
                    }
                }
            }

            Debug.Log($"[PlayerSpawnManager] Resolved spawn points: {resolvedSpawnPoints.Count}");
        }

        private Vector3 GetFallbackSpawn(ulong clientId)
        {
            Vector3 center = new Vector3(0f, topDownFallbackY, 0f);
            float angleDeg = (clientId + 1) * 137.5f;
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(
                Mathf.Cos(angleRad) * defaultSpawnRadius,
                0f,
                Mathf.Sin(angleRad) * defaultSpawnRadius
            );
            return center + offset;
        }

        public NetworkObject GetPlayerForClient(ulong clientId)
        {
            spawnedPlayers.TryGetValue(clientId, out NetworkObject player);
            return player;
        }

        public IReadOnlyDictionary<ulong, NetworkObject> GetAllPlayers()
        {
            return spawnedPlayers;
        }

        public Vector3 GetRespawnPosition(ulong clientId)
        {
            return GetSpawnPosition(clientId);
        }
    }
}
