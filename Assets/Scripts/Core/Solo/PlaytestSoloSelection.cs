#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AbsoluteZero.Core.Solo.Configuration;
using Unity.Behavior;

namespace AbsoluteZero.Core.Solo
{
    // Implemented by the normal menu. Core owns the contract and never imports UI.
    public interface ISoloPlaytestSelection
    {
        bool TrySelectEncounter(string encounterId, out string expectedDifficultyId, out string error);
    }

    [Serializable]
    internal sealed class PlaytestSoloEvidence
    {
        public string requestedEncounterId, expectedDifficultyId, encounterId, difficultyId, botId;
        public string graphName, graphAuthoringId, readyPolicy;
        public string[] configurationVersions, catalogNames;
        public float[] effectiveDelays, miniGameSuccess;
        public float minimumThinkSeconds, maximumThinkSeconds, readyReserveSeconds, extraItemDelaySeconds;
        public bool provisionalTuning, validationFixture, inheritOneVsOneGrants;
        public int retryBudget, humanDriverSeed;
        public long generation;
        public BotJudgementSettings judgement;
        public BotScoringCoefficients coefficients;

        internal static string GraphAuthoringId(BehaviorGraph graph)
        {
            if (graph == null) throw new InvalidOperationException("Compiled graph identity is unavailable.");
            // Read the installed package's serialized identity only, never tick
            // or mutate its modules. Fail rather than inventing a graph identity.
            var modules = typeof(BehaviorGraph).GetField("Graphs", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.GetValue(graph) as IList;
            if (modules == null || modules.Count == 0)
                throw new InvalidOperationException("Compiled graph identity is unavailable.");
            var id = modules[0].GetType().GetField("AuthoringAssetID")?.GetValue(modules[0])?.ToString();
            if (string.IsNullOrEmpty(id) || id.Length != 32 || id.All(x => x == '0'))
                throw new InvalidOperationException("Compiled graph authoring identity is invalid.");
            return id;
        }

        internal static PlaytestSoloEvidence Capture(ResolvedSoloSettings settings, string request, string expected, long generation, int seed)
        {
            if (settings == null || settings.IsValidationFixture || settings.EncounterId != request || settings.DifficultyId != expected)
                throw new InvalidOperationException("Requested/resolved Solo profile mismatch.");
            var success = new float[settings.Catalog.Count];
            for (short i = 0; i < success.Length; i++)
                if (!settings.TryGetMiniGameSuccess(i, out success[i]))
                    throw new InvalidOperationException("Missing effective item outcome setting.");
            return new PlaytestSoloEvidence {
                requestedEncounterId = request, expectedDifficultyId = expected, encounterId = settings.EncounterId,
                difficultyId = settings.DifficultyId, botId = settings.BotId, generation = generation, humanDriverSeed = seed,
                graphName = settings.Graph.name, graphAuthoringId = GraphAuthoringId(settings.Graph),
                configurationVersions = settings.ConfigurationVersions.Select(x => x.Kind + "|" + x.Id + "|" + x.Version).ToArray(),
                catalogNames = settings.Catalog.Select(x => x == null ? "null" : x.ItemName).ToArray(),
                effectiveDelays = settings.ItemDelaySeconds.ToArray(), miniGameSuccess = success,
                minimumThinkSeconds = settings.MinimumThinkSeconds, maximumThinkSeconds = settings.MaximumThinkSeconds,
                readyPolicy = settings.ReadyPolicy.ToString(), readyReserveSeconds = settings.ReadyReserveSeconds,
                extraItemDelaySeconds = settings.ExtraItemDelaySeconds, retryBudget = settings.RetryBudget,
                provisionalTuning = settings.ProvisionalTuning, validationFixture = settings.IsValidationFixture,
                inheritOneVsOneGrants = settings.InheritOneVsOneGrants, judgement = settings.Judgement, coefficients = settings.Coefficients
            };
        }
    }
}
#endif
