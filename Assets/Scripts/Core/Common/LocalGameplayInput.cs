using System.Collections.Generic;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    // Presentation-only lock. Game clocks and server-authoritative results keep running.
    public static class LocalGameplayInput
    {
        static readonly HashSet<object> Owners = new();
        static int _releasedFrame = -1;
        public static bool IsBlocked => Owners.Count > 0 || _releasedFrame == Time.frameCount;
        public static void Block(object owner) { if (owner != null) Owners.Add(owner); }
        public static void Release(object owner)
        {
            if (owner != null && Owners.Remove(owner)) _releasedFrame = Time.frameCount;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { Owners.Clear(); _releasedFrame = -1; }
    }
}
