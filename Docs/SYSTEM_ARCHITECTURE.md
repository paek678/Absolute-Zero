# System Architecture — Absolute Zero

> Detailed technical spec. Server-authoritative. Unity 6 / NGO 2.11.2 / URP.
> Demo target: 4 basic items, core turn loop, temperature system.
> Last updated: 2026-07-14 (rev3 — 13 fixes + 5 cross-ref fixes)

### Applied Fixes (rev2)

| FIX | Issue | Severity | Solution |
|-----|-------|----------|----------|
| **01** | `ItemContext` struct에 `ref` 필드 → 컴파일 에러 | Critical | class로 변경 + `PlayerModifiers[]` 배열 인덱스 패턴 |
| **02** | `NetworkList` 초기화 누락 → NRE | Critical | `Awake()`에서 초기화 |
| **03** | `MaxUses=-1` → `byte` 캐스트 → 영구 아이템 1회 소모 | Critical | `255` = unlimited sentinel, `IsUsable` 통합 검증 |
| **04** | `PrepTimer` 매 프레임 NV 갱신 → 네트워크 폭주 | Critical | `PrepStartServerTime` + `PrepDuration` 1회 전송, 클라이언트 로컬 계산 |
| **05** | `_envSystem` 데모에서 null → NRE | Critical | null 체크 추가 (데모에서 환경 시스템 없음) |
| **06** | `SendTo.Owner` on MiniGameManager → Host에게 전송됨 | Critical | `SendTo.SpecifiedInParams` + `RpcTarget.Single` |
| **07** | TemperatureSystem이 PlayerState private 필드 접근 | High | `ApplyDamage`에 `DefenseInfo?` 파라미터 추가 |
| **08** | 방어 ApplyDefense + ExecuteEffect 이중 적용 | High | ApplyDefense에서만 설정, DefenseItem은 CombatResolver에서 스킵 |
| **09** | `RoundWins`/`readyTimestamp` 중복 저장 → 동기화 불일치 | High | 단일 소유: RoundWins→MatchManager, readyTimestamp→ActionQueue |
| **10** | `CombatEvent.ResultTemp` 회복 아이템에서 잘못된 대상 | High | `UserResultTemp`/`TargetResultTemp` 분리 |
| **11** | `ItemSlotNetData.Empty` 미정의 → 컴파일 에러 | Medium | static property 추가 |
| **12** | 라운드 간 소모품 리셋 없음 | Medium | `ResetForNewRound()` 추가 |
| **13** | RecoveryItemDataSO useIndex 순서 의존 | Medium | ConsumeItem 호출 순서 주석 명시 |

**Rev3 교차 참조 수정:**

| FIX | Issue | Solution |
|-----|-------|----------|
| **R3-A** | `AttackPhaseRoutine`이 `Resolve()`를 옛 시그니처로 호출 | `_modifiers` 배열 전달로 수정 |
| **R3-B** | `PlayerState._actionQueue` private → TurnManager 접근 불가 | `GetActionQueue()` public 접근자 추가 |
| **R3-C** | `ActionQueue` 필드 private → CombatResolver 접근 불가 | 필드를 public으로 변경 |
| **R3-D** | `BuildContext()`, `GetItemData()` 호출되지만 미정의 | 두 메서드 본문 추가 |
| **R3-E** | `CombatResult`, `CombatEvent`, `EffectType` 등 타입 미정의 | Section 4.7 추가 |

---

## 0. Server Authority Rules

**절대 원칙: 클라이언트는 입력만 보내고, 서버가 모든 상태를 변경한다.**

| Rule | Description |
|------|-------------|
| **R1** | 모든 게임 상태 변경(온도, 아이템, 턴, 승패)은 서버에서만 실행 |
| **R2** | 클라이언트는 ServerRpc로 입력만 전송 (슬롯 인덱스, Ready, 미니게임 결과) |
| **R3** | 서버는 모든 입력을 검증 후 적용 (유효 슬롯? 사용 가능? 올바른 페이즈?) |
| **R4** | 클라이언트 UI는 NetworkVariable 콜백과 ClientRpc로만 갱신 |
| **R5** | 미니게임도 서버가 최종 판정 (클라이언트 결과를 서버가 bounds check) |

```
CLIENT (Input Only)                    SERVER (All Mutations)
──────────────────                    ─────────────────────
Click item slot ──[ServerRpc]───→    Validate slot index
                                     Check: correct phase? item exists? usable?
                                     Check: Main or Sub? turn state allows?
                                     ├── INVALID → reject (no response)
                                     └── VALID → execute effect
                                          ├── Modify PlayerState
                                          ├── Update NetworkVariables
                                          └── Broadcast [ClientRpc]
                  ←──[ClientRpc]───       Visual/audio feedback
                  ←──[NV callback]──      State change (temp, phase)
```

### Authority Map (모든 시스템)

| System | Runs On | Reason |
|--------|---------|--------|
| TurnManager | **Server only** | 턴 전환은 서버만 결정 |
| TemperatureSystem | **Server only** | 온도 계산은 서버만 실행 |
| CombatResolver | **Server only** | 전투 판정은 서버만 |
| ItemManager | **Server only** | 아이템 검증/소모/지급 |
| BuffDebuffSystem | **Server only** | 버프/디버프 적용 |
| MiniGameManager | **Server validates** | 클라이언트가 플레이, 서버가 결과 검증 |
| EnvironmentSystem | **Server only** | 환경 선택/적용 |
| MatchManager | **Server only** | 라운드/매치 승패 판정 |
| CameraManager | **Client only** | 로컬 카메라 연출 |
| AudioManager | **Client only** | 로컬 오디오 재생 |
| VFXManager | **Client only** | 로컬 이펙트 재생 |
| LightManager | **Client only** | 로컬 조명 제어 |
| GameUI | **Client only** | NV 콜백/ClientRpc로 갱신 |

---

## 1. Network Protocol

### 1.1 NetworkObject 배치

```
[LobbyScene — Build Index 0]
├── NetworkManager (Unity built-in, DDOL)
│   └── UnityTransport (DTLS via Relay)
└── Managers (DDOL GameObject)
    ├── LobbyManager
    ├── RelayManager
    ├── SessionManager
    └── PlayerSpawnManager

[GameScene — Build Index 1, loaded via NetworkSceneManager]
├── GameManager (NetworkObject, scene-placed)
│   ├── TurnManager (NetworkBehaviour)
│   ├── MatchManager (NetworkBehaviour)
│   ├── ItemManager (NetworkBehaviour)
│   └── MiniGameManager (NetworkBehaviour)
├── EnvironmentController (NetworkObject, scene-placed)
│   └── EnvironmentSystem (NetworkBehaviour)
├── Player Prefab × 2 (NetworkObject, spawned)
│   ├── PlayerState (NetworkBehaviour)
│   ├── PlayerInventory (NetworkBehaviour)
│   └── PlayerVisual (NetworkBehaviour)
└── Presentation (non-networked, local only)
    ├── CameraManager
    ├── AudioManager
    ├── VFXManager
    ├── LightManager
    └── ObjectPoolManager
```

### 1.2 NetworkVariables (서버 → 클라이언트 자동 동기화)

> 모든 NV는 `NetworkVariableReadPermission.Everyone`, `NetworkVariableWritePermission.Server`

**TurnManager:**

| NetworkVariable | Type | Purpose | 갱신 시점 |
|-----------------|------|---------|-----------|
| `CurrentPhase` | `NetworkVariable<TurnPhase>` | 현재 턴 페이즈 | 페이즈 전환 시 |
| `TurnNumber` | `NetworkVariable<int>` | 현재 턴 번호 (라운드 내) | 턴 시작 시 |
| `PrepStartServerTime` | `NetworkVariable<double>` | Prep 시작 시 서버 Time | PrepPhase 시작 시 1회 |
| `PrepDuration` | `NetworkVariable<float>` | 준비 시간 길이 (20s/10s) | PrepPhase 시작 시 1회 |

> **FIX-04: `PrepTimer`를 매 프레임 NV 갱신 → `PrepStartServerTime` + `PrepDuration` 1회 전송으로 변경.**
> 클라이언트가 `NetworkManager.ServerTime.Time - PrepStartServerTime`으로 로컬 계산.
> 매 프레임 NV 갱신은 60Hz × 4byte = 초당 240byte/클라이언트 — 턴 게임에 과도.

```csharp
enum TurnPhase : byte
{
    WaitingForPlayers = 0,
    PrepPhase = 1,
    AttackPhase = 2,
    ResolutionPhase = 3,
    RoundOver = 4
}
```

**MatchManager:**

| NetworkVariable | Type | Purpose |
|-----------------|------|---------|
| `RoundNumber` | `NetworkVariable<int>` | 현재 라운드 (1~3) |
| `P1RoundWins` | `NetworkVariable<int>` | P1 라운드 승리 수 |
| `P2RoundWins` | `NetworkVariable<int>` | P2 라운드 승리 수 |
| `MatchState` | `NetworkVariable<MatchState>` | 매치 상태 |

```csharp
enum MatchState : byte { WaitingToStart, RoundInProgress, RoundEnd, MatchComplete }
```

**PlayerState (Player Prefab, 플레이어당 1개):**

| NetworkVariable | Type | Purpose | 갱신 시점 |
|-----------------|------|---------|-----------|
| `Temperature` | `NetworkVariable<float>` | 현재 온도 (0~37) | 매 프레임 (팬/회복) + 아이템 사용 시 |
| `FanSpeed` | `NetworkVariable<float>` | 선풍기 속도 (기본 1, 드라이버 시 2) | 십자드라이버 효과 시 |
| `IsReady` | `NetworkVariable<bool>` | 준비 완료 여부 | Ready/Main 선택 시 |
| `IsFanActive` | `NetworkVariable<bool>` | 선풍기 가동 중 여부 | 페이즈 전환 시 |

