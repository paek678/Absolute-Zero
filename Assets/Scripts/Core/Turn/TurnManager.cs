using System.Collections;
using System.Collections.Generic;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Item.Data;
using AbsoluteZero.Core.Match;
using AbsoluteZero.Core.Player;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Turn
{
    public class TurnManager : NetworkBehaviour
    {
        public static TurnManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] float prepDuration = 20f;

        static bool _debugPaused;
        public static bool DebugPaused => _debugPaused;

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

        public static event System.Action<CombatResultData> OnCombatResult;
        public static event System.Action<EnvironmentType> OnEnvironmentAnnounced;

        PlayerState _p1;
        PlayerState _p2;
        PlayerModifiers[] _modifiers = new PlayerModifiers[2];
        TemperatureSystem _tempSystem;
        CombatResolver _combatResolver;
        BuffDebuffSystem _buffSystem;
        ItemManager _itemManager;
        MatchManager _matchManager;

        float _p1TempAtTurnStart;
        float _p2TempAtTurnStart;

        static readonly WaitForSeconds _waitHalf = new(0.5f);
        static readonly WaitForSeconds _waitOne = new(1f);
        static readonly WaitForSeconds _waitTwo = new(2f);
        static readonly WaitForSeconds _waitThree = new(3f);
        static readonly WaitForSeconds _waitFour = new(4f);
        static readonly WaitForSeconds _waitFive = new(5f);
        static readonly WaitForSeconds _waitKidsSteal = new(EnvironmentVFXManager.STEAL_STAGING_DURATION);
        static readonly WaitForSeconds _waitAmbulanceBlanket = new(EnvironmentVFXManager.BLANKET_STAGING_DURATION);

        public PlayerState GetPlayer(int index) => index == 0 ? _p1 : _p2;
        public PlayerModifiers[] GetModifiers() => _modifiers;
        public TemperatureSystem GetTempSystem() => _tempSystem;
        public BuffDebuffSystem GetBuffSystem() => _buffSystem;
        public ItemDropTable GetDropTable() => _itemManager != null ? _itemManager.GetDropTable() : null;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Instance = this;

            if (IsServer)
            {
                _tempSystem = new TemperatureSystem();
                _combatResolver = new CombatResolver();
                _buffSystem = new BuffDebuffSystem();

                _itemManager = GetComponentInParent<NetworkObject>()?.GetComponentInChildren<ItemManager>();
                _matchManager = GetComponentInParent<NetworkObject>()?.GetComponentInChildren<MatchManager>();

                if (_itemManager == null)
                    _itemManager = FindAnyObjectByType<ItemManager>();
                if (_matchManager == null)
                    _matchManager = FindAnyObjectByType<MatchManager>();

                StartCoroutine(WaitForPlayersRoutine());
            }
        }

        public override void OnNetworkDespawn()
        {
            StopAllCoroutines();
            OnCombatResult = null;
            OnEnvironmentAnnounced = null;
            if (Instance == this) Instance = null;
            _debugPaused = false;
            Time.timeScale = 1f;
            base.OnNetworkDespawn();
        }

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.f5Key.wasPressedThisFrame)
            {
                if (IsServer)
                {
                    SetDebugPause(!_debugPaused);
                    SyncDebugPauseClientRpc(_debugPaused);
                }
                else
                {
                    RequestDebugPauseServerRpc();
                }
            }
        }

        [Rpc(SendTo.Server)]
        void RequestDebugPauseServerRpc(RpcParams rpcParams = default)
        {
            SetDebugPause(!_debugPaused);
            SyncDebugPauseClientRpc(_debugPaused);
        }

        [Rpc(SendTo.NotServer)]
        void SyncDebugPauseClientRpc(bool paused)
        {
            SetDebugPause(paused);
        }

        void SetDebugPause(bool paused)
        {
            _debugPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            Debug.Log($"[DEBUG] Game {(paused ? "PAUSED" : "RESUMED")} (timeScale={Time.timeScale})");
        }

        IEnumerator WaitForPlayersRoutine()
        {
            CurrentPhase.Value = TurnPhase.WaitingForPlayers;

            while (true)
            {
                var players = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
                if (players.Length >= 2)
                {
                    if (players[0].OwnerClientId <= players[1].OwnerClientId)
                    {
                        _p1 = players[0];
                        _p2 = players[1];
                    }
                    else
                    {
                        _p1 = players[1];
                        _p2 = players[0];
                    }
                    break;
                }
                yield return _waitHalf;
            }

            _p1.Initialize(0, _p1.GetComponent<PlayerInventory>());
            _p2.Initialize(1, _p2.GetComponent<PlayerInventory>());

            if (_itemManager != null)
            {
                _itemManager.InitializePlayerInventory(_p1.GetInventory());
                _itemManager.InitializePlayerInventory(_p2.GetInventory());
            }

            if (_matchManager != null)
                _matchManager.StartRound();

            Debug.Log($"[TurnManager] Players found: P1={_p1.OwnerClientId}, P2={_p2.OwnerClientId}");

            yield return StartCoroutine(PrepPhaseRoutine());
        }

        // ─── 도발 이모티콘 윈도우 (공격 시작 지연용) ────────────
        const float EMOTE_DISPLAY_SEC = 1.0f;
        bool _emoteWindowClosed;
        /// <summary>서버: 지금 도발 이모티콘을 받아줄 수 있는가 (프렙 진행 중 & 공격 시작 지연 전).</summary>
        public bool AcceptEmotes => IsSpawned && CurrentPhase.Value == TurnPhase.PrepPhase && !_emoteWindowClosed;

        IEnumerator PrepPhaseRoutine()
        {
            if (!IsSpawned) yield break;
            TurnNumber.Value++;
            LastRoundWinner.Value = -1;

            _p1.ResetForNewTurn();
            _p2.ResetForNewTurn();
            _modifiers[0].Reset();
            _modifiers[1].Reset();

            _p1.IsReady.Value = false;
            _p2.IsReady.Value = false;
            _p1.Temperature.Value = Mathf.Clamp(_p1.Temperature.Value, 0f, TemperatureSystem.MAX_TEMP);
            _p2.Temperature.Value = Mathf.Clamp(_p2.Temperature.Value, 0f, TemperatureSystem.MAX_TEMP);

            _p1.IsFanActive.Value = true;
            _p2.IsFanActive.Value = true;

            if (ActiveEnvironment.Value != EnvironmentType.None)
            {
                Debug.Log($"[ENV] ===== Turn {TurnNumber.Value} — active: {ActiveEnvironment.Value} ({GetEnvironmentName(ActiveEnvironment.Value)}) =====");
                if (ActiveEnvironment.Value == EnvironmentType.SunnyDay)
                    Debug.Log("[ENV] SunnyDay: recovery rate 1 → 2°/sec (fan-off recovery doubled)");
                else if (ActiveEnvironment.Value == EnvironmentType.CoolBreeze)
                    Debug.Log("[ENV] CoolBreeze: recovery rate 1 → 0°/sec (no fan-off recovery)");
                else if (ActiveEnvironment.Value == EnvironmentType.CicadaSong)
                    Debug.Log("[ENV] CicadaSong: audio/visual distraction (no gameplay effect yet)");
                else if (ActiveEnvironment.Value == EnvironmentType.HeatWaveWarning)
                    Debug.Log("[ENV] HeatWave: lower-temp player acts first this turn");
            }

            float currentPrepDuration = prepDuration;
            if (ActiveEnvironment.Value == EnvironmentType.SummerVacation)
            {
                currentPrepDuration = 10f;
                Debug.Log($"[ENV] SummerVacation: prep duration {prepDuration}s → {currentPrepDuration}s");
            }

            if (ActiveEnvironment.Value == EnvironmentType.Kids && TurnNumber.Value == 3)
            {
                Debug.Log("[ENV] Kids: steal staging + removing 1 random item from each player");
                KidsStealStagingClientRpc();
                yield return _waitKidsSteal;
                RemoveRandomUnusedItem(_p1.GetInventory());
                RemoveRandomUnusedItem(_p2.GetInventory());
            }

            if (ActiveEnvironment.Value == EnvironmentType.Ambulance && TurnNumber.Value == 4)
            {
                Debug.Log($"[ENV] Ambulance: Turn 4 triggered — P0={_p1.Temperature.Value:F1}° P1={_p2.Temperature.Value:F1}°");
                bool p1Lower = _p1.Temperature.Value < _p2.Temperature.Value;
                bool p2Lower = _p2.Temperature.Value < _p1.Temperature.Value;
                if (p1Lower || p2Lower)
                {
                    AmbulanceBlanketStagingClientRpc(p1Lower);
                    yield return _waitAmbulanceBlanket;
                }

                if (p1Lower)
                {
                    _tempSystem.ApplyHeal(_p1, 10f);
                    Debug.Log($"[ENV] Ambulance: P0 healed +10° → {_p1.Temperature.Value:F1}° (lower temp)");
                }
                else if (p2Lower)
                {
                    _tempSystem.ApplyHeal(_p2, 10f);
                    Debug.Log($"[ENV] Ambulance: P1 healed +10° → {_p2.Temperature.Value:F1}° (lower temp)");
                }
                else
                {
                    Debug.Log("[ENV] Ambulance: same temp — no heal applied");
                }
            }

            PrepStartServerTime.Value = NetworkManager.ServerTime.Time;
            PrepDuration.Value = currentPrepDuration;

            CurrentPhase.Value = TurnPhase.PrepPhase;
            OnPhaseChangedClientRpc(TurnPhase.PrepPhase, TurnNumber.Value);
            _emoteWindowClosed = false;   // 새 프렙 시작 → 도발 허용

            _p1TempAtTurnStart = _p1.Temperature.Value;
            _p2TempAtTurnStart = _p2.Temperature.Value;

            _tempSystem.ResetTimer();
            float elapsed = 0f;

            RemainingTime.Value = Mathf.CeilToInt(currentPrepDuration);

            while (elapsed < currentPrepDuration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                _tempSystem.Accumulate(dt);

                int newRemaining = Mathf.CeilToInt(currentPrepDuration - elapsed);
                if (newRemaining != RemainingTime.Value)
                    RemainingTime.Value = Mathf.Max(0, newRemaining);

                float recoveryRate = TemperatureSystem.DEFAULT_RECOVERY_RATE;
                if (ActiveEnvironment.Value == EnvironmentType.SunnyDay)
                    recoveryRate = 2f;
                else if (ActiveEnvironment.Value == EnvironmentType.CoolBreeze)
                    recoveryRate = 0f;

                while (_tempSystem.ConsumeTick())
                {
                    _tempSystem.ApplyFanTick(_p1);
                    _tempSystem.ApplyFanTick(_p2);
                    _tempSystem.ApplyRecoveryTick(_p1, recoveryRate);
                    _tempSystem.ApplyRecoveryTick(_p2, recoveryRate);

                    var dropTable = GetDropTable();
                    _tempSystem.CheckThresholds(_p1, _p1.GetInventory(), _p1.GetInventory().GetThresholdGranted(), dropTable);
                    _tempSystem.CheckThresholds(_p2, _p2.GetInventory(), _p2.GetInventory().GetThresholdGranted(), dropTable);
                }

                if (_tempSystem.IsDead(_p1) || _tempSystem.IsDead(_p2))
                {
                    yield return StartCoroutine(HandleRoundEnd(DetermineDeathWinner()));
                    yield break;
                }

                if (_p1.IsReady.Value && _p2.IsReady.Value)
                    break;

                yield return null;
            }

            RemainingTime.Value = 0;

            if (!_p1.IsReady.Value) ForceReady(_p1);
            if (!_p2.IsReady.Value) ForceReady(_p2);

            RevertFanUpgrade(_p1);
            RevertFanUpgrade(_p2);

            // 재생 중인 도발이 끝난 뒤 공격 시작 — 도발 창 닫고 남은 표시 시간만큼 대기
            _emoteWindowClosed = true;
            double lastEmote = System.Math.Max(_p1.LastEmoteServerTime, _p2.LastEmoteServerTime);
            float emoteRemain = (float)(EMOTE_DISPLAY_SEC - (NetworkManager.ServerTime.Time - lastEmote));
            for (float t = 0f; t < emoteRemain; t += Time.deltaTime)
                yield return null;

            yield return StartCoroutine(AttackPhaseRoutine());
        }

        int DetermineDeathWinner()
        {
            bool p1Dead = _tempSystem.IsDead(_p1);
            bool p2Dead = _tempSystem.IsDead(_p2);
            if (p1Dead && p2Dead) return -1;
            if (p1Dead) return 1;
            return 0;
        }

        void ForceReady(PlayerState player)
        {
            player.IsReady.Value = true;
            player.IsFanActive.Value = false;
            player.GetActionQueue().SetReady(Time.time);
        }

        void RevertFanUpgrade(PlayerState player)
        {
            if (!player.IsFanUpgraded.Value) return;
            Debug.Log($"[COMBAT] FanSpeed revert: P{player.PlayerIndex} {player.FanSpeed.Value} → {TemperatureSystem.DEFAULT_FAN_SPEED}");
            player.FanSpeed.Value = TemperatureSystem.DEFAULT_FAN_SPEED;
            player.IsFanUpgraded.Value = false;
        }

        IEnumerator AttackPhaseRoutine()
        {
            if (!IsSpawned) yield break;
            CurrentPhase.Value = TurnPhase.AttackPhase;
            OnPhaseChangedClientRpc(TurnPhase.AttackPhase, TurnNumber.Value);

            Debug.Log($"[COMBAT] ========== TURN {TurnNumber.Value} ATTACK PHASE START ==========");
            Debug.Log($"[COMBAT] P0 temp={_p1.Temperature.Value:F1}° | P1 temp={_p2.Temperature.Value:F1}°");

            _p1.IsBasicBlocked.Value = false;
            _p2.IsBasicBlocked.Value = false;
            // 영구 아이템 잠금은 이번 턴 resolve에서 다시 세팅됨 (다음 턴 프렙에 적용)
            _p1.IsPermanentLocked.Value = false;
            _p2.IsPermanentLocked.Value = false;

            Debug.Log($"[COMBAT] --- Processing delayed buffs/debuffs ---");
            _buffSystem.ProcessTurnStart(_p1, _p2);
            Debug.Log($"[COMBAT] After buffs: P0={_p1.Temperature.Value:F1}° | P1={_p2.Temperature.Value:F1}°");

            var q1 = _p1.GetActionQueue();
            var q2 = _p2.GetActionQueue();

            string p1MainName = q1.selectedAction.HasValue ? q1.selectedAction.Value.ItemData.ItemName : "NONE";
            string p2MainName = q2.selectedAction.HasValue ? q2.selectedAction.Value.ItemData.ItemName : "NONE";
            string p1SubName = q1.subAction.HasValue ? q1.subAction.Value.ItemData.ItemName : "NONE";
            string p2SubName = q2.subAction.HasValue ? q2.subAction.Value.ItemData.ItemName : "NONE";
            Debug.Log($"[COMBAT] P0: main={p1MainName}, sub={p1SubName} | P1: main={p2MainName}, sub={p2SubName}");

            short p1SubId = q1.subAction.HasValue
                ? _p1.GetInventory().SlotStates[q1.subAction.Value.SlotIndex].ItemId
                : (short)-1;
            short p2SubId = q2.subAction.HasValue
                ? _p2.GetInventory().SlotStates[q2.subAction.Value.SlotIndex].ItemId
                : (short)-1;
            short p1MainId = q1.selectedAction.HasValue
                ? _p1.GetInventory().SlotStates[q1.selectedAction.Value.SlotIndex].ItemId
                : (short)-1;
            short p2MainId = q2.selectedAction.HasValue
                ? _p2.GetInventory().SlotStates[q2.selectedAction.Value.SlotIndex].ItemId
                : (short)-1;

            float p1TempBeforeCombat = _p1.Temperature.Value;
            float p2TempBeforeCombat = _p2.Temperature.Value;

            yield return _waitOne;
            if (!IsSpawned) yield break;

            Debug.Log($"[COMBAT] --- Resolving main combat ---");
            var result = _combatResolver.Resolve(q1, q2, _modifiers, _p1, _p2, _tempSystem, _buffSystem, ActiveEnvironment.Value);

            result.P1TempAtTurnStart = _p1TempAtTurnStart;
            result.P2TempAtTurnStart = _p2TempAtTurnStart;
            result.P1TempBeforeCombat = p1TempBeforeCombat;
            result.P2TempBeforeCombat = p2TempBeforeCombat;
            result.P1TempAfterCombat = _p1.Temperature.Value;
            result.P2TempAfterCombat = _p2.Temperature.Value;
            result.P1SubItemId = p1SubId;
            result.P2SubItemId = p2SubId;
            result.P1MainItemId = p1MainId;
            result.P2MainItemId = p2MainId;

            string winText = result.WinnerIndex >= 0 ? $"P{result.WinnerIndex} WINS" : "no death";
            Debug.Log($"[COMBAT] ===== COMBAT RESULT: {winText} =====");
            Debug.Log($"[COMBAT] Final temps: P0={_p1.Temperature.Value:F1}° | P1={_p2.Temperature.Value:F1}°");

            string summary = $"Turn{TurnNumber.Value} | P0: {p1MainName}(sub:{p1SubName}) P1: {p2MainName}(sub:{p2SubName}) | P0: {_p1TempAtTurnStart:F1}→{_p1.Temperature.Value:F1}° P1: {_p2TempAtTurnStart:F1}→{_p2.Temperature.Value:F1}° | {winText}";
            CombatDebugLogRpc(summary);

            OnCombatResultClientRpc(result.ToNetData());

            yield return _waitOne;
            float vfxTimeout = 10f;
            while (CombatVFXManager.Instance != null && CombatVFXManager.Instance.IsPlaying && vfxTimeout > 0f)
            {
                vfxTimeout -= Time.deltaTime;
                yield return null;
            }
            yield return _waitOne;
            if (!IsSpawned) yield break;

            yield return StartCoroutine(ResolutionPhaseRoutine(result));
        }

        IEnumerator ResolutionPhaseRoutine(CombatResult result)
        {
            if (!IsSpawned) yield break;
            CurrentPhase.Value = TurnPhase.ResolutionPhase;

            if (result.WinnerIndex >= 0)
            {
                yield return StartCoroutine(HandleRoundEnd(result.WinnerIndex));
                yield break;
            }

            var inv1 = _p1.GetInventory();
            var inv2 = _p2.GetInventory();

            inv1.CompactSlots();
            inv2.CompactSlots();

            yield return _waitOne;
            if (!IsSpawned) yield break;

            if (TurnNumber.Value == 1 && ActiveEnvironment.Value == EnvironmentType.None)
            {
                yield return StartCoroutine(EnvironmentAnnouncementRoutine());
                if (!IsSpawned) yield break;
            }

            yield return StartCoroutine(PrepPhaseRoutine());
        }

        IEnumerator HandleRoundEnd(int winnerIndex)
        {
            if (!IsSpawned) yield break;
            LastRoundWinner.Value = winnerIndex;

            if (winnerIndex >= 0 && _matchManager != null)
                _matchManager.EndRound(winnerIndex);

            CurrentPhase.Value = TurnPhase.RoundOver;
            OnPhaseChangedClientRpc(TurnPhase.RoundOver, TurnNumber.Value);

            string winnerText = winnerIndex >= 0 ? $"P{winnerIndex + 1}" : "Draw";
            Debug.Log($"[TurnManager] Round over — Winner: {winnerText}");

            yield return _waitThree;
            if (!IsSpawned) yield break;

            if (_matchManager != null && _matchManager.IsMatchComplete())
            {
                Debug.Log("[TurnManager] Match complete. Stopping.");
                yield break;
            }

            yield return StartCoroutine(StartNextRound(winnerIndex < 0));
        }

        IEnumerator StartNextRound(bool isDraw = false)
        {
            _p1.Temperature.Value = TemperatureSystem.MAX_TEMP;
            _p2.Temperature.Value = TemperatureSystem.MAX_TEMP;
            _p1.IsReady.Value = false;
            _p2.IsReady.Value = false;
            _p1.IsFanActive.Value = false;
            _p2.IsFanActive.Value = false;
            _p1.FanSpeed.Value = TemperatureSystem.DEFAULT_FAN_SPEED;
            _p2.FanSpeed.Value = TemperatureSystem.DEFAULT_FAN_SPEED;
            _p1.IsFanUpgraded.Value = false;
            _p2.IsFanUpgraded.Value = false;

            _p1.GetInventory().ResetForNewRound();
            _p2.GetInventory().ResetForNewRound();

            var dropTable = GetDropTable();
            if (dropTable != null)
            {
                _p1.GetInventory().GrantRandomItems(4, dropTable);
                _p2.GetInventory().GrantRandomItems(4, dropTable);
            }

            _buffSystem.ClearAll();
            ActiveEnvironment.Value = EnvironmentType.None;
            TurnNumber.Value = 0;

            if (!isDraw && _matchManager != null)
                _matchManager.StartRound();

            ReviveVisualsClientRpc();

            Debug.Log(isDraw
                ? "[TurnManager] Draw — round voided, replaying"
                : "[TurnManager] New round started — temperatures reset");

            yield return StartCoroutine(PrepPhaseRoutine());
        }

        void ExecuteSubItems(PlayerState player, int playerIndex)
        {
            var queue = player.GetActionQueue();
            if (!queue.subAction.HasValue)
            {
                Debug.Log($"[COMBAT] ExecuteSubItems: P{playerIndex} — no sub item");
                return;
            }

            var action = queue.subAction.Value;
            var inventory = player.GetInventory();
            var opponent = playerIndex == 0 ? _p2 : _p1;

            float userTempBefore = player.Temperature.Value;
            float opponentTempBefore = opponent.Temperature.Value;

            Debug.Log($"[COMBAT] ExecuteSubItems: P{playerIndex} using '{action.ItemData.ItemName}' (slot={action.SlotIndex})");
            Debug.Log($"[COMBAT] ExecuteSubItems BEFORE: P{playerIndex}={userTempBefore:F1}° Opp={opponentTempBefore:F1}°");

            var ctx = new Item.Data.ItemContext
            {
                User = player,
                Target = opponent,
                UserIndex = playerIndex,
                TargetIndex = 1 - playerIndex,
                UserInventory = inventory,
                TargetInventory = opponent.GetInventory(),
                AllModifiers = _modifiers,
                TempSystem = _tempSystem,
                BuffSystem = _buffSystem,
                DropTable = GetDropTable(),
                SlotIndex = action.SlotIndex,
                UserSlot = inventory.SlotStates[action.SlotIndex],
            };

            action.ItemData.ExecuteEffect(ctx);
            inventory.ConsumeItem(action.SlotIndex);

            Debug.Log($"[COMBAT] ExecuteSubItems AFTER: P{playerIndex}={player.Temperature.Value:F1}° Opp={opponent.Temperature.Value:F1}°");
        }

        IEnumerator EnvironmentAnnouncementRoutine()
        {
            // TODO: 테스트용 — Kids/Ambulance/CoolBreeze만 출현. 테스트 후 원래 풀로 복원할 것
            var pool = new[] {
                EnvironmentType.Kids, EnvironmentType.Ambulance, EnvironmentType.CoolBreeze
            };
            ActiveEnvironment.Value = pool[Random.Range(0, pool.Length)];

            Debug.Log($"[ENV] Environment selected: {ActiveEnvironment.Value} ({GetEnvironmentName(ActiveEnvironment.Value)})");

            AnnounceEnvironmentClientRpc(ActiveEnvironment.Value);

            yield return _waitFour;
        }

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

        void RemoveRandomUnusedItem(PlayerInventory inventory)
        {
            if (!IsServer) return;

            var candidates = new List<int>();
            for (int i = 0; i < inventory.SlotStates.Count; i++)
            {
                var slot = inventory.SlotStates[i];
                if (slot.IsEmpty) continue;
                var itemData = inventory.GetItemData(i);
                if (itemData == null) continue;
                if (itemData.Persistence != ItemPersistence.RandomConsumable) continue;
                candidates.Add(i);
            }

            if (candidates.Count == 0) return;

            int targetSlot = candidates[Random.Range(0, candidates.Count)];
            var targetItem = inventory.GetItemData(targetSlot);
            string itemName = targetItem != null ? targetItem.ItemName : "?";

            var removedSlot = inventory.SlotStates[targetSlot];
            removedSlot.ItemId = -1;
            removedSlot.RemainingUses = 0;
            inventory.SlotStates[targetSlot] = removedSlot;
            inventory.CompactSlots();

            Debug.Log($"[ENV] Kids: removed '{itemName}' from slot {targetSlot}");
        }

        static string GetEnvironmentName(EnvironmentType env)
        {
            return env switch
            {
                EnvironmentType.SunnyDay => "햇살쨍쨍",
                EnvironmentType.CoolBreeze => "바람선선",
                EnvironmentType.CicadaSong => "매미울음",
                EnvironmentType.Kids => "잼민이들",
                EnvironmentType.Ambulance => "앰뷸런스",
                EnvironmentType.SummerVacation => "여름방학",
                EnvironmentType.HeatWaveWarning => "폭염경보",
                _ => ""
            };
        }

        [Rpc(SendTo.Everyone)]
        void ReviveVisualsClientRpc()
        {
            foreach (var visual in FindObjectsByType<AZPlayerVisual>(FindObjectsSortMode.None))
                visual.ReviveVisual();
        }

        [Rpc(SendTo.Everyone)]
        void OnPhaseChangedClientRpc(TurnPhase phase, int turnNumber)
        {
        }

        [Rpc(SendTo.Everyone)]
        public void OnItemUsedClientRpc(byte playerIndex, byte slotIndex, byte category, bool isInstant)
        {
        }

        public static event System.Action<byte, short> OnOpponentRevealed;

        [Rpc(SendTo.Everyone)]
        public void RevealOpponentItemClientRpc(byte forPlayerIndex, short opponentItemId)
        {
            OnOpponentRevealed?.Invoke(forPlayerIndex, opponentItemId);
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
            bool isLocalP1 = NetworkManager.Singleton.LocalClientId == 0;
            bool healSelf = (p1IsLower && isLocalP1) || (!p1IsLower && !isLocalP1);
            vfx.PlayAmbulanceBlanketStaging(healSelf);
        }
    }
}
