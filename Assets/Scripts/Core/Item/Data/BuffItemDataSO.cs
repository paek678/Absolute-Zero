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
            Debug.Log($"[COMBAT] BuffItem '{ItemName}': P{ctx.UserIndex} self-buff, immediate={ImmediateTempDelta}, delayed={DelayedTempDelta} in {DelayTurns}t");

            if (!Mathf.Approximately(ImmediateTempDelta, 0f))
            {
                if (ImmediateTempDelta > 0f)
                {
                    Debug.Log($"[COMBAT] BuffItem '{ItemName}': immediate HEAL self +{ImmediateTempDelta}");
                    ctx.TempSystem.ApplyHeal(ctx.User, ImmediateTempDelta);
                }
                else
                {
                    Debug.Log($"[COMBAT] BuffItem '{ItemName}': immediate DAMAGE self {ImmediateTempDelta}");
                    ctx.TempSystem.ApplyDamage(ctx.User, -ImmediateTempDelta, DamageFilter.All, null);
                }
            }

            if (!Mathf.Approximately(DelayedTempDelta, 0f))
            {
                Debug.Log($"[COMBAT] BuffItem '{ItemName}': scheduled delayed={DelayedTempDelta} on P{ctx.UserIndex} in {DelayTurns} turn(s)");
                ctx.BuffSystem.Schedule(ctx.UserIndex, EffectType.TempChange, DelayedTempDelta, DelayTurns);
            }
        }
    }
}
