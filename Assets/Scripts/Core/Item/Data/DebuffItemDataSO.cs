using UnityEngine;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// [기획 확정 2026-07-14] 디버프 아이템 (상대 대상, 지연 효과).
    /// 삼계탕: Immediate +3 (상대 온도 상승) / Delayed -7 (다음 턴 하락), Filter = Food.
    /// 마스크 규칙: 대상이 필터 일치 방어(마스크=Food) 활성 중이면 즉시/지연 효과 전체 무효.
    /// </summary>
    [CreateAssetMenu(menuName = "AbsoluteZero/Items/Debuff Item", fileName = "DebuffItem")]
    public class DebuffItemDataSO : ItemDataSO
    {
        [Header("Debuff (opponent)")]
        public float ImmediateTempDelta;   // 즉시 적용되는 상대 온도 변화 (삼계탕 +3)
        public float DelayedTempDelta;     // DelayTurns 후 적용되는 상대 온도 변화 (-7)
        public int DelayTurns = 1;
        public DamageFilter AttackFilter = DamageFilter.Food;  // 방어 필터 매칭용 (삼계탕 = Food)

        public override void ExecuteEffect(ItemContext ctx)
        {
            // [기획 확정] 대상의 활성 방어 필터가 이 아이템 필터와 일치하면 아무 효과 없음
            // (삼계탕 vs 마스크 = 완전 무효 — +3 상승도, 다음 턴 -7도 적용 안 됨)
            // ※ 현재 디버프 콘텐츠는 전부/전무 차단만 존재 (부분 차단 디버프 등장 시 재검토)
            var defense = ctx.TargetModifiers.ActiveDefense;
            if (defense.HasValue &&
                (defense.Value.Filter == AttackFilter || defense.Value.Filter == DamageFilter.All))
                return;

            if (!Mathf.Approximately(ImmediateTempDelta, 0f))
            {
                if (ImmediateTempDelta > 0f)
                    ctx.TempSystem.ApplyHeal(ctx.Target, ImmediateTempDelta);
                else
                    // 방어 판정은 위에서 전체 무효로 처리 완료 → 여기서는 통과 (null)
                    ctx.TempSystem.ApplyDamage(ctx.Target, -ImmediateTempDelta, AttackFilter, null);
            }

            if (!Mathf.Approximately(DelayedTempDelta, 0f))
                ctx.BuffSystem.Schedule(ctx.TargetIndex, EffectType.TempChange, DelayedTempDelta, DelayTurns);
        }
    }
}
