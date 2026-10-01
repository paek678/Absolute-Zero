using System;
using AbsoluteZero.Core.Session;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Network
{
    public class DisconnectDispatcher : MonoBehaviour
    {
        public static DisconnectDispatcher Instance { get; private set; }

        IDisconnectHandler _currentHandler;
        IDisconnectHandler _defaultHandler;
        NetworkManager _subscribedNm;
        NetworkSceneManager _subscribedSceneManager;
        MatchSessionRouter _router;
        MatchSessionLease _lease;
        Action<ulong> _disconnect;
        Action _started;
        Action _routerChanged;
        long _subscriptionVersion;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start() => RefreshSubscription();
        void Update() => RefreshSubscription();

        void RefreshSubscription()
        {
            var nm = NetworkManager.Singleton;
            var sceneManager = nm != null ? nm.SceneManager : null;
            var router = AppBootstrapper.Instance?.SessionRouter;
            var lease = router?.Current;
            if (ReferenceEquals(nm, _subscribedNm) && ReferenceEquals(sceneManager, _subscribedSceneManager)
                && ReferenceEquals(router, _router) && ReferenceEquals(lease, _lease)) return;

            bool adoption = ReferenceEquals(nm, _subscribedNm) && nm != null && nm.IsListening
                && ReferenceEquals(sceneManager, _subscribedSceneManager) && _lease == null
                && lease != null && lease.Mode != GameMode.Solo;
            Unsubscribe();
            // SetHandler refreshes the binding before installing a new match roster.
            if (!adoption) _currentHandler = _defaultHandler;
            _subscribedNm = nm;
            _subscribedSceneManager = sceneManager;
            _router = router;
            _lease = lease;
            long version = ++_subscriptionVersion;
            if (router != null)
            {
                _routerChanged = () =>
                {
                    if (this != null && ReferenceEquals(router, _router)
                        && ReferenceEquals(router, AppBootstrapper.Instance?.SessionRouter)) RefreshSubscription();
                };
                router.Changed += _routerChanged;
            }
            if (nm == null) return;
            _disconnect = clientId =>
            {
                if (!IsCurrentPublisher(nm, sceneManager, router, lease, version)) return;
                // Multi snapshots state before calling the default spawn manager.
                // Do not dispatch independently to both handlers.
                _currentHandler?.OnPlayerDisconnected(clientId);
            };
            _started = () =>
            {
                if (ReferenceEquals(nm, _subscribedNm) && version == _subscriptionVersion) RefreshSubscription();
            };
            nm.OnClientDisconnectCallback += _disconnect;
            nm.OnServerStarted += _started;
        }

        bool IsCurrentPublisher(NetworkManager nm, NetworkSceneManager scenes,
            MatchSessionRouter router, MatchSessionLease lease, long version)
            => this != null && version == _subscriptionVersion && nm != null
                && ReferenceEquals(nm, _subscribedNm) && ReferenceEquals(nm, NetworkManager.Singleton)
                && ReferenceEquals(router, AppBootstrapper.Instance?.SessionRouter)
                && ReferenceEquals(scenes, nm.SceneManager) && ReferenceEquals(lease, router?.Current)
                && (lease == null || router.IsCurrent(lease)) && nm.IsServer && nm.IsListening && !nm.ShutdownInProgress;

        void Unsubscribe()
        {
            if (_router != null) _router.Changed -= _routerChanged;
            _routerChanged = null;
            if (!ReferenceEquals(_subscribedNm, null))
            {
                _subscribedNm.OnClientDisconnectCallback -= _disconnect;
                _subscribedNm.OnServerStarted -= _started;
            }
            _disconnect = null;
            _started = null;
            _subscribedNm = null;
            _subscribedSceneManager = null;
        }

        public void SetDefaultHandler(IDisconnectHandler handler)
        {
            RefreshSubscription();
            var previous = _defaultHandler;
            _defaultHandler = handler;
            if (_currentHandler == null || ReferenceEquals(_currentHandler, previous)) _currentHandler = handler;
        }

        public void ClearDefaultIfCurrent(IDisconnectHandler handler)
        {
            if (!ReferenceEquals(_defaultHandler, handler)) return;
            _defaultHandler = null;
            if (ReferenceEquals(_currentHandler, handler)) _currentHandler = null;
        }

        public void SetHandler(IDisconnectHandler handler)
        {
            RefreshSubscription();
            _currentHandler = handler;
        }

        public void RestoreDefaultIfCurrent(IDisconnectHandler handler)
        {
            if (ReferenceEquals(_currentHandler, handler)) _currentHandler = _defaultHandler;
        }

        void OnDestroy()
        {
            ++_subscriptionVersion;
            Unsubscribe();
            _currentHandler = _defaultHandler = null;
            if (Instance == this) Instance = null;
        }
    }
}
