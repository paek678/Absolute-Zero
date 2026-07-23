# PLAN_015: InventoryPresenter — Centralized Inventory Visual Sync

> **Status:** 🔧 In Progress — Phase 1-5 Complete, Phase 6 (Edge Case) pending runtime test
> **Created:** 2026-07-23
> **Goal:** Client-side singleton that observes both P1/P2 PlayerInventory and centralizes ALL visual updates, eliminating scattered sync misses.

---

## Problem Statement

Currently, inventory visual updates are scattered across multiple scripts that independently discover players, subscribe to events, and rebuild views. This causes:

1. **Late subscription race** — `ItemWorldDisplay.TryInitialize()` and `AZGameUI.TrySpawnOpponentItems()` poll in `Update()`. If server mutates inventory before subscription, initial events are silently dropped.
2. **Rebuild during VFX** — `OnSlotStatesChanged` destroys/recreates `ItemWorldView` objects while `CombatVFXManager` may hold references to them (especially cat item via `FindCatItemView()`).
3. **Independent local/opponent views** — `ItemWorldDisplay` tracks local player only, `AZGameUI` tracks opponent separately. No shared timing guarantee when both inventories change (e.g., `StealRandomItem`).
4. **Selection index drift** — After `CompactSlots()`, slot indices shift but `_confirmedSlotIndex` still points to the old index.
5. **Multiple `InitializeClientRegistry` calls** — Called from 3 different places with no single authority.

---

## Current Flow Analysis

### Who Reads Inventory

| Script | What It Reads | How It Subscribes | What It Updates |
|--------|--------------|-------------------|-----------------|
| `ItemWorldDisplay` | Local player's `SlotStates`, `HasSelectedItem`, `IsBasicBlocked` | Polls `Update()` → `TryInitialize()` → `OnListChanged` | Local item world views (spawn/destroy/position), selection highlight, banned overlay |
| `AZGameUI` | Opponent's `SlotStates`, local `HasSelectedItem` | Polls `Update()` → `TrySpawnOpponentItems()` → `OnListChanged` | Opponent item sprites, item click → RPC dispatch, result panel item names |
| `CombatVFXManager` | Item data from `CombatResultData` | No subscription (reads on-demand) | VFX triggers, sprite references, `FindCatItemView()` via `FindAnyObjectByType` |
| `MiniGameHub` | Local player's item data | No subscription (reads once) | Mini-game icon display |
| `TurnManager` | Both inventories (server-only writes) | Direct reference | `GrantRandomItems`, `ResetForNewRound`, `RemoveRandomUnusedItem` |
| `CombatResolver` | Both inventories (server-only) | Direct reference | `ConsumeItem`, `ApplyDefense` |

### Identified Race Conditions

| # | Race Condition | Severity | Current Impact |
|---|---------------|----------|----------------|
| R1 | Late subscription — server mutates before client subscribes | High | Missing initial items on slow clients |
| R2 | Rebuild destroys views during CombatVFXManager animation | High | NullRef or broken VFX mid-animation |
| R3 | Local/opponent views update at different frame times | Medium | Visual desync on `StealRandomItem` |
| R4 | `_confirmedSlotIndex` invalid after `CompactSlots()` | Medium | Wrong item highlighted after combat |
| R5 | Multiple `InitializeClientRegistry` calls | Low | Redundant work, no crash |

---

## Proposed Architecture

```
Server: PlayerInventory(P1/P2).SlotStates mutation
         ↓ NetworkList.OnListChanged (automatic push)
Client: InventoryPresenter ← subscribes to BOTH P1 and P2
         ↓ deferred rebuild in LateUpdate (1x per frame max)
        ┌─────────────────────────────────────────────┐
        │ Local item world views (spawn/position/destroy) │
        │ Opponent item sprites                           │
        │ Selection highlight                             │
        │ Banned overlays                                 │
        │ Icebox animation trigger                        │
        │ Click detection + event dispatch                │
        └─────────────────────────────────────────────┘
         ↓ Events
        AZGameUI (RPC dispatch, result panel)
        CombatVFXManager (view lookup)
```

### InventoryPresenter Class Design

