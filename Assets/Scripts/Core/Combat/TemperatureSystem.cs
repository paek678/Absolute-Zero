using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    public class TemperatureSystem
    {
        public const float MAX_TEMP = 37f;
        public const float MIN_TEMP = 0f;
        public const float DEFAULT_FAN_SPEED = 1f;
        public const float DEFAULT_RECOVERY_RATE = 1f;
        public const float TICK_INTERVAL = 1f;

        float _tickTimer;

        public void ResetTimer()
        {
            _tickTimer = 0f;
        }

        public void Accumulate(float deltaTime)
        {
            _tickTimer += deltaTime;
        }

        public bool ConsumeTick()
        {
            if (_tickTimer >= TICK_INTERVAL)
            {
                _tickTimer -= TICK_INTERVAL;
                return true;
            }
            return false;
        }

        public void ApplyFanTick(PlayerState player)
        {
            if (!player.IsFanActive.Value) return;
            float newTemp = Mathf.Max(MIN_TEMP, player.Temperature.Value - player.FanSpeed.Value);
            player.Temperature.Value = newTemp;
        }

        public void ApplyRecoveryTick(PlayerState player, float recoveryRate)
        {
            if (player.IsFanActive.Value) return;
            if (!player.IsReady.Value) return;
            float newTemp = Mathf.Min(MAX_TEMP, player.Temperature.Value + recoveryRate);
            player.Temperature.Value = newTemp;
        }

        public float ApplyDamage(PlayerState target, float rawDamage, DamageFilter attackFilter,
                                  DefenseInfo? activeDefense)
        {
            float actualDamage = rawDamage;

            if (activeDefense.HasValue)
            {
                var defense = activeDefense.Value;
                if (defense.Filter == attackFilter || defense.Filter == DamageFilter.All)
                {
                    actualDamage = Mathf.Max(0f, rawDamage - defense.BlockAmount);
                }
            }

            target.Temperature.Value = Mathf.Max(MIN_TEMP, target.Temperature.Value - actualDamage);
            return actualDamage;
        }

        public void ApplyHeal(PlayerState target, float amount)
        {
            target.Temperature.Value = Mathf.Min(MAX_TEMP, target.Temperature.Value + amount);
        }

        public bool IsDead(PlayerState player) => player.Temperature.Value <= MIN_TEMP;

        static readonly float[] THRESHOLDS = { 30f, 20f, 10f };
        static readonly int[] GRANTS = { 1, 2, 3 };

        public void CheckThresholds(PlayerState player, PlayerInventory inventory,
                                     bool[] thresholdGranted, ItemDropTable dropTable)
        {
            for (int i = 0; i < THRESHOLDS.Length; i++)
            {
                if (!thresholdGranted[i] && player.Temperature.Value <= THRESHOLDS[i])
                {
                    thresholdGranted[i] = true;
                    inventory.GrantRandomItems(GRANTS[i], dropTable);
                }
            }
        }
    }
}
