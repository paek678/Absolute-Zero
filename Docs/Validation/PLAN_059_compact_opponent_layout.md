# Compact opponent regions — 2026-10-11

## User request

Move the opponents together with their items further outward/back and gather
their items more tightly, leaving the local player's arrangement untouched.

## Changes

- Multi presentation anchors in all four map prefabs: west/east `(±6,0,3)`
  become `(±7.2,0,4.2)`; north `(0,0,8.8)` becomes `(0,0,9.2)`.
- Side inventory offsets become `(±2.9,.85,-.6)`, placing their centers at
  `(±4.3,.85,3.6)` instead of `(±3.8,.85,2.4)`. The north rack follows its
  owner back by .4. Retain center-facing artwork and rows.
- Side column spacing `1.40 -> 1.00` (29% closer), row spacing `.70 -> .60`;
  north column spacing `.72 -> .62` (14% closer).
- Preserve icon sizes, character scales, local items/player/Ready/box placement,
  authoritative spawn seats and all Duel/Solo presentation anchors.
- Update fallback coordinates and existing map test expectations to the approved
  positions. No gameplay/network authority changes.

## Validation

Evidence: `output/validation/plan059/`.

- `tests-final.json`: 7/7 existing layout/spawn/grid/distribution checks passed.
- First capture: smaller racks clear the local region, but the side character
  feet still approach their outer icons. Final side anchors move farther outward
  while preserving the first-pass rack centers.
- First build returned Succeeded with one `ProducePlayerScriptAssemblies`
  internal CLR error (0x80131506) from BuildPlayerDataGenerator; final rebuild
  status is recorded separately rather than treating it as an error-free build.
- `build-final.json`: rebuild succeeded, 0 errors / 489 warnings; the first
  internal build-tool error did not recur. Package shader warnings remain.
- Actual Relay Host + 3 clients reviewed at 1600x900 in
  `output/manual-four-player/20261011-133640-f342db/`: all four views show compact,
  separated opponent regions clear of the local items and visible side health
  bars. `after.png` is the Host frame. No exception/input-failure markers found
  in the four logs before stopping these test processes.
- Editor remains clean LobbyScene/Edit Mode. Existing font/bot-graph hashes
  preserved. No commit or push.

## Limits

Startup screenshots validate the visible initial inventory arrangement at
1600x900. Large inventories, other aspect ratios and complete combat replays
are outside this focused visual adjustment check.