> FIX-09: `RoundWins` 제거 — `MatchManager.P1RoundWins`/`P2RoundWins`에서만 관리 (중복 방지)

**PlayerInventory (Player Prefab, 플레이어당 1개):**

| NetworkVariable | Type | Purpose |
|-----------------|------|---------|
| `SlotStates` | `NetworkList<ItemSlotNetData>` | 12 슬롯 상태 (4 basic + 8 random) |

```csharp
struct ItemSlotNetData : IEquatable<ItemSlotNetData>, INetworkSerializable
{
    public short ItemId;          // -1 = empty, else ItemRegistry index
    public byte RemainingUses;    // 0 = consumed/empty, 255 = unlimited (Permanent)
    public byte Flags;            // bit 0: blocked by BlueTape, bit 1: is Sub type

    // FIX-11: Empty 정적 프로퍼티 정의 (FindEmptyRandomSlot에서 사용)
    public static ItemSlotNetData Empty => new() { ItemId = -1, RemainingUses = 0, Flags = 0 };

    // FIX-03: 영구 아이템 체크 (RemainingUses == 255 = unlimited)
    public bool IsUnlimited => RemainingUses == 255;
    public bool IsEmpty => ItemId == -1;
    public bool IsUsable => !IsEmpty && (IsUnlimited || RemainingUses > 0) && !IsBlocked;
    public bool IsBlocked => (Flags & 1) != 0;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ItemId);
        serializer.SerializeValue(ref RemainingUses);
        serializer.SerializeValue(ref Flags);
    }

    public bool Equals(ItemSlotNetData other)
        => ItemId == other.ItemId && RemainingUses == other.RemainingUses && Flags == other.Flags;
}
```

### 1.3 Server RPCs (Client → Server)

> 클라이언트가 서버에 입력을 보내는 유일한 경로. 모든 RPC에서 서버가 검증 후 처리.

```csharp
// PlayerState.cs에 위치 — Owner만 호출 가능

/// Sub 아이템 사용 요청. 서버가 검증 후 즉시 효과 적용.
[Rpc(SendTo.Server)]
void UseSubItemServerRpc(byte slotIndex)
{
    // 검증: Phase == Prep? IsReady == false? slot valid? item is Sub type? item usable?
    // 통과 → execute Sub effect
    // 실패 → 무시 (또는 reject ClientRpc)
}

/// Main 아이템 선택 요청. 서버가 검증 후 액션 큐잉 + 자동 Ready.
[Rpc(SendTo.Server)]
void SelectMainItemServerRpc(byte slotIndex)
{
    // 검증: Phase == Prep? IsReady == false? slot valid? item is Main type? item usable?
    // 통과 → queue Main action + set IsReady = true + record timestamp + fan OFF
    // 실패 → 무시
}

/// Main 없이 Ready 버튼. Sub 효과만으로 턴 종료.
[Rpc(SendTo.Server)]
void PressReadyServerRpc()
{
    // 검증: Phase == Prep? IsReady == false?
    // 통과 → set IsReady = true + record timestamp + fan OFF
}

/// 미니게임 결과 제출.
[Rpc(SendTo.Server)]
void SubmitMiniGameServerRpc(MiniGameResultData result)
{
    // 서버가 결과 검증 (시간 내? 입력 횟수 합리적?)
    // 통과 → 아이템 효과 적용
    // 실패 → 아이템 파괴, 클라이언트에 재선택 알림
}
```

**서버 검증 체크리스트 (모든 ServerRpc 공통):**
```
1. Phase 확인: CurrentPhase == TurnPhase.PrepPhase
2. Ready 확인: IsReady.Value == false (이미 Ready면 거부)
3. 슬롯 유효성: slotIndex < inventory.Count && slot.ItemId != -1
4. 아이템 사용 가능: slot.RemainingUses > 0 && !blocked
5. 타입 확인: Sub RPC → item.SlotType == Sub / Main RPC → item.SlotType == Main
6. 소유권: RPC 발신자 == 이 PlayerState의 Owner
```

### 1.4 Client RPCs (Server → Client)

> 서버가 클라이언트에 이벤트/결과를 알리는 경로.

```csharp
// TurnManager.cs
[Rpc(SendTo.Everyone)]
void OnPhaseChangedClientRpc(TurnPhase newPhase, int turnNumber)
// → UI 전환, 카메라 상태 변경, 오디오 전환

// CombatResolver 결과 → TurnManager가 broadcast
[Rpc(SendTo.Everyone)]
void OnCombatResultClientRpc(CombatResultData result)
// → 공격 애니메이션, 데미지 표시, VFX 재생

// MatchManager.cs
[Rpc(SendTo.Everyone)]
void OnRoundEndClientRpc(int winnerPlayerIndex, int roundNumber)
// → 라운드 결과 UI, 동결/파쇄 연출

[Rpc(SendTo.Everyone)]
void OnMatchEndClientRpc(int winnerPlayerIndex)
// → 최종 결과 UI

// ItemManager.cs
[Rpc(SendTo.Everyone)]
void OnSubItemUsedClientRpc(int playerIndex, byte slotIndex, byte effectType)
// → Sub 아이템 사용 이펙트 재생

[Rpc(SendTo.Everyone)]
void OnItemGrantedClientRpc(int playerIndex, ItemSlotNetData newSlot, byte slotIndex)
// → 아이템 획득 이펙트

// EnvironmentSystem.cs
[Rpc(SendTo.Everyone)]
void OnEnvironmentRevealClientRpc(byte environmentType)
// → 환경 변수 공개 연출

// MiniGameManager.cs — 특정 플레이어에게만 전송
// FIX-06: SendTo.Owner는 이 NetworkObject의 Owner에게 전송 → MiniGameManager는
// GameManager 하위이므로 Owner = Host. 특정 플레이어에게 보내려면
// SendTo.SpecifiedInParams + RpcParams.Send.Target 사용
[Rpc(SendTo.SpecifiedInParams)]
void OnMiniGameStartClientRpc(byte miniGameType, float timeLimit, RpcParams rpcParams)

[Rpc(SendTo.SpecifiedInParams)]
void OnMiniGameResultClientRpc(bool success, RpcParams rpcParams)
// 호출 시: OnMiniGameStartClientRpc(type, time,
//   RpcTarget.Single(targetClientId, RpcTargetUse.Temp))
```

### 1.5 동기화 흐름도

```
                    SERVER                              CLIENT
                    ══════                              ══════

NetworkVariable 자동 동기화 (지속적 상태):
  Temperature ─────────────────────────────→  OnTemperatureChanged callback
  PrepStartServerTime + PrepDuration ──────→  클라이언트 로컬 타이머 계산
  CurrentPhase ────────────────────────────→  OnPhaseChanged callback
  IsReady ─────────────────────────────────→  UI ready 표시
  IsFanActive ─────────────────────────────→  팬 애니메이션 on/off
  SlotStates (NetworkList) ────────────────→  인벤토리 UI 갱신

ServerRpc (클라이언트 입력):
  UseSubItemServerRpc(slot) ←──────────────  Sub 아이템 클릭
  SelectMainItemServerRpc(slot) ←──────────  Main 아이템 클릭
  PressReadyServerRpc() ←─────────────────  준비 버튼 클릭
  SubmitMiniGameServerRpc(result) ←────────  미니게임 완료

ClientRpc (서버 이벤트):
  OnCombatResultClientRpc ─────────────────→  전투 결과 연출
  OnSubItemUsedClientRpc ──────────────────→  Sub 효과 연출
  OnRoundEndClientRpc ─────────────────────→  라운드 결과
  OnEnvironmentRevealClientRpc ────────────→  환경 공개
```

---

## 2. PlayerState (NetworkBehaviour, Player Prefab)

### 2.1 전체 필드

```csharp
public class PlayerState : NetworkBehaviour
{
    // ═══ NetworkVariables (서버 write, 클라이언트 read) ═══
    public NetworkVariable<float> Temperature = new(37f);
    public NetworkVariable<float> FanSpeed = new(1f);
    public NetworkVariable<bool> IsReady = new(false);
    public NetworkVariable<bool> IsFanActive = new(false);
    // FIX-09: RoundWins는 MatchManager에만 존재 (중복 제거)
    // PlayerState는 개별 플레이어 턴 상태만 관리

    // ═══ Server-local (동기화 안함, 서버만 사용) ═══
    ActionQueue _actionQueue = new();

    // ═══ References ═══
    PlayerInventory _inventory;
    int _playerIndex;                // 0 or 1 (서버가 spawn 시 할당)
    public int PlayerIndex => _playerIndex;

    // ═══ FIX-REV3: TurnManager/CombatResolver에서 접근 필요한 내부 상태 ═══
    public ActionQueue GetActionQueue() => _actionQueue;
    public PlayerInventory GetInventory() => _inventory;

    /// ServerRpc에서 ItemContext 생성 시 사용
    /// TurnManager._modifiers 배열 참조 필요 → TurnManager에서 주입
    ItemContext BuildContext()
    {
        var tm = TurnManager.Instance;
        var opponent = _playerIndex == 0 ? tm.GetPlayer(1) : tm.GetPlayer(0);
        return new ItemContext
        {
            User = this, Target = opponent,
            UserIndex = _playerIndex, TargetIndex = opponent.PlayerIndex,
            UserInventory = _inventory, TargetInventory = opponent.GetInventory(),
            AllModifiers = tm.GetModifiers(),
            TempSystem = tm.GetTempSystem(),
            BuffSystem = tm.GetBuffSystem(),
            DropTable = tm.GetDropTable(),
        };
    }
}
```

### 2.2 ActionQueue (서버 전용)

