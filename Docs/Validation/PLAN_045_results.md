# PLAN045 — Multi layout validation

## Outcome

Four-player layout adjusted: cyclic west/north/east identity is unchanged; characters have more space; opponent items face the viewer and have consistent visible-art sizing and use labels; the local random row is centered; the basic row leaves a gap around Ready. Redundant floor squares are hidden and the local fan clears the full item row. The icebox remains south-center at a smaller presentation size.

Plan: [PLAN045](../Plans/PLAN_045_multi_layout_readability.md). Evidence root: `output/validation/plan045/`.

## Findings and fixes

| Finding | Change |
|---|---|
| 24 legacy local/opponent floor squares remain visible regardless of inventory | Disable those scene SpriteRenderers, preserving transforms, references and sprites |
| Four random items occupy the left half of an eight-slot row | Center current copies inside the existing authored endpoints; support eight random copies after theft |
| Opponent spacing is smaller than the icon bounds; icons rotate sideways/upside down | Upright seat-relative grids; west/east two rows, north one row; additional columns fit the same region |
| Transparent PNG padding makes some opponent icons tiny | Normalize the sprite's tight mesh bounds, without modifying image files |
| Use text inherits per-sprite size | Separate icon child and fixed label layout |
| Distribution assigns Vector3.one instead of the item's original scale | Capture and restore per-item scale in the shared distribution coroutine |
| Icebox clips overwrite a root scale adjustment | Put presentation scale on a parent outside the Animator; sample both actual clips in an Editor test |
| A delayed inventory refresh during a close-up could use the animated camera angle | Cache the initial seating camera orientation |
| Full local inventory intersects its stay-fan; Windbreaker partly intersects Ready | Move only the Multi stay-fan anchor and widen the four basic-item center gap |

## Verification

| Check | Observed result / evidence |
|---|---|
| Unity Editor tests | **1042/1042**; `editor-tests-final.json`. One initial new test incorrectly expected serialized colliders; current code creates those colliders at runtime. Corrected to inspect serialized target/sprite references, with actual pointer checks below |
| Final development build | `build_98d02055aab2`, **0 errors**, 486 warnings; `build-final-ready-gap.json`. Warnings are existing Pipeline configuration / package shader diagnostics. No runtime Pipeline dependency was introduced |
| Pre-change real Relay fixture | 8 items on all four seats; all peers matched authoritative inventory data; `before-full/` |
| Final real Relay layout | **2/2** cases, **8 peer observations**; normal 8 and overflow 12 items. Same-run CopyId/inventory signatures match on all peers; `ready-gap-layout/` |
| Screen containment | All 32 item views per peer in the normal case and 48 per peer in the overflow case are inside the captured screen bounds. Four seats in both cases; no item views clipped |
| Visual inspection | Four perspectives reviewed at 960×540 for both counts. Separate source icons/counts, clear character bodies/faces, hidden chalk squares, clear fan and Ready space |
| Input regression before final center-gap adjustment | Real Relay 1v1/3/4-player cases **3/3**, `final-input/`; actual input-coordinate click/aim/cancel/Ready probe |
| Input regression with final center gap | **3/3**, **9 peer observations**, real Relay 1v1/3/4-player; `ready-gap-input/`. Coordinates resolve from current live item/character views |
| Contact animation with moved Multi seats | **1/1**, **4 peer observations**, real Relay host-to-west hug, local FPS/remote view and restore assertions; `contact/online-4-hug-0to1/` |

`layout` fixtures run only in Editor/development builds with explicit command-line flags. They seed a reproducible inventory on the server, verify all four peers' own replicated state/views, and wait for the last peer before exiting. The twelve-item fixture represents the existing total-slot upper bound; it does not change production drop limits. Pre/post content signatures match in the normal fixture.

Captures are **actual running-player camera + active Canvas renders into a diagnostic RenderTexture**, not a native desktop screenshot. Their pass gates check replicated contents/view presence; overlap assessment is a separate visual review. The test does not claim human acceptance or an exhaustive screen-overlap proof.

## Remaining verification / refinement

| ID | Status | Exact scope / closing evidence |
|---|---|---|
| QA045-01 | Visual refinement | Twelve north-seat item icons/counts remain compact at 960×540. They do not overlap, but human readability preference and smaller screen sizes need a direct play check before claiming final UI polish |
| QA045-02 | Not run | Native fullscreen/ultrawide and a different PC. Repeat full-inventory screenshots plus item/target/Ready clicks at those display settings |
| QA045-03 | Not exhaustive | The full-inventory fixtures cover four representative random artworks and the four basic items; every possible item combination is not visually enumerated |

## Preservation

No art, sprite slicing, clips, gameplay rules, authoritative spawn coordinates, network messages, seat identities, 1v1/Solo layout assets or unrelated work were intentionally changed. The shared scale-restoration fix uses each mode's existing item size. The original LobbyScene remains clean in Edit Mode, console ground-truth errors are zero, and all test players exited. Baseline 1,338 existing files: ten intended code/scene/map/test changes, no missing files; unrelated baseline files including fonts are byte-identical. Harness fixture and new tests/docs are additional intended changes. Summary: `exit-evidence.json`.

Final runtime total: **6/6 scenarios, 21 peer observations**. Intermediate captures under `after-full/` and `final-layout/` predate the final Ready gap; use **`ready-gap-layout/`** for current screenshots.
