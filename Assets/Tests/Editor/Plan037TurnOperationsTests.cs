using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037TurnOperationsTests
    {
        readonly List<GameObject> _objects = new();
        [TearDown] public void TearDown()
        {
            foreach (var go in _objects) Object.DestroyImmediate(go);
            _objects.Clear();
        }
        PlayerState Player(float temperature = 37f, bool ghost = false)
        {
            var go = new GameObject("Turn operation sample");
            _objects.Add(go); go.AddComponent<PlayerInventory>(); go.SetActive(false);
            var p = go.AddComponent<PlayerState>();
            p.Temperature.Value = temperature;
            p.CurrentLifeState.Value = ghost ? LifeState.Ghost : LifeState.Alive;
            return p;
        }

        [TestCase(true, true)] [TestCase(false, false)]
        public void ReadinessSkipsMissingSeatsAndOnlyMultiSkipsGhost(bool multi, bool expected)
        {
            var alive = Player(); alive.IsReady.Value = true;
            Assert.That(PrepTurnOperations.AllReady(new[] { alive, null, Player(ghost:true) }, multi), Is.EqualTo(expected));
        }

        [TestCase(true, 0)] [TestCase(false, 2)]
        public void FirstReadyKeepsSeatTieOrderAndModeEligibility(bool multi, int expected)
        {
            var a = Player(); var b = Player(); var ghost = Player(ghost:true);
            a.GetActionQueue().SetReady(4f); b.GetActionQueue().SetReady(4f); ghost.GetActionQueue().SetReady(1f);
            Assert.That(PrepTurnOperations.FirstReadySeat(new[] { a, b, ghost, null }, multi), Is.EqualTo((byte)expected));
        }

        [Test]
        public void EmptyOrNonpositiveReadyTimestampDoesNotInventFirstSeat()
        {
            var a = Player(); var b = Player(); b.GetActionQueue().SetReady(-1f);
            Assert.That(PrepTurnOperations.FirstReadySeat(new[] { a, b, null }, true), Is.EqualTo(byte.MaxValue));
            Assert.That(PrepTurnOperations.AllReady(Array.Empty<PlayerState>(), true), Is.True);
        }

        [Test]
        public void MultiTickSkipsFirstCoolingButStillRecoversReadyPlayerAndSkipsGhost()
        {
            var fan = Player(31f); fan.IsFanActive.Value = true; fan.FanSpeed.Value = 2f;
            var ready = Player(30f); ready.IsFanActive.Value = false; ready.IsReady.Value = true;
            var ghost = Player(12f, true); ghost.IsFanActive.Value = true;
            var players = new[] {fan, ready, null, ghost};
            var mods = new PlayerModifiers[4]; foreach (ref var m in mods.AsSpan()) m.Reset();
            mods[0].FanSpeedMultiplier = 1.5f; mods[1].RecoveryMultiplier = 2f;
            var system = new TemperatureSystem();
            PrepTurnOperations.ApplyMultiTick(players, mods, system, true, 1f);
            Assert.That(fan.Temperature.Value, Is.EqualTo(31f));
            Assert.That(ready.Temperature.Value, Is.EqualTo(32f));
            PrepTurnOperations.ApplyMultiTick(players, mods, system, false, 1f);
            Assert.That(fan.Temperature.Value, Is.EqualTo(28f));
            Assert.That(ready.Temperature.Value, Is.EqualTo(34f));
            Assert.That(ghost.Temperature.Value, Is.EqualTo(12f));
        }

        [Test]
        public void MultiThresholdGrantSkipsGhostAndMarksAliveOnce()
        {
            var alive = Player(10f); var ghost = Player(0f, true);
            PrepTurnOperations.GrantMultiThresholds(new[] { alive, ghost, null }, new TemperatureSystem(), null, 4);
            Assert.That(alive.GetInventory().GetThresholdGranted(), Is.EqualTo(new[] {true,true,true}));
            Assert.That(ghost.GetInventory().GetThresholdGranted(), Is.EqualTo(new[] {false,false,false}));
        }

        [Test]
        public void CompleteReadyKeepsReadyTimestampAndGhostUpgradeUntouched()
        {
            var ready = Player(); ready.IsReady.Value = true; ready.GetActionQueue().SetReady(5f);
            ready.IsFanUpgraded.Value = true; ready.FanSpeed.Value = 2f;
            var ghost = Player(0f, true); ghost.IsFanUpgraded.Value = true; ghost.FanSpeed.Value = 2f;
            var pending = Player(); pending.IsFanActive.Value = true;
            PrepTurnOperations.CompleteReady(new[] { ready, pending, ghost, null }, true, new RoundLifecycleService());
            Assert.That(ready.GetActionQueue().readyTimestamp, Is.EqualTo(5f));
            Assert.That(ready.FanSpeed.Value, Is.EqualTo(1f));
            Assert.That(pending.IsReady.Value, Is.True); Assert.That(pending.IsFanActive.Value, Is.False);
            Assert.That(ghost.IsReady.Value, Is.False); Assert.That(ghost.FanSpeed.Value, Is.EqualTo(2f));
        }

        [Test]
        public void InvalidParticipantSetIsRejectedAndInvalidVisualIsNotReady()
        {
            Assert.Throws<InvalidOperationException>(() => TurnParticipantSetup.ValidateBatch(null, null, 2));
            Assert.That(TurnParticipantSetup.AreViewsReady(new[] {new PlayerBinding(null, null, null)}), Is.False);
        }

        [Test]
        public void MultiCapturePreservesSparseSeatsAndBuildsFreshTemperatures()
        {
            var alive = Player(19f);
            // EditMode does not run this non-ExecuteAlways component's Awake.
            using var slots = new Unity.Netcode.NetworkList<ItemSlotNetData>();
            alive.GetInventory().SlotStates = slots;
            var capture = new MultiCombatCapture(new[] { alive, null, null },
                new PlayerModifiers[3], new[] { 37f, 37f, 37f }, null, null, null, EnvironmentType.None, null);
            var rules = Array.Empty<ItemEffectRuleSnapshot>();
            var before = capture.BuildSnapshot(rules);
            alive.Temperature.Value = 7f;
            var after = capture.BuildSnapshot(rules);
            Assert.That(before.CurrentTemperatures, Is.EqualTo(new[] {19f, 0f, 0f}));
            Assert.That(after.CurrentTemperatures, Is.EqualTo(new[] {7f, 0f, 0f}));
            Assert.That(after.LifeStates[1], Is.EqualTo(LifeState.Ghost));
            Assert.That(capture.BuildActionIntentForSeat(-1).IsEmpty, Is.True);
            Assert.That(capture.BuildActionIntentForSeat(3).IsEmpty, Is.True);
        }

        [Test]
        public void MultiCaptureDoesNotTurnGhostOrMissingQueueIntoAnAction()
        {
            var alive = Player(); var ghost = Player(0f, true);
            ghost.GetActionQueue().SetSelected(0, null, 0, 10);
            var capture = new MultiCombatCapture(new[] { alive, null, ghost },
                new PlayerModifiers[3], new float[3], null, null, null, EnvironmentType.None, null);
            foreach (var intent in capture.BuildActionIntents()) Assert.That(intent.IsEmpty, Is.True);
            Assert.That(capture.BuildActionIntentForSeat(2).IsEmpty, Is.True);
        }

        [Test]
        public void GhostRequestHistoryKeepsLatest32AndRejectsEvictedReplay()
        {
            var history = new GhostRequestHistory();
            for (uint id = 1; id <= 33; id++) history.Record(42, id, GhostSkillRequestResult.Accepted);
            Assert.That(history.TryGet(42, 1, out _), Is.False);
            Assert.That(history.IsStale(42, 1), Is.True);
            Assert.That(history.TryGet(42, 2, out var result), Is.True);
            Assert.That(result, Is.EqualTo(GhostSkillRequestResult.Accepted));
            Assert.That(history.TryGet(42, 33, out _), Is.True);
            Assert.That(history.IsStale(42, 34), Is.False);
        }

        [Test]
        public void GhostRequestHistorySeparatesSendersAndCachesRejection()
        {
            var history = new GhostRequestHistory();
            history.Record(7, 100, GhostSkillRequestResult.InvalidTarget);
            history.Record(9, 100, GhostSkillRequestResult.Accepted);
            Assert.That(history.TryGet(7, 100, out var first), Is.True);
            Assert.That(first, Is.EqualTo(GhostSkillRequestResult.InvalidTarget));
            Assert.That(history.TryGet(9, 100, out var second), Is.True);
            Assert.That(second, Is.EqualTo(GhostSkillRequestResult.Accepted));
            Assert.That(history.IsStale(8, 1), Is.False);
        }

        [Test]
        public void NewMatchClearDropsGhostRepliesAndHighWaterTogether()
        {
            var history = new GhostRequestHistory();
            history.Record(0, uint.MaxValue, GhostSkillRequestResult.InputClosed);
            history.Clear(); history.Clear();
            Assert.That(history.TryGet(0, uint.MaxValue, out _), Is.False);
            Assert.That(history.IsStale(0, 1), Is.False);
            history.Record(0, 1, GhostSkillRequestResult.Accepted);
            Assert.That(history.TryGet(0, 1, out _), Is.True);
        }
    }
}
