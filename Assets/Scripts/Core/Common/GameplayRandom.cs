using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    // Production preserves Unity's exact existing calls. An explicit campaign seed
    // isolates authoritative draws from audio/VFX draws in development runs only.
    public static class GameplayRandom
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static readonly System.Collections.Generic.Dictionary<string, System.Random> Streams = new();
        static bool _resolved, _seeded;
        static int _seed;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Streams.Clear(); _resolved = _seeded = false; _seed = 0; }
        static System.Random Stream(string domain)
        {
            if (!_resolved)
            {
                _resolved = true;
                _seeded = !string.IsNullOrEmpty(Solo.PlaytestTelemetry.Arg("--az-playtest"))
                    && int.TryParse(Solo.PlaytestTelemetry.Arg("--az-gameplay-seed"), out _seed);
            }
            if (!_seeded) return null;
            if (!Streams.TryGetValue(domain, out var stream))
            {
                uint hash = unchecked((uint)_seed) ^ 2166136261;
                foreach (char ch in domain) hash = unchecked((hash ^ ch) * 16777619);
                Streams.Add(domain, stream = new System.Random(unchecked((int)hash)));
            }
            return stream;
        }
#endif
        public static int Range(string domain, int min, int max)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var stream = Stream(domain);
            if (stream != null)
            {
                int value = min == max ? min : stream.Next(min, max);
                Solo.PlaytestTelemetry.Current?.Record("random_draw", domain + ":" + min + ":" + max + ":" + value);
                return value;
            }
#endif
            return Random.Range(min, max);
        }
        public static float Range(string domain, float min, float max)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var stream = Stream(domain);
            if (stream != null)
            {
                float value = min + (max - min) * (float)stream.NextDouble();
                Solo.PlaytestTelemetry.Current?.Record("random_draw", domain + ":" + min.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + ":" + max.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                return value;
            }
#endif
            return Random.Range(min, max);
        }
    }
}
