using System;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Network;

namespace AbsoluteZero.Core.Turn
{
    internal static class EnvironmentCalculations
    {
        static readonly EnvironmentType[] Pool =
        {
            EnvironmentType.SunnyDay, EnvironmentType.CoolBreeze,
            EnvironmentType.Kids, EnvironmentType.Ambulance, EnvironmentType.SummerVacation,
            EnvironmentType.HeatWaveWarning
        };
        // Multi uses the night pavilion, including three-player and 4 -> 3 continuation.
        // These effects need no sunlight/cicada/heat-wave daytime staging.
        static readonly EnvironmentType[] NightPool =
        {
            EnvironmentType.CoolBreeze, EnvironmentType.Kids,
            EnvironmentType.Ambulance, EnvironmentType.SummerVacation
        };
        static EnvironmentType[] PoolFor(GameMode mode) => mode == GameMode.Multi ? NightPool : Pool;
        internal static int PoolCountFor(GameMode mode) => PoolFor(mode).Length;
        internal static EnvironmentType Select(int draw, GameMode mode) => PoolFor(mode)[draw];
        internal static float PrepDuration(EnvironmentType env, float duration)
            => env == EnvironmentType.SummerVacation ? 10f : duration;
        internal static float RecoveryRate(EnvironmentType env) => env switch
        {
            EnvironmentType.SunnyDay => 2f,
            EnvironmentType.CoolBreeze => 0f,
            _ => TemperatureSystem.DEFAULT_RECOVERY_RATE
        };
        internal static bool KidsTurn(EnvironmentType env, int turn) => env == EnvironmentType.Kids && turn == 3;
        internal static bool AmbulanceTurn(EnvironmentType env, int turn) => env == EnvironmentType.Ambulance && turn == 3;
        internal static bool KidsCandidate(bool empty, ItemPersistence? persistence)
            => !empty && persistence == ItemPersistence.RandomConsumable;

        // Multi deliberately chooses the first eligible lowest seat on a tie.
        // The existing duel method keeps its distinct no-heal-on-tie policy.
        internal static int MultiAmbulanceTarget(ReadOnlySpan<float> temperatures, ReadOnlySpan<bool> eligible)
        {
            int target = -1;
            float lowest = float.MaxValue;
            for (int i = 0; i < temperatures.Length; i++)
            {
                if (!eligible[i] || !(temperatures[i] < lowest)) continue;
                lowest = temperatures[i]; target = i;
            }
            return target;
        }
    }
}
