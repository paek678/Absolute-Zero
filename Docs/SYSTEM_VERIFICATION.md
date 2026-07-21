# System Verification Report — 2026-07-21

> Full flow verification of Environment Variable system, Mini-Game system, and Item Animation system.
> Covers: implementation status, flow correctness, detected issues, and missing resources.

---

## 1. Environment Variable System

### 1.1 Flow Overview

```
Turn 1 Resolution (no environment yet)
  └─ EnvironmentAnnouncementRoutine() [TurnManager, server]
       ├─ Random select from 6 types → ActiveEnvironment.Value
       ├─ AnnounceEnvironmentClientRpc() → [all clients]
       │    ├─ OnEnvironmentAnnounced event fires
       │    │    ├─ EnvironmentVFXManager.OnEnvironmentAnnounced() — visuals
       │    │    └─ AZGameUI.OnEnvironmentAnnounced() — UI text + SummerVac shake
       │    └─ EnvironmentCameraRoutine() — camera pan left → hold → return
       └─ yield 4s wait

Turn 2+ PrepPhaseRoutine [TurnManager, server]
  ├─ SummerVacation: prep timer 20s → 10s
  ├─ Kids (Turn 2+): KidsStealStagingClientRpc → yield 3.2s → RemoveRandomUnusedItem ×2
  ├─ Ambulance (Turn 4): AmbulanceBlanketStagingClientRpc → yield 3s → ApplyHeal +10°
  ├─ SunnyDay: recovery rate 1 → 2°/sec
  ├─ CoolBreeze: recovery rate 1 → 0°/sec
  └─ HeatWave: lower-temp acts first (in AttackPhaseRoutine)
```

### 1.2 Implementation Status

| Component | File | Status | Notes |
|-----------|------|--------|-------|
| Environment enum (7 types) | `Scripts/Core/Item/ItemEnums.cs:25` | ✅ Done | None, SunnyDay, CoolBreeze, CicadaSong, Kids, Ambulance, SummerVacation, HeatWaveWarning |
| Random selection | `TurnManager.cs:565-578` | ✅ Done | Pool of 6 types, assigned after Turn 1 Resolution |
| Announcement ClientRpc | `TurnManager.cs:581-586` | ✅ Done | Fires OnEnvironmentAnnounced event + camera pan |
| Camera pan (left → return) | `TurnManager.cs:588-616` | ✅ Done | 0.6s Slerp ±25° Y rotation |
| **VFX Manager — SunnyDay** | `EnvironmentVFXManager.cs:80-84` | ✅ Done | Light → yellow (1, 0.95, 0.7), intensity 3, cooler + tea shown |
| **VFX Manager — HeatWave** | `EnvironmentVFXManager.cs:85-87` | ✅ Done | Light → red (1, 0.6, 0.5), intensity 2.5 |
| **VFX Manager — CoolBreeze** | `EnvironmentVFXManager.cs:88-91` | ✅ Done | Light → blue (0.8, 0.9, 1), intensity 1.8, cooler shown |
| **VFX Manager — Kids entrance** | `EnvironmentVFXManager.cs:92-95` | ✅ Done | KidsRiseRoutine — ease-out Y rise from below, cat shown |
| **VFX Manager — Ambulance entrance** | `EnvironmentVFXManager.cs:96-98` | ✅ Done | AmbulanceSlideRoutine — ease-out X slide from left |
| **VFX Manager — CicadaSong** | — | ⚠️ No visual staging | Design says "audio/visual distraction" — SFX only (no SFX system yet) |
| **VFX Manager — SummerVacation** | — | ⚠️ No VFX Manager staging | Handled in AZGameUI instead (timer shake + red) |
| **AZGameUI — staging text** | `AZGameUI.cs:1025-1054` | ✅ Done | All 7 env names displayed in Korean |
| **AZGameUI — SummerVacation shake** | `AZGameUI.cs:1070-1081` | ✅ Done | sin/cos, 3px amplitude, 6Hz, timer fill → red |
| **AZGameUI — SummerVacation cleanup** | `AZGameUI.cs:1056-1068` | ✅ Done | StopSummerVacShake on env change + OnDestroy |
| **TurnManager — SummerVacation timer** | `TurnManager.cs:223-227` | ✅ Done | prepDuration → 10s |
| **TurnManager — SunnyDay recovery** | `TurnManager.cs:290-291` | ✅ Done | recoveryRate = 2f |
| **TurnManager — CoolBreeze recovery** | `TurnManager.cs:292-293` | ✅ Done | recoveryRate = 0f |
| **TurnManager — Kids steal (Turn 2+)** | `TurnManager.cs:229-236` | ✅ Done | ClientRpc → 3.2s wait → RemoveRandomUnusedItem ×2 |
| **TurnManager — Ambulance heal (Turn 4)** | `TurnManager.cs:238-263` | ✅ Done | ClientRpc → 3s wait → ApplyHeal +10° to lower-temp player |
| **TurnManager — HeatWave act order** | `TurnManager.cs:218-219` | ⚠️ Log only | Debug log present, but actual first-act logic needs verification in AttackPhaseRoutine |
| **Kids steal staging visual** | `EnvironmentVFXManager.cs:195-222` | ✅ Done | Sink → reposition behind opponent → rise → pause → sink |
| **Ambulance blanket staging** | `EnvironmentVFXManager.cs:229-270` | ✅ Done | Self: blanket overlay fade; Opponent: rescue worker rise/sink |
| **KidsStealStagingClientRpc** | `TurnManager.cs:693-698` | ✅ Done | Calls PlayKidsStealStaging on all clients |
| **AmbulanceBlanketStagingClientRpc** | `TurnManager.cs:700-708` | ✅ Done | Calls PlayAmbulanceBlanketStaging with healSelf calculation |

