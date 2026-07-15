using System.Collections;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Common;
using AbsoluteZero.Core.Item;
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
        [SerializeField] float roundEndDelay = 3f;

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

        public static event System.Action<CombatResultData> OnCombatResult;

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
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
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

            float currentPrepDuration = prepDuration;
            PrepStartServerTime.Value = NetworkManager.ServerTime.Time;
            PrepDuration.Value = currentPrepDuration;

            CurrentPhase.Value = TurnPhase.PrepPhase;
            OnPhaseChangedClientRpc(TurnPhase.PrepPhase, TurnNumber.Value);

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
                while (_tempSystem.ConsumeTick())
                {
                    _tempSystem.ApplyFanTick(_p1);
                    _tempSystem.ApplyFanTick(_p2);
                    _tempSystem.ApplyRecoveryTick(_p1, recoveryRate);
                    _tempSystem.ApplyRecoveryTick(_p2, recoveryRate);
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

        IEnumerator AttackPhaseRoutine()
        {
            if (!IsSpawned) yield break;
            CurrentPhase.Value = TurnPhase.AttackPhase;
            OnPhaseChangedClientRpc(TurnPhase.AttackPhase, TurnNumber.Value);

            _buffSystem.ProcessTurnStart(_p1, _p2);

            var q1 = _p1.GetActionQueue();
            var q2 = _p2.GetActionQueue();
            short p1SubId = q1.subAction.HasValue
                ? _p1.GetInventory().SlotStates[q1.subAction.Value.SlotIndex].ItemId
                : (short)-1;
            short p2SubId = q2.subAction.HasValue
                ? _p2.GetInventory().SlotStates[q2.subAction.Value.SlotIndex].ItemId
                : (short)-1;

            ExecuteSubItems(_p1, 0);
            ExecuteSubItems(_p2, 1);

            float p1TempBeforeCombat = _p1.Temperature.Value;
            float p2TempBeforeCombat = _p2.Temperature.Value;

            yield return _waitOne;
            if (!IsSpawned) yield break;

            var result = _combatResolver.Resolve(q1, q2, _modifiers, _p1, _p2, _tempSystem, _buffSystem);

            result.P1TempAtTurnStart = _p1TempAtTurnStart;
            result.P2TempAtTurnStart = _p2TempAtTurnStart;
            result.P1TempBeforeCombat = p1TempBeforeCombat;
            result.P2TempBeforeCombat = p2TempBeforeCombat;
            result.P1TempAfterCombat = _p1.Temperature.Value;
            result.P2TempAfterCombat = _p2.Temperature.Value;
            result.P1SubItemId = p1SubId;
            result.P2SubItemId = p2SubId;

            OnCombatResultClientRpc(result.ToNetData());

            yield return _waitTwo;
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
            _tempSystem.CheckThresholds(_p1, inv1, inv1.GetThresholdGranted(), GetDropTable());
            _tempSystem.CheckThresholds(_p2, inv2, inv2.GetThresholdGranted(), GetDropTable());

            yield return _waitOne;
            if (!IsSpawned) yield break;

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

            _p1.GetInventory().ResetForNewRound();
            _p2.GetInventory().ResetForNewRound();

            _buffSystem.ClearAll();
            TurnNumber.Value = 0;

            if (!isDraw && _matchManager != null)
                _matchManager.StartRound();

            Debug.Log(isDraw
                ? "[TurnManager] Draw — round voided, replaying"
                : "[TurnManager] New round started — temperatures reset");

            yield return StartCoroutine(PrepPhaseRoutine());
        }

        void ExecuteSubItems(PlayerState player, int playerIndex)
        {
            var queue = player.GetActionQueue();
            if (!queue.subAction.HasValue) return;

            var action = queue.subAction.Value;
            var inventory = player.GetInventory();
            var opponent = playerIndex == 0 ? _p2 : _p1;

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

            Debug.Log($"[TurnManager] Sub item executed: P{playerIndex} used {action.ItemData.ItemName}");
        }

        [Rpc(SendTo.Everyone)]
        void OnPhaseChangedClientRpc(TurnPhase phase, int turnNumber)
        {
        }

        [Rpc(SendTo.Everyone)]
        public void OnItemUsedClientRpc(byte playerIndex, byte slotIndex, byte category, bool isInstant)
        {
        }

        [Rpc(SendTo.Everyone)]
        void OnCombatResultClientRpc(CombatResultData resultData)
        {
            OnCombatResult?.Invoke(resultData);
        }
    }
}
