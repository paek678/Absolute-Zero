using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using UnityEngine;

namespace AbsoluteZero.Core.Common
{
    public static class ItemPresentation
    {
        static bool _warnedLegacy;
        public static ItemPresentationCatalogSO Catalog => MatchCompositionRoot.Instance != null
            && MatchCompositionRoot.Instance.IsSessionCurrent ? MatchCompositionRoot.Instance.ViewBindings?.Catalog : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDiagnostics() => _warnedLegacy = false;

        internal static void WarnLegacy()
        {
            if (_warnedLegacy) return;
            _warnedLegacy = true;
            Debug.LogWarning("[ItemPresentation] Legacy lookup used by an unmigrated view or validation fixture.");
        }

        public static ItemChoreography GetChoreography(ItemDataSO item)
        {
            if (Catalog != null && Catalog.TryGet(item, out var entry)) return entry.Choreography;
            WarnLegacy();
            return LegacyChoreography(item?.ItemName);
        }

        // Migration/fixture compatibility only. Current game scenes resolve by SO identity.
        public static ItemChoreography LegacyChoreography(string name) => name switch
        {
            "Samgyetang" or "Ice Cream" or "Iced Americano" => ItemChoreography.Feed,
            "Hug T-shirt" => ItemChoreography.Hug,
            "Cat" => ItemChoreography.Cat,
            "Buldak Noodles" => ItemChoreography.Buldak,
            "Screwdriver" => ItemChoreography.Screwdriver,
            "Red Card" => ItemChoreography.RedCard,
            _ => ItemChoreography.Ordinary
        };
    }
}