```csharp
class ActionQueue
{
    // FIX-REV3: CombatResolver.DetermineOrder에서 접근 필요 → public
    public List<QueuedAction> subActions = new();
    public QueuedAction? mainAction = null;
    public float readyTimestamp;
    public bool isReady;

    // Sub 추가 — 여러 번 호출 가능
    void AddSub(byte slotIndex, ItemDataSO itemData)
    {
        subActions.Add(new QueuedAction(slotIndex, itemData));
    }

    // Main 설정 — 1번만, 이후 isReady = true
    void SetMain(byte slotIndex, ItemDataSO itemData, float timestamp)
    {
        mainAction = new QueuedAction(slotIndex, itemData);
        readyTimestamp = timestamp;
        isReady = true;
    }

    // Ready (Main 없이)
    void SetReadyNoMain(float timestamp)
    {
        readyTimestamp = timestamp;
        isReady = true;
    }

    void Clear()
    {
        subActions.Clear();
        mainAction = null;
        isReady = false;
    }
}

struct QueuedAction
{
    public byte SlotIndex;
    public ItemDataSO ItemData;
}
```

### 2.3 PlayerModifiers (서버 전용, 턴마다 리셋)

```csharp
struct PlayerModifiers
{
    public bool BasicItemsBlocked;      // 청테이프 효과: 기본 아이템 사용 불가
    public bool ActionNeutralized;      // 레드카드 효과: Main 행동 무효화
    public DefenseInfo? ActiveDefense;  // 바람막이/마스크 방어 활성
    public bool HasExtraAction;         // 타로카드 효과: 추가 행동 가능
    public bool OpponentRevealed;       // 타로카드 효과: 상대 선택 보임

    public void Reset()
    {
        BasicItemsBlocked = false;
        ActionNeutralized = false;
        ActiveDefense = null;
        HasExtraAction = false;
        OpponentRevealed = false;
    }
}

struct DefenseInfo
{
    public DamageFilter Filter;    // Temperature, Food
    public float BlockAmount;      // 4 (Windbreaker), float.MaxValue (Mask)
}

enum DamageFilter : byte { Temperature, Food, All }
```

---

## 3. Temperature System (서버 전용 C# 클래스)

### 3.1 핵심 로직

```csharp
class TemperatureSystem
{
    public const float MAX_TEMP = 37f;
    public const float MIN_TEMP = 0f;
    public const float DEFAULT_FAN_SPEED = 1f;
    public const float DEFAULT_RECOVERY_RATE = 1f;

    // ── 매 프레임 서버에서 호출 ──

    /// PrepPhase 중 선풍기 가동: 온도 감소
    void TickFan(PlayerState player, float deltaTime)
    {
        if (!player.IsFanActive.Value) return;

        float decrease = player.FanSpeed.Value * deltaTime;
        float newTemp = Mathf.Max(MIN_TEMP, player.Temperature.Value - decrease);
        player.Temperature.Value = newTemp;
    }

    /// Ready 후 회복: 온도 증가
    void TickRecovery(PlayerState player, float deltaTime, float recoveryRate)
    {
        if (player.IsFanActive.Value) return;  // 팬 가동 중이면 회복 안함
        if (!player.IsReady.Value) return;      // Ready 안 했으면 회복 안함

        float increase = recoveryRate * deltaTime;
        float newTemp = Mathf.Min(MAX_TEMP, player.Temperature.Value + increase);
        // ※ MAX_TEMP 캡 여부는 Q9 대기 — 현재 37° 캡 적용
        player.Temperature.Value = newTemp;
    }

    // ── 아이템 효과에 의한 즉시 변경 ──

    /// 데미지 적용 (방어 계산 포함)
    /// FIX-07: PlayerState._modifiers는 private → 방어 정보를 파라미터로 전달
    /// FIX-08: 방어 판정은 여기서만 수행 (CombatResolver.ApplyDefense는 설정만)
    float ApplyDamage(PlayerState target, float rawDamage, DamageFilter attackFilter,
                       DefenseInfo? activeDefense)
    {
        float actualDamage = rawDamage;

        if (activeDefense.HasValue)
        {
            var defense = activeDefense.Value;
            if (defense.Filter == attackFilter || defense.Filter == DamageFilter.All)
            {
                actualDamage = Mathf.Max(0f, rawDamage - defense.BlockAmount);
            }
        }

        target.Temperature.Value = Mathf.Max(MIN_TEMP, target.Temperature.Value - actualDamage);
        return actualDamage;
    }

    /// 힐 적용
    void ApplyHeal(PlayerState target, float amount)
    {
        target.Temperature.Value = Mathf.Min(MAX_TEMP, target.Temperature.Value + amount);
    }

    /// 사망 체크
    bool IsDead(PlayerState player) => player.Temperature.Value <= MIN_TEMP;

    // ── 구간 판정 ──

    static readonly float[] THRESHOLDS = { 30f, 20f, 10f };
    static readonly int[] GRANTS = { 1, 2, 3 };

    /// 구간 통과 시 아이템 지급 (서버 전용)
    void CheckThresholds(PlayerState player, PlayerInventory inventory,
                         bool[] thresholdGranted, ItemDropTable dropTable)
    {
        for (int i = 0; i < THRESHOLDS.Length; i++)
        {
            if (!thresholdGranted[i] && player.Temperature.Value <= THRESHOLDS[i])
            {
                thresholdGranted[i] = true;
                inventory.GrantRandomItems(GRANTS[i], dropTable);
            }
        }
    }
}
```

---

## 4. Item System

### 4.1 ItemDataSO (ScriptableObject 기반 클래스)

```csharp
// ═══ Base Class ═══
public abstract class ItemDataSO : ScriptableObject
{
    [Header("Basic Info")]
    public string ItemName;            // "부채", "바람막이" 등
    public string Description;         // 한글 설명
    public Sprite Icon;                // 2D 스프라이트

    [Header("Classification")]
    public ItemCategory Category;      // ATK, DEF, REC, BUF, DBF, SAB, SPC
    public ItemSlotType SlotType;      // Main, Sub
    public ItemPersistence Persistence; // Permanent, BasicConsumable, RandomConsumable

    [Header("Usage")]
    public int MaxUses = 1;            // -1 = 무한 (영구), 1 = 1회, 3 = 스마트폰

    [Header("Drop")]
    public float DropWeight;           // 랜덤 풀 가중치 (기본 아이템은 0)

    [Header("Mini-Game")]
    public bool RequiresMiniGame;
    public MiniGameType MiniGameType;
    public float MiniGameTimeLimit;
    public string MiniGameDescription;

    // 서버 전용: 효과 실행
    public abstract void ExecuteEffect(ItemContext ctx);

    // 서버 전용: 사용 가능 여부 검증
    public virtual bool CanUse(ItemContext ctx)
    {
        if (ctx.UserModifiers.BasicItemsBlocked && Persistence != ItemPersistence.RandomConsumable)
            return false;  // 기본 아이템 봉쇄 중
        return true;
    }
}

// ═══ Concrete Subclasses ═══

public class AttackItemDataSO : ItemDataSO
{
    public float Damage;                        // 3, 4, 5, 7
    public DamageFilter AttackFilter = DamageFilter.Temperature;

    public override void ExecuteEffect(ItemContext ctx)
    {
        // FIX-07: 방어 정보를 TargetModifiers에서 전달
        ctx.TempSystem.ApplyDamage(ctx.Target, Damage, AttackFilter,
                                    ctx.TargetModifiers.ActiveDefense);
    }
}

public class DefenseItemDataSO : ItemDataSO
{
    public float BlockAmount;                   // 4 (Windbreaker), float.Max (Mask)
    public DamageFilter Filter;                 // Temperature, Food

    public override void ExecuteEffect(ItemContext ctx)
    {
        ctx.UserModifiers.ActiveDefense = new DefenseInfo
        {
            Filter = this.Filter,
            BlockAmount = this.BlockAmount
        };
    }
}

public class RecoveryItemDataSO : ItemDataSO
{
    public float[] HealPerUse;                  // [7], [5], [10], [3,5,7]

    public override void ExecuteEffect(ItemContext ctx)
    {
        // FIX-13: ConsumeItem이 ExecuteEffect 이후 호출되므로
        // RemainingUses는 아직 감소 전 상태 → (MaxUses - Remaining) = 이전 사용 횟수
        // 예: 스마트폰 MaxUses=3, 첫 사용 시 Remaining=3 → useIndex=0 ✓
        //     두 번째 사용 시 Remaining=2 → useIndex=1 ✓
        int useIndex = MaxUses - ctx.UserSlot.RemainingUses;
        float heal = HealPerUse[Mathf.Min(useIndex, HealPerUse.Length - 1)];
        ctx.TempSystem.ApplyHeal(ctx.User, heal);
    }
    // ※ ConsumeItem은 반드시 ExecuteEffect 이후 호출해야 useIndex 정확
}

public class SabotageItemDataSO : ItemDataSO
{
    public SabotageType SabotageType;

    public override void ExecuteEffect(ItemContext ctx)
    {
        switch (SabotageType)
        {
            case SabotageType.Reroll:
                ctx.TargetInventory.RerollAllRandom(ctx.DropTable);
                break;
            case SabotageType.Steal:
                ctx.TargetInventory.StealRandomItem(ctx.UserInventory);
                break;
            case SabotageType.BlockBasic:
                ctx.TargetModifiers.BasicItemsBlocked = true; // 다음 턴 적용
                break;
            case SabotageType.Neutralize:
                ctx.TargetModifiers.ActionNeutralized = true;
                break;
        }
    }
}
```

### 4.2 ItemContext (서버 전용 — 효과 실행 시 전달)

