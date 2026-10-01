using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    [CreateAssetMenu(fileName = "New Sabotage Item", menuName = "AbsoluteZero/Items/Sabotage Item")]
    public class SabotageItemDataSO : ItemDataSO
    {
        [Header("Sabotage")]
        public SabotageType SabotageType;

        public override ItemEffectOutcome ComputeEffect(ItemContext ctx)
        {
            Debug.Log($"[COMBAT] SabotageItem '{ItemName}': P{ctx.UserIndex} → P{ctx.TargetIndex}, type={SabotageType}");

            return ItemEffectCalculations.Sabotage(SabotageType);
        }
    }
}
