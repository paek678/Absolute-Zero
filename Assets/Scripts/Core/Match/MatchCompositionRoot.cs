using System;
using System.Collections.Generic;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player.Identity;
using AbsoluteZero.Core.Session;
using Unity.Collections;
using UnityEngine;

namespace AbsoluteZero.Core.Match
{
    public enum MatchInitializationState { NotStarted, Applying, Complete, Failed }

    public class MatchCompositionRoot : MonoBehaviour
    {
        public static MatchCompositionRoot Instance { get; private set; }

        [SerializeField] GameModeRuleSO[] gameModeRules;
        [SerializeField] MatchViewBindings viewBindings;
        [SerializeField] bool requireViewBindings;
        public MatchViewBindings ViewBindings => viewBindings;

        PlayerRegistry _registry;
        public IReadOnlyPlayerRegistry Registry => _registry;
        public PlayerRegistry WritableRegistry => _registry;

        ItemManager _itemManager;
        MatchManager _matchManager;
        MatchNetworkState _networkState;
        MatchRoster _roster;
        public ItemManager ItemManager => _itemManager;
        public MatchManager MatchManager => _matchManager;
        public MatchNetworkState NetworkState => _networkState;
        public MatchRoster Roster => _roster;

        MatchConfig _activeConfig;
        public MatchConfig ActiveConfig => _activeConfig;
        public string InitializationFailure { get; private set; }
        public const float InitializationTimeout = 30f;
        MatchSessionRouter _sessionRouter;
        MatchSessionLease _sessionLease;
        public long Generation { get; private set; }
        public IReadOnlyList<MatchParticipantDescriptor> Participants { get; private set; }
            = Array.Empty<MatchParticipantDescriptor>();
        public MatchInitializationState InitializationState { get; private set; }
        public int InitializationCount { get; private set; }
        public bool IsSessionCurrent => _sessionLease == null || _sessionRouter.IsCurrent(_sessionLease);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField] bool allowHeadlessSoloValidation;
#endif
        public bool RequiresSoloPresentation
        {
            get
            {
                if (_activeConfig?.Mode != GameMode.Solo) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (allowHeadlessSoloValidation && _sessionRouter?.LaunchContext?.Solo?.IsValidationFixture == true)
                    return false;
#endif
                return true;
            }
        }

        public bool TryBeginInitialization()
        {
            if (!IsSessionCurrent || InitializationFailure != null
                || InitializationState != MatchInitializationState.NotStarted) return false;
            InitializationState = MatchInitializationState.Applying;
            return true;
        }

        public void CompleteInitialization()
        {
            if (!IsSessionCurrent || InitializationState != MatchInitializationState.Applying)
                throw new InvalidOperationException("Match initialization no longer owns this session");
            InitializationState = MatchInitializationState.Complete;
            InitializationCount++;
        }

        public void FailInitialization(string reason)
        {
            if (InitializationFailure != null) return;
            InitializationFailure = reason;
            InitializationState = MatchInitializationState.Failed;
            if (_networkState != null && _networkState.IsSpawned && _networkState.IsServer)
            {
                FixedString128Bytes summary = default;
                summary.CopyFromTruncated(reason ?? "Match initialization failed");
                _networkState.InitializationError.Value = summary;
            }
            Debug.LogError("[MatchInitialization] " + reason);
            if (_sessionLease?.Mode == GameMode.Solo && _sessionRouter.IsCurrent(_sessionLease))
                StopFailedSolo();
        }

        async void StopFailedSolo()
        {
            try { await _sessionRouter.StopAsync(); }
            catch (Exception error) { Debug.LogException(error); }
        }

        void Update()
        {
            if (InitializationFailure == null && _networkState != null && _networkState.IsSpawned
                && _networkState.InitializationError.Value.Length > 0)
                FailInitialization(_networkState.InitializationError.Value.ToString());
        }

