using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Object = UnityEngine.Object;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Session;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Networking.Transport.Relay;

namespace AbsoluteZero.Tests
{
    public class Plan034RecoveryTests
    {
        GameObject _go;
        NetworkSessionCoordinator _coordinator;
        Runtime _runtime;
        Scene _scene;
        static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string key, object value) => typeof(NetworkSessionCoordinator).GetField(key, Flags).SetValue(_coordinator, value);
        void Stopped() => typeof(NetworkSessionCoordinator).GetMethod("OnNetworkStopped", Flags).Invoke(_coordinator, new object[] { false });

        [SetUp] public void Setup()
        {
            _go = new GameObject("RecoveryTest");
            _coordinator = _go.AddComponent<NetworkSessionCoordinator>();
            _coordinator.Router = new MatchSessionRouter();
            _runtime = new Runtime(); _scene = new Scene();
            Set("_networkRuntime", _runtime); Set("_sceneTransition", _scene);
            Set("_services", new Services()); Set("_state", SessionState.InGame);
            typeof(NetworkSessionCoordinator).GetMethod("TryAcquireOnlineLease", Flags).Invoke(_coordinator, null);
        }
        [TearDown] public void Teardown() { Object.DestroyImmediate(_go); }

        [UnityTest] public IEnumerator DuplicateStopAndLeave_RunOneShutdown()
        {
            Stopped(); Stopped();
            var leave = _coordinator.LeaveAsync();
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_coordinator.State == SessionState.Disconnecting && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, _runtime.ShutdownCount);
            Assert.AreEqual(SessionState.Ready, _coordinator.State);
            Assert.LessOrEqual(_scene.Loads, 1);
        }
        [UnityTest] public IEnumerator EntryOperation_OwnsLoadingFailure()
        {
            Set("_state", SessionState.LoadingGame); Stopped();
            yield return null;
            Assert.AreEqual(0, _runtime.ShutdownCount);
            Assert.AreEqual(SessionState.LoadingGame, _coordinator.State);
        }
        [UnityTest] public IEnumerator SupersededLeave_CannotShutdownNewOperation()
        {
            var leave = _coordinator.LeaveAsync();
            Set("_operationGeneration", (uint)99); Set("_state", SessionState.InGame);
            while (!leave.IsCompleted) yield return null;
            Assert.AreEqual(0, _runtime.ShutdownCount);
            Assert.AreEqual(0, _scene.Loads);
            Assert.AreEqual(SessionState.InGame, _coordinator.State);
        }
        [UnityTest] public IEnumerator ReadyWithoutSession_StopDoesNotNavigate()
        {
            Set("_state", SessionState.Ready); Stopped(); yield return null;
            Assert.AreEqual(0, _runtime.ShutdownCount); Assert.AreEqual(0, _scene.Loads);
        }
        [UnityTest] public IEnumerator CleanupException_StillFinishesLocalRecovery()
        {
            var gateway = new LobbyFake();
            gateway.Cleanup.SetException(new InvalidOperationException("injected cleanup failure"));
            Set("_lobbyGateway", gateway); Set("_currentLobby", new Lobby(id: "old-session"));
            var leave = _coordinator.LeaveAsync();
            while (!leave.IsCompleted) yield return null;
            Assert.IsFalse(leave.IsFaulted);
            Assert.AreEqual(SessionState.Ready, _coordinator.State);
            Assert.AreEqual(1, _runtime.ShutdownCount);
            Assert.AreEqual("old-session", gateway.CleanupId);
        }
        [UnityTest] public IEnumerator CleanupTimeout_LateCompletionCannotClearNewSession()
        {
            var gateway = new LobbyFake();
            Set("_lobbyGateway", gateway); Set("_currentLobby", new Lobby(id: "old-session"));
            var leave = _coordinator.LeaveAsync();
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!leave.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(leave.IsCompleted, "Remote cleanup must be bounded");
            Assert.AreEqual(SessionState.Ready, _coordinator.State);
            var next = new Lobby(id: "new-session");
            Set("_operationGeneration", (uint)99); Set("_state", SessionState.InGame); Set("_currentLobby", next);
            gateway.Cleanup.SetResult(Result<Unit>.Success(Unit.Value));
            yield return null; yield return null;
            Assert.AreEqual(SessionState.InGame, _coordinator.State);
            Assert.AreSame(next, typeof(NetworkSessionCoordinator).GetField("_currentLobby", Flags).GetValue(_coordinator));
            Assert.AreEqual(1, _runtime.ShutdownCount);
            Assert.AreEqual("old-session", gateway.CleanupId);
        }
        [UnityTest] public IEnumerator SupersededEntryCompensation_PreservesNewLobby()
        {
            var gateway = new LobbyFake(); Set("_lobbyGateway", gateway);
            uint generation = 1;
            var scope = new OperationScope(() => generation);
            var method = typeof(NetworkSessionCoordinator).GetMethod("CleanupLobby", Flags);
            var cleanup = (Task)method.Invoke(_coordinator, new object[] { "old-session", scope });
            generation++;
            var next = new Lobby(id: "new-session"); Set("_currentLobby", next); Set("_state", SessionState.InGame);
            gateway.Cleanup.SetResult(Result<Unit>.Success(Unit.Value));
            while (!cleanup.IsCompleted) yield return null;
            Assert.AreSame(next, typeof(NetworkSessionCoordinator).GetField("_currentLobby", Flags).GetValue(_coordinator));
            Assert.AreEqual(SessionState.InGame, _coordinator.State);
        }
        sealed class LobbyFake : ILobbyGateway
        {
            public readonly TaskCompletionSource<Result<Unit>> Cleanup = new();
            public string CleanupId;
            public Task<Result<Unit>> DeleteAsync(string id) { CleanupId = id; return Cleanup.Task; }
            public Task<Result<Unit>> RemovePlayerAsync(string id, string player) { CleanupId = id; return Cleanup.Task; }
            public Task<Result<Lobby>> CreateAsync(string name, int max, CreateLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> JoinByCodeAsync(string code, JoinLobbyByCodeOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> JoinByIdAsync(string id, JoinLobbyByIdOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> QuickJoinAsync(QuickJoinLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<List<Lobby>>> QueryAsync(QueryLobbiesOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> GetAsync(string id) => throw new NotSupportedException();
            public Task<Result<Lobby>> UpdateAsync(string id, UpdateLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> UpdatePlayerAsync(string id, string player, UpdatePlayerOptions options) => throw new NotSupportedException();
            public Task<Result<Unit>> SendHeartbeatAsync(string id) => throw new NotSupportedException();
        }
        sealed class Scene : ISceneTransitionService
        { public int Loads; public void LoadTitleScene() { Loads++; } }
        sealed class Services : IUnityServicesGateway
        {
            public bool IsInitialized => true; public bool IsSignedIn => true; public string PlayerId => "test";
            public Task<Result<Unit>> InitializeAndSignInAsync(string profileOverride = null)
                => Task.FromResult(Result<Unit>.Success(Unit.Value));
        }
        sealed class Runtime : INetworkRuntime
        {
            public int ShutdownCount; public bool IsListening => false; public bool IsHost => false;
            public bool IsClient => true; public ulong LocalClientId => 7; public int ConnectedClientCount => 1;
            public void Shutdown() { ShutdownCount++; }
            public Result<Unit> StartHost(RelayServerData data) => Result<Unit>.Success(Unit.Value);
            public Result<Unit> StartClient(RelayServerData data) => Result<Unit>.Success(Unit.Value);
            public Result<Unit> LoadNetworkScene(string name) => Result<Unit>.Success(Unit.Value);
        }
    }
}
