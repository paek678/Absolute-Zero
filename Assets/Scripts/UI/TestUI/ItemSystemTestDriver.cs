using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using AbsoluteZero.Core.Buff;
using AbsoluteZero.Core.Combat;
using AbsoluteZero.Core.Item;
using AbsoluteZero.Core.Player;

namespace AbsoluteZero.UI.TestUI
{
    /// <summary>
    /// 아이템 시스템 테스트 드라이버 (ItemTestScene 전용, PLAN_005).
    /// 기획서 Section 5 턴 루프를 그대로 따라 혼자서 양쪽을 조작하며 검증한다:
    ///   준비 페이즈(20s): 양쪽 선풍기 자동 ON(-1°/s) → Sub는 즉시 실행,
    ///   Main 선택 또는 [준비 끝] = 내 선풍기 정지 + 회복(+1°/s) 시작
    ///   → 양쪽 준비 완료(또는 시간 만료) → 공격 페이즈: 먼저 Ready한 쪽부터 순차 실행
    ///   (방어는 순서 무관 선적용 — FIX-08) → 사망 체크 → 다음 턴 자동 진행.
    /// 온도 구간(30/20/10°) 통과 시 테스트용 랜덤 아이템 지급 → 고양이 리롤 검증 가능.
    /// </summary>
    public class ItemSystemTestDriver : MonoBehaviour
    {
        [Header("References (씬 셋업 시 연결)")]
        // 씬 배치 NetworkObject는 프로그램 생성 씬에서 GlobalObjectIdHash가 0으로 남아
        // StartHost가 실패함 → 프리팹 등록 + 런타임 spawn 방식 사용
        [SerializeField] private GameObject playerPrefab;     // ItemTestPlayer.prefab
        [SerializeField] private ItemDataSO fanItem;          // 부채
        [SerializeField] private ItemDataSO windbreakerItem;  // 바람막이
        [SerializeField] private ItemDataSO warmTeaItem;      // 따뜻한 차
        [SerializeField] private ItemDataSO catItem;          // 고양이

        private enum TestPhase { Boot, Prep, Attack, RoundOver }

        private const float PREP_DURATION = 20f;   // GAME_DESIGN: 준비 20초
        private const int BASIC_SLOTS = 4;
        private const int SHOWN_RANDOM_SLOTS = 8;  // 기획 확정(07-15): 랜덤 보관 4×2 = 8칸
        private const int SHOWN_SLOTS = BASIC_SLOTS + SHOWN_RANDOM_SLOTS;

        // RULE-020: WaitForSeconds 캐싱
        private static readonly WaitForSeconds WaitHalf = new(0.5f);
        private static readonly WaitForSeconds WaitOne = new(1f);

        private readonly PlayerState[] _players = new PlayerState[2];
        private readonly PlayerInventory[] _inventories = new PlayerInventory[2];
        private ItemDataSO[] _registry;
        private ItemDropTable _dropTable;
        private readonly TemperatureSystem _tempSystem = new();
        private readonly BuffDebuffSystem _buffSystem = new();
        private PlayerModifiers[] _modifiers = new PlayerModifiers[2];

        private TestPhase _phase = TestPhase.Boot;
        private int _turnNumber;
        private readonly int[] _queuedMainSlot = { -1, -1 };
        private readonly float[] _readyTimestamp = new float[2];
        private Coroutine _turnLoop;

        // ═══ UI 참조 ═══
        private readonly TextMeshProUGUI[] _tempTexts = new TextMeshProUGUI[2];
        private readonly Image[] _tempBars = new Image[2];
        private readonly TextMeshProUGUI[] _stateTexts = new TextMeshProUGUI[2];
        private readonly TextMeshProUGUI[][] _slotBtnLabels = new TextMeshProUGUI[2][];
        private TextMeshProUGUI _phaseText;
        private TextMeshProUGUI _logText;
        private readonly Queue<string> _logLines = new();
        private const int MaxLogLines = 10;

