using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using NUnit.Framework;
using Unity.Behavior;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    [Category("SoloIntegration")]
    public sealed class SoloCompiledGraphTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject _owner;
        readonly List<string> _calls = new();
        BotTurnController _controller;

        [SetUp] public void Setup()
        {
            _owner = new GameObject("Compiled graph control-flow fixture");
            _owner.SetActive(false);
            _controller = _owner.AddComponent<BotTurnController>();
            _calls.Clear();
        }
        [TearDown] public void Cleanup() => UnityEngine.Object.DestroyImmediate(_owner);

        void Configure(bool crisis, bool advanced, bool lethal)
        {
            var settings = (ResolvedSoloSettings)FormatterServices.GetUninitializedObject(typeof(ResolvedSoloSettings));
            typeof(ResolvedSoloSettings).GetField("<Judgement>k__BackingField", Private).SetValue(settings,
                advanced ? BotJudgementSettings.Advanced : BotJudgementSettings.Beginner);
            typeof(BotTurnController).GetField("_settings", Private).SetValue(_controller, settings);
            var sheet = new BotScoreSheet(crisis, 2, 2, 5, new[] {
                new BotScoredItem(new BotCandidate(1, 0, 0, 2), BotItemCategory.Attack, 8, 108, 1, lethal, 0),
                new BotScoredItem(new BotCandidate(2, 1, 1, 2, 7), BotItemCategory.Recovery, 7, 7, 1, false, 1)
            });
            typeof(BotTurnController).GetField("_sheet", Private).SetValue(_controller, sheet);
        }

        string Run(Func<string, Node.Status> callback = null)
        {
            var helper = Type.GetType("SoloCompiledGraphFixture, Assembly-CSharp-Editor", true);
            Func<string, Node.Status> execute = name => {
                _calls.Add(name);
                return callback == null ? Node.Status.Success : callback(name);
            };
            return (string)helper.GetMethod("Run").Invoke(null, new object[] { _owner, execute });
        }

        [Test]
        public void SavedAuthoringAndRuntimeReferencesValidateAfterDomainReload()
        {
            var builder = Type.GetType("Plan046SoloBotBuilder, Assembly-CSharp-Editor", true);
            var result = (string)builder.GetMethod("Validate").Invoke(null, null);
            StringAssert.StartsWith("PASS:", result);
        }

        [TestCase(true, true, true, "SoloPickCrisis")]
        [TestCase(true, false, true, "SoloPickCrisis")]
        [TestCase(false, true, true, "SoloPickLethal")]
        [TestCase(false, false, true, "SoloPickWeighted")]
        [TestCase(false, true, false, "SoloPickWeighted")]
        public void SavedGraphTicksRealGuardsInPriorityOrder(bool crisis, bool advanced, bool lethal, string expected)
        {
            Configure(crisis, advanced, lethal);
            Assert.AreEqual("Success", Run());
            CollectionAssert.AreEqual(new[] { "SoloObserve", "SoloCandidates", "SoloScore", expected,
                "SoloThinking", "SoloSubmitUse", "SoloAwaitUse", "SoloChooseReady", "SoloWaitReady", "SoloReady" }, _calls);
        }

        [Test]
        public void SavedGraphWaitsForPendingLeafBeforeReady()
        {
            Configure(false, true, true);
            int polls = 0;
            Assert.AreEqual("Success", Run(name => name == "SoloAwaitUse" && ++polls < 3 ? Node.Status.Running : Node.Status.Success));
            Assert.AreEqual(3, polls);
            Assert.AreEqual(1, _calls.FindAll(x => x == "SoloReady").Count);
            Assert.Greater(_calls.IndexOf("SoloReady"), _calls.LastIndexOf("SoloAwaitUse"));
        }

        [TestCase("SoloCandidates")]
        [TestCase("SoloSubmitUse")]
        public void ClosedControllerStopsRetryAndExecutesOnlyOneFallback(string failedLeaf)
        {
            Configure(false, true, true);
            Assert.AreEqual("Success", Run(name => name == failedLeaf ? Node.Status.Failure : Node.Status.Success));
            Assert.AreEqual(1, _calls.FindAll(x => x == "SoloObserve").Count);
            Assert.AreEqual(1, _calls.FindAll(x => x == "SoloFallback").Count);
            CollectionAssert.DoesNotContain(_calls, "SoloReady");
        }
    }
}