> **FIX-01: struct에서 ref 필드 사용 불가 → class로 변경.**
> C# struct는 ref 필드를 가질 수 없다 (ref struct는 힙 할당/컬렉션 저장 불가).
> PlayerModifiers를 직접 참조하기 위해 class + 배열 인덱스 패턴 사용.

```csharp
class ItemContext
{
    // 플레이어 참조
    public PlayerState User;
    public PlayerState Target;
    public PlayerInventory UserInventory;
    public PlayerInventory TargetInventory;

    // Modifiers는 TurnManager가 보유한 배열을 통해 접근
    // PlayerModifiers[]에 [0]=P1, [1]=P2로 저장
    public int UserIndex;           // _modifiers[] 접근용
    public int TargetIndex;

    // 슬롯 정보
    public ItemSlotNetData UserSlot;
    public byte SlotIndex;

    // 시스템 참조
    public TemperatureSystem TempSystem;
    public BuffDebuffSystem BuffSystem;
    public ItemDropTable DropTable;
    public EnvironmentType ActiveEnvironment;
    public PlayerModifiers[] AllModifiers;  // [0]=P1, [1]=P2

    // 편의 접근자
    public ref PlayerModifiers UserModifiers => ref AllModifiers[UserIndex];
    public ref PlayerModifiers TargetModifiers => ref AllModifiers[TargetIndex];
}
```

### 4.3 Main vs Sub 분류

| 분류 | 행동 | 턴당 수량 | 타이밍 |
|------|------|-----------|--------|
| **Main** | 선택 즉시 턴 종료 (= 자동 Ready) | 최대 1개 | Sub 이후, 또는 단독 |
| **Sub** | 즉시 실행, 턴 유지 | 제한 없음 | Main 이전에만 |

**Action 순서 규칙:**
```
[허용] Sub → Sub → Sub → Main → END
[허용] Sub → Sub → Ready (no Main) → END
[허용] Main → END (Sub 없이 바로)
[금지] Main → Sub (Main 선택 = 턴 종료, 더 이상 행동 불가)
```

**Main 아이템 = "준비 끝" 대체:**
Main 아이템을 선택하면 자동으로 Ready 처리됨.
- 서버: `IsReady.Value = true`, `IsFanActive.Value = false`, `readyTimestamp` 기록
- 별도 Ready 버튼은 Main 없이 Sub만 사용할 때 누름

### 4.4 ItemInventory (NetworkBehaviour, Player Prefab)

```csharp
public class PlayerInventory : NetworkBehaviour
{
    // ═══ 동기화 ═══
    // FIX-02: NetworkList는 Awake()에서 반드시 초기화해야 함
    // 필드 선언 시 초기화 또는 Awake() 중 택1 — Spawn 이후 초기화하면 NRE
    public NetworkList<ItemSlotNetData> SlotStates;

    void Awake()
    {
        SlotStates = new NetworkList<ItemSlotNetData>();
    }

    // ═══ Server-local ═══
    const int BASIC_SLOT_COUNT = 4;
    const int MAX_RANDOM_SLOTS = 8;
    const int MAX_SLOTS = BASIC_SLOT_COUNT + MAX_RANDOM_SLOTS;

    // FIX-16: _itemRegistry는 ItemManager에서 주입 (OnNetworkSpawn 시)
    ItemDataSO[] _itemRegistry;
    bool[] _thresholdGranted = new bool[3];  // 30/20/10 지급 이력

    public void Initialize(ItemDataSO[] registry)
    {
        _itemRegistry = registry;
    }

    /// FIX-REV3: ServerRpc에서 SO 데이터 접근 시 사용
    public ItemDataSO GetItemData(int slotIndex)
    {
        return _itemRegistry[SlotStates[slotIndex].ItemId];
    }

    // ═══ Server Methods ═══

    /// 초기화: 4개 기본 아이템 세팅
    public void InitializeBasicItems(ItemDataSO fan, ItemDataSO windbreaker,
                                      ItemDataSO warmTea, ItemDataSO cat)
    {
        SlotStates.Add(MakeSlot(fan));          // slot 0: Fan
        SlotStates.Add(MakeSlot(windbreaker));  // slot 1: Windbreaker
        SlotStates.Add(MakeSlot(warmTea));      // slot 2: Warm Tea
        SlotStates.Add(MakeSlot(cat));          // slot 3: Cat
        // slot 4~11: empty (random slots)
    }

    /// 아이템 소모 (서버 전용)
    /// FIX-03: Permanent(RemainingUses==255) 아이템은 소모하지 않음
    public void ConsumeItem(byte slotIndex)
    {
        var slot = SlotStates[slotIndex];
        if (slot.IsUnlimited) return;  // Permanent → 소모 안 함

        slot.RemainingUses--;

        if (slot.RemainingUses <= 0)
        {
            var item = _itemRegistry[slot.ItemId];
            if (item.Persistence == ItemPersistence.RandomConsumable)
            {
                slot.ItemId = -1;  // 슬롯에서 완전 제거
            }
            // BasicConsumable: ItemId 유지, RemainingUses=0 → UI에서 회색 처리
            slot.RemainingUses = 0;
        }

        SlotStates[slotIndex] = slot;  // NetworkList 갱신 트리거
    }

    /// 랜덤 아이템 지급 (서버 전용)
    public void GrantRandomItems(int count, ItemDropTable table)
    {
        for (int i = 0; i < count; i++)
        {
            int emptySlot = FindEmptyRandomSlot();
            if (emptySlot == -1) break;  // 슬롯 풀

            ItemDataSO item = table.Roll();
            SlotStates[emptySlot] = MakeSlot(item);
        }
    }

    int FindEmptyRandomSlot()
    {
        for (int i = BASIC_SLOT_COUNT; i < MAX_SLOTS; i++)
        {
            if (i >= SlotStates.Count) { SlotStates.Add(ItemSlotNetData.Empty); return i; }
            if (SlotStates[i].ItemId == -1) return i;
        }
        return -1;
    }

    /// FIX-03: MaxUses == -1 (Permanent) → RemainingUses = 255 (unlimited sentinel)
    ItemSlotNetData MakeSlot(ItemDataSO item)
    {
        short id = (short)System.Array.IndexOf(_itemRegistry, item);
        byte uses = item.MaxUses <= 0 ? (byte)255 : (byte)item.MaxUses;
        return new ItemSlotNetData
        {
            ItemId = id,
            RemainingUses = uses,
            Flags = (byte)(item.SlotType == ItemSlotType.Sub ? 0b10 : 0)
            // bit 0 = BlueTape blocked, bit 1 = Sub type
        };
    }

    /// FIX-12: 라운드 간 BasicConsumable 리셋
    public void ResetForNewRound()
    {
        _thresholdGranted = new bool[3];

        // BasicConsumable 아이템 리필 (Tea, Cat)
        for (int i = 0; i < BASIC_SLOT_COUNT; i++)
        {
            var slot = SlotStates[i];
            if (slot.ItemId == -1) continue;
            var item = _itemRegistry[slot.ItemId];
            if (item.Persistence == ItemPersistence.BasicConsumable)
            {
                slot.RemainingUses = (byte)item.MaxUses;
                SlotStates[i] = slot;
            }
        }

        // 랜덤 슬롯 초기화 (새 라운드에서 다시 지급)
        for (int i = BASIC_SLOT_COUNT; i < SlotStates.Count; i++)
        {
            SlotStates[i] = ItemSlotNetData.Empty;
        }
    }
}
```

### 4.5 ItemDropTable (서버 전용)

```csharp
class ItemDropTable
{
    struct WeightedItem { public ItemDataSO Item; public float Weight; }

    WeightedItem[] _pool;
    float _totalWeight;  // = 102 (기획서 합계)

    public ItemDropTable(ItemDataSO[] randomItems)
    {
        _pool = new WeightedItem[randomItems.Length];
        _totalWeight = 0;
        for (int i = 0; i < randomItems.Length; i++)
        {
            _pool[i] = new WeightedItem { Item = randomItems[i], Weight = randomItems[i].DropWeight };
            _totalWeight += randomItems[i].DropWeight;
        }
    }

    public ItemDataSO Roll()
    {
        float roll = UnityEngine.Random.Range(0f, _totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < _pool.Length; i++)
        {
            cumulative += _pool[i].Weight;
            if (roll <= cumulative) return _pool[i].Item;
        }
        return _pool[^1].Item;  // fallback
    }
}
```

### 4.6 Enums

```csharp
enum ItemCategory : byte { Attack, Defense, Recovery, Buff, Debuff, Sabotage, Special }
enum ItemSlotType : byte { Main, Sub }
enum ItemPersistence : byte { Permanent, BasicConsumable, RandomConsumable }
enum SabotageType : byte { Reroll, Steal, BlockBasic, Neutralize }
enum MiniGameType : byte { None, HitTargets, HugCharacter, PatternUnlock, TapRepeat,
                            GaugeMatch, BoilWater, ScrewTighten, CardPick, ClawGrab, TapeCut, RedTap }
enum EffectType : byte { TempChange, FanSpeedChange, BasicBlock, RecoveryRateChange }
enum CombatEventType : byte { MainEffect, DefenseActivated, Neutralized, Death }
```

### 4.7 Combat Data Structs (FIX-REV3: 미정의 타입 추가)

