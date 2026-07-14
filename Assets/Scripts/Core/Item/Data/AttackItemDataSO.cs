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

        public override void ExecuteEffect(ItemContext ctx)
        {
            // FIX-07: 방어 정보를 TargetModifiers에서 전달
            ctx.TempSystem.ApplyDamage(ctx.Target, Damage, AttackFilter,
                                       ctx.TargetModifiers.ActiveDefense);
        }
    }
}
