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

        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
        [TestCase(3, false)] [TestCase(4, false)]
        public void GenerationStopsAtThreePhysicalCopies(int count, bool eligible)
        {
            var slots = new List<ItemSlotNetData> { ItemSlotNetData.Empty };
            for (int i = 0; i < count; i++) slots.Add(new ItemSlotNetData { ItemId = 2, RemainingUses = 1 });
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(2, false, slots), Is.EqualTo(eligible));
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(-1, false, slots), Is.False);
        }

        [Test]
        public void RerollExcludesOnlyItsOwnSlotAndCountsPendingReplacements()
        {
            var slots = new[] {
                new ItemSlotNetData { ItemId = 2, RemainingUses = 1 },
                new ItemSlotNetData { ItemId = 2, RemainingUses = 1 },
                new ItemSlotNetData { ItemId = 2, RemainingUses = 1 },
                new ItemSlotNetData { ItemId = 3, RemainingUses = 1 } };
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(2, false, slots, 0), Is.True);
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(2, false, slots, 3), Is.False);
            slots[0].ItemId = 3;
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(2, false, slots, 3), Is.True);
            slots[3].ItemId = 2;
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(2, false, slots, 0), Is.False);
        }

        [Test]
        public void ExistingUnlimitedCannotGenerateADuplicateAndExhaustedCopiesStillCount()
        {
            var slots = new[] { new ItemSlotNetData { ItemId = 2, RemainingUses = 255 } };
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(2, false, slots), Is.False);
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(2, true, slots), Is.False);
            slots[0].RemainingUses = 0;
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(2, true, slots), Is.False);
            Assert.That(InventoryMutationCalculations.CanGenerateCopy(2, false, new[] { slots[0], slots[0], slots[0] }), Is.False);
        }

        [Test]
        public void DominantCappedCandidateCannotStarveLegalDrop()
        {
            var capped = Item(1000000f); var legal = Item(1f);
            var slots = new[] { new ItemSlotNetData { ItemId = 0, RemainingUses = 1 },
                new ItemSlotNetData { ItemId = 0, RemainingUses = 1 },
                new ItemSlotNetData { ItemId = 0, RemainingUses = 1 } };
            var table = new ItemDropTable(new ItemDataSO[] { capped, legal });
            for (int i = 0; i < 32; i++)
                Assert.That(table.Roll(item => InventoryMutationCalculations.CanGenerateCopy(
                    (short)(item == capped ? 0 : 1), false, slots)), Is.SameAs(legal));
            var before = Random.state;
            Assert.That(new ItemDropTable(new ItemDataSO[] { capped }).Roll(item =>
                InventoryMutationCalculations.CanGenerateCopy(0, false, slots)), Is.Null);
            Assert.That(Random.state, Is.EqualTo(before));
        }

        [TestCase(12, -1, -1)]
        [TestCase(12, 5, 5)]
        [TestCase(11, -1, 11)]
        [TestCase(4, 1, 1)]
        [TestCase(0, -1, 0)]
        public void NewCopyUsesItsOwnSlotAndNeverOverwritesAnOccupiedSlot(int count, int empty, int expected)
        {
            Assert.That(InventoryMutationCalculations.SeparateCopySlot(count, empty, 12), Is.EqualTo(expected));
        }
    }
}
