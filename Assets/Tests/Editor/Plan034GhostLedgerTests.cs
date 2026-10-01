using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Player;
using NUnit.Framework;
using UnityEditor;
using System;

namespace AbsoluteZero.Tests
{
    public sealed class Plan034GhostLedgerTests
    {
        [Test]
        public void MiniGameView_OnlyAcceptsNewerTicketAcrossAttemptsAndRounds()
        {
            var first = new MiniGameTicket(4, MiniGameType.HugCharacter, 5f, 1,
                3, 7, 2, 40, 120);
            var nextAttempt = new MiniGameTicket(4, MiniGameType.HugCharacter, 5f, 1,
                3, 7, 2, 41, 121);
            var nextRound = new MiniGameTicket(4, MiniGameType.HugCharacter, 5f, 1,
                3, 8, 1, 42, 122);
            var nextMatch = new MiniGameTicket(4, MiniGameType.HugCharacter, 5f, 1,
                4, 1, 1, 1, 1);

            Assert.That(nextAttempt.IsNewerThan(first), Is.True);
            Assert.That(first.IsNewerThan(nextAttempt), Is.False);
            Assert.That(nextAttempt.IsNewerThan(nextAttempt), Is.False);
            Assert.That(nextRound.IsNewerThan(nextAttempt), Is.True);
            Assert.That(nextAttempt.IsNewerThan(nextRound), Is.False);
            Assert.That(nextMatch.IsNewerThan(nextRound), Is.True);
            Assert.That(nextRound.IsNewerThan(nextMatch), Is.False);
        }

        [Test]
        public void Grudge_UsesNextTurnAsCooldown_AndLimitsEachTarget()
        {
            var ledger = new GhostSkillLedger();
            ledger.BeginMatch(1, 4);
            ledger.BeginTurn(1);
            Assert.That(ledger.TryCommitGrudge(2, 0), Is.True);
            Assert.That(ledger.GetGrudgeAvailableTurn(2), Is.EqualTo(3));
            Assert.That(ledger.TryCommitGrudge(3, 0), Is.False);
            Assert.That(ledger.TryCommitGrudge(3, 1), Is.True);
            ledger.BeginTurn(2);
            Assert.That(ledger.TryCommitGrudge(2, 0), Is.False);
            ledger.BeginTurn(3);
            Assert.That(ledger.TryCommitGrudge(2, 0), Is.True);
        }

        [Test]
        public void Possession_IsOncePerMatch_AndTurnMarkerExpires()
        {
            var ledger = new GhostSkillLedger();
            ledger.BeginMatch(7, 4);
            ledger.BeginTurn(1);
            Assert.That(ledger.TryCommitPossession(2, 0), Is.True);
            Assert.That(ledger.IsPossessed(0), Is.True);
            Assert.That(ledger.TryCommitPossession(3, 0), Is.False);
            ledger.BeginTurn(2);
            Assert.That(ledger.IsPossessed(0), Is.False);
            Assert.That(ledger.TryCommitPossession(2, 1), Is.False);
            ledger.BeginRound();
            ledger.BeginTurn(1);
            Assert.That(ledger.TryCommitPossession(2, 1), Is.False);
            Assert.That(ledger.TryCommitPossession(3, 1), Is.True);
            ledger.BeginMatch(8, 4);
            ledger.BeginTurn(1);
            Assert.That(ledger.TryCommitPossession(2, 1), Is.True);
        }

        [Test]
        public void InvalidAndDuplicateCommands_DoNotSpendState()
        {
            var ledger = new GhostSkillLedger();
            ledger.BeginMatch(1, 4);
            ledger.BeginTurn(1);
            Assert.That(ledger.TryCommitPossession(2, 2), Is.False);
            Assert.That(ledger.TryCommitGrudge(2, 9), Is.False);
            Assert.That(ledger.PossessionSpentMask, Is.Zero);
            Assert.That(ledger.GhostDamageMask, Is.Zero);
            Assert.That(ledger.TryCommitPossession(2, 1), Is.True);
            Assert.That(ledger.TryCommitPossession(2, 0), Is.False);
            Assert.That(ledger.PossessionSpentMask, Is.EqualTo(1 << 2));
        }

        [Test]
        public void PossessedAttack_ConsumesOnce_WithoutDamageOrNewEffect()
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(
                "Assets/Data/Items/Random/Attack/IceCream.asset");
            Assert.That(item, Is.Not.Null);
            var snapshot = Snapshot(ItemEffectRuleSnapshot.From(item, 10), 10, 1);
            var result = new CombatResolver().ResolveMultiAction(snapshot,
                new ActionIntent(0, 0, 10, 1, 1));
            Assert.That(result.EventCount, Is.EqualTo(1));
            Assert.That(result.OrderedEvents[0].Type, Is.EqualTo(CombatEventType.SuppressedItemUse));
            Assert.That(result.InventoryChangeCount, Is.EqualTo(1));
            Assert.That(result.InventoryChanges[0].Consumed, Is.True);
            Assert.That(result.TemperatureDeltas, Is.All.EqualTo(0));
            Assert.That(result.ScheduledCount, Is.Zero);
            Assert.That(result.DeadMask, Is.Zero);
        }

