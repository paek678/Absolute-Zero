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

        public override void ExecuteEffect(ItemContext ctx)
        {
            Debug.Log($"[COMBAT] DebuffItem '{ItemName}': P{ctx.UserIndex} → P{ctx.TargetIndex}, immediate={ImmediateTempDelta}, delayed={DelayedTempDelta} in {DelayTurns}t, filter={AttackFilter}");

            var defense = ctx.TargetModifiers.ActiveDefense;
            if (defense.HasValue &&
                (defense.Value.Filter == AttackFilter || defense.Value.Filter == DamageFilter.All))
            {
                Debug.Log($"[COMBAT] DebuffItem '{ItemName}': FULLY BLOCKED by defense (defFilter={defense.Value.Filter})");
                return;
            }

            if (!Mathf.Approximately(ImmediateTempDelta, 0f))
            {
                if (ImmediateTempDelta > 0f)
                {
                    Debug.Log($"[COMBAT] DebuffItem '{ItemName}': immediate HEAL target +{ImmediateTempDelta}");
                    ctx.TempSystem.ApplyHeal(ctx.Target, ImmediateTempDelta);
                }
                else
                {
                    Debug.Log($"[COMBAT] DebuffItem '{ItemName}': immediate DAMAGE target {ImmediateTempDelta}");
                    ctx.TempSystem.ApplyDamage(ctx.Target, -ImmediateTempDelta, AttackFilter, null);
                }
            }

            if (!Mathf.Approximately(DelayedTempDelta, 0f))
            {
                Debug.Log($"[COMBAT] DebuffItem '{ItemName}': scheduled delayed={DelayedTempDelta} on P{ctx.TargetIndex} in {DelayTurns} turn(s)");
                ctx.BuffSystem.Schedule(ctx.TargetIndex, EffectType.TempChange, DelayedTempDelta, DelayTurns);
            }
        }
    }
}
