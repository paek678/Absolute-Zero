using UnityEngine;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// 방어 아이템 (SYSTEM_ARCHITECTURE.md Section 4.1).
    /// 바람막이(4° 차단), 마스크(100% 차단).
    /// FIX-08: Main 방어는 CombatResolver.ApplyDefense에서 설정되며 ExecuteEffect는
    /// CombatResolver 경로에서 스킵됨 — 이 메서드는 그 외 경로(Sub 방어 등) 대비 동일 로직 유지.
    /// </summary>
    [CreateAssetMenu(menuName = "AbsoluteZero/Items/Defense Item", fileName = "DefenseItem")]
    public class DefenseItemDataSO : ItemDataSO
    {
        [Header("Defense")]
        public float BlockAmount;                   // 4 (Windbreaker), float.MaxValue (Mask)
        public DamageFilter Filter;                 // Temperature, Food

        public override void ExecuteEffect(ItemContext ctx)
        {
            ctx.UserModifiers.ActiveDefense = new DefenseInfo
            {
                Filter = this.Filter,
                BlockAmount = this.BlockAmount
            };
        }
    }
}
