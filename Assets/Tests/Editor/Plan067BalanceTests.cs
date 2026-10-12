using System;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public class Plan067BalanceTests
    {
        [TestCase(1, 0, -5, 2, 2)]
        [TestCase(36, 37, 5, 0, 4)]
        [TestCase(20, 20, -2, 2, 0)]
        [TestCase(20, 17, -3, 0, 0)]
        public void ImpactSeparatesMitigationFromClipping(float before, float after, float requested, float blocked, float clipped)
        {
            var value = PlaytestTelemetry.Impact(default, 1, before, after, requested, blocked);
            Assert.That(value.mitigated, Is.EqualTo(blocked));
            Assert.That(value.clipped, Is.EqualTo(clipped));
            Assert.That(Math.Abs(requested), Is.EqualTo(Math.Abs(after-before)+value.mitigated+value.clipped));
        }
        [Test]
        public void DelayedIdentitySurvivesSerialization()
        {
            var value = new BalanceObservation { action = new BalanceActionIdentity { id = 9, source = 2, target = 1, item = 6, copy = 23 }, effect = 44, reason = "scheduled" };
            var copy = JsonUtility.FromJson<BalanceObservation>(JsonUtility.ToJson(value));
            Assert.That(copy.action.copy, Is.EqualTo(23)); Assert.That(copy.action.id, Is.EqualTo(9));
            Assert.That(copy.effect, Is.EqualTo(44)); Assert.That(copy.action.source, Is.EqualTo(2));
        }
        [TestCase(0, 3, 0)]
        [TestCase(2, 1, 2)]
        [TestCase(99, 0, 3)]
        public void MultiMetricsComeFromActualDamageBranch(float block, float damage, float prevented)
        {
            var item = ScriptableObject.CreateInstance<AttackItemDataSO>();
            try
            {
                item.Damage = 3; item.AttackFilter = DamageFilter.Temperature;
                var mods = new PlayerModifiers[2];
                mods[1].ActiveDefense = new DefenseInfo { ItemId = 20, Filter = DamageFilter.All, BlockAmount = block };
                var temps = new[] {20f,20f};
                var snap = new MatchCombatSnapshot(temps, temps, mods, new[] {LifeState.Alive,LifeState.Alive},
                    new[] {new InventorySnapshot(0,new[]{new SlotSnapshot(10,false,1)}),new InventorySnapshot(1,Array.Empty<SlotSnapshot>())},
                    Array.Empty<ScheduledEffectSnapshot>(),new int[2],new[]{true,true},EnvironmentType.None,default,
                    new[]{ItemEffectRuleSnapshot.From(item,10)});
                var result = new CombatResolver().ResolveMultiAction(snap,new ActionIntent(0,0,10,1,1));
                Assert.That(result.TemperatureDeltas[1],Is.EqualTo(-damage));
                Assert.That(result.BalanceImpacts[0].mitigated,Is.EqualTo(prevented));
                Assert.That(result.BalanceImpacts[0].after,Is.EqualTo(20-damage));
                Assert.That(snap.CurrentTemperatures[1],Is.EqualTo(20));
            }
            finally { UnityEngine.Object.DestroyImmediate(item); }
        }
    }
}
