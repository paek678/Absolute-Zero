using System.Reflection;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    // Boundary tests only. Successful selection/consumption parity is exercised by
    // the separate real-network ActionFixture, including actual human RPC tickets.
    public sealed class Plan036ActionAuthorityTests
    {
        GameObject _object;
        PlayerState _state;
        readonly PrepInputKey _key = new(10, 1, 1, 1);

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Shared action authority boundary");
            _object.SetActive(false);
            _object.AddComponent<NetworkObject>();
            _state = _object.AddComponent<PlayerState>();
            _state.GetActionQueue().SetSelected(4, null, 1, 77);
        }

        [TearDown]
        public void TearDown()
        {
            SetBehaviour(nameof(NetworkBehaviour.IsServer), false);
            SetBehaviour(nameof(NetworkBehaviour.IsSpawned), false);
            Object.DestroyImmediate(_object);
        }

        [Test]
        public void ServerOwnedHumanCannotUseTrustedBotCommandsToBypassItsMiniGame()
        {
            _state.ConfigureParticipant(new MatchParticipantDescriptor("human", 0,
                PlayerControllerKind.Human, 0, 10));
            SetBehaviour(nameof(NetworkBehaviour.IsServer), true);
            SetBehaviour(nameof(NetworkBehaviour.IsSpawned), true);
            AssertEveryCommandRejected(PlayerActionStatus.Rejected, PlayerActionReason.WrongController);
        }

        [Test]
        public void ConfiguredBotCannotActBeforeNetworkSpawn()
        {
            _state.ConfigureParticipant(new MatchParticipantDescriptor("bot", 1,
                PlayerControllerKind.Bot, null, 10));
            SetBehaviour(nameof(NetworkBehaviour.IsServer), true);
            AssertEveryCommandRejected(PlayerActionStatus.Rejected, PlayerActionReason.NotServer);
        }

        [Test]
        public void ClientCannotCallTrustedBotCommandsEvenWhenItHasTheBotDescriptor()
        {
            _state.ConfigureParticipant(new MatchParticipantDescriptor("bot", 1,
                PlayerControllerKind.Bot, null, 10));
            SetBehaviour(nameof(NetworkBehaviour.IsSpawned), true);
            AssertEveryCommandRejected(PlayerActionStatus.Rejected, PlayerActionReason.NotServer);
        }

        [Test]
        public void ASpawnFlagAndBotDescriptorDoNotReplaceAnActiveSoloBindingAndSession()
        {
            _state.ConfigureParticipant(new MatchParticipantDescriptor("bot", 1,
                PlayerControllerKind.Bot, null, 10));
            SetBehaviour(nameof(NetworkBehaviour.IsServer), true);
            SetBehaviour(nameof(NetworkBehaviour.IsSpawned), true);
            AssertEveryCommandRejected(PlayerActionStatus.Stale, PlayerActionReason.StaleSession);
        }

        void AssertEveryCommandRejected(PlayerActionStatus status, PlayerActionReason reason)
        {
            AssertResult(_state.ServerValidateBotItem(_key, 77, 0, out var candidate), status, reason);
            Assert.That(candidate.ItemData, Is.Null);
            AssertResult(_state.ServerQueueBotItem(_key, candidate), status, reason);
            AssertResult(_state.ServerCancelBotSelection(_key), status, reason);
            AssertResult(_state.ServerPressBotReady(_key), status, reason);
            Assert.That(_state.HasSelectedItem.Value, Is.False);
            Assert.That(_state.IsReady.Value, Is.False);
            Assert.That(_state.GetActionQueue().selectedAction.HasValue, Is.True);
            Assert.That(_state.GetActionQueue().selectedAction.Value.CopyId, Is.EqualTo(77));
            Assert.That(_state.GetActionQueue().isReady, Is.False);
        }

        static void AssertResult(PlayerActionResult result, PlayerActionStatus status, PlayerActionReason reason)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Status, Is.EqualTo(status));
            Assert.That(result.Reason, Is.EqualTo(reason));
        }

        void SetBehaviour(string property, bool value)
            => typeof(NetworkBehaviour).GetProperty(property, BindingFlags.Public | BindingFlags.Instance).SetValue(_state, value);
    }
}
