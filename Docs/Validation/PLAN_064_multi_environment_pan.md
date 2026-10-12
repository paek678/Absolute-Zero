# PLAN064 — Subtle Multi environment camera pan

## Current decision

The user now wants the four-player environment announcement to turn the camera
slightly left while keeping all gameplay 2D art and attached UI screen-facing.
This supersedes the Multi stationary-camera decision in PLAN056/PLAN062.

## Implementation

- `DuelSpriteFacing.EnvironmentPanDegrees`: Multi 3 degrees, OneVsOne/Solo 25,
  unsupported/None 0. Keep screen-alignment eligibility independent of pan policy.
- `TurnManager.AnnounceEnvironmentClientRpc`: use the replicated match mode on
  every peer and pass its angle to the existing camera coroutine.
- Existing timing remains 0.6s left pan, 2s hold, 0.6s return. Existing cancellation,
  teardown and finally restoration continue to own the camera's borrowed rotation.
- Reuse whole-art render alignment and attached UI facing from PLAN062. Do not
  move fixed inventory anchors, change item sizes or alter gameplay authority.
- Expand the opt-in camera probe to measure remote rack icons as well as character
  sprites, local items, TextMesh labels and world canvases.

## Validation

Evidence root: `output/validation/plan064/`.

- Connected Editor verified as this project, Unity 6000.3.11f1.
- Focused camera/facing tests: final 19/19 passed (`final/tests.json`).
- Fixed-path development player: final 0 errors / 4 warnings (`final/build.json`).
- C64-01: PASS. Initial actual Relay Multi/duel plus local-host Solo passed on all
  seven peers (`runtime`). Visual review found the 5-degree Multi pan cropped the
  far-right health bar, so Multi was reduced to 3 degrees. Final Relay replay
  passed on all four peers (`final/runtime/players-4/*.camera-result.txt`): maximum
  pan 3.000 degrees, return error 0.000, art/UI plane error 0.000. All processes
  exited 0. Reviewed final `0.camera.png`: both side characters and health bars
  remain inside the 1280x720 frame. Duel/Solo remain at their verified 25 degrees.
- C64-02: Repeated interruptions and every attack/cosmetic combination — not
  exhaustively replayed; existing cancellation/pose ownership remains unchanged.

The prior user-observed death test windows were stopped before rebuilding the
same executable. No unrelated assets or gameplay rules were changed.

Editor left in LobbyScene / Edit Mode / clean; test processes ended. Existing
font and Solo graph hashes retained. No commit or push. The screenshot uses an
explicit Kids announcement stimulus, rendered by the actual game.
