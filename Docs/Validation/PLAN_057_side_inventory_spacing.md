# Side inventory spacing — 2026-10-11

## Request and change

Place west/east opponents outside their inventory racks, with items beside them
towards the central box. Reduce visual obstruction and make spacing regular.
Keep the north opponent and local inventory layout unchanged.

`MultiPerspectiveLayout` only (client-local presentation):

- Side rack offsets from character floor anchors: `(±3.4, .85, -.6)`, replacing
  `(±3, .85, -3.6)`. The racks now sit beside their owner instead of far forward.
- Side icons: `.54` to `.44` world-unit maximum dimension (18.5% smaller).
- Side row spacing `.70`, column spacing `1.05`; labels remain upright and move
  closer to the smaller art. North values remain unchanged.
- Distribute side items across two balanced rows: 6 = 3+3, 8 = 4+4. Odd counts
  leave one empty end cell; rows share columns instead of staggered centers.
- Extra columns above eight items shrink to fit the same region. No item identity,
  server inventory, seat ordering, character anchor, or targeting rules changed.

## Evidence

`output/validation/plan057/`

- `regression-final.json`: existing map/grid/distribution checks 7/7 passed.
- `build-final.json`: Windows development build succeeded, 0 errors / 4 warnings.
- First four-peer Relay screenshot exposed staggered partial rows; corrected to
  balanced, shared columns. First-pass evidence retained as `first-pass.png`.
- Final actual Relay Host + 3 client captures reviewed: all show outer characters
  followed by inward item racks, with shared aligned rows and no overlap with
  the character silhouettes in this startup view. `launch-final.txt` identifies
  all four frames; `after.png` is the Host frame at 1600x900.
- No exception/input failure markers found in the four logs. Test players stopped
  after capture. Editor remains clean LobbyScene/Edit Mode; no commit/push.
- Scope: visual layout/startup validation, not a complete combat/rematch replay.
