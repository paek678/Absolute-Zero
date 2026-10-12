# PLAN055 — Multi floor contact and inward inventory racks

## Request and implementation

The user requested correction of floor-cut character silhouettes and inventory
row placement that faces the central icebox, rather than rotating only each icon.

- Character owner: `MapCharacterAnchor.ApplyVisualPose`. For Multi remote visuals,
  align the lower-body sprite mesh minimum to the map's floor anchor plus 0.025.
  Preserve original bone/sprite local transforms, pivots, scale and seat identity.
- Inventory owner: `MultiPerspectiveLayout`. Form each remote rack using a ground
  plane basis towards the box; columns run perpendicular to the inward direction.
  All icons in a rack share the same projected heading; use labels stay upright.
- Local inventory owner: `InventoryPresenter`. Retain its authored south row and
  Ready-button gap, use one shared inward heading, and lift the sprite enough to
  clear the floor including hover/outline enlargement.
- Shared geometry: `MultiItemFacing`. Rack placement, shared heading and mesh-based
  floor clearance. No gameplay randomness, network values or inventory IDs change.
- Scene: deactivate only `EnemyPlayer_1/Test` and its SpriteRenderer, an extra
  lower-body copy visible beneath the participant banner. Renderer-only hiding
  was insufficient because presentation code re-enabled it. Keep the object.

## Scope boundaries

No sprite-pivot rewrite, animation replacement, map relocation, server-spawn or
turn changes. Solo/1v1 preserve existing character offset and inventory placement.
Ground anchors remain designer-authorable; the character visual is aligned to
the floor anchor rather than moving the floor anchor down.

## Evidence

- Geometry before: `output/validation/plan055/geometry.txt`: lower-body bounds
  extend to Y=-0.36 against floor top Y=0; extra Test sprite at Y=4.4–5.06.
- Focused geometry/scene tests: `tests.json`, 6/6 passed.
- Final tests: `tests-final.json` 6/6, `regression-layout.json` 7/7 (including
  item distribution destination/scale restoration). Initial test invocation
  before the new test assembly compiled discovered zero tests and was repeated.
- Build: `build-final.json`, success, 0 errors / 489 warnings.
- Actual Relay four-peer startup screenshots: `launch-final.txt` identifies the
  capture directory; `after.png` is the Host framebuffer at 1600x900. All four
  viewpoints were captured. Test processes were ended after capture.
- Actual Relay four-peer input fixture: `relay-input-final/summary.json`, PASS.
  This is an input fixture, not a settlement-signature equality test.
- Two earlier input attempts are retained: first failed to receive the Relay
  code in time; second used the runner's manual-input flag incorrectly with an
  automated input fixture, slowing game time and timing out its visible fixture
  wait. The correctly configured final run passed; no game code was changed to
  bypass those checks.
- Visual review: full feet and local cat silhouette visible, Test fragment absent,
  side racks lie on the floor pointing toward the box, north rack centered even
  with fewer than eight items, local Ready gap retained. Review comparison is in
  `output/ui-style-20261011/four-player/`.
- Limits: no exhaustive animation/skin matrix, all-inventory-capacity visual
  review, or cross-PC testing in this task. Future art with a different seated
  silhouette should be checked against its lowerbody sprite mesh. No runtime
  Solo/1v1 replay this turn; modified character/item paths are Multi-gated.
- Existing source/scene changes retained. Scene backup is
  `output/validation/plan055/GameScene_Multi.before.unity`. Existing font/graph
  hashes retained. Editor restored to LobbyScene/Edit Mode. No commit or push.
