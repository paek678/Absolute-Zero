using UnityEngine;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// 공격 아이템 (SYSTEM_ARCHITECTURE.md Section 4.1).
    /// 부채(3°), 손풍기(4°), 아이스크림(5°), 아.아(5°), 물총(7°), 안아줘요(=내 온도) 등.
    /// </summary>
    [CreateAssetMenu(menuName = "AbsoluteZero/Items/Attack Item", fileName = "AttackItem")]
    public class AttackItemDataSO : ItemDataSO
    {
        [Header("Attack")]
        public float Damage;                        // 3, 4, 5, 7
        public DamageFilter AttackFilter = DamageFilter.Temperature;

        [Header("Special Mode")]
        // [기획 확정 07-15] 안아줘요 티셔츠: 상대 온도가 내 온도와 "같아진다" — 양방향
        // (내가 더 차가우면 상대가 내려오고, 내가 더 따뜻하면 상대가 올라온다)
        public bool EqualizeToUserTemp;

        public override void ExecuteEffect(ItemContext ctx)
        {
            if (EqualizeToUserTemp)
            {
                float diff = ctx.Target.Temperature.Value - ctx.User.Temperature.Value;
                if (diff > 0f)
                    // 내리는 방향은 온도 공격 — 방어(바람막이) 판정 적용
                    ctx.TempSystem.ApplyDamage(ctx.Target, diff, AttackFilter,
                                               ctx.TargetModifiers.ActiveDefense);
                else if (diff < 0f)
                    // 올리는 방향은 회복 취급 — 방어와 무관
                    ctx.TempSystem.ApplyHeal(ctx.Target, -diff);
                return;
            }

            // FIX-07: 방어 정보를 TargetModifiers에서 전달
            ctx.TempSystem.ApplyDamage(ctx.Target, Damage, AttackFilter,
                                       ctx.TargetModifiers.ActiveDefense);
        }
    }
}
