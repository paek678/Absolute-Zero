using System.Collections.Generic;
using UnityEngine;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.Core.Buff
{
    /// <summary>
    /// 지연 효과(버프/디버프) 예약 시스템 (SYSTEM_ARCHITECTURE.md Section 7 — 서버 전용).
    /// ※ 공유 계약 타입: 코어 시스템(동업자) 병합 시 이 파일이 기준 정의.
    /// 아이템 사용 시 Schedule()로 예약 → TurnManager가 턴 시작마다 ProcessTurnStart() 호출.
    /// </summary>
    public class BuffDebuffSystem
    {
        struct ScheduledEffect
        {
            public int TargetPlayerIndex;
            public EffectType Type;      // TempChange, FanSpeedChange, BasicBlock
            public float Value;
            public int TurnsRemaining;   // 0 = 이번 턴 시작에 적용
        }

        readonly List<ScheduledEffect> _pending = new();

        /// <summary>효과 예약 (아이템 사용 시 호출)</summary>
        public void Schedule(int targetPlayer, EffectType type, float value, int delayTurns)
        {
            _pending.Add(new ScheduledEffect
            {
                TargetPlayerIndex = targetPlayer,
                Type = type,
                Value = value,
                TurnsRemaining = delayTurns
            });
        }

        /// <summary>턴 시작 시 호출: 예약된 효과 적용/진행</summary>
        public void ProcessTurnStart(PlayerState p1, PlayerState p2)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var eff = _pending[i];
                eff.TurnsRemaining--;

                if (eff.TurnsRemaining <= 0)
                {
                    var target = eff.TargetPlayerIndex == 0 ? p1 : p2;
                    ApplyEffect(target, eff);
                    _pending.RemoveAt(i);
                }
                else
                {
                    _pending[i] = eff;
                }
            }
        }

        void ApplyEffect(PlayerState target, ScheduledEffect eff)
        {
            switch (eff.Type)
            {
                case EffectType.TempChange:
                    target.Temperature.Value = Mathf.Clamp(
                        target.Temperature.Value + eff.Value, 0f, 37f);
                    break;
                case EffectType.FanSpeedChange:
                    target.FanSpeed.Value = eff.Value;
                    break;
                case EffectType.BasicBlock:
                    // 턴 시작 시 PlayerModifiers에서 처리 (TurnManager 소관)
                    break;
            }
        }

        public void ClearAll() => _pending.Clear();
    }
}
