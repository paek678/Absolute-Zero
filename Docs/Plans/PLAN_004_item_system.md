# PLAN_004 — Item System (ItemDataSO + 7 Subclasses + 4 Basic Item Assets)

> Status: ✅ Complete (2026-07-14) — awaiting user diff review + push
> Branch: `feature/item-system` (from 71d47ae)
> Source of truth: `Docs/SYSTEM_ARCHITECTURE.md` Section 4 (+ 9.2 for the 4 basic item values)
> Requested by: partner (core systems owner). Partner implements core (PlayerState full / TurnManager / CombatResolver); this plan implements the item side only. Partner will wire + merge.

---

## User Decisions (recorded)

1. **All 7 subclasses** — Attack/Defense/Recovery/Sabotage exactly per Section 4.1; Buff/Debuff/Special are DRAFTS (not in Section 4) based on Section 7 `BuffDebuffSystem.Schedule()` API and Section 2.3 `PlayerModifiers` fields — flagged for review.
2. **Dependency types**: core types are partner's scope. Create only spec-exact minimal versions needed for the item layer to compile, clearly marked for merge.
3. **`RerollAllRandom` / `StealRandomItem`**: obvious implementation (iterate random slots 4–11; demo naturally no-ops per Section 9.2).

## Work Scope Declaration

### Will create (item-owned, full implementation)
| File | Source |
|------|--------|
| `Assets/Scripts/Core/Item/ItemEnums.cs` | Section 4.6 (+ `SpecialEffectType` draft, `EnvironmentType` temp-located here — move at merge) |
| `Assets/Scripts/Core/Item/ItemSlotNetData.cs` | Section 1.2 (exact) |
| `Assets/Scripts/Core/Item/ItemContext.cs` | Section 4.2 (exact) |
| `Assets/Scripts/Core/Item/ItemDropTable.cs` | Section 4.5 (exact) |
| `Assets/Scripts/Core/Item/Data/ItemDataSO.cs` | Section 4.1 (exact) |
| `Assets/Scripts/Core/Item/Data/AttackItemDataSO.cs` | Section 4.1 (exact) |
| `Assets/Scripts/Core/Item/Data/DefenseItemDataSO.cs` | Section 4.1 (exact) |
| `Assets/Scripts/Core/Item/Data/RecoveryItemDataSO.cs` | Section 4.1 (exact, FIX-13) |
| `Assets/Scripts/Core/Item/Data/SabotageItemDataSO.cs` | Section 4.1 (exact) |
| `Assets/Scripts/Core/Item/Data/BuffItemDataSO.cs` | **DRAFT** (Section 7 Schedule API) |
| `Assets/Scripts/Core/Item/Data/DebuffItemDataSO.cs` | **DRAFT** (Section 7 Schedule API) |
| `Assets/Scripts/Core/Item/Data/SpecialItemDataSO.cs` | **DRAFT** (PlayerModifiers / FanSpeed) |
| `Assets/Scripts/Core/Player/PlayerInventory.cs` | Section 4.4 (exact) + Reroll/Steal |
| `Assets/Data/Items/Item_Fan.asset` | Section 9.2 |
| `Assets/Data/Items/Item_Windbreaker.asset` | Section 9.2 |
| `Assets/Data/Items/Item_WarmTea.asset` | Section 9.2 |
| `Assets/Data/Items/Item_Cat.asset` | Section 9.2 |

### Will create (shared contract types — spec-exact, marked "merge with core")
| File | Source |
|------|--------|
| `Assets/Scripts/Core/Player/PlayerModifiers.cs` | Section 2.3 (exact: `PlayerModifiers`, `DefenseInfo`, `DamageFilter`) |
| `Assets/Scripts/Core/Player/PlayerState.cs` | Section 2.1 **STUB** — NVs + PlayerIndex only; RPCs/ActionQueue/BuildContext are partner's |
| `Assets/Scripts/Core/Combat/TemperatureSystem.cs` | Section 3 (exact) |
| `Assets/Scripts/Core/Buff/BuffDebuffSystem.cs` | Section 7 (exact) |

### Will NOT touch
- `Assets/Scripts/Core/Game/*` (AbsoluteZeroTurnManager, TurnEnums)
- `Assets/Scripts/Core/Network/*`, `Assets/Scripts/Core/Player/AZPlayerVisual.cs`
- `Assets/Scripts/UI/*`, scenes, prefabs, packages, project settings
- `CLAUDE.md`, `Docs/SAFETY_RULES.md` (approval-gated)
- Pre-existing working-tree changes: `.gitignore`, `.mcp.json`, `.vsconfig` (excluded from commit)

