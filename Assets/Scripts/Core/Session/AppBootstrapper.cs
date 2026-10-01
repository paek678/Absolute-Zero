using System;
using AbsoluteZero.Core.Solo;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Session
{
    public class AppBootstrapper : MonoBehaviour
    {
        public static AppBootstrapper Instance { get; private set; }

        public bool IsReady { get; private set; }
        public event Action OnReady;
        public MatchSessionRouter SessionRouter { get; } = new();
        public SoloSessionCoordinator SoloSession { get; private set; }

        void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(this);
                return;
            }
        }

        void Start()
        {
            try
            {
                InitializeLocalServices();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AppBootstrapper] Fatal init error: {e}");
            }
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            SoloSession?.Dispose();
            Instance = null;
        }

        void InitializeLocalServices()
        {
            var coordinator = NetworkSessionCoordinator.Instance;
            var network = NetworkManager.Singleton;
            if (coordinator == null || network == null)
            {
                Debug.LogError("[AppBootstrapper] Local network components are missing");
                return;
            }

            coordinator.Router = SessionRouter;
            SoloSession = new SoloSessionCoordinator(SessionRouter, new NgoLocalHostRuntime(network));
            IsReady = true;
            Debug.Log("[AppBootstrapper] Local services ready; online services initialize on demand");
            OnReady?.Invoke();
        }
    }
}
