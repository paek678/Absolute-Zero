using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace AbsoluteZero.Core.Game
{
    public class AbsoluteZeroTurnManager : NetworkBehaviour
    {
        public static AbsoluteZeroTurnManager Instance { get; private set; }

        [Header("=== Turn Settings ===")]
        [SerializeField] private float prepTurnDuration = 20f;
        [SerializeField] private float attackTurnDuration = 3f;
        [SerializeField] private float startingTemperature = 37f;

        [Header("=== Action Values ===")]
        [SerializeField] private float attackDamage = 5f;
        [SerializeField] private float defendBlock = 3f;
        [SerializeField] private float chargeHeal = 3f;

        public readonly NetworkVariable<TurnPhase> CurrentPhase = new(
            TurnPhase.WaitingForPlayers, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> Player1Temp = new(
            37f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> Player2Temp = new(
            37f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> TurnTimer = new(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> RoundNumber = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<ulong> Player1ClientId = new(
            ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<ulong> Player2ClientId = new(
            ulong.MaxValue, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> WinnerIndex = new(
            -1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public event Action<ActionType, ActionType, float, float> OnResultsReceived;

        private readonly Dictionary<ulong, int> clientToPlayerIndex = new();
        private ActionType player1Action;
        private ActionType player2Action;
        private bool player1Submitted;
        private bool player2Submitted;
        private float localTimer;
        private float timerSyncAccum;
        private float tempTickAccum;
        private float attackPhaseTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                Player1Temp.Value = startingTemperature;
                Player2Temp.Value = startingTemperature;
                RoundNumber.Value = 0;
                WinnerIndex.Value = -1;
                CurrentPhase.Value = TurnPhase.WaitingForPlayers;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (!IsServer) return;

            switch (CurrentPhase.Value)
            {
                case TurnPhase.WaitingForPlayers:
                    CheckForPlayers();
                    break;
                case TurnPhase.PrepTurn:
                    UpdatePrepTurn();
                    break;
                case TurnPhase.AttackTurn:
                    UpdateAttackTurn();
                    break;
            }
        }

        #region Server: State Machine

        private void CheckForPlayers()
        {
            if (NetworkManager.Singleton.ConnectedClientsList.Count < 2) return;

            AssignPlayerIndices();
            BeginPrepTurn();
        }

        private void AssignPlayerIndices()
        {
            clientToPlayerIndex.Clear();
            int index = 0;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (index >= 2) break;
                clientToPlayerIndex[client.ClientId] = index;

                if (index == 0)
                    Player1ClientId.Value = client.ClientId;
                else
                    Player2ClientId.Value = client.ClientId;

                index++;
            }

            Debug.Log($"[TurnManager] Players assigned: P1={Player1ClientId.Value}, P2={Player2ClientId.Value}");
        }

        private void BeginPrepTurn()
        {
            RoundNumber.Value++;
            player1Action = ActionType.None;
            player2Action = ActionType.None;
            player1Submitted = false;
            player2Submitted = false;
            localTimer = prepTurnDuration;
            timerSyncAccum = 0f;
            tempTickAccum = 0f;

            TurnTimer.Value = prepTurnDuration;
            CurrentPhase.Value = TurnPhase.PrepTurn;

            NotifyPrepStartRpc();
            Debug.Log($"[TurnManager] PrepTurn started — Round {RoundNumber.Value}");
        }

        private void UpdatePrepTurn()
        {
            float dt = Time.deltaTime;
            localTimer -= dt;

            timerSyncAccum += dt;
            if (timerSyncAccum >= 0.1f)
            {
                timerSyncAccum = 0f;
                TurnTimer.Value = Mathf.Max(0f, localTimer);
            }

            tempTickAccum += dt;
            while (tempTickAccum >= 1f)
            {
                tempTickAccum -= 1f;
                Player1Temp.Value = Mathf.Max(0f, Player1Temp.Value - 1f);
                Player2Temp.Value = Mathf.Max(0f, Player2Temp.Value - 1f);

                if (Player1Temp.Value <= 0f || Player2Temp.Value <= 0f)
                {
                    EndGame();
                    return;
                }
            }

            if (localTimer <= 0f || (player1Submitted && player2Submitted))
            {
                BeginAttackTurn();
            }
        }

        private void BeginAttackTurn()
        {
            if (!player1Submitted) player1Action = ActionType.Defend;
            if (!player2Submitted) player2Action = ActionType.Defend;

            float p1OutDmg = GetOutgoingDamage(player1Action);
            float p1Def = GetDefense(player1Action);
            float p1Heal = GetSelfHeal(player1Action);

            float p2OutDmg = GetOutgoingDamage(player2Action);
            float p2Def = GetDefense(player2Action);
            float p2Heal = GetSelfHeal(player2Action);

            float damageToP1 = Mathf.Max(0f, p2OutDmg - p1Def);
            float damageToP2 = Mathf.Max(0f, p1OutDmg - p2Def);

            Player1Temp.Value = Mathf.Max(0f, Player1Temp.Value - damageToP1 + p1Heal);
            Player2Temp.Value = Mathf.Max(0f, Player2Temp.Value - damageToP2 + p2Heal);

            TurnTimer.Value = 0f;
            attackPhaseTimer = attackTurnDuration;
            CurrentPhase.Value = TurnPhase.AttackTurn;

            ShowResultsRpc(
                (byte)player1Action, (byte)player2Action,
                damageToP1, damageToP2, p1Heal, p2Heal);

            Debug.Log($"[TurnManager] AttackTurn — P1:{player1Action}(dmg:{damageToP2}) P2:{player2Action}(dmg:{damageToP1})");
        }

        private void UpdateAttackTurn()
        {
            attackPhaseTimer -= Time.deltaTime;
            if (attackPhaseTimer > 0f) return;

            if (Player1Temp.Value <= 0f || Player2Temp.Value <= 0f)
            {
                EndGame();
            }
            else
            {
                BeginPrepTurn();
            }
        }

        private void EndGame()
        {
            if (Player1Temp.Value <= 0f && Player2Temp.Value <= 0f)
                WinnerIndex.Value = -1; // draw
            else if (Player1Temp.Value <= 0f)
                WinnerIndex.Value = 1;
            else if (Player2Temp.Value <= 0f)
                WinnerIndex.Value = 0;
            else
                WinnerIndex.Value = Player1Temp.Value >= Player2Temp.Value ? 0 : 1;

            CurrentPhase.Value = TurnPhase.GameOver;
            Debug.Log($"[TurnManager] GameOver — Winner: Player {WinnerIndex.Value + 1}");
        }

        #endregion

        #region Action Values

        private float GetOutgoingDamage(ActionType action) =>
            action == ActionType.Attack ? attackDamage : 0f;

        private float GetDefense(ActionType action) =>
            action == ActionType.Defend ? defendBlock : 0f;

        private float GetSelfHeal(ActionType action) =>
            action == ActionType.Charge ? chargeHeal : 0f;

        #endregion

        #region RPCs

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SubmitActionRpc(byte actionByte, RpcParams rpcParams = default)
        {
            if (CurrentPhase.Value != TurnPhase.PrepTurn) return;

            ulong senderId = rpcParams.Receive.SenderClientId;
            if (!clientToPlayerIndex.TryGetValue(senderId, out int playerIndex)) return;

            ActionType action = (ActionType)actionByte;

            if (playerIndex == 0 && !player1Submitted)
            {
                player1Action = action;
                player1Submitted = true;
                Debug.Log($"[TurnManager] P1 submitted: {action}");
            }
            else if (playerIndex == 1 && !player2Submitted)
            {
                player2Action = action;
                player2Submitted = true;
                Debug.Log($"[TurnManager] P2 submitted: {action}");
            }
        }

        [Rpc(SendTo.Everyone)]
        private void ShowResultsRpc(byte p1Action, byte p2Action,
            float damageToP1, float damageToP2, float p1Heal, float p2Heal)
        {
            float p1Delta = -damageToP1 + p1Heal;
            float p2Delta = -damageToP2 + p2Heal;
            OnResultsReceived?.Invoke((ActionType)p1Action, (ActionType)p2Action, p1Delta, p2Delta);
        }

        [Rpc(SendTo.Everyone)]
        private void NotifyPrepStartRpc()
        {
            // UI uses this to reset action buttons
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestRestartRpc()
        {
            if (CurrentPhase.Value != TurnPhase.GameOver) return;

            Player1Temp.Value = startingTemperature;
            Player2Temp.Value = startingTemperature;
            RoundNumber.Value = 0;
            WinnerIndex.Value = -1;

            Debug.Log("[TurnManager] Game restarted");
            BeginPrepTurn();
        }

        #endregion

        #region Public Queries

        public int GetPlayerIndexForClient(ulong clientId)
        {
            if (Player1ClientId.Value == clientId) return 0;
            if (Player2ClientId.Value == clientId) return 1;
            return -1;
        }

        public int GetLocalPlayerIndex()
        {
            if (NetworkManager.Singleton == null) return -1;
            return GetPlayerIndexForClient(NetworkManager.Singleton.LocalClientId);
        }

        public float GetPlayerTemperature(int playerIndex)
        {
            return playerIndex == 0 ? Player1Temp.Value : Player2Temp.Value;
        }

        public string GetActionName(ActionType action) => action switch
        {
            ActionType.Attack => "Attack",
            ActionType.Defend => "Defend",
            ActionType.Charge => "Charge",
            _ => "None"
        };

        #endregion
    }
}
