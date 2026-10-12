# PLAN062 validation — 2026-10-11

Evidence root: `output/validation/plan062/`.

## Completed

- Connected Editor verified: Absolute Zero, Unity 6000.3.11f1. Normal asset refresh,
  no forced recompile. Existing dirty work retained.
- `Plan056CameraFacingTests`: 15/15 passed. Includes independent pan/alignment
  mode policies, screen-normal alignment, preserved child animation, floor
  height and restoration, and upright attached TextMesh/world Canvas.
- `Plan061FixedInventorySlotsTests`: 5/5 passed, fixed slot mapping retained.
- `Plan054CasualUiTests`: 9/9 passed after the bar-position follow-up (29 focused
  and regression assertions in total across the three test runs).
- Windows player at the existing fixed executable path: build errors 0, warnings 4.
- Actual Relay four-player and duel, plus local-host Solo: all seven processes
  exited 0. `runtime/*/*.camera-result.txt` reports PASS. Render-time normal error
  is 0.000 degrees for active remote character sprites, local item roots, TextMesh
  labels and world canvases. Multi camera excursion 0.000 degrees; duel/Solo
  excursion 25.000 degrees with restoration error 0.000 degrees.
- Explicit Kids environment presentation was injected by the opt-in camera probe;
  the screenshots are live game rendering of that stimulus. Normal gameplay does
  not enable the probe. Reviewed Multi and Solo framebuffer captures.
- First Relay input and 8/12-item layout matrix: 3/3 scenarios, 12/12 processes
  passed (`relay/matrix.json`). Reviewed all four 8-item viewer screenshots.
- Visual review found the old bar alignment used the head's pre-render position.
  Added a post-alignment notification and made the presenter follow the rendered
  head. Final build also reports errors 0 / warnings 4 (`build-final.json`).

## Final result and remaining coverage

- C62-01: PASS. Final Multi item/Ready input and 8/12-item layout matrix after the
  bar-position follow-up: 3/3 scenarios, 12/12 processes exited 0, Relay verified
  for all three (`final-relay/matrix.json`). Reviewed the final 8-item and 12-item
  host screenshots; final full image: `final-relay/layout-4-w1/seat0.layout-full.png`.
- C62-02: Exhaustive cosmetic and every attack-animation/camera combination — not
  claimed by these focused checks. Existing animation pose ownership is preserved.

Editor left in LobbyScene / Edit Mode / clean. All test players stopped. Existing
MalgunGothic font and SoloDuelDecision graph hashes retained. No commit or push.

The camera service intentionally leaves background enclosure, horizontal floor
decals and camera-attached first-person artwork under their current owners.