### 1.3 Detected Issues (Environment)

| # | Severity | Issue | File:Line | Description |
|---|----------|-------|-----------|-------------|
| 1 | 🔧 Fixed | Kid invisible after first sink | `EnvironmentVFXManager.cs:211` | SinkTransform deactivates GameObject → kid invisible for Rise. **Fixed**: added `SetActive(true)` before Rise |
| 2 | ✅ Resolved | HeatWave act-first implemented | `CombatResolver.cs:97-109` | CombatResolver.Resolve() checks HeatWaveWarning and swaps firstIdx based on lower temperature |
| 3 | ℹ️ Low | CicadaSong has no staging at all | Design spec | Design says "audio/visual distraction" — requires SFX_cicada + possible visual distortion. Currently no-op |
| 4 | ℹ️ Low | CoolBreeze wind particle missing | Design spec | Design says "Wind particle effect appears around scene" — not implemented, needs particle system |
| 5 | ℹ️ Low | Light lerp uses fixed 1.5s | `EnvironmentVFXManager.cs:127` | All light transitions take 1.5s regardless of env type — this is fine but could be configurable |

---

## 2. Mini-Game System

### 2.1 Flow Overview

```
[PrepPhase] Player clicks item
  └─ PlayerState.SelectItemServerRpc(slotIndex) [server]
       ├─ Phase/Ready/Usability checks
       ├─ RequiresMiniGame? → YES:
       │    ├─ Calculate deadline = min(now + timeLimit, prepEnd) + 1s grace
       │    ├─ _pendingMiniGameSlot = slotIndex
       │    └─ StartMiniGameClientRpc(slot, type, time, goal) → [owner only]
       │         └─ OnMiniGameStart event fires
       │              └─ MiniGameHub.HandleStart()
       │                   ├─ Calculate budget = min(timeLimit, prepRemaining)
       │                   ├─ Create UI by MiniGameType switch
       │                   ├─ Lookup ItemIcon from SO
       │                   └─ MiniGameUIBase.Begin(slot, budget, goal, canvas, icon)
       │                        ├─ BuildFrame (dim + timer bar + content area)
       │                        ├─ BuildContent (subclass-specific UI)
       │                        └─ BannerRoutine ("탭해라!" etc.)
       │
       ├─ [During mini-game] Timer runs in MiniGameUIBase.Update()
       │    ├─ Subclass logic (taps, drags, etc.)
       │    ├─ Goal reached → Finish(true)
       │    └─ Time expires → Finish(false)
       │
       └─ MiniGameUIBase.Finish(success)
            ├─ OnFinished event → MiniGameHub.HandleFinished()
            │    └─ PlayerState.SubmitMiniGameResultServerRpc(slot, success)
            │         ├─ Server validates: pendingSlot match, phase, deadline
            │         ├─ SUCCESS → ServerQueueItem (item enters action queue)
            │         └─ FAIL → ConsumeItem + CompactSlots
            └─ ResultOutroRoutine (성공!/실패… pop + fade → Destroy)

[Phase change to non-PrepPhase]
  └─ MiniGameHub.HandlePhaseChanged()
       └─ ForceCancel() on active mini-game
```

