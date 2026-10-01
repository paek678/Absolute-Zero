# PLAN 035 — Atlas-based character customization

## Scope and contract

Use the current front player and existing five equipment IDs (Head, Top, Back, Bottom, Tail). Each ID can reference a read-only atlas mapping asset containing multiple character parts and animation poses. The user supplies replacement sprites later. Empty mappings retain original artwork. Head sets may combine hat/hair/face; Top sets combine body and arms. Independent extra equipment categories are outside this change.

Preserve animation clips, transforms, SpriteResolver pose selection, combat, seats, ghost rules and current wire DTO. Replicate registered IDs only through existing owner-intent/server-validation/retained-NetworkVariable flow. All peers must include the same catalog and sprite assets. Importing a new image on one player's machine cannot distribute it over this protocol.

## Implementation

1. Add CosmeticAtlasSO bindings: renderer path, view, original pose sprite, optional replacement, overlay transform/order. Remap the rendered sprite after pose selection without mutating imported libraries or clips; clear restores the latest original pose.
2. Extend CosmeticItemSO with optional atlas; keep legacy overlay/swap assets compatible. Resolve the real nested head anchor. Bind remote visuals from retained state as well as later changes, and apply owner first-person entries separately.
3. Provide Editor authoring tools and reference templates derived from real prefab/library/clip data, existing hat slices and existing FPS animation clips. Keep unassigned slices inert. Provide an isolated preview prefab and documentation.
4. Validate pose switching/restoration/empty mappings/part isolation, ID admission and late binding; compile naturally and exercise actual multiplayer processes. Record exact results and limitations.

## Boundaries

No new art, slicing, side animation, package changes, protocol expansion, gameplay effects or scene repositioning. Character atlas sprites and first-person hand sprites are distinct authored mappings. Missing first-person replacements preserve existing hand artwork.

## Progress

- [x] Inspect current prefab, atlas labels, existing service and network route.
- [x] Implement and validate mappings and retained-state binding.
- [x] Generate authoring assets and preview.
- [x] Complete focused tests and multiplayer checks; write usage instructions.

Results: 8 atlas tests and 38 combat regression tests passed; actual Relay four-player hat binding and local 1v1 rematch passed. See [validation](../Validation/PLAN_035_results.md) and [authoring guide](../COSMETIC_ATLAS_SETUP.md). Future replacement art still requires visual fitting and pose review.
