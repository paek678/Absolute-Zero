# System Architecture — Absolute Zero

> Code system structure. All game logic is host-authoritative.
> SOLID principles applied. Item system uses class hierarchy.
> Last updated: 2026-07-14

---

## Design Principles

1. **Host-authoritative**: ALL mutations (damage, healing, time, win/loss, item use, buff/debuff, mini-game validation) happen on server. Client sends input only.
2. **SOLID**:
   - **S**: Each system has one responsibility (TurnSystem doesn't touch items, ItemSystem doesn't know about turns)
   - **O**: New items/effects added by creating new classes, not modifying existing ones
   - **L**: Any item subclass works wherever base item is expected
   - **I**: Systems expose minimal interfaces (IItemEffect, IDamageable, IBuffable)
   - **D**: Systems depend on interfaces, not concrete classes
3. **NetworkVariable for state, Rpc for events**: Continuous state → NetworkVariable, one-shot events → Rpc
4. **2.5D Rendering**: 3D background (Korean pavilion) + 2D hand-drawn sprites (characters, items). Fixed 3rd-person camera behind player, looking at opponent. URP pipeline.

---

## System Overview

```
┌─────────────────────────────────────────────────────────┐
│                    SERVER (Host)                         │
│                                                         │
│  ┌─────────────┐    ┌──────────────┐                   │
│  │ MatchManager │───→│ TurnManager  │                   │
│  │ (Bo3 rounds) │    │ (state machine)                  │
│  └─────────────┘    └──────┬───────┘                   │
│                            │                            │
│         ┌──────────────────┼──────────────────┐        │
│         │                  │                  │        │
│  ┌──────▼──────┐  ┌───────▼───────┐  ┌───────▼──────┐ │
│  │ Temperature  │  │   Combat      │  │ Environment  │ │
│  │ System       │  │   Resolver    │  │ System       │ │
│  └──────────────┘  └───────┬───────┘  └──────────────┘ │
│                            │                            │
│         ┌──────────────────┼──────────────────┐        │
│         │                  │                  │        │
│  ┌──────▼──────┐  ┌───────▼───────┐  ┌───────▼──────┐ │
│  │   Item      │  │  BuffDebuff   │  │  MiniGame    │ │
│  │   System    │  │  System       │  │  System      │ │
│  └─────────────┘  └───────────────┘  └──────────────┘ │
│                                                         │
└─────────────────────────────────────────────────────────┘
          │ NetworkVariable (state)
          │ Rpc (events)
          ▼
┌─────────────────────────────────────────────────────────┐
│                    CLIENT                                │
│                                                         │
│  ┌──────────────┐  ┌───────────────┐  ┌──────────────┐ │
│  │   GameUI     │  │  MiniGameUI   │  │  ItemUI      │ │
│  │  (display)   │  │  (input only) │  │  (select)    │ │
│  └──────────────┘  └───────────────┘  └──────────────┘ │
│                                                         │
│  Client sends ONLY: ItemSelect, Ready, MiniGameInput   │
└─────────────────────────────────────────────────────────┘
```

---

## Systems Detail

### 1. MatchManager (NetworkBehaviour)
**Responsibility**: Best-of-3 round lifecycle

| Data | Type | Sync |
|------|------|------|
| Round number (1~3) | int | NetworkVariable |
| P1 round wins | int | NetworkVariable |
| P2 round wins | int | NetworkVariable |
| Match state | enum (InProgress/P1Win/P2Win) | NetworkVariable |

**Flow**:
```
Match Start → Round 1 → (winner) → Round 2 → (winner) → Round 3 (if needed) → Match End
```

**Delegates to**: TurnManager for each round's turn loop.

---

### 2. TurnManager (NetworkBehaviour)
**Responsibility**: Single-round turn state machine

**State Machine**:
```
WaitingForPlayers → PrepPhase → AttackPhase → ResolutionPhase ─→ PrepPhase (loop)
                                                                └→ RoundOver (if winner)
```

| Data | Type | Sync |
|------|------|------|
| Turn phase | enum | NetworkVariable |
| Turn number (within round) | int | NetworkVariable |
| Prep timer | float | NetworkVariable |
| P1 ready | bool | NetworkVariable |
| P2 ready | bool | NetworkVariable |
| P1 ready timestamp | float | server-local |
| P2 ready timestamp | float | server-local |

