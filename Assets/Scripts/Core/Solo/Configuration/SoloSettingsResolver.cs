using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Cosmetic;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Network;
using Unity.Behavior;
using Unity.Properties;
using UnityEngine;

namespace AbsoluteZero.Core.Solo.Configuration
{
    public sealed class SoloConfigurationContext
    {
        public GameModeRuleSO SharedOneVsOneRule { get; }
        public IReadOnlyList<ItemDataSO> Catalog { get; }
        public CosmeticRegistrySO CosmeticRegistry { get; }
        public Func<string, bool> IsSceneAvailable { get; }

        // Production uses the build's loadability check. Editor fixtures can explicitly
        // supply an AssetDatabase existence check without editing the production build list.
        public SoloConfigurationContext(GameModeRuleSO sharedOneVsOneRule, IReadOnlyList<ItemDataSO> catalog,
            CosmeticRegistrySO cosmeticRegistry, Func<string, bool> isSceneAvailable = null)
        {
            SharedOneVsOneRule = sharedOneVsOneRule;
            Catalog = catalog;
            CosmeticRegistry = cosmeticRegistry;
            IsSceneAvailable = isSceneAvailable ?? Application.CanStreamedLevelBeLoaded;
        }
    }

    public static class SoloSettingsResolver
    {
        static readonly Regex ConfigIdPattern = new("^[a-z0-9][a-z0-9_.-]{0,63}$");

        public static bool TryResolve(SoloDuelDefinitionSO encounter, SoloConfigurationContext context,
            out ResolvedSoloSettings settings, out IReadOnlyList<string> errors)
            => Resolve(encounter, context, false, out settings, out errors);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Ready production graph with explicitly provisional tuning, development
        // builds only. Drafts and ValidationFixtures remain rejected here.
        public static bool TryResolveForDevelopment(SoloDuelDefinitionSO encounter, SoloConfigurationContext context,
            out ResolvedSoloSettings settings, out IReadOnlyList<string> errors)
            => Resolve(encounter, context, false, out settings, out errors, true);
        // Explicit test entry only. This method is absent from non-development players.
        public static bool TryResolveForValidation(SoloDuelDefinitionSO encounter, SoloConfigurationContext context,
            out ResolvedSoloSettings settings, out IReadOnlyList<string> errors)
            => Resolve(encounter, context, true, out settings, out errors);
#endif

