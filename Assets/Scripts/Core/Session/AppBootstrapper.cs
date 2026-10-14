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
            ushort soloPort = 18777;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            soloPort = ResolveDevelopmentSoloPort(Environment.GetCommandLineArgs());
#endif
            SoloSession = new SoloSessionCoordinator(SessionRouter, new NgoLocalHostRuntime(network), soloPort);
            IsReady = true;
            Debug.Log("[AppBootstrapper] Local services ready; online services initialize on demand");
            OnReady?.Invoke();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Process-local transport isolation for explicit development runs. The
        // shipping constructor keeps its established loopback port unchanged.
        internal static ushort ResolveDevelopmentSoloPort(string[] arguments)
        {
            const string option = "--az-solo-local-port";
            int index = Array.IndexOf(arguments, option);
            if (index < 0) return 18777;
            if (Array.LastIndexOf(arguments, option) != index || index + 1 >= arguments.Length
                || !ushort.TryParse(arguments[index + 1], out ushort port) || port < 1024)
                throw new ArgumentException("Development Solo loopback port must appear once with a value from 1024 to 65535.");
            return port;
        }
#endif
    }
}
