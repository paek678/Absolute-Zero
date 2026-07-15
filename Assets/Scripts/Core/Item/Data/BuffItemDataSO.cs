using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    [CreateAssetMenu(fileName = "New Buff Item", menuName = "AbsoluteZero/Items/Buff Item")]
    public class BuffItemDataSO : ItemDataSO
    {
        [Header("Buff (self)")]
        public float ImmediateTempDelta;
        public float DelayedTempDelta;
        public int DelayTurns = 1;

        public override void ExecuteEffect(ItemContext ctx)
        {
            if (!Mathf.Approximately(ImmediateTempDelta, 0f))
            {
                if (ImmediateTempDelta > 0f)
                    ctx.TempSystem.ApplyHeal(ctx.User, ImmediateTempDelta);
                else
                    ctx.TempSystem.ApplyDamage(ctx.User, -ImmediateTempDelta, DamageFilter.All, null);
            }

            if (!Mathf.Approximately(DelayedTempDelta, 0f))
                ctx.BuffSystem.Schedule(ctx.UserIndex, EffectType.TempChange, DelayedTempDelta, DelayTurns);
        }
    }
}
