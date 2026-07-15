# PLAN_007: Item System Merge from Feature Branch

**Status:** ✅ Complete
**Source:** `C:\Users\paek6\Downloads\Absolute-Zero-feature-item-system\`
**Created:** 2026-07-15

---

## Scope

Merge new item types (Buff/Debuff/Special) and improvements from feature-item-system branch into main.
Keep main's architecture (TurnManager, PlayerState, TemperatureSystem, namespaces).

### Will Modify
- `Assets/Scripts/Core/Item/ItemEnums.cs` — add ItemSlotType, SpecialEffectType, EnvironmentType
- `Assets/Scripts/Core/Item/Data/ItemDataSO.cs` — add SlotType field, keep IsInstantUse as compat property
- `Assets/Scripts/Core/Item/Data/ItemContext.cs` — add EnvironmentType field
- `Assets/Scripts/Core/Item/Data/AttackItemDataSO.cs` — add EqualizeToUserTemp
- `Assets/Scripts/Core/Player/PlayerInventory.cs` — ConsumeItem improvements, StealRandomItem random pick
- `Assets/Scripts/Core/Item/ItemManager.cs` — fix Cat to Reroll, add all 17 random items
- `Assets/Scripts/Core/Common/GameSprites.cs` — update sprite mapping for new items
- `Assets/Scripts/Core/Player/ActionQueue.cs` — add subAction for Sub items
- `Assets/Scripts/Core/Player/PlayerState.cs` — Sub item selection flow
- `Assets/Scripts/Core/Turn/TurnManager.cs` — Sub execution at attack start
- `Docs/GAME_DESIGN.md` — update 5.3 Cat timing

### Will Create
- `Assets/Scripts/Core/Item/Data/BuffItemDataSO.cs`
- `Assets/Scripts/Core/Item/Data/DebuffItemDataSO.cs`
- `Assets/Scripts/Core/Item/Data/SpecialItemDataSO.cs`

### Will NOT Touch
- TurnManager core architecture (coroutine phases)
- CombatResolver (Main item resolution logic)
- TemperatureSystem (tick-based system)
- BuffDebuffSystem (only add BasicBlock case if needed)
- Network layer / Lobby / Relay
- Scene files
- UI files (AZGameUI, ItemWorldDisplay, ItemWorldView — sprite mapping update only in GameSprites)

---

## Phase 1: Type/Enum Foundation (non-breaking)
- [x] Add `ItemSlotType`, `SpecialEffectType`, `EnvironmentType` to ItemEnums.cs
- [x] Add `EnvironmentType ActiveEnvironment` to ItemContext.cs
- [x] Add `ItemSlotType SlotType` to ItemDataSO.cs (keep IsInstantUse as `=> SlotType == ItemSlotType.Sub`)

## Phase 2: New Item SOs (3 new files)
- [x] Create BuffItemDataSO.cs in Core/Item/Data/ (namespace AbsoluteZero.Core.Item.Data)
- [x] Create DebuffItemDataSO.cs in Core/Item/Data/
- [x] Create SpecialItemDataSO.cs in Core/Item/Data/

## Phase 3: Existing SO Enhancements
- [x] AttackItemDataSO: add EqualizeToUserTemp field + equalize logic

## Phase 4: PlayerInventory Improvements
- [x] ConsumeItem: byte underflow guard + RandomConsumable auto-slot clear
- [x] StealRandomItem: random pick instead of first-found
- [x] MakeSlot: Sub type Flags bit

## Phase 5: ItemManager — Full Item Registry (21 items)
- [x] Fix Cat: SabotageType.Neutralize → Reroll, SlotType = Sub
- [x] Add all 17 random items with correct stats from GAME_DESIGN.md
- [x] Update GameSprites mapping for available sprites

## Phase 6: Sub Item Timing (core change)
- [x] ActionQueue: add subAction field (renamed instantAction → subAction)
- [x] PlayerState.SelectItemServerRpc: Sub items → store in subAction, don't auto-ready
- [x] TurnManager.AttackPhaseRoutine: ExecuteSubItems before CombatResolver

## Phase 7: Doc Updates
- [x] GAME_DESIGN.md: Sub item flow updated (attack turn start)
- [x] ACTIVE_CONTEXT.md: update status
- [x] RECENT_CHANGES.md: record changes

## Discovered Issues
(none yet)
