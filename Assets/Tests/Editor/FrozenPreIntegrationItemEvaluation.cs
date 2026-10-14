#if UNITY_EDITOR
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Solo;
using UnityEngine;
namespace AbsoluteZero.Tests
{
    // Target b74ec19: retained test oracle, never used by gameplay or the driver.
    internal static class FrozenPreIntegrationItemEvaluation
    {
        internal static PlaytestItemEstimate Estimate(ItemEffectRuleSnapshot rule, ItemSlotNetData slot,
            byte target, double delay, float ownTemperature, float opponentTemperature, bool permanent)
        {
            float damage = rule.EqualizeToUserTemp ? Mathf.Max(0, opponentTemperature - ownTemperature) : Mathf.Max(0, rule.BaseDamage);
            float heal = 0;
            if (rule.HealPerUse?.Length > 0)
                heal = rule.HealPerUse[Mathf.Clamp(rule.MaxUses - slot.RemainingUses, 0, rule.HealPerUse.Length - 1)];
            if (rule.IsSelfTarget) heal += Mathf.Max(0, rule.ImmediateTempDelta);
            else damage += Mathf.Max(0, -rule.ImmediateTempDelta);
            float utility = rule.InventoryAction != InventoryMutationType.None || rule.BlocksTargetBasics || rule.NeutralizesTarget ? 2 : 0;
            if (rule.WritesFanSpeed || rule.WritesTargetFanSpeed || rule.HasScheduledEffect) utility += 1;
            // Estimates never promise hidden-opponent defense or delayed-effect outcomes.
            return new PlaytestItemEstimate(slot.CopyId, target, slot.ItemId, delay, damage,
                Mathf.Min(Mathf.Max(0, 37 - ownTemperature), Mathf.Max(0, heal)),
                rule.IsDefense ? Mathf.Min(10, Mathf.Max(0, rule.DefenseReduction)) : 0, utility, permanent);
        }
    }
}
#endif
