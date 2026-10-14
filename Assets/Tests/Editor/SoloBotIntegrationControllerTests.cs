using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Session;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    // Controller-owned decision/lifetime state in EditMode. Network admission and
    // production graph execution have separate fixture gates; no production test hook is added.
    [Category("SoloIntegration")]
    public sealed class SoloBotIntegrationControllerTests
    {
        GameObject _object;
        BotTurnController _controller;
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        void Set(string name, object value) => typeof(BotTurnController).GetField(name, Private).SetValue(_controller, value);
        T Get<T>(string name) => (T)typeof(BotTurnController).GetField(name, Private).GetValue(_controller);
        object Invoke(string name, params object[] args) => typeof(BotTurnController).GetMethod(name, Private).Invoke(_controller, args);

        [SetUp] public void Setup()
        {
            _object = new GameObject("Isolated controller state fixture"); _object.SetActive(false);
            _controller = _object.AddComponent<BotTurnController>();
            // This fixture never launches or calls authority. Only judgement is needed by selection.
            var settings = (ResolvedSoloSettings)FormatterServices.GetUninitializedObject(typeof(ResolvedSoloSettings));
            typeof(ResolvedSoloSettings).GetField("<Judgement>k__BackingField", Private).SetValue(settings, BotJudgementSettings.Advanced);
            Set("_settings", settings); Set("_random", new System.Random(46));
        }
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(_object);

        static BotScoreSheet Sheet(bool initialCrisis)
        {
            var candidate = new BotCandidate(1, 0, 0, 6);
            var projection = new BotCandidateProjection(6, 6, 2, 1, 5, true);
            return new BotScoreSheet(initialCrisis, 2, 2, 5,
                new[] { new BotScoredItem(candidate, BotItemCategory.Attack, 5, 105, 1, true, 0, projection, 5) });
        }

        [TestCase(true)] [TestCase(false)]
        public void T02ActualControllerCommitsNoItemReadyWithoutFallingThrough(bool initial)
        {
            var sheet = Sheet(initial); Set("_sheet", sheet); Set("_chosen", sheet.Ranked[0].Candidate);
            var result = initial ? Invoke("PickCareOrReady", false) : Invoke("Picked", "odds", true);
            Assert.AreEqual(true, result); Assert.IsTrue(Get<bool>("_noItemReady"));
            Assert.That(Get<BotCandidate>("_chosen").Copy, Is.Zero);
            Assert.AreEqual(0, _controller.SelectionCount);
            Assert.AreEqual(new System.Random(46).Next(), Get<System.Random>("_random").Next());
        }

        [TestCase(7, 2, 4, false)] [TestCase(7, 3, 1, true)] [TestCase(8, 2, 3, true)]
        public void T06CoolingHistoryHasRoundAndSessionLifetime(int generation, int round, int turn, bool cleared)
        {
            Set("_started", true); Set("_key", new PrepInputKey(7, 2, 3, 4));
            Get<List<float>>("_opponentCooling").Add(3);
            Set("_coolingOpen", true); Set("_coolingRecorded", true);
            Invoke("ResetCoolingFor", new PrepInputKey(generation, round, turn, 5));
            Assert.That(Get<List<float>>("_opponentCooling").Count, Is.EqualTo(cleared ? 0 : 1));
            if (cleared) { Assert.IsFalse(Get<bool>("_coolingOpen")); Assert.IsFalse(Get<bool>("_coolingRecorded")); }
        }

        [Test]
        public void T06DetachIsIdempotentAndAllowsSameKeyInANewBinding()
        {
            Set("_started", true); Set("_noItemReady", true); Get<List<float>>("_opponentCooling").Add(4);
            Invoke("Detach"); Invoke("Detach");
            Assert.IsFalse(Get<bool>("_started")); Assert.IsFalse(Get<bool>("_noItemReady"));
            Assert.That(Get<List<float>>("_opponentCooling"), Is.Empty); Assert.IsNull(Get<ResolvedSoloSettings>("_settings"));
            Assert.IsFalse(_controller.IsRunning);
        }

        [Test]
        public void T04FreshCoolingRejectsAnAttackBeforeAdmission()
        {
            Assert.IsFalse(BotDecisionModel.CanSubmitChoice(Sheet(false)));
            var c = new BotCandidate(1, 0, 0, 2);
            var safe = new BotScoreSheet(false, 1, 2, 5, new[] {
                new BotScoredItem(c, BotItemCategory.Attack, 5, 5, 1, false, 0,
                    new BotCandidateProjection(2, 2, 8, 30, 5, true), 5) });
            Assert.IsTrue(BotDecisionModel.CanSubmitChoice(safe));
            Assert.IsFalse(BotDecisionModel.CanSubmitChoice(new BotScoreSheet(false, 1, 0, 0, Array.Empty<BotScoredItem>())));
        }
    }

    [Category("SoloIntegration")]
    public sealed class SoloDevelopmentLaunchTests
    {
        [Test]
        public void UnspecifiedPortRetainsTheExistingLocalEndpoint()
            => Assert.AreEqual(18777, AppBootstrapper.ResolveDevelopmentSoloPort(new[] { "player" }));

        [TestCase("1024", 1024)] [TestCase("18877", 18877)] [TestCase("65535", 65535)]
        public void ExplicitPortUsesOnlyTheSuppliedProcessLocalValue(string value, int expected)
            => Assert.AreEqual(expected, AppBootstrapper.ResolveDevelopmentSoloPort(new[] { "player", "--az-solo-local-port", value }));

        [TestCase("0")] [TestCase("1023")] [TestCase("65536")] [TestCase("-1")] [TestCase("invalid")]
        public void InvalidPortCannotStartLocalServices(string value)
            => Assert.Throws<ArgumentException>(() => AppBootstrapper.ResolveDevelopmentSoloPort(new[] { "--az-solo-local-port", value }));

        [Test]
        public void MissingOrDuplicatedPortCannotSilentlyFallBack()
        {
            Assert.Throws<ArgumentException>(() => AppBootstrapper.ResolveDevelopmentSoloPort(new[] { "--az-solo-local-port" }));
            Assert.Throws<ArgumentException>(() => AppBootstrapper.ResolveDevelopmentSoloPort(
                new[] { "--az-solo-local-port", "18877", "--az-solo-local-port", "18878" }));
        }
    }
}
