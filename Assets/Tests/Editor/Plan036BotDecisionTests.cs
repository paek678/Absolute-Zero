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
    // General decision-model contracts. Spec values live in Plan046BotJudgementTests.
    [Category("SoloIntegration")]
    public sealed class Plan036BotDecisionTests
    {
        static readonly PrepInputKey Key = new(7, 2, 3, 4);
        static readonly BotItemProfile[] Profiles =
        {
            new(0, true, BotItemFormula.DirectAttack, BotItemCategory.Attack, BotAttackKind.Tool, 3, 0, 1, 3, false, false, 0),
            new(1, true, BotItemFormula.Heal, BotItemCategory.Recovery, BotAttackKind.None, 0, 0, 1, 7, true, false, 1),
            new(2, true, BotItemFormula.DirectAttack, BotItemCategory.Attack, BotAttackKind.Food, 5, 0, 1, 5, true, true, 2),
            new(3, false, BotItemFormula.DirectAttack, BotItemCategory.Attack, BotAttackKind.Tool, 9, 0, 1, 0, true, true, int.MaxValue)
        };
        static readonly BotCandidate Attack = new(1, 0, 0, 1);
        static readonly BotCandidate Heal = new(2, 1, 1, 1, 7);
        static readonly BotCandidate Food = new(3, 0, 2, 1);
        static BotObservation Observe(float own = 30, float opponent = 30, double remaining = 10,
            IList<BotCandidate> candidates = null, bool opponentReady = false)
            => new(Key, 1, 0, own, opponent, 1, 1, opponentReady, remaining, 3,
                Array.Empty<BotOpponentItem>(), candidates ?? new[] { Attack, Heal, Food });
        static BotScoreSheet Score(BotObservation observation)
            => BotDecisionModel.Score(observation, Profiles, BotJudgementSettings.Beginner, BotScoringCoefficients.Approved, .25);

        [Test] public void IndependentBotEvaluationCannotChangeAnotherBotsDecisionStream()
        {
            var sheet = Score(Observe());
            var control = new System.Random(22); var subject = new System.Random(22); var unrelated = new System.Random(23);
            for (int i = 0; i < 40; i++)
            {
                BotDecisionModel.TryPickWeighted(sheet, BotJudgementSettings.Beginner, control, out var a);
                for (int extra = 0; extra < i; extra++)
                    BotDecisionModel.TryPickWeighted(sheet, BotJudgementSettings.Beginner, unrelated, out _);
                BotDecisionModel.TryPickWeighted(sheet, BotJudgementSettings.Beginner, subject, out var b);
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
        { Assert.That(Score(Observe(remaining: remaining)).Ranked, Is.Empty); }
        [Test] public void OpponentReadySkipsTheDelayInAffordabilityAndOwnCooling()
        {
            var sheet = Score(Observe(remaining: 1, opponentReady: true));
            Assert.That(sheet.Ranked, Is.Not.Empty);
            Assert.That(sheet.OwnLoss, Is.Zero);
        }
        [Test] public void EmptyInventoryYieldsNoCandidateForFallback()
        {
            var sheet = Score(Observe(candidates: Array.Empty<BotCandidate>()));
            Assert.False(BotDecisionModel.TryPickWeighted(sheet, BotJudgementSettings.Beginner, new System.Random(1), out _));
            Assert.False(BotDecisionModel.TryPickCrisis(sheet, BotJudgementSettings.Beginner, new System.Random(1), out _));
        }
        [Test] public void UnknownItemIsNeverChosen()
        {
            var unknown = new BotCandidate(4, 0, 3, 1);
            Assert.That(Score(Observe(candidates: new[] { unknown })).Ranked, Is.Empty);
        }
        [Test] public void DuplicateCopiesAreRankedOncePerItem()
        {
            var second = new BotCandidate(9, 0, 0, 1);
            var sheet = Score(Observe(candidates: new[] { Attack, second, Food }));
            Assert.That(sheet.Ranked.Count, Is.EqualTo(2));
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
                var sheet = Score(Observe());
                for (int i = 0; i < 30; i++)
                {
                    Assert.True(BotDecisionModel.TryPickWeighted(sheet, BotJudgementSettings.Beginner, a, out var x));
                    Assert.True(BotDecisionModel.TryPickWeighted(sheet, BotJudgementSettings.Beginner, b, out var y));
                    Assert.AreEqual(x.Copy, y.Copy);
                }
                Assert.AreEqual(JsonUtility.ToJson(baseline), JsonUtility.ToJson(UnityEngine.Random.state));
                Assert.AreNotEqual(seed, BotDecisionModel.Seed("default", new PrepInputKey(7, 2, 4, 5)));
            }
            finally { UnityEngine.Random.state = saved; }
        }
        [Test] public void RecoveryHealUsesCurrentUseAndScoreCapsAtMaximumTemperature()
        {
            var item = ScriptableObject.CreateInstance<RecoveryItemDataSO>();
            try
            {
                item.MaxUses = 3; item.HealPerUse = new float[] { 2, 5, 9 };
                var slot = new ItemSlotNetData { CopyId = 9, RemainingUses = 2, ItemId = 1 };
                float heal = BotDecisionModel.CurrentHeal(ItemEffectRuleSnapshot.From(item), slot);
                Assert.AreEqual(5, heal); Assert.AreEqual(2, slot.RemainingUses); Assert.AreEqual(5, item.HealPerUse[1]);
                var sheet = Score(Observe(own: 35, candidates: new[] { new BotCandidate(2, 1, 1, 1, heal) }));
                // One tick during the delay projects 35 -> 34, leaving three degrees of capacity.
                Assert.AreEqual(3, sheet.Ranked[0].Score, 1e-4);
            }
            finally { UnityEngine.Object.DestroyImmediate(item); }
        }
    }
}
