using System;
using AbsoluteZero.Core.Item;
using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    /// <summary>Immutable action classification shared by the server barrier and client VFX.</summary>
    public readonly struct MultiPresentationSchedule
    {
        const float IntroSeconds = 0.5f;
        const float MinimumActionSeconds = 3f;
        const float PauseSeconds = 0.3f;
        const float PerActionMarginSeconds = 1.5f;
        const float PerDeathSeconds = 4.5f;
        const float NetworkMarginSeconds = 5f;

        public readonly byte ActionMask;
        public readonly int ActionCount;
        public readonly float BarrierBudgetSeconds;
        readonly float[] _actionDurations;

        MultiPresentationSchedule(byte actionMask, int actionCount, float budget,
            float[] actionDurations)
        {
            ActionMask = actionMask;
            ActionCount = actionCount;
            BarrierBudgetSeconds = budget;
            _actionDurations = actionDurations;
        }

        public bool HasAction(int seat)
            => seat >= 0 && seat < CombatResolutionBatchNetData.MaxSeats
                && (ActionMask & (1 << seat)) != 0;

        public float ActionDurationSeconds(int seat)
            => HasAction(seat) && _actionDurations != null
                ? _actionDurations[seat] : 0f;

        public static MultiPresentationSchedule Build(CombatResolutionBatchNetData batch,
            Func<short, float> itemDuration, Func<short, bool> isDefense)
        {
            if (batch.SeatCount > CombatResolutionBatchNetData.MaxSeats
                || batch.Events == null || batch.EventCount > batch.Events.Length
                || batch.MainItemIds == null || batch.MainItemIds.Length < batch.SeatCount)
                throw new ArgumentException("Invalid Multi presentation batch", nameof(batch));

            byte mask = 0;
            int actions = 0;
            float seconds = IntroSeconds + NetworkMarginSeconds;
            var durations = new float[CombatResolutionBatchNetData.MaxSeats];
            for (int seat = 0; seat < batch.SeatCount; seat++)
            {
                bool hasAction = false;
                for (int e = 0; e < batch.EventCount; e++)
                {
                    var evt = batch.Events[e];
                    if (evt.ActorSeat != seat) continue;
                    var kind = (CombatEventType)evt.EventType;
                    if (kind == CombatEventType.MainEffect || kind == CombatEventType.DefenseActivated
                        || (kind == CombatEventType.SuppressedItemUse
                            && !(isDefense?.Invoke(evt.ItemId) ?? false)))
                    {
                        hasAction = true;
                        break;
                    }
                }
                if (!hasAction) continue;
                mask |= (byte)(1 << seat);
                actions++;
                durations[seat] = Mathf.Max(MinimumActionSeconds,
                    itemDuration?.Invoke(batch.MainItemIds[seat]) ?? 0f);
                seconds += durations[seat] + PerActionMarginSeconds;
            }
            if (actions > 1) seconds += (actions - 1) * PauseSeconds;
            byte deaths = batch.DeadMask;
            while (deaths != 0)
            {
                seconds += (deaths & 1) * PerDeathSeconds;
                deaths >>= 1;
            }
            return new MultiPresentationSchedule(mask, actions, seconds, durations);
        }
    }
}