### 2.2 Implementation Status

| MiniGameType | Enum Value | Item | Hub Case | UI Class | Status |
|-------------|-----------|------|----------|----------|--------|
| TapRepeat | 4 | Hot Pack (핫팩) | ✅ | HotPackMiniGameUI | ✅ Done |
| BoilWater | 6 | Buldak Noodles (불닭볶음면) | ✅ | BuldakMiniGameUI | ✅ Done |
| TightenScrews | 7 | Screwdriver (십자드라이버) | ✅ | ScrewdriverMiniGameUI | ✅ Done |
| HitTargets | 1 | Water Gun (물총) | ✅ | WaterGunMiniGameUI | ✅ Done |
| ClawGrab | 9 | Claw Machine (집게손) | ✅ | ClawGrabMiniGameUI | ✅ Done |
| TimingCut | 10 | Blue Tape (청테이프) | ✅ | TapeCutMiniGameUI | ✅ Done |
| PatternUnlock | 3 | Smartphone (스마트폰) | ✅ | PatternUnlockMiniGameUI | ✅ Done |
| HugCharacter | 2 | Hug T-shirt (안아줘요 티셔츠) | ❌ Missing | — | 🔨 User will create |
| TapCard | 11 | Red Card (레드카드) | ❌ Missing | — | 📋 Deferred |
| GaugeMatch | 5 | — | ❌ No item | — | Unused enum value |
| PickCard | 8 | — | ❌ No item | — | Unused enum value |
| FanBoost | 12 | — | ❌ No item | — | Unused enum value |

### 2.3 Flow Verification Results

| Check | Result | Details |
|-------|--------|---------|
| SelectItemServerRpc → RequiresMiniGame check | ✅ Pass | Line 130, correct SO field check |
| Deadline calculation (min of timeLimit vs prepEnd + grace) | ✅ Pass | Line 136, 1s grace period |
| StartMiniGameClientRpc uses SendTo.Owner | ✅ Pass | Line 199, only owning client gets mini-game |
| MiniGameHub binds to OnMiniGameStart | ✅ Pass | Line 62, binds on TryBindLocalPlayer |
| Budget = min(timeLimit, prepRemain) | ✅ Pass | Line 75, clamped to 0.5s minimum |
| Hub creates correct UI per type | ✅ Pass | 7/9 types implemented, 2 auto-fail with warning log |
| ItemIcon lookup from SO | ✅ Pass | Lines 99-105, inv.GetItemData → data.Icon |
| Begin() passes icon to base | ✅ Pass | Line 108, itemIcon parameter |
| Timer expiry → Finish(false) | ✅ Pass | MiniGameUIBase.Update line 61-64 |
| Goal reached → Finish(true) | ✅ Pass | Each subclass calls Finish(true) on goal |
| SubmitMiniGameResultServerRpc validates slot | ✅ Pass | Line 209, pendingSlot match |
| SubmitMiniGameResultServerRpc validates phase | ✅ Pass | Line 216, PrepPhase check |
| SubmitMiniGameResultServerRpc validates deadline | ✅ Pass | Line 222, ServerTime comparison |
| Success → ServerQueueItem | ✅ Pass | Line 249 |
| Fail → ConsumeItem + CompactSlots | ✅ Pass | Lines 231-233 |
| Phase change → ForceCancel | ✅ Pass | MiniGameHub line 122-123 |
| Double-finish prevention | ✅ Pass | MiniGameUIBase.Finish line 72, `_finished` flag |
| Cached WaitForSeconds | ✅ Pass | `_waitResultHold`, `_waitBannerHold` static readonly |

### 2.4 Detected Issues (Mini-Game)