        private void Start()
        {
            BuildUI();
            StartCoroutine(Bootstrap());
        }

        private void OnDestroy()
        {
            // RULE-010: NV 콜백 정리
            if (_players[0] != null) _players[0].Temperature.OnValueChanged -= OnP1TempChanged;
            if (_players[1] != null) _players[1].Temperature.OnValueChanged -= OnP2TempChanged;
            for (int i = 0; i < 2; i++)
            {
                if (_inventories[i] != null && _inventories[i].SlotStates != null)
                    _inventories[i].SlotStates.OnListChanged -= OnAnySlotChanged;
            }
        }

        #region Bootstrap

        private IEnumerator Bootstrap()
        {
            if (playerPrefab == null || fanItem == null || windbreakerItem == null || warmTeaItem == null || catItem == null)
            {
                Log("<color=red>프리팹/아이템 SO 참조 누락 — Tools > AbsoluteZero > Build ItemTestScene 재실행</color>");
                yield break;
            }

            var nm = NetworkManager.Singleton;
            nm.AddNetworkPrefab(playerPrefab);   // StartHost 전에 등록
            if (!nm.IsHost)
                nm.StartHost();

            // 레지스트리 = 기본 4종 + 테스트용 랜덤 아이템 (서버/클라 공용 팩토리)
            _registry = ItemTestRegistry.Build(fanItem, windbreakerItem, warmTeaItem, catItem, out _dropTable);

            for (int i = 0; i < 2; i++)
            {
                // 프리팹에서 spawn — 호스트(서버)에서는 Spawn() 즉시 완료
                var go = Instantiate(playerPrefab, new Vector3(i == 0 ? -2f : 2f, 1f, 0f), Quaternion.identity);
                go.name = $"P{i + 1}";
                go.GetComponent<NetworkObject>().Spawn();

                _players[i] = go.GetComponent<PlayerState>();
                _players[i].SetPlayerIndex(i);
                _inventories[i] = go.GetComponent<PlayerInventory>();
                _inventories[i].Initialize(_registry);
                _inventories[i].InitializeBasicItems(fanItem, windbreakerItem, warmTeaItem, catItem);
                _inventories[i].GrantRandomItems(4, _dropTable);   // 기획 확정(07-15): 시작 시 랜덤 4개 기본 지급
                _players[i].Temperature.Value = TemperatureSystem.MAX_TEMP;
                _players[i].IsFanActive.Value = false;
                _inventories[i].SlotStates.OnListChanged += OnAnySlotChanged;
            }
            _players[0].Temperature.OnValueChanged += OnP1TempChanged;
            _players[1].Temperature.OnValueChanged += OnP2TempChanged;

            Log("호스트 시작 — 기본 4종 + 시작 랜덤 4개 지급, 30/20/10° 통과 시 추가 지급 (최대 8칸)");
            _turnLoop = StartCoroutine(TurnLoop());
        }

        // 테스트용 랜덤 아이템/레지스트리 생성은 ItemTestRegistry(공용 팩토리) 참조

        #endregion

        #region Turn Loop (기획서 Section 5 축소판)

