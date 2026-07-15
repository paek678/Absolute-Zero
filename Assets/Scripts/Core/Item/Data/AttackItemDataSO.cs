using UnityEngine;

namespace AbsoluteZero.Core.Item.Data
{
    [CreateAssetMenu(fileName = "New Attack Item", menuName = "AbsoluteZero/Items/Attack Item")]
    public class AttackItemDataSO : ItemDataSO
    {
        [Header("Attack")]
        public float Damage;
        public DamageFilter AttackFilter = DamageFilter.Temperature;

        [Header("Special Mode")]
        public bool EqualizeToUserTemp;

        public override void ExecuteEffect(ItemContext ctx)
        {
            if (EqualizeToUserTemp)
            {
                float diff = ctx.Target.Temperature.Value - ctx.User.Temperature.Value;
                if (diff > 0f)
                    ctx.TempSystem.ApplyDamage(ctx.Target, diff, AttackFilter,
                                                ctx.TargetModifiers.ActiveDefense);
                else if (diff < 0f)
                    ctx.TempSystem.ApplyHeal(ctx.Target, -diff);
                return;
            }

            ctx.TempSystem.ApplyDamage(ctx.Target, Damage, AttackFilter,
                                        ctx.TargetModifiers.ActiveDefense);
        }
    }
}
