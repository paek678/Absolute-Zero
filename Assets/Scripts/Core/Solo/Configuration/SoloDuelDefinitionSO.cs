using AbsoluteZero.Core.Match;
using UnityEngine;

namespace AbsoluteZero.Core.Solo.Configuration
{
    [CreateAssetMenu(menuName = "AbsoluteZero/Solo/Encounter", fileName = "SoloDuel")]
    public sealed class SoloDuelDefinitionSO : SoloConfigurationSO
    {
        [SerializeField] SoloConfigurationReadiness readiness = SoloConfigurationReadiness.Draft;
        [SerializeField] BotDefinitionSO bot;
        [SerializeField] BotDifficultyProfileSO difficulty;
        [SerializeField] GameModeRuleSO sharedOneVsOneRule;
        [Tooltip("Full Assets/.../*.unity path. Must be loadable for a normal launch.")]
        [SerializeField] string gameplayScenePath;
        public SoloConfigurationReadiness Readiness => readiness;
        public BotDefinitionSO Bot => bot;
        public BotDifficultyProfileSO Difficulty => difficulty;
        public GameModeRuleSO SharedOneVsOneRule => sharedOneVsOneRule;
        public string GameplayScenePath => gameplayScenePath;
    }
}