```csharp
class CombatResult
{
    public int FirstPlayerIndex;
    public int WinnerIndex = -1;        // -1 = no winner this turn
    public List<CombatEvent> Events = new();

    public CombatResultData ToNetData()
    {
        return new CombatResultData
        {
            FirstPlayerIndex = (byte)FirstPlayerIndex,
            WinnerIndex = (sbyte)WinnerIndex,
            EventCount = (byte)Events.Count,
            // 이벤트 데이터 직렬화 (최대 4개)
        };
    }
}

struct CombatEvent
{
    public CombatEventType Type;
    public int SourcePlayer;
    public int TargetPlayer;
    public short ItemId;
    public float UserResultTemp;        // FIX-10: 양쪽 온도 기록
    public float TargetResultTemp;
}

/// ClientRpc 전송용 (INetworkSerializable)
struct CombatResultData : INetworkSerializable
{
    public byte FirstPlayerIndex;
    public sbyte WinnerIndex;           // -1 = no winner
    public byte EventCount;
    public short Event0ItemId;          // 최대 2개 이벤트 (1v1이므로)
    public short Event1ItemId;
    public float Event0UserTemp;
    public float Event0TargetTemp;
    public float Event1UserTemp;
    public float Event1TargetTemp;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref FirstPlayerIndex);
        serializer.SerializeValue(ref WinnerIndex);
        serializer.SerializeValue(ref EventCount);
        serializer.SerializeValue(ref Event0ItemId);
        serializer.SerializeValue(ref Event1ItemId);
        serializer.SerializeValue(ref Event0UserTemp);
        serializer.SerializeValue(ref Event0TargetTemp);
        serializer.SerializeValue(ref Event1UserTemp);
        serializer.SerializeValue(ref Event1TargetTemp);
    }
}

/// MiniGame ServerRpc 전송용
struct MiniGameResultData : INetworkSerializable
{
    public byte SlotIndex;
    public bool Success;
    public float CompletionTime;        // 서버 검증: timeLimit 이내인지
    public int InputCount;              // 서버 검증: 합리적 범위인지

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SlotIndex);
        serializer.SerializeValue(ref Success);
        serializer.SerializeValue(ref CompletionTime);
        serializer.SerializeValue(ref InputCount);
    }
}
```

---

## 5. Turn System

### 5.1 TurnManager (NetworkBehaviour, 서버 전용 로직)

```csharp
public class TurnManager : NetworkBehaviour
{
    // ═══ NetworkVariables ═══
    public NetworkVariable<TurnPhase> CurrentPhase = new(TurnPhase.WaitingForPlayers);
    public NetworkVariable<int> TurnNumber = new(0);
    // FIX-04: 타이머는 시작 시각 + 지속 시간만 동기화, 클라이언트가 로컬 계산
    public NetworkVariable<double> PrepStartServerTime = new(0);
    public NetworkVariable<float> PrepDuration = new(20f);

    // ═══ Server-local ═══
    PlayerState _p1, _p2;
    TemperatureSystem _tempSystem;
    CombatResolver _combatResolver;
    BuffDebuffSystem _buffSystem;
    EnvironmentSystem _envSystem;       // nullable (데모에서는 null)
    ItemManager _itemManager;           // 아이템 레지스트리 + 드롭 테이블
    PlayerModifiers[] _modifiers = new PlayerModifiers[2]; // FIX-09: [0]=P1, [1]=P2

    const float PREP_DURATION = 20f;

    // FIX-14: 싱글턴 접근자 (PlayerState ServerRpc에서 사용)
    public static TurnManager Instance { get; private set; }
    public override void OnNetworkSpawn()
    {
        if (IsServer) Instance = this;
    }

    // FIX-REV3: PlayerState.BuildContext()에서 필요한 접근자
    public PlayerState GetPlayer(int index) => index == 0 ? _p1 : _p2;
    public PlayerModifiers[] GetModifiers() => _modifiers;
    public TemperatureSystem GetTempSystem() => _tempSystem;
    public BuffDebuffSystem GetBuffSystem() => _buffSystem;
    public ItemDropTable GetDropTable() => _itemManager?.GetDropTable();

    // ═══ WaitForSeconds Cache (GC 방지) ═══
    static readonly WaitForSeconds _waitHalf = new(0.5f);
    static readonly WaitForSeconds _waitOne = new(1f);
    static readonly WaitForSeconds _waitTwo = new(2f);
}
```

### 5.2 PrepPhase 상세 로직 (서버)

```csharp
// TurnManager.cs (continued)

IEnumerator PrepPhaseRoutine()
{
    // ── 1. 턴 초기화 ──
    TurnNumber.Value++;
    float prepDuration = GetPrepDuration();

    _p1.GetActionQueue().Clear();
    _p2.GetActionQueue().Clear();
    _modifiers[0].Reset();  // FIX-09: TurnManager가 modifiers 배열 소유
    _modifiers[1].Reset();
    _p1.IsReady.Value = false;
    _p2.IsReady.Value = false;

    // ── 2. 버프/디버프 적용 (이전 턴에서 예약된 것) ──
    _buffSystem.ProcessTurnStart(_p1, _p2);

    // ── 3. 환경 변수 공개 (2턴째) ──
    // FIX-05: _envSystem null 체크 (데모에서는 환경 시스템 없음)
    if (TurnNumber.Value == 2 && _envSystem != null && _envSystem.HasEnvironment)
    {
        OnEnvironmentRevealClientRpc((byte)_envSystem.CurrentEnvironment);
        _envSystem.ApplyModifiers(_p1, _p2, this);
    }

    // ── 4. 선풍기 가동 ──
    _p1.IsFanActive.Value = true;
    _p2.IsFanActive.Value = true;

    // FIX-04: 타이머는 시작 시각 + 지속 시간만 1회 동기화
    PrepStartServerTime.Value = NetworkManager.ServerTime.Time;
    PrepDuration.Value = prepDuration;

    CurrentPhase.Value = TurnPhase.PrepPhase;
    OnPhaseChangedClientRpc(TurnPhase.PrepPhase, TurnNumber.Value);

    // ── 5. 타이머 루프 ──
    float elapsed = 0f;

    while (elapsed < prepDuration)
    {
        float dt = Time.deltaTime;
        elapsed += dt;

        // 선풍기 온도 감소 (아직 Ready 안 한 플레이어)
        if (_p1.IsFanActive.Value)
            _tempSystem.TickFan(_p1, dt);
        if (_p2.IsFanActive.Value)
            _tempSystem.TickFan(_p2, dt);

        // Ready 후 회복
        // FIX-05: _envSystem null → 기본 회복률 사용
        float recoveryRate = _envSystem != null
            ? _envSystem.GetRecoveryRate()
            : TemperatureSystem.DEFAULT_RECOVERY_RATE;
        if (_p1.IsReady.Value && !_p1.IsFanActive.Value)
            _tempSystem.TickRecovery(_p1, dt, recoveryRate);
        if (_p2.IsReady.Value && !_p2.IsFanActive.Value)
            _tempSystem.TickRecovery(_p2, dt, recoveryRate);

        // 온도 0° 체크 (PrepPhase 중 사망)
        if (_tempSystem.IsDead(_p1) || _tempSystem.IsDead(_p2))
        {
            yield return StartCoroutine(HandlePrepDeath());
            yield break;
        }

        // 양쪽 모두 Ready → 즉시 AttackPhase
        if (_p1.IsReady.Value && _p2.IsReady.Value)
            break;

        yield return null;  // 다음 프레임
    }

    // ── 6. 타이머 만료 처리 ──
    if (!_p1.IsReady.Value) ForceReady(_p1);
    if (!_p2.IsReady.Value) ForceReady(_p2);

    // ── 7. AttackPhase 전환 ──
    yield return StartCoroutine(AttackPhaseRoutine());
}

void ForceReady(PlayerState player)
{
    player.IsReady.Value = true;
    player.IsFanActive.Value = false;
    player.GetActionQueue().SetReadyNoMain(Time.time);
}

float GetPrepDuration()
{
    // FIX-05: _envSystem null 안전
    if (_envSystem != null && _envSystem.CurrentEnvironment == EnvironmentType.SummerVacation)
        return 10f;
    return PREP_DURATION;
}
```

### 5.3 Sub/Main 아이템 처리 (서버, ServerRpc 수신 시)

```csharp
// PlayerState.cs — ServerRpc 핸들러

[Rpc(SendTo.Server)]
void UseSubItemServerRpc(byte slotIndex, RpcParams rpcParams = default)
{
    // ═══ 검증 ═══
    if (!IsServer) return;
    if (TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase) return;
    if (IsReady.Value) return;                                      // 이미 Ready
    if (slotIndex >= _inventory.SlotStates.Count) return;           // 범위 초과
    var slot = _inventory.SlotStates[slotIndex];
    if (!slot.IsUsable) return;                                     // FIX-03: 통합 검증 (empty/consumed/blocked)
    var itemData = _inventory.GetItemData(slotIndex);
    if (itemData.SlotType != ItemSlotType.Sub) return;              // Main인데 Sub RPC 보냄
    if (!itemData.CanUse(BuildContext())) return;                    // 사용 불가 (봉쇄 등)

    // ═══ 미니게임 체크 ═══
    if (itemData.RequiresMiniGame)
    {
        MiniGameManager.Instance.StartMiniGame(this, slotIndex, itemData);
        return;  // 미니게임 결과 후 효과 적용
    }

    // ═══ 효과 즉시 실행 (Sub는 PrepPhase 중 바로 적용) ═══
    var ctx = BuildContext();
    itemData.ExecuteEffect(ctx);
    _inventory.ConsumeItem(slotIndex);
    _actionQueue.AddSub(slotIndex, itemData);

    // ═══ 클라이언트 알림 ═══
    TurnManager.Instance.OnSubItemUsedClientRpc(_playerIndex, slotIndex, (byte)itemData.Category);
}

[Rpc(SendTo.Server)]
void SelectMainItemServerRpc(byte slotIndex, RpcParams rpcParams = default)
{
    // ═══ 검증 (Sub와 동일 + Main 타입 확인) ═══
    if (!IsServer) return;
    if (TurnManager.Instance.CurrentPhase.Value != TurnPhase.PrepPhase) return;
    if (IsReady.Value) return;
    if (slotIndex >= _inventory.SlotStates.Count) return;
    var slot = _inventory.SlotStates[slotIndex];
    if (!slot.IsUsable) return;                                     // FIX-03: 통합 검증
    var itemData = _inventory.GetItemData(slotIndex);
    if (itemData.SlotType != ItemSlotType.Main) return;             // Sub인데 Main RPC 보냄
    if (!itemData.CanUse(BuildContext())) return;

    // ═══ 미니게임 체크 ═══
    if (itemData.RequiresMiniGame)
    {
        MiniGameManager.Instance.StartMiniGame(this, slotIndex, itemData);
        return;
    }

    // ═══ Main 큐잉 + 자동 Ready ═══
    // FIX-09: readyTimestamp는 ActionQueue에서만 관리 (중복 제거)
    _actionQueue.SetMain(slotIndex, itemData, Time.time);
    IsReady.Value = true;
    IsFanActive.Value = false;  // 선풍기 정지 → 회복 시작
}
```

