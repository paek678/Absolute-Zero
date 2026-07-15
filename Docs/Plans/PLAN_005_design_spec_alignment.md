# PLAN_005 — Design Spec Alignment

> Align current implementation with confirmed design document

## Status: ✅ Complete

## Scope

Fix all system-level differences between the confirmed design spec and current code.

### Will Modify
- `Assets/Scripts/Core/Item/ItemEnums.cs` — Remove ItemSlotType (Sub/Main)
- `Assets/Scripts/Core/Item/Data/ItemDataSO.cs` — SlotType → IsInstantUse bool
- `Assets/Scripts/Core/Player/ActionQueue.cs` — Remove subActions, rename mainAction → selectedAction
- `Assets/Scripts/Core/Player/PlayerState.cs` — Remove UseSubItemServerRpc, refactor SelectMainItemServerRpc → SelectItemServerRpc, decouple Ready
- `Assets/Scripts/Core/Combat/CombatResolver.cs` — mainAction → selectedAction, tie-break by temp
- `Assets/Scripts/Core/Turn/TurnManager.cs` — Draw = void round, buff timing to AttackPhase
- `Assets/Scripts/Core/Item/ItemManager.cs` — Remove SlotType from test items
- `Assets/Scripts/UI/Game/AZGameUI.cs` — Confirmation overlay, separate Ready, slot layout

### Will NOT Touch
- DefenseItemDataSO, AttackItemDataSO, RecoveryItemDataSO, SabotageItemDataSO (no SlotType refs)
- PlayerInventory (slot structure unchanged)
- MatchManager (only indirect via TurnManager)
- Camera/1인칭 (separate visual task)
- Environment variables (separate system)
- Opponent item display (separate UI task)

## Tasks

### Phase 1: Core Data
- [x] ItemEnums.cs — `ItemSlotType` renamed to Standard/InstantUse
- [x] ItemDataSO.cs — Replaced `SlotType` with `bool IsInstantUse`

### Phase 2: Action Queue
- [x] ActionQueue.cs — Removed `subActions`, renamed `mainAction` → `selectedAction`, added `instantAction`

### Phase 3: Player State RPCs
- [x] PlayerState.cs — Removed `UseSubItemServerRpc`
- [x] PlayerState.cs — New `SelectItemServerRpc` (handles both instant-use and queued)
- [x] PlayerState.cs — Added `HasSelectedItem` NV, decoupled item selection from auto-Ready
- [x] PlayerState.cs — Added `CancelSelectionServerRpc` for undo
- [x] PlayerState.cs — Added `ResetForNewTurn()` for per-turn state reset

### Phase 4: Combat & Turn Fixes
- [x] CombatResolver.cs — Updated `mainAction` → `selectedAction`, added temp tie-break in DetermineOrder
- [x] TurnManager.cs — Draw = void round (`StartNextRound(isDraw: true)` skips MatchManager.StartRound)
- [x] TurnManager.cs — Moved BuffDebuff processing from PrepPhase start to AttackPhase start
- [x] TurnManager.cs — Updated RPC: `OnSubItemUsedClientRpc` → `OnItemUsedClientRpc`

### Phase 5: Test Items & UI
- [x] ItemManager.cs — Removed SlotType, set IsInstantUse = false, fixed values (Fan 3°, Windbreaker 4°, Warm Tea 7°)
- [x] AZGameUI.cs — Added confirmation overlay (사용하기/취소), separate Ready (준비 끝), removed Sub/Main distinction

## Discovered Issues
(none yet)
