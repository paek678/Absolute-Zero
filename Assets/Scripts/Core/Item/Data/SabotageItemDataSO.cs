using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// 사보타주 아이템 (SYSTEM_ARCHITECTURE.md Section 4.1).
    /// 고양이(Reroll), 집게손(Steal), 청테이프(BlockBasic), 레드카드(Neutralize).
    /// </summary>
    [CreateAssetMenu(menuName = "AbsoluteZero/Items/Sabotage Item", fileName = "SabotageItem")]
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
                    ctx.TargetModifiers.BasicItemsBlocked = true; // 다음 턴 적용
                    break;
                case SabotageType.Neutralize:
                    ctx.TargetModifiers.ActionNeutralized = true;
                    break;
            }
        }
    }
}