**Key Logic (server-only)**:
- PrepPhase: tick timer, track temperature decrease, accept item selection + ready input
- AttackPhase: delegate to CombatResolver with ready-order
- ResolutionPhase: apply results, check 0° threshold, trigger threshold item grants

**Client Input (Rpc to Server)**:
- `SelectItemRpc(byte slotIndex)`
- `ReadyRpc()`
- `ConfirmItemUseRpc(byte slotIndex)`

---

### 3. TemperatureSystem (plain C# class, server-only)
**Responsibility**: Temperature state per player

| Data | Sync |
|------|------|
| P1 temperature | NetworkVariable<float> |
| P2 temperature | NetworkVariable<float> |
| P1 fan active | server-local bool |
| P2 fan active | server-local bool |
| Fan decrease rate | config (1°/sec — confirmed) |
| Recovery rate | config (1°/sec — confirmed: "기존 초당 1") |

**Key Methods**:
```csharp
void TickFan(int playerIndex, float deltaTime)  // decrease temp while fan on
void TickRecovery(int playerIndex, float deltaTime)  // recover temp while fan off
void ApplyDamage(int playerIndex, float amount)
void ApplyHeal(int playerIndex, float amount)
bool CheckDeath(int playerIndex)  // temp <= 0
bool CheckThreshold(int playerIndex, float threshold)  // crossed 30/20/10
```

---

### 4. CombatResolver (plain C# class, server-only)
**Responsibility**: Resolve attack phase actions in correct order

**Input**: P1 action, P2 action, ready-order (who pressed first)
**Output**: List of effects to apply

**Resolution Rules** (server logic):
1. Determine execution order (ready-press order, or environment override)
2. Defense always activates regardless of order
3. Execute actions sequentially
4. Check 0° after each action (first to kill wins)
5. Return results for broadcast

```csharp
CombatResult Resolve(PlayerAction p1, PlayerAction p2, int firstPlayerIndex)
```

---

### 5. ItemSystem

#### 5-1. Class Hierarchy

```
IItemEffect (interface)
│   void Execute(ItemContext ctx)
│   bool Validate(ItemContext ctx)
│
ItemData (ScriptableObject) ─ READ ONLY at runtime
│   string itemName
│   string description
│   Sprite icon
│   ItemCategory category        // enum
│   ItemPersistence persistence   // enum: Permanent, BasicConsumable, RandomConsumable
│   float dropWeight              // for random pool
│   bool requiresMiniGame
│   MiniGameType miniGameType
│   IItemEffect effectPrefab      // reference to effect implementation
│
├── AttackItemData ─ float damage
├── DefenseItemData ─ float blockAmount, DamageFilter filter (Temp/Food/All)
├── RecoveryItemData ─ float healAmount, int maxUses (1 for normal, 3 for Smartphone)
├── BuffItemData ─ BuffType type, float value, int duration (turns)
├── DebuffItemData ─ DebuffType type, float value, int duration
├── SabotageItemData ─ SabotageType type (Reroll/Steal/Block/Neutralize)
└── SpecialItemData ─ (unique per item, each has own IItemEffect)
```

#### 5-2. Enums

```csharp
enum ItemCategory
{
    Attack,       // 온도 떨구기
    Defense,      // 온도 유지하기(방어)
    Recovery,     // 회복
    Buff,         // 음식 먹기(버프)
    Debuff,       // 음식 먹이기(디버프)
    Sabotage,     // 사보타주(방해)
    Special       // 특수
}

enum ItemPersistence
{
    Permanent,         // 부채, 바람막이
    BasicConsumable,   // 따뜻한 차, 고양이
    RandomConsumable   // 모든 랜덤 아이템
}

enum MiniGameType
{
    None,
    ScrewTightening,   // 십자드라이버
    CraneGame,         // 집게손
    TargetShoot,       // 물총
    RapidTap,          // 핫팩, 불닭볶음면
    CardPick,          // 타로카드
    TapeTiming,        // 청테이프
    PatternUnlock,     // 스마트폰
    GaugeMatch,        // 탄산음료
    TouchTiming,       // 레드카드
    HugTiming          // 안아줘요 티셔츠
}
```

#### 5-3. Effect Implementations (one class per item behavior)