        private IEnumerator TurnLoop()
        {
            while (true)
            {
                // ── 1. 턴 초기화 (PrepPhaseRoutine 준용) ──
                _turnNumber++;
                _modifiers[0].Reset();
                _modifiers[1].Reset();
                _buffSystem.ProcessTurnStart(_players[0], _players[1]);   // 예약된 지연 효과 적용
                for (int i = 0; i < 2; i++)
                {
                    _queuedMainSlot[i] = -1;
                    _readyTimestamp[i] = 0f;
                    _players[i].IsReady.Value = false;
                    _players[i].IsFanActive.Value = true;   // 준비 페이즈: 선풍기 자동 ON
                }
                _phase = TestPhase.Prep;
                Log($"─── 턴 {_turnNumber} 준비 페이즈: 선풍기 ON (-1°/s), 선택하면 내 선풍기 정지 + 회복 ───");
                RefreshAll();

                // ── 2. 준비 페이즈 타이머 루프 ──
                float elapsed = 0f;
                while (elapsed < PREP_DURATION)
                {
                    float dt = Time.deltaTime;
                    elapsed += dt;

                    for (int i = 0; i < 2; i++)
                    {
                        _tempSystem.TickFan(_players[i], dt);
                        _tempSystem.TickRecovery(_players[i], dt, TemperatureSystem.DEFAULT_RECOVERY_RATE);
                        // 구간 통과 시 랜덤 아이템 지급 (1회성)
                        _tempSystem.CheckThresholds(_players[i], _inventories[i],
                            _inventories[i].GetThresholdGranted(), _dropTable);
                    }

                    _phaseText.text = $"턴 {_turnNumber} — 준비 페이즈 (남은 {Mathf.Max(0f, PREP_DURATION - elapsed):F1}s)";

                    // 준비 중 0° 도달 = 즉시 패배 (GAME_DESIGN)
                    for (int i = 0; i < 2; i++)
                    {
                        if (_tempSystem.IsDead(_players[i]))
                        {
                            EndRound(1 - i, $"P{i + 1} 준비 중 동결 (0°)");
                            yield break;
                        }
                    }

                    if (_players[0].IsReady.Value && _players[1].IsReady.Value)
                        break;

                    yield return null;
                }

                // ── 3. 시간 만료 → 강제 Ready (무행동, 완전 무방비) ──
                for (int i = 0; i < 2; i++)
                {
                    if (!_players[i].IsReady.Value)
                    {
                        SetReady(i);
                        Log($"P{i + 1} 시간 만료 — 무행동 처리");
                    }
                }

                // ── 4. 공격 페이즈: Ready 순서대로 순차 실행 ──
                _phase = TestPhase.Attack;
                _phaseText.text = $"턴 {_turnNumber} — 공격 페이즈";
                RefreshAll();
                yield return WaitHalf;

                bool roundEnded = ResolveAttack();
                if (roundEnded) yield break;

                yield return WaitOne;   // 결과 확인 시간 → 다음 턴
            }
        }

        /// <summary>CombatResolver.Resolve(Section 6) 축소판 — 방어 선적용 + Ready 순 순차 실행</summary>
        private bool ResolveAttack()
        {
            int first = _readyTimestamp[0] <= _readyTimestamp[1] ? 0 : 1;
            Log($"공격 순서: P{first + 1} → P{2 - first} (먼저 Ready한 쪽부터)");

            // Step 1: 방어 먼저 적용 — 순서 무관 (FIX-08: 방어 Main은 여기서만 설정)
            for (int p = 0; p < 2; p++)
            {
                if (_queuedMainSlot[p] < 0) continue;
                if (_inventories[p].GetItemData(_queuedMainSlot[p]) is DefenseItemDataSO def)
                {
                    _modifiers[p].ActiveDefense = new DefenseInfo { Filter = def.Filter, BlockAmount = def.BlockAmount };
                    _inventories[p].ConsumeItem((byte)_queuedMainSlot[p]);
                    Log($"P{p + 1} <b>{def.ItemName}</b> 방어 활성 ({def.BlockAmount}° / {def.Filter})");
                }
            }

            // Step 2: Main 순차 실행 (방어는 스킵) + 사이사이 사망 체크
            foreach (int p in new[] { first, 1 - first })
            {
                int slot = _queuedMainSlot[p];
                if (slot < 0)
                {
                    Log($"P{p + 1} 무행동");
                    continue;
                }
                var item = _inventories[p].GetItemData(slot);
                if (item is DefenseItemDataSO) continue;   // Step 1에서 처리됨

                var ctx = BuildContext(p, (byte)slot, _inventories[p].SlotStates[slot]);
                item.ExecuteEffect(ctx);                    // FIX-13: Consume 전에 실행
                _inventories[p].ConsumeItem((byte)slot);

                Log($"P{p + 1} <b>{item.ItemName}</b> → P1 {_players[0].Temperature.Value:F1}° / P2 {_players[1].Temperature.Value:F1}°");

                for (int i = 0; i < 2; i++)
                {
                    if (_tempSystem.IsDead(_players[i]))
                    {
                        EndRound(1 - i, $"P{i + 1} 동결 (0°)");
                        return true;
                    }
                }
            }

            RefreshAll();
            return false;
        }

