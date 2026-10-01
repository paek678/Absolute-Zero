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

        public override ItemEffectOutcome ComputeEffect(ItemContext ctx)
        {
            Debug.Log($"[COMBAT] AttackItem '{ItemName}': P{ctx.UserIndex} → P{ctx.TargetIndex}, damage={Damage}, filter={AttackFilter}, equalize={EqualizeToUserTemp}");
            float userTemperature = EqualizeToUserTemp ? ctx.User.Temperature.Value : 0f;
            float targetTemperature = EqualizeToUserTemp ? ctx.Target.Temperature.Value : 0f;
            return ItemEffectCalculations.Attack(Damage, AttackFilter, EqualizeToUserTemp,
                userTemperature, targetTemperature,
                !EqualizeToUserTemp || targetTemperature > userTemperature ? ctx.TargetModifiers.ActiveDefense : null);
        }
    }
}