        void Awake()
        {
            if (Instance != null)
            {
                Debug.LogError("[MatchCompositionRoot] Duplicate Root detected — destroying this instance");
                Destroy(this);
                return;
            }

            Instance = this;
            _registry = new PlayerRegistry();
            ValidateSceneReferences();
            SubscribeNetworkState();
            Debug.Log("[MatchCompositionRoot] Awake — Registry created, scene references validated");
        }

        void ValidateSceneReferences()
        {
            _itemManager = FindAnyObjectByType<ItemManager>();
            _matchManager = FindAnyObjectByType<MatchManager>();
            _networkState = FindAnyObjectByType<MatchNetworkState>();

            if (_itemManager == null)
                Debug.LogWarning("[MatchCompositionRoot] ItemManager not found in scene");
            if (_matchManager == null)
                Debug.LogWarning("[MatchCompositionRoot] MatchManager not found in scene");
            if (_networkState == null)
                Debug.LogWarning("[MatchCompositionRoot] MatchNetworkState not found in scene");

            // Headless test fixtures intentionally omit views. Migrated game scenes
            // carry this serialized reference and must satisfy the complete contract.
            if (requireViewBindings && viewBindings == null)
                FailInitialization("Required match view bindings are missing.");
            else if (viewBindings != null)
            {
                var errors = new List<string>();
                if (viewBindings.gameObject.scene != gameObject.scene)
                    errors.Add("Match view bindings belong to a different scene.");
                viewBindings.Validate(_itemManager != null ? _itemManager.GetAllItems() : null, errors);
                if (errors.Count > 0) FailInitialization(string.Join("; ", errors));
            }
        }

        void SubscribeNetworkState()
        {
            if (_networkState != null)
                _networkState.OnConfigSynced += OnConfigSynced;
        }

        void UnsubscribeNetworkState()
        {
            if (_networkState != null)
                _networkState.OnConfigSynced -= OnConfigSynced;
        }

        void OnConfigSynced(MatchConfigNetData netData)
        {
            AssembleMatchConfig(netData);
        }

        public MatchConfig AssembleMatchConfig(MatchConfigNetData netData)
        {
            if (InitializationFailure != null) return null;
            var mode = (GameMode)netData.Mode;
            IGameModeRule rule = FindRuleForMode(mode);
            if (rule == null)
            {
                FailInitialization($"No GameModeRule found for mode {mode}");
                return null;
            }

            _activeConfig = new MatchConfig(rule, netData.RequiredPlayerCount, mode);
            Debug.Log($"[MatchCompositionRoot] MatchConfig assembled — Mode={mode}, Players={netData.RequiredPlayerCount}");
            return _activeConfig;
        }

        IGameModeRule FindRuleForMode(GameMode mode)
        {
            if (mode == GameMode.Solo)
                return AppBootstrapper.Instance?.SessionRouter.LaunchContext?.Solo?.SharedOneVsOneRule;
            if (gameModeRules == null) return null;
            foreach (var rule in gameModeRules)
            {
                if (rule != null && rule.TargetMode == mode)
                    return rule;
            }
            return null;
        }

        public void InitializeRoster(MatchRoster roster)
        {
            _roster = roster;
        }

        public bool ServerBootstrapMatch()
        {
            if (InitializationFailure != null) return false;
            if (_activeConfig != null) return true;
            if (_networkState == null)
            {
                FailInitialization("MatchNetworkState is missing");
                return false;
            }
            if (!_networkState.IsSpawned || !_networkState.IsServer) return false;
            _sessionRouter = AppBootstrapper.Instance?.SessionRouter;
            _sessionLease = _sessionRouter?.Current;
            if (_sessionLease != null && !_sessionRouter.IsCurrent(_sessionLease)) return false;
            Generation = _sessionLease?.Generation ?? 0;
            var nsc = NetworkSessionCoordinator.Instance;
            var launch = _sessionRouter?.LaunchContext;
            GameMode mode = launch?.Mode ?? (nsc != null ? nsc.SelectedMode : GameMode.OneVsOne);
            int playerCount = launch?.RequiredSeats ?? (nsc != null ? nsc.SelectedPlayerCount : 2);

            if (_networkState != null)
            {
                if (!_networkState.ServerInitialize(new MatchConfigNetData
                {
                    Mode = (byte)mode,
                    RequiredPlayerCount = (byte)playerCount
                }))
                {
                    FailInitialization("Conflicting match configuration");
                    return false;
                }
                if (_activeConfig == null) AssembleMatchConfig(_networkState.Config.Value);
            }

            Debug.Log($"[MatchCompositionRoot] ServerBootstrapMatch — Mode={mode}, Players={playerCount}");
            return _activeConfig != null;
        }

