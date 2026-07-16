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
            Debug.Log($"[COMBAT] SpecialItem '{ItemName}': P{ctx.UserIndex}, effect={SpecialEffect}, value={EffectValue}, targetsSelf={TargetsSelf}");

            switch (SpecialEffect)
            {
                case SpecialEffectType.FanSpeedChange:
                    int targetIdx = TargetsSelf ? ctx.UserIndex : ctx.TargetIndex;
                    if (DelayTurns > 0)
                    {
                        Debug.Log($"[COMBAT] SpecialItem '{ItemName}': scheduled FanSpeed={EffectValue} on P{targetIdx} in {DelayTurns}t");
                        ctx.BuffSystem.Schedule(targetIdx, EffectType.FanSpeedChange, EffectValue, DelayTurns);
                    }
                    else
                    {
                        Debug.Log($"[COMBAT] SpecialItem '{ItemName}': immediate FanSpeed={EffectValue} on P{targetIdx}");
                        (TargetsSelf ? ctx.User : ctx.Target).FanSpeed.Value = EffectValue;
                    }
                    break;

                case SpecialEffectType.ExtraAction:
                    Debug.Log($"[COMBAT] SpecialItem '{ItemName}': P{ctx.UserIndex} granted ExtraAction");
                    ctx.UserModifiers.HasExtraAction = true;
                    break;

                case SpecialEffectType.RevealOpponent:
                    Debug.Log($"[COMBAT] SpecialItem '{ItemName}': P{ctx.UserIndex} revealed opponent");
                    ctx.UserModifiers.OpponentRevealed = true;
                    break;
            }
        }
    }
}
