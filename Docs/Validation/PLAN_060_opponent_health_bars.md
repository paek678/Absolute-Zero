# Opponent health-bar sizing and alignment — 2026-10-11

## Change

- Multi (3/4-seat HUD): reduce the opponent world-space Canvas scale by 20%,
  `.007 -> .0056`. Preserve the bar/thermometer proportions and local HUD.
- Align Multi bars horizontally to the bound character's `head` SpriteRenderer
  bounds using projected screen X (preserving bar depth/Y), instead of assuming
  the visual root is head-centered. Perspective means matching world X alone
  cannot align two objects at different heights.
- Cache the renderer together with the visual binding, clear both on remapping,
  and retain root-based placement if the renderer is absent.
- Update in LateUpdate after character animation; retain the original vertical
  offset so expression animation does not bounce the bar vertically.
- Duel/Solo bar sizing and root-based alignment remain unchanged. Character,
  inventory and map positions remain as approved in PLAN059.

## Validation

Evidence directory: `output/validation/plan060/`.

- Existing clean HUD tests (`Plan052CleanPlayTests`): 5/5 passed.
- Windows development build: succeeded, 0 errors / 2 warnings.
- First capture showed root-based alignment still active: the scene's head is
  nested under `body`. Corrected cached lookup to search descendant renderers.
  Final UI tests again passed 5/5 and build succeeded, 0 errors / 2 warnings.
- Actual Relay Host + 3 clients: all four startup views reviewed at 1600x900 in
  `output/manual-four-player/20261011-140056-1f2182/`. Smaller bars and temperatures
  are aligned over each head, with no screen-edge clipping. `after.png` is the
  final Host capture. No exception/input-failure markers found in player logs.
- Test processes stopped after capture; Editor clean LobbyScene/Edit Mode.
- Existing font and bot-graph hashes preserved.

This check covers startup presentation at 1600x900, not all cosmetic head
variants, other aspect ratios or an entire combat/rematch sequence.
