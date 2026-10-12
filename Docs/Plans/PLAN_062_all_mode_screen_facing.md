# PLAN062 — Screen-facing gameplay art in every mode

## Request and scope

The user wants side characters, gameplay 2D images and their attached UI to keep
their front visible regardless of camera direction. Extend the existing PLAN056
screen-aligned presentation to Multi, without changing PLAN061 inventory anchors.
Keep Multi environment camera motion disabled. Background enclosure, floor decals
and camera-owned first-person artwork retain their intentional rendering roles.

## Implementation

- `DuelSpriteFacing`: separate camera-pan eligibility from artwork alignment.
  Solo, OneVsOne and Multi align assembled art roots during camera rendering;
  whole-root rotation preserves the animation and part ordering. Preserve the
  artwork's bottom height and return the borrowed pose after rendering.
- Register remote inventory roots as they are created. ItemWorldView registers
  after its labels exist. Registration can install the camera service, so runtime
  creation order does not silently skip objects.
- Cache world canvases and attached TextMesh labels. Keep them upright after art
  roots turn; world UI also faces the camera outside rendering for hit testing.
  Screen-space HUD stays under its existing canvas.
- GameHudBuilder registers opponent bars and the world Ready button explicitly.
- OpponentBarPresenter follows the already-aligned render pose through the
  camera service's completion callback, keeping the bar centered over the visible
  head. The callback subscribes/unsubscribes with component enable/disable.
- No server state, seat identity, item counts, inventory slot positions or
  environment camera-pan policy changed.

## Verification

See [validation](../Validation/PLAN_062_all_mode_screen_facing.md). Focus on render
plane alignment, hierarchy restoration, floor contact, Ready/item input and all
four viewer perspectives, plus the existing Solo/duel camera pan and return.
