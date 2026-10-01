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

        public override TargetMode GetTargetMode() => TargetMode.Self;

        public override ItemEffectOutcome ComputeEffect(ItemContext ctx)
        {
            Debug.Log($"[COMBAT] BuffItem '{ItemName}': P{ctx.UserIndex} self-buff, immediate={ImmediateTempDelta}, delayed={DelayedTempDelta} in {DelayTurns}t");

            return ItemEffectCalculations.Buff(ImmediateTempDelta, DelayedTempDelta, DelayTurns, ctx.UserIndex);
        }
    }
}
