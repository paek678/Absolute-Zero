# PLAN_006 — Networked Item Test Scene (real ServerRpc/ClientRpc round-trip)

> Status: ✅ Complete (logic verified 15/15) — one deferred follow-up below

## Deferred Follow-up (user: "나중에 해도 됨", 2026-07-15)
- User play-tested after the layout fixes and says something still looks "이상한" (off) — no specifics/screenshot yet. **Ask for a screenshot when resuming.** Candidate suspects to check first:
  - Opponent still shows as capsule if the builder menu wasn't re-run after the board-prefab change (prefab recreate happens in `LoadOrCreatePlayerPrefab`)
  - Opponent tile labels overlapping each other (small tiles, label width 3.0 vs spacing 0.9)
  - Log panel (bottom-left overlay) covering my left basic tiles
  - Flat 준비끝 world-canvas button clickability/readability at the low camera angle
> Branch: `feature/item-system`
> Goal: test the item system through the REAL network path — client sends ServerRpc, server validates (Section 5.3 checklist) and resolves, clients update only via NetworkVariable / ClientRpc. 2-player via MPPM virtual player; solo via a server-owned dummy opponent.

## New design facts (from user, 2026-07-15)

- **4 random items are granted at game start** in addition to the 4 basic items.
- Random storage capacity is **8, laid out 4×2**. (PlayerInventory MAX_RANDOM_SLOTS=8 already matches.)
- Applied to both the local sandbox (PLAN_005) and this networked test.

## Will create
| File | Purpose |
|------|---------|
| `Assets/Scripts/UI/TestUI/ItemTestRegistry.cs` | Shared deterministic registry factory (4 basic SOs + 4 runtime-instance random items + drop table) — server & client build identical registries |
| `Assets/Scripts/UI/TestUI/TestUiKit.cs` | Shared runtime-UI builders + Korean font loader (used by both test UIs) |
| `Assets/Scripts/UI/TestUI/ItemNetTestManager.cs` | NetworkBehaviour: server-only turn loop, Section 5.3 ServerRpcs (`UseSubItemRpc`/`SelectMainItemRpc`/`PressReadyRpc`), `LogRpc` ClientRpc, FIX-04 timer NVs, dummy opponent |
| `Assets/Scripts/UI/TestUI/ItemNetTestUI.cs` | Per-client UI: host/join buttons, own inventory buttons (input → RPC only), opponent read-only panel, NV-driven display |
| `Assets/Prefabs/ItemNetTestManager.prefab` | via builder |
| `Assets/Scenes/ItemNetTestScene.unity` | via builder menu **Tools > AbsoluteZero > Build ItemNetTestScene** |

## Will modify
- `ItemSystemTestDriver.cs` — registry via ItemTestRegistry, initial 4 random grant, 8 random slots (2×4), UI helpers moved to TestUiKit
- `ItemTestSceneBuilder.cs` — second menu item + manager prefab creation

## Will NOT touch
- PLAN_004 item/contract code except none; core (partner) files; build settings; everything per PLAN_004/005 lists

## 2.5D Greybox View (user mockup, added mid-plan)

- Camera behind my player looking across the 마루 (floor plane); per-client mirroring (each client sees own side near camera).
- **My HP bar: Screen-Space Overlay canvas. Opponent HP bar + [준비 끝] button: World-Space canvases** (opponent bar floats above their head; ready button lies flat on the floor like an object) — explicit user instruction.
- Items are floor objects: greybox cube tiles + world-space TMP labels, clicked via physics raycast (new Input System `Mouse.current`). Category colors (ATK red / DEF blue / REC green / SAB purple), dimmed when consumed/empty.
- Fan cubes next to each player spin while that player's `IsFanActive` NV is true.

## Checklist
- [x] 1. ItemTestRegistry.cs + TestUiKit.cs (shared)
- [x] 2. ItemNetTestManager.cs (server loop + RPC validation per 5.3, FIX-04 timer NVs, dummy opponent)
- [x] 3. ItemNetTestUI.cs (2.5D greybox client view, input → RPC only)
- [x] 4. Sandbox driver updated (initial 4 randoms, 8 slots 2×4, shared registry factory)
- [x] 5. Builder: Build ItemNetTestScene menu + manager prefab (+ player prefab reuse)
- [x] 6. Compile clean — verified via MCP read_console (0 errors, 0 warnings) after MCP reconnected
- [x] 6b. **Item system self-test: 15/15 PASS** (`ItemSystemSelfTest.cs`, menu Tools > AbsoluteZero > Run Item SelfTest, executed via MCP 2026-07-15) — asset values(9.2), fan attack, windbreaker full-block & pierce, tea heal+cap, consume/refill(FIX-12), unlimited(FIX-03), initial-4 + threshold grants + 8-cap + one-shot re-arm, cat reroll, steal, mask full-negation, samgyetang +3/−7, soda −5/+15, drop table validity, death clamp. NGO "NetworkVariable written before spawn" warnings during the test are expected (edit-mode, pre-spawn writes) and harmless.
- [x] 7. Docs updated, staged (user commits)

## Play-test fixes (2026-07-15)

- Connection screen rebuilt in AZLobbyUI's design language (centered 500×450 panel, ABSOLUTE ZERO title, stacked buttons, status line) — user found the first version cluttered; verbose help text removed. In-game UI untouched per user.
- `AddNetworkPrefab` duplicate GlobalObjectIdHash errors: prefabs were auto-registered in DefaultNetworkPrefabs at import → now guarded with `NetworkConfig.Prefabs.Contains()`.
- `StartHost()`/`StartClient()` return values now checked — port-7777-in-use failure shows a status message instead of spawning into a dead session. Port holder was the editor process itself (leaked UDP socket from a previous play session; released by domain reload).

## Design rulings from play-testing (2026-07-15 — flag to core/partner)

- **All item effects AND consumption resolve during the attack phase.** Flow: select (prep) → ready → attack turn resolves everything → repeat. **Sub items (Cat) are queued at selection and executed at attack-turn start** (ready order, before defense/mains) — this CONTRADICTS SYSTEM_ARCHITECTURE 5.3 where Sub executes immediately during prep. Doc needs updating; partner's ServerRpc flow should queue Subs, not execute them.
- Recovery after Ready (+1°/s) confirmed correct per design (user briefly questioned, then confirmed).
- Player greybox = flat board (1.1×1.7×0.1 cube), NOT capsule — designer explicitly rejects capsules (2D character style).
- Camera locked: seated eye height (y 1.7), gaze slightly up (target y 1.3) — "마주 보는" composition approved by user.
- 준비끝 plaque lies FLAT on the floor (Euler 90). Fans sit outside the item fields beside each character.
- Note: the local sandbox (`ItemTestScene`) still executes Subs immediately per old spec 5.3 — net test scene is the source of truth for confirmed timing.

## Notes
- ItemNetTestScene NetworkManager has `EnableSceneManagement = false` — MPPM virtual players already load the same scene, and enabling it demands build-settings registration (the dialog the user saw earlier).
- Dynamic prefabs (player/manager) are registered via `AddNetworkPrefab` on BOTH host and client before Start — required for replication of dynamically spawned objects.
- Sandbox (`ItemTestScene`) still has its own UI helper copies; TestUiKit is used by the net test UI. Dedupe later if these tools live on.