| # | Severity | Issue | Description |
|---|----------|-------|-------------|
| 1 | ℹ️ Info | HugCharacter/TapCard auto-fail | Hub switch default → auto-fail with log warning. By design (deferred) |
| 2 | ℹ️ Info | GaugeMatch/PickCard/FanBoost unused | Enum values with no SO item. Harmless |

---

## 3. Item Animation System (CombatVFXManager)

### 3.1 Flow Overview

```
[AttackPhase] TurnManager resolves combat
  └─ OnCombatResultClientRpc(resultData) → all clients
       └─ CombatVFXManager.OnCombatResult()
            └─ PlayCombatVFXSequence(result)
                 ├─ Determine action order (firstIdx, secondIdx)
                 ├─ PlayItemSequence(firstIdx) — uses SO AnimTrigger/timing
                 │    ├─ userVisual.PlayCombatAnimation(trigger)
                 │    ├─ EffectDelay wait → multi-hit with EffectInterval
                 │    ├─ targetVisual.PlayCombatAnimation(opponentTrigger)
                 │    └─ Wait for animation completion
                 ├─ Check first-action kill → death sequence → early exit
                 ├─ Brief pause between actions
                 └─ PlayItemSequence(secondIdx)
```

### 3.2 Key SO Timing Fields

| Field | Type | Purpose |
|-------|------|---------|
| AnimTrigger | string | User's Animator trigger name |
| OpponentAnimTrigger | string | Target's Animator trigger name |
| AnimDuration | float | Override animation length (0 = use clip length) |
| EffectDelay | float | Delay before first hit effect |
| EffectHitCount | int | Number of hit effects |
| EffectInterval | float | Time between multi-hits |

### 3.3 Detected Issues (Animation)

| # | Severity | Issue | File:Line | Description |
|---|----------|-------|-----------|-------------|
| 1 | ⚠️ Medium | Non-cached WaitForSeconds in loop | `CombatVFXManager.cs:133,150,156,160,183` | Uses `new WaitForSeconds(itemData.EffectDelay)` etc. — values are dynamic per-item so cannot be statically cached. Acceptable but creates GC pressure on multi-hit items |

---

## 4. Cross-System Flow Verification

### 4.1 Turn Lifecycle (Full Round)

```
1. PrepPhaseRoutine [Server]
   ├─ TurnNumber++, reset player states
   ├─ [If env active] Apply env effects:
   │    ├─ SummerVacation → prep time 10s
   │    ├─ Kids Turn 2+ → steal staging 3.2s → remove items
   │    └─ Ambulance Turn 4 → blanket staging 3s → heal +10°
   ├─ Start prep timer (with recovery rate per env)
   ├─ Players select items (possibly with mini-games)
   └─ Both ready or timer expires → AttackPhaseRoutine

2. AttackPhaseRoutine [Server]
   ├─ Resolve sub items first
   ├─ Determine action order (HeatWave: lower-temp first)
   ├─ CombatResolver.Resolve()
   ├─ OnCombatResultClientRpc → triggers CombatVFXManager
   └─ Wait for VFX duration

3. ResolutionPhaseRoutine [Server]
   ├─ Check winner → HandleRoundEnd if won
   ├─ Compact inventories
   ├─ [Turn 1 only, no env yet] → EnvironmentAnnouncementRoutine
   │    ├─ Random select → ActiveEnvironment
   │    ├─ AnnounceEnvironmentClientRpc → VFX + UI + camera pan
   │    └─ 4s wait
   └─ Loop back to PrepPhaseRoutine
```

### 4.2 Event Subscription Integrity

| Event | Publisher | Subscriber(s) | Subscribe | Unsubscribe |
|-------|-----------|---------------|-----------|-------------|
| OnEnvironmentAnnounced | TurnManager (static) | EnvironmentVFXManager, AZGameUI | Start() | OnDestroy() |
| OnCombatResult | TurnManager (static) | CombatVFXManager | Start() | OnDestroy() |
| OnMiniGameStart | PlayerState (instance) | MiniGameHub | TryBindLocalPlayer() | OnDestroy() |
| OnFinishedLocal | MiniGameHub (static) | (external listeners) | — | OnDestroy() (nulled) |
| CurrentPhase.OnValueChanged | TurnManager (NetworkVariable) | MiniGameHub | Update() lazy bind | OnDestroy() |

All subscriptions have matching unsubscriptions. ✅

