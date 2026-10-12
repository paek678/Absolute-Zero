# Force-feeding mouth presentation verification — 2026-10-12

> Follow-up: the mouth placement checks below did not validate the giver's semantic
> animation role. Ice Cream / Iced Americano incorrectly used the recipient clip
> for their remote giver. See [the correction and independent role tests](feed_giver_roles_20261012.md).

## Result

All three opponent-feeding items already use the shared Feed choreography: Ice Cream, Iced Americano and Samgyetang. No production code or asset changes were necessary. Food appears at the recipient's mouth area after the feeding gesture; it does not fly from the actor to the recipient.

`ContactItemChoreography.PlayFeedSequence` creates the food sprite at the head-derived contact point, invokes impact, waits for the animation and removes the sprite. The catalog and current item definitions were inspected. Self-consumption and possession-suppressed use are different presentation paths.

## Runtime evidence

- Nine scenarios passed across 21 game processes using the current fixed-path build.
- Real Relay 1v1: each of the three foods, seat 0 to seat 1, two peers per case.
- Solo: each food, human seat 0 to bot seat 1.
- Real Relay four-player: ice cream to left seat 1, coffee to front seat 2, soup to right seat 3; four peers per case.
- Reports checked choreography completion, ordered presentation events, camera/FPS restoration, presentation acknowledgement, next turn and lobby return. Processes exited normally.
- Existing opt-in contact probe fixtures grant one original item and stabilize temperature/fan settings. They do not modify item ScriptableObjects.
- Aggregate results: `output/validation/feed-mouth/summary.json`; raw per-peer reports/logs are under `initial/` and `modes/` in that directory.

## User screenshots

Actual 1600x900 player framebuffer frames extracted from freshly recorded 1v1 videos:

1. `output/validation/feed-mouth/screenshots/01_아이스크림.png`
2. `output/validation/feed-mouth/screenshots/02_아이스아메리카노.png`
3. `output/validation/feed-mouth/screenshots/03_삼계탕.png`

All three were visually inspected. Frames show food at the mouth/lower face just after the white impact flash, not necessarily the first damage frame. Other probe impact PNGs are diagnostic camera/canvas renders and are not represented as framebuffer captures.

## Scope and limits

Normal undefended feeding was tested. This is not an exhaustive defense/suppression matrix, every seat pairing, or a balance test. The existing fixed spawn point is head-derived, not continuous mouth tracking. Existing food size and presentation were preserved. No commit or push.