        private void EndRound(int winner, string reason)
        {
            _phase = TestPhase.RoundOver;
            for (int i = 0; i < 2; i++) _players[i].IsFanActive.Value = false;
            _phaseText.text = $"라운드 종료 — P{winner + 1} 승리!";
            Log($"<color=#66CCFF>═══ {reason} → P{winner + 1} 승리! [리셋]으로 재시작 ═══</color>");
            RefreshAll();
        }

        #endregion

        #region Player Input (기획서 5.3 검증 규칙 준용)

        private void OnSlotClicked(int p, int slot)
        {
            // 검증: 준비 페이즈? 아직 Ready 전? 슬롯 유효? 사용 가능?
            if (_phase != TestPhase.Prep) return;
            if (_players[p].IsReady.Value) return;
            if (slot >= _inventories[p].SlotStates.Count) return;
            var slotData = _inventories[p].SlotStates[slot];
            if (!slotData.IsUsable) return;

            var item = _inventories[p].GetItemData(slot);
            var ctx = BuildContext(p, (byte)slot, slotData);
            if (!item.CanUse(ctx)) return;

            if (item.SlotType == ItemSlotType.Sub)
            {
                // Sub: 준비 페이즈 중 즉시 실행, 턴 유지 (5.3 UseSubItemServerRpc)
                item.ExecuteEffect(ctx);
                _inventories[p].ConsumeItem((byte)slot);
                Log($"P{p + 1} <b>{item.ItemName}</b> (Sub) 즉시 사용" +
                    (item is SabotageItemDataSO ? $" → P{2 - p} 랜덤 아이템 리롤" : ""));
                RefreshAll();
            }
            else
            {
                // Main: 큐잉 + 자동 Ready → 선풍기 정지, 회복 시작 (5.3 SelectMainItemServerRpc)
                _queuedMainSlot[p] = slot;
                SetReady(p);
                Log($"P{p + 1} Main <b>{item.ItemName}</b> 선택 → 준비 완료 (선풍기 OFF, 회복 시작)");
                RefreshAll();
            }
        }

        private void OnReadyClicked(int p)
        {
            if (_phase != TestPhase.Prep) return;
            if (_players[p].IsReady.Value) return;
            SetReady(p);
            Log($"P{p + 1} 준비 끝 (Main 없음 — Sub 효과만)");
            RefreshAll();
        }

        private void SetReady(int p)
        {
            _players[p].IsReady.Value = true;
            _players[p].IsFanActive.Value = false;   // 선풍기 정지 → 회복 시작
            _readyTimestamp[p] = Time.time;
        }

        private void OnResetClicked()
        {
            if (_phase == TestPhase.Boot) return;
            if (_turnLoop != null) StopCoroutine(_turnLoop);

            _buffSystem.ClearAll();
            _modifiers[0].Reset();
            _modifiers[1].Reset();
            _turnNumber = 0;
            for (int i = 0; i < 2; i++)
            {
                _queuedMainSlot[i] = -1;
                _players[i].Temperature.Value = TemperatureSystem.MAX_TEMP;
                _players[i].IsFanActive.Value = false;
                _players[i].IsReady.Value = false;
                _inventories[i].ResetForNewRound();   // 소모품 리필 + 랜덤 슬롯/구간 이력 초기화
            }
            Log("═══ 리셋 — 온도 37°, 소모품 리필, 새 라운드 시작 ═══");
            _turnLoop = StartCoroutine(TurnLoop());
        }

