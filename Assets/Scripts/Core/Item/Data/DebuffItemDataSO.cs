using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    [CreateAssetMenu(fileName = "New Debuff Item", menuName = "AbsoluteZero/Items/Debuff Item")]
    public class DebuffItemDataSO : ItemDataSO
    {
        [Header("Debuff (opponent)")]
        public float ImmediateTempDelta;
        public float DelayedTempDelta;
        public int DelayTurns = 1;
        public DamageFilter AttackFilter = DamageFilter.Food;

        public override ItemEffectOutcome ComputeEffect(ItemContext ctx)
        {
            Debug.Log($"[COMBAT] DebuffItem '{ItemName}': P{ctx.UserIndex} → P{ctx.TargetIndex}, immediate={ImmediateTempDelta}, delayed={DelayedTempDelta} in {DelayTurns}t, filter={AttackFilter}");

            return ItemEffectCalculations.DuelDebuff(ImmediateTempDelta, DelayedTempDelta, DelayTurns,
                ctx.TargetIndex, AttackFilter, ctx.TargetModifiers.ActiveDefense);
        }
    }
}
