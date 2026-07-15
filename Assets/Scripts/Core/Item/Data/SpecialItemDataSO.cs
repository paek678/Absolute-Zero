using UnityEngine;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// [기획 확정 2026-07-15] 특수 아이템 (고유 메커니즘).
    /// 십자드라이버: FanSpeedChange, Value 2, TargetsSelf=false — 다음 준비시간 상대 선풍기 강풍(2°/s).
    /// 속마음 타로카드: RevealOpponent — 상대가 선택하고 준비 완료한 아이템을 본다.
    ///   상대가 준비 완료한 뒤에만 사용 가능(CanUse에서 검증). 실제 표시는 UI/코어 연출 몫.
    ///   카드 뽑기(1/3) 판정은 미니게임 영역 — 미니게임 구현 시 처리.
    /// </summary>
    [CreateAssetMenu(menuName = "AbsoluteZero/Items/Special Item", fileName = "SpecialItem")]
    public class SpecialItemDataSO : ItemDataSO
    {
        [Header("Special")]
        public SpecialEffectType SpecialEffect;
        public float EffectValue;          // FanSpeedChange: 새 선풍기 속도 (드라이버 = 2)
        public bool TargetsSelf;           // FanSpeedChange 대상 (false = 상대)
        public int DelayTurns;             // 0 = 즉시, 1+ = BuffDebuffSystem 예약

        public override bool CanUse(ItemContext ctx)
        {
            if (!base.CanUse(ctx)) return false;
            // [기획 확정 07-15] 타로카드는 상대가 선택을 끝낸(준비 완료) 뒤에만 사용 가능
            if (SpecialEffect == SpecialEffectType.RevealOpponent && !ctx.Target.IsReady.Value)
                return false;
            return true;
        }

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