        private ItemContext BuildContext(int userIdx, byte slotIndex, ItemSlotNetData slot)
        {
            int targetIdx = 1 - userIdx;
            return new ItemContext
            {
                User = _players[userIdx],
                Target = _players[targetIdx],
                UserIndex = userIdx,
                TargetIndex = targetIdx,
                UserInventory = _inventories[userIdx],
                TargetInventory = _inventories[targetIdx],
                UserSlot = slot,
                SlotIndex = slotIndex,
                TempSystem = _tempSystem,
                BuffSystem = _buffSystem,
                DropTable = _dropTable,
                AllModifiers = _modifiers,
            };
        }

        #endregion

        #region NV Callbacks / UI Refresh

        private void OnP1TempChanged(float _, float v) => UpdateTemp(0, v);
        private void OnP2TempChanged(float _, float v) => UpdateTemp(1, v);
        private void OnAnySlotChanged(NetworkListEvent<ItemSlotNetData> _) => RefreshSlots();

        private void UpdateTemp(int idx, float value)
        {
            if (_tempTexts[idx] == null) return;
            _tempTexts[idx].text = $"{value:F1}°";
            _tempBars[idx].fillAmount = value / TemperatureSystem.MAX_TEMP;
            // 구간 색상: 37 RED → 30 PINK → 20 SKY → 10 BLUE (GAME_DESIGN 온도 구간)
            _tempBars[idx].color = value > 30f ? new Color(0.9f, 0.25f, 0.2f)
                                 : value > 20f ? new Color(0.95f, 0.55f, 0.65f)
                                 : value > 10f ? new Color(0.5f, 0.8f, 0.95f)
                                 : new Color(0.25f, 0.4f, 0.9f);
        }

        private void RefreshAll()
        {
            if (_players[0] == null) return;
            RefreshSlots();
            for (int i = 0; i < 2; i++)
            {
                UpdateTemp(i, _players[i].Temperature.Value);

                var def = _modifiers[i].ActiveDefense;
                string defense = def.HasValue
                    ? $" | 방어 {(def.Value.BlockAmount >= float.MaxValue ? "100%" : def.Value.BlockAmount + "°")}"
                    : "";
                string state = _phase switch
                {
                    TestPhase.Prep when _players[i].IsReady.Value && _queuedMainSlot[i] >= 0
                        => $"준비 완료 — {_inventories[i].GetItemData(_queuedMainSlot[i]).ItemName} 대기 (회복 중)",
                    TestPhase.Prep when _players[i].IsReady.Value => "준비 완료 — 무행동 (회복 중)",
                    TestPhase.Prep => "선택 중... (선풍기 가동)",
                    TestPhase.Attack => "공격 페이즈",
                    TestPhase.RoundOver => "라운드 종료",
                    _ => "부팅 중",
                };
                _stateTexts[i].text = state + defense;
            }
        }

        private void RefreshSlots()
        {
            if (_registry == null || _inventories[0] == null) return;
            for (int p = 0; p < 2; p++)
            {
                var slots = _inventories[p].SlotStates;
                if (slots == null) return;
                for (int s = 0; s < SHOWN_SLOTS; s++)
                {
                    var label = _slotBtnLabels[p][s];
                    if (s >= slots.Count || slots[s].IsEmpty)
                    {
                        label.text = s < BASIC_SLOTS ? "—" : "(빈 슬롯)";
                        label.color = new Color(1f, 1f, 1f, 0.25f);
                        continue;
                    }
                    var slot = slots[s];
                    string uses = slot.IsUnlimited ? "∞" : slot.RemainingUses.ToString();
                    label.text = $"{_registry[slot.ItemId].ItemName} ({uses})";
                    label.color = slot.IsUsable ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                }
            }
        }

        private void Log(string line)
        {
            _logLines.Enqueue(line);
            while (_logLines.Count > MaxLogLines) _logLines.Dequeue();
            var sb = new StringBuilder();
            foreach (var l in _logLines) sb.AppendLine(l);
            _logText.text = sb.ToString();
        }

