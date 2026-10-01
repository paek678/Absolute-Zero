using System.Collections.Generic;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan036PerspectiveTests
    {
        readonly List<GameObject> _objects = new();

        PlayerBinding Binding(ulong objectId, ulong owner, bool isPlayer)
        {
            var go = new GameObject("Perspective registry fixture");
            _objects.Add(go);
            var network = go.AddComponent<NetworkObject>();
            var inventory = go.AddComponent<PlayerInventory>();
            var state = go.AddComponent<PlayerState>();
            SetNetwork(network, nameof(NetworkObject.NetworkObjectId), objectId);
            SetNetwork(network, nameof(NetworkObject.OwnerClientId), owner);
            SetNetwork(network, nameof(NetworkObject.IsPlayerObject), isPlayer);
            SetNetwork(network, nameof(NetworkObject.IsSpawned), true);
            return new PlayerBinding(state, inventory, network);
        }

        PlayerBinding Add(PlayerRegistry registry, string id, byte seat, ulong? client, ulong objectId, long generation = 42)
        {
            var binding = Binding(objectId, client ?? 0, client.HasValue);
            Assert.That(registry.RegisterPending(binding), Is.True);
            Assert.That(registry.TryPromote(binding, new MatchParticipantDescriptor(id, seat,
                client.HasValue ? PlayerControllerKind.Human : PlayerControllerKind.Bot, client, generation), out var error),
                Is.True, error);
            return binding;
        }

        static void SetNetwork(NetworkObject target, string property, object value)
            => typeof(NetworkObject).GetProperty(property).SetValue(target, value);

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                SetNetwork(go.GetComponent<NetworkObject>(), nameof(NetworkObject.IsSpawned), false);
                go.GetComponent<PlayerInventory>().SlotStates?.Dispose();
                Object.DestroyImmediate(go);
            }
            _objects.Clear();
        }

        [Test]
        public void BotFirstAndSameNgoOwnerNeverSelectBotAsLocalHuman()
        {
            var registry = new PlayerRegistry();
            var bot = Add(registry, "bot", 0, null, 1);
            Assert.That(LocalMatchPerspective.TryResolve(registry, 0, out _), Is.False);
            var human = Add(registry, "human", 1, 0, 2);
            Assert.That(bot.NetworkObject.OwnerClientId, Is.EqualTo(human.NetworkObject.OwnerClientId));
            Assert.That(LocalMatchPerspective.TryResolve(registry, 0, out var perspective), Is.True);
            Assert.That(perspective.HumanSeat, Is.EqualTo(1));
            Assert.That(perspective.HumanBinding, Is.SameAs(human));
            Assert.That(perspective.IsHumanSeat(0), Is.False);
            Assert.That(perspective.TryGetOpponentBinding(out var opponent), Is.True);
            Assert.That(opponent, Is.SameAs(bot));
            Assert.That(opponent.NetworkObject.IsPlayerObject, Is.False);
        }

        [Test]
        public void PendingHumanCannotResolveEvenWhenItsNgoOwnerMatchesTheLocalConnection()
        {
            var registry = new PlayerRegistry();
            Add(registry, "bot", 1, null, 1);
            var human = Binding(2, 0, true);
            registry.RegisterPending(human);
            Assert.That(LocalMatchPerspective.TryResolve(registry, 0, out var unresolved), Is.False);
            Assert.That(unresolved.HumanSeat, Is.EqualTo(byte.MaxValue));
            Assert.That(unresolved.TryGetBinding(1, out _), Is.False);
            Assert.That(registry.TryPromote(human,
                new MatchParticipantDescriptor("human", 0, PlayerControllerKind.Human, 0, 42), out _), Is.True);
            Assert.That(LocalMatchPerspective.TryResolve(registry, 0, out var ready), Is.True);
            Assert.That(ready.HumanBinding, Is.SameAs(human));
        }

        [Test]
        public void OnlineClientIdAndLogicalSeatRemainDifferentDomains()
        {
            var registry = new PlayerRegistry();
            var host = Add(registry, "host", 1, 0, 11);
            var remote = Add(registry, "remote", 0, 73, 12);
            Assert.That(LocalMatchPerspective.TryResolve(registry, 73, out var perspective), Is.True);
            Assert.That(perspective.HumanSeat, Is.Zero);
            Assert.That(perspective.HumanBinding, Is.SameAs(remote));
            Assert.That(perspective.TryGetOpponentBinding(out var opponent), Is.True);
            Assert.That(opponent, Is.SameAs(host));
            Assert.That(LocalMatchPerspective.TryResolve(registry, 1, out _), Is.False);
        }

        [Test]
        public void MultiRequiresExplicitSeatInsteadOfChoosingAnArbitraryOpponent()
        {
            var registry = new PlayerRegistry();
            var seats = new PlayerBinding[4];
            foreach (byte seat in new byte[] { 2, 0, 3, 1 })
                seats[seat] = Add(registry, "p" + seat, seat, (ulong)(40 + seat), (ulong)(80 + seat));
            Assert.That(LocalMatchPerspective.TryResolve(registry, 42, out var perspective), Is.True);
            Assert.That(perspective.TryGetOpponentBinding(out var arbitrary), Is.False);
            Assert.That(arbitrary, Is.Null);
            for (byte seat = 0; seat < 4; seat++)
            {
                Assert.That(perspective.TryGetBinding(seat, out var binding), Is.True);
                Assert.That(binding, Is.SameAs(seats[seat]));
                Assert.That(perspective.IsHumanSeat(seat), Is.EqualTo(seat == 2));
            }
            Assert.That(perspective.TryGetBinding(-1, out _), Is.False);
            Assert.That(perspective.TryGetBinding(256, out _), Is.False);
        }

        [Test]
        public void RetainedPerspectiveRejectsAReplacementThatReusesSeatClientAndNetworkObjectId()
        {
            var registry = new PlayerRegistry();
            var old = Add(registry, "human", 0, 0, 1);
            Add(registry, "bot", 1, null, 2);
            Assert.That(LocalMatchPerspective.TryResolve(registry, 0, out var stale), Is.True);
            registry.Unregister(old);
            var replacement = Add(registry, "human", 0, 0, 1);
            Assert.That(stale.IsHumanSeat(0), Is.False);
            Assert.That(stale.TryGetBinding(1, out _), Is.False);
            Assert.That(stale.TryGetOpponentBinding(out _), Is.False);
            Assert.That(LocalMatchPerspective.TryResolve(registry, 0, out var current), Is.True);
            Assert.That(current.HumanBinding, Is.SameAs(replacement));
        }

        [Test]
        public void LiveRegistryViewAcceptsLateOpponentAndRejectsItsDespawn()
        {
            var registry = new PlayerRegistry();
            Add(registry, "human", 0, 0, 1);
            LocalMatchPerspective.TryResolve(registry, 0, out var perspective);
            Assert.That(perspective.TryGetOpponentBinding(out _), Is.False);
            var bot = Add(registry, "bot", 1, null, 2);
            Assert.That(perspective.TryGetOpponentBinding(out var opponent), Is.True);
            Assert.That(opponent, Is.SameAs(bot));
            SetNetwork(bot.NetworkObject, nameof(NetworkObject.IsSpawned), false);
            Assert.That(perspective.TryGetOpponentBinding(out _), Is.False);
            Assert.That(perspective.TryGetBinding(1, out _), Is.False);
        }

        [Test]
        public void ClearedRegistryAndNextGenerationCannotReviveAnOldPerspective()
        {
            var registry = new PlayerRegistry();
            Add(registry, "human", 0, 0, 1);
            LocalMatchPerspective.TryResolve(registry, 0, out var old);
            registry.Clear();
            Add(registry, "human", 0, 0, 1, 43);
            Add(registry, "bot", 1, null, 2, 43);
            Assert.That(old.IsHumanSeat(0), Is.False);
            Assert.That(old.TryGetOpponentBinding(out _), Is.False);
            Assert.That(LocalMatchPerspective.TryResolve(registry, 0, out var current), Is.True);
            Assert.That(current.TryGetOpponentBinding(out var opponent), Is.True);
            Assert.That(opponent.Identity.Generation, Is.EqualTo(43));
        }

        [TestCase(-1)]
        [TestCase(4)]
        [TestCase(255)]
        public void UnresolvedLocalSeatCannotClaimAnyRemoteVisualSlot(int localSeat)
        {
            for (int seat = 0; seat < 4; seat++)
                Assert.That(AZPlayerVisual.GetRemoteVisualSlot(seat, localSeat), Is.EqualTo(-1));
        }
    }
}
