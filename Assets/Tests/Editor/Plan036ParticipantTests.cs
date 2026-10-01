using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan036ParticipantTests
    {
        readonly List<GameObject> _objects = new();
        static MatchParticipantDescriptor Human(byte seat = 0, ulong client = 0, long generation = 12, string id = "human")
            => new(id, seat, PlayerControllerKind.Human, client, generation);
        static MatchParticipantDescriptor Bot(byte seat = 1, long generation = 12, string id = "bot")
            => new(id, seat, PlayerControllerKind.Bot, null, generation);

        // These are registry unit fixtures, not transport evidence. Set only NGO's
        // object metadata; the separate Play fixture exercises real Spawn calls.
        PlayerBinding Binding(ulong objectId, ulong owner, bool playerObject)
        {
            var go = new GameObject("Participant registry test");
            _objects.Add(go);
            var network = go.AddComponent<NetworkObject>();
            var inventory = go.AddComponent<PlayerInventory>();
            var state = go.AddComponent<PlayerState>();
            SetNetworkProperty(network, nameof(NetworkObject.NetworkObjectId), objectId);
            SetNetworkProperty(network, nameof(NetworkObject.OwnerClientId), owner);
            SetNetworkProperty(network, nameof(NetworkObject.IsPlayerObject), playerObject);
            SetNetworkProperty(network, nameof(NetworkObject.IsSpawned), true);
            return new PlayerBinding(state, inventory, network);
        }

        static void SetNetworkProperty(NetworkObject target, string property, object value)
            => typeof(NetworkObject).GetProperty(property).SetValue(target, value);

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects)
            {
                if (go == null) continue;
                SetNetworkProperty(go.GetComponent<NetworkObject>(), nameof(NetworkObject.IsSpawned), false);
                var inventory = go.GetComponent<PlayerInventory>();
                inventory.SlotStates?.Dispose();
                UnityEngine.Object.DestroyImmediate(go);
            }
            _objects.Clear();
        }

        [Test]
        public void DescriptorRejectsFakeBotConnectionAndUnserializableIdentity()
        {
            Assert.Throws<ArgumentException>(() => new MatchParticipantDescriptor("bot", 1, PlayerControllerKind.Bot, 0, 12));
            Assert.Throws<ArgumentException>(() => new MatchParticipantDescriptor(new string('가', 42), 0, PlayerControllerKind.Human, 0, 12));
            Assert.Throws<ArgumentException>(() => new MatchParticipantDescriptor(" ", 0, PlayerControllerKind.Human, 0, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchParticipantDescriptor("bot", 4, PlayerControllerKind.Bot, null, 12));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MatchParticipantDescriptor("bot", 1, PlayerControllerKind.Bot, null, -1));
        }

        [Test]
        public void MetadataRetainsServerGenerationAndDistinguishesHostZeroFromNoConnection()
        {
            var human = new ParticipantMetadataNetData(Human(generation: 97)).ToDescriptor();
            var bot = new ParticipantMetadataNetData(Bot(generation: 97)).ToDescriptor();
            Assert.That(human.ClientId, Is.EqualTo((ulong)0));
            Assert.That(bot.ClientId, Is.Null);
            Assert.That(human.Generation, Is.EqualTo(97));
            Assert.That(bot.Generation, Is.EqualTo(97));
            Assert.That(new ParticipantMetadataNetData().ToDescriptor(), Is.Null);
        }

        [TestCase("seat")]
        [TestCase("participant")]
        [TestCase("connection")]
        [TestCase("generation")]
        [TestCase("missing")]
        public void DescriptorSetRequiresExactUniqueSeatsAndOneGeneration(string invalid)
        {
            var entries = new List<MatchParticipantDescriptor> { Human(), Bot() };
            if (invalid == "seat") entries[1] = Bot(0);
            if (invalid == "participant") entries[1] = Bot(id: "human");
            if (invalid == "connection") entries[1] = Human(1, 0, id: "other");
            if (invalid == "generation") entries[1] = Bot(generation: 13);
            if (invalid == "missing") entries.RemoveAt(1);
            Assert.That(MatchParticipantDescriptor.TryValidateSet(entries, 2, out var result, out var reason), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void TwoServerOwnedObjectsHaveTwoSeatsButOnlyOneHumanLookup()
        {
            var registry = new PlayerRegistry();
            var human = Binding(10, 0, true);
            var bot = Binding(11, 0, false);
            int registered = 0;
            registry.Registered += _ => registered++;
            Assert.That(registry.RegisterPending(bot), Is.True);
            Assert.That(registry.RegisterPending(human), Is.True);
            Assert.That(registry.TryPromote(bot, Bot(), out _), Is.True);
            Assert.That(registry.TryPromote(human, Human(), out _), Is.True);
            Assert.That(registry.TryPromote(human, Human(), out _), Is.True);
            Assert.That(registered, Is.EqualTo(2));
            Assert.That(registry.ReadyCount, Is.EqualTo(2));
            Assert.That(registry.TryGetByClientId(0, out var local), Is.True);
            Assert.That(local, Is.SameAs(human));
            Assert.That(registry.TryGetByParticipantId("bot", out var npc), Is.True);
            Assert.That(npc, Is.SameAs(bot));
            Assert.That(registry.TryCaptureReadyBindings(new[] { Bot(), Human() }, out var ordered, out _), Is.True);
            Assert.That(ordered[0], Is.SameAs(human));
            Assert.That(ordered[1], Is.SameAs(bot));
        }

        [TestCase("seat")]
        [TestCase("participant")]
        [TestCase("connection")]
        [TestCase("generation")]
        public void PromotionRejectsConflictsWithoutChangingTheExistingHuman(string conflict)
        {
            var registry = new PlayerRegistry();
            var human = Binding(1, 0, true);
            var candidate = Binding(2, 0, conflict == "connection");
            registry.RegisterPending(human);
            Assert.That(registry.TryPromote(human, Human(), out _), Is.True);
            registry.RegisterPending(candidate);
            var descriptor = conflict switch
            {
                "seat" => Bot(0),
                "participant" => Bot(id: "human"),
                "connection" => Human(1, 0, id: "other"),
                _ => Bot(generation: 13)
            };
            Assert.That(registry.TryPromote(candidate, descriptor, out _), Is.False);
            Assert.That(registry.TryGetByClientId(0, out var current), Is.True);
            Assert.That(current, Is.SameAs(human));
            Assert.That(registry.ReadyCount, Is.EqualTo(1));
        }

        [TestCase(true, 4UL, true)]
        [TestCase(false, 4UL, false)]
        [TestCase(false, 0UL, true)]
        public void PromotionRejectsMismatchedObjectOwnershipOrPlayerObjectRole(bool isHuman, ulong owner, bool playerObject)
        {
            var registry = new PlayerRegistry();
            var binding = Binding(2, owner, playerObject);
            Assert.That(registry.RegisterPending(binding), Is.True);
            Assert.That(registry.TryPromote(binding, isHuman ? Human() : Bot(), out _), Is.False);
            Assert.That(registry.ReadyCount, Is.Zero);
        }

        [Test]
        public void OldDespawnCannotUnregisterNewObjectReusingTheSeatAndObjectId()
        {
            var registry = new PlayerRegistry();
            var old = Binding(3, 0, true);
            registry.RegisterPending(old);
            registry.TryPromote(old, Human(), out _);
            registry.Unregister(old);
            var replacement = Binding(3, 0, true);
            registry.RegisterPending(replacement);
            Assert.That(registry.TryPromote(replacement, Human(), out _), Is.True);
            registry.Unregister(old);
            Assert.That(registry.TryGetByPlayerIndex(0, out var current), Is.True);
            Assert.That(current, Is.SameAs(replacement));
        }

        [Test]
        public void PendingReadyHandoffCanReconcileIdenticalMetadataWithoutDuplicateEvents()
        {
            var registry = new PlayerRegistry();
            var human = Binding(1, 0, true);
            var bot = Binding(2, 0, false);
            registry.RegisterPending(human); registry.RegisterPending(bot);
            registry.TryPromote(human, Human(), out _); registry.TryPromote(bot, Bot(), out _);
            registry.Unregister(bot);
            Assert.That(registry.RegisterPending(bot), Is.True);
            Assert.That(registry.TryCaptureReadyBindings(new[] { Human(), Bot() }, out _, out _), Is.False);
            int notifications = 0;
            registry.Registered += _ => notifications++;
            Assert.That(registry.TryPromote(bot, Bot(), out _), Is.True);
            Assert.That(registry.TryPromote(bot, Bot(), out _), Is.True);
            Assert.That(notifications, Is.EqualTo(1));
            Assert.That(registry.TryCaptureReadyBindings(new[] { Human(), Bot() }, out _, out _), Is.True);
        }

        [Test]
        public void CaptureRejectsDespawnedObjectsEvenWhenTheReadyCountMatches()
        {
            var registry = new PlayerRegistry();
            var human = Binding(1, 0, true);
            var bot = Binding(2, 0, false);
            registry.RegisterPending(human); registry.RegisterPending(bot);
            registry.TryPromote(human, Human(), out _); registry.TryPromote(bot, Bot(), out _);
            SetNetworkProperty(bot.NetworkObject, nameof(NetworkObject.IsSpawned), false);
            Assert.That(registry.ReadyCount, Is.EqualTo(2));
            Assert.That(registry.TryCaptureReadyBindings(new[] { Human(), Bot() }, out _, out _), Is.False);
            Assert.That(registry.TryCaptureReadyBindings(new[] { Human(generation: 13), Bot(generation: 13) }, out _, out _), Is.False);
        }

        [Test]
        public void LocalBotIsEligibleWithoutTransportAndUsesLiveModifierState()
        {
            var registry = new PlayerRegistry();
            var human = Binding(1, 0, true);
            var bot = Binding(2, 0, false);
            registry.RegisterPending(human); registry.RegisterPending(bot);
            registry.TryPromote(human, Human(), out _); registry.TryPromote(bot, Bot(), out _);
            using var roster = new MatchRoster(2);
            Assert.That(roster.Hydrate(new[] { Human(), Bot() }), Is.True);
            roster.SetRegistry(registry);
            roster.SetModifiersSource(seat => new PlayerModifiers { FanSpeedMultiplier = seat == 1 ? 3f : 1f });
            Assert.That(roster.ConnectedCount, Is.EqualTo(1));
            Assert.That(roster.ActiveCount, Is.EqualTo(2));
            Assert.That(roster.IsConnected(1), Is.False);
            Assert.That(roster.IsParticipantAvailable(1), Is.True);
            Assert.That(roster.IsTurnEligible(1), Is.True);
            Assert.That(roster.IsAutoReady(1), Is.False);
            Assert.That(roster.GetModifiers(1).FanSpeedMultiplier, Is.EqualTo(3f));
            Assert.That(roster.BindReconnect(1, 9), Is.False);
            roster.MarkTimedOut(1);
            Assert.That(roster.IsParticipantAvailable(1), Is.True);
            Assert.Throws<InvalidOperationException>(() => roster.CaptureOfflineState(1, SeatRuntimeState.CreateDefault(1)));
            Assert.That(roster.TryGetOfflineState(1, out _), Is.False);
            registry.Unregister(bot);
            Assert.That(roster.IsParticipantAvailable(1), Is.False);
            Assert.That(roster.IsTurnEligible(1), Is.False);
            Assert.That(roster.IsAutoReady(1), Is.False);
        }

        [Test]
        public void DisconnectedHumanRetainsOnlineAutoReadyAndReconnectSemantics()
        {
            using var roster = new MatchRoster(2);
            var host = new SessionParticipantEntry("host", "token", 0) { IsConnected = true, CurrentClientId = 0 };
            var remote = new SessionParticipantEntry("remote", "token", 1);
            Assert.That(roster.Hydrate(new[] { host, remote }, 8), Is.True);
            Assert.That(roster.IsConnected(1), Is.False);
            Assert.That(roster.IsAutoReady(1), Is.True);
            Assert.That(roster.CountsAsAliveForRoundEnd(1), Is.True);
            Assert.That(roster.BindReconnect(1, 5), Is.True);
            Assert.That(roster.IsAutoReady(1), Is.False);
            Assert.That(roster.TryGetSeatByClientId(5, out var seat), Is.True);
            Assert.That(seat, Is.EqualTo(1));
            Assert.That(roster.TryGetEntry(1, out var entry), Is.True);
            Assert.That(entry.Generation, Is.EqualTo(8));
        }
    }
}
