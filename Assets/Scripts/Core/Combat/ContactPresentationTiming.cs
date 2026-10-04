using UnityEngine;

namespace AbsoluteZero.Core.Combat
{
    // Contact points in the existing authored clips; keep their retract frames intact.
    internal static class ContactPresentationTiming
    {
        public const float FeedTransfer = 0.5f;
        public const float HugApproach = 1f;
        public const float HugImpact = 0.5f;
        public const float HugReturn = 1f;
        public static float FeedDuration(float configured, float giver, float receiver)
            => Mathf.Max(configured, giver, FeedTransfer + receiver);
        public static float HugDuration(float configured, float gesture)
            => Mathf.Max(configured, HugApproach + Mathf.Max(HugImpact, gesture) + HugReturn);
    }
}
