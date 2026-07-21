# Mini-Game System — Complete Reference

> Last updated: 2026-07-21 (feature/minigame-batch2 branch)
> Scope: 7 mini-games (Batch 1: 3, Batch 2: 4) + common feedback + manager changes

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Data Flow (Server ↔ Client)](#2-data-flow)
3. [MiniGameType Enum ↔ Item SO Mapping](#3-enum-mapping)
4. [File Inventory](#4-file-inventory)
5. [Common Base: MiniGameUIBase](#5-common-base)
6. [Batch 1 Mini-Games (3)](#6-batch-1)
7. [Batch 2 Mini-Games (4)](#7-batch-2)
8. [MiniGameArt — Procedural Sprites](#8-minigame-art)
9. [MiniGameHub — Orchestrator](#9-minigame-hub)
10. [Manager & Infrastructure Changes](#10-manager-changes)
11. [Support Systems](#11-support-systems)
12. [Rollback History](#12-rollback)

---

## 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│  SERVER (Host)                                                      │
│                                                                     │
│  PlayerState                                                        │
│    ├─ SelectItemServerRpc()     ← client selects item               │
│    │    └─ if RequiresMiniGame → record _pendingMiniGameSlot        │
│    │                           → StartMiniGameClientRpc [→ Owner]   │
│    │                                                                │
│    └─ SubmitMiniGameResultServerRpc()  ← client sends result        │
│         ├─ success=true  → ServerQueueItem() (item queued)          │
│         └─ success=false → ConsumeItem() + CompactSlots() (1 use)  │
│                                                                     │
│  Validation:                                                        │
│    - _pendingMiniGameSlot must match submitted slotIndex             │
│    - Must still be PrepPhase                                        │
│    - ServerTime < _pendingMiniGameDeadline                           │
│    - IsReady must be false                                          │
└──────────────────────────────────────────────────────────────────────┘
         │                              ▲
         │ [Rpc(SendTo.Owner)]          │ [Rpc(SendTo.Server)]
         ▼                              │
┌──────────────────────────────────────────────────────────────────────┐
│  CLIENT (Owner)                                                      │
│                                                                      │
│  PlayerState.OnMiniGameStart event                                   │
│    ▼                                                                 │
│  MiniGameHub.HandleStart()                                           │
│    ├─ Recalculate budget = Min(timeLimit, Max(0.5, prepRemain))      │
│    ├─ Switch on MiniGameType → instantiate specific UI component     │
│    └─ Call MiniGameUIBase.Begin(slotIndex, budget, goal, canvas)      │
│         ▼                                                            │
│  [Specific MiniGame UI] — all judgment is client-side                │
│    ├─ BuildContent() — construct UI elements                         │
│    ├─ OnTick() / input handlers — game logic                         │
│    └─ Finish(success) → fires OnFinished event                       │
│         ▼                                                            │
│  MiniGameHub.HandleFinished()                                        │
│    ├─ PlayerState.SubmitMiniGameResultServerRpc(slot, success)        │
│    └─ MiniGameHub.OnFinishedLocal event → AZGameUI status update     │
└──────────────────────────────────────────────────────────────────────┘
```

**Key design decisions:**
- All judgment happens on client (no server-side mini-game logic)
- Server only validates: timing (deadline), phase (PrepPhase), and slot match
- `OnFinished` fires immediately on judgment — result outro animation is async and does not delay server submission
- Grace period: `MINIGAME_GRACE_SEC = 0.5f` added to deadline for network latency

---

## 2. Data Flow

### Server → Client (Start)

```
PlayerState.SelectItemServerRpc(slotIndex)
  │
  ├─ Reads ItemDataSO from inventory
  │   └─ RequiresMiniGame == true?
  │       ├─ Record: _pendingMiniGameSlot = slotIndex
  │       ├─ Calculate deadline:
  │       │   prepEnd = PrepStartServerTime + PrepDuration
  │       │   deadline = Min(now + MiniGameTimeLimit, prepEnd) + 0.5s
  │       └─ StartMiniGameClientRpc(slotIndex, MiniGameType, timeLimit, goal)
  │
  └─ RequiresMiniGame == false → normal ServerQueueItem flow
```

### Client → Server (Result)

```
MiniGameUIBase.Finish(success)
  │
  ├─ Set _finished = true (prevents double-finish)
  ├─ Fire OnFinished(slotIndex, success) ← immediate server submission
  └─ Start ResultOutroRoutine(success) ← visual only, async
        │
        MiniGameHub.HandleFinished(slotIndex, success)
          ├─ PlayerState.SubmitMiniGameResultServerRpc(slotIndex, success)
          └─ MiniGameHub.OnFinishedLocal.Invoke(slotIndex, success)
```

### Server Validation on Result

```
SubmitMiniGameResultServerRpc(slotIndex, success)
  │
  ├─ REJECT if _pendingMiniGameSlot != slotIndex
  ├─ REJECT if CurrentPhase != PrepPhase
  ├─ REJECT if IsReady == true
  ├─ REJECT if ServerTime > _pendingMiniGameDeadline
  │
  ├─ success == false:
  │   └─ ConsumeItem(slotIndex) → CompactSlots()
  │      (1 use consumed even on failure)
  │
  └─ success == true:
      └─ Re-validate slot → CanUse() check → ServerQueueItem()
```

---

## 3. MiniGameType Enum ↔ Item SO Mapping

### Enum Definition (`ItemEnums.cs`)

```csharp
public enum MiniGameType : byte
{
    None,           // 0
    HitTargets,     // 1  ← Water Gun        [Batch 2]
    HugCharacter,   // 2  ← (not implemented)
    PatternUnlock,  // 3  ← Smartphone       [Batch 2]
    TapRepeat,      // 4  ← Hot Pack         [Batch 1]
    GaugeMatch,     // 5  ← (not implemented)
    BoilWater,      // 6  ← Buldak Noodles   [Batch 1]
    TightenScrews,  // 7  ← Screwdriver      [Batch 1]
    PickCard,       // 8  ← (not implemented)
    ClawGrab,       // 9  ← Claw Machine     [Batch 2]
    TimingCut,      // 10 ← Blue Tape        [Batch 2]
    TapCard,        // 11 ← Red Card (RequiresMiniGame=1 in SO)
    FanBoost        // 12 ← (not implemented)
}
```

### Item SO Configuration

| Item Name      | SO Path                            | Category | MiniGameType   | TimeLimit | Goal | RequiresMiniGame |
|----------------|-------------------------------------|----------|----------------|-----------|------|------------------|
| Water Gun      | `Data/Items/Random/Attack/WaterGun.asset`       | Attack   | 1 (HitTargets)    | 5s  | 3    | true  |
| Smartphone     | `Data/Items/Random/Recovery/Smartphone.asset`   | Recovery | 3 (PatternUnlock) | 5s  | 1    | true  |
| Hot Pack       | `Data/Items/Random/Recovery/HotPack.asset`      | Recovery | 4 (TapRepeat)     | 7s  | 15   | true  |
| Buldak Noodles | `Data/Items/Random/Buff/BuldakNoodles.asset`    | Buff     | 6 (BoilWater)     | 10s | 25   | true  |
| Screwdriver    | `Data/Items/Random/Special/Screwdriver.asset`   | Special  | 7 (TightenScrews) | 7s  | 3    | true  |
| Claw Machine   | `Data/Items/Random/Sabotage/ClawMachine.asset`  | Sabotage | 9 (ClawGrab)      | 7s  | 1    | true  |
| Blue Tape      | `Data/Items/Random/Sabotage/BlueTape.asset`     | Sabotage | 10 (TimingCut)    | 5s  | 1    | true  |
| Red Card       | `Data/Items/Random/Sabotage/RedCard.asset`      | Sabotage | 11 (TapCard)      | 5s  | 1    | true  |

> **Unimplemented types** (2, 5, 8, 11, 12): MiniGameHub returns `null` from the switch → auto-fail + `SubmitMiniGameResultServerRpc(slotIndex, false)`.

### ItemDataSO Mini-Game Fields (`ItemDataSO.cs`)

```csharp
[Header("Mini-Game")]
public bool RequiresMiniGame;       // enables mini-game flow
public MiniGameType MiniGameType;   // which game to launch
public float MiniGameTimeLimit;     // seconds (budget capped by PrepPhase remaining)
public int MiniGameGoal = 1;        // target count (taps, targets, screws, etc.)
```

---

## 4. File Inventory

### New Files (Batch 2)

| File | Lines | Purpose |
|------|-------|---------|
| `Scripts/UI/MiniGame/WaterGunMiniGameUI.cs` | 132 | Water gun — tap moving targets |
| `Scripts/UI/MiniGame/ClawGrabMiniGameUI.cs` | 167 | Claw grab — timing grab |
| `Scripts/UI/MiniGame/TapeCutMiniGameUI.cs` | 152 | Tape cut — timing bar |
| `Scripts/UI/MiniGame/PatternUnlockMiniGameUI.cs` | 194 | Pattern unlock — drag draw |
| `Scripts/UI/MiniGame/MiniGameArt.cs` | 78 | Procedural Circle/Arc sprites |
| `Scripts/Core/Game/DebugItemGranter.cs` | 75 | F1~F8 cheat key (restored) |

### Modified Files

| File | Change |
|------|--------|
| `Scripts/UI/MiniGame/MiniGameUIBase.cs` (356 lines) | Banner band, PlayConfirmPop, ResultOutro, CreateCircle, CreateIconTapTarget |
| `Scripts/UI/MiniGame/MiniGameHub.cs` (135 lines) | 4 new cases in switch |
| `Scripts/Core/Player/PlayerInventory.cs` | `GrantSpecificItem()` method added |
| `Scripts/Core/Network/PlayerSpawnManager.cs` | Null Transform checks in `GetSpawnPosition()` |
| `Scripts/UI/Game/AZGameUI.cs` | Hub + Granter runtime creation, event wiring, IsRunning guards |
| `Data/Items/Random/Attack/WaterGun.asset` | MiniGameType=1, Goal=3, Time=5 |
| `Data/Items/Random/Sabotage/ClawMachine.asset` | MiniGameType=9, Time=7 |
| `Data/Items/Random/Sabotage/BlueTape.asset` | MiniGameType=10, Time=5 |
| `Data/Items/Random/Recovery/Smartphone.asset` | MiniGameType=3, Time=5 |

---

## 5. Common Base: MiniGameUIBase

**File:** `Scripts/UI/MiniGame/MiniGameUIBase.cs` (356 lines)
**Namespace:** `AbsoluteZero.UI.MiniGame`

### Lifecycle

```
Begin(slotIndex, timeLimit, goal, canvasRoot)
  ├─ Store SlotIndex, Goal, _timeLimit
  ├─ BuildFrame(canvasRoot)         ← dim overlay + timer line + content container
  ├─ BuildContent(Content)          ← abstract, each game implements
  └─ StartCoroutine(BannerRoutine)  ← WarioWare-style text banner

Update()
  ├─ Track _elapsed time
  ├─ Update timer fill (color lerp + pulse)
  ├─ If elapsed >= timeLimit → Finish(false)  ← timeout = fail
  └─ Call OnTick(dt)                ← virtual, each game overrides

Finish(success)
  ├─ Set _finished = true (idempotent guard)
  ├─ Fire OnFinished(slotIndex, success)     ← immediate
  └─ StartCoroutine(ResultOutroRoutine)      ← async visual

ForceCancel()
  └─ Finish(false)  ← called when phase changes away from PrepPhase
```

### Frame Structure (BuildFrame)

```
Root (RectTransform, full screen)
  ├─ Dim (Image, black 40% opacity, full screen)
  │   └─ raycastTarget = true ← blocks clicks to game UI below
  ├─ TimerLineBg (top bar, 12px tall, black 60%)
  │   └─ TimerLineFill (Filled Horizontal, yellow→red lerp)
  │       └─ Last 1.5s: alpha PingPong 0.55~1.0 at 6Hz
  └─ Content (RectTransform, 900×700, centered)
      └─ Each game builds its elements here
```

### Banner System (BannerRoutine)

WarioWare-style text announcement at game start.

```
Elements (all raycastTarget=false, don't block gameplay):
  ├─ Band:       4000×210px, black 60% ← readability over busy content
  ├─ TopLine:    620×8px, warm yellow
  ├─ Label:      82pt Bold white, game's BannerText
  └─ BottomLine: 620×8px, warm yellow

Animation:
  1. Slide in from left (-1500px → 0) per element
     - Duration: 0.13s each, 0.06s stagger, smoothstep easing
  2. Hold: 0.55s
  3. Slide out to right (0 → +1500px), same timing
  4. Destroy banner object
```

> **Bug fix**: Band added because "쏘아라!" was invisible over red target circles. The semi-transparent black band suppresses background noise.

### PlayConfirmPop (Achievement Feedback)

```csharp
protected void PlayConfirmPop(Vector2 pos, float diameter = 140f, Color? color = null)
```

- **Default color**: ConfirmGreen `(0.35, 0.95, 0.45, 0.85)`
- **Miss color** (passed explicitly): `(1.0, 0.3, 0.25, 0.7)` red
- **Animation**: CreateCircle at pos → 0.3s scale 0.55→1.4 + alpha 1→0 → Destroy
- **Applied to all 7 games** at every success/fail moment

### Result Outro (ResultOutroRoutine)

```
1. Input blocker (full-screen transparent Image)
2. Result band (4000×170px, black 55%)
3. Result text:
   - Success: "성공!" 84pt Bold, green (0.45, 1.0, 0.5)
   - Failure: "실패…" 84pt Bold, red (1.0, 0.4, 0.35)
4. Pop-in: 0.16s, scale 0.4→1.12 (overshoot) → 1.0
5. Hold: 0.45s
6. Fade out: 0.18s, band + label alpha → 0
7. Destroy(gameObject)
```

> Server submission happens BEFORE this animation starts. The outro is purely visual.

### Shared Builder Methods

| Method | Signature | Purpose |
|--------|-----------|---------|
| `CreateText` | `(parent, name, pos, size, text, fontSize)` → `TMP_Text` | Creates TMP element with `UiFont.Apply()` for Korean support |
| `CreatePanel` | `(parent, name, pos, size, color)` → `Image` | Creates colored rectangle |
| `CreateCircle` | `(parent, name, pos, diameter, color)` → `Image` | Circle with `MiniGameArt.Circle()` sprite + color tint |
| `CreateIconTapTarget` | `(parent, name, pos, hitSize, iconSize, sprite, color)` → `(Button, Image)` | Transparent hit area (generous) + child icon visual |

---

## 6. Batch 1 Mini-Games (3)

### 6.1 HotPackMiniGameUI — "탭해라!" (TapRepeat)

**File:** `Scripts/UI/MiniGame/HotPackMiniGameUI.cs` (53 lines)

| Property | Value |
|----------|-------|
| Banner | "탭해라!" |
| Input | Button.onClick on icon tap target |
| Win condition | Tap count ≥ Goal (default 15) |
| Fail condition | Timeout only |

**Mechanics:**
- Item icon (Hot Pack sprite via `GameSprites.GetItemSprite`) as tap target
- Hit area: 400×400px (generous), icon visual: 250×250px
- Color lerp: ColdColor(grey) → HotColor(red) as taps increase
- Counter text: `"{taps} / {Goal}"` below icon
- On goal reached: `PlayConfirmPop(340px)` + `Finish(true)`

### 6.2 BuldakMiniGameUI — "탭해라!" (BoilWater)

**File:** `Scripts/UI/MiniGame/BuldakMiniGameUI.cs` (73 lines)

| Property | Value |
|----------|-------|
| Banner | "탭해라!" |
| Input | Button.onClick on icon tap target |
| Win condition | Tap count ≥ Goal (default 25) |
| Fail condition | Timeout only |

**Mechanics:**
- Pot icon (Buldak Noodles sprite) as tap target, 380×340 hit / 230×200 icon
- Fill gauge: Filled Horizontal Image (396×20), color GaugeCool(orange) → GaugeHot(red)
- Percent text below gauge
- Pot color lerp: PotCold(grey) → PotBoiling(red)
- On goal: gauge turns green `(0.35, 0.95, 0.45)` + `PlayConfirmPop(320px)` + `Finish(true)`

### 6.3 ScrewdriverMiniGameUI — "조여라!" (TightenScrews)

**File:** `Scripts/UI/MiniGame/ScrewdriverMiniGameUI.cs` (147 lines)

| Property | Value |
|----------|-------|
| Banner | "조여라!" |
| Input | IDragHandler (circular drag) |
| Win condition | 3 screws each rotated Goal×360° |
| Fail condition | Timeout only |

**Mechanics:**
- 3 screws at x = -190 / 0 / +190, each 140×140px
- Cross-slot visual: vertical 20×108 + horizontal 108×20 dark panels
- Borders: Pending(dark) → Active(yellow) → Done(green)
- Drag input: calculates angle delta from pointer position relative to screw center
- Only counter-clockwise (delta < 0) accumulates rotation
- Per-screw completion: `PlayConfirmPop(210px)` + border → green
- After 3rd screw: `Finish(true)`

---

## 7. Batch 2 Mini-Games (4)

### 7.1 WaterGunMiniGameUI — "쏘아라!" (HitTargets)

**File:** `Scripts/UI/MiniGame/WaterGunMiniGameUI.cs` (132 lines)

| Property | Value |
|----------|-------|
| Banner | "쏘아라!" |
| Input | IPointerDownHandler via inner `HitRelay` class |
| Win condition | Hit all Goal targets (default 3) |
| Fail condition | Timeout only (missed taps = no penalty) |
| Movement area | ±380×210px |

**Visual structure per target:**
```
Outer circle (100px, TargetRed)
  ├─ Ring (64px, white)
  └─ Core (30px, TargetRed)
```

**Mechanics:**
- Each target has random velocity (240~330 px/s, random direction)
- Wall bounce: velocity component flips when hitting area boundary, position clamped
- **IPointerDown (not onClick)**: fast-moving targets need instant hit detection; press-release misses
- Inner class `HitRelay : MonoBehaviour, IPointerDownHandler` receives tap → calls `OnTargetHit`

**Hit feedback:**
1. Mark `target.Hit = true`, increment counter
2. `PlayConfirmPop(150px)` at target position
3. `HitFlashRoutine`: Outer/Core → HitGreen, scale 1.0→1.3 over 0.16s, then `SetActive(false)`
4. Counter text updates: `"{hits} / {Goal}"`

### 7.2 ClawGrabMiniGameUI — "잡아라!" (ClawGrab)

**File:** `Scripts/UI/MiniGame/ClawGrabMiniGameUI.cs` (167 lines)

| Property | Value |
|----------|-------|
| Banner | "잡아라!" |
| Input | Button.onClick (full-screen tap area) |
| Win condition | Grab doll (claw center within GrabTolerance of doll) |
| Fail condition | Miss = instant fail (1 attempt only) |
| Swing range | ±280px, speed 320px/s |
| Descend speed | 640px/s |
| Grab tolerance | 60px |

**State machine:**
```
Swinging ──(tap)──→ Descending
                        │
            ┌───────────┼───────────┐
            ▼                       ▼
     pos within tolerance    pos outside tolerance
            │                       │
      Lifting (success)      Missed (fail)
       ├─ Doll → SetParent(claw)   ├─ Arms → red
       ├─ Arms → green             ├─ ConfirmPop red (140px)
       ├─ ConfirmPop (170px)       └─ 0.4s delay → Finish(false)
       └─ Rise to ClawTopY
          → Finish(true)
```

**Claw visual structure:**
```
Claw root (RectTransform)
  ├─ Rope:     6×460px, MetalDark
  ├─ BodyTop:  44px circle, MetalLight
  ├─ BodyMid:  50×44px rect, MetalLight    ← _clawBody (color changes)
  ├─ BodyBot:  52px circle, MetalMid
  ├─ BoltL/R:  8px circles, MetalDark
  └─ Arms[3]:  MiniGameArt.Arc() sprite, angles 25°/0°/-25°
      ├─ Pivot: (0.5, 1.0) top-center — hangs from body
      ├─ Size: 30×100px
      └─ Tip: 14px circle at bottom of each arm
```

**Doll:** Pink circle (104px) + 2 black eyes (14px each) at (-19, 12) and (19, 12)

**Y positions:** ClawTopY=175, DollY=-130, JudgeY=DollY+52=-78

### 7.3 TapeCutMiniGameUI — "끊어라!" (TimingCut)

**File:** `Scripts/UI/MiniGame/TapeCutMiniGameUI.cs` (152 lines)

| Property | Value |
|----------|-------|
| Banner | "끊어라!" |
| Input | Button.onClick (full-screen tap area) |
| Win condition | Tap when cursor is within green zone |
| Fail condition | Tap outside green zone = instant fail |
| Cursor speed | 430px/s |
| Green zone width | ±46px (92px total) |
| Green zone position | Random in [-150, 150] |

**Visual structure:**
```
Content
  ├─ Tape Roll (left, x=-280)
  │   ├─ Roll: 90px circle, RollColor blue
  │   │   ├─ Hole: 28px circle, dark
  │   │   └─ Sheen: 14×50px white 15% opacity
  │   └─ Animation: rotate -120°/s, scale 1.0→0.75 over 4s
  │
  ├─ Tape (pulls from roll rightward)
  │   ├─ Pivot: (0, 0.5) left-aligned
  │   ├─ Width: 40px → 480px over 4 seconds (Lerp)
  │   ├─ Color: TapeBlue
  │   ├─ Sheen: top 65%~85% stripe, white 20%
  │   └─ End: 14px torn edge, TapeDark
  │
  ├─ Timing Bar (y=-150)
  │   ├─ Background: 520×26px, black 65%
  │   ├─ Green Zone: 92×22px at random X
  │   └─ Cursor: 8×38px white, PingPong ±250px
  │
  └─ Tap Area: 1920×1080 transparent button
```

**Judgment (1 attempt):**
- `|cursor.x - greenCenterX| <= GreenHalfWidth(46)` → success
- Success: cursor + zone → bright green + `PlayConfirmPop(150px)` + `Finish(true)`
- Failure: cursor → red + `PlayConfirmPop red(120px)` + `Finish(false)`

### 7.4 PatternUnlockMiniGameUI — "그려라!" (PatternUnlock)

**File:** `Scripts/UI/MiniGame/PatternUnlockMiniGameUI.cs` (194 lines)

| Property | Value |
|----------|-------|
| Banner | "그려라!" |
| Input | IDragHandler + IPointerDownHandler + IPointerUpHandler via `DragRelay` |
| Win condition | Complete pattern by connecting all dots in order |
| Fail condition | Wrong dot = instant fail |
| Snap radius | 46px |
| Grid spacing | 110px |

**Pattern pool (random selection):**
```
Index layout (row-major):  0  1  2
                           3  4  5
                           6  7  8

ㄱ pattern: [0, 1, 2, 5, 8]         (right then down)
ㄴ pattern: [0, 3, 6, 7, 8]         (down then right)
Z pattern:  [0, 1, 2, 4, 6, 7, 8]   (right, diagonal, right)
```

**Visual structure:**
```
Content
  ├─ Preview (top, y=210)
  │   ├─ Mini grid: spacing=34px
  │   ├─ Answer lines: 5px, warm yellow
  │   └─ 9 mini dots: 11px, light grey
  │
  ├─ Main Grid (center, y=-60)
  │   └─ 9 dots: 36px circles, DotIdle grey
  │       Colors: Idle(grey) → Connected(cyan) → Wrong(red) → Success(green)
  │
  └─ Drag Surface: 1920×1080 transparent, DragRelay component
```

**Connection logic:**
```
HandlePointer(eventData):
  1. Convert screen pos → grid local pos
  2. Find nearest dot within SnapRadius (46px)
  3. TryConnect(dotIndex):
     ├─ Already connected? → ignore (re-traversal OK)
     ├─ Matches _answer[_progress]?
     │   ├─ Yes: mark connected, cyan color, draw line (9px),
     │   │       PlayConfirmPop(80px, cyan tint)
     │   │       If all connected → all green + PlayConfirmPop(380px) + Finish(true)
     │   └─ No:  dot → red, PlayConfirmPop red(90px) → Finish(false)
     └─ (wrong dot = instant fail, no retry on wrong connection)

ResetChain() — on PointerUp:
  - If not finished and progress > 0:
    reset all dots to idle, destroy drawn lines, progress = 0
  - Allows retry within time limit (but only on pointer release, not wrong dot)
```

**Line drawing:**
```csharp
Image DrawLine(parent, from, to, thickness, color)
  // Positioned at midpoint of from→to
  // Width = distance, height = thickness
  // Rotated to angle between points
```

---

## 8. MiniGameArt — Procedural Sprites

**File:** `Scripts/UI/MiniGame/MiniGameArt.cs` (78 lines)

Generates sprites at runtime — no art assets needed for greybox mini-games.

### Circle()

```
Size: 96×96 RGBA32
Shape: Filled circle, center pivot (0.5, 0.5)
Edge: 1px soft edge (alpha = Clamp01(radius - dist + 0.5))
Color: White (tinted via Image.color at usage site)
Filter: Bilinear
Caching: Static field, created once on first call

Used by: All mini-games (dots, targets, confirm pop rings, claw body,
         doll, bolts, tape roll, etc.) — everything circular
```

### Arc()

```
Size: 48×128 RGBA32
Shape: C-curve, open at top, curling inward toward bottom
Pivot: (0.5, 1.0) top-center — hangs from claw body
Curve: curveX = w*0.5 + 12*sin(t*π*0.95)
Thickness: 7px at top → 5px at bottom (taper)
Color: White (tinted to MetalMid at usage site)
Filter: Bilinear
Caching: Static field, created once

Used by: ClawGrabMiniGameUI only — 3 arms at angles 25°/0°/-25°
```

---

## 9. MiniGameHub — Orchestrator

**File:** `Scripts/UI/MiniGame/MiniGameHub.cs` (135 lines)

### Responsibilities

1. **Singleton** lifecycle (`Instance`, `Awake`/`OnDestroy`)
2. **Bind to local player**: polls in `Update()` until `PlayerState` found, subscribes to `OnMiniGameStart`
3. **Launch mini-games**: `HandleStart` — budget calculation + type switch + `Begin()`
4. **Report results**: `HandleFinished` — forwards to `SubmitMiniGameResultServerRpc`
5. **Phase guard**: `HandlePhaseChanged` — `ForceCancel()` if phase leaves PrepPhase
6. **Status query**: `static bool IsRunning` — used by AZGameUI to block item selection / ready

### Type Switch (lines 79–88)

```csharp
_active = type switch
{
    MiniGameType.TapRepeat     => go.AddComponent<HotPackMiniGameUI>(),
    MiniGameType.BoilWater     => go.AddComponent<BuldakMiniGameUI>(),
    MiniGameType.TightenScrews => go.AddComponent<ScrewdriverMiniGameUI>(),
    MiniGameType.HitTargets    => go.AddComponent<WaterGunMiniGameUI>(),
    MiniGameType.ClawGrab      => go.AddComponent<ClawGrabMiniGameUI>(),
    MiniGameType.TimingCut     => go.AddComponent<TapeCutMiniGameUI>(),
    MiniGameType.PatternUnlock => go.AddComponent<PatternUnlockMiniGameUI>(),
    _ => null   // unimplemented → auto-fail
};
```

### Canvas Setup

```
MiniGameCanvas (ScreenSpaceOverlay, sortingOrder=50)
  ├─ CanvasScaler: ScaleWithScreenSize, 1920×1080 reference, match=0.5
  └─ GraphicRaycaster
```

---

## 10. Manager & Infrastructure Changes

### 10.1 PlayerSpawnManager — Null Transform Fix

**File:** `Scripts/Core/Network/PlayerSpawnManager.cs`

**Problem:** Scene transition destroys SpawnPoint Transforms, but `resolvedSpawnPoints` list retains dead references → `NullReferenceException` on next spawn.

**Fix (GetSpawnPosition, lines 240–252):**
```
1. Check resolvedSpawnPoints[index] != null before accessing .position
2. If null → call RefreshResolvedSpawnPoints() to rebuild list
3. Re-check after refresh → if still null → fall back to GetFallbackSpawn()
```

**Also in RefreshResolvedSpawnPoints (line 265–267):**
```
foreach (Transform point in spawnPoints)
    if (point == null) continue;   ← skip destroyed Inspector references
```

### 10.2 PlayerInventory — GrantSpecificItem

**File:** `Scripts/Core/Player/PlayerInventory.cs` (lines 129–148)

```csharp
public bool GrantSpecificItem(short itemId)
```

- **Purpose:** Debug/test item granting that respects the stack model
- **Stack model:** If player already owns the item → add MaxUses to existing slot's RemainingUses (cap 254)
- **New slot:** If not owned → create new slot (returns false if inventory full at MAX_SLOTS=12)
- **Server-only:** `if (!IsServer) return false;`
- **Used by:** `DebugItemGranter` only

### 10.3 DebugItemGranter — Restored

**File:** `Scripts/Core/Game/DebugItemGranter.cs` (75 lines)

- **Deleted during merge** — restored with stack model compliance
- **Conditional compilation:** `#if UNITY_EDITOR || DEVELOPMENT_BUILD`
- **Host-only:** `if (!nm.IsServer) return;`
- **Grants to ALL players:** iterates `FindObjectsByType<PlayerInventory>`

**Key bindings:**

| Key | Item | Category | Mini-Game |
|-----|------|----------|-----------|
| F1 | Hot Pack | Recovery | TapRepeat (B1) |
| F2 | Buldak Noodles | Buff | BoilWater (B1) |
| F3 | Screwdriver | Special | TightenScrews (B1) |
| F4 | Random | — | GrantRandomItems(1) |
| F5 | Water Gun | Attack | HitTargets (B2) |
| F6 | Claw Machine | Sabotage | ClawGrab (B2) |
| F7 | Blue Tape | Sabotage | TimingCut (B2) |
| F8 | Smartphone | Recovery | PatternUnlock (B2) |

### 10.4 AZGameUI — Hub & Granter Wiring

**File:** `Scripts/UI/Game/AZGameUI.cs`

**Runtime object creation (lines 86–92):**
```csharp
var hubGO = new GameObject("MiniGameHub");
hubGO.AddComponent<MiniGameHub>();
MiniGameHub.OnFinishedLocal += OnMiniGameFinished;

var debugGranterGO = new GameObject("DebugItemGranter");
debugGranterGO.AddComponent<Core.Game.DebugItemGranter>();
```

**Event handling:**
- `OnMiniGameFinished(slot, success)` → updates status text ("Mini-game clear!" / "Mini-game failed...")
- `MiniGameHub.IsRunning` check in `OnItemClicked` (line 376) and `OnReadyClicked` (line 405) — prevents item selection and ready press during active mini-game
- Cleanup: `MiniGameHub.OnFinishedLocal -= OnMiniGameFinished` in `OnDestroy` (line 160)

---

## 11. Support Systems

### 11.1 UiFont — Korean Font Loader

**File:** `Scripts/UI/Common/UiFont.cs` (58 lines)

- Searches OS installed fonts for Korean typeface: malgun → nanumgothic → gulim → batang
- Creates `TMP_FontAsset` at runtime via `TMP_FontAsset.CreateFontAsset(new Font(path))`
- Registers as TMP global fallback font
- Static caching with retry-once guard
- Called via `UiFont.Apply(tmp)` in `MiniGameUIBase.CreateText()` — prevents □ rendering for Korean characters

### 11.2 GameSprites — Sprite Sheet Accessor

**File:** `Scripts/Core/Common/GameSprites.cs` (64 lines)

- Loads `Resources/objtest1` sprite sheet on first access
- `GetItemSprite(itemName)` maps item names to sprite names
- Used by Batch 1 games (HotPack, BuldakNoodles) for icon display
- **Batch 2 games do NOT use GameSprites** — they use `MiniGameArt` procedural sprites or build geometry directly

---

## 12. Rollback History

**Context:** Quality pass on 2026-07-20 added decorative polish (glow, steam, shakes, etc.) across all 7 games. User ordered rollback: "장식 연출 전반 롤백" / "간단하고 직관적인 구현에 성취 피드백만"

**Removed (per PLAN_011):**
- Glow effects, steam particles
- Tap punch animations, counter punch
- Phone frame decoration
- Doll shadow
- Tape split animation
- Prong grip animation
- Progress bar (beyond what each game needs)
- Screw sink depth effect
- Live drag line (pattern game)

**Kept:**
- `PlayConfirmPop` (green success / red fail rings) — all 7 games
- Banner readability band (black semi-transparent)
- Result outro ("성공!/실패…" pop + fade)
- Timer line color lerp + last-1.5s pulse
- Per-game core feedback (hit flash, claw arm color, gauge green, etc.)

---

## Quick Reference: Adding a New Mini-Game

1. **Define enum value** in `ItemEnums.cs` → `MiniGameType`
2. **Create UI class** inheriting `MiniGameUIBase`:
   - Override `BannerText` (string)
   - Override `BuildContent(RectTransform content)` — build UI elements
   - Override `OnTick(float dt)` — per-frame logic (optional)
   - Call `Finish(true/false)` when judgment is made
   - Use `PlayConfirmPop()` for achievement feedback
3. **Register in MiniGameHub** switch statement (line 79)
4. **Configure Item SO**: set `RequiresMiniGame=true`, `MiniGameType`, `MiniGameTimeLimit`, `MiniGameGoal`
5. **(Optional)** Add F-key to `DebugItemGranter` for testing
6. **(Optional)** Add procedural sprite to `MiniGameArt` if needed
