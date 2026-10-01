using System;
using System.Collections.Generic;
using System.Reflection;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.UI.Game.Bridge;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    // These are binding/observer unit tests. They do not start a transport or replace
    // the separate presentation fixture's real spawn, rendering and ACK checks.
    public sealed class Plan036UiPerspectiveTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<GameObject> _objects = new();
        readonly List<LocalPlayerCommandAdapter> _adapters = new();
        PlayerRegistry _registry;
        MatchCompositionRoot _match;
        NetworkManager _network;
        NetworkManager _originalNetwork;
        MatchCompositionRoot _originalMatch;
        GameDataBridge _bridge;
        Action<PlayerBinding> _registered;
        Action<PlayerIdentity> _unregistered;

        [SetUp]
        public void SetUp()
        {
            _originalNetwork = NetworkManager.Singleton;
            _originalMatch = MatchCompositionRoot.Instance;
            _registry = new PlayerRegistry();
            _network = NewInactive("UI perspective network metadata").AddComponent<NetworkManager>();
            SetProperty(typeof(NetworkManager), null, nameof(NetworkManager.Singleton), _network);
            SetProperty(typeof(NetworkManager), _network, nameof(NetworkManager.IsListening), true);
            SetProperty(typeof(NetworkClient), _network.LocalClient, "IsClient", true);
            _network.LocalClient.ClientId = 0;
            _match = NewInactive("UI perspective composition").AddComponent<MatchCompositionRoot>();
            Set(_match, "_registry", _registry);
            SetProperty(typeof(MatchCompositionRoot), null, nameof(MatchCompositionRoot.Instance), _match);
            _bridge = NewInactive("UI perspective bridge").AddComponent<GameDataBridge>();
            Set(_bridge, "_registry", _registry);
            _registered = binding => Call(_bridge, "OnPlayerRegistered", binding);
            _unregistered = identity => Call(_bridge, "OnPlayerUnregistered", identity);
            _registry.Registered += _registered;
            _registry.Unregistered += _unregistered;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var adapter in _adapters) adapter.Dispose();
            _adapters.Clear();
            _registry.Registered -= _registered;
            _registry.Unregistered -= _unregistered;
            Call(_bridge, "OnDestroy");
            SetProperty(typeof(NetworkManager), _network, nameof(NetworkManager.IsListening), false);
            SetProperty(typeof(NetworkClient), _network.LocalClient, "IsClient", false);
            // Restore the Editor's preexisting singletons before destroying inactive
            // metadata fixtures so test cleanup cannot invalidate the open scene.
            SetProperty(typeof(NetworkManager), null, nameof(NetworkManager.Singleton), _originalNetwork);
            SetProperty(typeof(MatchCompositionRoot), null, nameof(MatchCompositionRoot.Instance), _originalMatch);
            for (int i = _objects.Count - 1; i >= 0; --i)
            {
                var obj = _objects[i];
                var no = obj.GetComponent<NetworkObject>();
                if (no != null) SetProperty(typeof(NetworkObject), no, nameof(NetworkObject.IsSpawned), false);
                obj.GetComponent<PlayerInventory>()?.SlotStates?.Dispose();
                UnityEngine.Object.DestroyImmediate(obj);
            }
            _objects.Clear();
        }

        [Test]
        public void BotFirstNeverClaimsLocalSeatAndLateHumanGetsTheOnlyLocalSnapshot()
        {
            Add("bot", 0, null, 1);
            Assert.That(_bridge.LocalSeatIndex, Is.EqualTo(byte.MaxValue));
            Assert.That(_bridge.TryGetSeat(0, out var bot), Is.True);
            Assert.That(bot.IsLocal, Is.False);
            Assert.That(bot.ClientId, Is.Null);
            Assert.That(bot.ControllerKind, Is.EqualTo(PlayerControllerKind.Bot));
            Add("human", 1, 0, 2);
            Call(_bridge, "FlushSeats");
            Assert.That(_bridge.LocalSeatIndex, Is.EqualTo(1));
            Assert.That(_bridge.TryGetSeat(1, out var human), Is.True);
            Assert.That(human.IsLocal, Is.True);
            Assert.That(human.ClientId, Is.EqualTo((ulong)0));
            Assert.That(_bridge.TryGetSeat(0, out bot), Is.True);
            Assert.That(bot.IsLocal, Is.False);
        }

        [Test]
        public void HumanDespawnAndLateOldUnregisterCannotTransferOrClearLocalControl()
        {
            var human = Add("human", 0, 0, 1);
            Add("bot", 1, null, 2);
            var adapter = Adapter();
            Assert.That(BoundPlayer(adapter), Is.SameAs(human.State));
            _registry.Unregister(human);
            Assert.That(_bridge.LocalSeatIndex, Is.EqualTo(byte.MaxValue));
            Assert.That(BoundPlayer(adapter), Is.Null, "The server-owned bot must not replace the departed human.");
            var replacement = Add("new-human", 0, 0, 3);
            Call(_bridge, "OnPlayerUnregistered", human.Identity);
            _registry.Unregister(human);
            Assert.That(_bridge.LocalSeatIndex, Is.Zero);
            Assert.That(_bridge.TryGetSeat(0, out var snapshot), Is.True);
            Assert.That(snapshot.IsLocal, Is.True);
            Assert.That(BoundPlayer(adapter), Is.SameAs(replacement.State));
        }

        [Test]
        public void OnlineClientSeventyThreeUsesItsSeatRatherThanClientOrCollectionIndex()
        {
            _network.LocalClient.ClientId = 73;
            Add("host", 1, 0, 1);
            var local = Add("remote", 0, 73, 2);
            Add("remote-two", 3, 200, 3);
            Add("remote-three", 2, 201, 4);
            Call(_bridge, "FlushSeats");
            Assert.That(_bridge.LocalSeatIndex, Is.Zero);
            for (byte seat = 0; seat < 4; ++seat)
            {
                Assert.That(_bridge.TryGetSeat(seat, out var snapshot), Is.True);
                Assert.That(snapshot.IsLocal, Is.EqualTo(seat == 0));
            }
            Assert.That(BoundPlayer(Adapter()), Is.SameAs(local.State));
        }

        [Test]
        public void ShutdownInvalidatesBridgeAndQueuedCommandsEvenBeforeDespawn()
        {
            Add("human", 0, 0, 1);
            Add("bot", 1, null, 2);
            var adapter = Adapter();
            SetProperty(typeof(NetworkManager), _network, nameof(NetworkManager.IsListening), false);
            Call(_bridge, "ResolveLocalSeat");
            Call(_bridge, "FlushSeats");
            Assert.That(_bridge.LocalSeatIndex, Is.EqualTo(byte.MaxValue));
            Assert.That(_bridge.TryGetSeat(0, out var oldHuman), Is.True);
            Assert.That(oldHuman.IsLocal, Is.False);
            Assert.That((bool)Call(adapter, "HasCurrentLocalHuman"), Is.False);
            Assert.That(adapter.TryCancelSelection(), Is.False);
            adapter.Dispose();
            Assert.That((bool)Call(adapter, "HasCurrentLocalHuman"), Is.False);
        }

        [Test]
        public void ReplacementMatchCannotReuseTheOldBridgesRegistryOrCommandTarget()
        {
            Add("human", 0, 0, 1);
            var adapter = Adapter();
            var newer = NewInactive("New match").AddComponent<MatchCompositionRoot>();
            Set(newer, "_registry", new PlayerRegistry());
            SetProperty(typeof(MatchCompositionRoot), null, nameof(MatchCompositionRoot.Instance), newer);
            Call(_bridge, "ResolveLocalSeat");
            Assert.That(_bridge.LocalSeatIndex, Is.EqualTo(byte.MaxValue));
            Assert.That((bool)Call(adapter, "HasCurrentLocalHuman"), Is.False);
        }

        [Test]
        public void ResultsWaitForTheLocalHumanInsteadOfUsingUnresolvedSeat255()
        {
            Add("bot", 1, null, 1);
            Set(_bridge, "_roundResultPending", true);
            Set(_bridge, "_matchEndPending", true);
            Set(_bridge, "_roundResultTimer", 1f);
            Set(_bridge, "_matchEndTimer", 1f);
            int rounds = 0, matches = 0;
            _bridge.OnRoundResult += _ => rounds++;
            _bridge.OnMatchEnd += _ => matches++;
            Call(_bridge, "ProcessRoundResult"); Call(_bridge, "ProcessMatchEnd");
            Assert.That(rounds, Is.Zero); Assert.That(matches, Is.Zero);
            Add("human", 0, 0, 2);
            Call(_bridge, "ProcessRoundResult"); Call(_bridge, "ProcessMatchEnd");
            Call(_bridge, "ProcessRoundResult"); Call(_bridge, "ProcessMatchEnd");
            Assert.That(rounds, Is.EqualTo(1)); Assert.That(matches, Is.EqualTo(1));
        }

        [Test]
        public void BotCosmeticsUseTheSameFivePartWireFormatWithoutReadingHumanProfile()
        {
            Assert.That(PlayerState.TrySerializeBotCosmetics(new BotCosmeticIds
                { Head = "hat_01", Top = "top_01", Back = "back_01", Bottom = "bottom_01", Tail = "tail_01" }, out var wire), Is.True);
            var dto = JsonUtility.FromJson<CosmeticDto>(wire.ToString());
            Assert.That(dto.v, Is.EqualTo(1));
            Assert.That(dto.head, Is.EqualTo("hat_01"));
            Assert.That(dto.top, Is.EqualTo("top_01"));
            Assert.That(dto.back, Is.EqualTo("back_01"));
            Assert.That(dto.bottom, Is.EqualTo("bottom_01"));
            Assert.That(dto.tail, Is.EqualTo("tail_01"));
            Assert.That(PlayerState.TrySerializeBotCosmetics(default, out var empty), Is.True);
            Assert.That(JsonUtility.FromJson<CosmeticDto>(empty.ToString()).head, Is.Empty);
        }

        [Test]
        public void BotCosmeticWireLimitCountsUtf8BytesAndRejectsBeforeFixedStringConstruction()
        {
            Assert.DoesNotThrow(() =>
            {
                Assert.That(PlayerState.TrySerializeBotCosmetics(new BotCosmeticIds { Head = new string('가', 43) }, out var rejected), Is.False);
                Assert.That(rejected.Length, Is.Zero);
            });
        }

        GameObject NewInactive(string name)
        {
            var obj = new GameObject(name);
            obj.SetActive(false);
            _objects.Add(obj);
            return obj;
        }

        PlayerBinding Add(string id, byte seat, ulong? client, ulong objectId)
        {
            var obj = NewInactive(id);
            var network = obj.AddComponent<NetworkObject>();
            var inventory = obj.AddComponent<PlayerInventory>();
            var state = obj.AddComponent<PlayerState>();
            SetProperty(typeof(NetworkObject), network, nameof(NetworkObject.NetworkObjectId), objectId);
            SetProperty(typeof(NetworkObject), network, nameof(NetworkObject.OwnerClientId), client ?? 0);
            SetProperty(typeof(NetworkObject), network, nameof(NetworkObject.IsPlayerObject), client.HasValue);
            SetProperty(typeof(NetworkObject), network, nameof(NetworkObject.IsSpawned), true);
            var binding = new PlayerBinding(state, inventory, network);
            Assert.That(_registry.RegisterPending(binding), Is.True);
            Assert.That(_registry.TryPromote(binding, new MatchParticipantDescriptor(id, seat,
                client.HasValue ? PlayerControllerKind.Human : PlayerControllerKind.Bot, client, 42), out var error), Is.True, error);
            return binding;
        }

        LocalPlayerCommandAdapter Adapter()
        {
            var adapter = new LocalPlayerCommandAdapter(_registry);
            _adapters.Add(adapter);
            return adapter;
        }
        static PlayerState BoundPlayer(LocalPlayerCommandAdapter adapter)
            => (PlayerState)typeof(LocalPlayerCommandAdapter).GetField("_localPlayer", PrivateInstance).GetValue(adapter);
        static void Set(object target, string name, object value)
            => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
        static object Call(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, PrivateInstance).Invoke(target, args);
        static void SetProperty(Type type, object target, string name, object value)
            => type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).SetValue(target, value);
    }
}
