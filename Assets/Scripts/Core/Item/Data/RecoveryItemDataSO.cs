using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// 회복 아이템 (SYSTEM_ARCHITECTURE.md Section 4.1).
    /// 따뜻한 차([7]), 뜨.아([5]), 핫팩([10]), 스마트폰([3,5,7] — 사용 횟수별 증가).
    /// </summary>
    [CreateAssetMenu(menuName = "AbsoluteZero/Items/Recovery Item", fileName = "RecoveryItem")]
    public class RecoveryItemDataSO : ItemDataSO
    {
        [Header("Recovery")]
        public float[] HealPerUse;                  // [7], [5], [10], [3,5,7]

        public override void ExecuteEffect(ItemContext ctx)
        {
            // FIX-13: ConsumeItem이 ExecuteEffect 이후 호출되므로
            // RemainingUses는 아직 감소 전 상태 → (MaxUses - Remaining) = 이전 사용 횟수
            // 예: 스마트폰 MaxUses=3, 첫 사용 시 Remaining=3 → useIndex=0 ✓
            //     두 번째 사용 시 Remaining=2 → useIndex=1 ✓
            int useIndex = MaxUses - ctx.UserSlot.RemainingUses;
            float heal = HealPerUse[Mathf.Min(useIndex, HealPerUse.Length - 1)];
            ctx.TempSystem.ApplyHeal(ctx.User, heal);
        }
        // ※ ConsumeItem은 반드시 ExecuteEffect 이후 호출해야 useIndex가 정확함
    }
}
