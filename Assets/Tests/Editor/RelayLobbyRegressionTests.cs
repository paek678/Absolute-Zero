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
    public sealed class RelayLobbyRegressionTests
    {
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject _go;
        NetworkSessionCoordinator _coordinator;
        MatchSessionRouter _router;
        Gateway _gateway;
        Relay _relay;

        internal static Lobby Room(string mode = "Multi", int count = 4, bool starting = false, bool started = false,
            string presence = "pulse0", string relay = "", string id = "room", string code = "ABC123",
            string host = "host", bool includeSelf = true)
        {
            var players = new List<Unity.Services.Lobbies.Models.Player> { new(id: host) };
            if (includeSelf && host != "self") players.Add(new Unity.Services.Lobbies.Models.Player(id: "self"));
            return new Lobby(id: id, lobbyCode: code, maxPlayers: count, hostId: host, players: players,
                data: new Dictionary<string, DataObject>
                {
                    ["GameMode"] = new(DataObject.VisibilityOptions.Public, mode),
                    ["PlayerCount"] = new(DataObject.VisibilityOptions.Public, count.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ["GameStarting"] = new(DataObject.VisibilityOptions.Member, starting.ToString()),
                    ["GameStarted"] = new(DataObject.VisibilityOptions.Member, started.ToString()),
                    ["RelayJoinCode"] = new(DataObject.VisibilityOptions.Member, relay),
                    ["HostPresence"] = new(DataObject.VisibilityOptions.Member, presence)
                }, version: 1);
        }

        static OnlineLobbyWait WaitPolicy(Lobby room, float now = 0)
        {
            Assert.IsTrue(OnlineLobbyJoinPolicy.TryRead(room, out var settings));
            return new OnlineLobbyWait(room, "self", settings, now, 30);
        }

        [Test] public void HealthyPregameWait_DoesNotExpireAfterThirtySecondsOrTwentyMinutes()
        {
            var wait = WaitPolicy(Room());
            for (int now = 0; now <= 1200; now += 15)
            {
                var result = wait.Observe(Room(presence: "pulse" + now), now);
                Assert.IsTrue(result.IsSuccess, $"Healthy waiting room expired at {now}");
                Assert.IsNull(result.Value);
            }
            Assert.AreEqual("RELAY", wait.Observe(Room(started: true, relay: "RELAY"), 1201).Value);
        }

        [Test] public void MissingHostPresence_IsBoundedAndDoesNotResetOnRepeatedSnapshots()
        {
            var room = Room(); var wait = WaitPolicy(room);
            Assert.IsTrue(wait.Observe(room, 35).IsSuccess);
            Assert.IsTrue(wait.Observe(room, 59.99f).IsSuccess);
            Assert.AreEqual(OperationErrorCode.Timeout, wait.Observe(room, 60).ErrorCode);
        }

        [Test] public void HostPresenceChange_UsesLocalElapsedTime_NotWallClockOrNonceOrdering()
        {
            var wait = WaitPolicy(Room(presence: "z"), 1000);
            Assert.IsTrue(wait.Observe(Room(presence: "a"), 1059).IsSuccess);
            Assert.IsTrue(wait.Observe(Room(presence: "a"), 1118).IsSuccess);
            Assert.AreEqual(OperationErrorCode.Timeout, wait.Observe(Room(presence: "a"), 1119).ErrorCode);
        }

        [TestCase(false)] [TestCase(true)]
        public void StartupDeadline_BeginsOnlyAtStart_AndHeartbeatCannotExtendIt(bool startedWithoutCode)
        {
            var wait = WaitPolicy(Room());
            for (int now = 15; now <= 90; now += 15) Assert.IsTrue(wait.Observe(Room(presence: now.ToString()), now).IsSuccess);
            Assert.IsTrue(wait.Observe(Room(starting: !startedWithoutCode, started: startedWithoutCode, presence: "100"), 100).IsSuccess);
            Assert.IsTrue(wait.Observe(Room(starting: !startedWithoutCode, started: startedWithoutCode, presence: "129"), 129).IsSuccess);
            Assert.AreEqual(OperationErrorCode.Timeout, wait.Observe(Room(starting: true, presence: "130"), 130).ErrorCode);
        }

        [Test] public void HostMigrationOrMissingSelf_IsRejectedWithoutUsingNewRelayCode()
        {
            Assert.AreEqual(OperationErrorCode.LobbyNotFound, WaitPolicy(Room()).Observe(Room(host: "other", started: true, relay: "RELAY"), 1).ErrorCode);
            Assert.AreEqual(OperationErrorCode.LobbyNotFound, WaitPolicy(Room()).Observe(Room(includeSelf: false), 1).ErrorCode);
        }

        [TestCase("OneVsOne", 2, true)] [TestCase("Multi", 3, true)] [TestCase("Multi", 4, true)]
        [TestCase("OneVsOne", 4, false)] [TestCase("Multi", 2, false)] [TestCase("Solo", 2, false)]
        [TestCase("1", 2, false)] [TestCase("None", 2, false)] [TestCase("Unknown", 4, false)]
        public void Metadata_OnlyRecognizesSupportedAuthoritativeModeAndCount(string mode, int count, bool accepted)
            => Assert.AreEqual(accepted, OnlineLobbyJoinPolicy.TryRead(Room(mode, count), out _));

        [TestCase("GameMode")] [TestCase("PlayerCount")] [TestCase("GameStarting")]
        [TestCase("GameStarted")] [TestCase("HostPresence")] [TestCase("RelayJoinCode")]
        public void MissingMetadata_IsExplicitlyRejected_IncludingIncompatibleOldRooms(string key)
        {
            var room = Room(); room.Data.Remove(key);
            Assert.IsFalse(OnlineLobbyJoinPolicy.TryRead(room, out _));
        }

        [Test] public void ConflictingCapacityOrUnstartedRelay_IsRejected()
        {
            var room = Room(); room.Data["PlayerCount"] = new(DataObject.VisibilityOptions.Public, "3");
            Assert.IsFalse(OnlineLobbyJoinPolicy.TryRead(room, out _));
            Assert.IsFalse(OnlineLobbyJoinPolicy.TryRead(Room(relay: "RELAY"), out _));
            Assert.AreEqual(OperationErrorCode.InvalidState, WaitPolicy(Room()).Observe(Room("OneVsOne", 2), 1).ErrorCode);
        }

        [SetUp] public void Setup()
        {
            _go = new GameObject("RelayLobbyRegressionTests");
            _coordinator = _go.AddComponent<NetworkSessionCoordinator>();
            _coordinator.Router = _router = new MatchSessionRouter();
            _gateway = new Gateway(); _relay = new Relay();
            Set("_services", new Services()); Set("_lobbyGateway", _gateway);
            Set("_membershipReservations", new LobbyMembershipReservations());
            Set("_relayGateway", _relay); Set("_networkRuntime", new Runtime()); Set("_sceneTransition", new Scene());
            _relay.Capture = () => (_coordinator.SelectedMode, _coordinator.SelectedPlayerCount);
        }
        [TearDown] public void TearDown() => Object.DestroyImmediate(_go);
        void Set(string name, object value) => typeof(NetworkSessionCoordinator).GetField(name, Private).SetValue(_coordinator, value);

        [UnityTest] public IEnumerator DuelSelection_AdoptsMultiFourBeforeRelay() => Adoption(GameMode.OneVsOne, "Multi", 4);
        [UnityTest] public IEnumerator DuelSelection_AdoptsMultiThreeBeforeRelay() => Adoption(GameMode.OneVsOne, "Multi", 3);
        [UnityTest] public IEnumerator MultiSelection_AdoptsDuelBeforeRelay() => Adoption(GameMode.Multi, "OneVsOne", 2);
        IEnumerator Adoption(GameMode chosen, string authoritative, int count)
        {
            _coordinator.SetMatchParameters(chosen, chosen == GameMode.Multi ? 4 : 2);
            _gateway.Join = Result<Lobby>.Success(Room(authoritative, count));
            var join = _coordinator.JoinGameAsync("ABC123");
            Assert.AreEqual(count, _coordinator.SelectedPlayerCount);
            Assert.AreEqual(authoritative, _coordinator.SelectedMode.ToString());
            Assert.AreEqual(_coordinator.SelectedMode, _router.Current.Mode);
            Assert.AreEqual(0, _relay.Calls);
            var sameLease = _coordinator.OnlineLease;
            Set("_currentLobby", Room(authoritative, count, started: true, relay: "RELAY"));
            yield return Wait(join);
            Assert.AreEqual(OperationErrorCode.RelayFailed, join.Result.ErrorCode); // Deliberate transport seam stop.
            Assert.AreEqual(1, _relay.Calls);
            Assert.AreEqual(authoritative, _relay.Mode.ToString()); Assert.AreEqual(count, _relay.Count);
            Assert.AreSame(sameLease, _router.Current);
            yield return Wait(_coordinator.LeaveAsync());
        }

        [UnityTest] public IEnumerator WaitingClientCancel_CompletesWithoutStartingRelay()
        {
            _gateway.Join = Result<Lobby>.Success(Room());
            var join = _coordinator.JoinGameAsync("ABC123");
            Assert.AreEqual(SessionOperation.WaitingRelayCode, _coordinator.Operation);
            yield return Wait(_coordinator.LeaveAsync()); yield return Wait(join);
            Assert.AreEqual(OperationErrorCode.Cancelled, join.Result.ErrorCode);
            Assert.AreEqual(0, _relay.Calls); Assert.IsNull(_router.Current); Assert.IsNull(_coordinator.CurrentLobby);
        }

        [UnityTest] public IEnumerator MalformedRoom_RemovesOnlyAuthenticatedSelf_AndNeverStartsRelay()
        {
            var room = Room(); room.Data.Remove("PlayerCount"); _gateway.Join = Result<Lobby>.Success(room);
            var join = _coordinator.JoinGameAsync("ABC123"); yield return Wait(join);
            Assert.AreEqual(OperationErrorCode.InvalidState, join.Result.ErrorCode);
            CollectionAssert.AreEqual(new[] { "room:self" }, _gateway.Removed);
            Assert.AreEqual(0, _relay.Calls); Assert.IsNull(_coordinator.CurrentLobby);
            yield return Wait(_coordinator.LeaveAsync());
        }

        [UnityTest] public IEnumerator ExistingWaitingMembership_RecoversExactCode_NotFirstJoinedLobby()
        {
            PrepareRecovery(Room());
            var join = _coordinator.JoinGameAsync(" abc123 ");
            Assert.AreEqual(SessionOperation.WaitingRelayCode, _coordinator.Operation);
            Assert.AreEqual("room", _coordinator.CurrentLobby.Id);
            Assert.AreEqual(4, _coordinator.SelectedPlayerCount); Assert.AreEqual(0, _gateway.Removed.Count);
            yield return Wait(_coordinator.LeaveAsync()); yield return Wait(join);
            Assert.AreEqual(OperationErrorCode.Cancelled, join.Result.ErrorCode);
            Assert.IsTrue(_gateway.Removed.TrueForAll(x => x == "room:self"));
        }

        [UnityTest] public IEnumerator ExistingActiveMembership_IsRejectedAndOnlyOwnMembershipRemoved() => RejectRejoin(false);
        [UnityTest] public IEnumerator ExistingStartingMembership_IsRejectedBeforeRelay() => RejectRejoin(true);
        IEnumerator RejectRejoin(bool starting)
        {
            PrepareRecovery(Room(starting: starting, started: !starting, relay: starting ? "" : "RELAY"));
            var join = _coordinator.JoinGameAsync("ABC123"); yield return Wait(join);
            Assert.AreEqual(OperationErrorCode.InvalidState, join.Result.ErrorCode);
            Assert.AreEqual(OnlineLobbyJoinPolicy.RejoinBlockedMessage, join.Result.ErrorMessage);
            CollectionAssert.AreEqual(new[] { "room:self" }, _gateway.Removed);
            Assert.AreEqual(0, _relay.Calls); Assert.IsNull(_coordinator.CurrentLobby);
            yield return Wait(_coordinator.LeaveAsync());
        }

        [UnityTest] public IEnumerator ClearedMembership_SecondFreshJoinCannotEnterSameRunningMatch()
        {
            var active = Room(started: true, relay: "RELAY");
            PrepareRecovery(active);
            var first = _coordinator.JoinGameAsync("ABC123"); yield return Wait(first);
            Assert.AreEqual(OperationErrorCode.InvalidState, first.Result.ErrorCode);
            _gateway.Join = Result<Lobby>.Success(active); // The first refusal removed the old membership.
            var retry = _coordinator.JoinGameAsync("ABC123"); yield return Wait(retry);
            Assert.AreEqual(OperationErrorCode.InvalidState, retry.Result.ErrorCode);
            Assert.AreEqual(OnlineLobbyJoinPolicy.RejoinBlockedMessage, retry.Result.ErrorMessage);
            CollectionAssert.AreEqual(new[] { "room:self", "room:self" }, _gateway.Removed);
            Assert.AreEqual(0, _relay.Calls); Assert.IsNull(_coordinator.CurrentLobby);
            yield return Wait(_coordinator.LeaveAsync());
        }

        [UnityTest] public IEnumerator FreshJoinDuringHostStart_IsRejectedBeforeRelay()
        {
            _gateway.Join = Result<Lobby>.Success(Room(starting: true));
            var join = _coordinator.JoinGameAsync("ABC123"); yield return Wait(join);
            Assert.AreEqual(OperationErrorCode.InvalidState, join.Result.ErrorCode);
            CollectionAssert.AreEqual(new[] { "room:self" }, _gateway.Removed);
            Assert.AreEqual(0, _relay.Calls);
            yield return Wait(_coordinator.LeaveAsync());
        }

        [UnityTest] public IEnumerator RecoveredHost_IsNotDeletedOrSilentlyMigrated()
        {
            PrepareRecovery(Room(host: "self"));
            var join = _coordinator.JoinGameAsync("ABC123"); yield return Wait(join);
            Assert.AreEqual(OperationErrorCode.InvalidState, join.Result.ErrorCode);
            Assert.AreEqual(0, _gateway.Removed.Count); Assert.AreEqual(0, _relay.Calls);
            yield return Wait(_coordinator.LeaveAsync());
        }

        [UnityTest] public IEnumerator RecoveryCancellation_IgnoresUncancellableLateReadAndNewSoloOwner()
        {
            PrepareRecovery(Room());
            _gateway.JoinedFlight = new TaskCompletionSource<Result<List<string>>>();
            var join = _coordinator.JoinGameAsync("ABC123");
            yield return Wait(_coordinator.LeaveAsync());
            Assert.IsTrue(_router.TryBegin(GameMode.Solo, () => Task.CompletedTask, out var solo));
            yield return Wait(join);
            Assert.AreEqual(OperationErrorCode.Cancelled, join.Result.ErrorCode);
            _gateway.JoinedFlight.SetResult(Result<List<string>>.Success(new List<string> { "room" }));
            yield return null;
            Assert.AreSame(solo, _router.Current); Assert.AreEqual(0, _gateway.ReadIds.Count);
            Assert.AreEqual(0, _gateway.Removed.Count); Assert.AreEqual(0, _relay.Calls);
        }

        [UnityTest] public IEnumerator TimedOutLocalLeave_ReservesSameMembershipUntilRemoteRemovalSettles()
        {
            _gateway.Join = Result<Lobby>.Success(Room());
            var original = _coordinator.JoinGameAsync("ABC123");
            _gateway.RemoveFlight = new TaskCompletionSource<Result<Unit>>();
            var oldRemoval = _gateway.RemoveFlight;
            yield return Wait(_coordinator.LeaveAsync()); yield return Wait(original);
            Assert.IsNull(_router.Current); Assert.IsNull(_coordinator.CurrentLobby);
            Assert.IsFalse(oldRemoval.Task.IsCompleted); Assert.AreEqual(1, _gateway.Removed.Count);
            var same = _coordinator.JoinGameAsync(" abc123 "); yield return Wait(same);
            Assert.AreEqual(OperationErrorCode.InvalidState, same.Result.ErrorCode);
            Assert.AreEqual(LobbyMembershipReservations.PendingMessage, same.Result.ErrorMessage);
            Assert.AreEqual(1, _gateway.JoinCalls); // No SDK rejoin or membership lookup raced the old removal.

            _gateway.Join = Result<Lobby>.Success(Room(id: "other", code: "OTHER1"));
            var different = _coordinator.JoinGameAsync("OTHER1");
            Assert.AreEqual("other", _coordinator.CurrentLobby.Id); Assert.AreEqual(2, _gateway.JoinCalls);
            oldRemoval.SetResult(Result<Unit>.Success(Unit.Value)); yield return null; yield return null;
            Assert.AreEqual("other", _coordinator.CurrentLobby.Id);
            yield return Wait(_coordinator.LeaveAsync()); yield return Wait(different);

            _gateway.Join = Result<Lobby>.Success(Room()); _gateway.RemoveFlight = null;
            var retry = _coordinator.JoinGameAsync("ABC123");
            Assert.AreEqual("room", _coordinator.CurrentLobby.Id); Assert.AreEqual(3, _gateway.JoinCalls);
            yield return Wait(_coordinator.LeaveAsync()); yield return Wait(retry);
        }

        [UnityTest] public IEnumerator CancelledJoin_UnresolvedSdkResponseReservesSameCodeButNotDifferentRoom()
        {
            var oldJoin = new TaskCompletionSource<Result<Lobby>>(); _gateway.JoinFlight = oldJoin;
            var original = _coordinator.JoinGameAsync("ABC123");
            yield return Wait(_coordinator.LeaveAsync());
            var same = _coordinator.JoinGameAsync("ABC123"); yield return Wait(same);
            Assert.AreEqual(LobbyMembershipReservations.PendingMessage, same.Result.ErrorMessage);
            Assert.AreEqual(1, _gateway.JoinCalls);
            _gateway.JoinFlight = null;
            _gateway.Join = Result<Lobby>.Success(Room(id: "other", code: "OTHER1"));
            var current = _coordinator.JoinGameAsync("OTHER1");
            Assert.AreEqual("other", _coordinator.CurrentLobby.Id);
            var currentLease = _coordinator.OnlineLease;
            oldJoin.SetResult(Result<Lobby>.Success(Room())); yield return Wait(original);
            Assert.AreEqual(OperationErrorCode.Cancelled, original.Result.ErrorCode);
            Assert.AreSame(currentLease, _coordinator.OnlineLease); Assert.AreEqual("other", _coordinator.CurrentLobby.Id);
            CollectionAssert.AreEqual(new[] { "room:self" }, _gateway.Removed);
            yield return Wait(_coordinator.LeaveAsync()); yield return Wait(current);
        }

        [Test] public async Task MembershipReservation_RemoteMutationIsSingleFlightAndScopedToExactUserAndCode()
        {
            var gate = new LobbyMembershipReservations(); int sends = 0;
            var remote = new TaskCompletionSource<Result<Unit>>();
            var first = gate.RemoveAsync("room", "self", "ABC123", () => { sends++; return remote.Task; });
            Assert.AreSame(first, gate.RemoveAsync("room", "self", "ABC123", () => throw new Exception("duplicate dispatch")));
            Assert.IsFalse(gate.TryReserveJoin(" abc123 ", "self", out _));
            Assert.IsTrue(gate.TryReserveJoin("OTHER1", "self", out var differentCode)); differentCode.Dispose();
            Assert.IsTrue(gate.TryReserveJoin("ABC123", "another-player", out var differentPlayer)); differentPlayer.Dispose();
            remote.SetResult(Result<Unit>.Success(Unit.Value)); Assert.IsTrue((await first).IsSuccess);
            Assert.IsTrue(gate.TryReserveJoin("ABC123", "self", out var same)); same.Dispose(); Assert.AreEqual(1, sends);
        }

        [Test] public async Task Recovery_RequiresExactCodeAndAuthenticatedMembership()
        {
            PrepareRecovery(Room(code: "ABC1234"));
            var noMatch = await OnlineLobbyJoinPolicy.RecoverMembershipAsync(_gateway, "ABC123", "self", () => true);
            Assert.AreEqual(OperationErrorCode.LobbyNotFound, noMatch.ErrorCode);
            _gateway.Reads["room"] = Room(includeSelf: false);
            var noSelf = await OnlineLobbyJoinPolicy.RecoverMembershipAsync(_gateway, "ABC123", "self", () => true);
            Assert.AreEqual(OperationErrorCode.LobbyNotFound, noSelf.ErrorCode);
            Assert.AreEqual(0, _gateway.Removed.Count);
        }

        [Test] public async Task Recovery_AmbiguousExactCode_DoesNotPickOrRemoveArbitraryLobby()
        {
            PrepareRecovery(Room()); _gateway.Reads["unrelated"] = Room(id: "unrelated");
            var result = await OnlineLobbyJoinPolicy.RecoverMembershipAsync(_gateway, "ABC123", "self", () => true);
            Assert.AreEqual(OperationErrorCode.InvalidState, result.ErrorCode); Assert.AreEqual(0, _gateway.Removed.Count);
        }

        [Test] public void RouterAdoption_PreservesLeaseAndRejectsSoloForeignOrStoppingOwner()
        {
            Assert.IsTrue(_router.TryBegin(GameMode.OneVsOne, () => Task.CompletedTask, out var lease));
            Assert.IsTrue(_router.TryAdoptOnlineLobbyMode(lease, GameMode.Multi));
            Assert.AreSame(lease, _router.Current); Assert.AreEqual(GameMode.Multi, lease.Mode);
            Assert.IsFalse(_router.TryAdoptOnlineLobbyMode(lease, GameMode.Solo));
            Assert.IsFalse(_router.TryAdoptOnlineLobbyMode(new MatchSessionLease(50, GameMode.Multi), GameMode.OneVsOne));
            _router.End(lease); Assert.IsTrue(_router.TryBegin(GameMode.Solo, () => Task.CompletedTask, out var solo));
            Assert.IsFalse(_router.TryAdoptOnlineLobbyMode(solo, GameMode.OneVsOne));
        }

        [Test] public async Task RouterAdoption_CannotChangeStoppingSession()
        {
            var stopping = new TaskCompletionSource<bool>();
            Assert.IsTrue(_router.TryBegin(GameMode.OneVsOne, () => stopping.Task, out var lease));
            var task = _router.StopAsync();
            Assert.IsFalse(_router.TryAdoptOnlineLobbyMode(lease, GameMode.Multi));
            Assert.AreEqual(GameMode.OneVsOne, lease.Mode);
            stopping.SetResult(true); await task;
        }

        [Test] public async Task Admission_AfterInitialRosterOrDeparture_CannotCreateUnassignedParticipant()
        {
            _coordinator.SetMatchParameters(GameMode.Multi, 4);
            Assert.IsTrue((await _coordinator.EnsureInitializedAsync()).IsSuccess);
            var method = typeof(NetworkSessionCoordinator).GetMethod("ApproveConnection", Private);
            var request = new Unity.Netcode.NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 17 };
            var before = new Unity.Netcode.NetworkManager.ConnectionApprovalResponse { CreatePlayerObject = true };
            method.Invoke(_coordinator, new object[] { request, before });
            Assert.IsTrue(before.Approved); Assert.IsFalse(before.CreatePlayerObject);
            var table = _coordinator.BuildParticipantTable(new ulong[] { 0, 1, 2, 3 }, 4);
            var fixedRoster = new Unity.Netcode.NetworkManager.ConnectionApprovalResponse { CreatePlayerObject = true };
            method.Invoke(_coordinator, new object[] { request, fixedRoster });
            Assert.IsFalse(fixedRoster.Approved); Assert.IsFalse(fixedRoster.CreatePlayerObject);
            table.Unbind(1); // Actual disconnect keeps the archived logical seat.
            var afterDeparture = new Unity.Netcode.NetworkManager.ConnectionApprovalResponse { CreatePlayerObject = true };
            method.Invoke(_coordinator, new object[] { request, afterDeparture });
            Assert.IsFalse(afterDeparture.Approved); Assert.IsFalse(afterDeparture.CreatePlayerObject);
            Assert.AreEqual(OnlineLobbyJoinPolicy.RejoinBlockedMessage, afterDeparture.Reason);
            Assert.AreEqual(4, table.Count); Assert.IsFalse(table.TryGetByClientId(17, out _));
        }

        [Test] public async Task HostPresence_UsesExistingSingleHeartbeatFlight_AndOnlyWritesNonce()
        {
            var room = Room(host: "self"); _gateway.Reads["room"] = room;
            _gateway.BeatFlight = new TaskCompletionSource<Result<Unit>>();
            using var work = new LobbySessionWork(_gateway, room, "self", null, () => true,
                _ => { }, () => { }, _ => { });
            work.Tick(0.1f, true, false); work.Tick(60, true, false);
            Assert.AreEqual(1, _gateway.Beats); Assert.AreEqual(0, _gateway.HostUpdates.Count);
            _gateway.BeatFlight.SetResult(Result<Unit>.Success(Unit.Value));
            await Task.Yield();
            Assert.AreEqual(1, _gateway.HostUpdates.Count);
            CollectionAssert.AreEqual(new[] { "HostPresence" }, _gateway.HostUpdates[0].Data.Keys);
            Assert.IsNotEmpty(_gateway.HostUpdates[0].Data["HostPresence"].Value);
        }

        [TestCase(false)] [TestCase(true)]
        public async Task PendingHeartbeat_AfterGameStartOrDispose_CannotPublishPresence(bool dispose)
        {
            var room = Room(host: "self"); _gateway.Reads["room"] = room;
            _gateway.BeatFlight = new TaskCompletionSource<Result<Unit>>();
            using var work = new LobbySessionWork(_gateway, room, "self", null, () => true,
                _ => { }, () => { }, _ => { });
            work.Tick(0.1f, true, false);
            if (dispose) work.Dispose(); else work.Tick(0.1f, true, true);
            _gateway.BeatFlight.SetResult(Result<Unit>.Success(Unit.Value));
            await Task.Yield(); Assert.AreEqual(0, _gateway.HostUpdates.Count);
        }

        [Test] public void WaitingClient_NeverSendsHostHeartbeatOrPresence()
        {
            var room = Room(); _gateway.Reads["room"] = room;
            using var work = new LobbySessionWork(_gateway, room, "self", null, () => true,
                _ => { }, () => { }, _ => { });
            work.Tick(60, false, false);
            Assert.AreEqual(0, _gateway.Beats); Assert.AreEqual(0, _gateway.HostUpdates.Count);
        }

        [Test] public async Task AlreadyMemberSdkResponse_IsRecoverableWithoutTreatingOtherErrorsAsSuccess()
        {
            var method = typeof(LobbyGateway).GetMethod("WrapLobbyCall", BindingFlags.Static | BindingFlags.NonPublic);
            Func<Task<Lobby>> sdk = () => Task.FromException<Lobby>(new LobbyServiceException(LobbyExceptionReason.LobbyConflict, "player is already a member of the lobby"));
            var result = await (Task<Result<Lobby>>)method.Invoke(null, new object[] { sdk, "JoinByCode" });
            Assert.AreEqual(OperationErrorCode.LobbyAlreadyMember, result.ErrorCode);
            Assert.IsNull(result.Value);
        }

        void PrepareRecovery(Lobby matching)
        {
            _gateway.Join = Result<Lobby>.Failure(OperationErrorCode.LobbyAlreadyMember, "already a member");
            _gateway.JoinedIds = new List<string> { "unrelated", "room" };
            _gateway.Reads["unrelated"] = Room(id: "unrelated", code: "OTHER1");
            _gateway.Reads["room"] = matching;
        }
        static IEnumerator Wait(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(task.IsCompleted, "Application operation stranded its caller");
            Assert.IsFalse(task.IsFaulted, task.Exception?.ToString()); Assert.IsFalse(task.IsCanceled);
        }

        sealed class Gateway : ILobbyGateway
        {
            internal Result<Lobby> Join;
            internal readonly List<string> Removed = new(), ReadIds = new();
            internal List<string> JoinedIds = new();
            internal readonly Dictionary<string, Lobby> Reads = new();
            internal TaskCompletionSource<Result<List<string>>> JoinedFlight;
            internal TaskCompletionSource<Result<Lobby>> JoinFlight;
            internal TaskCompletionSource<Result<Unit>> RemoveFlight;
            internal int JoinCalls;
            internal TaskCompletionSource<Result<Unit>> BeatFlight;
            internal int Beats;
            internal readonly List<UpdateLobbyOptions> HostUpdates = new();
            public Task<Result<Lobby>> JoinByCodeAsync(string code, JoinLobbyByCodeOptions options)
            { JoinCalls++; return JoinFlight?.Task ?? Task.FromResult(Join); }
            public Task<Result<List<string>>> GetJoinedLobbyIdsAsync() => JoinedFlight?.Task ?? Task.FromResult(Result<List<string>>.Success(JoinedIds));
            public Task<Result<Lobby>> GetAsync(string id) { ReadIds.Add(id); return Task.FromResult(Result<Lobby>.Success(Reads[id])); }
            public Task<Result<Unit>> RemovePlayerAsync(string id, string player)
            { Removed.Add(id + ":" + player); return RemoveFlight?.Task ?? Task.FromResult(Result<Unit>.Success(Unit.Value)); }
            public Task<Result<Lobby>> CreateAsync(string name, int count, CreateLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> JoinByIdAsync(string id, JoinLobbyByIdOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> QuickJoinAsync(QuickJoinLobbyOptions options) => throw new NotSupportedException();
            public Task<Result<List<Lobby>>> QueryAsync(QueryLobbiesOptions options) => throw new NotSupportedException();
            public Task<Result<Lobby>> UpdateAsync(string id, UpdateLobbyOptions options)
            { HostUpdates.Add(options); return Task.FromResult(Result<Lobby>.Success(Reads[id])); }
            public Task<Result<Lobby>> UpdatePlayerAsync(string id, string player, UpdatePlayerOptions options) => throw new NotSupportedException();
            public Task<Result<Unit>> DeleteAsync(string id) => throw new NotSupportedException();
            public Task<Result<Unit>> SendHeartbeatAsync(string id)
            { Beats++; return BeatFlight?.Task ?? Task.FromResult(Result<Unit>.Success(Unit.Value)); }
        }
        sealed class Services : IUnityServicesGateway
        {
            public bool IsInitialized => true; public bool IsSignedIn => true; public string PlayerId => "self";
            public Task<Result<Unit>> InitializeAndSignInAsync(string profileOverride = null) => Task.FromResult(Result<Unit>.Success(Unit.Value));
        }
        sealed class Relay : IRelayGateway
        {
            internal int Calls, Count; internal GameMode Mode; internal Func<(GameMode, int)> Capture;
            public Task<Result<RelayJoinResult>> JoinAsync(string code)
            { Calls++; (Mode, Count) = Capture(); return Task.FromResult(Result<RelayJoinResult>.Failure(OperationErrorCode.RelayFailed, "fixture transport stop")); }
            public Task<Result<RelayHostResult>> AllocateAsync(int connections) => throw new NotSupportedException();
        }
        sealed class Runtime : INetworkRuntime
        {
            public bool IsListening => false; public bool IsHost => false; public bool IsClient => false;
            public ulong LocalClientId => 0; public int ConnectedClientCount => 0;
            public void Shutdown() { }
            public Result<Unit> StartHost(RelayServerData data) => throw new NotSupportedException();
            public Result<Unit> StartClient(RelayServerData data) => throw new NotSupportedException();
            public Result<Unit> LoadNetworkScene(string scene) => throw new NotSupportedException();
        }
        sealed class Scene : ISceneTransitionService { public void LoadTitleScene() { } }
    }
}
