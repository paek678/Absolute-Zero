using System;
using AbsoluteZero.Core.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Network
{
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        [SerializeField] private string gameSceneName = "GameScene";
        [SerializeField] private string titleSceneName = "LobbyScene";

        public event Action OnGameStarted;

        public static event Action OnLoadingShow;
        public static event Action OnLoadingHide;

        private bool networkCallbacksRegistered;
        private bool sceneLoadCallbackRegistered;
        private bool isDisconnecting;
        NetworkManager _subscribedManager;
        NetworkSceneManager _subscribedSceneManager;
        long _subscribedGeneration;
        Action<ulong> _connected, _disconnected;
        Action<bool> _stopped;
        Action _failed;
        static MatchSessionRouter Router => AppBootstrapper.Instance?.SessionRouter;

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

        private void OnEnable() => TryRegisterNetworkCallbacks();
        private void Start() => TryRegisterNetworkCallbacks();

        private void Update()
        {
            TryRegisterNetworkCallbacks();
        }

        private void OnDisable()
        {
            UnregisterNetworkCallbacks();
            UnregisterSceneLoadCallback();
        }

        private void OnDestroy()
        {
            UnregisterNetworkCallbacks();
            UnregisterSceneLoadCallback();
            if (Instance == this) Instance = null;
        }

        #region Network Callbacks

        private void TryRegisterNetworkCallbacks()
        {
            var nm = NetworkManager.Singleton;
            long generation = Router?.Current?.Generation ?? 0;
            if (networkCallbacksRegistered && (_subscribedManager != nm || _subscribedGeneration != generation))
            {
                UnregisterNetworkCallbacks();
                UnregisterSceneLoadCallback();
            }
            if (networkCallbacksRegistered) return;
            if (nm == null) return;
            _subscribedManager = nm; _subscribedGeneration = generation;
            bool Current() => nm == NetworkManager.Singleton && generation == (Router?.Current?.Generation ?? 0);
            _connected = id => { if (Current()) OnClientConnected(id); };
            _disconnected = id => { if (Current()) OnClientDisconnected(id); };
            _stopped = host => { if (Current()) OnClientStopped(host); };
            _failed = () => { if (Current()) OnTransportFailure(); };
            nm.OnClientConnectedCallback += _connected;
            nm.OnClientDisconnectCallback += _disconnected;
            nm.OnClientStopped += _stopped;
            nm.OnTransportFailure += _failed;
            networkCallbacksRegistered = true;
        }

        private void UnregisterNetworkCallbacks()
        {
            if (!networkCallbacksRegistered) return;

            var nm = _subscribedManager;
            if (nm != null)
            {
                nm.OnClientConnectedCallback -= _connected;
                nm.OnClientDisconnectCallback -= _disconnected;
                nm.OnClientStopped -= _stopped;
                nm.OnTransportFailure -= _failed;
            }
            _subscribedManager = null;
            _connected = null; _disconnected = null; _stopped = null; _failed = null;
            networkCallbacksRegistered = false;
        }

        #endregion

        #region Scene Load

        private void RegisterSceneLoadCallback()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null) return;
            UnregisterSceneLoadCallback();
            _subscribedSceneManager = nm.SceneManager;
            _subscribedSceneManager.OnLoadComplete += OnSceneLoadComplete;
            sceneLoadCallbackRegistered = true;
        }

        private void UnregisterSceneLoadCallback()
        {
            if (!sceneLoadCallbackRegistered) return;

            if (_subscribedSceneManager != null)
                _subscribedSceneManager.OnLoadComplete -= OnSceneLoadComplete;
            _subscribedSceneManager = null;
            sceneLoadCallbackRegistered = false;
        }

        private void OnSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            if (clientId == nm.LocalClientId)
            {
                Debug.Log($"[SessionManager] Local scene load complete - ClientId: {clientId}, Scene: {sceneName}");
                OnGameStarted?.Invoke();
                UnregisterSceneLoadCallback();
            }
        }

        #endregion

        #region Game Start

        public void StartGame()
        {
            if (Router?.IsSolo == true) return;
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[SessionManager] NetworkManager missing. Cannot start game.");
                return;
            }

            if (!nm.IsHost)
            {
                Debug.LogWarning("[SessionManager] Only host can start the game.");
                return;
            }

            if (nm.SceneManager == null)
            {
                Debug.LogError("[SessionManager] SceneManager missing. Cannot load game scene.");
                return;
            }

            Debug.Log("[SessionManager] Starting game scene transition");
            LobbyManager.Instance?.SetGameSessionActive(true);
            OnLoadingShow?.Invoke();

            RegisterSceneLoadCallback();
            var status = nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[SessionManager] Scene load failed: {status}");
                UnregisterSceneLoadCallback();
                LobbyManager.Instance?.SetGameSessionActive(false);
                OnLoadingHide?.Invoke();
            }
        }

        #endregion

        #region Disconnect

        public void Disconnect()
        {
            if (Router?.Current != null)
            {
                StopOwnedSession(Router);
                return;
            }
            if (isDisconnecting) return;
            isDisconnecting = true;

            try
            {
                OnLoadingHide?.Invoke();
                UnregisterSceneLoadCallback();

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                    Debug.Log("[SessionManager] NetworkManager shutdown complete");
                }

                if (RelayManager.Instance != null)
                    RelayManager.Instance.ClearState();

                if (LobbyManager.Instance != null)
                    LobbyManager.Instance.ForceCleanup();

                SceneManager.LoadScene(titleSceneName);
                Debug.Log("[SessionManager] Returned to title scene");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SessionManager] Disconnect cleanup error: {e.Message}");

                if (RelayManager.Instance != null)
                    RelayManager.Instance.ClearState();

                try { SceneManager.LoadScene(titleSceneName); } catch { }
            }
            finally
            {
                isDisconnecting = false;
            }
        }

        #endregion

        #region Callback Handlers

        static async void StopOwnedSession(MatchSessionRouter router)
        {
            try { await router.StopAsync(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[SessionManager] Client connected - ClientId: {clientId}");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (Router?.IsSolo == true) return; // The Solo coordinator owns local failure/exit.
            var nm = NetworkManager.Singleton;

            if (nm == null)
            {
                Debug.Log("[SessionManager] NetworkManager gone during disconnect. Returning to lobby.");
                Disconnect();
                return;
            }

            if (!(RelayManager.Instance?.IsRelayConnected ?? false)) return;

            if (!nm.IsServer && clientId == nm.LocalClientId)
            {
                Debug.Log("[SessionManager] Server connection lost. Returning to lobby.");
                Disconnect();
            }
        }

        private void OnClientStopped(bool isHost)
        {
            if (Router?.IsSolo == true) return;
            if (!(RelayManager.Instance?.IsRelayConnected ?? false)) return;

            Debug.Log($"[SessionManager] OnClientStopped (IsHost: {isHost}). Returning to lobby.");
            Disconnect();
        }

        private void OnTransportFailure()
        {
            if (Router?.IsSolo == true) return;
            Debug.LogWarning("[SessionManager] Transport failure. Returning to lobby.");
            Disconnect();
        }

        #endregion
    }
}
