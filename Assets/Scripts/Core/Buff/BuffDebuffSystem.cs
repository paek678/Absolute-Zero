using System.Collections.Generic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;
using UnityEngine;

namespace AbsoluteZero.Core.Buff
{
    public class BuffDebuffSystem
    {
        struct ScheduledEffect
        {
            public int TargetPlayerIndex;
            public EffectType Type;
            public float Value;
            public int TurnsRemaining;
        }

        readonly List<ScheduledEffect> _pending = new();

        public void Schedule(int targetPlayer, EffectType type, float value, int delayTurns)
        {
            Debug.Log($"[COMBAT] BuffSystem.Schedule: P{targetPlayer} {type} value={value} in {delayTurns} turn(s)");
            _pending.Add(new ScheduledEffect
            {
                TargetPlayerIndex = targetPlayer,
                Type = type,
                Value = value,
                TurnsRemaining = delayTurns
            });
        }

        public void ProcessTurnStart(PlayerState p1, PlayerState p2)
        {
            Debug.Log($"[COMBAT] BuffSystem.ProcessTurnStart: {_pending.Count} pending effect(s)");

            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var eff = _pending[i];
                eff.TurnsRemaining--;

                if (eff.TurnsRemaining <= 0)
                {
                    var target = eff.TargetPlayerIndex == 0 ? p1 : p2;
                    Debug.Log($"[COMBAT] BuffSystem: FIRING delayed effect on P{eff.TargetPlayerIndex} — {eff.Type} value={eff.Value}");
                    ApplyEffect(target, eff);
                    _pending.RemoveAt(i);
                }
                else
                {
                    Debug.Log($"[COMBAT] BuffSystem: P{eff.TargetPlayerIndex} {eff.Type}={eff.Value} — {eff.TurnsRemaining} turn(s) remaining");
                    _pending[i] = eff;
                }
            }
        }

        void ApplyEffect(PlayerState target, ScheduledEffect eff)
        {
            float beforeTemp = target.Temperature.Value;
            switch (eff.Type)
            {
                case EffectType.TempChange:
                    target.Temperature.Value = Mathf.Clamp(
                        target.Temperature.Value + eff.Value, 0f, 37f);
                    Debug.Log($"[COMBAT] BuffSystem.ApplyEffect: P{eff.TargetPlayerIndex} TempChange {beforeTemp:F1} → {target.Temperature.Value:F1} (delta={eff.Value})");
                    break;
                case EffectType.FanSpeedChange:
                    if (target.IsFanUpgraded.Value)
                    {
                        Debug.Log($"[COMBAT] BuffSystem.ApplyEffect: P{eff.TargetPlayerIndex} FanSpeed ALREADY upgraded — skipping");
                        break;
                    }
                    float beforeFan = target.FanSpeed.Value;
                    target.FanSpeed.Value = eff.Value;
                    target.IsFanUpgraded.Value = true;
                    Debug.Log($"[COMBAT] BuffSystem.ApplyEffect: P{eff.TargetPlayerIndex} FanSpeed {beforeFan} → {eff.Value} (upgraded)");
                    break;
            }
        }

        public void ClearAll() => _pending.Clear();
    }
}