        static bool Resolve(SoloDuelDefinitionSO encounter, SoloConfigurationContext context, bool validation,
            out ResolvedSoloSettings settings, out IReadOnlyList<string> errors, bool allowProvisional = false)
        {
            settings = null;
            var issues = new List<string>();
            var stamps = new List<SoloConfigurationStamp>();
            var seen = new HashSet<SoloConfigurationSO>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            errors = issues.AsReadOnly();
            if (encounter == null) { issues.Add("Encounter is required."); return false; }
            if (context == null) { issues.Add("Composition context is required."); return false; }

            CheckIdentity(encounter, issues, stamps, seen, ids);
            if (!Enum.IsDefined(typeof(SoloConfigurationReadiness), encounter.Readiness))
                issues.Add("Encounter readiness is undefined.");
            else if (encounter.Readiness == SoloConfigurationReadiness.Draft)
                issues.Add("Draft encounters cannot launch, including validation launches.");
            else if (validation != (encounter.Readiness == SoloConfigurationReadiness.ValidationFixture))
                issues.Add("ValidationFixture requires the explicit validation entry; normal entry requires Ready.");

            var rule = encounter.SharedOneVsOneRule;
            if (rule == null || context.SharedOneVsOneRule == null || rule != context.SharedOneVsOneRule
                || rule.TargetMode != GameMode.OneVsOne)
                issues.Add("Encounter must reference the composition's shared OneVsOne rule asset.");
            if (rule != null && (!Finite(rule.PrepPhaseDuration) || rule.PrepPhaseDuration <= 0))
                issues.Add("Shared rule preparation duration must be finite and positive.");

            string scene = encounter.GameplayScenePath;
            if (string.IsNullOrWhiteSpace(scene) || !scene.StartsWith("Assets/", StringComparison.Ordinal)
                || !scene.EndsWith(".unity", StringComparison.Ordinal) || scene.Contains("..") || scene.IndexOf('\\') >= 0)
                issues.Add("Gameplay scene requires a full Assets/.../*.unity path.");
            else
            {
                try { if (!context.IsSceneAvailable(scene)) issues.Add("Gameplay scene does not exist or is not loadable: " + scene); }
                catch (Exception ex) { issues.Add("Gameplay scene validation failed: " + ex.Message); }
            }

            var catalog = BuildCatalog(context.Catalog, issues, out var itemIndices);
            var bot = encounter.Bot;
            var difficulty = encounter.Difficulty;
            if (bot == null) issues.Add("Bot definition is required.");
            if (difficulty == null) issues.Add("Difficulty is required.");
            if (bot == null || difficulty == null) return false;

            CheckIdentity(bot, issues, stamps, seen, ids);
            CheckIdentity(difficulty, issues, stamps, seen, ids);
            if (string.IsNullOrWhiteSpace(bot.DisplayName)) issues.Add("Bot display name is required.");
            if (!HasCompiledRoot(bot.Graph)) issues.Add("Bot graph requires a compiled root with no placeholder nodes.");
            CheckCosmetics(bot.Cosmetics, context.CosmeticRegistry, issues);
            CheckWeights(bot.TacticalDefaults, "Bot tactical defaults", issues);
            CheckWeights(difficulty.TacticalWeights, "Difficulty tactical weights", issues);
            CheckNonNegative(difficulty.MinimumThinkSeconds, "Minimum think delay", issues);
            CheckNonNegative(difficulty.MaximumThinkSeconds, "Maximum think delay", issues);
            if (difficulty.MaximumThinkSeconds < difficulty.MinimumThinkSeconds) issues.Add("Think delay range is reversed.");
            if (!Enum.IsDefined(typeof(BotReadyPolicy), difficulty.ReadyPolicy)) issues.Add("Ready policy is undefined.");
            CheckNonNegative(difficulty.ReadyReserveSeconds, "Ready reserve", issues);
            if (rule != null && difficulty.ReadyReserveSeconds >= rule.PrepPhaseDuration)
                issues.Add("Ready reserve must be less than the shared preparation duration.");
            if (!Finite(difficulty.DecisionNoise) || difficulty.DecisionNoise < 0 || difficulty.DecisionNoise > 1)
                issues.Add("Decision noise must be finite and in [0, 1].");
            if (difficulty.RetryBudget < 0 || difficulty.RetryBudget > 32)
                issues.Add("Retry budget must be in [0, 32] to keep decisions bounded.");

            CheckStats(bot.BaseStats, "Base stats", itemIndices, issues, stamps, seen, ids);
            CheckStats(difficulty.StatOverride, "Difficulty stats", itemIndices, issues, stamps, seen, ids);
            CheckPolicy(bot.ItemUsePolicy, "Base item policy", itemIndices, issues, stamps, seen, ids);
            CheckPolicy(difficulty.ItemUseOverride, "Difficulty item policy", itemIndices, issues, stamps, seen, ids);
            var policy = difficulty.ItemUseOverride != null ? difficulty.ItemUseOverride : bot.ItemUsePolicy;
            if (policy == null) issues.Add("An effective item-use policy is required.");
            if (!validation && !allowProvisional && (difficulty.ProvisionalTuning || (policy != null && policy.ProvisionalTuning)))
                issues.Add("Provisional tuning is restricted to explicit validation fixtures.");
            if (issues.Count > 0) return false;

            var delays = new float[catalog.Length];
            for (int i = 0; i < delays.Length; i++) delays[i] = policy.DefaultDelaySeconds;
            foreach (var value in policy.Overrides) delays[itemIndices[value.Item]] = value.DelaySeconds;
            settings = new ResolvedSoloSettings(encounter, policy, stamps, catalog, delays);
            return true;
        }

        // Behavior 1.0.16 exposes no public RootGraph. Its generated property bags expose
        // serialized fields through Unity.Properties without reflection or starting the asset.
        public static bool HasCompiledRoot(BehaviorGraph graph)
        {
            if (graph == null) return false;
            try
            {
                return PropertyContainer.TryGetValue(ref graph, new PropertyPath("Graphs[0].Root"), out Node root)
                    && root != null
                    && PropertyContainer.TryGetValue(ref graph, new PropertyPath("m_WasCompileWithPlaceholderNode"), out bool placeholder)
                    && !placeholder;
            }
            catch (Exception) { return false; }
        }

        static ItemDataSO[] BuildCatalog(IReadOnlyList<ItemDataSO> input, List<string> issues,
            out Dictionary<ItemDataSO, short> indices)
        {
            indices = new Dictionary<ItemDataSO, short>();
            if (input == null || input.Count == 0 || input.Count > short.MaxValue + 1)
            { issues.Add("Current item catalog must contain 1..32768 entries."); return Array.Empty<ItemDataSO>(); }
            var copy = new ItemDataSO[input.Count];
            for (int i = 0; i < input.Count; i++)
            {
                copy[i] = input[i];
                if (copy[i] == null) issues.Add("Current item catalog has a null entry at " + i + ".");
                else if (indices.ContainsKey(copy[i])) issues.Add("Current item catalog contains the same item reference twice.");
                else
                {
                    indices.Add(copy[i], (short)i);
                    // Both the bot estimator and shared combat resolver require this
                    // mapping. Reject an unintegrated item at launch, before the BT runs.
                    try { ItemEffectRuleSnapshot.From(copy[i], (short)i); }
                    catch (Exception error)
                    { issues.Add("Catalog item " + i + " has an unsupported effect: " + error.Message); }
                }
            }
            return copy;
        }

