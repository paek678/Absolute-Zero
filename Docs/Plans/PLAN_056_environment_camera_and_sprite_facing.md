# PLAN056 — Environment camera and screen-aligned sprite facing

## User decisions (2026-10-11)

- Multi environment announcements keep the camera fixed on every peer.
- Duel and Solo retain the environment pan. Standing 2D artwork stays parallel to the
  camera screen as one assembly, regardless of camera pitch, yaw or roll. This
  clarification supersedes the first yaw-only implementation.
- Preserve map layout, animations, environment effects, timings and game rules.

## Implementation

| Requirement | Owner | Change |
|---|---|---|
| Multi fixed view | TurnManager.AnnounceEnvironmentClientRpc | Gate on the client's shared ActiveConfig, not server-only `_gameMode` |
| Duel pan cleanup | TurnManager.EnvironmentCameraRoutine | Quaternion pan, cancel prior pan, restore on finish/despawn |
| Assembled 2D facing | DuelSpriteFacing | Scene scan once at installation/announcement; register newly created items, fans and presentation-owned effects |
| Animation safety | DuelSpriteFacing render callbacks | Borrow assembled root rotation/position for the gameplay camera render, restore immediately after; retain child transforms and in-plane animation lean |
| Exclusions | DuelSpriteFacing discovery | Baked map background/floor, horizontal decals, Canvas, camera-child art and FPSVisualController are excluded |
| Dynamic art | ItemWorldView, FanSpawner, PresentationResources | Register complete roots, reject overlapping parent/child registrations |

Camera rotation determines the picture plane; object-to-camera position is not
used (no LookAt). Retain in-plane artwork lean while removing out-of-plane tilt.
Compensate render-only root Y to retain the pre-rotation 2D artwork lower bound.
This does not create side art, rotate individual bones, change authoritative
transforms, or modify network state. The service is inactive for Multi.
Current project uses URP; begin/end camera rendering callbacks own the temporary
pose, with an explicit disable cleanup. No per-frame scene-wide discovery.

## Validation

Evidence directory: `output/validation/plan056/`.

- Focused Editor tests: mode policy, whole-assembly facing, double registration,
  repeated application/restoration, animation pose preservation, teardown,
  floor/camera-child exclusions.
- Development-only `--az-camera-check` injects a Kids announcement through the
  real Host RPC after normal lobby entry. It samples every rendered frame,
  records camera excursion/restoration, and captures the final framebuffer.
  This is a presentation stimulus, not a claim that random selection chose Kids.
- Runtime targets: actual Relay Host + 3 clients; actual Relay duel; local Solo.

## Deferred validation ledger

| ID | Status | Stimulus / evidence required |
|---|---|---|
| C56-01 | Passed | `screen-aligned/tests-final.json`: 10/10; normal import; build 0 errors / 4 warnings |
| C56-02 | Passed | `screen-aligned/runtime/players-4/*.camera-result.txt`: all four peers max=0, restored=0 |
| C56-03 | Passed | `screen-aligned/runtime/players-2` and `players-1`: all three peers max=25, restored=0, planeError=0; framebuffer screenshot reviewed |
| C56-04 | Deferred | Exhaustive skin/item/animation and future map-art visual matrix |


## Results

- All three launch scenarios / seven processes passed and exited 0. Nickname
  room/network/HUD checks also remained valid in the same runs.
- Real framebuffer captures: `runtime/players-4/0.camera.png`,
  `runtime/players-2/0.camera.png`, `runtime/players-1/0.camera.png`.
  Multi retains the central box view; Duel/Solo pan left with assembled characters,
  fans, box and item art retaining their fronts. Frame screenshots are from an
  explicit injected Kids presentation, not a normal randomly selected event.
- An initial probe compile failed due to a missing namespace import; corrected.
  Initial test discovery ran before compilation (0 tests, not counted). Two
  cleanup test attempts used EditMode lifecycle mechanisms that do not dispatch
  runtime OnDisable; changed the fixture to directly invoke the callback, then
  all six passed. Runtime behavior was not weakened to satisfy the fixture.
- Latest fixed-path Windows development player rebuilt. Normal game launch does
  not enable the probe. Shader/compiler warnings and the existing shutdown
  ComputeBuffer disposal warning were not expanded into unrelated work.
- Editor remains LobbyScene / Edit Mode / clean. Test processes stopped; previous
  dirty files and font/graph hashes preserved. No commit or push.
- Remaining limits: exhaustive cosmetics, every contact-animation camera angle,
  and cross-PC runs are not claimed. The initial yaw-only results above are
  historical; see the screen-aligned follow-up below for the current policy.


## Screen-aligned follow-up (user clarification, 2026-10-11)

- Replaced yaw delta with camera rotation times artwork in-plane lean; every
  assembled root has its plane normal parallel to the screen plane normal.
- Preserve pre-render artwork lower bounds and restore root position/rotation
  after rendering. No character bone or shared physics pose is rewritten.
- Focused tests: `screen-aligned/tests-final.json`, 10/10. Three arbitrary
  pitch/yaw/roll combinations, live pose preservation and actual 2D sprite vertex
  floor height included. Initial floor assertion incorrectly included the
  renderer's thick 3D culling box; the corrected test measures actual vertices.
- Build: `screen-aligned/build.json`, 0 errors / 4 warnings.
- Runtime replay: all 3 scenarios / 7 peers PASS, exit 0. Multi camera excursion
  remains 0 degrees; Duel/Solo excursion is 25 degrees then restores to 0. During
  rendering the remote character plane-normal error relative to the camera is
  0.000 degrees on both duel peers and Solo. Evidence:
  `screen-aligned/runtime/*/*.camera-result.txt`, scenario `summary.json`, and
  framebuffer `*.camera.png`. Reviewed duel pan screenshot.
- Editor restored/retained LobbyScene, clean Edit Mode. Prior font/graph hashes
  unchanged. No commit/push. Exhaustive contact-animation/cosmetic matrix remains
  deferred as C56-04.
