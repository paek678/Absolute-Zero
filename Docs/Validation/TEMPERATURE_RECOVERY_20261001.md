# Temperature recovery presentation — 2026-10-01

## Requested behavior

Recovery should fill 2–3 times faster in every mode, while damage retains its existing gradual display. Recovery must not jump to full health at round/turn transition.

## Change

- Shared `TemperaturePresenter`: damage remains 6 degrees/second; recovery is 15 degrees/second (2.5x). Local and all remote bars use the same direction-aware step.
- A full 0-to-37 recovery takes about 2.47 seconds instead of 6.17 seconds. `MoveTowards` ends exactly at the target without overshoot or an asymptotic tail.
- `GameUIManager` uses `ReconcilePrepTemperature` at Prep entry: existing pending-damage settlement remains, but recovery keeps its current visible value and continues smoothly.
- Actual temperature, healing amount, damage, authority, synchronization and item/VFX impact timing are unchanged.
- GameScene, GameScene_Multi and GameScene_Solo all reference the shared GameUIRoot that creates this presenter.

## Validation

Unity automatic compilation succeeded. The actual presenter and Unity sliders were exercised with a synthetic bridge in an isolated Editor preview scene, manually stepping frames at 30/60/144fps. **30 cases passed**: 1v1 local seats 0/1, Solo human seat 0, all local perspectives in 3-player and 4-player Multi.

Checks: original damage speed, 2.5x recovery, exact completion, monotonic frame increments, Prep entry without a healing snap, existing VFX override/clear, small recovery and damage interrupting recovery. No authoritative snapshot mutation.

Evidence: `C:/Users/paek6/.codex/backups/absolute-zero/temperature-recovery-20261001/validate.cs` and `result.json`.

The initial checks above isolate shared HUD behavior. The additional standalone runs below cover actual sessions. Console entries from earlier deliberately failing-input equipment/lobby tests were not cleared. Existing dirty LobbyScene and local MCP configuration were preserved.

## Standalone runtime follow-up

Built a fresh Windows Development player, then recorded the actual local/remote Unity slider values in LateUpdate. `AZ_TEMPERATURE_CAPTURE_DIR` enables an observation-only development probe; it does not write temperature, inventory, phase, input or presentation targets. The matrix fixture waits 4 seconds on clients and 6 seconds on the host after PASS when this flag is set, allowing the recovery tail to complete.

| Run | Scope | Result |
|---|---|---|
| 1v1 `duel-mini`, actual Relay | Host + client, real minigame/round transition with fixture setup | Both peers PASS; both local and remote bars observed; no recovery jumps |
| 4-player `multi-round`, actual Relay | Host + 3 clients; fixture marks three seats dead, production flow performs round reset | All four peers PASS; 12 dead-seat recovery observations complete without jumps |
| Solo seed 4101, mixed input policy | Natural production BT match, no forced temperature/round/winner | PASS: 1 match, 2 rounds, 31 combats, 12 acceptance checks, no logged errors |

### Measured behavior

- 1v1: 0 to 36 degrees in 2.401–2.402 seconds on both peers; 34 to 37 in about 0.20 seconds.
- Multi: 0 to 36 in 2.400–2.409 seconds across all four perspectives and three recovering seats.
- Solo round reset: 0 to 36 in 2.400 seconds, and 28 to 37 in 0.600 seconds.
- Solo item recovery: Warm Tea 10 to 17 in 0.467 seconds; Hot Americano 25 to 30 in 0.333 seconds; Hot Pack 25 to 37 in 0.800 seconds. Actual combat logs confirm the corresponding item effects.
- The target can drop from 37 to 36 before display recovery finishes because the normal fan tick resumes. This is authoritative gameplay cooling, not incomplete UI healing. The probe's `roundRecoveries` counter only counts endpoints at 37; use the CSV/analyzer for round transitions ending at 36.
- No observed positive step exceeded `15 * deltaTime` plus numeric tolerance. Existing damage speed remains 6, as covered by the isolated presenter cases.

### Evidence and limits

Root: `C:/Users/paek6/.codex/backups/absolute-zero/temperature-runtime-20261001/`.

- `duel/`, `multi/`: per-peer actual Relay logs and matrix results.
- `solo/report.json`: natural match result and checks; `solo/peer0.log`: item effects.
- `*-telemetry/*.csv` and `*.json`: actual per-frame slider observations and raw counters.
- `analyze.py` / `analysis.json`: recovery segments and independent frame-step checks.
- Representative visually inspected frame: `multi-telemetry/41668-10f1b7-06-seat3-mid.png`; 1v1: `duel-telemetry/12808-14cd7a-02-seat1-mid.png`.

1v1 and Multi framebuffer captures show real intermediate recovery. Solo's hidden-window framebuffer captures were black and are excluded as visual evidence; its slider values, combat logs and acceptance results remain valid. Multiple same-frame capture requests can collapse to one image, so raw capture request counts are not image counts. These are focused recovery tests, not a new full multiplayer regression or multi-PC latency certification. Three-player mode is covered by the 30 isolated cases, not a new standalone run.

Build succeeded with 0 errors and 486 warnings: 485 shader warnings and one missing optional RuntimePipelineConfig warning. These are not a warning-free build. Build-generated font changes were restored to the pre-test backup; the user's dirty LobbyScene remained byte-identical. Editor remained in Edit Mode, and test-owned players exited normally.
