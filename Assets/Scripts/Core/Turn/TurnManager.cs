using System.Collections;
using System.Collections.Generic;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using AbsoluteZero.Core.Network;
using AbsoluteZero.Core.Player.Identity;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Turn
{
    public enum GhostSkillRequestResult : byte
    {
        Accepted,
        InvalidContext,
        InputClosed,
        InvalidActor,
        InvalidTarget,
        Unavailable,
        TargetAlreadyAffected,
        MatchEnded,
        StaleRequest
    }

    public class TurnManager : NetworkBehaviour, ITurnContext, IPlayerTurnCancellation
    {
        public static TurnManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] float prepDuration = 20f;

        public readonly NetworkVariable<TurnPhase> CurrentPhase = new(
            TurnPhase.WaitingForPlayers, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> TurnNumber = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<double> PrepStartServerTime = new(
            0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> PrepDuration = new(
            20f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> RemainingTime = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> LastRoundWinner = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<EnvironmentType> ActiveEnvironment = new(
            EnvironmentType.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<byte> FirstReadySeat = new(
            byte.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public static event System.Action<CombatResultData> OnCombatResult;
        public static event System.Action<EnvironmentType> OnEnvironmentAnnounced;

        readonly PrepInputWindow _prepInput = new();
        MatchCompositionRoot _prepMatch;
        ulong _prepSequence;
        public event System.Action<PrepInputSnapshot> PrepInputOpened;

        public bool TryGetPrepInputSnapshot(out PrepInputSnapshot snapshot)
        {
            snapshot = null;
            var match = MatchCompositionRoot.Instance;
            if (!IsSpawned || !IsServer || NetworkManager == null || NetworkManager.ShutdownInProgress
                || !NetworkManager.IsListening || match == null || !match.IsSessionCurrent
                || !ReferenceEquals(match, _prepMatch) || !ReferenceEquals(Instance, this)
                || match.InitializationFailure != null || CurrentPhase.Value != TurnPhase.PrepPhase
                || _ghostRoundEndTriggered || _multiTerminalWinnerMask != 0 || IsDeathmatchGrantInProgress
                || _matchManager == null) return false;
            return _prepInput.TryRead(new PrepInputKey(match.Generation, _matchManager.RoundNumber.Value,
                TurnNumber.Value, _prepSequence), NetworkManager.ServerTime.Time, out snapshot);
        }

        PlayerState[] _players = new PlayerState[2];
        PlayerModifiers[] _modifiers = new PlayerModifiers[2];
        TemperatureSystem _tempSystem;
        CombatEngine _combatEngine;
        BuffDebuffSystem _buffSystem;
        ItemManager _itemManager;
        MatchManager _matchManager;
        PresentationBarrier _barrier;
        EnvironmentRuleService _envRules;
        RoundLifecycleService _roundLifecycle;
        CombatResolver _combatResolver;
        AuthoritativeDeathService _deathService;
        GhostSkillService _ghostSkillService;

        [SerializeField, Min(1f)] float presentationTimeoutSeconds = 10f;

        float[] _tempsAtTurnStart = new float[2];
        uint _resultSequence;
        Coroutine _rematchWaitHandle;
        GameMode _gameMode = GameMode.OneVsOne;
        IGameModeRule _gameRule;

        const float EMOTE_DISPLAY_SEC = 1.0f;
        bool _emoteWindowClosed;
        bool _ghostRoundEndTriggered;
        DeathmatchGrantCoordinator _deathmatchGrantCoordinator;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DebugInjectTopUpFailureAfterFirst()
            => _deathmatchGrantCoordinator?.DebugFailAfterFirstAttempts(1);
        public void DebugInjectTopUpFailureAfterBothAttempts()
            => _deathmatchGrantCoordinator?.DebugFailAfterFirstAttempts(2);
        public void DebugRetryDeathmatchTopUp() => TryGrantDeathmatchItems();
#endif
        uint _nextGhostCastId;
        public bool IsDeathmatchGrantInProgress => _deathmatchGrantCoordinator?.IsAdmissionClosed == true;
        public bool IsDeathmatchGrantFaulted => _deathmatchGrantCoordinator?.IsFaulted == true;
        byte _multiTerminalWinnerMask;
        bool _multiPresentationInFlight;
        bool _multiRoundEndInProgress;
        bool _ghostInputOpen;
        readonly GhostRequestHistory _ghostRequests = new();
        public static event System.Action<uint, GhostSkillRequestResult> OnGhostSkillRequestResult;
        public bool AcceptEmotes => IsSpawned && CurrentPhase.Value == TurnPhase.PrepPhase && !_emoteWindowClosed;

        static readonly WaitForSeconds _waitHalf = new(0.5f);
        static readonly WaitForSeconds _waitOne = new(1f);
        static readonly WaitForSeconds _waitTwo = new(2f);
        static readonly WaitForSeconds _waitThree = new(3f);
        static readonly WaitForSeconds _waitFour = new(4f);
        static readonly WaitForSeconds _waitFive = new(5f);
        static readonly WaitForSeconds _waitSix = new(6f);
        static readonly WaitForSeconds _waitKidsSteal = new(EnvironmentRuleService.KIDS_STEAL_STAGING_SECONDS);
        static readonly WaitForSeconds _waitAmbulanceBlanket = new(EnvironmentRuleService.AMBULANCE_BLANKET_STAGING_SECONDS);

        bool IsMulti => _gameMode == GameMode.Multi;

        public PlayerState GetPlayer(int index) => _players[index];
        public PlayerModifiers[] GetModifiers() => _modifiers;
        public TemperatureSystem GetTempSystem() => _tempSystem;
        public BuffDebuffSystem GetBuffSystem() => _buffSystem;
        public ItemDropTable GetDropTable() => _itemManager != null
            ? _itemManager.GetRuleAwareDropTable(_gameRule)
            : null;

        TurnPhase ITurnContext.Phase => CurrentPhase.Value;
        bool ITurnContext.CanAcceptEmotes => AcceptEmotes;
        double ITurnContext.PrepStartTime => PrepStartServerTime.Value;
        float ITurnContext.PrepDurationSeconds => PrepDuration.Value;

        void ITurnContext.PublishItemUsed(byte playerIdx, byte slotIdx, byte category, bool isSub)
            => OnItemUsedClientRpc(playerIdx, slotIdx, category, isSub);

        void ITurnContext.PublishOpponentRevealed(byte playerIdx, short itemId)
            => RevealOpponentItemClientRpc(playerIdx, itemId);

        void OnSeatKilledClearBuffs(byte seat, DamageSource _)
            => _buffSystem?.ClearForSeat(seat);

        // ─── IPlayerTurnCancellation ─────────────────────────

        public void CancelTurnParticipation(byte seat)
        {
            if (!IsServer) return;
            if (_players == null || seat >= _players.Length) return;
            var player = _players[seat];
            if (player == null) return;
            player.ClearPendingIntent();
            player.CancelPendingMiniGame();
            player.IsReady.Value = false;
            player.HasSelectedItem.Value = false;
        }

        public void ClearPendingIntent(byte seat)
        {
            if (!IsServer) return;
            if (_players == null || seat >= _players.Length) return;
            _players[seat]?.ClearPendingIntent();
        }

        // ─── Publication Methods ─────────────────────────────

        void PublishPhaseChanged(TurnPhase phase, int turnNumber)
            => OnPhaseChangedClientRpc(phase, turnNumber);

        void PublishCombatResult(CombatResultData data)
            => OnCombatResultClientRpc(data);

        void PublishEnvironment(EnvironmentType env)
            => AnnounceEnvironmentClientRpc(env);

        void PublishDeathSequence(int loserIndex, bool endsMatch)
            => TriggerDeathSequenceRpc(loserIndex, endsMatch);

        void PublishKidsStealStaging()
            => KidsStealStagingClientRpc();

        void PublishAmbulanceBlanketStaging(bool p1IsLower)
            => AmbulanceBlanketStagingClientRpc(p1IsLower);

        void PublishAmbulanceBlanketStagingMulti(int healSeat)
            => AmbulanceBlanketStagingMultiClientRpc(healSeat);

        void PublishDebugLog(string message)
            => CombatDebugLogRpc(message);

        void PublishReviveVisuals()
            => ReviveVisualsClientRpc();

        // ─── Lifecycle ───────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Instance = this;

            if (IsServer)
            {
                _tempSystem = new TemperatureSystem();
                _combatEngine = new CombatEngine();
                _combatResolver = new CombatResolver();
                _buffSystem = new BuffDebuffSystem();
                _barrier = new PresentationBarrier();
                _envRules = new EnvironmentRuleService();
                _roundLifecycle = new RoundLifecycleService();
                _deathmatchGrantCoordinator = new DeathmatchGrantCoordinator(
                    reason => MatchCompositionRoot.Instance?.FailInitialization(reason));

                var mcr = MatchCompositionRoot.Instance;
                if (mcr != null)
                {
                    _itemManager = mcr.ItemManager;
                    _matchManager = mcr.MatchManager;
                }

                if (_itemManager == null)
                    _itemManager = FindAnyObjectByType<ItemManager>();
                if (_matchManager == null)
                    _matchManager = FindAnyObjectByType<MatchManager>();

                NetworkManager.OnClientDisconnectCallback += OnClientDisconnectForBarrier;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnectForRematch;
                StartCoroutine(WaitForPlayersRoutine());
            }
        }

        public override void OnNetworkDespawn()
        {
            _prepInput.Close();
            PrepInputOpened = null;
            if (_rematchWaitHandle != null)
            {
                StopCoroutine(_rematchWaitHandle);
                _rematchWaitHandle = null;
            }
            StopAllCoroutines();
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectForBarrier;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnectForRematch;
            }
            if (_deathService != null)
                _deathService.OnSeatKilled -= OnSeatKilledClearBuffs;
            var rosterForUnsub = MatchCompositionRoot.Instance?.Roster;
            if (rosterForUnsub != null)
                rosterForUnsub.OnPlayerDisconnectedFromSeat -= OnSeatDisconnectedForceGhost;
            _ghostSkillService?.Dispose();
            _ghostSkillService = null;
            _ghostInputOpen = false;
            _ghostRequests.Clear();
            _barrier?.Reset();
            OnMultiMatchOutcome = null;
            OnOpponentRevealed = null;
            // Unmigrated transient compatibility events belong to the active facade.
            // A late despawn of an old object cannot erase the new match's listeners.
            if (Instance == this)
            {
                OnCombatResult = null;
                OnMultiCombatResult = null;
                OnMultiDeathPresentation = null;
                OnGhostSkillUsed = null;
                OnGhostSkillUsedSequenced = null;
                OnGhostSkillRequestResult = null;
                OnEnvironmentAnnounced = null;
                Instance = null;
            }
            base.OnNetworkDespawn();
        }

        void OnClientDisconnectForBarrier(ulong clientId)
        {
            _barrier?.HandleDisconnect(clientId);
        }

        void OnClientDisconnectForRematch(ulong clientId)
        {
            if (!IsServer) return;
            _matchManager?.RecordDisconnect(clientId);
        }

        void OnSeatDisconnectedForceGhost(ulong clientId, byte seat)
        {
            if (!IsServer || _deathService == null) return;
            var roster = MatchCompositionRoot.Instance?.Roster;
            if (roster == null) return;
            if (roster.GetLifeState(seat) != LifeState.Alive) return;

            Debug.Log($"[TurnManager] Seat {seat} disconnected while Alive — forcing Ghost");
            _deathService.TryKill(seat, DamageSource.None);
            _deathService.FlushDeathQueue();

            if (CurrentPhase.Value == TurnPhase.RoundOver || _multiTerminalWinnerMask != 0) return;
            if (!TryGrantDeathmatchItems()) return;
            // The running phase observes the updated roster at its next boundary.
            // Never start a second phase driver from a network callback.
        }

        bool TryGetCurrentMultiRoundEnd(out int winner)
        {
            winner = -1;
            if (!IsMulti || _deathService == null || _multiTerminalWinnerMask != 0) return false;
            var end = _deathService.EvaluateRoundEnd();
            if (!end.IsRoundOver) return false;
            winner = end.IsDraw ? -1 : end.WinnerSeat;
            return true;
        }

        public void ReceivePresentationAck(uint sequence, ulong senderClientId)
        {
            if (!IsServer) return;
            _barrier?.ReceiveAck(sequence, senderClientId);
        }

        List<ulong> GetPresentationViewers(MatchCompositionRoot mcr)
        {
            var viewers = new List<ulong>();
            foreach (var binding in mcr.Registry.Players)
            {
                if (!binding.IsValid || binding.Identity.ControllerKind != PlayerControllerKind.Human
                    || !binding.Identity.ClientId.HasValue) continue;
                ulong id = binding.Identity.ClientId.Value;
                if (NetworkManager.ConnectedClients.ContainsKey(id) && !viewers.Contains(id)) viewers.Add(id);
            }
            return viewers;
        }

        // ─── Player Discovery ────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static System.Action<MatchCompositionRoot> DebugBeforeReadyHandoff;
        public static bool DebugFailAfterFirstInitialGrant;
        public static string DebugInitialGrantFailureMessage;
        public void DebugRetryInitialDiscovery() => StartCoroutine(WaitForPlayersRoutine());
        public void DebugHoldTurnForPresentationValidation()
        {
            if (!IsServer || _gameMode != GameMode.Solo) throw new System.InvalidOperationException("Solo server required");
            StopAllCoroutines();
            foreach (var player in _players) if (player != null) player.IsFanActive.Value = false;
        }
        public BarrierState DebugPresentationBarrierState => _barrier?.State ?? BarrierState.Idle;
        public int DebugPresentationPendingCount => _barrier?.DebugPendingCount ?? 0;
        public ulong[] DebugExpectedPresentationViewers => GetPresentationViewers(MatchCompositionRoot.Instance).ToArray();
        public uint DebugPresentForValidation(CombatResultData data)
        {
            if (!IsServer || _gameMode != GameMode.Solo) throw new System.InvalidOperationException("Solo server required");
            data.ResultSequence = ++_resultSequence;
            if (!_barrier.Begin(data.ResultSequence, GetPresentationViewers(MatchCompositionRoot.Instance)))
                throw new System.InvalidOperationException("Presentation already active");
            PublishCombatResult(data);
            return data.ResultSequence;
        }
        public void DebugTriggerDeathForValidation(byte seat, bool endsMatch)
        {
            if (!IsServer || _gameMode != GameMode.Solo) throw new System.InvalidOperationException("Solo server required");
            PublishDeathSequence(seat, endsMatch);
        }
#endif

        IEnumerator WaitForPlayersRoutine()
        {
            var mcr = MatchCompositionRoot.Instance;
            if (mcr == null)
            {
                Debug.LogError("[TurnManager] MatchCompositionRoot not found — cannot discover players");
                yield break;
            }
            if (mcr.InitializationState != MatchInitializationState.NotStarted) yield break;
            CurrentPhase.Value = TurnPhase.WaitingForPlayers;
            float deadline = Time.realtimeSinceStartup + MatchCompositionRoot.InitializationTimeout;
            while (mcr != null && mcr.ActiveConfig == null)
            {
                if (!IsSpawned || !mcr.IsSessionCurrent || mcr.InitializationFailure != null) yield break;
                if (IsServer) mcr.ServerBootstrapMatch();
                if (Time.realtimeSinceStartup >= deadline)
                {
                    mcr.FailInitialization("Timed out waiting for network match configuration");
                    yield break;
                }
                yield return _waitHalf;
            }
            if (mcr == null || !IsSpawned || !mcr.IsSessionCurrent) yield break;
            _gameMode = mcr.ActiveConfig.Mode;
            _gameRule = mcr.ActiveConfig.Rule;
            int requiredCount = mcr.ActiveConfig.RequiredPlayerCount;
            var registry = mcr.WritableRegistry;
            deadline = Time.realtimeSinceStartup + MatchCompositionRoot.InitializationTimeout;

            if (_gameMode == GameMode.Solo)
            {
                if (mcr.ServerCreateRoster(requiredCount, NetworkManager.ConnectedClientsIds) == null) yield break;
                var spawner = PlayerSpawnManager.Instance;
                string sceneError = "PlayerSpawnManager is missing";
                while (spawner == null || !spawner.TryPrepareMatchScene(mcr.Generation, out sceneError))
                {
                    if (!IsSpawned || mcr == null || !mcr.IsSessionCurrent || mcr.InitializationFailure != null) yield break;
                    if (Time.realtimeSinceStartup >= deadline)
                    {
                        mcr.FailInitialization("Solo spawn scene did not become ready: " + sceneError);
                        yield break;
                    }
                    yield return _waitHalf;
                    spawner = PlayerSpawnManager.Instance;
                }
                if (!spawner.TrySpawnSoloParticipants(mcr.Participants, out string spawnError))
                {
                    mcr.FailInitialization("Solo participants could not spawn: " + spawnError);
                    yield break;
                }
            }
            else
            {
                while (registry.TotalCount < requiredCount)
                {
                    if (!IsSpawned || mcr == null || !mcr.IsSessionCurrent || mcr.InitializationFailure != null) yield break;
                    if (Time.realtimeSinceStartup >= deadline)
                    {
                        mcr.FailInitialization($"Timed out waiting for players: {registry.TotalCount}/{requiredCount}");
                        yield break;
                    }
                    yield return _waitHalf;
                }
                if (mcr.ServerCreateRoster(requiredCount, NetworkManager.ConnectedClientsIds) == null) yield break;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugBeforeReadyHandoff?.Invoke(mcr);
#endif
            PlayerBinding[] ready;
            string bindingError;
            while (true)
            {
                if (!IsSpawned || mcr == null || !mcr.IsSessionCurrent || mcr.InitializationFailure != null) yield break;
                TurnParticipantSetup.AssignPending(registry, mcr.Participants);
                if (registry.TryCaptureReadyBindings(mcr.Participants, out ready, out bindingError)) break;
                if (Time.realtimeSinceStartup >= deadline)
                {
                    mcr.FailInitialization("Ready participant handoff failed: " + bindingError);
                    yield break;
                }
                yield return _waitHalf;
            }
            // NetworkBehaviour references can exist before their OnNetworkSpawn ran.
            // In particular, ItemManager has no drop table until its server spawn.
            while (_itemManager == null || !_itemManager.IsSpawned || _itemManager.GetDropTable() == null
                || _matchManager == null || !_matchManager.IsSpawned)
            {
                if (!IsSpawned || mcr == null || !mcr.IsSessionCurrent || mcr.InitializationFailure != null) yield break;
                if (Time.realtimeSinceStartup >= deadline)
                {
                    mcr.FailInitialization("Match services did not finish network initialization");
                    yield break;
                }
                _itemManager = mcr.ItemManager;
                _matchManager = mcr.MatchManager;
                yield return _waitHalf;
            }
            if (!registry.TryCaptureReadyBindings(mcr.Participants, out ready, out bindingError))
            {
                mcr.FailInitialization("Participants changed while services initialized: " + bindingError);
                yield break;
            }
            if (!InitializeReadyParticipants(mcr, ready)) yield break;
            while (mcr.RequiresSoloPresentation && !TurnParticipantSetup.AreViewsReady(ready))
            {
                if (!IsSpawned || !mcr.IsSessionCurrent || mcr.InitializationFailure != null) yield break;
                if (Time.realtimeSinceStartup >= deadline)
                {
                    mcr.FailInitialization("Solo participant presentation did not finish initialization");
                    yield break;
                }
                yield return null;
            }
            yield return StartCoroutine(PrepPhaseRoutine());
        }

        bool InitializeReadyParticipants(MatchCompositionRoot mcr, PlayerBinding[] ready)
        {
            if (!mcr.TryBeginInitialization()) return false;
            try
            {
                TurnParticipantSetup.ValidateBatch(ready, _gameRule, mcr.ActiveConfig.RequiredPlayerCount);
                _players = new PlayerState[ready.Length];
                _modifiers = new PlayerModifiers[ready.Length];
                _tempsAtTurnStart = new float[ready.Length];
                TurnParticipantSetup.BindStates(ready, _players, this);
                var roster = mcr.Roster;
                roster.SetModifiersSource(seat => _modifiers[seat], (seat, value) => _modifiers[seat] = value);
                _ghostSkillService?.Dispose();
                _ghostSkillService = null;
                if (IsMulti)
                {
                    _deathService = new AuthoritativeDeathService(roster, mcr.NetworkState);
                    _deathService.SetTurnCancellation(this);
                    _deathService.OnSeatKilled += OnSeatKilledClearBuffs;
                    roster.OnPlayerDisconnectedFromSeat += OnSeatDisconnectedForceGhost;
                    if (_gameRule.EnableGhostSystem)
                    {
                        _ghostSkillService = new GhostSkillService(roster, _modifiers);
                        mcr.NetworkState.ServerBeginGhostMatch();
                        _ghostSkillService.BeginMatch(mcr.NetworkState, ready.Length);
                    }
                }
                for (int i = 0; i < _players.Length; i++)
                {
                    if (IsMulti) _itemManager.InitializePlayerInventory(ready[i].Inventory, _gameRule);
                    else _itemManager.InitializePlayerInventory(ready[i].Inventory);
                    if (!ready[i].Inventory.IsRegistryReady || ready[i].Inventory.SlotStates.Count < 4)
                        throw new System.InvalidOperationException($"Initial inventory grant failed for seat {i}");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (i == 0 && DebugFailAfterFirstInitialGrant)
                    {
                        DebugFailAfterFirstInitialGrant = false;
                        throw new System.InvalidOperationException(DebugInitialGrantFailureMessage
                            ?? "Injected initial grant failure after seat 0");
                    }
#endif
                }
                if (!_matchManager.FixMatchRoster(_players))
                    throw new System.InvalidOperationException("Logical match membership could not be fixed");
                _matchManager.StartRound();
                if (_matchManager.RoundNumber.Value != 1 || _matchManager.CurrentMatchState.Value != MatchState.RoundInProgress)
                    throw new System.InvalidOperationException("Initial round did not start exactly once");
                mcr.CompleteInitialization();
                Debug.Log($"[TurnManager] Initialized {ready.Length} logical participants, generation={mcr.Generation}");
                return true;
            }
            catch (System.Exception error)
            {
                mcr.FailInitialization(error.Message);
                return false;
            }
        }

        // ─── Prep Phase ──────────────────────────────────────

        IEnumerator PrepPhaseRoutine()
        {
            _prepInput.Close();
            if (!IsSpawned || IsDeathmatchGrantFaulted) yield break;
            if (_multiTerminalWinnerMask != 0 || _multiRoundEndInProgress) yield break;
            if (TryGetCurrentMultiRoundEnd(out int entryWinner))
            {
                yield return StartCoroutine(HandleRoundEnd(entryWinner));
                yield break;
            }
            TurnNumber.Value++;
            LastRoundWinner.Value = -1;
            FirstReadySeat.Value = byte.MaxValue;
            _ghostRoundEndTriggered = false;

            if (IsMulti)
            {
                _roundLifecycle.ResetForNewTurn(_players, _modifiers);
                var nState = MatchCompositionRoot.Instance?.NetworkState;
                if (nState != null)
                {
                    nState.ServerTickAllCooldowns();
                    _ghostSkillService?.BeginTurn(nState, TurnNumber.Value);
                }
            }
            else
                _roundLifecycle.ResetForNewTurn(_players[0], _players[1], _modifiers);

            _envRules.LogActiveEnvironment(ActiveEnvironment.Value, TurnNumber.Value);

            float currentPrepDuration = _envRules.GetPrepDuration(ActiveEnvironment.Value, prepDuration);

            if (_envRules.ShouldApplyKidsEffect(ActiveEnvironment.Value, TurnNumber.Value))
            {
                Debug.Log("[ENV] Kids: steal staging + removing 1 random item from each player");
                PublishKidsStealStaging();
                yield return _waitKidsSteal;
                PrepTurnOperations.RemoveKidsItems(_players, IsMulti, _envRules);
            }

            if (_envRules.ShouldApplyAmbulanceEffect(ActiveEnvironment.Value, TurnNumber.Value))
            {
                int healTarget;
                if (IsMulti)
                {
                    var roster = MatchCompositionRoot.Instance?.Roster;
                    healTarget = _envRules.DetermineAmbulanceTargetMulti(_players, roster);
                    Debug.Log($"[ENV] Ambulance Multi: Turn 3 triggered — target=P{healTarget}");
                }
                else
                {
                    Debug.Log($"[ENV] Ambulance: Turn 3 triggered — P0={_players[0].Temperature.Value:F1}° P1={_players[1].Temperature.Value:F1}°");
                    healTarget = _envRules.DetermineAmbulanceTarget(
                        _players[0].Temperature.Value, _players[1].Temperature.Value);
                }

                if (healTarget >= 0)
                {
                    if (IsMulti)
                        PublishAmbulanceBlanketStagingMulti(healTarget);
                    else
                        PublishAmbulanceBlanketStaging(healTarget == 0);
                    yield return _waitAmbulanceBlanket;
                }

                if (healTarget >= 0 && healTarget < _players.Length && _players[healTarget] != null)
                {
                    _tempSystem.ApplyHeal(_players[healTarget], 10f);
                    Debug.Log($"[ENV] Ambulance: P{healTarget} healed +10° → {_players[healTarget].Temperature.Value:F1}° (lower temp)");
                }
                else if (healTarget < 0)
                {
                    Debug.Log("[ENV] Ambulance: same temp — no heal applied");
                }
            }

            PrepStartServerTime.Value = NetworkManager.ServerTime.Time;
            PrepDuration.Value = currentPrepDuration;

            _emoteWindowClosed = false;
            _ghostInputOpen = IsMulti;
            for (int i = 0; i < _players.Length; i++)
                _tempsAtTurnStart[i] = _players[i] != null ? _players[i].Temperature.Value : 0f;

            _tempSystem.ResetTimer();
            float elapsed = 0f;
            bool skipFirstFanTick = true;

            RemainingTime.Value = Mathf.CeilToInt(currentPrepDuration);

            // Publish only after resets, timing and temperature baselines are committed.
            // Keep the existing elapsed/first-cooling-tick behavior below unchanged.
            CurrentPhase.Value = TurnPhase.PrepPhase;
            _prepMatch = MatchCompositionRoot.Instance;
            var prep = _prepInput.Commit(new PrepInputKey(MatchCompositionRoot.Instance.Generation,
                _matchManager.RoundNumber.Value, TurnNumber.Value, checked(++_prepSequence)),
                PrepStartServerTime.Value, currentPrepDuration, _tempsAtTurnStart);
            PublishPhaseChanged(TurnPhase.PrepPhase, TurnNumber.Value);
            PrepInputOpened?.Invoke(prep);

            while (elapsed < currentPrepDuration)
            {
                if (IsDeathmatchGrantFaulted) yield break;
                if (_ghostRoundEndTriggered) yield break;
                if (IsMulti && _multiPresentationInFlight)
                {
                    yield return null;
                    continue;
                }

                if (TryGetCurrentMultiRoundEnd(out int disconnectWinner))
                {
                    yield return StartCoroutine(HandleRoundEnd(disconnectWinner));
                    yield break;
                }

                float dt = Time.deltaTime;
                elapsed += dt;
                _tempSystem.Accumulate(dt);

                int newRemaining = Mathf.CeilToInt(currentPrepDuration - elapsed);
                if (newRemaining != RemainingTime.Value)
                    RemainingTime.Value = Mathf.Max(0, newRemaining);

                float recoveryRate = _envRules.GetRecoveryRate(ActiveEnvironment.Value);
                var dropTable = GetDropTable();

                while (_tempSystem.ConsumeTick())
                {
                    if (IsMulti)
                    {
                        int[] scoresBefore = CaptureKillScores();
                        PrepTurnOperations.ApplyMultiTick(_players, _modifiers, _tempSystem, skipFirstFanTick, recoveryRate);

                        for (int i = 0; i < _players.Length; i++)
                        {
                            if (_players[i] == null || !_tempSystem.IsDead(_players[i]) || _deathService == null)
                                continue;
                            _deathService.TryKill((byte)i, DamageSource.None);
                        }
                        _deathService?.FlushDeathQueue();
                        byte deathMask = _deathService?.ConsumeDeathMask() ?? 0;
                        byte matchWinners = FindNewMultiWinners(scoresBefore);
                        if (matchWinners != 0)
                        {
                            LatchMultiVictory(matchWinners);
                            if (deathMask != 0)
                                yield return StartCoroutine(PresentMultiDeathsAndWait(deathMask, true));
                            CompleteLatchedMultiMatch(_resultSequence);
                            yield break;
                        }

                        int maxRandom = _gameRule != null ? _gameRule.MaxRandomItems : int.MaxValue;
                        PrepTurnOperations.GrantMultiThresholds(_players, _tempSystem, dropTable, maxRandom);

                        if (deathMask != 0 && !TryGrantDeathmatchItems()) yield break;
                        var roundEnd = _deathService?.EvaluateRoundEnd() ?? default;
                        if (deathMask != 0)
                            yield return StartCoroutine(PresentMultiDeathsAndWait(deathMask, roundEnd.IsRoundOver));
                        if (roundEnd.IsRoundOver)
                        {
                            yield return StartCoroutine(HandleRoundEnd(
                                roundEnd.IsDraw ? -1 : roundEnd.WinnerSeat));
                            yield break;
                        }
                    }
                    else
                    {
                        PrepTurnOperations.ApplyDuelTick(_players, _tempSystem, skipFirstFanTick, recoveryRate, dropTable);
                    }
                    skipFirstFanTick = false;
                }

                if (!IsMulti)
                {
                    var deathWinner = _roundLifecycle.DetermineDeathWinner(_tempSystem, _players[0], _players[1]);
                    if (deathWinner.HasValue)
                    {
                        yield return StartCoroutine(HandleRoundEnd(deathWinner.Value));
                        yield break;
                    }
                }

                bool allReady = PrepTurnOperations.AllReady(_players, IsMulti);
                if (allReady) break;

                yield return null;
            }

            if (_ghostRoundEndTriggered || IsDeathmatchGrantFaulted) yield break;

            _prepInput.Close();
            _ghostInputOpen = false;
            RemainingTime.Value = 0;

            PrepTurnOperations.CompleteReady(_players, IsMulti, _roundLifecycle);

            byte firstSeat = PrepTurnOperations.FirstReadySeat(_players, IsMulti);
            FirstReadySeat.Value = firstSeat;

            _emoteWindowClosed = true;
            double lastEmote = PrepTurnOperations.LastEmoteTime(_players, IsMulti);

            float emoteRemain = (float)(EMOTE_DISPLAY_SEC - (NetworkManager.ServerTime.Time - lastEmote));
            for (float t = 0f; t < emoteRemain; t += Time.deltaTime)
            {
                if (_ghostRoundEndTriggered || IsDeathmatchGrantFaulted) yield break;
                yield return null;
            }

            if (_ghostRoundEndTriggered || IsDeathmatchGrantFaulted) yield break;
            yield return StartCoroutine(AttackPhaseRoutine());
        }

        // ─── Multi Combat Helpers ─────────────────────────────

        int[] CaptureKillScores()
        {
            var scores = MatchCompositionRoot.Instance?.NetworkState?.KillScores;
            var copy = new int[_players.Length];
            if (scores != null)
                for (int i = 0; i < copy.Length && i < scores.Count; i++) copy[i] = scores[i];
            return copy;
        }

        byte FindNewMultiWinners(int[] before)
        {
            if (!IsMulti || _gameRule == null || before == null) return 0;
            return MultiVictoryRules.FindThresholdCrossings(
                before, CaptureKillScores(), _gameRule.KillsToWin);
        }

        void LatchMultiVictory(byte winnerMask)
        {
            if (_multiTerminalWinnerMask != 0 || winnerMask == 0) return;
            _prepInput.Close();
            _multiTerminalWinnerMask = winnerMask;
            _ghostRoundEndTriggered = true;
            for (byte seat = 0; seat < _players.Length; seat++)
                CancelTurnParticipation(seat);
        }

        // ─── Attack Phase ────────────────────────────────────

        IEnumerator AttackPhaseRoutine()
        {
            _prepInput.Close();
            if (!IsSpawned || IsDeathmatchGrantFaulted) yield break;
            if (_multiTerminalWinnerMask != 0 || _multiRoundEndInProgress) yield break;
            if (TryGetCurrentMultiRoundEnd(out int entryWinner))
            {
                yield return StartCoroutine(HandleRoundEnd(entryWinner));
                yield break;
            }
            CurrentPhase.Value = TurnPhase.AttackPhase;
            PublishPhaseChanged(TurnPhase.AttackPhase, TurnNumber.Value);

            Debug.Log($"[COMBAT] ========== TURN {TurnNumber.Value} ATTACK PHASE START ==========");
            var tempLogStart = new System.Text.StringBuilder("[COMBAT]");
            for (int i = 0; i < _players.Length; i++)
                if (_players[i] != null) tempLogStart.Append($" P{i} temp={_players[i].Temperature.Value:F1}°");
            Debug.Log(tempLogStart.ToString());

            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null) continue;
                _players[i].IsBasicBlocked.Value = false;
            }

            Debug.Log($"[COMBAT] --- Processing delayed buffs/debuffs ---");
            if (IsMulti)
            {
                _buffSystem.BeginMultiTurnStart();
                while (_buffSystem.TryProcessNextDueMulti(_players, out var effect))
                {
                    if (IsDeathmatchGrantFaulted) yield break;
                    int[] scoresBefore = CaptureKillScores();
                    if (effect.CausedDeath && _deathService != null)
                    {
                        var src = effect.SourceSeat != DamageSource.InvalidSeat
                            ? DamageSource.Create(effect.SourceSeat, DamageOrigin.DelayedEffect)
                            : DamageSource.None;
                        _deathService.TryKill((byte)effect.TargetSeat, src);
                        _deathService.FlushDeathQueue();
                    }

                    byte deathMask = _deathService?.ConsumeDeathMask() ?? 0;
                    byte matchWinners = FindNewMultiWinners(scoresBefore);
                    if (matchWinners != 0)
                    {
                        LatchMultiVictory(matchWinners);
                        if (deathMask != 0)
                            yield return StartCoroutine(PresentMultiDeathsAndWait(deathMask, true));
                        CompleteLatchedMultiMatch(_resultSequence);
                        yield break;
                    }

                    if (!TryGrantDeathmatchItems()) yield break;
                    var roundEnd = _deathService.EvaluateRoundEnd();
                    if (deathMask != 0)
                        yield return StartCoroutine(PresentMultiDeathsAndWait(deathMask, roundEnd.IsRoundOver));
                    roundEnd = _deathService.EvaluateRoundEnd();
                    if (roundEnd.IsRoundOver)
                    {
                        int winner = roundEnd.IsDraw ? -1 : roundEnd.WinnerSeat;
                        yield return StartCoroutine(HandleRoundEnd(winner));
                        yield break;
                    }
                }
            }
            else
                _buffSystem.ProcessTurnStart(_players[0], _players[1]);

            var tempLog = new System.Text.StringBuilder("[COMBAT] After buffs:");
            for (int i = 0; i < _players.Length; i++)
                if (_players[i] != null) tempLog.Append($" P{i}={_players[i].Temperature.Value:F1}°");
            Debug.Log(tempLog.ToString());

            var mainNames = new string[_players.Length];
            var subNames = new string[_players.Length];
            for (int i = 0; i < _players.Length; i++)
            {
                if (_players[i] == null) { mainNames[i] = "N/A"; subNames[i] = "N/A"; continue; }
                var q = _players[i].GetActionQueue();
                mainNames[i] = q.selectedAction.HasValue ? q.selectedAction.Value.ItemData.ItemName : "NONE";
                subNames[i] = q.subAction.HasValue ? q.subAction.Value.ItemData.ItemName : "NONE";
            }
            var actionLog = new System.Text.StringBuilder("[COMBAT] Actions:");
            for (int i = 0; i < _players.Length; i++)
                actionLog.Append($" P{i}: main={mainNames[i]} sub={subNames[i]}");
            Debug.Log(actionLog.ToString());

            yield return _waitOne;
            if (!IsSpawned) yield break;

            if (IsMulti)
            {
                var itemRules = MultiCombatCapture.BuildItemRules(_itemManager);
                var mcr = MatchCompositionRoot.Instance;
                var roster = mcr?.Roster;
                if (roster == null || _deathService == null)
                {
                    Debug.LogError($"[TurnManager] Multi combat aborted: roster={roster != null} deathService={_deathService != null}");
                    yield break;
                }

                var capture = new MultiCombatCapture(_players, _modifiers, _tempsAtTurnStart,
                    _itemManager, mcr, _gameRule, ActiveEnvironment.Value, _ghostSkillService);
                var initialSnapshot = capture.BuildSnapshot(itemRules);
                if (TryGetCurrentMultiRoundEnd(out int disconnectedWinner))
                {
                    yield return StartCoroutine(HandleRoundEnd(disconnectedWinner));
                    yield break;
                }
                var operation = new MultiAttackOperation(capture, itemRules, initialSnapshot,
                    _combatResolver, _deathService, _buffSystem, GetDropTable());
                var roundEnd = default(RoundEndResult);
                if (!operation.TryApplyDefenses())
                {
                    Debug.LogError("[TurnManager] Multi defense preflight failed — stopping attack phase");
                    yield break;
                }

                while (operation.HasNext)
                {
                    if (IsDeathmatchGrantFaulted) yield break;
                    var step = operation.ApplyNext();
                    if (step.Failed)
                    {
                        Debug.LogError($"[TurnManager] Multi action preflight failed: seat={step.ActorSeat}, item={step.ItemId}");
                        yield break;
                    }
                    if (!step.Committed) continue;
                    // Commit, attributed deaths, score and winner latch stay synchronous.
                    if (step.WinnerMask != 0)
                    {
                        LatchMultiVictory(step.WinnerMask);
                        break;
                    }
                    if (!TryGrantDeathmatchItems()) yield break;
                    roundEnd = _deathService.EvaluateRoundEnd();
                    if (roundEnd.IsRoundOver) break;
                }

                var aggregate = operation.Finish();

                byte roundWinnerMask = roundEnd.IsRoundOver && !roundEnd.IsDraw
                    ? (byte)(1 << roundEnd.WinnerSeat)
                    : (byte)0;
                uint nextSeq = _resultSequence + 1;
                if (!CombatResolutionBatchNetData.TryFromResolution(
                    aggregate, initialSnapshot, roundWinnerMask, nextSeq, out var batchData))
                {
                    Debug.LogError($"[TurnManager] TryFromResolution failed (seq={nextSeq}) — aborting match");
                    yield break;
                }
                batchData = batchData.WithMatchWinnerMask(_multiTerminalWinnerMask);
                _resultSequence = nextSeq;

                if (_multiTerminalWinnerMask != 0)
                {
                    var outcome = CountBits(_multiTerminalWinnerMask) > 1
                        ? MultiMatchOutcome.JointVictory
                        : MultiMatchOutcome.SingleWinner;
                    mcr?.NetworkState?.ServerSetTerminalResult(
                        _resultSequence, outcome, _multiTerminalWinnerMask, released: false);
                }

                if (mcr != null)
                {
                    var expectedIds = GetPresentationViewers(mcr);
                    if (!_barrier.Begin(_resultSequence, expectedIds))
                    {
                        Debug.LogError($"[TurnManager] Multi presentation overlap rejected (seq={_resultSequence})");
                        yield break;
                    }
                }

                OnMultiCombatResultClientRpc(batchData);

                var schedule = MultiPresentationSchedule.Build(batchData,
                    id => _itemManager?.GetItemData(id)?.AnimDuration ?? 0f,
                    id => _itemManager?.GetItemData(id)?.Category == ItemCategory.Defense);
                float timeout = Mathf.Max(presentationTimeoutSeconds,
                    schedule.BarrierBudgetSeconds);
                yield return StartCoroutine(_barrier.WaitForCompletion(timeout));

                if (_barrier.State == BarrierState.TimedOut)
                    Debug.LogWarning($"[TurnManager] Barrier timed out (seq={_resultSequence}) — proceeding");

                string summaryMulti = TurnResultOperations.MultiSummary(TurnNumber.Value, _players,
                    _tempsAtTurnStart, roundEnd);
                PublishDebugLog(summaryMulti);

                if (_multiTerminalWinnerMask != 0)
                {
                    CompleteLatchedMultiMatch(_resultSequence);
                    yield break;
                }

                yield return _waitOne;
                if (!IsSpawned || IsDeathmatchGrantFaulted) yield break;

                yield return StartCoroutine(MultiResolutionPhaseRoutine(roundEnd));
            }
            else
            {
                var snapshot = _combatEngine.CapturePreCombatState(
                    _players[0], _players[1], _tempsAtTurnStart[0], _tempsAtTurnStart[1]);

                var result = _combatEngine.ResolveCombat(
                    _players[0], _players[1], _modifiers, _tempSystem, _buffSystem,
                    ActiveEnvironment.Value, snapshot, ref _resultSequence, GetDropTable());

                string summary = TurnResultOperations.DuelSummary(TurnNumber.Value, mainNames, subNames,
                    _players, _tempsAtTurnStart, result.WinnerIndex);
                PublishDebugLog(summary);

                var mcr = MatchCompositionRoot.Instance;
                if (mcr != null)
                {
                    var expectedIds = GetPresentationViewers(mcr);
                    if (!_barrier.Begin(result.ResultSequence, expectedIds))
                    {
                        Debug.LogError($"[TurnManager] 1v1 presentation overlap rejected (seq={result.ResultSequence})");
                        yield break;
                    }
                }

                var netData = result.ToNetData();
                int combatWinner = result.WinnerIndex;
                netData.EndsMatch = combatWinner >= 0 && _matchManager != null
                    && _matchManager.WouldEndMatch(combatWinner);
                PublishCombatResult(netData);

                yield return StartCoroutine(_barrier.WaitForCompletion(Mathf.Max(presentationTimeoutSeconds, 15f)));

                if (_barrier.State == BarrierState.TimedOut)
                    Debug.LogWarning($"[TurnManager] Barrier timed out (seq={result.ResultSequence}) — proceeding");

                yield return _waitOne;
                if (!IsSpawned) yield break;

                yield return StartCoroutine(ResolutionPhaseRoutine(result));
            }
        }

        // ─── Resolution Phase ────────────────────────────────

        IEnumerator ResolutionPhaseRoutine(CombatResult result)
        {
            if (!IsSpawned) yield break;
            CurrentPhase.Value = TurnPhase.ResolutionPhase;

            if (result.WinnerIndex >= 0)
            {
                yield return StartCoroutine(HandleRoundEnd(result.WinnerIndex));
                yield break;
            }

            TurnResultOperations.CompactInventories(_players);

            yield return _waitOne;
            if (!IsSpawned) yield break;

            if (TurnNumber.Value == 1 && ActiveEnvironment.Value == EnvironmentType.None)
            {
                yield return StartCoroutine(EnvironmentAnnouncementRoutine());
                if (!IsSpawned) yield break;
            }

            yield return StartCoroutine(PrepPhaseRoutine());
        }

        IEnumerator MultiResolutionPhaseRoutine(RoundEndResult roundEnd)
        {
            if (!IsSpawned || IsDeathmatchGrantFaulted) yield break;
            if (_multiTerminalWinnerMask != 0 || _multiRoundEndInProgress) yield break;
            // Re-read after the presentation wait: the roster may have changed.
            roundEnd = _deathService.EvaluateRoundEnd();
            CurrentPhase.Value = TurnPhase.ResolutionPhase;

            if (roundEnd.IsRoundOver)
            {
                int winner = roundEnd.IsDraw ? -1 : roundEnd.WinnerSeat;
                yield return StartCoroutine(HandleRoundEnd(winner));
                yield break;
            }

            TurnResultOperations.CompactInventories(_players);

            yield return _waitOne;
            if (!IsSpawned || IsDeathmatchGrantFaulted) yield break;

            if (TurnNumber.Value == 1 && ActiveEnvironment.Value == EnvironmentType.None)
            {
                yield return StartCoroutine(EnvironmentAnnouncementRoutine());
                if (!IsSpawned || IsDeathmatchGrantFaulted) yield break;
            }

            yield return StartCoroutine(PrepPhaseRoutine());
        }

        IEnumerator GhostKillDeathPresentation(byte deathMask, RoundEndResult roundEnd, uint castId)
        {
            yield return StartCoroutine(PresentMultiDeathsAndWait(
                deathMask, roundEnd.IsRoundOver, presentationSlotReserved: true,
                ghostCastId: castId));

            if (IsDeathmatchGrantFaulted) yield break;
            if (roundEnd.IsRoundOver)
                yield return StartCoroutine(HandleRoundEnd(roundEnd.IsDraw ? -1 : roundEnd.WinnerSeat));
        }

        IEnumerator GhostTerminalDeathPresentation(byte deathMask, uint castId)
        {
            if (deathMask != 0)
                yield return StartCoroutine(PresentMultiDeathsAndWait(
                    deathMask, true, presentationSlotReserved: true,
                    ghostCastId: castId));
            else
                _multiPresentationInFlight = false;
            CompleteLatchedMultiMatch(_resultSequence);
        }

        IEnumerator PresentMultiDeathsAndWait(byte deathMask, bool endsRound,
            bool presentationSlotReserved = false, uint ghostCastId = 0)
        {
            if (deathMask == 0) yield break;
            if (!presentationSlotReserved)
            {
                while (_multiPresentationInFlight)
                    yield return null;
                _multiPresentationInFlight = true;
            }

            try
            {
                while (_barrier.IsActive)
                    yield return null;
                if (IsDeathmatchGrantFaulted) yield break;

                _resultSequence++;
                var mcr = MatchCompositionRoot.Instance;
                if (_multiTerminalWinnerMask != 0)
                {
                    var outcome = CountBits(_multiTerminalWinnerMask) > 1
                        ? MultiMatchOutcome.JointVictory
                        : MultiMatchOutcome.SingleWinner;
                    mcr?.NetworkState?.ServerSetTerminalResult(
                        _resultSequence, outcome, _multiTerminalWinnerMask, released: false);
                }
                if (mcr != null)
                {
                    var expectedIds = GetPresentationViewers(mcr);
                    if (!_barrier.Begin(_resultSequence, expectedIds))
                    {
                        Debug.LogError($"[TurnManager] Death presentation overlap rejected (seq={_resultSequence})");
                        yield break;
                    }
                }

                PresentMultiDeathsRpc(deathMask, endsRound, _resultSequence, ghostCastId);

                yield return StartCoroutine(_barrier.WaitForCompletion(presentationTimeoutSeconds));

                if (_barrier.State == BarrierState.TimedOut)
                    Debug.LogWarning($"[TurnManager] Death barrier timed out (seq={_resultSequence})");
            }
            finally
            {
                _multiPresentationInFlight = false;
            }
        }

        // ─── Round End ───────────────────────────────────────

        IEnumerator HandleRoundEnd(int winnerIndex)
        {
            _prepInput.Close();
            if (!IsSpawned || IsDeathmatchGrantFaulted) yield break;
            if (IsMulti)
            {
                if (_multiTerminalWinnerMask != 0 || _multiRoundEndInProgress) yield break;
                _multiRoundEndInProgress = true;
                var currentEnd = _deathService.EvaluateRoundEnd();
                if (currentEnd.IsRoundOver)
                    winnerIndex = currentEnd.IsDraw ? -1 : currentEnd.WinnerSeat;
            }
            LastRoundWinner.Value = winnerIndex;

            bool endsMatch;
            if (IsMulti)
            {
                if (_matchManager != null
                    && !_matchManager.TryTransitionMatchState(
                        Network.MatchState.RoundInProgress, Network.MatchState.RoundEnd))
                {
                    Debug.LogWarning("[TurnManager] Multi HandleRoundEnd: RoundInProgress→RoundEnd transition failed");
                }
                endsMatch = _multiTerminalWinnerMask != 0;
            }
            else
            {
                endsMatch = winnerIndex >= 0 && _matchManager != null
                    && _matchManager.WouldEndMatch(winnerIndex);

                if (winnerIndex >= 0)
                {
                    for (int i = 0; i < _players.Length; i++)
                        if (i != winnerIndex)
                            PublishDeathSequence(i, endsMatch);
                }

                if (winnerIndex >= 0 && _matchManager != null)
                    _matchManager.EndRound(winnerIndex);
            }

            CurrentPhase.Value = TurnPhase.RoundOver;
            PublishPhaseChanged(TurnPhase.RoundOver, TurnNumber.Value);

            string winnerText = winnerIndex >= 0 ? $"P{winnerIndex + 1}" : "Draw";
            Debug.Log($"[TurnManager] Round over — Winner: {winnerText}");

            yield return _waitSix;
            if (!IsSpawned) yield break;

            bool matchComplete = IsMulti ? endsMatch :
                (_matchManager != null && _matchManager.IsMatchComplete());
            if (matchComplete)
            {
                if (IsMulti && _matchManager != null)
                {
                    var multiWinnerMask = _multiTerminalWinnerMask;
                    var multiOutcome = CountBits(multiWinnerMask) > 1
                        ? MultiMatchOutcome.JointVictory
                        : MultiMatchOutcome.SingleWinner;
                    if (!_matchManager.TryTransitionMatchState(
                        Network.MatchState.RoundEnd, Network.MatchState.MatchComplete))
                    {
                        Debug.LogWarning("[TurnManager] Multi MatchComplete transition failed — proceeding to next round");
                        yield return StartCoroutine(StartNextRound(winnerIndex < 0));
                        yield break;
                    }
                    OnMultiMatchEndClientRpc((byte)multiOutcome, multiWinnerMask);
                    Debug.Log($"[TurnManager] Multi match complete — outcome={multiOutcome}, winnerMask={multiWinnerMask:X2}");
                    yield break;
                }

                // Solo has one human connection and no opponent vote. MatchComplete
                // remains published until an explicit replay/exit owns cleanup.
                if (MatchCompositionRoot.Instance?.ActiveConfig?.Mode == Network.GameMode.Solo)
                    yield break;

                _matchManager.DisconnectedMask = _matchManager.BuildInitialDisconnectMask();

                if (_matchManager.DisconnectedMask != 0)
                {
                    Debug.Log("[TurnManager] Opponent disconnected during result screen — skipping rematch vote");
                    _matchManager.TryTransitionMatchState(
                        Network.MatchState.MatchComplete, Network.MatchState.RematchDeclined);
                    yield break;
                }

                if (!_matchManager.EnterRematchVote())
                {
                    Debug.Log("[TurnManager] EnterRematchVote failed — stopping");
                    yield break;
                }

                _rematchWaitHandle = StartCoroutine(WaitForRematchDecision());
                yield return _rematchWaitHandle;
                yield break;
            }

            yield return StartCoroutine(StartNextRound(winnerIndex < 0));
        }

        static int CountBits(byte mask)
        {
            int count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }
            return count;
        }

        void CompleteLatchedMultiMatch(uint decidingSequence)
        {
            _prepInput.Close();
            if (!IsServer || !IsMulti || _multiTerminalWinnerMask == 0) return;

            var networkState = MatchCompositionRoot.Instance?.NetworkState;
            if (networkState == null)
            {
                Debug.LogError("[TurnManager] Cannot release Multi terminal result: MatchNetworkState missing");
                return;
            }
            var existingTerminal = networkState.TerminalResult.Value;
            if (existingTerminal.IsValid && existingTerminal.Released)
                return;

            CurrentPhase.Value = TurnPhase.RoundOver;
            PublishPhaseChanged(TurnPhase.RoundOver, TurnNumber.Value);
            LastRoundWinner.Value = -1;

            if (_matchManager != null
                && _matchManager.CurrentMatchState.Value == Network.MatchState.RoundInProgress)
                _matchManager.TryTransitionMatchState(
                    Network.MatchState.RoundInProgress, Network.MatchState.RoundEnd);
            if (_matchManager != null
                && _matchManager.CurrentMatchState.Value == Network.MatchState.RoundEnd)
                _matchManager.TryTransitionMatchState(
                    Network.MatchState.RoundEnd, Network.MatchState.MatchComplete);

            var outcome = CountBits(_multiTerminalWinnerMask) > 1
                ? MultiMatchOutcome.JointVictory
                : MultiMatchOutcome.SingleWinner;
            networkState.ServerSetTerminalResult(
                decidingSequence, outcome, _multiTerminalWinnerMask, released: true);
            OnMultiMatchEndClientRpc((byte)outcome, _multiTerminalWinnerMask);
            Debug.Log($"[TurnManager] Multi match complete at action boundary — sequence={decidingSequence}, winnerMask={_multiTerminalWinnerMask:X2}");
        }

        [Rpc(SendTo.Everyone)]
        void OnMultiMatchEndClientRpc(byte outcome, byte winnerMask)
        {
            var o = (MultiMatchOutcome)outcome;
            Debug.Log($"[TurnManager] Multi match end — outcome: {o}, winnerMask: {winnerMask:X2}");
            OnMultiMatchOutcome?.Invoke(o, winnerMask);
        }

        [Rpc(SendTo.Server)]
        public void UseGhostSkillRpc(byte skillIndex, byte targetSeat, uint matchEpoch,
            uint roundEpoch, int turn, uint requestId, RpcParams rpcParams = default)
        {
            if (!IsServer) return;
            ulong senderId = rpcParams.Receive.SenderClientId;
            if (IsDeathmatchGrantInProgress)
            {
                ReplyGhostSkillRequest(senderId, matchEpoch, requestId, GhostSkillRequestResult.InputClosed);
                return;
            }
            var mcr = MatchCompositionRoot.Instance;
            var roster = mcr?.Roster;
            var nState = mcr?.NetworkState;
            if (!IsMulti || nState == null || roster == null || _ghostSkillService == null
                || _gameRule == null || !_gameRule.EnableGhostSystem)
            {
                ReplyGhostSkillRequest(senderId, matchEpoch, requestId, GhostSkillRequestResult.InvalidContext);
                return;
            }

            if (matchEpoch != nState.GhostMatchEpoch.Value
                || roundEpoch != nState.GhostRoundEpoch.Value
                || turn != TurnNumber.Value || requestId == 0)
            {
                ReplyGhostSkillRequest(senderId, matchEpoch, requestId, GhostSkillRequestResult.InvalidContext);
                return;
            }

            if (_ghostRequests.TryGet(senderId, requestId, out var previous))
            {
                ReplyGhostSkillRequest(senderId, matchEpoch, requestId, previous);
                return;
            }
            if (_ghostRequests.IsStale(senderId, requestId))
            {
                ReplyGhostSkillRequest(senderId, matchEpoch, requestId, GhostSkillRequestResult.StaleRequest);
                return;
            }

            GhostSkillRequestResult outcome;
            if (_multiTerminalWinnerMask != 0)
                outcome = GhostSkillRequestResult.MatchEnded;
            else if (!_ghostInputOpen || _ghostRoundEndTriggered || _multiRoundEndInProgress
                || CurrentPhase.Value != TurnPhase.PrepPhase)
                outcome = GhostSkillRequestResult.InputClosed;
            else if (!roster.TryGetSeatByClientId(senderId, out byte ghostSeat)
                || ghostSeat >= _players.Length || _players[ghostSeat] == null
                || !roster.IsConnected(ghostSeat)
                || _players[ghostSeat].CurrentLifeState.Value != LifeState.Ghost)
                outcome = GhostSkillRequestResult.InvalidActor;
            else if (targetSeat >= _players.Length || targetSeat == ghostSeat
                || !roster.IsConnected(targetSeat)
                || roster.GetLifeState(targetSeat) != LifeState.Alive)
                outcome = GhostSkillRequestResult.InvalidTarget;
            else
            {
                var ledger = _ghostSkillService.Ledger;
                bool success = false;
                uint castId = 0;
                if (skillIndex == GhostSkillService.SKILL_GRUDGE)
                {
                    if ((ledger.GhostDamageMask & (1 << targetSeat)) != 0)
                        outcome = GhostSkillRequestResult.TargetAlreadyAffected;
                    else if (!ledger.CanUseGrudge(ghostSeat, targetSeat))
                        outcome = GhostSkillRequestResult.Unavailable;
                    else
                    {
                        int[] scoresBefore = CaptureKillScores();
                        success = _ghostSkillService.TryUseGrudge(
                            ghostSeat, targetSeat, nState, _deathService, _players, roster);
                        outcome = success ? GhostSkillRequestResult.Accepted
                            : GhostSkillRequestResult.InvalidTarget;
                        if (success)
                        {
                            castId = ++_nextGhostCastId;
                            if (castId == 0) castId = ++_nextGhostCastId;
                            GhostSkillUsedClientRpc(ghostSeat, skillIndex, targetSeat, castId);
                            byte ghostKillMask = _deathService.ConsumeDeathMask();
                            byte matchWinners = FindNewMultiWinners(scoresBefore);
                            if (matchWinners != 0)
                            {
                                LatchMultiVictory(matchWinners);
                                _ghostInputOpen = false;
                                RecordAndReplyGhostRequest(senderId, matchEpoch, requestId, outcome);
                                _multiPresentationInFlight = true;
                                StartCoroutine(GhostTerminalDeathPresentation(ghostKillMask, castId));
                                return;
                            }

                            if (!TryGrantDeathmatchItems())
                            {
                                RecordAndReplyGhostRequest(senderId, matchEpoch, requestId, outcome);
                                return;
                            }
                            var roundEnd = _deathService.EvaluateRoundEnd();
                            if (roundEnd.IsRoundOver)
                            {
                                _ghostRoundEndTriggered = true;
                                _ghostInputOpen = false;
                            }
                            if (ghostKillMask != 0)
                            {
                                _multiPresentationInFlight = true;
                                StartCoroutine(GhostKillDeathPresentation(ghostKillMask, roundEnd, castId));
                            }
                            else if (roundEnd.IsRoundOver)
                                StartCoroutine(HandleRoundEnd(roundEnd.IsDraw ? -1 : roundEnd.WinnerSeat));
                        }
                    }
                }
                else if (skillIndex == GhostSkillService.SKILL_POSSESSION)
                {
                    if ((ledger.PossessedMask & (1 << targetSeat)) != 0)
                        outcome = GhostSkillRequestResult.TargetAlreadyAffected;
                    else if (!ledger.CanUsePossession(ghostSeat, targetSeat))
                        outcome = GhostSkillRequestResult.Unavailable;
                    else
                    {
                        success = _ghostSkillService.TryUsePossession(ghostSeat, targetSeat, nState, roster);
                        outcome = success ? GhostSkillRequestResult.Accepted
                            : GhostSkillRequestResult.InvalidTarget;
                        if (success)
                        {
                            castId = ++_nextGhostCastId;
                            if (castId == 0) castId = ++_nextGhostCastId;
                        }
                    }
                }
                else
                    outcome = GhostSkillRequestResult.Unavailable;

                if (success && skillIndex == GhostSkillService.SKILL_POSSESSION)
                    GhostSkillUsedClientRpc(ghostSeat, skillIndex, targetSeat, castId);
            }

            RecordAndReplyGhostRequest(senderId, matchEpoch, requestId, outcome);
        }

        void RecordAndReplyGhostRequest(ulong senderId, uint epoch, uint requestId,
            GhostSkillRequestResult result)
        {
            _ghostRequests.Record(senderId, requestId, result);
            ReplyGhostSkillRequest(senderId, epoch, requestId, result);
        }

        void ReplyGhostSkillRequest(ulong senderId, uint epoch, uint requestId,
            GhostSkillRequestResult result)
            => GhostSkillRequestResultClientRpc(senderId, epoch, requestId, (byte)result);

        [Rpc(SendTo.Everyone)]
        void GhostSkillRequestResultClientRpc(ulong recipient, uint epoch, uint requestId, byte result)
        {
            if (NetworkManager == null || NetworkManager.LocalClientId != recipient) return;
            var currentEpoch = MatchCompositionRoot.Instance?.NetworkState?.GhostMatchEpoch.Value ?? 0;
            if (currentEpoch != epoch) return;
            OnGhostSkillRequestResult?.Invoke(requestId, (GhostSkillRequestResult)result);
        }

        [Rpc(SendTo.Everyone)]
        void GhostSkillUsedClientRpc(byte ghostSeat, byte skillIndex, byte targetSeat, uint castId)
        {
            Debug.Log($"[Ghost] Skill used: Ghost P{ghostSeat} → P{targetSeat}, skill={skillIndex}");
            OnGhostSkillUsed?.Invoke(ghostSeat, skillIndex, targetSeat);
            OnGhostSkillUsedSequenced?.Invoke(ghostSeat, skillIndex, targetSeat, castId);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Synthetic joint-group fixture only; absent from release builds and not exposed by RPC.
        public bool DebugResolveJointDeathsForValidation()
        {
            if (!IsServer || !IsMulti || CurrentPhase.Value != TurnPhase.PrepPhase
                || _deathService == null || _players == null || _players.Length != 4
                || _players[2] == null || _players[3] == null
                || _players[2].CurrentLifeState.Value != LifeState.Alive
                || _players[3].CurrentLifeState.Value != LifeState.Alive
                || _multiTerminalWinnerMask != 0 || _multiPresentationInFlight
                || (_barrier?.IsActive ?? false)) return false;
            int[] before = CaptureKillScores();
            _players[2].Temperature.Value = TemperatureSystem.MIN_TEMP;
            _players[3].Temperature.Value = TemperatureSystem.MIN_TEMP;
            _deathService.TryKill(2, DamageSource.Create(0, DamageOrigin.Item));
            _deathService.TryKill(3, DamageSource.Create(1, DamageOrigin.Item));
            _deathService.FlushDeathQueue();
            byte mask = FindNewMultiWinners(before);
            if (mask == 0) return false;
            LatchMultiVictory(mask);
            _ghostInputOpen = false;
            _multiPresentationInFlight = true;
            StartCoroutine(GhostTerminalDeathPresentation(_deathService.ConsumeDeathMask(), 0));
            return true;
        }

        public bool DebugForceTwoGhostsForTopUp(byte firstSeat, byte secondSeat)
        {
            if (!IsServer || !IsMulti || firstSeat == secondSeat || _deathService == null
                || _players == null || firstSeat >= _players.Length || secondSeat >= _players.Length
                || _players[firstSeat] == null || _players[secondSeat] == null
                || _players[firstSeat].CurrentLifeState.Value != LifeState.Alive
                || _players[secondSeat].CurrentLifeState.Value != LifeState.Alive
                || _multiPresentationInFlight || (_barrier?.IsActive ?? false))
                return false;

            _players[firstSeat].Temperature.Value = TemperatureSystem.MIN_TEMP;
            _players[secondSeat].Temperature.Value = TemperatureSystem.MIN_TEMP;
            if (!_deathService.TryKill(firstSeat, DamageSource.None)
                || !_deathService.TryKill(secondSeat, DamageSource.None))
                return false;
            _deathService.FlushDeathQueue();
            byte deathMask = _deathService.ConsumeDeathMask();
            TryGrantDeathmatchItems();
            var roundEnd = _deathService.EvaluateRoundEnd();
            if (roundEnd.IsRoundOver) _ghostRoundEndTriggered = true;
            _multiPresentationInFlight = true;
            StartCoroutine(GhostKillDeathPresentation(deathMask, roundEnd, 0));
            Debug.Log($"[MATRIX] FORCE_TWO_GHOSTS mask={deathMask:X2}");
            return true;
        }

        public bool DebugForceGhostForVisual(byte seat)
        {
            if (!IsServer || !IsMulti || _deathService == null || _players == null
                || seat >= _players.Length || _players[seat] == null
                || _players[seat].CurrentLifeState.Value != LifeState.Alive
                || _multiPresentationInFlight || (_barrier?.IsActive ?? false))
                return false;

            _players[seat].Temperature.Value = TemperatureSystem.MIN_TEMP;
            if (!_deathService.TryKill(seat, DamageSource.None)) return false;
            _deathService.FlushDeathQueue();
            byte deathMask = _deathService.ConsumeDeathMask();
            TryGrantDeathmatchItems();
            var roundEnd = _deathService.EvaluateRoundEnd();
            if (roundEnd.IsRoundOver) _ghostRoundEndTriggered = true;
            _multiPresentationInFlight = true;
            StartCoroutine(GhostKillDeathPresentation(deathMask, roundEnd, 0));
            Debug.Log($"[VISUAL] FORCE_GHOST seat={seat} deathMask={deathMask:X2}");
            return true;
        }
#endif

        IEnumerator WaitForRematchDecision()
        {
            try
            {
                while (true)
                {
                    if (_matchManager.DisconnectedMask != 0 || _matchManager.AnyExplicitDecline())
                    {
                        _matchManager.ForceDecline();
                        yield break;
                    }

                    if (_matchManager.RematchCommitted)
                    {
                        _matchManager.CommitRematch(_matchManager.RematchVoteEpoch.Value);
                        BootstrapNewMatch(clearTerminalResult: true);
                        _matchManager.StartRound();
                        PublishReviveVisuals();
                        Debug.Log("[TurnManager] Rematch accepted — starting new match");
                        yield return StartCoroutine(PrepPhaseRoutine());
                        yield break;
                    }

                    if (NetworkManager.ServerTime.Time >= _matchManager.RematchDeadlineServerTime.Value)
                    {
                        _matchManager.ForceDecline();
                        Debug.Log("[TurnManager] Rematch vote timed out");
                        yield break;
                    }

                    yield return null;
                }
            }
            finally
            {
                _rematchWaitHandle = null;
            }
        }

        void BootstrapNewMatch(bool clearTerminalResult)
        {
            _ghostInputOpen = false;
            if (clearTerminalResult)
            {
                _ghostRequests.Clear();
            }
            _multiRoundEndInProgress = false;
            _buffSystem.ClearAll();
            ActiveEnvironment.Value = EnvironmentType.None;
            TurnNumber.Value = 0;
            LastRoundWinner.Value = -1;
            _emoteWindowClosed = false;
            _ghostRoundEndTriggered = false;
            _deathmatchGrantCoordinator?.BeginRound();
            if (clearTerminalResult)
                _multiTerminalWinnerMask = 0;
            _multiPresentationInFlight = false;

            if (IsMulti)
            {
                var nState = MatchCompositionRoot.Instance?.NetworkState;
                if (_ghostSkillService != null && nState != null)
                {
                    if (clearTerminalResult)
                    {
                        nState.ServerBeginGhostMatch();
                        _ghostSkillService.BeginMatch(nState, _players.Length);
                    }
                    else
                    {
                        nState.ServerBeginGhostRound();
                        _ghostSkillService.BeginRound(nState);
                    }
                }
                else
                    nState?.ServerClearAllCooldowns();
                if (clearTerminalResult)
                    nState?.ServerClearTerminalResult();

                _roundLifecycle.ResetPlayersForNewRound(_players);
                int startItems = _gameRule?.InitialRandomItems ?? 4;
                int maxRandom = _gameRule?.MaxRandomItems ?? int.MaxValue;
                _roundLifecycle.GrantStartingItems(_players, GetDropTable(), startItems, maxRandom);

                for (int i = 0; i < _modifiers.Length; i++)
                    _modifiers[i].Reset();
                for (int i = 0; i < _players.Length; i++)
                    if (_players[i] != null)
                        _players[i].ResetForNewTurn();
            }
            else
            {
                _roundLifecycle.ResetPlayersForNewRound(_players[0], _players[1]);
                _roundLifecycle.GrantStartingItems(_players[0], _players[1], GetDropTable());

                _modifiers[0].Reset();
                _modifiers[1].Reset();
                _players[0].ResetForNewTurn();
                _players[1].ResetForNewTurn();
            }
        }

        bool TryGrantDeathmatchItems()
        {
            if (!IsMulti || _gameRule == null || _multiTerminalWinnerMask != 0) return true;
            var root = MatchCompositionRoot.Instance;
            var outcome = _deathmatchGrantCoordinator?.TryGrant(root?.Roster, _players,
                root?.ItemManager, _gameRule, root?.NetworkState, terminal: false)
                ?? DeathmatchGrantOutcome.Faulted;
            if (outcome == DeathmatchGrantOutcome.Faulted)
            {
                _ghostInputOpen = false;
                return false;
            }
            if (outcome == DeathmatchGrantOutcome.Granted)
                Debug.Log($"[TurnManager] Deathmatch top-up: 2 alive → fill random slots to {_gameRule.MaxRandomItems}");
            return true;
        }

        IEnumerator StartNextRound(bool isDraw = false)
        {
            if (IsDeathmatchGrantFaulted) yield break;
            if (_multiTerminalWinnerMask != 0)
            {
                Debug.LogError("[TurnManager] Next round rejected because a Multi terminal winner is latched");
                yield break;
            }
            _multiRoundEndInProgress = false;
            BootstrapNewMatch(clearTerminalResult: false);

            if (_matchManager != null && (!isDraw || IsMulti))
                _matchManager.StartRound();

            PublishReviveVisuals();

            Debug.Log(isDraw
                ? "[TurnManager] Draw — round voided, replaying"
                : "[TurnManager] New round started — temperatures reset");

            yield return StartCoroutine(PrepPhaseRoutine());
        }

        // ─── Environment ─────────────────────────────────────

        IEnumerator EnvironmentAnnouncementRoutine()
        {
            ActiveEnvironment.Value = _envRules.SelectRandom();

            Debug.Log($"[ENV] Environment selected: {ActiveEnvironment.Value} ({EnvironmentRuleService.GetName(ActiveEnvironment.Value)})");

            PublishEnvironment(ActiveEnvironment.Value);

            yield return _waitFour;
        }

        // ─── Client RPCs ─────────────────────────────────────

        [Rpc(SendTo.Everyone)]
        void AnnounceEnvironmentClientRpc(EnvironmentType env)
        {
            OnEnvironmentAnnounced?.Invoke(env);
            StartCoroutine(EnvironmentCameraRoutine());
        }

        IEnumerator EnvironmentCameraRoutine()
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            Vector3 startEuler = cam.transform.eulerAngles;
            float startY = startEuler.y;
            float targetY = startY - 25f;

            const float panDuration = 0.6f;
            float t = 0f;
            while (t < panDuration)
            {
                t += Time.deltaTime;
                float ratio = Mathf.SmoothStep(0f, 1f, t / panDuration);
                cam.transform.eulerAngles = new Vector3(startEuler.x, Mathf.Lerp(startY, targetY, ratio), startEuler.z);
                yield return null;
            }
            cam.transform.eulerAngles = new Vector3(startEuler.x, targetY, startEuler.z);

            yield return _waitTwo;

            t = 0f;
            while (t < panDuration)
            {
                t += Time.deltaTime;
                float ratio = Mathf.SmoothStep(0f, 1f, t / panDuration);
                cam.transform.eulerAngles = new Vector3(startEuler.x, Mathf.Lerp(targetY, startY, ratio), startEuler.z);
                yield return null;
            }
            cam.transform.eulerAngles = startEuler;
        }

        [Rpc(SendTo.Everyone)]
        void ReviveVisualsClientRpc()
        {
            var mcr = MatchCompositionRoot.Instance;
            if (mcr == null) return;
            foreach (var p in mcr.Registry.Players)
            {
                var visual = p.State.GetComponent<AZPlayerVisual>();
                if (visual != null) visual.ReviveVisual();
            }
        }

        [Rpc(SendTo.Everyone)]
        void OnPhaseChangedClientRpc(TurnPhase phase, int turnNumber)
        {
        }

        [Rpc(SendTo.Everyone)]
        public void OnItemUsedClientRpc(byte playerIndex, byte slotIndex, byte category, bool isInstant)
        {
        }

        public event System.Action<byte, short> OnOpponentRevealed;

        [Rpc(SendTo.Everyone)]
        public void RevealOpponentItemClientRpc(byte forPlayerIndex, short opponentItemId)
        {
            OnOpponentRevealed?.Invoke(forPlayerIndex, opponentItemId);
        }

        [Rpc(SendTo.Everyone)]
        void TriggerDeathSequenceRpc(int loserIndex, bool endsMatch)
        {
            var registry = MatchCompositionRoot.Instance?.Registry;
            if (registry == null || loserIndex < 0 || loserIndex >= 4
                || !registry.TryGetByPlayerIndex((byte)loserIndex, out var binding)
                || !binding.IsValid) return;
            binding.State.GetComponent<AZPlayerVisual>()?.PlayDeathSequence(endsMatch);
        }

        [Rpc(SendTo.Everyone)]
        void CombatDebugLogRpc(string message)
        {
            Debug.Log($"[COMBAT-SYNC] {message}");
        }

        [Rpc(SendTo.Everyone)]
        void OnCombatResultClientRpc(CombatResultData resultData)
        {
            OnCombatResult?.Invoke(resultData);
        }

        public static event System.Action<CombatResolutionBatchNetData> OnMultiCombatResult;

        [Rpc(SendTo.Everyone)]
        void OnMultiCombatResultClientRpc(CombatResolutionBatchNetData batchData)
        {
            OnMultiCombatResult?.Invoke(batchData);
        }

        public static event System.Action<byte, bool, uint, uint> OnMultiDeathPresentation;
        public event System.Action<Match.MultiMatchOutcome, byte> OnMultiMatchOutcome;
        public static event System.Action<byte, byte, byte> OnGhostSkillUsed;
        public static event System.Action<byte, byte, byte, uint> OnGhostSkillUsedSequenced;

        [Rpc(SendTo.Everyone)]
        void PresentMultiDeathsRpc(byte deathMask, bool endsRound, uint presentationId, uint ghostCastId)
        {
            OnMultiDeathPresentation?.Invoke(deathMask, endsRound, presentationId, ghostCastId);
        }

        [Rpc(SendTo.Everyone)]
        void KidsStealStagingClientRpc()
        {
            var vfx = EnvironmentVFXManager.Instance;
            if (vfx != null) vfx.PlayKidsStealStaging();
        }

        [Rpc(SendTo.Everyone)]
        void AmbulanceBlanketStagingClientRpc(bool p1IsLower)
        {
            var vfx = EnvironmentVFXManager.Instance;
            if (vfx == null) return;

            byte localSeat = 0;
            var mcr = MatchCompositionRoot.Instance;
            if (mcr != null)
            {
                var localId = NetworkManager.Singleton.LocalClientId;
                foreach (var p in mcr.Registry.Players)
                {
                    if (p.Identity.ClientId == localId)
                    {
                        localSeat = p.Identity.PlayerIndex;
                        break;
                    }
                }
            }
            bool isLocalP1 = localSeat == 0;
            bool healSelf = (p1IsLower && isLocalP1) || (!p1IsLower && !isLocalP1);
            vfx.PlayAmbulanceBlanketStaging(healSelf);
        }

        [Rpc(SendTo.Everyone)]
        void AmbulanceBlanketStagingMultiClientRpc(int healSeat)
        {
            var vfx = EnvironmentVFXManager.Instance;
            if (vfx == null) return;

            int localSeat = -1;
            var mcr = MatchCompositionRoot.Instance;
            if (mcr != null)
            {
                var localId = NetworkManager.Singleton.LocalClientId;
                foreach (var p in mcr.Registry.Players)
                {
                    if (p.Identity.ClientId == localId)
                    {
                        localSeat = p.Identity.PlayerIndex;
                        break;
                    }
                }
            }
            if (localSeat < 0) return;
            bool healSelf = localSeat == healSeat;
            vfx.PlayAmbulanceBlanketStaging(healSelf);
        }
    }
}