### 5.4 AttackPhase 상세 로직 (서버)

```csharp
IEnumerator AttackPhaseRoutine()
{
    CurrentPhase.Value = TurnPhase.AttackPhase;
    OnPhaseChangedClientRpc(TurnPhase.AttackPhase, TurnNumber.Value);

    // 클라이언트 연출 대기 (카메라 전환 등)
    yield return _waitOne;

    // ═══ Combat Resolution ═══
    // FIX-REV3: Resolve 시그니처에 맞게 _modifiers 배열 전달
    var result = _combatResolver.Resolve(
        _p1.GetActionQueue(),
        _p2.GetActionQueue(),
        _modifiers,            // PlayerModifiers[2] 배열
        _p1, _p2,
        _tempSystem,
        _buffSystem,
        _envSystem
    );

    // ═══ 결과 브로드캐스트 ═══
    OnCombatResultClientRpc(result.ToNetData());

    // 클라이언트 연출 대기 (공격 애니메이션)
    yield return _waitTwo;

    // ═══ Resolution Phase ═══
    yield return StartCoroutine(ResolutionPhaseRoutine(result));
}
```

---

## 6. CombatResolver (서버 전용 C# 클래스)

### 6.1 Resolution 알고리즘

```csharp
class CombatResolver
{
    /// FIX-08: modifiers는 배열 참조로 전달 (ref struct 문제 회피)
    public CombatResult Resolve(
        ActionQueue p1Queue, ActionQueue p2Queue,
        PlayerModifiers[] modifiers,   // [0]=P1, [1]=P2
        PlayerState p1, PlayerState p2,
        TemperatureSystem tempSystem,
        BuffDebuffSystem buffSystem,
        EnvironmentSystem envSystem)   // nullable
    {
        var result = new CombatResult();

        // ═══ Step 1: 실행 순서 결정 ═══
        int firstIdx = DetermineOrder(p1Queue, p2Queue, p1, p2, envSystem);
        int secondIdx = 1 - firstIdx;
        result.FirstPlayerIndex = firstIdx;

        // ═══ Step 2: 무력화 체크 (Red Card) ═══
        // Red Card는 Sub로 PrepPhase에서 이미 modifiers에 적용됨

        // ═══ Step 3: 방어 먼저 적용 (양쪽 동시, 순서 무관) ═══
        // FIX-08: 방어는 여기서만 설정. DefenseItemDataSO.ExecuteEffect()는
        // CombatResolver를 통하지 않고 직접 호출되지 않음 (Main 방어는 여기서 처리)
        ApplyDefense(p1Queue, modifiers, 0);
        ApplyDefense(p2Queue, modifiers, 1);

        // ═══ Step 4: Main 행동 순차 실행 ═══
        var firstQueue = firstIdx == 0 ? p1Queue : p2Queue;
        var secondQueue = secondIdx == 0 ? p1Queue : p2Queue;
        var firstPlayer = firstIdx == 0 ? p1 : p2;
        var secondPlayer = secondIdx == 0 ? p1 : p2;

        // 1st player Main (방어 아이템은 Step 3에서 이미 처리 → 스킵)
        if (firstQueue.mainAction.HasValue
            && !modifiers[firstIdx].ActionNeutralized
            && firstQueue.mainAction.Value.ItemData is not DefenseItemDataSO)
        {
            result.Events.Add(ExecuteMain(
                firstQueue.mainAction.Value, firstPlayer, secondPlayer,
                firstIdx, secondIdx, modifiers, tempSystem, buffSystem));
        }

        // 사망 체크 — 1st player가 상대 죽였으면 여기서 종료
        if (tempSystem.IsDead(secondPlayer))
        {
            result.WinnerIndex = firstIdx;
            return result;
        }
        if (tempSystem.IsDead(firstPlayer))
        {
            result.WinnerIndex = secondIdx;
            return result;
        }

        // 2nd player Main
        if (secondQueue.mainAction.HasValue
            && !modifiers[secondIdx].ActionNeutralized
            && secondQueue.mainAction.Value.ItemData is not DefenseItemDataSO)
        {
            result.Events.Add(ExecuteMain(
                secondQueue.mainAction.Value, secondPlayer, firstPlayer,
                secondIdx, firstIdx, modifiers, tempSystem, buffSystem));
        }

        // 최종 사망 체크
        if (tempSystem.IsDead(firstPlayer))
            result.WinnerIndex = secondIdx;
        else if (tempSystem.IsDead(secondPlayer))
            result.WinnerIndex = firstIdx;

        return result;
    }

    int DetermineOrder(ActionQueue p1, ActionQueue p2,
                       PlayerState ps1, PlayerState ps2, EnvironmentSystem env)
    {
        // FIX-05: env null 안전
        if (env != null && env.CurrentEnvironment == EnvironmentType.HeatWaveWarning)
        {
            if (ps1.Temperature.Value < ps2.Temperature.Value) return 0;
            if (ps2.Temperature.Value < ps1.Temperature.Value) return 1;
        }

        // 기본: Ready 먼저 누른 쪽
        if (p1.readyTimestamp <= p2.readyTimestamp) return 0;
        return 1;
    }

    CombatEvent ExecuteMain(QueuedAction action,
                             PlayerState user, PlayerState target,
                             int userIdx, int targetIdx,
                             PlayerModifiers[] modifiers,
                             TemperatureSystem tempSystem, BuffDebuffSystem buffSystem)
    {
        var ctx = new ItemContext
        {
            User = user, Target = target,
            UserIndex = userIdx, TargetIndex = targetIdx,
            AllModifiers = modifiers,
            TempSystem = tempSystem, BuffSystem = buffSystem,
        };

        action.ItemData.ExecuteEffect(ctx);

        // FIX-10: 공격은 target 온도, 회복은 user 온도 기록
        bool isRecovery = action.ItemData.Category == ItemCategory.Recovery;
        return new CombatEvent
        {
            Type = CombatEventType.MainEffect,
            SourcePlayer = userIdx,
            TargetPlayer = targetIdx,
            ItemId = (short)action.SlotIndex,
            UserResultTemp = user.Temperature.Value,
            TargetResultTemp = target.Temperature.Value
        };
    }

    void ApplyDefense(ActionQueue queue, PlayerModifiers[] modifiers, int playerIdx)
    {
        if (queue.mainAction.HasValue && queue.mainAction.Value.ItemData is DefenseItemDataSO defItem)
        {
            modifiers[playerIdx].ActiveDefense = new DefenseInfo
            {
                Filter = defItem.Filter,
                BlockAmount = defItem.BlockAmount
            };
        }
    }
}
```

---

## 7. Buff/Debuff System (서버 전용)

```csharp
class BuffDebuffSystem
{
    struct ScheduledEffect
    {
        public int TargetPlayerIndex;
        public EffectType Type;      // TempChange, FanSpeedChange, BasicBlock
        public float Value;
        public int TurnsRemaining;   // 0 = apply this turn start
    }

    List<ScheduledEffect> _pending = new();

    /// 효과 예약 (아이템 사용 시 호출)
    public void Schedule(int targetPlayer, EffectType type, float value, int delayTurns)
    {
        _pending.Add(new ScheduledEffect
        {
            TargetPlayerIndex = targetPlayer,
            Type = type, Value = value,
            TurnsRemaining = delayTurns
        });
    }

    /// 턴 시작 시 호출: 예약된 효과 적용/진행
    public void ProcessTurnStart(PlayerState p1, PlayerState p2)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var eff = _pending[i];
            eff.TurnsRemaining--;

            if (eff.TurnsRemaining <= 0)
            {
                var target = eff.TargetPlayerIndex == 0 ? p1 : p2;
                ApplyEffect(target, eff);
                _pending.RemoveAt(i);
            }
            else
            {
                _pending[i] = eff;
            }
        }
    }

    void ApplyEffect(PlayerState target, ScheduledEffect eff)
    {
        switch (eff.Type)
        {
            case EffectType.TempChange:
                target.Temperature.Value = Mathf.Clamp(
                    target.Temperature.Value + eff.Value, 0f, 37f);
                break;
            case EffectType.FanSpeedChange:
                target.FanSpeed.Value = eff.Value;
                break;
            case EffectType.BasicBlock:
                // handled by PlayerModifiers at turn start
                break;
        }
    }

    public void ClearAll() => _pending.Clear();
}
```

---

## 8. Managers (Presentation Layer — 클라이언트 전용)

### 8.1 Manager 전체 목록

