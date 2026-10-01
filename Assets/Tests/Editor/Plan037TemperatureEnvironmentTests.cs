using System.Collections.Generic;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;
using UnityEngine;

namespace AbsoluteZero.Tests
{
    public sealed class Plan037TemperatureEnvironmentTests
    {
        readonly List<GameObject> _objects = new();
        Random.State _random;
        [SetUp] public void SetUp() => _random = Random.state;
        [TearDown] public void TearDown()
        {
            Random.state = _random;
            foreach (var go in _objects) Object.DestroyImmediate(go);
            _objects.Clear();
        }
        PlayerState Player(float temperature)
        {
            var go = new GameObject("Temperature policy sample");
            _objects.Add(go);
            go.AddComponent<PlayerInventory>();
            go.SetActive(false);
            var player = go.AddComponent<PlayerState>();
            player.Temperature.Value = temperature;
            return player;
        }

        [TestCase(32.5f, true, 2f, 1.5f, 29.5f)]
        [TestCase(1f, true, 2f, 1f, 0f)]
        [TestCase(37f, false, 2f, 1f, 37f)]
        [TestCase(37f, true, -1f, 1f, 38f)] // Preserve current one-sided clamp.
        [TestCase(-2f, true, 0f, 1f, 0f)]
        public void FanTickKeepsCurrentEligibilityAndLowerClamp(float temp, bool active, float speed, float multiplier, float expected)
        {
            var p = Player(temp); p.IsFanActive.Value = active; p.FanSpeed.Value = speed;
            new TemperatureSystem().ApplyFanTick(p, multiplier);
            Assert.That(p.Temperature.Value, Is.EqualTo(expected));
        }

        [TestCase(30f, false, true, 2f, 1.5f, 33f)]
        [TestCase(36f, false, true, 2f, 1f, 37f)]
        [TestCase(30f, true, true, 2f, 1f, 30f)]
        [TestCase(30f, false, false, 2f, 1f, 30f)]
        [TestCase(1f, false, true, -2f, 1f, -1f)]
        public void RecoveryTickRequiresReadyAndFanOffAndKeepsUpperClamp(float temp, bool fan, bool ready, float rate, float multiplier, float expected)
        {
            var p = Player(temp); p.IsFanActive.Value = fan; p.IsReady.Value = ready;
            new TemperatureSystem().ApplyRecoveryTick(p, rate, multiplier);
            Assert.That(p.Temperature.Value, Is.EqualTo(expected));
        }

        [Test]
        public void TickAccumulatorPreservesFractionalCarryAndConsumesEachSecondOnce()
        {
            var system = new TemperatureSystem();
            system.Accumulate(0.75f); Assert.That(system.ConsumeTick(), Is.False);
            system.Accumulate(2.5f);
            Assert.That(system.ConsumeTick(), Is.True); Assert.That(system.ConsumeTick(), Is.True);
            Assert.That(system.ConsumeTick(), Is.True); Assert.That(system.ConsumeTick(), Is.False);
            system.Accumulate(0.75f); Assert.That(system.ConsumeTick(), Is.True);
            system.Accumulate(0.99f); system.ResetTimer(); Assert.That(system.ConsumeTick(), Is.False);
        }

        [TestCase(31f, 0)] [TestCase(30f, 1)] [TestCase(20.001f, 1)]
        [TestCase(20f, 2)] [TestCase(10.001f, 2)] [TestCase(10f, 3)] [TestCase(0f, 3)]
        public void ThresholdFlagsUseInclusiveBoundariesEvenWhenGrantCannotRun(float temperature, int count)
        {
            // Unspawned inventory cannot grant; this isolates the existing flag policy.
            var p = Player(temperature); var flags = new bool[3];
            var system = new TemperatureSystem();
            system.CheckThresholds(p, p.GetInventory(), flags, null);
            for (int i = 0; i < 3; i++) Assert.That(flags[i], Is.EqualTo(i < count));
            p.Temperature.Value = 37f;
            system.CheckThresholds(p, p.GetInventory(), flags, null);
            for (int i = 0; i < 3; i++) Assert.That(flags[i], Is.EqualTo(i < count));
        }

        [TestCase(EnvironmentType.None, 30f, 1f)]
        [TestCase(EnvironmentType.SunnyDay, 30f, 2f)]
        [TestCase(EnvironmentType.CoolBreeze, 30f, 0f)]
        [TestCase(EnvironmentType.CicadaSong, 30f, 1f)]
        [TestCase(EnvironmentType.Kids, 30f, 1f)]
        [TestCase(EnvironmentType.Ambulance, 30f, 1f)]
        [TestCase(EnvironmentType.SummerVacation, 10f, 1f)]
        [TestCase(EnvironmentType.HeatWaveWarning, 30f, 1f)]
        public void EveryEnvironmentRetainsDurationsRecoveryAndThirdTurnTriggers(EnvironmentType env, float duration, float recovery)
        {
            var rules = new EnvironmentRuleService();
            Assert.That(rules.GetPrepDuration(env, 30f), Is.EqualTo(duration));
            Assert.That(rules.GetRecoveryRate(env), Is.EqualTo(recovery));
            for (int turn = 0; turn < 5; turn++)
            {
                Assert.That(rules.ShouldApplyKidsEffect(env, turn), Is.EqualTo(env == EnvironmentType.Kids && turn == 3));
                Assert.That(rules.ShouldApplyAmbulanceEffect(env, turn), Is.EqualTo(env == EnvironmentType.Ambulance && turn == 3));
            }
            Assert.That(EnvironmentRuleService.KIDS_STEAL_STAGING_SECONDS, Is.EqualTo(3.2f));
            Assert.That(EnvironmentRuleService.AMBULANCE_BLANKET_STAGING_SECONDS, Is.EqualTo(3f));
        }