**Location:** `Assets/Scripts/Core/Inventory/InventoryPresenter.cs`
**Type:** `MonoBehaviour` (NOT NetworkBehaviour — observes existing NetworkLists)
**Pattern:** Singleton (same as IceboxController)
**Scene placement:** GameScene, on existing manager object or new "InventoryPresenter" object

#### Core Fields
```csharp
// Player references
PlayerState _localPlayer, _opponentPlayer;
PlayerInventory _localInventory, _opponentInventory;
bool _localBound, _opponentBound;

// Local item views
ItemWorldView[] _localViews;
int _confirmedSlotIndex = -1;
int _confirmedItemId = -1;        // NEW: tracks by ItemId, not index
bool _needsLocalRebuild;
bool _fullRedistribute;
bool _rebuildLocked;              // NEW: blocks rebuild during VFX

// Opponent item views
GameObject[] _opponentItemObjects;
bool _needsOpponentRebuild;
```

#### Events (for consumers)
```csharp
public event Action OnLocalInventoryChanged;
public event Action OnOpponentInventoryChanged;
public event Action<int> OnLocalSlotValueChanged;  // in-place single slot update
public event Action OnSelectionChanged;
public event Action OnBannedStateChanged;
public event Action OnViewsRebuilt;                // for VFX safety
public event Action<int> OnWorldItemClicked;       // AZGameUI subscribes for RPC
```

#### Key Methods
| Method | Responsibility | Replaces |
|--------|---------------|----------|
| `TryBindPlayers()` | Find local/opponent PlayerState, subscribe to OnListChanged | `ItemWorldDisplay.TryInitialize()` + `AZGameUI.TrySpawnOpponentItems()` |
| `BindLocalInventory()` | Subscribe SlotStates + HasSelectedItem + IsBasicBlocked | Scattered across ItemWorldDisplay |
| `BindOpponentInventory()` | Subscribe opponent SlotStates | `AZGameUI.OnOppSlotStatesChanged` subscription |
| `RebuildLocalViews()` | Destroy/recreate all ItemWorldView, position at markers | `ItemWorldDisplay.SpawnItemViews()` |
| `RebuildOpponentItems()` | Destroy/recreate opponent sprites at EnemyItem markers | `AZGameUI.RebuildOpponentItems()` |
| `UpdateSelectionVisuals()` | Highlight confirmed slot, dim others | `ItemWorldDisplay` selection logic |
| `UpdateBannedOverlays()` | Show/hide banned tape on Main items | `ItemWorldDisplay` banned logic |
| `HandleLocalItemClick()` | Raycast → find ItemWorldView → validate → fire event | `ItemWorldDisplay.HandleClick()` |
| `FindLocalViewByName(string)` | Search views by name fragment | `CombatVFXManager.FindCatItemView()` hack |
| `NotifyItemConfirmed(int)` | Set confirmed slot + item ID | `ItemWorldDisplay.NotifyItemConfirmed()` |

---

## Race Condition Mitigations

### R1: Late Subscription Fix
After subscribing to `OnListChanged`, immediately read current `SlotStates` and do a full rebuild. This ensures no gap between data population and subscription.

### R2: VFX-Safe Rebuild
```
OnSlotStatesChanged → _needsLocalRebuild = true
LateUpdate:
  if (_needsLocalRebuild && !_rebuildLocked)
    RebuildLocalViews()
  // else: rebuild deferred until VFX completes
```
`CombatVFXManager` sets `_rebuildLocked = true` before animation, clears after. Presenter checks on clear and runs deferred rebuild.

### R3: Unified Rebuild Timing
Both local and opponent rebuilds happen in the same `LateUpdate` frame. When `StealRandomItem` modifies both inventories, both `_needsLocalRebuild` and `_needsOpponentRebuild` are set, and both execute together.

### R4: Selection by ItemId
```csharp
void NotifyItemConfirmed(int slotIndex)
{
    _confirmedSlotIndex = slotIndex;
    _confirmedItemId = _localInventory.SlotStates[slotIndex].ItemId;
}

// During rebuild, re-locate by ItemId:
for (int i = 0; i < _localInventory.SlotStates.Count; i++)
    if (_localInventory.SlotStates[i].ItemId == _confirmedItemId)
        _confirmedSlotIndex = i;
```

### R5: Single InitializeClientRegistry
Called exactly once per inventory in `BindLocalInventory()` / `BindOpponentInventory()`. All other call sites removed.

