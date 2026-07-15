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
            switch (SabotageType)
            {
                case SabotageType.Reroll:
                    ctx.TargetInventory.RerollAllRandom(ctx.DropTable);
                    break;
                case SabotageType.Steal:
                    ctx.TargetInventory.StealRandomItem(ctx.UserInventory);
                    break;
                case SabotageType.BlockBasic:
                    ctx.TargetModifiers.BasicItemsBlocked = true;
                    break;
                case SabotageType.Neutralize:
                    ctx.TargetModifiers.ActionNeutralized = true;
                    break;
            }
        }
    }
}
