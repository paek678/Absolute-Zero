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
            }
        }

        public void ClearAll() => _pending.Clear();
    }
}