```
IItemEffect implementations (MonoBehaviour or plain C#):
│
├── DirectDamageEffect        // 부채, 아이스크림, 물총, 손풍기, 아.아 (values TBD)
├── DirectHealEffect          // 따뜻한 차, 핫팩, 뜨.아 (values TBD)
├── MultiUseHealEffect        // 스마트폰 (3 uses, escalating — values TBD)
├── ShieldEffect              // 바람막이 (block temp attacks 100%)
├── FoodShieldEffect          // 마스크 (block food effects 100%)
├── DelayedDebuffEffect       // 삼계탕 (opp temp ↑ now, ↓ next turn — values TBD)
├── DelayedBuffEffect         // 불닭볶음면 (large self ↑ next turn — value TBD)
├── SplitBuffEffect           // 탄산음료 (self ↓ now, ↑ next turn — values TBD)
├── RerollEffect              // 고양이 (reroll all opponent random items)
├── StealEffect               // 집게손 (steal 1 opponent item, slot refills)
├── BlockBasicEffect          // 청테이프 (block opponent basic items next turn)
├── NeutralizeEffect          // 레드카드 (cancel opponent action this turn)
├── FanSabotageEffect         // 십자드라이버 (opponent fan → 2°/sec)
├── RevealEffect              // 타로카드 (reveal + extra action, after opponent Ready)
├── TempEqualizeEffect        // 안아줘요 티셔츠 (opponent temp = my temp)
```

#### 5-4. ItemInventory (per player, server-managed)

```csharp
class ItemInventory
{
    ItemSlot[] basicSlots;          // 4 slots (2 permanent + 2 consumable)
    ItemSlot[] randomSlots;         // up to 8 slots
    bool[] thresholdGranted;        // [30°, 20°, 10°] already granted?
    
    void GrantRandomItems(int count)
    void ConsumeItem(int slotIndex)
    void RerollAllRandom()          // for Cat item
    ItemData GetItem(int slotIndex)
    int GetRandomItemCount()
}
```

---

### 6. BuffDebuffSystem (plain C# class, server-only)
**Responsibility**: Track and apply multi-turn effects

```csharp
class ActiveEffect
{
    EffectType type;
    float value;
    int remainingTurns;
    int sourcePlayerIndex;
}

class BuffDebuffSystem
{
    List<ActiveEffect> p1Effects;
    List<ActiveEffect> p2Effects;
    
    void AddEffect(int targetPlayer, ActiveEffect effect)
    void TickTurnStart(int playerIndex)  // apply effects, decrement duration
    void ClearAll(int playerIndex)       // on round reset
}
```

---

### 7. MiniGameSystem (NetworkBehaviour)
**Responsibility**: Mini-game lifecycle management

**Flow**:
```
Client selects item requiring mini-game
  → Server validates and starts mini-game timer
  → Client displays mini-game UI
  → Client sends input (tap count, timing results)
  → Server validates result (anti-cheat)
  → Success: item effect queued
  → Failure: item destroyed, client selects again
```

| Data | Sync |
|------|------|
| Active mini-game type | Rpc (server → client) |
| Mini-game timer | NetworkVariable |
| Mini-game result | Rpc (client → server) |

**Server validates**: tap count within human limits, timing within acceptable window, completion within time limit.

---

### 8. EnvironmentSystem (plain C# class, server-only)
**Responsibility**: Random environment modifier per round

```csharp
enum EnvironmentType
{
    None,
    SunnyDay,       // recovery rate 2x
    CoolBreeze,     // recovery rate 0
    CicadaSong,     // audio/visual distraction
    Kids,           // steal 1 unused random item
    Ambulance,      // heal lower-temp player at turn 4
    SummerVacation, // prep time = 10s
    HeatWaveWarning // lower-temp acts first
}
```

- Selected randomly at round start
- Revealed to players at 2nd prep phase start
- Modifies relevant system parameters (fan rate, prep duration, combat order)

---

## Data Flow: One Full Turn

```
1. [Server] TurnManager → PrepPhase
   ├── TemperatureSystem.TickFan() every frame
   ├── EnvironmentSystem modifiers applied
   └── BuffDebuffSystem.TickTurnStart()

2. [Client] Player selects item → SelectItemRpc(slotIndex)
   [Server] ItemSystem validates selection, stores

3. [Client] Player presses Ready → ReadyRpc()
   [Server] TurnManager records ready + timestamp
   [Server] TemperatureSystem switches to recovery mode for that player

4. [Server] Both ready OR timer expires → AttackPhase
   ├── Determine execution order (timestamp or environment override)
   ├── CombatResolver.Resolve(p1Action, p2Action, firstPlayer)
   │   ├── Check defense first (always activates)
   │   ├── Execute first player's item effect
   │   ├── Check 0° threshold
   │   ├── Execute second player's item effect
   │   └── Check 0° threshold
   ├── TemperatureSystem applies all changes
   └── BuffDebuffSystem queues new effects

5. [Server] → ResolutionPhase
   ├── Broadcast results via Rpc
   ├── Check threshold items (30°/20°/10°)
   │   └── ItemSystem.GrantRandomItems() if threshold crossed
   ├── Check round over (any player 0°)
   │   └── MatchManager.RecordRoundResult()
   └── Transition: next PrepPhase or RoundOver

6. [Client] UI updates from NetworkVariable callbacks + result Rpc
```

