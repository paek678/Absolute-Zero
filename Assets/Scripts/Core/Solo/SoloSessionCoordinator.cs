using System;
using System.Threading.Tasks;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Solo.Configuration;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbsoluteZero.Core.Solo
{
    public enum SoloSessionState { Idle, Starting, Running, Stopping, Failed }

    public sealed class SoloSessionCoordinator : IDisposable
    {
        readonly MatchSessionRouter _router;
        readonly NgoLocalHostRuntime _runtime;
        readonly ushort _port;
        readonly Func<Task> _returnToMenu;
        MatchSessionLease _lease;
        Action _transportFailure;
        Action<bool> _networkStopped;
        NetworkSceneManager _sceneManager;
        NetworkSceneManager.OnLoadDelegateHandler _sceneLoading;
        AsyncOperation _sceneLoad;
        bool _disposed;
        ResolvedSoloSettings _lastSettings;

        public SoloSessionState State { get; private set; }
        public string LastError { get; private set; }
        public MatchSessionLease Lease => _lease;
        public event Action<SoloSessionState> StateChanged;

        public SoloSessionCoordinator(MatchSessionRouter router, NgoLocalHostRuntime runtime,
            ushort port = 18777, Func<Task> returnToMenu = null)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _port = port;
            _returnToMenu = returnToMenu ?? ReturnToLobbyAsync;
        }

        public Task<Result<Unit>> StartAsync(SoloDuelDefinitionSO encounter, SoloConfigurationContext context)
        {
            if (!SoloSettingsResolver.TryResolve(encounter, context, out var settings, out var errors))
                return Task.FromResult(Result<Unit>.Failure(OperationErrorCode.InvalidState, string.Join("; ", errors)));
            return StartResolvedAsync(settings);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Task<Result<Unit>> StartDevelopmentAsync(SoloDuelDefinitionSO encounter, SoloConfigurationContext context)
        {
            if (!SoloSettingsResolver.TryResolveForDevelopment(encounter, context, out var settings, out var errors))
                return Task.FromResult(Result<Unit>.Failure(OperationErrorCode.InvalidState, string.Join("; ", errors)));
            return StartResolvedAsync(settings);
        }
        public Task<Result<Unit>> StartValidationAsync(SoloDuelDefinitionSO encounter, SoloConfigurationContext context)
        {
            if (!SoloSettingsResolver.TryResolveForValidation(encounter, context, out var settings, out var errors))
                return Task.FromResult(Result<Unit>.Failure(OperationErrorCode.InvalidState, string.Join("; ", errors)));
            return StartResolvedAsync(settings);
        }
#endif

        async Task<Result<Unit>> StartResolvedAsync(ResolvedSoloSettings settings)
        {
            if (_disposed || !_router.TryBegin(GameMode.Solo, StopOwnedAsync, out var lease))
                return Result<Unit>.Failure(OperationErrorCode.InvalidState, "Another session is active or cleanup is incomplete.");
            _lease = lease;
            _lastSettings = settings;
            LastError = null;
            SetState(SoloSessionState.Starting);
            try
            {
                if (!_router.BindLaunchContext(lease, new MatchLaunchContext(lease.Generation, GameMode.Solo, 2, settings)))
                    throw new InvalidOperationException("Could not bind the Solo launch context.");
                Subscribe(lease);
                var start = _runtime.StartHost(_port);
                if (start.IsFailure) throw new InvalidOperationException(start.ErrorMessage);
                await WaitUntilAsync(() => _runtime.Manager != null && _runtime.Manager.IsConnectedClient, lease, 10, "local host readiness");
                var scene = SceneManager.GetActiveScene();
                if (scene.path != settings.GameplayScenePath)
                {
                    var manager = _runtime.Manager.SceneManager;
                    if (manager == null) throw new InvalidOperationException("NGO scene management is unavailable.");
                    _sceneManager = manager;
                    _sceneLoading = (client, name, mode, operation) =>
                    {
                        if (_router.Owns(lease) && client == _runtime.Manager.LocalClientId
                            && (name == settings.GameplayScenePath
                                || name == System.IO.Path.GetFileNameWithoutExtension(settings.GameplayScenePath)))
                            _sceneLoad = operation;
                    };
                    manager.OnLoad += _sceneLoading;
                    var load = manager.LoadScene(settings.GameplayScenePath, LoadSceneMode.Single);
                    if (load != SceneEventProgressStatus.Started)
                        throw new InvalidOperationException("Solo scene load failed: " + load);
                    await WaitUntilAsync(() => SceneManager.GetActiveScene().path == settings.GameplayScenePath
                        && SceneManager.GetActiveScene().isLoaded, lease, 30, "Solo scene load");
                }
                RequireCurrent(lease);
                SetState(SoloSessionState.Running);
                return Result<Unit>.Success(Unit.Value);
            }
            catch (OperationCanceledException)
            {
                return Result<Unit>.Failure(OperationErrorCode.Cancelled, "Solo start was superseded by shutdown.");
            }
            catch (Exception error)
            {
                if (!_router.IsCurrent(lease))
                    return Result<Unit>.Failure(OperationErrorCode.Cancelled, "Solo start was superseded by shutdown.");
                LastError = error.Message;
                try { await _router.StopAsync(); }
                catch (Exception cleanup) { LastError += "; cleanup: " + cleanup.Message; }
                if (ReferenceEquals(_lease, lease)) SetState(SoloSessionState.Failed);
                return Result<Unit>.Failure(OperationErrorCode.NetworkStartFailed, LastError);
            }
        }

        async Task WaitUntilAsync(Func<bool> condition, MatchSessionLease lease, double seconds, string operation)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition())
            {
                RequireCurrent(lease);
                if (_runtime.Manager == null || !_runtime.Manager.IsListening)
                    throw new InvalidOperationException("Local network stopped during " + operation);
                if (Time.realtimeSinceStartupAsDouble >= deadline) throw new TimeoutException("Timed out waiting for " + operation);
                await Task.Delay(20);
            }
            RequireCurrent(lease);
        }

        void RequireCurrent(MatchSessionLease lease)
        {
            if (!_router.IsCurrent(lease)) throw new OperationCanceledException();
            if (!string.IsNullOrEmpty(LastError)) throw new InvalidOperationException(LastError);
        }

        void Subscribe(MatchSessionLease lease)
        {
            var manager = _runtime.Manager;
            if (manager == null) throw new InvalidOperationException("NetworkManager is missing.");
            _transportFailure = () => OnFailure(lease, "Local transport failed.");
            _networkStopped = _ => OnFailure(lease, "Local network stopped unexpectedly.");
            manager.OnTransportFailure += _transportFailure;
            manager.OnClientStopped += _networkStopped;
        }

        void Unsubscribe()
        {
            if (_runtime.Manager != null)
            {
                _runtime.Manager.OnTransportFailure -= _transportFailure;
                _runtime.Manager.OnClientStopped -= _networkStopped;
            }
            _transportFailure = null; _networkStopped = null;
            if (_sceneManager != null) _sceneManager.OnLoad -= _sceneLoading;
            _sceneManager = null; _sceneLoading = null;
        }

        void OnFailure(MatchSessionLease lease, string message)
        {
            if (!_router.IsCurrent(lease)) return;
            LastError = message;
            // During start, the start operation owns rollback and its returned error.
            if (State == SoloSessionState.Running) ObserveStop();
        }

        async void ObserveStop()
        {
            try { await StopAsync(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        public Task StopAsync() => _router.Owns(_lease) ? _router.StopAsync() : Task.CompletedTask;

        async Task StopOwnedAsync()
        {
            var lease = _lease;
            if (!_router.Owns(lease)) return;
            SetState(SoloSessionState.Stopping);
            // Native Unity scene loads cannot be cancelled by NGO Shutdown. Retain the
            // invalidated lease until that load settles, then unload through menu return.
            double deadline = Time.realtimeSinceStartupAsDouble + 35;
            while (_sceneLoad != null && !_sceneLoad.isDone)
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                    throw new TimeoutException("Scene load still pending; cleanup ownership retained.");
                await Task.Delay(20);
            }
            Unsubscribe();
            await _runtime.StopAsync();
            _sceneLoad = null;
            if (!_router.Owns(lease)) return;
            await _returnToMenu();
            if (_router.Owns(lease)) SetState(SoloSessionState.Idle);
        }

        public async Task<Result<Unit>> RestartAsync()
        {
            var settings = _lastSettings;
            if (settings == null) return Result<Unit>.Failure(OperationErrorCode.InvalidState, "No previous Solo settings to restart.");
            await StopAsync();
            return await StartResolvedAsync(settings);
        }

        static async Task ReturnToLobbyAsync()
        {
            if (SceneManager.GetActiveScene().name == "LobbyScene") return;
            var operation = SceneManager.LoadSceneAsync("LobbyScene", LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("Lobby scene could not load.");
            while (!operation.isDone) await Task.Delay(20);
        }

        void SetState(SoloSessionState state) { State = state; StateChanged?.Invoke(state); }
        public void Dispose() { if (_disposed) return; _disposed = true; ObserveStop(); }
    }
}
