using System;
using System.Linq;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    [Category("SoloIntegration")]
    public sealed class SoloBotIntegrationScoringTests
    {
        static BotItemProfile Attack(float success = 1, BotItemFormula formula = BotItemFormula.DirectAttack,
            float amount = 5, float offset = 0) => new(0, true, formula, BotItemCategory.Attack,
            BotAttackKind.Tool, amount, offset, success, 5, true, true, 0);
        static BotItemProfile Guard(float amount = 2, DamageFilter filter = DamageFilter.Temperature)
            => new(1, true, BotItemFormula.Guard, BotItemCategory.Defense, BotAttackKind.Tool,
                0, 0, 1, 2, false, false, 1, amount, filter);
        static BotObservation Observe(BotCandidate[] candidates, float own = 20, float other = 30,
            float fan = 0, bool ready = false, bool fanActive = true, bool canReceive = true,
            BotOpponentItem[] opponent = null)
            => new(new PrepInputKey(1, 1, 1, 1), 1, 0, own, other, fan, 0, ready, 20, 3,
                opponent ?? new[] { new BotOpponentItem(1, true) }, candidates,
                fanActive, false, canReceive);
        static BotScoreSheet Score(BotObservation o, BotItemProfile[] profiles, float lookahead = .5f, float block = .3f)
        {
            var j = BotJudgementSettings.Advanced; j.LookaheadFactor = lookahead; j.OpponentBlockChance = block;
            return BotDecisionModel.Score(o, profiles, j, BotScoringCoefficients.Approved, .25);
        }

        [TestCase(1f, 4.4f)] [TestCase(.8f, 3.52f)]
        public void E01PartialDefenseUsesActualTwoDegreeReduction(float success, float expected)
            => Assert.That(Score(Observe(new[] { new BotCandidate(1, 0, 0, 2) }),
                new[] { Attack(success), Guard() }).Ranked.Single().Score, Is.EqualTo(expected).Within(.0001));

        [TestCase(2f, DamageFilter.Food, .3f, 5f)]
        [TestCase(float.MaxValue, DamageFilter.All, .3f, 3.5f)]
        [TestCase(float.MaxValue, DamageFilter.All, 1f, 0f)]
        public void E02WrongFilterAndFullDefense(float reduction, DamageFilter filter, float chance, float expected)
        {
            var sheet = Score(Observe(new[] { new BotCandidate(1, 0, 0, 2) }),
                new[] { Attack(), Guard(reduction, filter) }, block: chance);
            Assert.That(sheet.Ranked.Sum(x => x.Score), Is.EqualTo(expected).Within(.0001));
        }

        [TestCase(10f, 15f, 4.4f)] [TestCase(15f, 10f, 0f)] [TestCase(10f, 10f, 0f)]
        public void E03EqualizationHasSignedGapAndActualDefense(float own, float other, float expected)
        {
            var sheet = Score(Observe(new[] { new BotCandidate(1, 0, 0, 2) }, own, other),
                new[] { Attack(formula: BotItemFormula.Equalize), Guard() });
            Assert.That(sheet.Ranked.Sum(x => x.Score), Is.EqualTo(expected).Within(.0001));
            Assert.IsFalse(sheet.HasLethal);
        }

        [TestCase(0f, .5f)] [TestCase(.3f, .35f)] [TestCase(1f, 0f)]
        public void E04FiniteGuardRejectsEntireDuelDebuff(float block, float expected)
        {
            var sheet = Score(Observe(new[] { new BotCandidate(1, 0, 0, 2) }),
                new[] { Attack(formula: BotItemFormula.DelayedDamage, amount: 7, offset: 3), Guard() }, block: block);
            Assert.That(sheet.Ranked.Sum(x => x.Score), Is.EqualTo(expected).Within(.0001));
            Assert.IsFalse(sheet.HasLethal);
        }

        [Test]
        public void E05FutureOnlyThreatDoesNotRescueImmediateCrisis()
        {
            var profiles = new[] { Attack(formula: BotItemFormula.DelayedDamage, amount: 20), Guard() };
            var o = Observe(new[] { new BotCandidate(2, 1, 1, 2) }, own: 2, fan: 1,
                opponent: new[] { new BotOpponentItem(0, true) });
            var sheet = Score(o, profiles);
            Assert.That(sheet.Threat, Is.Zero);
            Assert.IsFalse(BotDecisionModel.TryPickCrisis(sheet, BotJudgementSettings.Advanced, new Random(1), out _));
        }

        [Test]
        public void E06CurrentCopyHealIsCappedAtProjectedTemperature()
        {
            var heal = new BotItemProfile(0, true, BotItemFormula.Heal, BotItemCategory.Recovery,
                BotAttackKind.None, 0, 0, 1, 7, true, true, 0);
            var sheet = Score(Observe(new[] { new BotCandidate(3, 1, 0, 4, 7) }, own: 33,
                opponent: Array.Empty<BotOpponentItem>()), new[] { heal });
            Assert.That(sheet.Ranked.Single().Immediate, Is.EqualTo(4));
        }

        [Test]
        public void CopiesAreComparedBeforeItemOddsAndTieUsesCopyIdentity()
        {
            var heal = new BotItemProfile(0, true, BotItemFormula.Heal, BotItemCategory.Recovery,
                BotAttackKind.None, 0, 0, 1, 7, true, true, 0);
            var copies = new[] { new BotCandidate(90, 1, 0, 4, 4), new BotCandidate(80, 1, 0, 4, 7), new BotCandidate(70, 1, 0, 4, 7) };
            var sheet = Score(Observe(copies, opponent: Array.Empty<BotOpponentItem>()), new[] { heal });
            Assert.That(sheet.Ranked.Count, Is.EqualTo(1)); Assert.That(sheet.Ranked[0].Candidate.Copy, Is.EqualTo(70));
        }

        [TestCase(2, false)] [TestCase(6, true)]
        public void T01EachCandidateUsesItsOwnConservativeTickBound(double delay, bool crisis)
        {
            var profiles = new[] { Attack(), Guard() };
            var candidate = new BotCandidate(1, 0, 0, delay);
            var projected = BotDecisionModel.ProjectCandidate(Observe(new[] { candidate }, own: 5, fan: 1), candidate, profiles, .25);
            Assert.That(projected.Crisis, Is.EqualTo(crisis)); Assert.That(projected.OwnLoss, Is.EqualTo(delay));
        }

        [TestCase(false, true, 0f)] [TestCase(true, false, 0f)] [TestCase(false, false, 3f)]
        public void T03ReadyAndInactiveFanRemoveCoolingOtherwiseRoundTicksUp(bool ready, bool inactive, float expected)
        {
            var c = new BotCandidate(1, 0, 0, 2.01);
            var projection = BotDecisionModel.ProjectCandidate(Observe(new[] { c }, fan: 1, ready: ready, fanActive: !inactive), c,
                new[] { Attack(), Guard() }, .25);
            Assert.That(projection.OwnLoss, Is.EqualTo(expected));
        }

        [Test]
        public void FullListWithReusableEmptySlotStillAllowsSteal()
        {
            var steal = new BotItemProfile(0, true, BotItemFormula.Steal, BotItemCategory.Disrupt,
                BotAttackKind.None, 0, 0, 1, 6, true, true, 0);
            var finite = new BotItemProfile(1, true, BotItemFormula.Heal, BotItemCategory.Recovery,
                BotAttackKind.None, 0, 0, 1, 7, true, true, 1);
            var c = new[] { new BotCandidate(1, 0, 0, 4) };
            Assert.That(Score(Observe(c, canReceive: true), new[] { steal, finite }).Ranked.Count, Is.EqualTo(1));
            Assert.That(Score(Observe(c, canReceive: false), new[] { steal, finite }).Ranked, Is.Empty);
        }

        [Test]
        public void T02InitialCrisisWithoutCareTerminatesEvenWithALethalAttack()
        {
            var foe = new BotItemProfile(1, true, BotItemFormula.DirectAttack, BotItemCategory.Attack,
                BotAttackKind.Tool, 5, 0, 1, 5, true, true, 1);
            var sheet = Score(Observe(new[] { new BotCandidate(1, 0, 0, 2) }, own: 6, other: 1, fan: 1),
                new[] { Attack(), foe });
            Assert.IsTrue(sheet.Crisis); Assert.IsTrue(sheet.HasLethal);
            Assert.IsFalse(BotDecisionModel.MustTakeLethal(sheet, BotJudgementSettings.Advanced));
            BotDecisionModel.ChooseCrisisOrReady(sheet, BotJudgementSettings.Advanced, new Random(1), out var picked, out var ready);
            Assert.IsTrue(ready); Assert.That(picked.Copy, Is.Zero);
        }

        [Test]
        public void T02SelectedLongDelayRedirectsOnceToNoItemReady()
        {
            var foe = new BotItemProfile(1, true, BotItemFormula.DirectAttack, BotItemCategory.Attack,
                BotAttackKind.Tool, 5, 0, 1, 5, true, true, 1);
            var slow = new BotCandidate(2, 0, 1, 6);
            var sheet = Score(Observe(new[] { new BotCandidate(1, 0, 0, 2), slow }, own: 8, fan: 1), new[] { Attack(), foe });
            Assert.IsFalse(sheet.Crisis);
            Assert.IsTrue(BotDecisionModel.NeedsCrisisRedirect(sheet, slow));
            var stream = new Random(7); var control = new Random(7);
            BotDecisionModel.ChooseCrisisOrReady(sheet, BotJudgementSettings.Advanced, stream, out var picked, out var ready);
            Assert.IsTrue(ready); Assert.That(picked.Copy, Is.Zero);
            Assert.AreEqual(control.Next(), stream.Next(), "No additional random selection when no care exists");
        }

        [TestCase(1, 10, 11f)] [TestCase(5, 10, 1f)] [TestCase(2, -10, 0f)]
        public void T05KnownPublicEffectsRespectDueTimeAndDeath(double due, float delta, float expected)
        {
            var c = new BotCandidate(1, 0, 0, 4);
            var effects = new[] { new BotPublicDueEffect(true, due, delta), new BotPublicDueEffect(true, 3, 0) };
            var o = new BotObservation(new PrepInputKey(1, 1, 1, 1), 1, 0, 5, 30, 1, 0, false, 20, 0,
                Array.Empty<BotOpponentItem>(), new[] { c }, publicDueEffects: effects);
            var projection = BotDecisionModel.ProjectCandidate(o, c, new[] { Attack() }, .25);
            Assert.That(projection.OwnTemperature, Is.EqualTo(expected));
        }

        [Test]
        public void T05FutureHealCannotResurrectAnEarlierDeadProjection()
        {
            var c = new BotCandidate(1, 0, 0, 4);
            var o = new BotObservation(new PrepInputKey(1, 1, 1, 1), 1, 0, 2, 30, 1, 0, false, 20, 0,
                Array.Empty<BotOpponentItem>(), new[] { c }, publicDueEffects: new[] { new BotPublicDueEffect(true, 3, 20) });
            Assert.That(BotDecisionModel.ProjectCandidate(o, c, new[] { Attack() }, .25).OwnTemperature, Is.Zero);
        }
    }
}