        #endregion

        #region UI Construction (런타임 빌드 — 프로젝트 규칙)

        private void BuildUI()
        {
            var canvasGO = new GameObject("ItemTestCanvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<InputSystemUIInputModule>();
            }

            Transform root = canvasGO.transform;

            CreatePanel(root, "BG", Vector2.zero, new Vector2(1700, 950), new Color(0.08f, 0.08f, 0.12f, 0.92f));

            _phaseText = CreateText(root, "PhaseText", new Vector2(0, 430), new Vector2(800, 45), "부팅 중...", 28);

            for (int i = 0; i < 2; i++)
            {
                int p = i;
                float x = i == 0 ? -550f : 550f;

                CreateText(root, $"P{i + 1}Name", new Vector2(x, 380), new Vector2(200, 35), $"P{i + 1}", 26);
                _tempTexts[i] = CreateText(root, $"P{i + 1}Temp", new Vector2(x, 340), new Vector2(200, 35), "37.0°", 28);
                _tempBars[i] = CreateBar(root, $"P{i + 1}Bar", new Vector2(x, 300), new Vector2(340, 24));
                _stateTexts[i] = CreateText(root, $"P{i + 1}State", new Vector2(x, 265), new Vector2(420, 28), "부팅 중", 17);
                _stateTexts[i].color = new Color(0.7f, 0.85f, 1f);

                _slotBtnLabels[i] = new TextMeshProUGUI[SHOWN_SLOTS];

                // 기본 4종 (세로) — 부채/차: Main(선택=턴종료), 바람막이: Main 방어, 고양이: Sub(즉시)
                string[] basicNames = { "부채 (Main)", "바람막이 (Main)", "따뜻한 차 (Main)", "고양이 (Sub)" };
                Color[] basicColors =
                {
                    new(0.75f, 0.25f, 0.2f), new(0.2f, 0.45f, 0.75f),
                    new(0.25f, 0.6f, 0.3f), new(0.6f, 0.4f, 0.7f),
                };
                for (int s = 0; s < BASIC_SLOTS; s++)
                {
                    int slot = s;
                    var btn = CreateButton(root, $"P{i + 1}Slot{s}", new Vector2(x, 205 - s * 58), new Vector2(280, 50),
                        basicNames[s], basicColors[s], out _slotBtnLabels[i][s]);
                    btn.onClick.AddListener(() => OnSlotClicked(p, slot));
                }

                // 랜덤 슬롯 8개 (2열 × 4행) — 시작 4개 + 구간 지급, 최대 8칸 (기획 4×2)
                CreateText(root, $"P{i + 1}RandomLabel", new Vector2(x, -50), new Vector2(340, 26), "랜덤 (시작 4개 + 구간 지급, 최대 8)", 14)
                    .color = new Color(0.6f, 0.6f, 0.6f);
                for (int r = 0; r < SHOWN_RANDOM_SLOTS; r++)
                {
                    int slot = BASIC_SLOTS + r;
                    float rx = x + (r % 2 == 0 ? -72f : 72f);
                    float ry = -85f - (r / 2) * 50f;
                    var btn = CreateButton(root, $"P{i + 1}Slot{slot}", new Vector2(rx, ry), new Vector2(138, 44),
                        "(빈 슬롯)", new Color(0.3f, 0.3f, 0.38f), out _slotBtnLabels[i][slot]);
                    btn.onClick.AddListener(() => OnSlotClicked(p, slot));
                    _slotBtnLabels[i][slot].fontSize = 14;
                }

                // 준비 끝 (Main 없이 턴 종료)
                var readyBtn = CreateButton(root, $"P{i + 1}Ready", new Vector2(x, -300), new Vector2(280, 52),
                    "준비 끝", new Color(0.7f, 0.55f, 0.2f), out _);
                readyBtn.onClick.AddListener(() => OnReadyClicked(p));
            }

            // 중앙: 로그 + 리셋
            CreatePanel(root, "LogBG", new Vector2(0, 40), new Vector2(620, 500), new Color(0f, 0f, 0f, 0.45f));
            _logText = CreateText(root, "LogText", new Vector2(0, 40), new Vector2(590, 480), "부팅 중...", 16);
            _logText.alignment = TextAlignmentOptions.TopLeft;

            var resetBtn = CreateButton(root, "ResetBtn", new Vector2(0, -300), new Vector2(200, 52),
                "리셋", new Color(0.5f, 0.3f, 0.3f), out _);
            resetBtn.onClick.AddListener(OnResetClicked);

            CreateText(root, "Help", new Vector2(0, -420), new Vector2(1500, 60),
                "준비 페이즈: 선풍기 자동 ON(-1°/s) → Main 선택 또는 [준비 끝] = 내 선풍기 정지+회복(+1°/s) → 양쪽 완료 시 공격 페이즈 일괄 처리 | 30/20/10° 통과 시 랜덤 지급 → 고양이(Sub)로 상대 리롤", 15)
                .color = new Color(0.65f, 0.65f, 0.65f);
        }

