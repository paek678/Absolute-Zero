# Rectangular Multi presentation — 2026-10-11

## Request and implementation

Bring side opponents and their inventories forward so the floor arrangement
reads as a rectangle around the central box in a top-down view.

- All four map prefabs: Multi west/east visual floor anchors move from
  `(±7.4, 0, 5.1)` to `(±6, 0, 3)`. North remains `(0, 0, 8.8)`.
- Side character X/Y visual scale is reduced by 10% to compensate for moving
  closer to the camera. Floor contact still uses the lower-body sprite vertices.
- Side inventory offsets become `(±2.2, .85, -.6)`, putting rack centers at
  `(±3.8, .85, 2.4)`, at the box's Z coordinate. Their column axes now run
  parallel to the floor's Z axis; artwork points towards the box.
- Side column spacing increases from 1.05 to 1.40 to keep projected icons apart.
  Balanced rows, smaller icons, item identity and per-seat ownership are retained.
- Fallback side coordinates and the existing layout test expectations follow
  the new presentation anchors. Authoritative spawn coordinates, north/local
  inventory settings, and Duel/Solo placement remain unchanged.

## Validation

Evidence directory: `output/validation/plan058/`.

- `tests-final.json`: existing map/spawn, hidden-grid and item distribution
  regression tests passed 7/7 after the final asset changes.
- First actual Relay capture exposed clipped side health bars after moving
  forward. Corrected by moving side anchors inward.
- Second capture exposed a slight leg/item overlap. Corrected with the side
  character scale compensation and another 0.2-unit inward rack offset.
- `build-final.json`: Windows development build succeeded with 0 errors and
  489 warnings (including package compute-shader variant warnings).
- Actual Relay Host + 3 clients: all four 1600x900 startup screenshots reviewed
  in `output/manual-four-player/20261011-132833-e7ba50/`. Side health bars remain
  on screen; rectangular rack directions and forward side placement are visible.
  `after.png` is the final Host capture. No exception/input-failure markers were
  found in the four player logs before cleanup.
- Test players stopped after capture. Editor remains LobbyScene, clean/Edit Mode.
  Existing dirty font and bot graph hashes preserved; no commit/push performed.

## Scope

This is a visual layout and four-peer startup check, not a full combat replay.
Different aspect ratios and large inventory counts remain separate visual checks.
