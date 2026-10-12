using System.Collections.Generic;
using System.Linq;
using AbsoluteZero.Core.Inventory;
using AbsoluteZero.Core.Item;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public sealed class Plan061FixedInventorySlotsTests
    {
        static ItemSlotNetData Copy(uint copy, bool random = true) => new()
            { ItemId = (short)(random ? 1 : 2), CopyId = copy, RemainingUses = 1 };
        static InventoryViewSnapshot Snapshot(IEnumerable<ItemSlotNetData> items, uint round = 1) =>
            new(null, 1, round, 1, items.ToArray());

        [Test]
        public void IncrementalFillToTwelveNeverMovesAnExistingCopy()
        {
            var map = new InventoryPresentationSlots();
            var items = new List<ItemSlotNetData>();
            var previous = new Dictionary<uint, int>();
            for (uint id = 1; id <= 12; id++)
            {
                items.Add(Copy(id, id > 4));
                map.Refresh(Snapshot(items), item => item == 1);
                foreach (var pair in previous) Assert.That(map.Get(pair.Key), Is.EqualTo(pair.Value));
                previous.Add(id, map.Get(id));
                Assert.That(previous.Values.Distinct().Count(), Is.EqualTo(items.Count));
                Assert.That(map.Get(id), Is.InRange(0, 11));
            }
        }

        [Test]
        public void ConsumeCompactAndRefillOnlyOccupiesTheVacatedAnchor()
        {
            var map = new InventoryPresentationSlots();
            map.Refresh(Snapshot(new[] { Copy(10), Copy(11), Copy(12) }), item => true);
            int vacant = map.Get(11), survivor = map.Get(12);
            map.Refresh(Snapshot(new[] { Copy(12), Copy(10) }), item => true);
            Assert.That(map.Get(12), Is.EqualTo(survivor), "Network reordering must not move the picture.");
            Assert.That(map.Get(11), Is.EqualTo(-1));
            map.Refresh(Snapshot(new[] { Copy(12), Copy(10), Copy(13) }), item => true);
            Assert.That(map.Get(13), Is.EqualTo(vacant));
            Assert.That(map.Get(12), Is.EqualTo(survivor));
        }

        [Test]
        public void NewRoundDiscardsOldReservationsAndUsesAuthoredBasicRegion()
        {
            var map = new InventoryPresentationSlots();
            map.Refresh(Snapshot(new[] { Copy(1), Copy(2) }), item => item == 1);
            map.Refresh(Snapshot(new[] { Copy(3, false), Copy(4) }, 2), item => item == 1);
            Assert.That(map.Get(1), Is.EqualTo(-1));
            Assert.That(map.Get(3), Is.EqualTo(8));
            Assert.That(map.Get(4), Is.Zero);
        }

        [Test]
        public void ExtraStolenCopiesUseSpareCapacityWithoutMovingBasicItems()
        {
            var map = new InventoryPresentationSlots();
            var items = new[] { Copy(1, false), Copy(2, false) }.ToList();
            map.Refresh(Snapshot(items), item => item == 1);
            int first = map.Get(1), second = map.Get(2);
            for (uint id = 3; id <= 12; id++) items.Add(Copy(id));
            map.Refresh(Snapshot(items), item => item == 1);
            Assert.That(map.Get(1), Is.EqualTo(first));
            Assert.That(map.Get(2), Is.EqualTo(second));
            Assert.That(items.Select(i => map.Get(i.CopyId)).Distinct().Count(), Is.EqualTo(12));
            Assert.That(items.All(i => map.Get(i.CopyId) >= 0), Is.True);
        }

        [Test]
        public void CoalescedGrantsUseAcquisitionOrderRatherThanReusedNetworkIndex()
        {
            var map = new InventoryPresentationSlots();
            map.Refresh(Snapshot(new[] { Copy(30), Copy(10), Copy(20) }), _ => true);
            Assert.That(map.Get(10), Is.EqualTo(0));
            Assert.That(map.Get(20), Is.EqualTo(1));
            Assert.That(map.Get(30), Is.EqualTo(2));
        }

        [Test]
        public void UseUpdateAndDuplicateItemTypesRetainIndependentAnchors()
        {
            var map = new InventoryPresentationSlots();
            var items = new[] { Copy(1), Copy(2) };
            map.Refresh(Snapshot(items), item => true);
            int a = map.Get(1), b = map.Get(2);
            items[0].RemainingUses = 3;
            map.Refresh(Snapshot(items), item => true);
            Assert.That(map.Get(1), Is.EqualTo(a));
            Assert.That(map.Get(2), Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(b));
        }
    }
}
