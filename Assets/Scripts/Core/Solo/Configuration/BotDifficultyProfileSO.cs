using UnityEngine;

namespace AbsoluteZero.Core.Solo.Configuration
{
    [CreateAssetMenu(menuName = "AbsoluteZero/Solo/Difficulty", fileName = "BotDifficulty")]
    public sealed class BotDifficultyProfileSO : SoloConfigurationSO
    {
        [Tooltip("Unset tuning is invalid. Fixture values are not approved shipping balance.")]
        [SerializeField] bool provisionalTuning = true;
        [Tooltip("Finite, nonnegative range. The runtime may shorten thinking when the preparation deadline is near.")]
        [SerializeField] float minimumThinkSeconds = -1;
        [SerializeField] float maximumThinkSeconds = -1;
        [SerializeField] BotReadyPolicy readyPolicy = BotReadyPolicy.AfterSelection;
        [Tooltip("Finite and nonnegative; strictly less than the shared preparation duration. Reserved after item use.")]
        [SerializeField] float readyReserveSeconds;
        [SerializeField] bool overrideTacticalWeights;
        [Tooltip("Nonnegative candidate-score weights with a positive total. Does not reorder Behavior graph branches.")]
        [SerializeField] BotTacticalWeights tacticalWeights = BotTacticalWeights.Neutral;
        [SerializeField] float decisionNoise;
        [SerializeField] int retryBudget;
        [SerializeField] BotStatProfileSO statOverride;
        [Tooltip("When assigned, replaces the entire base item-delay policy; it does not merge missing overrides from the base.")]
        [SerializeField] BotItemUsePolicySO itemUseOverride;
        public bool ProvisionalTuning => provisionalTuning;
        public float MinimumThinkSeconds => minimumThinkSeconds;
        public float MaximumThinkSeconds => maximumThinkSeconds;
        public BotReadyPolicy ReadyPolicy => readyPolicy;
        public float ReadyReserveSeconds => readyReserveSeconds;
        public bool OverrideTacticalWeights => overrideTacticalWeights;
        public BotTacticalWeights TacticalWeights => tacticalWeights;
        public float DecisionNoise => decisionNoise;
        public int RetryBudget => retryBudget;
        public BotStatProfileSO StatOverride => statOverride;
        public BotItemUsePolicySO ItemUseOverride => itemUseOverride;
    }
}
