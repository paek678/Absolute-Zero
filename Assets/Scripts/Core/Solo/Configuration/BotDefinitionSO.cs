using Unity.Behavior;
using UnityEngine;

namespace AbsoluteZero.Core.Solo.Configuration
{
    [CreateAssetMenu(menuName = "AbsoluteZero/Solo/Bot", fileName = "Bot")]
    public sealed class BotDefinitionSO : SoloConfigurationSO
    {
        [SerializeField] string displayName;
        [SerializeField] BotCosmeticIds cosmetics;
        [SerializeField] AbsoluteZero.Core.Cosmetic.CharacterAppearanceProfileSO appearance;
        public AbsoluteZero.Core.Cosmetic.CharacterAppearanceProfileSO Appearance => appearance;
        [SerializeField] BehaviorGraph graph;
        [SerializeField] BotStatProfileSO baseStats;
        [SerializeField] BotItemUsePolicySO itemUsePolicy;
        [Tooltip("Required for normal launches: opponent item worth, mini-game tiers and score coefficients.")]
        [SerializeField] BotItemKnowledgeSO itemKnowledge;
        public string DisplayName => displayName;
        public BotCosmeticIds Cosmetics => cosmetics;
        public BehaviorGraph Graph => graph;
        public BotStatProfileSO BaseStats => baseStats;
        public BotItemUsePolicySO ItemUsePolicy => itemUsePolicy;
        public BotItemKnowledgeSO ItemKnowledge => itemKnowledge;
    }
}
