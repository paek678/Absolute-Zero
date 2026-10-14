using System.Collections.Generic;
using System.Collections.ObjectModel;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using Unity.Behavior;

namespace AbsoluteZero.Core.Solo.Configuration
{
    public readonly struct SoloConfigurationStamp
    {
        public string Kind { get; }
        public string Id { get; }
        public int Version { get; }
        internal SoloConfigurationStamp(SoloConfigurationSO source)
        { Kind = source.GetType().Name; Id = source.ConfigId; Version = source.Version; }
    }

    // Scalar values and collections are copied. Rule/graph/item asset references are identity
    // handles, never mutable match state; the graph runner must instantiate its own graph.
    public sealed class ResolvedSoloSettings
    {
        readonly ReadOnlyCollection<ItemDataSO> _catalog;
        readonly ReadOnlyCollection<float> _itemDelays;
        readonly ReadOnlyCollection<BotItemProfile> _profiles;
        public IReadOnlyList<SoloConfigurationStamp> ConfigurationVersions { get; }
        public string EncounterId { get; }
        public string BotId { get; }
        public string DifficultyId { get; }
        public string BotDisplayName { get; }
        public string GameplayScenePath { get; }
        public bool IsValidationFixture { get; }
        public bool ProvisionalTuning { get; }
        public GameModeRuleSO SharedOneVsOneRule { get; }
        public GameModeRuleSnapshot RuleSnapshot { get; }
        public BehaviorGraph Graph { get; }
        public BotCosmeticIds Cosmetics { get; }
        public AbsoluteZero.Core.Cosmetic.CharacterAppearanceSnapshot Appearance { get; }
        public float StartingTemperature => TemperatureSystem.MAX_TEMP;
        public float FanBaseline => TemperatureSystem.DEFAULT_FAN_SPEED;
        // This is a delegation policy, not an empty replacement loadout or the rule SO's
        // initialRandomItems value. Existing 1v1 grant/reset/threshold paths retain ownership.
        public bool InheritOneVsOneGrants => true;
        public float MinimumThinkSeconds { get; }
        public float MaximumThinkSeconds { get; }
        public BotReadyPolicy ReadyPolicy { get; }
        public float ReadyReserveSeconds { get; }
        public float ExtraItemDelaySeconds { get; }
        public BotJudgementSettings Judgement { get; }
        public BotScoringCoefficients Coefficients { get; }
        public bool HasItemKnowledge { get; }
        public int RetryBudget { get; }
        public IReadOnlyList<ItemDataSO> Catalog => _catalog;
        // Effective delays: item-use policy plus the difficulty's extra delay.
        public IReadOnlyList<float> ItemDelaySeconds => _itemDelays;
        internal IReadOnlyList<BotItemProfile> ItemProfiles => _profiles;

        internal ResolvedSoloSettings(SoloDuelDefinitionSO encounter, BotItemUsePolicySO policy,
            IList<SoloConfigurationStamp> versions, ItemDataSO[] catalog, float[] delays, BotItemProfile[] profiles)
        {
            var bot = encounter.Bot;
            var difficulty = encounter.Difficulty;
            ConfigurationVersions = new ReadOnlyCollection<SoloConfigurationStamp>(new List<SoloConfigurationStamp>(versions));
            EncounterId = encounter.ConfigId; BotId = bot.ConfigId; DifficultyId = difficulty.ConfigId;
            BotDisplayName = bot.DisplayName; GameplayScenePath = encounter.GameplayScenePath;
            IsValidationFixture = encounter.Readiness == SoloConfigurationReadiness.ValidationFixture;
            ProvisionalTuning = policy.ProvisionalTuning || difficulty.ProvisionalTuning;
            SharedOneVsOneRule = encounter.SharedOneVsOneRule;
            RuleSnapshot = GameModeRuleSnapshot.From(SharedOneVsOneRule);
            Graph = bot.Graph; Cosmetics = bot.Cosmetics;
            if (bot.Appearance != null && bot.Appearance.TrySnapshot(out var appearance, out _)) Appearance = appearance;
            MinimumThinkSeconds = difficulty.MinimumThinkSeconds; MaximumThinkSeconds = difficulty.MaximumThinkSeconds;
            ReadyPolicy = difficulty.ReadyPolicy; ReadyReserveSeconds = difficulty.ReadyReserveSeconds;
            ExtraItemDelaySeconds = difficulty.ExtraItemDelaySeconds; Judgement = difficulty.Judgement;
            HasItemKnowledge = bot.ItemKnowledge != null;
            Coefficients = HasItemKnowledge ? bot.ItemKnowledge.Coefficients : BotScoringCoefficients.Approved;
            RetryBudget = difficulty.RetryBudget;
            _catalog = new ReadOnlyCollection<ItemDataSO>((ItemDataSO[])catalog.Clone());
            _itemDelays = new ReadOnlyCollection<float>((float[])delays.Clone());
            _profiles = new ReadOnlyCollection<BotItemProfile>((BotItemProfile[])profiles.Clone());
        }

        public bool TryGetItemDelay(short catalogItemId, out float delaySeconds)
        {
            delaySeconds = 0;
            if (catalogItemId < 0 || catalogItemId >= _itemDelays.Count) return false;
            delaySeconds = _itemDelays[catalogItemId];
            return true;
        }

        // False for catalog items without a knowledge entry: the bot never chooses them.
        public bool KnowsItem(short catalogItemId)
            => catalogItemId >= 0 && catalogItemId < _profiles.Count && _profiles[catalogItemId].Known;

        // 1 for items without a human mini-game or without bot knowledge (fixtures).
        public bool TryGetMiniGameSuccess(short catalogItemId, out float chance)
        {
            chance = 1;
            if (catalogItemId < 0 || catalogItemId >= _profiles.Count) return false;
            chance = _profiles[catalogItemId].Success;
            return true;
        }
    }
}