        // TMP 기본 폰트(LiberationSans)에 한글 글리프가 없어 □로 표시됨
        // → OS 폰트 파일에서 직접 로드해 동적 TMP 폰트 생성 (에셋/재배포 불필요)
        private static TMP_FontAsset _koreanFont;
        private static bool _koreanFontTried;

        private static TMP_FontAsset GetKoreanFont()
        {
            if (_koreanFont != null) return _koreanFont;      // 살아있는 캐시만 재사용
            if (!ReferenceEquals(_koreanFont, null))          // 파괴된 캐시(fake null) → 정리 후 재생성
            {
                _koreanFont = null;
                _koreanFontTried = false;
            }
            if (_koreanFontTried) return null;                // OS 폰트 자체가 없는 환경 — 재시도 안 함
            _koreanFontTried = true;

            // CreateDynamicFontFromOSFont 경유는 이 환경에서 실패 (Editor.log 확인)
            // → OS 폰트 "파일 경로"에서 직접 로드하는 방식 사용 (맑은 고딕 우선)
            string[] keywords = { "malgun.ttf", "malgun", "nanumgothic", "gulim", "batang" };
            var paths = Font.GetPathsToOSFonts();
            foreach (var keyword in keywords)
            {
                foreach (var path in paths)
                {
                    if (!System.IO.Path.GetFileName(path).ToLowerInvariant().Contains(keyword)) continue;
                    var font = new Font(path);
                    var asset = TMP_FontAsset.CreateFontAsset(font);
                    if (asset != null)
                    {
                        _koreanFont = asset;
                        return _koreanFont;
                    }
                }
            }
            Debug.LogWarning("[ItemSystemTestDriver] 한글 OS 폰트를 찾지 못함 — TMP 기본 폰트 사용 (한글 □ 표시)");
            return null;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name,
            Vector2 pos, Vector2 size, string text, int fontSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            var koreanFont = GetKoreanFont();
            if (koreanFont != null) tmp.font = koreanFont;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        private static Image CreatePanel(Transform parent, string name,
            Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static Image CreateBar(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            CreatePanel(parent, name + "BG", pos, size, new Color(0.15f, 0.15f, 0.2f));
            var fill = CreatePanel(parent, name, pos, size, new Color(0.9f, 0.25f, 0.2f));
            // 스프라이트 없는 Image는 Filled 타입에서 fillAmount가 무시됨 → 흰색 스프라이트 지정
            var tex = Texture2D.whiteTexture;
            fill.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 1f;
            return fill;
        }

        private static Button CreateButton(Transform parent, string name,
            Vector2 pos, Vector2 size, string label, Color color, out TextMeshProUGUI labelText)
        {
            var img = CreatePanel(parent, name, pos, size, color);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            labelText = CreateText(img.transform, "Label", Vector2.zero, size, label, 19);
            return btn;
        }

        #endregion
    }
}
