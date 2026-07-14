using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.UI.TestUI
{
    /// <summary>
    /// 네트워크 아이템 테스트 매니저 (ItemNetTestScene 전용, PLAN_006).
    /// 기획서 5.3의 ServerRpc 왕복 구조를 실제로 검증한다:
    ///   클라 버튼 클릭 → ServerRpc → 서버 검증(페이즈/Ready/슬롯/타입/CanUse) → 서버에서만 상태 변경
    ///   → NetworkVariable/NetworkList 자동 동기화 + LogRpc(ClientRpc)로 클라 갱신.
    /// 타이머는 FIX-04 방식: PrepStartServerTime + PrepDuration만 동기화, 클라가 로컬 계산.
    /// ※ 테스트 전용 — 실제 게임에서는 이 역할을 PlayerState ServerRpc + TurnManager(코어 담당)가 수행.
    /// </summary>
    public class ItemNetTestManager : NetworkBehaviour
    {
        public static ItemNetTestManager Instance { get; private set; }

        public enum NetPhase : byte { Waiting = 0, Prep = 1, Attack = 2, RoundOver = 3 }

        // ═══ NetworkVariables (Everyone read / Server write) ═══
        public NetworkVariable<byte> Phase = new((byte)NetPhase.Waiting);
        public NetworkVariable<int> TurnNumber = new(0);
        public NetworkVariable<double> PrepStartServerTime = new(0);   // FIX-04: 시작 시각만 동기화
        public NetworkVariable<float> PrepDuration = new(20f);
        public NetworkVariable<sbyte> WinnerIndex = new(-1);
        public NetworkVariable<ulong> P1ClientId = new(ulong.MaxValue);
        public NetworkVariable<ulong> P2ClientId = new(ulong.MaxValue);
        public NetworkVariable<NetworkObjectReference> P1Ref = new(default);
        public NetworkVariable<NetworkObjectReference> P2Ref = new(default);

        /// <summary>클라 UI 구독용 — LogRpc 수신 시 발화</summary>
        public event System.Action<string> OnLogReceived;

        const float PREP_DURATION = 20f;      // GAME_DESIGN: 준비 20초
        const int INITIAL_RANDOM_GRANT = 4;   // 기획 확정(2026-07-15): 시작 시 랜덤 4개 기본 지급
        const float DUMMY_READY_DELAY = 3f;   // 더미 상대는 3초 후 자동 준비(무행동)
        const ulong DUMMY_SENTINEL = ulong.MaxValue - 1;

        // RULE-020: WaitForSeconds 캐싱
        static readonly WaitForSeconds WaitHalf = new(0.5f);
        static readonly WaitForSeconds WaitOne = new(1f);

        // ═══ Server-local (동기화 안 함) ═══
        readonly PlayerState[] _players = new PlayerState[2];
        readonly PlayerInventory[] _invs = new PlayerInventory[2];
        readonly ulong[] _clientIds = { ulong.MaxValue, ulong.MaxValue };
        readonly bool[] _isDummy = new bool[2];
        int _playerCount;
        GameObject _playerPrefab;
        ItemDataSO[] _basicItems;
        ItemDataSO[] _registry;
        ItemDropTable _dropTable;
        readonly TemperatureSystem _tempSystem = new();
        readonly BuffDebuffSystem _buffSystem = new();
        PlayerModifiers[] _modifiers = new PlayerModifiers[2];
        readonly int[] _queuedMain = { -1, -1 };
        // [기획 확정 07-15] Sub도 선택 시 큐잉만 — 발동/소모는 공격 턴 시작 시점 (5.3의 '준비 중 즉시 실행'과 다름)
        readonly List<byte>[] _queuedSubs = { new List<byte>(), new List<byte>() };
        readonly float[] _readyTs = new float[2];
        Coroutine _loop;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (IsServer)
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
        }

        public override void OnNetworkDespawn()
        {
            // RULE-010: 콜백 정리
            if (IsServer && NetworkManager != null)
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            if (Instance == this) Instance = null;
        }

        #region Server Setup

        /// <summary>서버 전용 — 호스트 UI가 매니저 spawn 직후 호출</summary>
        public void ServerInitialize(GameObject playerPrefab,
            ItemDataSO fan, ItemDataSO windbreaker, ItemDataSO warmTea, ItemDataSO cat)
        {
            if (!IsServer) return;
            _playerPrefab = playerPrefab;
            _basicItems = new[] { fan, windbreaker, warmTea, cat };
            _registry = ItemTestRegistry.Build(fan, windbreaker, warmTea, cat, out _dropTable);

            // 매니저 spawn 이전에 이미 접속한 클라(호스트 자신 포함) 처리
            foreach (var id in NetworkManager.ConnectedClientsIds)
                TrySpawnPlayerFor(id);
        }

        /// <summary>서버 전용 — 1인 테스트용 더미 상대 (서버 소유, 매 턴 무행동 자동 준비)</summary>
        public void ServerAddDummy()
        {
            if (!IsServer || _registry == null || _playerCount >= 2) return;
            SpawnPlayer(NetworkManager.ServerClientId, dummy: true);
        }

        void OnClientConnected(ulong clientId)
        {
            if (IsServer) TrySpawnPlayerFor(clientId);
        }

        void TrySpawnPlayerFor(ulong clientId)
        {
            if (_registry == null) return;   // ServerInitialize 전
            if (_playerCount >= 2)
            {
                LogRpc($"관전자 접속 (client {clientId}) — 슬롯 없음");
                return;
            }
            for (int i = 0; i < _playerCount; i++)
                if (!_isDummy[i] && _clientIds[i] == clientId) return;   // 중복 방지
            SpawnPlayer(clientId, dummy: false);
        }

        void SpawnPlayer(ulong ownerClientId, bool dummy)
        {
            int idx = _playerCount;
            // 2.5D 구도: P1/P2가 마루를 사이에 두고 z축으로 마주 앉음 (캐릭터 판 높이 1.7 → 중심 y 0.85)
            var go = Instantiate(_playerPrefab, new Vector3(0f, 0.85f, idx == 0 ? -1.9f : 1.9f), Quaternion.identity);
            go.name = dummy ? $"P{idx + 1}(Dummy)" : $"P{idx + 1}";
            var netObj = go.GetComponent<NetworkObject>();
            netObj.SpawnWithOwnership(ownerClientId);

            _players[idx] = go.GetComponent<PlayerState>();
            _players[idx].SetPlayerIndex(idx);
            _invs[idx] = go.GetComponent<PlayerInventory>();
            _invs[idx].Initialize(_registry);
            _invs[idx].InitializeBasicItems(_basicItems[0], _basicItems[1], _basicItems[2], _basicItems[3]);
            _invs[idx].GrantRandomItems(INITIAL_RANDOM_GRANT, _dropTable);   // 기획: 시작 랜덤 4개
            _players[idx].Temperature.Value = TemperatureSystem.MAX_TEMP;
            _players[idx].IsFanActive.Value = false;

            _clientIds[idx] = dummy ? DUMMY_SENTINEL : ownerClientId;
            _isDummy[idx] = dummy;
            _playerCount++;

            if (idx == 0) { P1ClientId.Value = _clientIds[0]; P1Ref.Value = netObj; }
            else { P2ClientId.Value = _clientIds[1]; P2Ref.Value = netObj; }

            LogRpc(dummy ? $"P{idx + 1} 더미 상대 참가 (매 턴 무행동)" : $"P{idx + 1} 참가 (client {ownerClientId})");

            if (_playerCount == 2)
                _loop = StartCoroutine(TurnLoop());
            else
                LogRpc("상대 대기 중 — MPPM 가상 플레이어로 접속하거나 [더미 상대 추가]");
        }

        int PlayerIndexOf(ulong senderClientId)
        {
            for (int i = 0; i < _playerCount; i++)
                if (!_isDummy[i] && _clientIds[i] == senderClientId) return i;
            return -1;
        }

        #endregion

        #region Turn Loop (서버 전용 — 기획서 Section 5 축소판)

        IEnumerator TurnLoop()
        {
            while (true)
            {
                // ── 턴 초기화 ──
                TurnNumber.Value++;
                _modifiers[0].Reset();
                _modifiers[1].Reset();
                _buffSystem.ProcessTurnStart(_players[0], _players[1]);
                for (int i = 0; i < 2; i++)
                {
                    _queuedMain[i] = -1;
                    _queuedSubs[i].Clear();
                    _readyTs[i] = 0f;
                    _players[i].IsReady.Value = false;
                    _players[i].IsFanActive.Value = true;   // 준비 페이즈: 선풍기 자동 ON
                }

                // FIX-04: 타이머는 시작 시각 + 지속 시간만 1회 동기화
                PrepStartServerTime.Value = NetworkManager.ServerTime.Time;
                PrepDuration.Value = PREP_DURATION;
                Phase.Value = (byte)NetPhase.Prep;
                LogRpc($"─── 턴 {TurnNumber.Value} 준비 페이즈 (20s): 선풍기 ON, Main 선택/준비 끝 = 회복 시작 ───");

                // ── 준비 페이즈 루프 ──
                float elapsed = 0f;
                bool roundEnded = false;
                while (elapsed < PREP_DURATION)
                {
                    float dt = Time.deltaTime;
                    elapsed += dt;

                    for (int i = 0; i < 2; i++)
                    {
                        _tempSystem.TickFan(_players[i], dt);
                        _tempSystem.TickRecovery(_players[i], dt, TemperatureSystem.DEFAULT_RECOVERY_RATE);
                        _tempSystem.CheckThresholds(_players[i], _invs[i],
                            _invs[i].GetThresholdGranted(), _dropTable);

                        // 더미: 일정 시간 후 자동 준비 (무행동)
                        if (_isDummy[i] && !_players[i].IsReady.Value && elapsed >= DUMMY_READY_DELAY)
                        {
                            SetReadyServer(i);
                            LogRpc($"P{i + 1}(더미) 준비 완료 — 무행동");
                        }

                        // 준비 중 0° = 즉시 패배 (GAME_DESIGN)
                        if (_tempSystem.IsDead(_players[i]))
                        {
                            EndRound(1 - i, $"P{i + 1} 준비 중 동결 (0°)");
                            roundEnded = true;
                            break;
                        }
                    }
                    if (roundEnded) yield break;

                    if (_players[0].IsReady.Value && _players[1].IsReady.Value)
                        break;

                    yield return null;
                }

                // ── 시간 만료 → 강제 Ready (무행동, 완전 무방비) ──
                for (int i = 0; i < 2; i++)
                {
                    if (!_players[i].IsReady.Value)
                    {
                        SetReadyServer(i);
                        LogRpc($"P{i + 1} 시간 만료 — 무행동 처리");
                    }
                }

                // ── 공격 페이즈 ──
                Phase.Value = (byte)NetPhase.Attack;
                yield return WaitHalf;

                if (ResolveAttack()) yield break;

                yield return WaitOne;
            }
        }

        /// <summary>CombatResolver(Section 6) 축소판 — 방어 선적용 + Ready 순 순차 실행</summary>
        bool ResolveAttack()
        {
            int first = _readyTs[0] <= _readyTs[1] ? 0 : 1;
            LogRpc($"공격 순서: P{first + 1} → P{2 - first} (먼저 Ready한 쪽부터)");

            // Step 0: Sub 효과 발동 — 공격 턴 시작 시점 (기획 확정 07-15), Ready 순서대로
            foreach (int p in new[] { first, 1 - first })
            {
                foreach (byte slot in _queuedSubs[p])
                {
                    if (slot >= _invs[p].SlotStates.Count) continue;
                    var slotData = _invs[p].SlotStates[slot];
                    if (!slotData.IsUsable) continue;   // 실행 시점 재검증
                    var subItem = _invs[p].GetItemData(slot);

                    var subCtx = BuildContext(p, slot, slotData);
                    subItem.ExecuteEffect(subCtx);
                    _invs[p].ConsumeItem(slot);
                    LogRpc($"P{p + 1} {subItem.ItemName} (Sub) 발동" +
                           (subItem is SabotageItemDataSO ? $" → P{2 - p} 랜덤 아이템 리롤" : ""));
                }
                _queuedSubs[p].Clear();
            }

            // Step 1: 방어 먼저 적용 — 순서 무관 (FIX-08)
            for (int p = 0; p < 2; p++)
            {
                if (_queuedMain[p] < 0) continue;
                if (_invs[p].GetItemData(_queuedMain[p]) is DefenseItemDataSO def)
                {
                    _modifiers[p].ActiveDefense = new DefenseInfo { Filter = def.Filter, BlockAmount = def.BlockAmount };
                    _invs[p].ConsumeItem((byte)_queuedMain[p]);
                    LogRpc($"P{p + 1} {def.ItemName} 방어 활성 ({def.BlockAmount}° / {def.Filter})");
                }
            }

            // Step 2: Main 순차 실행 + 사이사이 사망 체크
            foreach (int p in new[] { first, 1 - first })
            {
                int slot = _queuedMain[p];
                if (slot < 0)
                {
                    LogRpc($"P{p + 1} 무행동");
                    continue;
                }
                var item = _invs[p].GetItemData(slot);
                if (item is DefenseItemDataSO) continue;   // Step 1에서 처리됨

                var ctx = BuildContext(p, (byte)slot, _invs[p].SlotStates[slot]);
                item.ExecuteEffect(ctx);                    // FIX-13: Consume 전에 실행
                _invs[p].ConsumeItem((byte)slot);

                LogRpc($"P{p + 1} {item.ItemName} → P1 {_players[0].Temperature.Value:F1}° / P2 {_players[1].Temperature.Value:F1}°");

                for (int i = 0; i < 2; i++)
                {
                    if (_tempSystem.IsDead(_players[i]))
                    {
                        EndRound(1 - i, $"P{i + 1} 동결 (0°)");
                        return true;
                    }
                }
            }
            return false;
        }

        void EndRound(int winner, string reason)
        {
            Phase.Value = (byte)NetPhase.RoundOver;
            WinnerIndex.Value = (sbyte)winner;
            for (int i = 0; i < 2; i++) _players[i].IsFanActive.Value = false;
            LogRpc($"═══ {reason} → P{winner + 1} 승리! [리셋]으로 재시작 ═══");
        }

        void SetReadyServer(int p)
        {
            _players[p].IsReady.Value = true;
            _players[p].IsFanActive.Value = false;   // 선풍기 정지 → 회복 시작
            _readyTs[p] = Time.time;
        }

        ItemContext BuildContext(int userIdx, byte slotIndex, ItemSlotNetData slot)
        {
            int targetIdx = 1 - userIdx;
            return new ItemContext
            {
                User = _players[userIdx],
                Target = _players[targetIdx],
                UserIndex = userIdx,
                TargetIndex = targetIdx,
                UserInventory = _invs[userIdx],
                TargetInventory = _invs[targetIdx],
                UserSlot = slot,
                SlotIndex = slotIndex,
                TempSystem = _tempSystem,
                BuffSystem = _buffSystem,
                DropTable = _dropTable,
                AllModifiers = _modifiers,
            };
        }

        #endregion

        #region ServerRpcs — 클라 입력의 유일한 경로 (기획서 5.3 검증 체크리스트)

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void UseSubItemRpc(byte slotIndex, RpcParams rpcParams = default)
        {
            int p = ValidateItemRequest(slotIndex, ItemSlotType.Sub, rpcParams);
            if (p < 0) return;
            if (_queuedSubs[p].Contains(slotIndex)) return;   // 중복 큐잉 방지

            // [기획 확정 07-15] Sub는 선택 시 큐잉만 — 발동/소모는 공격 턴 시작 시점 (턴은 유지, 계속 선택 가능)
            _queuedSubs[p].Add(slotIndex);
            LogRpc($"P{p + 1} {_invs[p].GetItemData(slotIndex).ItemName} (Sub) 선택 — 공격 턴 시작 시 발동");
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void SelectMainItemRpc(byte slotIndex, RpcParams rpcParams = default)
        {
            int p = ValidateItemRequest(slotIndex, ItemSlotType.Main, rpcParams);
            if (p < 0) return;

            // Main: 큐잉 + 자동 Ready → 선풍기 정지, 회복 시작
            _queuedMain[p] = slotIndex;
            SetReadyServer(p);
            LogRpc($"P{p + 1} Main {_invs[p].GetItemData(slotIndex).ItemName} 선택 → 준비 완료 (회복 시작)");
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void PressReadyRpc(RpcParams rpcParams = default)
        {
            if ((NetPhase)Phase.Value != NetPhase.Prep) return;
            int p = PlayerIndexOf(rpcParams.Receive.SenderClientId);
            if (p < 0) return;
            if (_players[p].IsReady.Value) return;

            SetReadyServer(p);
            LogRpc($"P{p + 1} 준비 끝 (Main 없음 — Sub 효과만)");
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        public void RequestResetRpc(RpcParams rpcParams = default)
        {
            if ((NetPhase)Phase.Value == NetPhase.Waiting) return;
            if (PlayerIndexOf(rpcParams.Receive.SenderClientId) < 0) return;
            if (_loop != null) StopCoroutine(_loop);

            _buffSystem.ClearAll();
            _modifiers[0].Reset();
            _modifiers[1].Reset();
            TurnNumber.Value = 0;
            WinnerIndex.Value = -1;
            for (int i = 0; i < 2; i++)
            {
                _queuedMain[i] = -1;
                _players[i].Temperature.Value = TemperatureSystem.MAX_TEMP;
                _players[i].IsFanActive.Value = false;
                _players[i].IsReady.Value = false;
                _invs[i].ResetForNewRound();                       // 소모품 리필 + 랜덤/구간 초기화
                _invs[i].GrantRandomItems(INITIAL_RANDOM_GRANT, _dropTable);   // 시작 랜덤 4개 재지급
            }
            LogRpc("═══ 리셋 — 온도 37°, 소모품 리필, 시작 랜덤 4개 재지급 ═══");
            _loop = StartCoroutine(TurnLoop());
        }

        /// <summary>공통 검증: 서버? 페이즈? 발신자 유효? Ready 전? 슬롯 유효? 사용 가능? 타입 일치? CanUse?</summary>
        int ValidateItemRequest(byte slotIndex, ItemSlotType expectedType, RpcParams rpcParams)
        {
            if (!IsServer) return -1;
            if ((NetPhase)Phase.Value != NetPhase.Prep) return -1;
            int p = PlayerIndexOf(rpcParams.Receive.SenderClientId);
            if (p < 0) return -1;
            if (_players[p].IsReady.Value) return -1;
            if (slotIndex >= _invs[p].SlotStates.Count) return -1;
            var slot = _invs[p].SlotStates[slotIndex];
            if (!slot.IsUsable) return -1;
            var item = _invs[p].GetItemData(slotIndex);
            if (item.SlotType != expectedType) return -1;
            if (!item.CanUse(BuildContext(p, slotIndex, slot))) return -1;
            return p;
        }

        #endregion

        #region ClientRpc

        [Rpc(SendTo.Everyone)]
        void LogRpc(string message)
        {
            OnLogReceived?.Invoke(message);
        }

        #endregion
    }
}