### 4.3 NetworkVariable Authority Compliance

| Variable | Writer | Write Location | Correct? |
|----------|--------|----------------|----------|
| ActiveEnvironment | Server | TurnManager.EnvironmentAnnouncementRoutine | ✅ |
| Temperature | Server | TurnManager (via TemperatureSystem) | ✅ |
| TurnNumber | Server | TurnManager.PrepPhaseRoutine | ✅ |
| CurrentPhase | Server | TurnManager phase routines | ✅ |
| RemainingTime | Server | TurnManager.PrepPhaseRoutine | ✅ |
| IsReady | Server | PlayerState / TurnManager.ForceReady | ✅ |
| HasSelectedItem | Server | PlayerState.ServerQueueItem | ✅ |

No client-side NetworkVariable writes detected. ✅

### 4.4 Rpc Direction Compliance

| Rpc Method | Direction | Correct? |
|-----------|-----------|----------|
| SelectItemServerRpc | Client→Server | ✅ SendTo.Server |
| SubmitMiniGameResultServerRpc | Client→Server | ✅ SendTo.Server |
| StartMiniGameClientRpc | Server→Owner | ✅ SendTo.Owner |
| AnnounceEnvironmentClientRpc | Server→All | ✅ SendTo.Everyone |
| KidsStealStagingClientRpc | Server→All | ✅ SendTo.Everyone |
| AmbulanceBlanketStagingClientRpc | Server→All | ✅ SendTo.Everyone |
| OnCombatResultClientRpc | Server→All | ✅ SendTo.Everyone |
| OnPhaseChangedClientRpc | Server→All | ✅ SendTo.Everyone |

All Rpc directions match host-authoritative model. ✅

---

## 5. Missing Resources Summary

### 5.1 Sprites (Resources/ folder — runtime loaded)

| # | File | Used By | Required For |
|---|------|---------|-------------|
| 1 | `kid_idle.png` | EnvironmentVFXManager.BuildKidGroup | Kids env — child characters |
| 2 | `rescue_ready.png` | EnvironmentVFXManager.BuildAmbulanceGroup | Ambulance env — rescue worker |
| 3 | `sprite3DMat` (Material) | EnvironmentVFXManager (all env objects) | 3D-lit sprite rendering |

### 5.2 Item Sprites (Assets/ItemSprite/)

| # | File | Item |
|---|------|------|
| 1 | Screwdriver.png | 십자드라이버 |
| 2 | Samgyetang.png | 삼계탕 |
| 3 | ClawMachine.png | 집게손 |
| 4 | WaterGun.png | 물총 |
| 5 | Mask.png | 마스크 |
| 6 | RedCard.png | 레드카드 |

### 5.3 Animation Clips (Animator triggers)

| # | Trigger | Items Using | Description |
|---|---------|-------------|-------------|
| 1 | `use` | Samgyetang, Windbreaker, WarmTea, HotPack, Umbrella, etc. | Generic use animation |
| 2 | `gun` | Water Gun | Shooting animation |
| 3 | `tape` | Blue Tape | Tape wrapping animation |
| 4 | `fan` | Fan / HandFan | Fanning animation |
| 5 | `mask` | Mask | Wearing mask animation |

### 5.4 SFX (Audio — not yet implemented)

| # | ID | Environment/Item | Timing |
|---|-----|-----------------|--------|
| 1 | SFX_cicada | SunnyDay, CicadaSong | On environment announce |
| 2 | SFX_wind | CoolBreeze | On environment announce |
| 3 | SFX_kidWhistle | Kids | On kids entrance (rise) |
| 4 | SFX_kidSteal | Kids | On steal staging |
| 5 | SFX_siren | Ambulance | On ambulance entrance (slide) |
| 6 | SFX_clock | SummerVacation | On timer shake start |
| 7 | SFX_wear | Ambulance blanket | On blanket overlay |

### 5.5 Particle Systems

| # | Effect | Environment | Description |
|---|--------|------------|-------------|
| 1 | Wind particles | CoolBreeze | Around scene, flowing direction |

### 5.6 Unimplemented Mini-Game UIs

| # | MiniGameType | Item | Owner |
|---|-------------|------|-------|
| 1 | HugCharacter | Hug T-shirt | User (직접 제작) |
| 2 | TapCard | Red Card | Deferred (추후 추가) |