---

## File Structure (Planned)

```
Assets/Scripts/
├── Core/
│   ├── Match/
│   │   └── MatchManager.cs              # Bo3 round lifecycle
│   ├── Turn/
│   │   └── TurnManager.cs               # Turn state machine
│   ├── Combat/
│   │   ├── CombatResolver.cs            # Action resolution logic
│   │   └── TemperatureSystem.cs         # Temperature state
│   ├── Item/
│   │   ├── Data/
│   │   │   ├── ItemData.cs              # Base ScriptableObject
│   │   │   ├── AttackItemData.cs
│   │   │   ├── DefenseItemData.cs
│   │   │   ├── RecoveryItemData.cs
│   │   │   ├── BuffItemData.cs
│   │   │   ├── DebuffItemData.cs
│   │   │   ├── SabotageItemData.cs
│   │   │   └── SpecialItemData.cs
│   │   ├── Effects/
│   │   │   ├── IItemEffect.cs           # Interface
│   │   │   ├── DirectDamageEffect.cs
│   │   │   ├── DirectHealEffect.cs
│   │   │   ├── ShieldEffect.cs
│   │   │   ├── DelayedDebuffEffect.cs
│   │   │   ├── RerollEffect.cs
│   │   │   └── ... (one per unique behavior)
│   │   ├── ItemInventory.cs             # Per-player inventory
│   │   ├── ItemDropTable.cs             # Weighted random selection
│   │   └── ItemEnums.cs                 # ItemCategory, ItemPersistence
│   ├── Buff/
│   │   └── BuffDebuffSystem.cs          # Effect tracking
│   ├── MiniGame/
│   │   ├── MiniGameSystem.cs            # Lifecycle manager
│   │   ├── IMiniGameValidator.cs        # Server-side validation interface
│   │   └── Validators/
│   │       ├── TapCountValidator.cs
│   │       ├── TimingValidator.cs
│   │       └── ...
│   ├── Environment/
│   │   ├── EnvironmentSystem.cs         # Modifier selection + application
│   │   └── EnvironmentEnums.cs
│   └── Network/
│       ├── LobbyManager.cs             # (existing)
│       ├── RelayManager.cs             # (existing)
│       ├── SessionManager.cs           # (existing)
│       └── PlayerSpawnManager.cs       # (existing)
├── UI/
│   ├── Game/
│   │   ├── GameUI.cs                   # Main game HUD
│   │   ├── ItemSlotUI.cs               # Single item slot display
│   │   ├── TemperatureBarUI.cs         # Temperature gauge
│   │   └── CombatResultUI.cs           # Attack phase result display
│   ├── MiniGame/
│   │   ├── MiniGameUIBase.cs           # Abstract mini-game UI
│   │   ├── TapMiniGameUI.cs
│   │   ├── TimingMiniGameUI.cs
│   │   └── ...
│   └── Lobby/
│       └── AZLobbyUI.cs               # (existing)
└── Common/
    └── Enums/
        └── TurnEnums.cs                # TurnPhase, ActionType (existing)
```

---

## Interface Contracts

```csharp
// Item effect - executed server-side only
interface IItemEffect
{
    void Execute(ItemContext ctx);
    bool CanExecute(ItemContext ctx);
}

// Context passed to item effects
struct ItemContext
{
    int userPlayerIndex;
    int opponentPlayerIndex;
    TemperatureSystem tempSystem;
    BuffDebuffSystem buffSystem;
    ItemInventory userInventory;
    ItemInventory opponentInventory;
    EnvironmentType activeEnvironment;
}

// Mini-game validation - server-side only
interface IMiniGameValidator
{
    bool Validate(MiniGameResult clientResult);
    float GetTimeLimit();
}

// Temperature modification target
interface ITemperatureTarget
{
    float Temperature { get; }
    void ApplyDamage(float amount);
    void ApplyHeal(float amount);
    bool IsDead { get; }
}
```
