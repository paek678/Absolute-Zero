using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    [CreateAssetMenu(fileName = "New Special Item", menuName = "AbsoluteZero/Items/Special Item")]
    public class SpecialItemDataSO : ItemDataSO
    {
        [Header("Special")]
        public SpecialEffectType SpecialEffect;
        public float EffectValue;
        public bool TargetsSelf;
        public int DelayTurns;

        public override bool CanUse(ItemContext ctx)
        {
            if (!base.CanUse(ctx)) return false;
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
