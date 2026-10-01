using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player;
using AbsoluteZero.UI.Game.Bridge;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Turn;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037NotificationTests
    {
        static MatchSnapshot Result(uint sequence = 0) => new()
        {
            RoundNumber = 1, TurnNumber = 2, CurrentPhase = TurnPhase.RoundOver,
            MatchState = MatchState.MatchComplete, MultiDecidingSequence = sequence,
            KillScores = new[] { 5, 0, 0, 0 }, LifeStates = new[] { LifeState.Alive, LifeState.Ghost }
        };

        [Test]
        public void EmptyBufferDoesNotInventAResult()
            => Assert.That(new MatchResultBuffer().TryRead(out _), Is.False);

        [Test]
        public void LateSubscriberReadsTheSameDeliveredRevision()
        {
            var results = new MatchResultBuffer();
            Assert.That(results.TryPublish(Result(7), true, 100, out var published), Is.True);
            Assert.That(results.TryRead(out var late), Is.True);
            Assert.That(late.Revision, Is.EqualTo(published.Revision));
            Assert.That(late.IsMatchEnd, Is.True);
            Assert.That(late.Snapshot.MultiDecidingSequence, Is.EqualTo(7));
        }

        [Test]
        public void DuplicateTerminalOrSubsequentRoundEdgeCannotReplayTerminal()
        {
            var results = new MatchResultBuffer();
            results.TryPublish(Result(7), true, 100, out _);
            Assert.That(results.TryPublish(Result(7), true, 100, out _), Is.False);
            Assert.That(results.TryPublish(Result(7), false, 100, out _), Is.False);
        }

        [Test]
        public void TerminalCanReplaceAnAlreadyDeliveredRoundResultOnce()
        {
            var results = new MatchResultBuffer();
            results.TryPublish(Result(), false, 100, out var round);
            Assert.That(results.TryPublish(Result(), true, 100, out var terminal), Is.True);
            Assert.That(terminal.Revision, Is.GreaterThan(round.Revision));
            Assert.That(results.TryPublish(Result(), true, 100, out _), Is.False);
        }

        [Test]
        public void UnobservedDrawRoundCanReuseNumbersButHasANewPrepStart()
        {
            var results = new MatchResultBuffer();
            results.TryPublish(Result(), false, 100, out var old);
            Assert.That(results.TryPublish(Result(), false, 200, out var newer), Is.True);
            Assert.That(newer.Revision, Is.GreaterThan(old.Revision));
        }

        [Test]
        public void ResetHidesOldResultButDoesNotResetRevisionForSurvivingView()
        {
            var results = new MatchResultBuffer();
            results.TryPublish(Result(), false, 100, out var old);
            results.Clear();
            Assert.That(results.TryRead(out _), Is.False);
            Assert.That(results.TryPublish(Result(), false, 200, out var newer), Is.True);
            Assert.That(newer.Revision, Is.GreaterThan(old.Revision));
        }

        [Test]
        public void SnapshotArraysAreOwnedAcrossPublisherAndMultipleReaders()
        {
            var results = new MatchResultBuffer();
            var source = Result();
            results.TryPublish(source, true, 100, out var delivered);
            source.KillScores[0] = 0;
            delivered.Snapshot.KillScores[0] = 99;
            delivered.Snapshot.LifeStates[0] = LifeState.Ghost;
            results.TryRead(out var late);
            Assert.That(late.Snapshot.KillScores[0], Is.EqualTo(5));
            Assert.That(late.Snapshot.LifeStates[0], Is.EqualTo(LifeState.Alive));
            late.Snapshot.KillScores[0] = 3;
            results.TryRead(out var another);
            Assert.That(another.Snapshot.KillScores[0], Is.EqualTo(5));
        }

        [Test]
        public void AnotherMatchCannotReadPreviousMatchResults()
        {
            var old = new MatchResultBuffer();
            old.TryPublish(Result(7), true, 100, out _);
            Assert.That(new MatchResultBuffer().TryRead(out _), Is.False);
        }

        [Test]
        public void BridgePublishesOnceAndRetainsBeforeCallingALateSubscriber()
        {
            var go = new GameObject("Result bridge fixture");
            go.SetActive(false);
            var bridge = go.AddComponent<GameDataBridge>();
            try
            {
                Set(bridge, "_currentMatch", Result(7));
                int calls = 0;
                bridge.OnMatchEnd += snapshot =>
                {
                    calls++;
                    Assert.That(bridge.TryGetLatestResult(out var retained), Is.True);
                    Assert.That(retained.IsMatchEnd, Is.True);
                };
                Call(bridge, "PublishResult", true);
                Call(bridge, "PublishResult", true);
                Assert.That(calls, Is.EqualTo(1));
                bridge.OnPhaseChanged += (_, _) => Assert.That(bridge.TryGetLatestResult(out _), Is.False,
                    "A subscriber created during Prep notification must not see the previous result.");
                Call(bridge, "OnPhaseNVChanged", TurnPhase.RoundOver, TurnPhase.PrepPhase);
                Assert.That(bridge.TryGetLatestResult(out _), Is.False);
                Call(bridge, "PublishResult", true);
                Assert.That(calls, Is.EqualTo(2));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ReplacedMatchStopsAnOldManagerWaitWithoutBindingANewFacade()
        {
            var original = MatchCompositionRoot.Instance;
            var oldGo = new GameObject("Old match fixture"); oldGo.SetActive(false);
            var newGo = new GameObject("New match fixture"); newGo.SetActive(false);
            var view = new GameObject("Old view fixture"); view.SetActive(false);
            var oldRoot = oldGo.AddComponent<MatchCompositionRoot>();
            var newRoot = newGo.AddComponent<MatchCompositionRoot>();
            var bridge = view.AddComponent<GameDataBridge>();
            var registry = new PlayerRegistry();
            Set(oldRoot, "_registry", registry);
            Set(bridge, "_root", oldRoot); Set(bridge, "_registry", registry);
            Set(bridge, "_initialized", true); Set(bridge, "_binding", true);
            typeof(MatchCompositionRoot).GetProperty(nameof(MatchCompositionRoot.Instance))
                .SetValue(null, newRoot);
            try
            {
                Assert.That(((IEnumerator)Call(bridge, "WaitForManagers")).MoveNext(), Is.False);
                Assert.That(Get(bridge, "_tm"), Is.Null);
                Assert.That(Get(bridge, "_binding"), Is.False);
                Assert.That(bridge.TryGetLatestResult(out _), Is.False);
            }
            finally
            {
                typeof(MatchCompositionRoot).GetProperty(nameof(MatchCompositionRoot.Instance)).SetValue(null, original);
                Object.DestroyImmediate(view); Object.DestroyImmediate(oldGo); Object.DestroyImmediate(newGo);
            }
        }

        [Test]
        public void DisplayTemperatureBelongsToItsVisualAndClearsOnDisable()
        {
            var a = new GameObject("Visual A"); a.SetActive(false);
            var b = new GameObject("Visual B"); b.SetActive(false);
            var first = a.AddComponent<CombatVFXManager>();
            var second = b.AddComponent<CombatVFXManager>();
            try
            {
                int firstEvents = 0, secondEvents = 0;
                first.OnPlayerTempOverride += (_, _) => firstEvents++;
                second.OnPlayerTempOverride += (_, _) => secondEvents++;
                first.ShowTemperature(3, 17);
                Assert.That(first.TryGetDisplayTemperature(3, out var value), Is.True);
                Assert.That(value, Is.EqualTo(17));
                Assert.That(second.TryGetDisplayTemperature(3, out _), Is.False);
                Assert.That(firstEvents, Is.EqualTo(1)); Assert.That(secondEvents, Is.Zero);
                Call(first, "OnDisable");
                Assert.That(first.TryGetDisplayTemperature(3, out _), Is.False);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void OldBridgeCleanupCannotDetachAnotherBridgesInstanceNotifications()
        {
            var go = new GameObject("Notification sources"); go.SetActive(false);
            var tm = go.AddComponent<TurnManager>();
            var old = go.AddComponent<GameDataBridge>();
            var newer = go.AddComponent<GameDataBridge>();
            try
            {
                Set(old, "_tm", tm); Set(newer, "_tm", tm);
                Call(old, "SubscribeTurnManager"); Call(newer, "SubscribeTurnManager");
                int oldCalls = 0, newCalls = 0;
                old.OnOpponentRevealed += (_, _) => oldCalls++;
                newer.OnOpponentRevealed += (_, _) => newCalls++;
                Call(old, "EndBinding"); Call(old, "EndBinding");
                var handler = (System.Action<byte, short>)typeof(TurnManager)
                    .GetField("OnOpponentRevealed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tm);
                handler?.Invoke(1, 2);
                Assert.That(oldCalls, Is.Zero); Assert.That(newCalls, Is.EqualTo(1));
            }
            finally { Call(old, "EndBinding"); Call(newer, "EndBinding"); Object.DestroyImmediate(go); }
        }

        static void Set(object target, string field, object value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        static object Get(object target, string field)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        static object Call(object target, string method, params object[] args)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
