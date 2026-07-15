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
            var defense = ctx.TargetModifiers.ActiveDefense;
            if (defense.HasValue &&
                (defense.Value.Filter == AttackFilter || defense.Value.Filter == DamageFilter.All))
                return;

            if (!Mathf.Approximately(ImmediateTempDelta, 0f))
            {
                if (ImmediateTempDelta > 0f)
                    ctx.TempSystem.ApplyHeal(ctx.Target, ImmediateTempDelta);
                else
                    ctx.TempSystem.ApplyDamage(ctx.Target, -ImmediateTempDelta, AttackFilter, null);
            }

            if (!Mathf.Approximately(DelayedTempDelta, 0f))
                ctx.BuffSystem.Schedule(ctx.TargetIndex, EffectType.TempChange, DelayedTempDelta, DelayTurns);
        }
    }
}
