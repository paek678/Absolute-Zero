using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    [CreateAssetMenu(fileName = "New Recovery Item", menuName = "AbsoluteZero/Items/Recovery Item")]
    public class RecoveryItemDataSO : ItemDataSO
    {
        [Header("Recovery")]
        public float[] HealPerUse = { 7f };

        public override TargetMode GetTargetMode() => TargetMode.Self;

        public override ItemEffectOutcome ComputeEffect(ItemContext ctx)
        {
            int useIndex = ItemEffectCalculations.RecoveryUseIndex(MaxUses, ctx.UserSlot.RemainingUses);
            float heal = ItemEffectCalculations.RecoveryAtIndex(HealPerUse, useIndex);
            Debug.Log($"[COMBAT] RecoveryItem '{ItemName}': P{ctx.UserIndex} heal={heal}, useIndex={useIndex}, remaining={ctx.UserSlot.RemainingUses}/{MaxUses}");
            return new ItemEffectOutcome { UserHeal = heal };
        }
    }
}
