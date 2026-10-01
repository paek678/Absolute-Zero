using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan036BotDecisionTests
    {
        static readonly PrepInputKey Key = new(7, 2, 3, 4);
        static readonly BotCandidate Attack = new(1, 0, 0, 1, 3, 0, 0, 0, true);
        static readonly BotCandidate Heal = new(2, 1, 1, 1, 0, 7, 0, 0, false);
        static BotObservation Observe(float own = 30, float opponent = 30, double remaining = 10,
            IList<BotCandidate> candidates = null, float opponentFan = 1)
            => new(Key, 1, 0, own, opponent, 1, opponentFan, false, remaining, candidates ?? new[] { Attack, Heal });
        static bool Choose(BotObservation observation, BotTactic tactic, out BotCandidate candidate)
            => BotDecisionModel.TryChoose(observation, tactic, BotTacticalWeights.Neutral, 0, new System.Random(1), 0.25, out candidate);
        [Test] public void LowTemperatureSelectsHealingSurvival()
        { Assert.True(Choose(Observe(10), BotTactic.Survival, out var candidate)); Assert.AreEqual(2u, candidate.Copy); }
        [Test] public void SurvivalDoesNotMaskAnOrdinaryAttackAtFullTemperature()
        { Assert.False(Choose(Observe(37), BotTactic.Survival, out _)); }
        [Test] public void FinishRequiresEstimatedLethalDamage()
        { Assert.True(Choose(Observe(opponent: 3), BotTactic.Finish, out var c)); Assert.AreEqual(1u, c.Copy); Assert.False(Choose(Observe(opponent: 4), BotTactic.Finish, out _)); }
        [Test] public void ResourcePreservesFiniteItemsWhenBasicAttackIsEnough()
        { Assert.True(Choose(Observe(), BotTactic.Resource, out var c)); Assert.AreEqual(1u, c.Copy); }
        [Test] public void CounterUsesPublicPressureAndOwnDefenseOnly()
        {
            var defense = new BotCandidate(3, 1, 1, 1, 0, 0, 10, 0, true);
            var o = Observe(18, candidates: new[] { Attack, defense }, opponentFan: 2);
            Assert.True(Choose(o, BotTactic.Counter, out var selected)); Assert.AreEqual(3u, selected.Copy);
            Assert.False(Choose(Observe(18, candidates: new[] { defense }), BotTactic.Counter, out _));
        }
        [Test] public void GeneralSelectsLegalUtilityWithoutInventingDamage()
        {
            var utility = new BotCandidate(4, 0, 10, .8, 0, 0, 0, 2, false);
            Assert.True(Choose(Observe(candidates: new[] { utility }), BotTactic.General, out var c));
            Assert.AreEqual(4u, c.Copy); Assert.AreEqual(0, c.Damage);
        }
        [Test] public void IndependentBotEvaluationCannotChangeAnotherBotsDecisionStream()
        {
            var control = new System.Random(22); var subject = new System.Random(22); var unrelated = new System.Random(23);
            for (int i = 0; i < 40; i++)
            {
                BotDecisionModel.TryChoose(Observe(), BotTactic.General, BotTacticalWeights.Neutral, 1, control, .25, out var a);
                for (int extra = 0; extra < i; extra++)
                    BotDecisionModel.TryChoose(Observe(), BotTactic.General, BotTacticalWeights.Neutral, 1, unrelated, .25, out _);
                BotDecisionModel.TryChoose(Observe(), BotTactic.General, BotTacticalWeights.Neutral, 1, subject, .25, out var b);
                Assert.AreEqual(a.Copy, b.Copy);
            }
        }
        [TestCase(10f, 10f, 20f, 25f, EnvironmentType.None, 0)]
        [TestCase(10f, 10f, 25f, 20f, EnvironmentType.None, 1)]
        [TestCase(10f, 10f, 25f, 25f, EnvironmentType.None, 0)]
        [TestCase(9f, 10f, 25f, 20f, EnvironmentType.None, 0)]
        [TestCase(9f, 10f, 25f, 20f, EnvironmentType.HeatWaveWarning, 1)]
        public void SoloReusesDuelReadyTemperatureAndSeatOrdering(float ready0, float ready1,
            float temp0, float temp1, EnvironmentType environment, int first)
        {
            var objects = new[] { new GameObject("duel order 0"), new GameObject("duel order 1") };
            try
            {
                foreach (var go in objects) { go.SetActive(false); go.AddComponent<Unity.Netcode.NetworkObject>(); }
                var p0 = objects[0].AddComponent<PlayerState>(); var p1 = objects[1].AddComponent<PlayerState>();
                p0.Temperature.Value = temp0; p1.Temperature.Value = temp1;
                var q0 = new ActionQueue(); var q1 = new ActionQueue(); q0.SetReady(ready0); q1.SetReady(ready1);
                var result = new CombatResolver().Resolve(q0, q1, new PlayerModifiers[2], p0, p1, new TemperatureSystem(), null, environment);
                Assert.AreEqual(first, result.FirstPlayerIndex);
                Assert.AreEqual(temp0, p0.Temperature.Value); Assert.AreEqual(temp1, p1.Temperature.Value);
            }
            finally { foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go); }
        }
        [TestCase(1.30)] [TestCase(1.29)]
        public void DelayReserveAndDeadlineDoNotFitAtEquality(double remaining)
        { Assert.False(Choose(Observe(remaining: remaining), BotTactic.General, out _)); }
        [Test] public void EmptyInventoryYieldsNoCandidateForFallback()
        { Assert.False(Choose(Observe(candidates: Array.Empty<BotCandidate>()), BotTactic.General, out _)); }
        [Test] public void DisabledWeightSkipsBranch()
        {
            var weights = BotTacticalWeights.Neutral; weights.Survival = 0;
            Assert.False(BotDecisionModel.TryChoose(Observe(5), BotTactic.Survival, weights, 0, new System.Random(1), 0.25, out _));
        }
        [Test] public void ObservationCopiesCandidateCollection()
        { var list = new List<BotCandidate> { Attack }; var o = Observe(candidates: list); list.Clear(); Assert.AreEqual(1, o.Candidates.Count); }
        [Test] public void FixedSeedReplayIsDeterministicWithoutAdvancingUnityRandom()
        {
            var saved = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(17); var baseline = UnityEngine.Random.state;
                int seed = BotDecisionModel.Seed("default", Key);
                var a = new System.Random(seed); var b = new System.Random(seed);
                for (int i = 0; i < 30; i++)
                {
                    Assert.True(BotDecisionModel.TryChoose(Observe(), BotTactic.General, BotTacticalWeights.Neutral, 8, a, .25, out var x));
                    Assert.True(BotDecisionModel.TryChoose(Observe(), BotTactic.General, BotTacticalWeights.Neutral, 8, b, .25, out var y));
                    Assert.AreEqual(x.Copy, y.Copy);
                }
                Assert.AreEqual(JsonUtility.ToJson(baseline), JsonUtility.ToJson(UnityEngine.Random.state));
                Assert.AreNotEqual(seed, BotDecisionModel.Seed("default", new PrepInputKey(7, 2, 4, 5)));
            }
            finally { UnityEngine.Random.state = saved; }
        }
        [Test] public void RecoveryEstimateUsesCurrentUseAndClampsOverhealWithoutEffects()
        {
            var item = ScriptableObject.CreateInstance<RecoveryItemDataSO>();
            try
            {
                item.MaxUses = 3; item.HealPerUse = new float[] { 2, 5, 9 };
                var slot = new ItemSlotNetData { CopyId = 9, RemainingUses = 2, ItemId = 0 };
                var c = BotDecisionModel.Estimate(ItemEffectRuleSnapshot.From(item), slot, 1, 1, 35, 30, false);
                Assert.AreEqual(2, c.Heal); Assert.AreEqual(2, slot.RemainingUses); Assert.AreEqual(5, item.HealPerUse[1]);
            }
            finally { UnityEngine.Object.DestroyImmediate(item); }
        }
        [Test] public void NeutralizeIsAUsableUtilityCandidateWithoutInventingDamage()
        {
            var item = ScriptableObject.CreateInstance<SabotageItemDataSO>();
            try
            {
                item.SabotageType = SabotageType.Neutralize;
                var estimate = BotDecisionModel.Estimate(ItemEffectRuleSnapshot.From(item),
                    new ItemSlotNetData { CopyId = 8, RemainingUses = 1 }, 0, 1.5, 30, 30, false);
                Assert.That(estimate.Damage, Is.Zero);
                Assert.That(estimate.Utility, Is.GreaterThan(0));
                Assert.True(Choose(Observe(candidates: new[] { estimate }), BotTactic.General, out var chosen));
                Assert.AreEqual(8u, chosen.Copy);
            }
            finally { UnityEngine.Object.DestroyImmediate(item); }
        }
        [Test] public void EqualizeEstimateCannotCreateDamageAgainstColderOpponent()
        {
            var item = ScriptableObject.CreateInstance<AttackItemDataSO>();
            try
            {
                item.EqualizeToUserTemp = true;
                var slot = new ItemSlotNetData { CopyId = 1, RemainingUses = 1 };
                Assert.AreEqual(0, BotDecisionModel.Estimate(ItemEffectRuleSnapshot.From(item), slot, 0, 1, 20, 10, false).Damage);
                Assert.AreEqual(10, BotDecisionModel.Estimate(ItemEffectRuleSnapshot.From(item), slot, 0, 1, 10, 20, false).Damage);
            }
            finally { UnityEngine.Object.DestroyImmediate(item); }
        }
    }
}