---

## 6. Potential Issues & Recommendations

### 6.1 Issues Requiring Attention

| # | Priority | Issue | Details | Recommendation |
|---|----------|-------|---------|----------------|
| 1 | ✅ Verified | HeatWave first-act logic confirmed | CombatResolver.Resolve() at line 97-109 handles HeatWaveWarning — lower-temp player gets firstIdx, same-temp falls back to ready order | No action needed |
| 2 | ✅ Verified | Ambulance healSelf calc confirmed | TurnManager.WaitForPlayersRoutine sorts by OwnerClientId — lower ID = P1 (index 0). Host is always ClientId 0 = P1. `LocalClientId == 0` is correct | No action needed |
| 3 | 🟡 Medium | EnvironmentVFXManager objects null when sprites missing | BuildKidGroup/BuildAmbulanceGroup early-return if sprites not loaded → staging methods operate on null groups | Graceful — null checks exist, but staging silently skipped |
| 4 | 🟢 Low | No audio system | All SFX playback points are comment placeholders | Need AudioManager or AudioSource integration |
| 5 | 🟢 Low | CicadaSong is a complete no-op | No visual, no audio, no gameplay effect | Either implement distraction or remove from pool |

### 6.2 Code Quality

| Check | Status |
|-------|--------|
| All literal WaitForSeconds cached | ✅ (dynamic SO values are acceptable exceptions) |
| No client-side NetworkVariable writes | ✅ |
| All event subscriptions have unsubscribe | ✅ |
| Rpc directions correct | ✅ |
| No `mcp__unityMCP__recompile_scripts` calls | ✅ |
| Unity compilation | ✅ 0 errors |

---

## 7. File Reference

| File | Lines | Role |
|------|-------|------|
| `Assets/Scripts/Core/Turn/TurnManager.cs` | ~711 | Server-authoritative turn state machine, env effects, staging ClientRpcs |
| `Assets/Scripts/Core/Combat/EnvironmentVFXManager.cs` | ~465 | Client-side environment visuals (light, sprites, staging animations) |
| `Assets/Scripts/Core/Combat/CombatVFXManager.cs` | ~200 | Client-side item use animation sequencing |
| `Assets/Scripts/UI/Game/AZGameUI.cs` | ~1090 | Runtime-built game UI, env text, SummerVac shake |
| `Assets/Scripts/Core/Player/PlayerState.cs` | ~306 | Player network state, mini-game RPC flow |
| `Assets/Scripts/UI/MiniGame/MiniGameHub.cs` | ~142 | Mini-game orchestrator (create, bind, cancel) |
| `Assets/Scripts/UI/MiniGame/MiniGameUIBase.cs` | ~329 | Abstract base: timer, banner, confirm pop, result outro |
| `Assets/Scripts/UI/MiniGame/HotPackMiniGameUI.cs` | ~52 | TapRepeat — rapid tap |
| `Assets/Scripts/UI/MiniGame/BuldakMiniGameUI.cs` | ~72 | BoilWater — rapid tap + gauge |
| `Assets/Scripts/UI/MiniGame/ScrewdriverMiniGameUI.cs` | ~147 | TightenScrews — circular drag |
| `Assets/Scripts/UI/MiniGame/WaterGunMiniGameUI.cs` | ~132 | HitTargets — tap moving targets |
| `Assets/Scripts/UI/MiniGame/ClawGrabMiniGameUI.cs` | ~167 | ClawGrab — timing tap state machine |
| `Assets/Scripts/UI/MiniGame/TapeCutMiniGameUI.cs` | ~152 | TimingCut — cursor in green zone |
| `Assets/Scripts/UI/MiniGame/PatternUnlockMiniGameUI.cs` | ~194 | PatternUnlock — 3×3 drag pattern |
| `Assets/Scripts/UI/MiniGame/MiniGameArt.cs` | ~78 | Procedural Circle/Arc sprite generation |
| `Assets/Scripts/Core/Item/ItemEnums.cs` | ~36 | MiniGameType, EnvironmentType, etc. |
| `Assets/Scripts/Core/Item/Data/ItemDataSO.cs` | — | SO with RequiresMiniGame, timing fields, Icon |
