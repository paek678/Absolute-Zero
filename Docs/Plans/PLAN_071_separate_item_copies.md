# PLAN071 — Separate acquired copies and remove item count labels

## Decision

2026-10-12 user stopped media recording and requested no item quantity text, with duplicate acquisitions displayed independently in available item cells, in acquisition order. This supersedes the ordinary grant/steal use-stacking behavior. Existing item MaxUses, infinite-use rules, total capacity, mode random capacity and deathmatch three-copy top-up limit remain unchanged.

## Implementation and validation

| Requirement | Owner | Check |
|---|---|---|
| Every normal/random acquisition owns a distinct slot and CopyId | PlayerInventory | Repeated direct/random draws, order, consumption independence, full capacity and hole reuse |
| Steal transfers a separate copy with remaining uses intact | PlayerInventory / SeatInventoryMutator | Duel and prepared Multi steal, full destination, stale target/destination, queue invalidation |
| No count/infinity text on world item cards | ItemWorldView | Rendered labels in live scenes |
| Stable independent anchors in duel/solo and Multi | InventoryPresenter / InventoryPresentationSlots | Duplicate views, removal/refill, spare-region collision checks and live layout |

Server remains the inventory writer. The UI maps CopyId to anchors, while commands retain authoritative network slot identity. Single-item multiple uses are not split into invented copies. Existing in-progress state is not migrated; validate fresh sessions.

Tasks: focused code changes; focused Editor tests; fresh player build; real 1v1 and four-player inventory fixtures; four-player duplicate layout capture; solo UI check; results and deferred checks in Docs/Validation/PLAN_071_results.md.

Media recapture is stopped at the user's request. Do not resume automatically after this change. Partial files stay in output/media-delivery-20261012-30s and are not a completed delivery.