        public MatchRoster ServerCreateRoster(int requiredCount, IEnumerable<ulong> connectedClientIds)
        {
            if (_roster != null) return _roster;
            if (!IsSessionCurrent || _networkState == null || !_networkState.IsSpawned || !_networkState.IsServer)
                return null;
            if (_activeConfig?.Mode == GameMode.Solo)
            {
                var nm = _networkState.NetworkManager;
                return CreateParticipantRoster(new[]
                {
                    new MatchParticipantDescriptor("solo:human", 0, PlayerControllerKind.Human, nm.LocalClientId, Generation),
                    new MatchParticipantDescriptor("solo:bot", 1, PlayerControllerKind.Bot, null, Generation)
                }, requiredCount);
            }
            var nsc = NetworkSessionCoordinator.Instance;
            SessionParticipantTable spt;

            if (nsc != null)
            {
                spt = nsc.BuildParticipantTable(
                    new List<ulong>(connectedClientIds), requiredCount);
            }
            else
            {
                // Test-only fallback: NSC should always exist in production (DDOL in LobbyScene).
                // This path allows unit/integration tests to run without full lobby flow.
                Debug.LogWarning("[MCR] NSC is null — using throwaway SPT (test-only path)");
                spt = new SessionParticipantTable();
                var sorted = new List<ulong>(connectedClientIds);
                sorted.Sort();
                byte s = 0;
                foreach (var cid in sorted)
                {
                    if (s >= requiredCount) break;
                    string pid = cid.ToString();
                    spt.Register(pid, "match");
                    spt.TryValidateAndBind(pid, "match", cid, out _);
                    s++;
                }
            }

            var participants = new List<MatchParticipantDescriptor>();
            foreach (var entry in spt.AllEntries)
                participants.Add(new MatchParticipantDescriptor(entry.ParticipantId, entry.SeatIndex,
                    PlayerControllerKind.Human, entry.IsConnected ? entry.CurrentClientId : null, Generation));
            return CreateParticipantRoster(participants, requiredCount);
        }

        MatchRoster CreateParticipantRoster(IReadOnlyList<MatchParticipantDescriptor> participants, int requiredCount)
        {
            var roster = new MatchRoster(requiredCount);
            roster.SetRegistry(_registry);
            if (!roster.Hydrate(participants))
            {
                FailInitialization("Roster hydration failed");
                roster.Dispose();
                return null;
            }

            InitializeRoster(roster);
            var copy = new MatchParticipantDescriptor[participants.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = participants[i];
            Participants = Array.AsReadOnly(copy);

            if (_activeConfig != null && _activeConfig.Mode == GameMode.Multi)
            {
                var dispatcher = DisconnectDispatcher.Instance;
                if (dispatcher != null)
                    dispatcher.SetHandler(roster);
            }

            Debug.Log($"[MatchCompositionRoot] Roster created — {requiredCount} seats");
            return roster;
        }

        void OnDestroy()
        {
            if (Instance != this) return;

            UnsubscribeNetworkState();

            if (_roster != null)
            {
                var dispatcher = Network.DisconnectDispatcher.Instance;
                if (dispatcher != null)
                    dispatcher.RestoreDefaultIfCurrent(_roster);
                _roster.Dispose();
                _roster = null;
            }

            _registry?.Clear();
            _activeConfig = null;
            Instance = null;
            Debug.Log("[MatchCompositionRoot] Destroyed — Registry + Roster cleaned up");
        }
    }
}
