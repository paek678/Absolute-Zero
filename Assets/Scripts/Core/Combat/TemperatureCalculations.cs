using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    // Values only. TemperatureSystem keeps accumulator lifetime, eligibility
    // checks, grant flags and network writes in their original order.
    internal static class TemperatureCalculations
    {
        static readonly float[] Thresholds = { 30f, 20f, 10f };
        internal static int ThresholdCount => Thresholds.Length;
        internal static float ThresholdAt(int index) => Thresholds[index];
        internal static int GrantAt(int index, bool multi) => multi ? 1 : index + 1;
        internal static bool ReachedThreshold(int index, float temperature, bool alreadyGranted)
            => !alreadyGranted && temperature <= Thresholds[index];

        internal static float FanTick(float temperature, float speed, float multiplier)
            => Mathf.Max(TemperatureSystem.MIN_TEMP, temperature - speed * multiplier);
        internal static float RecoveryTick(float temperature, float rate, float multiplier)
            => Mathf.Min(TemperatureSystem.MAX_TEMP, temperature + rate * multiplier);
    }
}
