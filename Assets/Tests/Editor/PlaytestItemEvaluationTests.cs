using System;
using System.Linq;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Solo;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    [Category("SoloIntegration")]
    public sealed class PlaytestItemEvaluationTests
    {
        static ItemDataSO[] Items() => AssetDatabase.FindAssets("t:ItemDataSO", new[] { "Assets/Data/Items" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<ItemDataSO>).Where(ItemAvailability.IsEnabled).ToArray();

        [Test]
        public void EveryEnabledItemAndRecoveryUsePreservesFrozenEstimateAndInputs()
        {
            var items = Items(); Assert.That(items.Length, Is.GreaterThan(0));
            int comparisons = 0;
            foreach (var item in items)
            {
                var before = JsonUtility.ToJson(item);
                for (int use = 0; use < Math.Max(1, item.MaxUses); use++)
                foreach (float own in new[] { 0f, 1f, 10f, 35f, 37f })
                foreach (float other in new[] { 1f, 20f, 37f })
                {
                    var slot = new ItemSlotNetData { CopyId = 81, ItemId = 3,
                        RemainingUses = item.MaxUses > 0 ? (byte)(item.MaxUses - use) : (byte)255 };
                    var original = slot;
                    var rule = ItemEffectRuleSnapshot.From(item, slot.ItemId);
                    bool permanent = item.Persistence == ItemPersistence.Permanent;
                    var expected = FrozenPreIntegrationItemEvaluation.Estimate(rule, slot, 1, 1.5, own, other, permanent);
                    var actual = PlaytestItemEvaluation.Estimate(rule, slot, 1, 1.5, own, other, permanent);
                    Assert.AreEqual(expected, actual, item.name + " use=" + use + " own=" + own + " other=" + other);
                    Assert.AreEqual(original, slot); comparisons++;
                }
                Assert.AreEqual(before, JsonUtility.ToJson(item), item.name + " mutated");
            }
            TestContext.WriteLine("Frozen estimator comparisons: " + comparisons);
        }

        [TestCase("balanced", false)] [TestCase("attacker", false)]
        [TestCase("survival", false)] [TestCase("resource", false)]
        [TestCase("utility", false)] [TestCase("cautious", false)]
        [TestCase("balanced", true)] [TestCase("utility", true)]
        public void PoliciesPreserveSelectedCopyTargetAndRandomStream(string policy, bool media)
        {
            var items = Items();
            var oldRandom = new System.Random(723420); var newRandom = new System.Random(723420);
            for (int turn = 0; turn < 24; turn++)
            {
                // Same target tie draw and candidate order as the pre-integration driver.
                int oldTarget = oldRandom.Next(3), newTarget = newRandom.Next(3);
                Assert.AreEqual(oldTarget, newTarget);
                double oldBest = double.NegativeInfinity, newBest = double.NegativeInfinity;
                uint oldCopy = 0, newCopy = 0;
                for (short i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    var slot = new ItemSlotNetData { CopyId = (uint)(100 + i), ItemId = i,
                        RemainingUses = item.MaxUses > 0 ? (byte)item.MaxUses : (byte)255 };
                    var rule = ItemEffectRuleSnapshot.From(item, i);
                    bool permanent = item.Persistence == ItemPersistence.Permanent;
                    var oldValue = FrozenPreIntegrationItemEvaluation.Estimate(rule, slot, (byte)oldTarget, 0, turn + 10, 25, permanent);
                    var newValue = PlaytestItemEvaluation.Estimate(rule, slot, (byte)newTarget, 0, turn + 10, 25, permanent);
                    double bias = media ? -(turn % 3) * 100 + (permanent ? 0 : 30) : 0;
                    double a = Policy(oldValue, policy) + bias + oldRandom.NextDouble() * .2;
                    double b = Policy(newValue, policy) + bias + newRandom.NextDouble() * .2;
                    if (a > oldBest) { oldBest = a; oldCopy = slot.CopyId; }
                    if (b > newBest) { newBest = b; newCopy = slot.CopyId; }
                }
                Assert.AreEqual(oldBest, newBest); Assert.AreEqual(oldCopy, newCopy);
                Assert.AreEqual(oldRandom.Next(), newRandom.Next(), "RNG position after selection");
            }
        }

        // Frozen driver policy equations; production driver is verified separately to have only its callee changed.
        static double Policy(PlaytestItemEstimate e, string policy) => policy switch
        {
            "attacker" => e.Damage * 2 + e.Heal * .3 + e.Utility * .2,
            "survival" => e.Heal * 2 + e.Defense + e.Damage * .5,
            "resource" => e.Damage + e.Heal + (e.Permanent ? 4 : 0),
            "utility" => e.Utility * 3 + e.Heal + e.Damage * .5,
            _ => e.Damage + e.Heal + e.Utility + e.Defense * .2
        };
    }
}
