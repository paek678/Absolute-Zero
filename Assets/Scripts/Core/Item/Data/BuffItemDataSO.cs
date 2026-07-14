using UnityEngine;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// [기획 확정 2026-07-14] 버프 아이템 (자기 대상, 지연 효과).
    /// 탄산음료: Immediate -5 / Delayed +15, 불닭볶음면: Immediate 0 / Delayed +17.
    /// </summary>
    [CreateAssetMenu(menuName = "AbsoluteZero/Items/Buff Item", fileName = "BuffItem")]
    public class BuffItemDataSO : ItemDataSO
    {
        [Header("Buff (self)")]
        public float ImmediateTempDelta;   // 즉시 적용되는 자기 온도 변화 (탄산음료 -5)
        public float DelayedTempDelta;     // DelayTurns 후 적용되는 자기 온도 변화 (+15, +17)
        public int DelayTurns = 1;

        public override void ExecuteEffect(ItemContext ctx)
        {
            if (!Mathf.Approximately(ImmediateTempDelta, 0f))
            {
                if (ImmediateTempDelta > 0f)
                    ctx.TempSystem.ApplyHeal(ctx.User, ImmediateTempDelta);
                else
                    // 자기 자신에게 가하는 페널티 → 방어 판정 없이 적용 (defense = null)
                    ctx.TempSystem.ApplyDamage(ctx.User, -ImmediateTempDelta, DamageFilter.All, null);
            }

            if (!Mathf.Approximately(DelayedTempDelta, 0f))
                ctx.BuffSystem.Schedule(ctx.UserIndex, EffectType.TempChange, DelayedTempDelta, DelayTurns);
        }
    }
}
