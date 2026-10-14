using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace AbsoluteZero.Tests
{
    // Isolated EditMode production-method regression; no transport is started.
    // IsServer is set only on these unspawned inventories so real grant/consume
    // methods execute. Remote replication remains a separate runtime check.
    public sealed class PrepTurnItemCapacityTests
    {
        readonly List<GameObject> _objects = new();
        readonly List<NetworkList<ItemSlotNetData>> _lists = new();
        ItemDataSO[] _items;
        ItemDropTable _table;
        Random.State _random;

        [SetUp] public void SetUp()
        {
            _random = Random.state;
            _items = new ItemDataSO[8];
            for (int i = 0; i < _items.Length; i++)
            {
                var item = ScriptableObject.CreateInstance<AttackItemDataSO>();
                item.ItemName = "Capacity fixture " + i;
                item.Persistence = i < 2 ? ItemPersistence.Permanent
                    : i < 4 ? ItemPersistence.BasicConsumable : ItemPersistence.RandomConsumable;
                item.SlotType = i < 4 ? ItemSlotType.Main : ItemSlotType.Sub;
                item.MaxUses = i < 2 ? 0 : 1;
                item.DropWeight = i < 4 ? 0 : 1;
                _items[i] = item;
            }
            _table = new ItemDropTable(_items);
        }

        [TearDown] public void TearDown()
        {
            foreach (var go in _objects) Object.DestroyImmediate(go);
            foreach (var list in _lists) list.Dispose();
            foreach (var item in _items) Object.DestroyImmediate(item);
            _objects.Clear(); _lists.Clear(); Random.state = _random;
        }

        static void SetInventoryAuthority(PlayerInventory inventory, bool server)
            => typeof(NetworkBehaviour).GetProperty(nameof(NetworkBehaviour.IsServer))
                .GetSetMethod(true).Invoke(inventory, new object[] { server });

        static IEnumerable<ItemSlotNetData> Copies(PlayerInventory inventory)
        {
            foreach (var copy in inventory.SlotStates) yield return copy;
        }

        PlayerState Player(byte seat, PlayerControllerKind kind)
        {
            var go = new GameObject("Capacity " + kind + " seat " + seat);
            go.SetActive(false); _objects.Add(go);
            var inventory = go.AddComponent<PlayerInventory>();
            inventory.SlotStates = new NetworkList<ItemSlotNetData>(); _lists.Add(inventory.SlotStates);
            SetInventoryAuthority(inventory, true);
            inventory.Initialize(_items);
            Assert.That(inventory.InitializeBasicItems(0, 1, 2, 3, true), Is.True);
            var player = go.AddComponent<PlayerState>();
            player.SyncedPlayerIndex.Value = seat;
            player.ParticipantMetadata.Value = new ParticipantMetadataNetData(new MatchParticipantDescriptor(
                "capacity-" + seat, seat, kind, kind == PlayerControllerKind.Human ? (ulong?)seat : null, 1));
            return player;
        }

        PlayerState[] Duel(PlayerControllerKind opponent = PlayerControllerKind.Bot)
        {
            var players = new[] { Player(0, PlayerControllerKind.Human), Player(1, opponent) };
            // The ordinary Solo/Duel round lifecycle intentionally starts with random4.
            new RoundLifecycleService().GrantStartingItems(players[0], players[1], _table);
            foreach (var p in players)
            {
                Assert.That(p.GetInventory().GetRandomSlotCount(), Is.EqualTo(4));
                Assert.That(p.GetInventory().SlotStates.Count, Is.EqualTo(8));
            }
            return players;
        }

        static void ConsumeBasicSlots(PlayerState[] players)
        {
            foreach (var p in players)
            {
                var inventory = p.GetInventory();
                inventory.ConsumeItem(2); inventory.ConsumeItem(3); inventory.CompactSlots();
                Assert.That(inventory.GetRandomSlotCount(), Is.EqualTo(4));
                Assert.That(inventory.SlotStates.Count, Is.EqualTo(6));
            }
        }

        void TickAt(PlayerState[] players, float temperature, int capacity = 8)
        {
            foreach (var p in players) p.Temperature.Value = temperature;
            PrepTurnOperations.ApplyDuelTick(players, new TemperatureSystem(), true, 0f, _table, capacity);
        }

        [TestCase(PlayerControllerKind.Human)] [TestCase(PlayerControllerKind.Bot)]
        public void ProductionDuelTickCapsBothSeatsAfterConsumedBasicsAndKeepsExistingCopies(PlayerControllerKind opponent)
        {
            var players = Duel(opponent); ConsumeBasicSlots(players);
            var existing = players.Select(p => Copies(p.GetInventory()).ToArray()).ToArray();
            var temperatures = new[] { 30f, 20f, 10f };
            var counts = new[] { 5, 7, 8 };
            for (int step = 0; step < temperatures.Length; step++)
            {
                TickAt(players, temperatures[step]);
                for (int seat = 0; seat < players.Length; seat++)
                {
                    var inventory = players[seat].GetInventory();
                    Assert.That(inventory.GetRandomSlotCount(), Is.EqualTo(counts[step]), "seat " + seat);
                    Assert.That(inventory.SlotStates.Count, Is.EqualTo(counts[step] + 2));
                    Assert.That(inventory.GetThresholdGranted(), Is.EqualTo(Enumerable.Range(0, 3).Select(i => i <= step)));
                    foreach (var copy in existing[seat])
                        Assert.That(Copies(inventory).Any(current => current.Equals(copy)), Is.True, "Existing copy changed");
                }
            }
        }

        [TestCase(6)] [TestCase(8)]
        public void CrossingSeveralThresholdsUsesSuppliedCapAndNeverQueuesMissedRewards(int capacity)
        {
            var players = Duel(); ConsumeBasicSlots(players); TickAt(players, 10f, capacity);
            foreach (var p in players)
            {
                var inventory = p.GetInventory();
                Assert.That(inventory.GetRandomSlotCount(), Is.EqualTo(capacity));
                Assert.That(inventory.GetThresholdGranted(), Is.All.True);
                inventory.ConsumeItem(2); // A random copy, after the two surviving basic slots.
            }
            var fingerprints = players.Select(p => p.GetInventory().CurrentFingerprint()).ToArray();
            var before = Random.state;
            TickAt(players, 10f, capacity); TickAt(players, 37f, capacity); TickAt(players, 10f, capacity);
            Assert.That(players.Select(p => p.GetInventory().CurrentFingerprint()), Is.EqualTo(fingerprints));
            Assert.That(players.Select(p => p.GetInventory().GetRandomSlotCount()), Is.All.EqualTo(capacity - 1));
            Assert.That(Random.state, Is.EqualTo(before), "Claimed thresholds must not draw again");
        }

        [Test]
        public void NextRoundRestoresBasicsInitialFourAndFreshThresholdEligibility()
        {
            var players = Duel(); ConsumeBasicSlots(players); TickAt(players, 10f);
            var lifecycle = new RoundLifecycleService();
            lifecycle.ResetPlayersForNewRound(players[0], players[1]);
            lifecycle.GrantStartingItems(players[0], players[1], _table);
            foreach (var p in players)
            {
                Assert.That(p.Temperature.Value, Is.EqualTo(37f));
                Assert.That(p.GetInventory().GetRandomSlotCount(), Is.EqualTo(4));
                Assert.That(p.GetInventory().SlotStates.Count, Is.EqualTo(8));
                Assert.That(p.GetInventory().GetThresholdGranted(), Is.All.False);
            }
            TickAt(players, 30f);
            Assert.That(players.Select(p => p.GetInventory().GetRandomSlotCount()), Is.All.EqualTo(5));
        }

        [TestCase(false)] [TestCase(true)]
        public void LethalFanTickStillCapsRewardsAndPreservesExistingDeathWinner(bool bothDie)
        {
            var players = Duel(); ConsumeBasicSlots(players);
            players[0].Temperature.Value = 1f; players[0].FanSpeed.Value = 2f; players[0].IsFanActive.Value = true;
            if (bothDie)
            { players[1].Temperature.Value = 1f; players[1].FanSpeed.Value = 2f; players[1].IsFanActive.Value = true; }
            var temperature = new TemperatureSystem();
            PrepTurnOperations.ApplyDuelTick(players, temperature, false, 0f, _table, 8);
            Assert.That(players[0].Temperature.Value, Is.Zero);
            Assert.That(players[0].GetInventory().GetRandomSlotCount(), Is.EqualTo(8));
            Assert.That(players[1].GetInventory().GetRandomSlotCount(), Is.EqualTo(bothDie ? 8 : 4));
            Assert.That(new RoundLifecycleService().DetermineDeathWinner(temperature, players[0], players[1]),
                Is.EqualTo(bothDie ? -1 : 1));
        }

        [Test]
        public void FanSkipAndRecoveryStillHappenBeforeThresholdEvaluation()
        {
            var players = Duel();
            players[0].Temperature.Value = 31f; players[0].FanSpeed.Value = 2f; players[0].IsFanActive.Value = true;
            players[1].Temperature.Value = 30f; players[1].IsReady.Value = true;
            PrepTurnOperations.ApplyDuelTick(players, new TemperatureSystem(), true, 1f, _table, 8);
            Assert.That(players.Select(p => p.Temperature.Value), Is.EqualTo(new[] { 31f, 31f }));
            Assert.That(players.Select(p => p.GetInventory().GetRandomSlotCount()), Is.All.EqualTo(4));
            PrepTurnOperations.ApplyDuelTick(players, new TemperatureSystem(), false, 1f, _table, 8);
            Assert.That(players.Select(p => p.Temperature.Value), Is.EqualTo(new[] { 29f, 32f }));
            Assert.That(players.Select(p => p.GetInventory().GetRandomSlotCount()), Is.EqualTo(new[] { 5, 4 }));
        }

        [Test]
        public void UnspawnedClientInventoryCannotGrantEvenWhenThresholdIsReached()
        {
            var players = Duel();
            foreach (var p in players) SetInventoryAuthority(p.GetInventory(), false);
            var fingerprints = players.Select(p => p.GetInventory().CurrentFingerprint()).ToArray();
            var before = Random.state;
            TickAt(players, 10f);
            Assert.That(players.Select(p => p.GetInventory().CurrentFingerprint()), Is.EqualTo(fingerprints));
            Assert.That(Random.state, Is.EqualTo(before));
        }

        [Test]
        public void MultiKeepsOnePerThresholdCapFourAndExcludesGhost()
        {
            var players = Enumerable.Range(0, 4).Select(i => Player((byte)i, PlayerControllerKind.Human)).ToArray();
            new RoundLifecycleService().GrantStartingItems(players, _table, 2, 4);
            players[3].CurrentLifeState.Value = LifeState.Ghost;
            var temperatures = new[] { 30f, 20f, 10f };
            var counts = new[] { 3, 4, 4 };
            for (int step = 0; step < temperatures.Length; step++)
            {
                foreach (var p in players) p.Temperature.Value = temperatures[step];
                PrepTurnOperations.GrantMultiThresholds(players, new TemperatureSystem(), _table, 4);
                Assert.That(players.Take(3).Select(p => p.GetInventory().GetRandomSlotCount()), Is.All.EqualTo(counts[step]));
                Assert.That(players[3].GetInventory().GetRandomSlotCount(), Is.EqualTo(2));
                Assert.That(players[3].GetInventory().GetThresholdGranted(), Is.All.False);
            }
        }
    }
}
