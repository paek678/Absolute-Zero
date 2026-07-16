using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    [CreateAssetMenu(fileName = "New Sabotage Item", menuName = "AbsoluteZero/Items/Sabotage Item")]
    public class SabotageItemDataSO : ItemDataSO
    {
        [Header("Sabotage")]
        public SabotageType SabotageType;

        public override void ExecuteEffect(ItemContext ctx)
        {
            Debug.Log($"[COMBAT] SabotageItem '{ItemName}': P{ctx.UserIndex} → P{ctx.TargetIndex}, type={SabotageType}");

            switch (SabotageType)
            {
                case SabotageType.Reroll:
                    ctx.TargetInventory.RerollAllRandom(ctx.DropTable);
                    Debug.Log($"[COMBAT] SabotageItem '{ItemName}': P{ctx.TargetIndex} random items rerolled");
                    break;
                case SabotageType.Steal:
                    ctx.TargetInventory.StealRandomItem(ctx.UserInventory);
                    Debug.Log($"[COMBAT] SabotageItem '{ItemName}': P{ctx.UserIndex} stole from P{ctx.TargetIndex}");
                    break;
                case SabotageType.BlockBasic:
                    ctx.TargetModifiers.BasicItemsBlocked = true;
                    Debug.Log($"[COMBAT] SabotageItem '{ItemName}': P{ctx.TargetIndex} basic items blocked");
                    break;
                case SabotageType.Neutralize:
                    ctx.TargetModifiers.ActionNeutralized = true;
                    Debug.Log($"[COMBAT] SabotageItem '{ItemName}': P{ctx.TargetIndex} main action neutralized");
                    break;
            }
        }
    }
}
