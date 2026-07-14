using UnityEngine;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Combat
{
    /// <summary>
    /// 온도 계산 시스템 (SYSTEM_ARCHITECTURE.md Section 3 — 서버 전용 C# 클래스).
    /// ※ 공유 계약 타입: 코어 시스템(동업자) 병합 시 이 파일이 기준 정의.
    /// 모든 메서드는 서버에서만 호출할 것 (NetworkVariable write 발생).
    /// </summary>
    public class TemperatureSystem
    {
        public const float MAX_TEMP = 37f;
        public const float MIN_TEMP = 0f;
        public const float DEFAULT_FAN_SPEED = 1f;
        public const float DEFAULT_RECOVERY_RATE = 1f;

        // ── 매 프레임 서버에서 호출 ──

        /// <summary>PrepPhase 중 선풍기 가동: 온도 감소</summary>
        public void TickFan(PlayerState player, float deltaTime)
        {
            if (!player.IsFanActive.Value) return;

            float decrease = player.FanSpeed.Value * deltaTime;
            float newTemp = Mathf.Max(MIN_TEMP, player.Temperature.Value - decrease);
            player.Temperature.Value = newTemp;
        }

        /// <summary>Ready 후 회복: 온도 증가</summary>
        public void TickRecovery(PlayerState player, float deltaTime, float recoveryRate)
        {
            if (player.IsFanActive.Value) return;  // 팬 가동 중이면 회복 안함
            if (!player.IsReady.Value) return;      // Ready 안 했으면 회복 안함

            float increase = recoveryRate * deltaTime;
            float newTemp = Mathf.Min(MAX_TEMP, player.Temperature.Value + increase);
            // ※ MAX_TEMP 캡 여부는 Q9 대기 — 현재 37° 캡 적용
            player.Temperature.Value = newTemp;
        }

        // ── 아이템 효과에 의한 즉시 변경 ──

        /// <summary>
        /// 데미지 적용 (방어 계산 포함). 실제 적용된 데미지를 반환.
        /// FIX-07: PlayerState._modifiers는 private → 방어 정보를 파라미터로 전달
        /// FIX-08: 방어 판정은 여기서만 수행 (CombatResolver.ApplyDefense는 설정만)
        /// </summary>
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

        /// <summary>힐 적용 (37° 캡)</summary>
        public void ApplyHeal(PlayerState target, float amount)
        {
            target.Temperature.Value = Mathf.Min(MAX_TEMP, target.Temperature.Value + amount);
        }

        /// <summary>사망 체크 (0° 도달)</summary>
        public bool IsDead(PlayerState player) => player.Temperature.Value <= MIN_TEMP;

        // ── 구간 판정 ──

        static readonly float[] THRESHOLDS = { 30f, 20f, 10f };
        static readonly int[] GRANTS = { 1, 2, 3 };

        /// <summary>구간 통과 시 랜덤 아이템 지급 — 1회성 (서버 전용)</summary>
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
