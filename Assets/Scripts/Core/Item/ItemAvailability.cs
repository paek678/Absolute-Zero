using AbsoluteZero.Core.Item.Data;

namespace AbsoluteZero.Core.Item
{
    /// <summary>
    /// Playable content policy, independent of the stable serialized/wire catalog.
    /// Disabled entries remain addressable for compatibility and validation.
    /// </summary>
    public static class ItemAvailability
    {
        // D37-01: Tarot/reveal is not shipped in any mode. Display names, legacy
        // rule flags and positive drop weights must not reactivate this effect.
        public static bool IsEnabled(ItemDataSO item)
            => item != null && !(item is SpecialItemDataSO special
                && special.SpecialEffect == SpecialEffectType.RevealOpponent);
    }
}
