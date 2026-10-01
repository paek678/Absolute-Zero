using Unity.Behavior;
using UnityEngine;

namespace AbsoluteZero.Core.Solo.Configuration
{
    [CreateAssetMenu(menuName = "AbsoluteZero/Solo/Bot", fileName = "Bot")]
    public sealed class BotDefinitionSO : SoloConfigurationSO
    {
        [SerializeField] string displayName;
        [SerializeField] BotCosmeticIds cosmetics;
        [SerializeField] BehaviorGraph graph;
        [SerializeField] BotStatProfileSO baseStats;
        [SerializeField] BotItemUsePolicySO itemUsePolicy;
        [SerializeField] BotTacticalWeights tacticalDefaults = BotTacticalWeights.Neutral;
        public string DisplayName => displayName;
        public BotCosmeticIds Cosmetics => cosmetics;
        public BehaviorGraph Graph => graph;
        public BotStatProfileSO BaseStats => baseStats;
        public BotItemUsePolicySO ItemUsePolicy => itemUsePolicy;
        public BotTacticalWeights TacticalDefaults => tacticalDefaults;
    }
}