        [TestCase(3707)] [TestCase(299)]
        public void EnvironmentPoolUsesExactlyOneDrawInTheCurrentOrder(int seed)
        {
            var expectedPool = new[] { EnvironmentType.SunnyDay, EnvironmentType.CoolBreeze, EnvironmentType.CicadaSong,
                EnvironmentType.Kids, EnvironmentType.Ambulance, EnvironmentType.SummerVacation, EnvironmentType.HeatWaveWarning };
            Random.InitState(seed);
            var rules = new EnvironmentRuleService();
            for (int i = 0; i < 64; i++)
            {
                var before = Random.state;
                var expected = expectedPool[Random.Range(0, expectedPool.Length)];
                var after = Random.state; Random.state = before;
                Assert.That(rules.SelectRandom(), Is.EqualTo(expected));
                Assert.That(Random.state, Is.EqualTo(after));
            }
        }

        [Test]
        public void AmbulanceTiePoliciesRemainDifferentAndMultiSkipsMissingObjects()
        {
            var rules = new EnvironmentRuleService();
            Assert.That(rules.DetermineAmbulanceTarget(20f, 20f), Is.EqualTo(-1));
            Assert.That(rules.DetermineAmbulanceTarget(19f, 20f), Is.EqualTo(0));
            Assert.That(rules.DetermineAmbulanceTarget(20f, 19f), Is.EqualTo(1));
            var a = Player(20f); var b = Player(20f);
            Assert.That(rules.DetermineAmbulanceTargetMulti(new[] { a, b }, null), Is.EqualTo(0));
            Assert.That(rules.DetermineAmbulanceTargetMulti(new[] { null, a, b }, null), Is.EqualTo(1));
            Assert.That(rules.DetermineAmbulanceTargetMulti(new PlayerState[4], null), Is.EqualTo(-1));
        }

        [Test]
        public void DuelTurnResetPreservesWriteOrderingAndNeverRevertsFanUpgrade()
        {
            var players = new[] { Player(40f), Player(-2f) };
            var events = new List<string>();
            for (int i = 0; i < 2; i++)
            {
                int seat = i; var p = players[i];
                p.IsReady.Value = true; p.HasSelectedItem.Value = true; p.IsFanActive.Value = false;
                p.FanSpeed.Value = 2f; p.IsFanUpgraded.Value = true;
                p.HasSelectedItem.OnValueChanged += (_, _) => events.Add(seat + ":selected");
                p.IsReady.OnValueChanged += (_, _) => events.Add(seat + ":ready");
                p.Temperature.OnValueChanged += (_, _) => events.Add(seat + ":temp");
                p.IsFanActive.OnValueChanged += (_, _) => events.Add(seat + ":fan");
            }
            var modifiers = new[] { new PlayerModifiers { ActionNeutralized = true }, new PlayerModifiers { HasExtraAction = true } };
            new RoundLifecycleService().ResetForNewTurn(players[0], players[1], modifiers);
            CollectionAssert.AreEqual(new[] { "0:selected", "1:selected", "0:ready", "1:ready", "0:temp", "1:temp", "0:fan", "1:fan" }, events);
            foreach (var p in players) { Assert.That(p.IsFanUpgraded.Value, Is.True); Assert.That(p.FanSpeed.Value, Is.EqualTo(2f)); }
            foreach (var modifier in modifiers) { Assert.That(modifier.ActionNeutralized || modifier.HasExtraAction, Is.False); Assert.That(modifier.FanSpeedMultiplier, Is.EqualTo(1f)); }
        }

        [Test]
        public void MultiTurnResetClearsModifiersButSkipsGhostAndMissingPlayerState()
        {
            var alive = Player(40f); var ghost = Player(-2f);
            alive.IsReady.Value = true; ghost.IsReady.Value = true;
            ghost.HasSelectedItem.Value = true; ghost.CurrentLifeState.Value = LifeState.Ghost;
            var modifiers = new PlayerModifiers[3];
            for (int i = 0; i < modifiers.Length; i++) modifiers[i].ActionNeutralized = true;
            new RoundLifecycleService().ResetForNewTurn(new[] { alive, ghost, null }, modifiers);
            Assert.That(alive.IsReady.Value, Is.False); Assert.That(alive.Temperature.Value, Is.EqualTo(37f));
            Assert.That(ghost.IsReady.Value && ghost.HasSelectedItem.Value, Is.True);
            Assert.That(ghost.Temperature.Value, Is.EqualTo(-2f));
            foreach (var modifier in modifiers) Assert.That(modifier.ActionNeutralized, Is.False);
        }

        [Test]
        public void DelayedDamageRetainsLostSourceAndSkipsDeadTargets()
        {
            var alive = Player(5f); var ghost = Player(10f); ghost.CurrentLifeState.Value = LifeState.Ghost;
            var buff = new BuffDebuffSystem();
            buff.Schedule(0, EffectType.TempChange, -7f, 1, 2);
            buff.Schedule(1, EffectType.TempChange, -7f, 1, 2);
            buff.BeginMultiTurnStart();
            Assert.That(buff.TryProcessNextDueMulti(new[] { alive, ghost, null }, out var effect), Is.True);
            Assert.That(effect.SourceSeat, Is.EqualTo(2)); Assert.That(effect.TargetSeat, Is.EqualTo(0));
            Assert.That(effect.CausedDeath, Is.True); Assert.That(alive.Temperature.Value, Is.Zero);
            Assert.That(ghost.Temperature.Value, Is.EqualTo(10f));
            Assert.That(buff.TryProcessNextDueMulti(new[] { alive, ghost, null }, out _), Is.False);
        }
    }
}
