using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using AbsoluteZero.Core.Session;
using NUnit.Framework;
using AbsoluteZero.Core.Network;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037LobbySessionTests
    {
        sealed class Gateway : ILobbyGateway
        {
            internal readonly List<TaskCompletionSource<Result<Lobby>>> Writes = new();
            internal readonly List<string> Dtos = new();
            internal readonly List<TaskCompletionSource<Result<Lobby>>> Reads = new();
            internal readonly List<TaskCompletionSource<Result<Unit>>> Beats = new();
            internal bool ThrowWrite;
            public Task<Result<Lobby>> UpdatePlayerAsync(string id, string player, UpdatePlayerOptions options)
            {
                if (ThrowWrite) throw new InvalidOperationException("SDK fault");
                Dtos.Add(options.Data["CosmeticData"].Value);
                var completion = new TaskCompletionSource<Result<Lobby>>();
                Writes.Add(completion);
                return completion.Task;
            }
            public Task<Result<Lobby>> GetAsync(string id)
            { var completion = new TaskCompletionSource<Result<Lobby>>(); Reads.Add(completion); return completion.Task; }
            public Task<Result<Unit>> SendHeartbeatAsync(string id)
            { var completion = new TaskCompletionSource<Result<Unit>>(); Beats.Add(completion); return completion.Task; }
            public Task<Result<Lobby>> CreateAsync(string name, int count, CreateLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> JoinByCodeAsync(string code, JoinLobbyByCodeOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> JoinByIdAsync(string id, JoinLobbyByIdOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> QuickJoinAsync(QuickJoinLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<List<Lobby>>> QueryAsync(QueryLobbiesOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> UpdateAsync(string id, UpdateLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<Unit>> DeleteAsync(string id) => throw new NotSupportedException();
            public Task<Result<Unit>> RemovePlayerAsync(string id, string player) => throw new NotSupportedException();
        }

        Gateway _gateway;
        LobbyWriteReservations _gate;
        LobbySessionWork _work;
        bool _current;
        int _changed, _lost;
        readonly List<string> _diagnostics = new();
        static Lobby Snapshot(int version, string id = "lobby") => new(id: id, version: version);

        [SetUp] public void Setup()
        {
            _gateway = new Gateway(); _gate = new LobbyWriteReservations();
            _current = true; _changed = _lost = 0; _diagnostics.Clear();
            _work = Create();
        }
        LobbySessionWork Create(Func<Task> delay = null, Action<Lobby> changed = null, string player = "player")
            => new(_gateway, Snapshot(1), player, null, () => _current,
                changed ?? (_ => _changed++), () => _lost++, _diagnostics.Add,
                reservations: _gate, retryDelay: delay ?? (() => Task.CompletedTask));
        [TearDown] public void Teardown() => _work.Dispose();
        static async Task<T> Done<T>(Task<T> task)
        {
            if (await Task.WhenAny(task, Task.Delay(2000)) != task) Assert.Fail("Operation stranded a caller");
            return await task;
        }
        static async Task Until(Func<bool> ready)
        {
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (!ready() && DateTime.UtcNow < deadline) await Task.Delay(1);
            Assert.IsTrue(ready(), "Operation did not progress");
        }
        void Succeed(int index, int version) => _gateway.Writes[index].SetResult(Result<Lobby>.Success(Snapshot(version)));
        void Fail(int index, OperationErrorCode code = OperationErrorCode.Unexpected)
            => _gateway.Writes[index].SetResult(Result<Lobby>.Failure(code, "injected"));

        [Test] public async Task LatestPending_ReplacesB_AndAllCallersSettle()
        {
            var a = _work.PublishAsync("a", 1); var b = _work.PublishAsync("b", 2); var c = _work.PublishAsync("c", 3);
            Assert.AreEqual(1, _gateway.Writes.Count);
            Assert.AreEqual(CosmeticPublicationStatus.Superseded, (await Done(b)).Status);
            Succeed(0, 2);
            Assert.AreEqual(1, (await Done(a)).Revision);
            await Until(() => _gateway.Writes.Count == 2);
            CollectionAssert.AreEqual(new[] { "a", "c" }, _gateway.Dtos);
            Succeed(1, 3);
            Assert.AreEqual(CosmeticPublicationStatus.Published, (await Done(c)).Status);
            Assert.AreEqual(3, _work.ConfirmedRevision);
        }

        [Test] public async Task PermanentFailure_FailsPending_AndSameRevisionCanRetry()
        {
            var a = _work.PublishAsync("a", 1); var c = _work.PublishAsync("c", 3);
            Fail(0);
            Assert.AreEqual(CosmeticPublicationStatus.Failed, (await Done(a)).Status);
            Assert.AreEqual(CosmeticPublicationStatus.Failed, (await Done(c)).Status);
            var retry = _work.PublishAsync("c", 3);
            await Until(() => _gateway.Writes.Count == 2); Succeed(1, 2);
            Assert.AreEqual(CosmeticPublicationStatus.Published, (await Done(retry)).Status);
        }

        [Test] public async Task RateLimit_RetriesOnlyLatest_Once()
        {
            var a = _work.PublishAsync("a", 1); var b = _work.PublishAsync("b", 2);
            Fail(0, OperationErrorCode.RateLimited);
            await Until(() => _gateway.Writes.Count == 2);
            Assert.AreEqual(CosmeticPublicationStatus.Superseded, (await Done(a)).Status);
            Assert.AreEqual("b", _gateway.Dtos[1]);
            var c = _work.PublishAsync("c", 3);
            Fail(1, OperationErrorCode.RateLimited);
            Assert.AreEqual(CosmeticPublicationStatus.Failed, (await Done(b)).Status);
            Assert.AreEqual(CosmeticPublicationStatus.Failed, (await Done(c)).Status);
            Assert.AreEqual(2, _gateway.Writes.Count);
        }

        [Test] public async Task DisposeDuringRetryDelay_CancelsEveryWaiter_WithoutAnotherSend()
        {
            var delay = new TaskCompletionSource<bool>(); _work.Dispose(); _work = Create(() => delay.Task);
            var a = _work.PublishAsync("a", 1); var b = _work.PublishAsync("b", 2);
            Fail(0, OperationErrorCode.RateLimited); _work.Dispose();
            Assert.AreEqual(CosmeticPublicationStatus.Cancelled, (await Done(a)).Status);
            Assert.AreEqual(CosmeticPublicationStatus.Cancelled, (await Done(b)).Status);
            delay.SetResult(true); await Task.Yield(); Assert.AreEqual(1, _gateway.Writes.Count);
        }

        [Test] public async Task DisposedSession_RejoinSameIdentity_WaitsForUnresolvedRemoteWrite()
        {
            var a = _work.PublishAsync("old", 1); var b = _work.PublishAsync("old-pending", 2);
            _work.Dispose();
            Assert.AreEqual(CosmeticPublicationStatus.Cancelled, (await Done(a)).Status);
            Assert.AreEqual(CosmeticPublicationStatus.Cancelled, (await Done(b)).Status);
            _work = Create(); var current = _work.PublishAsync("new", 3);
            Assert.AreEqual(1, _gateway.Writes.Count);
            Succeed(0, 2); await Until(() => _gateway.Writes.Count == 2);
            Assert.AreEqual(0, _changed); // Old completion cannot publish into the new lease.
            Succeed(1, 3);
            Assert.AreEqual(CosmeticPublicationStatus.Published, (await Done(current)).Status);
        }

        [Test] public async Task UnresolvedWrite_DoesNotBlockDifferentRemoteIdentity()
        {
            var a = _work.PublishAsync("a", 1);
            using var other = Create(player: "other"); var b = other.PublishAsync("b", 1);
            Assert.AreEqual(2, _gateway.Writes.Count);
            Succeed(1, 2); Succeed(0, 2);
            Assert.AreEqual(CosmeticPublicationStatus.Published, (await Done(b)).Status);
            Assert.AreEqual(CosmeticPublicationStatus.Published, (await Done(a)).Status);
        }

        [TestCase(false)] [TestCase(true)]
        public async Task ThrownOrDefaultSdkResult_IsFailure_AndAllowsRetry(bool thrown)
        {
            _gateway.ThrowWrite = thrown;
            var request = _work.PublishAsync("a", 1);
            if (!thrown) _gateway.Writes[0].SetResult(default);
            Assert.AreEqual(CosmeticPublicationStatus.Failed, (await Done(request)).Status);
            _gateway.ThrowWrite = false; var retry = _work.PublishAsync("a", 1);
            await Until(() => _gateway.Writes.Count == (thrown ? 1 : 2));
            Succeed(_gateway.Writes.Count - 1, 2);
            Assert.AreEqual(CosmeticPublicationStatus.Published, (await Done(retry)).Status);
        }

        [Test] public async Task DuplicateSubmission_DoesNotWriteTwice_ConflictingRevisionRejected()
        {
            var a = _work.PublishAsync("a", 1);
            Assert.AreSame(a, _work.PublishAsync("a", 1));
            Assert.AreEqual(CosmeticPublicationStatus.Failed, (await Done(_work.PublishAsync("other", 1))).Status);
            Succeed(0, 2); await Done(a);
            Assert.AreEqual(CosmeticPublicationStatus.Published, (await Done(_work.PublishAsync("a", 1))).Status);
            Assert.AreEqual(1, _gateway.Writes.Count);
        }

        [Test] public void OlderAndEqualVersions_CannotRollbackOrNotifyTwice()
        {
            Assert.IsTrue(_work.TryAccept(Snapshot(4), 0));
            Assert.IsFalse(_work.TryAccept(Snapshot(3), 0)); Assert.IsFalse(_work.TryAccept(Snapshot(4), 0));
            Assert.IsFalse(_work.TryAccept(Snapshot(5, "another-lobby"), 0));
            Assert.AreEqual(4, _work.Current.Version); Assert.AreEqual(1, _changed);
        }

        [TestCase(LobbyExceptionReason.LobbyNotFound, true)]
        [TestCase(LobbyExceptionReason.PlayerNotFound, true)]
        [TestCase(LobbyExceptionReason.Unauthorized, false)]
        [TestCase(LobbyExceptionReason.RateLimited, false)]
        [TestCase(LobbyExceptionReason.ValidationError, false)]
        public async Task LeaveAlreadyAbsent_IsSuccessOnlyForTheRequestedAbsence(LobbyExceptionReason reason, bool expected)
        {
            Func<Task> sdk = () => Task.FromException(new LobbyServiceException(reason, "injected removal response"));
            if (!expected) UnityEngine.TestTools.LogAssert.Expect(LogType.Error, "[LobbyGateway] TestLeave failed: injected removal response");
            var wrapper = typeof(LobbyGateway).GetMethod("WrapLobbyVoidCall", BindingFlags.Static | BindingFlags.NonPublic);
            var operation = (Task<Result<Unit>>)wrapper.Invoke(null, new object[] { sdk, "TestLeave", true });
            Assert.AreEqual(expected, (await Done(operation)).IsSuccess);
        }

        [Test] public async Task PrePublicationReadOrNameReadyResponse_CannotReplaceConfirmedMetadata()
        {
            long before = _work.MetadataEpoch;
            var read = _work.PollAsync(); var write = _work.PublishAsync("new", 1);
            Succeed(0, 3); await Done(write);
            Assert.IsFalse(_work.TryAccept(Snapshot(8), before));
            _gateway.Reads[0].SetResult(Result<Lobby>.Success(Snapshot(9))); await read;
            Assert.AreEqual(3, _work.Current.Version);
            Assert.IsTrue(_work.TryAccept(Snapshot(10), _work.MetadataEpoch));
        }

        [Test] public async Task WriteSuccessWithOlderSnapshot_IsPublished_WithoutCacheRollback()
        {
            var write = _work.PublishAsync("a", 1);
            Assert.IsTrue(_work.TryAccept(Snapshot(10), 0)); Succeed(0, 5);
            Assert.AreEqual(CosmeticPublicationStatus.Published, (await Done(write)).Status);
            Assert.AreEqual(10, _work.Current.Version);
        }

        [Test] public async Task SubscriberFailure_DoesNotStrandPublication()
        {
            _work.Dispose(); _work = Create(changed: _ => throw new InvalidOperationException("view closed"));
            var a = _work.PublishAsync("a", 1); Succeed(0, 2);
            Assert.AreEqual(CosmeticPublicationStatus.Published, (await Done(a)).Status);
            Assert.AreEqual(1, _diagnostics.Count);
        }

        [Test] public async Task LostOwnership_DiscardsLateSuccess()
        {
            var a = _work.PublishAsync("a", 1); _current = false; Succeed(0, 2);
            Assert.AreEqual(CosmeticPublicationStatus.Cancelled, (await Done(a)).Status);
            Assert.AreEqual(0, _changed);
        }

        [Test] public async Task Scheduling_IsSingleFlight_AndStopsDuringGameOrDisposal()
        {
            _work.Tick(0.1f, true, false); _work.Tick(60, true, false);
            Assert.AreEqual(1, _gateway.Reads.Count); Assert.AreEqual(1, _gateway.Beats.Count);
            _gateway.Reads[0].SetResult(Result<Lobby>.Success(Snapshot(2)));
            _gateway.Beats[0].SetResult(Result<Unit>.Success(Unit.Value)); await Task.Yield();
            _work.Tick(60, true, true); Assert.AreEqual(1, _gateway.Reads.Count);
            _work.Tick(0, true, false); Assert.AreEqual(1, _gateway.Reads.Count);
            _work.Tick(2, true, false); Assert.AreEqual(2, _gateway.Reads.Count);
            _work.Dispose(); _gateway.Reads[1].SetResult(Result<Lobby>.Success(Snapshot(3)));
            _work.Tick(60, true, false); Assert.AreEqual(2, _gateway.Reads.Count);
        }

        [TestCase(OperationErrorCode.LobbyNotFound, 1)]
        [TestCase(OperationErrorCode.LobbyConflict, 1)]
        [TestCase(OperationErrorCode.Unexpected, 0)]
        public async Task PollFailure_OnlyKnownMembershipFailuresLoseLobby(OperationErrorCode code, int lost)
        {
            var read = _work.PollAsync();
            _gateway.Reads[0].SetResult(Result<Lobby>.Failure(code, "injected")); await read;
            Assert.AreEqual(lost, _lost);
        }

        sealed class Services : IUnityServicesGateway
        {
            public bool IsInitialized => true;
            public bool IsSignedIn => true;
            public string PlayerId => "player";
            public Task<Result<Unit>> InitializeAndSignInAsync(string profile = null)
                => throw new InvalidOperationException("Publication must not initialize services");
        }

        [Test] public async Task OfflinePublication_IsNotApplicable_AndDoesNotInitializeUgs()
        {
            var go = new GameObject("R10 offline test");
            try
            {
                var coordinator = go.AddComponent<NetworkSessionCoordinator>();
                coordinator.Router = new MatchSessionRouter();
                var result = await coordinator.PublishCosmeticsAsync("test", 1);
                Assert.AreEqual(CosmeticPublicationStatus.NotApplicable, result.Status);
                Assert.IsNull(coordinator.OnlineLease);
                Assert.AreEqual(SessionState.Offline, coordinator.State);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void CoordinatorAndLegacyCache_ShareOneSnapshotGate()
        {
            var go = new GameObject("R10 snapshot integration");
            // EditMode does not run this scene component's Awake. Bind only the
            // test singleton and restore the prior one; do not start persistent services.
            var singleton = typeof(LobbyManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousManager = singleton.GetValue(null);
            var coordinatorSingleton = typeof(NetworkSessionCoordinator).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousCoordinator = coordinatorSingleton.GetValue(null);
            try
            {
                var manager = go.AddComponent<LobbyManager>();
                singleton.SetValue(null, manager);
                var coordinator = go.AddComponent<NetworkSessionCoordinator>();
                coordinatorSingleton.SetValue(null, coordinator);
                var router = new MatchSessionRouter(); coordinator.Router = router;
                Assert.IsTrue(router.TryBegin(GameMode.Multi, () => Task.CompletedTask, out var lease));
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                void Set(string name, object value) => typeof(NetworkSessionCoordinator).GetField(name, flags).SetValue(coordinator, value);
                Set("_services", new Services()); Set("_lobbyGateway", _gateway);
                Set("_lease", lease); Set("_currentLobby", Snapshot(1));
                manager.SyncFromCoordinator(coordinator.CurrentLobby, false);
                var owner = coordinator.LobbyWork;
                int updates = 0; manager.OnLobbyUpdated += _ => updates++;
                Assert.IsTrue(coordinator.AcceptLobbyResponse(owner, Snapshot(6), 0));
                Assert.AreSame(coordinator.CurrentLobby, manager.CurrentLobby);
                var legacy = typeof(LobbyManager).GetMethod("AcceptUpdatedLobby", flags);
                Assert.IsFalse((bool)legacy.Invoke(manager, new object[] { Snapshot(5), owner, 0L }));
                Assert.IsFalse((bool)legacy.Invoke(manager, new object[] { Snapshot(6), owner, 0L }));
                Assert.IsFalse((bool)legacy.Invoke(manager, new object[] { Snapshot(8), null, 0L }));
                Assert.AreEqual(6, coordinator.CurrentLobby.Version); Assert.AreEqual(1, updates);
                coordinator.enabled = false;
                Assert.IsFalse(coordinator.AcceptLobbyResponse(owner, Snapshot(9), 0));
            }
            finally
            {
                singleton.SetValue(null, previousManager);
                coordinatorSingleton.SetValue(null, previousCoordinator);
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
