using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    [CreateAssetMenu(fileName = "New Special Item", menuName = "AbsoluteZero/Items/Special Item")]
    public class SpecialItemDataSO : ItemDataSO
    {
        [Header("Special")]
        public SpecialEffectType SpecialEffect;
        public float EffectValue;
        public bool TargetsSelf;
        public int DelayTurns;

        public override TargetMode GetTargetMode()
            => TargetsSelf ? TargetMode.Self : TargetMode.SingleTarget;

        public override bool CanUse(ItemContext ctx)
        {
            if (!base.CanUse(ctx)) return false;
            if (SpecialEffect == SpecialEffectType.RevealOpponent && !ctx.Target.IsReady.Value)
                return false;
            return true;
        }

        public override ItemEffectOutcome ComputeEffect(ItemContext ctx)
        {
            Debug.Log($"[COMBAT] SpecialItem '{ItemName}': P{ctx.UserIndex}, effect={SpecialEffect}, value={EffectValue}, targetsSelf={TargetsSelf}");

            return ItemEffectCalculations.Special(SpecialEffect, EffectValue, TargetsSelf,
                DelayTurns, ctx.UserIndex, ctx.TargetIndex);
        }
    }
}
