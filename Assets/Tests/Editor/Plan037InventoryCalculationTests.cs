using System.Collections.Generic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037InventoryCalculationTests
    {
        readonly List<ScriptableObject> _owned = new();
        Random.State _random;
        [SetUp] public void SetUp() => _random = Random.state;
        [TearDown] public void TearDown()
        {
            Random.state = _random;
            foreach (var asset in _owned) Object.DestroyImmediate(asset);
            _owned.Clear();
        }
        AttackItemDataSO Item(float weight)
        {
            var item = ScriptableObject.CreateInstance<AttackItemDataSO>();
            item.DropWeight = weight; _owned.Add(item); return item;
        }

        [TestCase(37)] [TestCase(2906)] [TestCase(930)]
        public void WeightedAndFilteredRollsPreserveOneDrawAndOriginalBoundaryOrder(int seed)
        {
            var items = new ItemDataSO[] { Item(2f), Item(3f), Item(5f) };
            var table = new ItemDropTable(items);
            Random.InitState(seed);
            for (int i = 0; i < 64; i++)
            {
                var before = Random.state;
                float draw = Random.Range(0f, 10f);
                var after = Random.state;
                var expected = draw <= 2f ? items[0] : draw <= 5f ? items[1] : items[2];
                Random.state = before;
                Assert.That(table.Roll(), Is.SameAs(expected));
                Assert.That(Random.state, Is.EqualTo(after));

                before = Random.state;
                draw = Random.Range(0f, 7f);
                after = Random.state;
                Random.state = before;
                Assert.That(table.Roll(item => item != items[1]), Is.SameAs(draw <= 2f ? items[0] : items[2]));
                Assert.That(Random.state, Is.EqualTo(after));
            }
        }

        [Test]
        public void NoEligibleOrNullPredicateNeverDraws()
        {
            var table = new ItemDropTable(new ItemDataSO[] { Item(3f) });
            var before = Random.state;
            Assert.That(table.Roll(_ => false), Is.Null);
            Assert.That(table.Roll(null), Is.Null);
            Assert.That(Random.state, Is.EqualTo(before));
        }

        [TestCase(0f, 0)] [TestCase(2f, 0)] [TestCase(2.0001f, 1)]
        [TestCase(5f, 1)] [TestCase(5.0001f, 2)] [TestCase(10f, 2)]
        public void ExplicitDrawPreservesInclusiveWeightedBoundary(float draw, int expected)
        {
            var items = new ItemDataSO[] { Item(2f), Item(3f), Item(5f) };
            var before = Random.state;
            Assert.That(new ItemDropTable(items).Select(draw), Is.SameAs(items[expected]));
            Assert.That(Random.state, Is.EqualTo(before));
        }

        [Test]
        public void FilteredDrawUsesRemainingWeightAndDoesNotVisitRemovedEntriesAsCandidates()
        {
            var items = new ItemDataSO[] { Item(2f), Item(3f), Item(5f) };
            var table = new ItemDropTable(items);
            var before = Random.state;
            Assert.That(table.SelectEligible(item => item != items[1], 2f), Is.SameAs(items[0]));
            Assert.That(table.SelectEligible(item => item != items[1], 2.001f), Is.SameAs(items[2]));
            Assert.That(table.SelectEligible(item => item != items[1], 7f), Is.SameAs(items[2]));
            Assert.That(Random.state, Is.EqualTo(before));
        }

        [Test]
        public void FiniteExhaustedCopiesRemainEligibleAndUnlimitedCopiesRemainExcluded()
        {
            var exhausted = new ItemSlotNetData { ItemId = 1, RemainingUses = 0 };
            Assert.That(InventoryMutationCalculations.IsFiniteCopy(exhausted), Is.True);
            Assert.That(InventoryMutationCalculations.CanReroll(exhausted, ItemSlotType.Sub), Is.True);
            Assert.That(InventoryMutationCalculations.CanReroll(exhausted, null), Is.False);
            Assert.That(InventoryMutationCalculations.IsFiniteCopy(ItemSlotNetData.Empty), Is.False);
            exhausted.RemainingUses = 255;
            Assert.That(InventoryMutationCalculations.IsFiniteCopy(exhausted), Is.False);
            Assert.That(InventoryMutationCalculations.CanReroll(exhausted, ItemSlotType.Sub), Is.False);
        }

        [Test]
        public void FullDestinationCanStackButNeverTurnsLimitedUsesIntoUnlimited()
        {
            var limited = new ItemSlotNetData { ItemId = 1, RemainingUses = 250 };
            var stacked = InventoryMutationCalculations.PlaceStolen(limited, 10, 12, 12);
            Assert.That(stacked.Destination, Is.EqualTo(StealDestination.Stack));
            Assert.That(stacked.StackedUses, Is.EqualTo(254));
            limited.RemainingUses = 255;
            Assert.That(InventoryMutationCalculations.PlaceStolen(limited, 10, 12, 12).Destination, Is.EqualTo(StealDestination.None));
            Assert.That(InventoryMutationCalculations.PlaceStolen(limited, 10, 11, 12).Destination, Is.EqualTo(StealDestination.Append));
            Assert.That(InventoryMutationCalculations.PlaceStolen(null, 10, 12, 12).Destination, Is.EqualTo(StealDestination.None));
        }
    }
}