        [TestCase("Random/Recovery/HotAmericano", 11)]
        [TestCase("Random/Sabotage/RedCard", 12)]
        [TestCase("Random/Buff/Soda", 13)]
        [TestCase("Basic/Cat", 14)]
        public void PossessedItemCategories_ConsumeWithoutNewEffects(string assetName, short itemId)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDataSO>(
                $"Assets/Data/Items/{assetName}.asset");
            Assert.That(item, Is.Not.Null);
            var snapshot = Snapshot(ItemEffectRuleSnapshot.From(item, itemId), itemId, 1);
            var result = new CombatResolver().ResolveMultiAction(snapshot,
                new ActionIntent(0, 0, itemId, 1, 1));
            Assert.That(result.EventCount, Is.EqualTo(1));
            Assert.That(result.OrderedEvents[0].Type, Is.EqualTo(CombatEventType.SuppressedItemUse));
            Assert.That(result.InventoryChangeCount, Is.EqualTo(1));
            Assert.That(result.InventoryChanges[0].Consumed, Is.True);
            Assert.That(result.TemperatureDeltas, Is.All.EqualTo(0));
            Assert.That(result.ScheduledCount, Is.Zero);
        }

        [Test]
        public void PossessedDefense_ConsumesOnce_WithoutActivatingDefense()
        {
            var item = AssetDatabase.LoadAssetAtPath<DefenseItemDataSO>(
                "Assets/Data/Items/Random/Defense/Mask.asset");
            Assert.That(item, Is.Not.Null);
            var snapshot = Snapshot(ItemEffectRuleSnapshot.From(item, 20), 20, 1);
            var intents = new[] { new ActionIntent(0, 0, 20, 0, 1), ActionIntent.Empty };
            var result = new CombatResolver().ResolveMultiDefenses(snapshot, intents);
            Assert.That(result.EventCount, Is.EqualTo(1));
            Assert.That(result.OrderedEvents[0].Type, Is.EqualTo(CombatEventType.SuppressedItemUse));
            Assert.That(result.InventoryChangeCount, Is.EqualTo(1));
            Assert.That(result.ModifierChanges[0].ActiveDefense.HasValue, Is.False);
        }

        [Test]
        public void PresentationSchedule_ChargesTimeForActionsButNotSuppressedDefense()
        {
            var batch = new CombatResolutionBatchNetData
            {
                SeatCount = 4,
                EventCount = 2,
                Events = new[]
                {
                    new CombatEventNetData { ActorSeat = 0, ItemId = 10,
                        EventType = (byte)CombatEventType.SuppressedItemUse },
                    new CombatEventNetData { ActorSeat = 1, ItemId = 20,
                        EventType = (byte)CombatEventType.SuppressedItemUse }
                },
                MainItemIds = new short[] { 10, 20, -1, -1 }
            };
            var schedule = MultiPresentationSchedule.Build(batch,
                id => id == 10 ? 4f : 1f, id => id == 20);
            Assert.That(schedule.ActionCount, Is.EqualTo(1));
            Assert.That(schedule.HasAction(0), Is.True);
            Assert.That(schedule.HasAction(1), Is.False);
            Assert.That(schedule.ActionDurationSeconds(0), Is.EqualTo(4f));
            Assert.That(schedule.ActionDurationSeconds(1), Is.Zero);
            Assert.That(schedule.BarrierBudgetSeconds, Is.EqualTo(11f).Within(0.01f));

            batch.EventCount = 1;
            batch.Events[0] = batch.Events[1];
            schedule = MultiPresentationSchedule.Build(batch, _ => 1f, _ => true);
            Assert.That(schedule.ActionCount, Is.Zero);
            Assert.That(schedule.BarrierBudgetSeconds, Is.EqualTo(5.5f).Within(0.01f));
        }

        [Test]
        public void PresentationSchedule_MixedEffectsShareConfiguredDurations()
        {
            var batch = new CombatResolutionBatchNetData
            {
                SeatCount = 4,
                EventCount = 3,
                Events = new[]
                {
                    new CombatEventNetData { ActorSeat = 0, ItemId = 10,
                        EventType = (byte)CombatEventType.MainEffect },
                    new CombatEventNetData { ActorSeat = 1, ItemId = 11,
                        EventType = (byte)CombatEventType.SuppressedItemUse },
                    new CombatEventNetData { ActorSeat = 2, ItemId = 20,
                        EventType = (byte)CombatEventType.SuppressedItemUse }
                },
                MainItemIds = new short[] { 10, 11, 20, -1 }
            };
            var schedule = MultiPresentationSchedule.Build(batch,
                id => id == 10 ? 4.25f : id == 20 ? 7f : 1.5f,
                id => id == 20);

            Assert.That(schedule.ActionCount, Is.EqualTo(2));
            Assert.That(schedule.ActionDurationSeconds(0), Is.EqualTo(4.25f));
            Assert.That(schedule.ActionDurationSeconds(1), Is.EqualTo(3f));
            Assert.That(schedule.ActionDurationSeconds(2), Is.Zero);
            Assert.That(schedule.BarrierBudgetSeconds, Is.EqualTo(16.05f).Within(0.01f));
        }

        static MatchCombatSnapshot Snapshot(ItemEffectRuleSnapshot rule, short itemId,
            byte possessedMask)
        {
            var slots = new[]
            {
                new InventorySnapshot(0, new[] { new SlotSnapshot(itemId, false, 1) }),
                new InventorySnapshot(1, Array.Empty<SlotSnapshot>())
            };
            return new MatchCombatSnapshot(new[] { 20f, 20f }, new[] { 20f, 20f },
                new PlayerModifiers[2], new[] { LifeState.Alive, LifeState.Alive }, slots,
                Array.Empty<ScheduledEffectSnapshot>(), new int[2], new[] { true, true },
                EnvironmentType.None, default, new[] { rule }, possessedMask);
        }
    }
}
