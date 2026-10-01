#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Item;

namespace AbsoluteZero.Core.Solo.Configuration
{
    // Editor-only projections of the real launch resolver. This is not a second
    // launch gate, a balance approver, or a mutable runtime configuration owner.
    public sealed class SoloLaunchPreview
    {
        public bool CanLaunch { get; }
        public ResolvedSoloSettings Settings { get; }
        public IReadOnlyList<string> Errors { get; }
        internal SoloLaunchPreview(bool allowed, ResolvedSoloSettings settings, IReadOnlyList<string> errors)
        {
            CanLaunch = allowed;
            Settings = settings;
            Errors = new List<string>(errors).AsReadOnly();
        }
    }

    public readonly struct BotItemTimingPreview
    {
        public short CatalogIndex { get; }
        public string ItemName { get; }
        public bool Enabled { get; }
        public float Delay { get; }
        public float NominalMinimum { get; }
        public float NominalMaximum { get; }
        public bool NeedsTimingReview { get; }

        internal BotItemTimingPreview(ResolvedSoloSettings settings, short index)
        {
            var item = settings.Catalog[index];
            CatalogIndex = index;
            ItemName = item.name;
            Enabled = ItemAvailability.IsEnabled(item);
            Delay = settings.ItemDelaySeconds[index];
            NominalMinimum = settings.MinimumThinkSeconds + Delay + settings.ReadyReserveSeconds;
            NominalMaximum = settings.MaximumThinkSeconds + Delay + settings.ReadyReserveSeconds;
            NeedsTimingReview = Enabled && NominalMaximum >= settings.RuleSnapshot.PrepPhaseDuration;
        }
    }

    public sealed class SoloAuthoringPreview
    {
        public SoloLaunchPreview Release { get; }
        public SoloLaunchPreview Development { get; }
        public SoloLaunchPreview Fixture { get; }
        public ResolvedSoloSettings PreviewSettings => Development.Settings ?? Fixture.Settings ?? Release.Settings;
        public IReadOnlyList<BotItemTimingPreview> ItemTiming { get; }
        public IReadOnlyList<string> Warnings { get; }

        SoloAuthoringPreview(SoloDuelDefinitionSO encounter, SoloConfigurationContext context)
        {
            bool allowed = SoloSettingsResolver.TryResolve(encounter, context, out var settings, out var errors);
            Release = new SoloLaunchPreview(allowed, settings, errors);
            allowed = SoloSettingsResolver.TryResolveForDevelopment(encounter, context, out settings, out errors);
            Development = new SoloLaunchPreview(allowed, settings, errors);
            allowed = SoloSettingsResolver.TryResolveForValidation(encounter, context, out settings, out errors);
            Fixture = new SoloLaunchPreview(allowed, settings, errors);

            var timings = new List<BotItemTimingPreview>();
            var warnings = new List<string>();
            settings = PreviewSettings;
            if (settings != null)
            {
                for (int i = 0; i < settings.Catalog.Count; i++)
                {
                    var row = new BotItemTimingPreview(settings, (short)i);
                    timings.Add(row);
                    if (row.NeedsTimingReview)
                        warnings.Add($"[{row.CatalogIndex}] {row.ItemName}: nominal think + use + Ready reserve " +
                            $"{row.NominalMinimum:0.###}..{row.NominalMaximum:0.###}s reaches the " +
                            $"{settings.RuleSnapshot.PrepPhaseDuration:0.###}s preparation window. " +
                            "The runtime may shorten thinking or choose another action / Ready fallback. This is not a launch failure.");
                }
                if (settings.ProvisionalTuning)
                    warnings.Add("Tuning is provisional. Development/fixture validation does not approve shipping balance.");
                if (settings.IsValidationFixture)
                    warnings.Add("Validation fixture: unavailable from normal Solo entry.");
            }
            ItemTiming = timings.AsReadOnly();
            Warnings = warnings.AsReadOnly();
        }

        public static SoloAuthoringPreview Inspect(SoloDuelDefinitionSO encounter, SoloConfigurationContext context)
            => new(encounter, context);

        // Independent encounters can each resolve while accidentally sharing an ID.
        // Report library ambiguity without changing the existing gameplay launch policy.
        public static IReadOnlyList<string> FindDuplicateIds(IEnumerable<SoloConfigurationSO> library)
        {
            var issues = new List<string>();
            var seen = new HashSet<SoloConfigurationSO>();
            var ids = new Dictionary<string, SoloConfigurationSO>(StringComparer.Ordinal);
            if (library == null) { issues.Add("Configuration library is missing."); return issues.AsReadOnly(); }
            foreach (var source in library)
            {
                if (source == null) { issues.Add("Configuration library contains a missing asset."); continue; }
                if (!seen.Add(source) || string.IsNullOrEmpty(source.ConfigId)) continue;
                if (ids.TryGetValue(source.ConfigId, out var other))
                    issues.Add($"Duplicate ID '{source.ConfigId}': {other.name} / {source.name}. Give copied assets a new stable ID.");
                else ids.Add(source.ConfigId, source);
            }
            return issues.AsReadOnly();
        }
    }
}
#endif
