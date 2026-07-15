using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    [CreateAssetMenu(fileName = "New Recovery Item", menuName = "AbsoluteZero/Items/Recovery Item")]
    public class RecoveryItemDataSO : ItemDataSO
    {
        [Header("Recovery")]
        public float[] HealPerUse = { 7f };

        public override void ExecuteEffect(ItemContext ctx)
        {
            int useIndex = MaxUses > 0 ? MaxUses - ctx.UserSlot.RemainingUses : 0;
            float heal = HealPerUse[Mathf.Clamp(useIndex, 0, HealPerUse.Length - 1)];
            ctx.TempSystem.ApplyHeal(ctx.User, heal);
        }
    }
}
