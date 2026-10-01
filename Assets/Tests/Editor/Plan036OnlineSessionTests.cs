using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Session;
using NUnit.Framework;
using Unity.Networking.Transport.Relay;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AbsoluteZero.Tests
{
    public class Plan036OnlineSessionTests
    {
        static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject _go;
        NetworkSessionCoordinator _coordinator;
        MatchSessionRouter _router;
        Services _services;
        LobbyFake _lobby;
        Runtime _runtime;

        void Set(string field, object value)
            => typeof(NetworkSessionCoordinator).GetField(field, Flags).SetValue(_coordinator, value);

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("Plan036OnlineSessionTest");
            _coordinator = _go.AddComponent<NetworkSessionCoordinator>();
            _router = new MatchSessionRouter();
            _coordinator.Router = _router;
            _services = new Services();
            _lobby = new LobbyFake();
            _runtime = new Runtime();
            Set("_services", _services);
            Set("_lobbyGateway", _lobby);
            Set("_networkRuntime", _runtime);
            Set("_sceneTransition", new Scene());
        }

        [TearDown]
        public void Teardown() => Object.DestroyImmediate(_go);

        [Test]
        public void ColdCoordinator_DoesNotAcquireOnlineServicesOrOwnership()
        {
            Assert.AreEqual(SessionState.Offline, _coordinator.State);
            Assert.AreEqual(0, _services.Calls);
            Assert.IsNull(_router.Current);
        }

        [UnityTest]
        public IEnumerator ConcurrentInitialization_SharesOneFlight()
        {
            var first = _coordinator.EnsureInitializedAsync();
            var second = _coordinator.EnsureInitializedAsync();
            Assert.AreSame(first, second);
            Assert.AreEqual(1, _services.Calls);
            Assert.AreEqual(SessionState.Initializing, _coordinator.State);
            _services.CompleteSuccess();
            yield return Wait(first);
            Assert.IsTrue(first.Result.IsSuccess);
            Assert.AreEqual(SessionState.Ready, _coordinator.State);
            Assert.IsTrue(_coordinator.HasOnlineOwnership);
        }

        [UnityTest]
        public IEnumerator LateAuthenticationSuccess_CannotMutateSoloOwner()
        {
            var pending = _coordinator.EnsureInitializedAsync();
            yield return Wait(_coordinator.LeaveAsync());
            Assert.IsTrue(_router.TryBegin(GameMode.Solo, () => Task.CompletedTask, out var solo));
            int events = 0;
            _coordinator.OnStateChanged += (_, _) => events++;
            _coordinator.OnError += _ => events++;
            _services.CompleteSuccess();
            yield return Wait(pending);
            Assert.AreEqual(OperationErrorCode.Cancelled, pending.Result.ErrorCode);
            Assert.AreEqual(0, events);
            Assert.AreEqual(SessionState.Offline, _coordinator.State);
            Assert.AreSame(solo, _router.Current);
        }

        [UnityTest]
        public IEnumerator LateAuthenticationFailure_CannotMutateSoloOwner()
        {
            var pending = _coordinator.EnsureInitializedAsync();
            yield return Wait(_coordinator.LeaveAsync());
            Assert.IsTrue(_router.TryBegin(GameMode.Solo, () => Task.CompletedTask, out var solo));
            int errors = 0;
            _coordinator.OnError += _ => errors++;
            _services.CompleteFailure();
            yield return Wait(pending);
            Assert.AreEqual(OperationErrorCode.Cancelled, pending.Result.ErrorCode);
            Assert.AreEqual(0, errors);
            Assert.AreEqual(SessionState.Offline, _coordinator.State);
            Assert.AreSame(solo, _router.Current);
        }

        [UnityTest]
        public IEnumerator ReentryDuringAuthentication_ReusesPendingSdkFlight()
        {
            var old = _coordinator.EnsureInitializedAsync();
            var oldLease = _coordinator.OnlineLease;
            yield return Wait(_coordinator.LeaveAsync());
            var current = _coordinator.EnsureInitializedAsync();
            Assert.AreNotSame(old, current);
            Assert.AreNotSame(oldLease, _coordinator.OnlineLease);
            Assert.AreEqual(1, _services.Calls);
            _services.CompleteSuccess();
            yield return Wait(Task.WhenAll(old, current));
            Assert.AreEqual(OperationErrorCode.Cancelled, old.Result.ErrorCode);
            Assert.IsTrue(current.Result.IsSuccess);
            Assert.IsTrue(_coordinator.HasOnlineOwnership);
            Assert.AreEqual(SessionState.Ready, _coordinator.State);
        }

        [UnityTest]
        public IEnumerator ActiveSolo_RejectsOnlineCommandsWithoutSdkCall()
        {
            Assert.IsTrue(_router.TryBegin(GameMode.Solo, () => Task.CompletedTask, out var solo));
            var initialization = _coordinator.EnsureInitializedAsync();
            var create = _coordinator.CreateLobbyAsync();
            var join = _coordinator.JoinGameAsync("CODE");
            yield return Wait(Task.WhenAll(initialization, create, join));
            Assert.AreEqual(OperationErrorCode.InvalidState, initialization.Result.ErrorCode);
            Assert.AreEqual(OperationErrorCode.InvalidState, create.Result.ErrorCode);
            Assert.AreEqual(OperationErrorCode.InvalidState, join.Result.ErrorCode);
            Assert.AreEqual(0, _services.Calls);
            Assert.AreEqual(0, _lobby.CreateCalls);
            Assert.AreEqual(0, _lobby.JoinCalls);
            Assert.AreSame(solo, _router.Current);
        }

        [UnityTest]
        public IEnumerator LateCreateSuccess_CleansOldRemoteLobbyWithoutMutatingSolo()
        {
            _services.SetReady();
            var create = _coordinator.CreateLobbyAsync("old");
            Assert.AreEqual(1, _lobby.CreateCalls);
            yield return Wait(_coordinator.LeaveAsync());
            Assert.IsTrue(_router.TryBegin(GameMode.Solo, () => Task.CompletedTask, out var solo));
            int events = 0;
            _coordinator.OnStateChanged += (_, _) => events++;
            _lobby.Create.SetResult(Result<Lobby>.Success(new Lobby(id: "old-lobby")));
            yield return Wait(create);
            Assert.AreEqual(OperationErrorCode.Cancelled, create.Result.ErrorCode);
            Assert.AreEqual("old-lobby", _lobby.DeletedId);
            Assert.AreEqual(1, _lobby.DeleteCalls);
            Assert.IsNull(_coordinator.CurrentLobby);
            Assert.AreEqual(0, events);
            Assert.AreSame(solo, _router.Current);
        }

        [UnityTest]
        public IEnumerator ConcurrentCreate_OnlyCreatesOneRemoteLobby()
        {
            _services.SetReady();
            var first = _coordinator.CreateLobbyAsync("first");
            var second = _coordinator.CreateLobbyAsync("second");
            yield return Wait(second);
            Assert.AreEqual(OperationErrorCode.InvalidState, second.Result.ErrorCode);
            Assert.AreEqual(1, _lobby.CreateCalls);
            _lobby.Create.SetResult(Result<Lobby>.Success(new Lobby(id: "owned")));
            yield return Wait(first);
            Assert.IsTrue(first.Result.IsSuccess);
            Assert.AreEqual("owned", _coordinator.CurrentLobby.Id);
        }

        [UnityTest]
        public IEnumerator AuthenticationFailure_ReleasesOwnerAndAllowsRetry()
        {
            var first = _coordinator.EnsureInitializedAsync();
            _services.CompleteFailure();
            yield return Wait(first);
            Assert.AreEqual(OperationErrorCode.AuthenticationFailed, first.Result.ErrorCode);
            Assert.AreEqual(SessionState.Failed, _coordinator.State);
            Assert.IsNull(_router.Current);
            _services.NewFlight();
            var retry = _coordinator.EnsureInitializedAsync();
            Assert.AreEqual(2, _services.Calls);
            _services.CompleteSuccess();
            yield return Wait(retry);
            Assert.IsTrue(retry.Result.IsSuccess);
            Assert.AreEqual(SessionState.Ready, _coordinator.State);
        }

        [UnityTest]
        public IEnumerator ActiveLobbyDisappears_ReleasesOnlineOwner()
        {
            _services.SetReady();
            yield return Wait(_coordinator.EnsureInitializedAsync());
            Set("_currentLobby", new Lobby(id: "lost-lobby"));
            typeof(NetworkSessionCoordinator).GetMethod("OnLobbyLost", Flags).Invoke(_coordinator, null);
            yield return Wait(_coordinator.LeaveAsync());
            Assert.IsNull(_router.Current);
            Assert.IsNull(_coordinator.CurrentLobby);
            Assert.AreEqual(SessionState.Ready, _coordinator.State);
        }

        [Test]
        public void ServicesIdentityGetters_AreSafeBeforeInitialization()
        {
            var gateway = new UnityServicesGateway();
            Assert.DoesNotThrow(() => { _ = gateway.IsSignedIn; _ = gateway.PlayerId; });
        }

        static IEnumerator Wait(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, "The application operation must complete within the fixture deadline");
            if (task.IsFaulted) Assert.Fail(task.Exception.ToString());
            Assert.IsFalse(task.IsCanceled);
        }

        sealed class Services : IUnityServicesGateway
        {
            TaskCompletionSource<Result<Unit>> _flight = new();
            public int Calls;
            public bool IsInitialized { get; private set; }
            public bool IsSignedIn { get; private set; }
            public string PlayerId => IsSignedIn ? "test-player" : null;
            public Task<Result<Unit>> InitializeAndSignInAsync(string profileOverride = null) { Calls++; return _flight.Task; }
            public void SetReady() { IsInitialized = true; IsSignedIn = true; }
            public void CompleteSuccess() { SetReady(); _flight.SetResult(Result<Unit>.Success(Unit.Value)); }
            public void CompleteFailure() => _flight.SetResult(Result<Unit>.Failure(OperationErrorCode.AuthenticationFailed, "injected authentication failure"));
            public void NewFlight() => _flight = new TaskCompletionSource<Result<Unit>>();
        }

        sealed class LobbyFake : ILobbyGateway
        {
            public readonly TaskCompletionSource<Result<Lobby>> Create = new();
            public int CreateCalls;
            public int JoinCalls;
            public int DeleteCalls;
            public string DeletedId;
            public Task<Result<Lobby>> CreateAsync(string name, int max, CreateLobbyOptions options) { CreateCalls++; return Create.Task; }
            public Task<Result<Unit>> DeleteAsync(string id) { DeleteCalls++; DeletedId = id; return Task.FromResult(Result<Unit>.Success(Unit.Value)); }
            public Task<Result<Unit>> RemovePlayerAsync(string id, string player) => Task.FromResult(Result<Unit>.Success(Unit.Value));
            public Task<Result<Lobby>> JoinByCodeAsync(string code, JoinLobbyByCodeOptions options) { JoinCalls++; throw new NotSupportedException(); }
            public Task<Result<Lobby>> JoinByIdAsync(string id, JoinLobbyByIdOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> QuickJoinAsync(QuickJoinLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<List<Lobby>>> QueryAsync(QueryLobbiesOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> GetAsync(string id) => throw new NotSupportedException();
            public Task<Result<Lobby>> UpdateAsync(string id, UpdateLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> UpdatePlayerAsync(string id, string player, UpdatePlayerOptions options) => throw new NotSupportedException();
            public Task<Result<Unit>> SendHeartbeatAsync(string id) => throw new NotSupportedException();
        }

        sealed class Scene : ISceneTransitionService
        { public void LoadTitleScene() { } }

        sealed class Runtime : INetworkRuntime
        {
            public bool IsListening => false;
            public bool IsHost => false;
            public bool IsClient => false;
            public ulong LocalClientId => 0;
            public int ConnectedClientCount => 0;
            public void Shutdown() { }
            public Result<Unit> StartHost(RelayServerData data) => Result<Unit>.Success(Unit.Value);
            public Result<Unit> StartClient(RelayServerData data) => Result<Unit>.Success(Unit.Value);
            public Result<Unit> LoadNetworkScene(string name) => Result<Unit>.Success(Unit.Value);
        }
    }
}