---

## File-by-File Changes

### NEW Files
| File | Description |
|------|-------------|
| `Assets/Scripts/Core/Inventory/InventoryPresenter.cs` | ~350-400 lines, all centralized logic |

### DELETED Files
| File | Reason |
|------|--------|
| `Assets/Scripts/UI/Game/ItemWorldDisplay.cs` | All responsibilities moved to InventoryPresenter |

### MODIFIED Files
| File | Changes | Scope |
|------|---------|-------|
| `AZGameUI.cs` | Remove opponent item spawning, remove ItemWorldDisplay creation, subscribe to InventoryPresenter events instead | Major |
| `CombatVFXManager.cs` | Replace `FindCatItemView()` with `InventoryPresenter.Instance?.FindLocalViewByName("Cat")` | Minor (1 method) |
| `MiniGameHub.cs` | Optionally read from InventoryPresenter instead of direct PlayerInventory | Minimal |

### UNCHANGED (Server-Side)
- `PlayerInventory.cs` — data source, no changes
- `PlayerState.cs` — server RPC flow, no changes
- `TurnManager.cs` — server-only mutations, no changes
- `CombatResolver.cs` — server-only combat, no changes
- `TemperatureSystem.cs` — server-only temperature, no changes
- `ItemManager.cs` — registry, no changes
- `ItemWorldView.cs` — dumb view component, no changes
- `IceboxController.cs` — animation system, no changes

---

## Implementation Phases

### Phase 1: Skeleton + Binding ✅
- [x] Create `Assets/Scripts/Core/Inventory/` folder
- [x] Create `InventoryPresenter.cs` — singleton, Awake/OnDestroy/Update lifecycle
- [x] Implement `TryBindPlayers()` — find local + opponent PlayerState
- [x] Implement `BindLocalInventory()` — subscribe OnListChanged + HasSelectedItem + IsBasicBlocked
- [x] Implement `BindOpponentInventory()` — subscribe OnListChanged
- [x] Implement OnListChanged handlers (set rebuild flags)
- [x] Add deferred rebuild in LateUpdate
- [x] Add accessor methods (GetLocalSlot, GetLocalItemData, LocalSlotCount, etc.)
- [x] AZGameUI.Start() creates InventoryPresenter at runtime
- [ ] **Test:** Verify presenter binds to both inventories and logs changes

### Phase 2: Local Item View Management ✅
- [x] Port SpawnItemViews → SpawnLocalViews + RebuildLocalViews
- [x] Port selection visuals + banned overlays
- [x] Port NotifyItemConfirmed with _confirmedItemId fix (E4)
- [x] Port icebox animation triggers
- [x] Port HandleClick + CanSelectItem
- [x] Implement FindLocalViewByName
- [x] Implement _rebuildLocked guard for VFX safety (E1)
- [ ] **Test:** Local items spawn, click, select, ban — all via InventoryPresenter

### Phase 3: Opponent Item View Management ✅
- [x] Port RebuildOpponentItems from AZGameUI
- [x] Move opponent sprite creation + marker positioning
- [x] Fire OnOpponentInventoryChanged event
- [ ] **Test:** Opponent items appear and update correctly

### Phase 4: Refactor Consumers ✅
- [x] AZGameUI: remove _worldDisplay, _oppInventory, _oppItemsSpawned, _oppItemObjects
- [x] AZGameUI: remove TrySpawnOpponentItems, RebuildOpponentItems, OnOppSlotStatesChanged
- [x] AZGameUI: replace ItemWorldDisplay creation with InventoryPresenter creation
- [x] AZGameUI: update OnItemClicked to use InventoryPresenter
- [x] AZGameUI: remove redundant InitializeClientRegistry call from FindLocalPlayer
- [x] AZGameUI: subscribe to InventoryPresenter.OnWorldItemClicked
- [x] CombatVFXManager: replace FindCatItemView body with InventoryPresenter.FindLocalViewByName
- [ ] **Test:** Full integration — item selection, combat VFX, round transitions

### Phase 5: Remove ItemWorldDisplay ✅
- [x] Delete ItemWorldDisplay.cs + .meta
- [x] Grep confirmed 0 remaining references in Assets/Scripts
- [ ] **Test:** Full regression

