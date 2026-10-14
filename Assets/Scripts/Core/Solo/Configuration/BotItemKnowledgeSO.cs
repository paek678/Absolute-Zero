using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Item.Data;
using UnityEngine;

namespace AbsoluteZero.Core.Solo.Configuration
{
    [Serializable]
    public struct BotItemKnowledgeEntry
    {
        [Tooltip("Catalog item asset. Damage/heal/scheduled values are read from the item itself.")]
        public ItemDataSO Item;
        [Tooltip("Worth of this item while the opponent holds it (Cat, Claw, Red Card scores).")]
        public float OpponentValue;
        [Tooltip("Required for items whose human path plays a mini-game; selects the difficulty success chance.")]
        public BotMiniGameTier MiniGameTier;
    }

    // Formula constants from the approved score sheet; shared by every difficulty.
    [Serializable]
    public struct BotScoringCoefficients
    {
        [Tooltip("Cat: sum of opponent random item values x this x lookahead.")]
        public float RerollValueFactor;
        [Tooltip("Claw: highest stealable opponent item value x this x lookahead x success.")]
        public float StealValueFactor;
        [Tooltip("Red Card: highest opponent item value x this x success.")]
        public float NeutralizeValueFactor;
        [Tooltip("Blue Tape: this x lookahead x success.")]
        public float BlockBasicsValue;
        [Tooltip("Screwdriver: turns of extra cooling counted by its score.")]
        public float FanBoostTurns;

        public static BotScoringCoefficients Approved => new()
        { RerollValueFactor = .4f, StealValueFactor = 2, NeutralizeValueFactor = .7f, BlockBasicsValue = 3, FanBoostTurns = 2 };
    }

    // The per-item sheet of the bot design: opponent item worth and mini-game tier.
    // Items without an entry are never chosen by the bot and are worth 0 when held by the opponent.
    [CreateAssetMenu(menuName = "AbsoluteZero/Solo/Item knowledge", fileName = "BotItemKnowledge")]
    public sealed class BotItemKnowledgeSO : SoloConfigurationSO
    {
        [Tooltip("Entry order breaks score ties: an earlier entry wins.")]
        [SerializeField] BotItemKnowledgeEntry[] entries = Array.Empty<BotItemKnowledgeEntry>();
        [SerializeField] BotScoringCoefficients coefficients = BotScoringCoefficients.Approved;
        public IReadOnlyList<BotItemKnowledgeEntry> Entries => Array.AsReadOnly(entries ?? Array.Empty<BotItemKnowledgeEntry>());
        public BotScoringCoefficients Coefficients => coefficients;
    }
}