| # | Manager | Scene | DontDestroyOnLoad | Network |
|---|---------|-------|:-:|:-:|
| 1 | NetworkManager | Lobby | ✓ | Built-in |
| 2 | LobbyManager | Lobby | ✓ | — |
| 3 | RelayManager | Lobby | ✓ | — |
| 4 | SessionManager | Lobby | ✓ | — |
| 5 | PlayerSpawnManager | Lobby | ✓ | — |
| 6 | MatchManager | Game | — | NB |
| 7 | TurnManager | Game | — | NB |
| 8 | ItemManager | Game | — | NB |
| 9 | MiniGameManager | Game | — | NB |
| 10 | CameraManager | Game | — | Local |
| 11 | AudioManager | Lobby | ✓ | Local |
| 12 | VFXManager | Game | — | Local |
| 13 | LightManager | Game | — | Local |
| 14 | ObjectPoolManager | Game | — | Local |

> NB = NetworkBehaviour, Local = MonoBehaviour (네트워크 안 탐)

### 8.2 CameraManager (MonoBehaviour)

```csharp
class CameraManager : MonoBehaviour
{
    enum CamState { Default, ItemZoom, AttackView, DefeatFreeze, DefeatSelf }

    // ═══ 상태별 동작 ═══
    // Default:      고정 3인칭 — 플레이어 뒤, 약간 위에서 상대방 바라봄
    // ItemZoom:     선택한 아이템 클로즈업 + 배경 DOF blur
    // AttackView:   중앙으로 약간 줌인, 양쪽 플레이어 프레임
    // DefeatFreeze: 상대 동결→파쇄 관전
    // DefeatSelf:   화면 가장자리 서리 → 최종 라운드: 카메라 180° 회전

    [Header("Camera Positions")]
    Transform _defaultPos;          // Scene에 배치된 Transform
    Transform _attackPos;
    float _transitionDuration = 0.3f;

    [Header("Effects")]
    float _shakeIntensity;
    float _shakeDuration;

    // ═══ Public API ═══
    public void SetState(CamState state, float duration = 0.3f);
    public void ShakeCamera(float intensity, float duration);
    public void SetFrostOverlay(float intensity);  // 0~1, 온도 기반

    // ═══ NV Callback 연결 ═══
    // TurnManager.CurrentPhase.OnValueChanged → 페이즈에 따라 상태 전환
    // PlayerState.Temperature.OnValueChanged → 서리 오버레이 강도 조절
}
```

### 8.3 AudioManager (MonoBehaviour, DDOL)

```csharp
class AudioManager : MonoBehaviour
{
    [Header("Audio Sources")]
    AudioSource _bgmSource;
    AudioSource _ambientSource;
    AudioSource[] _sfxPool;         // 풀링된 SFX 소스 (8개)

    [Header("Audio Library (SO)")]
    AudioLibrarySO _library;

    // ═══ BGM ═══
    // lobby, gameNormal, gameTense, gameClimax, victory, defeat
    public void PlayBGM(BGMTrack track, float crossfade = 1f);
    public void SetBGMTension(float t);   // 0~1 → Normal↔Tense↔Climax 전환

    // ═══ SFX ═══
    // 아이템별: fanSwing, windBlock, teaPour, catMeow,
    //          iceCrack, waterSplash, screwTurn, clawGrab,
    //          phoneUnlock, noodleSlurp, sodaShake, tapeRip,
    //          cardFlip, redWhistle, hugSqueeze
    // 시스템: freeze, shatter, timerTick, timerWarning, readyChime
    public void PlaySFX(SFXType type);

    // ═══ Ambient ═══
    // fanHum (항상), cicada, kidsPlaying, ambulanceSiren, summerBreeze
    public void SetAmbient(AmbientType type, float fade = 0.5f);

    // ═══ UI ═══
    // buttonClick, itemSelect, itemZoom, miniGameSuccess, miniGameFail
    public void PlayUI(UISoundType type);
}
```

### 8.4 VFXManager (MonoBehaviour)

```csharp
class VFXManager : MonoBehaviour
{
    // ═══ 효과 목록 ═══
    // TempGaugePulse:    온도 변경 시 게이지 바 흔들림 + 플래시
    // FrostEdge:         온도 ≤20° → 화면 가장자리 서리 (10°에서 더 강해짐)
    // Freeze:            온도 0° → 캐릭터 얼음 재질 스왑
    // Shatter:           동결 후 → 얼음 조각 파티클
    // FanWind:           Prep 중 → 선풍기에서 바람 파티클
    // FanSpeedUp:        드라이버 효과 → 바람 파티클 강화
    // ItemUseVFX:        아이템별 파티클 (얼음 결정, 물 스플래시, 불 등)
    // DefenseBlock:      방어 발동 시 쉴드 반짝임
    // TempColorShift:    온도에 따른 캐릭터 색상 (빨강→분홍→하늘→파랑)
    // EnvironmentReveal: 환경 변수 공개 시 전체 화면 플래시

    public void PlayEffect(VFXType type, Vector3 position, float intensity = 1f);
    public void SetFrostOverlay(float intensity);
    public void SetPlayerTempColor(int playerIndex, float temperature);
    public void PlayFreezeSequence(int playerIndex);  // freeze → hold → shatter
    public void SetFanWindIntensity(int playerIndex, float speed);
}
```

### 8.5 LightManager (MonoBehaviour)

```csharp
class LightManager : MonoBehaviour
{
    [Header("URP Lights")]
    Light _directionalLight;        // 메인 태양광
    Volume _postProcessVolume;      // Color Grading, Bloom

    // ═══ 온도 기반 조명 ═══
    // 37°: 따뜻한 오후 햇살 (color temp 6500K)
    // 20°: 서늘한 톤 (color temp 8000K, 약간 어두움)
    // 10°: 차가운 파란색 (color temp 10000K, 더 어두움)
    //  0°: 극한 푸른 백색 (동결 강조)
    public void SetTemperatureInfluence(float lowestTemp);

    // ═══ 페이즈별 조명 ═══
    // AttackPhase: ambient 약간 어둡게 + 활성 플레이어 스팟 강조
    // Freeze: 동결 캐릭터에 harsh blue-white, 나머지 dim
    public void SetAttackPhaseLight(int activePlayerIndex);
    public void PlayFreezeLight(int playerIndex);

    // ═══ 환경별 조명 ═══
    // SunnyDay: 밝고 따뜻하게
    // CoolBreeze: 서늘하고 바람 그림자
    public void SetEnvironmentLight(EnvironmentType env);
    public void ResetToNeutral(float duration = 0.5f);
}
```

### 8.6 ObjectPoolManager (MonoBehaviour)

```csharp
class ObjectPoolManager : MonoBehaviour
{
    // VFX 파티클, 데미지 숫자(TMP), UI 아이콘 풀링
    // Pre-warm: VFX 10개/타입, 데미지 텍스트 8개

    public T Get<T>(PoolType type) where T : Component;
    public void Return<T>(T obj, PoolType type);
    public void PreWarm(PoolType type, int count);
}
```

---

## 9. Demo Specification (4개 아이템)

### 9.1 데모 범위

| 포함 | 제외 (추후 구현) |
|------|-----------------|
| Temperature System (팬 감소 + 회복) | 랜덤 아이템 드롭 (구간 지급) |
| Turn System (Prep → Attack → Resolution) | 미니게임 |
| 4 기본 아이템 (부채, 바람막이, 따뜻한 차, 고양이) | 17 랜덤 아이템 |
| Sub/Main 아이템 구분 | 버프/디버프 (지연 효과) |
| CombatResolver (순차 실행, 방어) | 환경 변수 |
| 단일 라운드 | Bo3 매치 |
| 네트워크 동기화 (NV + RPC) | VFX/조명 연출 |
| 기본 UI (온도 바, 아이템 슬롯, 타이머) | 풀 비주얼 (동결/파쇄) |

### 9.2 Demo 아이템 4개 상세

```
┌─────────────────────────────────────────────────────────────┐
│  DEMO ITEM 1: 부채 (Fan)                                    │
│  ─────────────────────────────────────────────────────────  │
│  Category: Attack          SlotType: Main                   │
│  Persistence: Permanent    MaxUses: -1 (무한)               │
│  Damage: 3°                AttackFilter: Temperature        │
│  MiniGame: None            DropWeight: 0 (기본 아이템)       │
│                                                             │
│  효과: opponent.Temperature -= 3                            │
│  방어 상호작용: Windbreaker에 의해 차단 (4° block > 3° atk)  │
│  서버 로직:                                                  │
│    ctx.TempSystem.ApplyDamage(ctx.Target, 3f,               │
│                                DamageFilter.Temperature);   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  DEMO ITEM 2: 바람막이 (Windbreaker)                         │
│  ─────────────────────────────────────────────────────────  │
│  Category: Defense         SlotType: Main                   │
│  Persistence: Permanent    MaxUses: -1 (무한)               │
│  BlockAmount: 4°           Filter: Temperature              │
│  MiniGame: None            DropWeight: 0                    │
│                                                             │
│  효과: user.Defense = {Temperature, 4°}                     │
│  CombatResolver에서 방어 먼저 적용 (순서 무관)                │
│  서버 로직:                                                  │
│    ctx.UserModifiers.ActiveDefense = new DefenseInfo         │
│    { Filter = Temperature, BlockAmount = 4f };              │
│                                                             │
│  데미지 계산 예:                                             │
│    부채(3°) vs 바람막이(4°) → 3-4 = -1 → 0° damage (완전차단)│
│    물총(7°) vs 바람막이(4°) → 7-4 = 3° damage (관통)        │
│    ※ Q34 답변에 따라 변경 가능                                │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  DEMO ITEM 3: 따뜻한 차 (Warm Tea)                           │
│  ─────────────────────────────────────────────────────────  │
│  Category: Recovery        SlotType: Main                   │
│  Persistence: BasicConsumable   MaxUses: 1                  │
│  HealPerUse: [7]           DropWeight: 0                    │
│  MiniGame: None                                             │
│                                                             │
│  효과: user.Temperature += 7 (최대 37°)                     │
│  사용 후 소모 → 다음 "게임"에 리필 (Q6 답변 대기)            │
│  서버 로직:                                                  │
│    ctx.TempSystem.ApplyHeal(ctx.User, 7f);                  │
│    ctx.UserInventory.ConsumeItem(ctx.SlotIndex);            │
│                                                             │
│  소모 후 UI: 슬롯 회색 처리, 사용 불가                       │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  DEMO ITEM 4: 고양이 (Cat)                                   │
│  ─────────────────────────────────────────────────────────  │
│  Category: Sabotage        SlotType: Sub                    │
│  Persistence: BasicConsumable   MaxUses: 1                  │
│  SabotageType: Reroll      DropWeight: 0                    │
│  MiniGame: None                                             │
│                                                             │
│  효과: opponent의 모든 랜덤 아이템을 리롤                    │
│  ※ 데모에서는 랜덤 아이템이 없으므로 no-op (구조만 검증)    │
│  Sub이므로 사용 후 턴 유지 → Main이나 Ready 추가 가능       │
│  서버 로직:                                                  │
│    ctx.TargetInventory.RerollAllRandom(ctx.DropTable);      │
│    ctx.UserInventory.ConsumeItem(ctx.SlotIndex);            │
│                                                             │
│  데모 가치: Sub/Main 분리 흐름 테스트                        │
│    Cat(Sub) → Fan(Main) → END  ← 이 흐름 검증              │
└─────────────────────────────────────────────────────────────┘
```