        static void CheckIdentity(SoloConfigurationSO value, List<string> issues, List<SoloConfigurationStamp> stamps,
            HashSet<SoloConfigurationSO> seen, HashSet<string> ids)
        {
            if (!seen.Add(value)) return;
            if (string.IsNullOrEmpty(value.ConfigId) || !ConfigIdPattern.IsMatch(value.ConfigId))
                issues.Add(value.GetType().Name + " requires a stable lowercase configuration ID (1..64 characters).");
            else if (!ids.Add(value.ConfigId)) issues.Add("Different configuration assets share ID " + value.ConfigId + ".");
            if (value.Version < 1) issues.Add(value.GetType().Name + " version must be positive.");
            stamps.Add(new SoloConfigurationStamp(value));
        }

        static void CheckStats(BotStatProfileSO stats, string label, Dictionary<ItemDataSO, short> catalog,
            List<string> issues, List<SoloConfigurationStamp> stamps, HashSet<SoloConfigurationSO> seen, HashSet<string> ids)
        {
            if (stats == null) return; // Every field inherits current 1v1 behavior.
            CheckIdentity(stats, issues, stamps, seen, ids);
            if (!Finite(stats.StartingTemperature) || stats.StartingTemperature <= TemperatureSystem.MIN_TEMP
                || stats.StartingTemperature > TemperatureSystem.MAX_TEMP) issues.Add(label + " temperature is outside the live 1v1 range.");
            CheckNonNegative(stats.FanBaseline, label + " fan baseline", issues);
            foreach (var item in stats.InitialItems)
                if (item == null || !catalog.ContainsKey(item)) issues.Add(label + " initial item is missing from the catalog.");
            if (stats.OverrideStartingTemperature || stats.OverrideFanBaseline || stats.OverrideInitialItems || stats.InitialItems.Count > 0)
                issues.Add(label + " custom stats/loadouts are disabled until reset/merge semantics are approved.");
        }

        static void CheckPolicy(BotItemUsePolicySO policy, string label, Dictionary<ItemDataSO, short> catalog,
            List<string> issues, List<SoloConfigurationStamp> stamps, HashSet<SoloConfigurationSO> seen, HashSet<string> ids)
        {
            if (policy == null) return;
            CheckIdentity(policy, issues, stamps, seen, ids);
            CheckPositive(policy.DefaultDelaySeconds, label + " default delay", issues);
            var seenItems = new HashSet<ItemDataSO>();
            foreach (var entry in policy.Overrides)
            {
                CheckPositive(entry.DelaySeconds, label + " item delay", issues);
                if (entry.Item == null || !catalog.ContainsKey(entry.Item)) issues.Add(label + " override is missing from the current catalog.");
                else if (!seenItems.Add(entry.Item)) issues.Add(label + " has duplicate overrides for one item reference.");
            }
        }

        static void CheckCosmetics(BotCosmeticIds ids, CosmeticRegistrySO registry, List<string> issues)
        {
            CheckCosmetic(ids.Head, CosmeticPart.Head, registry, issues);
            CheckCosmetic(ids.Top, CosmeticPart.Top, registry, issues);
            CheckCosmetic(ids.Back, CosmeticPart.Back, registry, issues);
            CheckCosmetic(ids.Bottom, CosmeticPart.Bottom, registry, issues);
            CheckCosmetic(ids.Tail, CosmeticPart.Tail, registry, issues);
        }

        static void CheckCosmetic(string id, CosmeticPart part, CosmeticRegistrySO registry, List<string> issues)
        {
            if (string.IsNullOrEmpty(id)) return; // Existing base character, no equipped cosmetic.
            var item = registry != null ? registry.GetById(id) : null;
            if (item == null || item.Part != part) issues.Add("Unknown or wrong-part cosmetic ID for " + part + ": " + id);
        }

        static void CheckWeights(BotTacticalWeights value, string label, List<string> issues)
        {
            CheckNonNegative(value.Survival, label + " survival", issues);
            CheckNonNegative(value.Finish, label + " finish", issues);
            CheckNonNegative(value.Counter, label + " counter", issues);
            CheckNonNegative(value.Resource, label + " resource", issues);
            CheckNonNegative(value.General, label + " general", issues);
            float sum = value.Survival + value.Finish + value.Counter + value.Resource + value.General;
            if (!Finite(sum) || sum <= 0) issues.Add(label + " must have a finite positive total.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void CheckNonNegative(float value, string label, List<string> issues)
        { if (!Finite(value) || value < 0) issues.Add(label + " must be finite and nonnegative."); }
        static void CheckPositive(float value, string label, List<string> issues)
        { if (!Finite(value) || value <= 0) issues.Add(label + " must be finite and positive; zero delay is not approved."); }
    }
}
