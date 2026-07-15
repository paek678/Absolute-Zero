# Plan 003: Hover Fix + Turn Result Display Panel

**Status:** ✅ Complete  
**Created:** 2026-07-15

## Context

Two features requested by user:
1. **Item hover/selection effect is completely broken** — mouse hover shows no scale-up or outline, click does nothing. Root cause: project uses New Input System only (`activeInputHandler: 2`) but code uses old `UnityEngine.Input` API which is completely non-functional.
2. **Turn result display panel** — after both players ready and combat resolves, show a centered overlay (auto-close ~4s) on both screens with: who used what items, temperature changes from ticks, and temperature changes from items.

---

## Phase A: Fix Hover/Selection Effect

- [x] **A1.** Add "Interactable" layer (layer 6) via MCP `execute_code`
- [x] **A2.** Fix `HoverRaycaster.cs` — New Input System + LayerMask
- [x] **A3.** Fix `ItemWorldDisplay.cs` — New Input System
- [x] **A4.** Fix `ItemWorldView.cs` — Set layer + thicker collider

---

## Phase B: Turn Result Display Panel

- [x] **B1.** Fix ItemId capture bug in `CombatResolver.cs` — capture BEFORE `ConsumeItem()`
- [x] **B2.** Extend `CombatResultData` with temp snapshots + sub-item IDs
- [x] **B3.** Capture snapshots in `TurnManager.cs`
- [x] **B4.** Wire `OnCombatResultClientRpc` + static event
- [x] **B5.** Build result panel in `AZGameUI.cs`

---

## Files Modified

| File | Phase | Changes |
|---|---|---|
| `Assets/Scripts/Core/Common/HoverRaycaster.cs` | A | New Input System API + LayerMask |
| `Assets/Scripts/UI/Game/ItemWorldDisplay.cs` | A | New Input System API |
| `Assets/Scripts/Core/Item/ItemWorldView.cs` | A | Layer assignment + collider depth |
| `Assets/Scripts/Core/Combat/CombatResolver.cs` | B | Fix ItemId capture order |
| `Assets/Scripts/Core/Combat/CombatResult.cs` | B | Add temp snapshot + sub-item fields |
| `Assets/Scripts/Core/Turn/TurnManager.cs` | B | Capture snapshots + event + RPC body |
| `Assets/Scripts/UI/Game/AZGameUI.cs` | B | Result panel UI |

## Will NOT Touch
- `HoverEffect.cs` — works correctly, just never gets called
- `PlayerState.cs`, `PlayerInventory.cs` — no changes needed

## Verification
- **Phase A:** Play mode → hover items → yellow outline + 1.15x scale. Click → green outline. Ground never triggers.
- **Phase B:** 2-client test → both ready → result panel both screens → items + temp deltas → auto-close 4s.