### Phase 6: Edge Case Hardening
- [ ] Late join scenario (one player joins after items granted)
- [ ] Disconnect/reconnect
- [ ] Round reset with redistribution
- [ ] VFX during rebuild (combat animation while inventory changes)
- [ ] CompactSlots index shift with active selection
- [ ] Threshold grants mid-turn (temp crosses 30/20/10)
- [ ] Kids environment stealing from both players simultaneously

---

## Error Mitigation Strategies

### E1: `_rebuildLocked` Deadlock Prevention — Event + Timeout Dual Guard
- CombatVFXManager calls `InventoryPresenter.Instance.LockRebuild()` before animation
- On animation end, calls `UnlockRebuild()` → runs any deferred rebuild immediately
- LateUpdate checks `Time.time - _rebuildLockTime > 8f` → force unlock as fallback
- **Prevents:** infinite rebuild lock if VFX crashes or never completes

### E2: NetworkObject Destroy Order — SafeUnsubscribe Pattern
- All subscriptions consolidated in `Unbind()` method with null-safe checks
- `OnDestroy()` calls `Unbind()`
- Callbacks check `_localInventory == null` at entry → call `Unbind()` and return
- `Update()` monitors `_opponentBound && _opponentPlayer == null` → auto-unbind on disconnect
- **Prevents:** MissingReferenceException from stale NetworkList callbacks

### E3: Icebox Transform[] Collision — Flag-First Clear + IsAnimating Guard
- `_needsLocalRebuild = false` BEFORE `RebuildLocalViews()` executes
- `CanRebuild()` checks both `_rebuildLocked` and `IceboxController.Instance.IsAnimating`
- If rebuild deferred, flag stays true → retries next LateUpdate
- **Prevents:** destroying ItemWorldView transforms while Icebox holds references

### E4: Confirm Panel Index Drift — ItemId-Based 2-Phase Resolve
- `RequestItemConfirm(slotIndex)` stores `_pendingItemId` (not index)
- `ResolvePendingSlotIndex()` scans current SlotStates for matching ItemId at confirm time
- Returns -1 if item was consumed/stolen → AZGameUI closes confirm panel gracefully
- **Prevents:** wrong item selected after CompactSlots shifts indices

### E5: Late Opponent Binding — Immediate Snapshot on Subscribe
- After `OnListChanged` subscription, check `SlotStates.Count > 0`
- If items already exist, set `_needsOpponentRebuild = true` immediately
- Same pattern for local binding
- **Prevents:** missing initial items when opponent connects before presenter subscribes

### E6: ItemWorldDisplay Deletion Safety — 3-Step Grep Verification
- Step 1: Grep `ItemWorldDisplay` across entire codebase
- Step 2: Replace each reference with InventoryPresenter equivalent
- Step 3: Re-grep to confirm 0 references, then delete .cs + .meta
- Check GameScene.unity YAML for any MonoBehaviour references
- **Prevents:** compile errors and missing script warnings

---

## Estimated Scope
- **New code:** ~350-400 lines (InventoryPresenter.cs)
- **Deleted code:** ~300 lines (ItemWorldDisplay.cs)
- **Modified code:** ~80 lines across AZGameUI + CombatVFXManager
- **Net change:** roughly neutral line count, but significantly cleaner architecture
- **Risk:** Medium — touches core visual pipeline, but server-side logic is untouched

---

## Discovered Issues (from sync analysis, not in scope)
These bugs were found during the sync architecture analysis but are NOT part of the InventoryPresenter scope:

| # | Bug | File | Severity |
|---|-----|------|----------|
| 1 | `(int)netObj.OwnerClientId == playerIndex` conflates ClientId with PlayerIndex | CombatVFXManager.cs:138,502,552 | Critical |
| 2 | `LocalClientId == 0` hardcoded as P1 | TurnManager.cs:709 | Critical |
| 3 | `IsBasicBlocked` not reset in `StartNextRound()` | TurnManager.cs:488-523 | High |
| 4 | `OnOpponentRevealed` not cleared in `OnNetworkDespawn` | TurnManager.cs:100-108 | Medium |
| 5 | VFX wait only checks host-side | TurnManager.cs:421-427 | Medium |
