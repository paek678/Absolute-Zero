using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// [기획 확정 2026-07-14] 특수 아이템 (고유 메커니즘).
    /// 십자드라이버: FanSpeedChange, Value 2, TargetsSelf=false (상대 선풍기 강화).
    /// 속마음 타로카드: 3장 중 1장 선택(1/3) — 카드 판정은 미니게임 영역이라 미니게임 구현 시 처리.
    /// 여기서는 성공 시 발동되는 효과(ExtraAction/RevealOpponent)만 정의.
    /// </summary>
    [CreateAssetMenu(menuName = "AbsoluteZero/Items/Special Item", fileName = "SpecialItem")]
    public class SpecialItemDataSO : ItemDataSO
    {
        [Header("Special")]
        public SpecialEffectType SpecialEffect;
        public float EffectValue;          // FanSpeedChange: 새 선풍기 속도 (드라이버 = 2)
        public bool TargetsSelf;           // FanSpeedChange 대상 (false = 상대)
        public int DelayTurns;             // 0 = 즉시, 1+ = BuffDebuffSystem 예약

        public override void ExecuteEffect(ItemContext ctx)
        {
            switch (SpecialEffect)
            {
                case SpecialEffectType.FanSpeedChange:
                    int targetIdx = TargetsSelf ? ctx.UserIndex : ctx.TargetIndex;
                    if (DelayTurns > 0)
                        ctx.BuffSystem.Schedule(targetIdx, EffectType.FanSpeedChange, EffectValue, DelayTurns);
                    else
                        (TargetsSelf ? ctx.User : ctx.Target).FanSpeed.Value = EffectValue;
                    break;

                case SpecialEffectType.ExtraAction:
                    ctx.UserModifiers.HasExtraAction = true;
                    break;

                case SpecialEffectType.RevealOpponent:
                    ctx.UserModifiers.OpponentRevealed = true;
                    break;
            }
        }
    }
}
