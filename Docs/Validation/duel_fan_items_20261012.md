# Duel/Solo local fan and opponent item presentation

## Scope
- User stopped the fresh media recording. Stopped only that capture pipeline and its active player; prior captures remain.
- Local standing fan body/blades/grille now use the existing foreground sprite material, orders 20011/20012/20013. Opponent fan keeps its original material and orders 3/4/5.
- Duel/Solo opponent inventory art scales to 0.70 on each axis before floor contact adjustment. Fixed slot anchors, local items, Multi racks and game rules are unchanged.

## Validation
- Unity CLI verified the connected project, normal asset refresh compiled the saved changes, then launched Solo through the actual lobby buttons.
- Live GameScene_Solo inspection: all three local fan renderers use AbsoluteZero/WorldInteractionForeground; all three opponent renderers retain their original shader/order; all eight opponent items have scale (0.70, 0.70, 0.70).
- Inspected actual Game View screenshot: local fan draws in front of overlapping item art, opponent items are reduced.
- Evidence: output/validation/duel-fan-items/solo-check.json and solo.png.
- git diff --check passed for the two changed scripts.
- Console inspection reported compilationFailed=false. Its retained history contains two earlier Editor Stop shutdown exceptions (`SoloSessionCoordinator.ReturnToLobbyAsync`: Lobby scene could not load), matching the already recorded session teardown issue; these are not a clean-console pass. No new rendering exception appeared in the inspected run.
- OneVsOne shares these paths, but a fresh networked duel and standalone rebuild were not run for this adjustment. Stopped recording has not resumed; old standalone recordings/builds do not include this change.