### Path/symbol verification (done)
- Grep confirmed **zero** existing definitions of `PlayerState`, `PlayerInventory`, `TemperatureSystem`, `ItemDataSO`, `BuffDebuffSystem`, `ItemDropTable`, `ItemSlotNetData`, `PlayerModifiers`, item enums → no collisions.
- Existing namespace convention `AbsoluteZero.Core.<Folder>` confirmed; new code follows it (`.Core.Item`, `.Core.Combat`, `.Core.Buff`, `.Core.Player`).
- Existing `TurnEnums.cs` (TurnPhase/ActionType) untouched; no enum name overlap.

## Checklist

- [x] 1. ItemEnums.cs (Section 4.6 enums + drafts)
- [x] 2. ItemSlotNetData.cs (Section 1.2)
- [x] 3. Contract types: PlayerModifiers.cs / PlayerState.cs (stub) / TemperatureSystem.cs / BuffDebuffSystem.cs
- [x] 4. ItemContext.cs (Section 4.2)
- [x] 5. ItemDropTable.cs (Section 4.5)
- [x] 6. ItemDataSO.cs base (Section 4.1)
- [x] 7. Spec subclasses: Attack / Defense / Recovery / Sabotage (Section 4.1)
- [x] 8. Draft subclasses: Buff / Debuff / Special (flagged for review)
- [x] 9. PlayerInventory.cs (Section 4.4 + Reroll/Steal)
- [x] 10. Unity compile check (refresh + read_console, 0 errors, 0 warnings)
- [x] 11. 4 basic item SO assets via MCP (values per Section 9.2)
- [x] 12. Verify asset field values (read back — Item_Fan / Item_WarmTea confirmed, incl. HealPerUse=[7], MaxUses=-1)
- [x] 13. Update RECENT_CHANGES.md / CHANGES.md / ACTIVE_CONTEXT.md
- [x] 14. ~~Commit on feature branch~~ → reverted (`reset --soft`) at user request — user commits and pushes personally; changes left staged

## Deferred Follow-ups (user-requested)
- **Runtime test via copied lobby scene** — user asked to test with a duplicate of LobbyScene, then said "later" (나중에). Not done in this plan; do when requested.

## Designer Confirmations (2026-07-14, via user)

- **Mask rule (fixed in code):** if the target has an active defense whose filter matches the debuff's filter (Mask = Food), the **entire** debuff is negated — both immediate (+3) and delayed (−7) parts. Original draft only defense-checked negative immediate deltas; corrected in `DebuffItemDataSO.ExecuteEffect`.
- **Buff/Debuff interpretation confirmed** as implemented (immediate + next-turn delta split).
- **Tarot card:** 1-of-3 card pick is mini-game territory — deferred until mini-games are implemented. Effect definitions (ExtraAction/RevealOpponent) stay.
- **Steal:** the claw item is consumed, and the stolen item lands in the slot the claw vacated — a full thief inventory cannot happen by design. `dest == -1` guard kept as defensive code only.
- **Mini-games:** out of scope for now — items only.

## Draft Design Notes (Buff/Debuff/Special — review before merge)

- **BuffItemDataSO** (self-target): `ImmediateTempDelta` applied instantly via TemperatureSystem (negative bypasses defense — self-inflicted); `DelayedTempDelta` scheduled via `BuffSystem.Schedule(UserIndex, TempChange, value, DelayTurns)`. Covers Soda (−5/+15) and Buldak (+17 delayed).
- **DebuffItemDataSO** (opponent-target): same field shape, applied to Target; immediate negative delta goes through `ApplyDamage` with configurable `DamageFilter` (default Food) so Mask/defense interaction works. Covers Samgyetang (+3/−7).
- **SpecialItemDataSO**: `SpecialEffectType { FanSpeedChange, ExtraAction, RevealOpponent }` + `EffectValue`, `TargetsSelf`, `DelayTurns`. FanSpeedChange writes `FanSpeed` NV (or schedules); ExtraAction/RevealOpponent set `PlayerModifiers` flags. Covers Screwdriver (fan 2°/s) and Tarot Card.
- **EnvironmentType** enum temporarily lives in `ItemEnums.cs` (only `ItemContext.ActiveEnvironment` uses it); relocate when partner lands EnvironmentSystem.

## Discovered Issues
- `unity-*` skills referenced by CLAUDE.md rule 13 are not present in this environment (`.claude/skills` empty at project and user level) — proceeded with doc spec + standard NGO patterns.
- Section 4.4 `ConsumeItem` byte underflow guard added (`RemainingUses == 0` early return) — spec-exact code would wrap 0→255 (= unlimited) if contract violated; guard is defensive only, behavior unchanged on the documented call path.
