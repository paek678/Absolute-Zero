using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace AbsoluteZero.Core.Session
{
    // A temporary transport preserves the online transport's private Relay/CLI state
    // without reflection. Only one transport is attached to the active NGO session.
    public sealed class NgoLocalHostRuntime
    {
        public NetworkManager Manager { get; }
        public UnityTransport ActiveTransport { get; private set; }
        public bool HasSnapshot => _config != null;
        NetworkConfig _config;
        NetworkTransport _originalTransport;
        GameObject _originalPlayerPrefab;
        bool _originalApproval;
        byte[] _originalConnectionData;
        Action<NetworkManager.ConnectionApprovalRequest, NetworkManager.ConnectionApprovalResponse> _originalCallback;
        Task _stop;

        public NgoLocalHostRuntime(NetworkManager manager) { Manager = manager; }

        public Result<Unit> StartHost(ushort port)
        {
            if (Manager == null || port == 0 || HasSnapshot || Manager.IsListening || Manager.ShutdownInProgress)
                return Result<Unit>.Failure(OperationErrorCode.InvalidState, "Local host requires an idle NetworkManager and a nonzero port.");
            try
            {
                _config = Manager.NetworkConfig;
                _originalTransport = _config.NetworkTransport;
                _originalPlayerPrefab = _config.PlayerPrefab;
                _originalApproval = _config.ConnectionApproval;
                _originalConnectionData = _config.ConnectionData;
                _originalCallback = Manager.ConnectionApprovalCallback;
                var owner = new GameObject("SoloLoopbackTransport");
                owner.transform.SetParent(Manager.transform, false);
                ActiveTransport = owner.AddComponent<UnityTransport>();
                ActiveTransport.SetConnectionData(true, "127.0.0.1", port, "127.0.0.1");
                _config.NetworkTransport = ActiveTransport;
                _config.PlayerPrefab = null;
                _config.ConnectionApproval = true;
                _config.ConnectionData = Array.Empty<byte>();
                Manager.ConnectionApprovalCallback = null;
                Manager.ConnectionApprovalCallback = ApproveLocalHost;
                return Manager.StartHost()
                    ? Result<Unit>.Success(Unit.Value)
                    : Result<Unit>.Failure(OperationErrorCode.NetworkStartFailed, "Local StartHost failed (the port may be occupied).");
            }
            catch (Exception error)
            {
                return Result<Unit>.Failure(OperationErrorCode.NetworkStartFailed, error.Message);
            }
        }

        static void ApproveLocalHost(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = request.ClientNetworkId == NetworkManager.ServerClientId;
            response.CreatePlayerObject = false;
            response.Pending = false;
            if (!response.Approved) response.Reason = "This local Solo session does not accept remote clients.";
        }

        public Task StopAsync()
        {
            if (_stop != null && !_stop.IsCompleted) return _stop;
            return _stop = StopAndRestoreAsync();
        }

        async Task StopAndRestoreAsync()
        {
            if (!HasSnapshot) return;
            if (Manager != null)
            {
                if (!ReferenceEquals(Manager.NetworkConfig, _config))
                    throw new InvalidOperationException("NetworkConfig changed during the local session; refusing to release its owner.");
                if (Manager.IsListening && !Manager.ShutdownInProgress) Manager.Shutdown();
                double deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (Manager != null && (Manager.IsListening || Manager.ShutdownInProgress))
                {
                    if (Time.realtimeSinceStartupAsDouble >= deadline)
                        throw new TimeoutException("Local host shutdown did not finish; ownership retained for cleanup retry.");
                    await Task.Delay(20);
                }
                if (Manager != null)
                {
                    Manager.ConnectionApprovalCallback = null;
                    Manager.ConnectionApprovalCallback = _originalCallback;
                    _config.NetworkTransport = _originalTransport;
                    _config.PlayerPrefab = _originalPlayerPrefab;
                    _config.ConnectionApproval = _originalApproval;
                    _config.ConnectionData = _originalConnectionData;
                }
            }
            if (ActiveTransport != null) UnityEngine.Object.Destroy(ActiveTransport.gameObject);
            ActiveTransport = null;
            _config = null;
            _originalCallback = null;
            _originalTransport = null;
            _originalPlayerPrefab = null;
            _originalConnectionData = null;
        }
    }
}
