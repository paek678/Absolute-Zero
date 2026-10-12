# Fixed Multi inventory anchors

## Approved behavior

User request (2026-10-11): reserve the fully occupied layout as fixed spawn
anchors, filling one empty anchor at a time instead of pushing existing items
when inventory grows. Applies to the ongoing four-player layout work.

## Implementation

1. Preserve presentation slots by CopyId within participant/match/round scope.
   Removing/compacting server inventory releases only the removed copy's anchor.
   New copies fill first free preferred anchors; stacked uses retain their slot.
2. Local Multi uses the existing 12 `PlayerItem` anchors directly: random region
   0–7 and basic region 8–11. Remove count-dependent centering/spacing.
3. Each remote relative seat builds 12 fixed `RemoteInventoryAnchors_N/Slot_NN`
   transforms once: north 12 columns, sides 6 by 2, with full-capacity scale from
   the first item. Allocation changes never move these anchors or rescale icons.
4. Partial box distribution identifies new view objects instead of new array
   indices, so network compaction cannot replay a survivor or skip a replacement.
5. Verify sequential fill, use/compaction/refill, duplicate types, spare-capacity
   stealing, scope reset, existing presentation/distribution tests, and real Relay
   8/12-item layout plus item input.

## Ownership and exclusions

`InventoryPresentationSlots`, `InventoryPresenter`, `MultiPerspectiveLayout`
own display placement only. Server inventories, network indices/CopyIds,
selection identities, gameplay rules and the fixed character/box positions
remain unchanged. Duel/Solo keep their existing anchor placement; their partial
distribution also benefits from detecting actual newly created views.

## Evidence

See [validation](../Validation/PLAN_061_fixed_inventory_anchors.md).
