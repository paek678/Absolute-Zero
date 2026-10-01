using System.Collections.Generic;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    // Value calculations only. Callers retain mode-specific ordering, defense
    // application, consumption, source attribution and authoritative writes.
    internal static class ItemEffectCalculations
    {
        internal static float DamageAfterBlock(float damage, float blocked) => Mathf.Max(0f, damage - blocked);
        internal static float EqualizationDelta(float userTemperature, float targetTemperature)
            => userTemperature - targetTemperature;

        internal static ItemEffectOutcome Attack(float damage, DamageFilter filter, bool equalize,
            float userTemperature, float targetTemperature, DefenseInfo? targetDefense)
        {
            var outcome = new ItemEffectOutcome();
            if (equalize)
            {
                float delta = EqualizationDelta(userTemperature, targetTemperature);
                if (delta < 0f)
                {
                    outcome.TargetDamage = -delta;
                    outcome.TargetDamageFilter = filter;
                    outcome.TargetDefenseCheck = targetDefense;
                }
                else if (delta > 0f) outcome.TargetHeal = delta;
                return outcome;
            }
            outcome.TargetDamage = damage;
            outcome.TargetDamageFilter = filter;
            outcome.TargetDefenseCheck = targetDefense;
            return outcome;
        }

        internal static int RecoveryUseIndex(int maximumUses, byte remainingUses)
            => maximumUses > 0 ? maximumUses - remainingUses : 0;

        internal static float RecoveryAtIndex(IReadOnlyList<float> amounts, int useIndex)
            => amounts[Mathf.Clamp(useIndex, 0, amounts.Count - 1)];

        internal static DefenseInfo Defense(float amount, DamageFilter filter, short itemId = 0)
            => new() { ItemId = itemId, Filter = filter, BlockAmount = amount };

        internal static ItemEffectOutcome Sabotage(SabotageType type) => new()
        {
            InventoryAction = type switch
            {
                SabotageType.Reroll => InventoryMutationType.RerollTarget,
                SabotageType.Steal => InventoryMutationType.StealFromTarget,
                _ => InventoryMutationType.None
            },
            BlockTargetBasics = type == SabotageType.BlockBasic,
            NeutralizeTarget = type == SabotageType.Neutralize
        };

        internal static ItemEffectOutcome Special(SpecialEffectType type, float value, bool self,
            int delayTurns, int userIndex, int targetIndex)
        {
            var outcome = new ItemEffectOutcome();
            switch (type)
            {
                case SpecialEffectType.FanSpeedChange:
                    if (delayTurns > 0)
                    {
                        outcome.HasScheduledEffect = true;
                        outcome.ScheduledTargetIndex = self ? userIndex : targetIndex;
                        outcome.ScheduledType = EffectType.FanSpeedChange;
                        outcome.ScheduledValue = value;
                        outcome.ScheduledDelayTurns = delayTurns;
                    }
                    else if (self)
                    {
                        outcome.WriteUserFanSpeed = true;
                        outcome.UserFanSpeedValue = value;
                    }
                    else
                    {
                        outcome.WriteTargetFanSpeed = true;
                        outcome.TargetFanSpeedValue = value;
                    }
                    break;
                case SpecialEffectType.ExtraAction: outcome.GrantExtraAction = true; break;
                case SpecialEffectType.RevealOpponent: outcome.RevealOpponent = true; break;
            }
            return outcome;
        }

        internal static ItemEffectOutcome Buff(float immediate, float delayed, int delayTurns, int userIndex)
            => TemperatureEffect(immediate, delayed, delayTurns, userIndex, true, DamageFilter.All);

        // Duel intentionally blocks the entire debuff on any matching defense.
        // Multi's finite-block policy remains in its resolver; these are not interchangeable.
        internal static ItemEffectOutcome DuelDebuff(float immediate, float delayed, int delayTurns,
            int targetIndex, DamageFilter filter, DefenseInfo? defense)
        {
            if (defense.HasValue && (defense.Value.Filter == filter || defense.Value.Filter == DamageFilter.All))
                return new ItemEffectOutcome { Blocked = true, TargetDefenseCheck = defense };
            return TemperatureEffect(immediate, delayed, delayTurns, targetIndex, false, filter);
        }

        static ItemEffectOutcome TemperatureEffect(float immediate, float delayed, int delayTurns,
            int targetIndex, bool self, DamageFilter filter)
        {
            var outcome = new ItemEffectOutcome();
            if (!Mathf.Approximately(immediate, 0f))
            {
                if (immediate > 0f)
                {
                    if (self) outcome.UserHeal = immediate;
                    else outcome.TargetHeal = immediate;
                }
                else if (self)
                {
                    outcome.UserDamage = -immediate;
                    outcome.UserDamageFilter = filter;
                }
                else
                {
                    outcome.TargetDamage = -immediate;
                    outcome.TargetDamageFilter = filter;
                }
            }
            if (!Mathf.Approximately(delayed, 0f))
            {
                outcome.HasScheduledEffect = true;
                outcome.ScheduledTargetIndex = targetIndex;
                outcome.ScheduledType = EffectType.TempChange;
                outcome.ScheduledValue = delayed;
                outcome.ScheduledDelayTurns = delayTurns;
            }
            return outcome;
        }
    }
}