### 9.3 Demo 턴 시나리오 (검증용)

```
═══ Scenario A: 양쪽 공격 ═══
P1: Fan(Main) → Ready         P2: Fan(Main) → Ready
P1 readyTimestamp < P2 → P1 먼저
  P1 Fan → P2 temp: 37-3 = 34°
  P2 Fan → P1 temp: 37-3 = 34°
  → 양쪽 34°, 다음 턴

═══ Scenario B: 공격 vs 방어 ═══
P1: Fan(Main, 3°) → Ready     P2: Windbreaker(Main, block 4°) → Ready
CombatResolver:
  방어 먼저 적용: P2 defense = {Temp, 4°}
  P1 Fan → P2 temp: ApplyDamage(3°) → block 4° → actual 0° → P2 무피해
  P2 Windbreaker는 방어 전용 → 공격 효과 없음
  → P1 37°, P2 37°

═══ Scenario C: Sub → Main 콤보 ═══
P1: Cat(Sub) → Fan(Main) → Ready
P2: Warm Tea(Main) → Ready
서버 처리:
  P1 Sub(Cat): 즉시 실행 → P2 랜덤 리롤 (데모에선 no-op)
  P1 Main(Fan): 큐잉 + Ready
  AttackPhase:
    P1 Fan → P2 temp: 37-3 = 34°
    P2 Tea → P2 temp: 34+7 = 37° (자가 회복으로 상쇄, cap 37°)

═══ Scenario D: 타이머 만료 ═══
P1: Ready 누르지 않음 (20초 만료)
P2: Fan(Main) → Ready (5초에 누름)
서버: P1 ForceReady (mainAction = null)
AttackPhase:
  P2가 먼저 (P2 readyTimestamp < P1 forced timestamp)
  P2 Fan → P1 temp -= 3
  P1 무행동
  ※ P1은 20초 동안 팬 작동 → temp: 37 - 20 = 17°
  ※ P2는 5초에 Ready → 15초 회복 → temp: 37 - 5 + 15 = 37° (cap)
```

### 9.4 Demo 구현 순서

```
Phase 1: Foundation (네트워크 없이 로컬 테스트)
  1-1. ItemDataSO 기반 클래스 + 4개 SO 에셋 생성
  1-2. ItemEnums 정의
  1-3. TemperatureSystem 핵심 로직
  1-4. ActionQueue 구조

Phase 2: Turn Loop (서버 전용 로직)
  2-1. TurnManager 상태 머신 (Prep → Attack → Resolution)
  2-2. PlayerState NV 세팅
  2-3. PlayerInventory + NetworkList
  2-4. CombatResolver (방어 + 순차 실행)

Phase 3: Network Integration
  3-1. ServerRpc (UseSubItem, SelectMainItem, PressReady) + 검증
  3-2. ClientRpc (CombatResult, SubItemUsed, PhaseChanged)
  3-3. NV 콜백 연결 (Temperature → UI, Phase → 카메라)

Phase 4: UI
  4-1. GameHUD (온도 바 2개, 타이머, Ready 버튼)
  4-2. ItemPanelUI (4 슬롯, Sub/Main 구분 표시)
  4-3. CombatResultUI (결과 텍스트)

Phase 5: Polish
  5-1. CameraManager (Default + AttackView)
  5-2. AudioManager (기본 SFX)
  5-3. 기본 VFX (온도 바 색상 변화)
```

---

## 10. File Structure

```
Assets/Scripts/
├── Core/
│   ├── Player/
│   │   ├── PlayerState.cs              # NB: 온도, 팬, Ready + ServerRpc
│   │   ├── PlayerInventory.cs          # NB: NetworkList<ItemSlotNetData>
│   │   ├── PlayerModifiers.cs          # struct: 턴별 수정자
│   │   └── ActionQueue.cs             # class: Sub/Main 행동 큐
│   ├── Match/
│   │   └── MatchManager.cs            # NB: Bo3 lifecycle
│   ├── Turn/
│   │   └── TurnManager.cs             # NB: 상태 머신 + PrepPhase 루프
│   ├── Combat/
│   │   ├── CombatResolver.cs          # C#: 전투 판정 알고리즘
│   │   ├── CombatResult.cs            # struct: 결과 데이터
│   │   └── TemperatureSystem.cs       # C#: 온도 계산
│   ├── Item/
│   │   ├── Data/
│   │   │   ├── ItemDataSO.cs          # SO base: 공통 필드 + abstract
│   │   │   ├── AttackItemDataSO.cs    # SO: damage
│   │   │   ├── DefenseItemDataSO.cs   # SO: blockAmount, filter
│   │   │   ├── RecoveryItemDataSO.cs  # SO: healPerUse[]
│   │   │   ├── SabotageItemDataSO.cs  # SO: sabotageType
│   │   │   └── SpecialItemDataSO.cs   # SO: unique effects
│   │   ├── ItemManager.cs            # NB: registry, drop table
│   │   ├── ItemDropTable.cs           # C#: weighted random
│   │   ├── ItemEnums.cs              # enums: Category, SlotType, etc.
│   │   └── ItemSlotNetData.cs        # struct: INetworkSerializable
│   ├── Buff/
│   │   └── BuffDebuffSystem.cs       # C#: scheduled effects
│   ├── MiniGame/
│   │   └── MiniGameManager.cs        # NB: lifecycle + validation
│   ├── Environment/
│   │   └── EnvironmentSystem.cs      # C#: modifier selection
│   └── Network/
│       ├── LobbyManager.cs           # (existing)
│       ├── RelayManager.cs           # (existing)
│       ├── SessionManager.cs         # (existing)
│       └── PlayerSpawnManager.cs     # (existing)
├── Presentation/
│   ├── CameraManager.cs
│   ├── AudioManager.cs
│   ├── AudioLibrarySO.cs
│   ├── VFXManager.cs
│   ├── LightManager.cs
│   └── ObjectPoolManager.cs
├── UI/
│   ├── Game/
│   │   ├── GameHUD.cs                # 온도 바, 타이머
│   │   ├── ItemPanelUI.cs            # 아이템 슬롯 (Sub/Main 구분)
│   │   ├── ItemDetailPopup.cs        # 아이템 상세 팝업
│   │   ├── ActionQueueUI.cs          # 큐잉된 행동 표시
│   │   └── CombatResultUI.cs         # 전투 결과 표시
│   ├── MiniGame/
│   │   └── MiniGameUIBase.cs         # abstract mini-game UI
│   ├── Match/
│   │   ├── RoundResultUI.cs
│   │   └── MatchResultUI.cs
│   └── Lobby/
│       └── AZLobbyUI.cs             # (existing)
└── Common/
    └── TurnEnums.cs                  # TurnPhase, MatchState
```

---

## 11. NV Callback → Client 연결 맵

> 클라이언트가 서버 상태 변경에 반응하는 경로.

| NetworkVariable | Callback | Client Action |
|-----------------|----------|---------------|
| `Temperature.OnValueChanged` | → | 온도 바 갱신, 색상 변경, 서리 오버레이 |
| `CurrentPhase.OnValueChanged` | → | UI 전환, 카메라 상태, BGM 전환 |
| `PrepStartServerTime` + `PrepDuration` | → | 클라이언트 로컬 계산: `remaining = Duration - (ServerTime - StartTime)` |
| `IsReady.OnValueChanged` | → | Ready 표시, 팬 애니메이션 정지 |
| `IsFanActive.OnValueChanged` | → | 팬 회전 애니메이션 on/off |
| `SlotStates.OnListChanged` | → | 인벤토리 UI 갱신 |
| `P1RoundWins.OnValueChanged` (MatchManager) | → | 라운드 스코어 UI |

```csharp
// 클라이언트 예시: 온도 바 연결
void OnEnable()
{
    _playerState.Temperature.OnValueChanged += OnTempChanged;
}

void OnTempChanged(float oldVal, float newVal)
{
    _tempBarUI.SetValue(newVal / 37f);                    // 0~1 비율
    _vfxManager.SetPlayerTempColor(_playerIndex, newVal); // 색상
    _vfxManager.SetFrostOverlay(newVal <= 20f              // 서리
        ? 1f - (newVal / 20f) : 0f);
    _cameraManager.SetFrostOverlay(newVal <= 20f
        ? 1f - (newVal / 20f) : 0f);
}
```
