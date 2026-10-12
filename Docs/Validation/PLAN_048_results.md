# PLAN 048 validation — fixed day/night modes

## Change

- Solo and online 1v1 scenes use the existing PavilionDay map and its lighting. Multi retains PavilionNight for three/four players and four-to-three continuation.
- Server environment selection excludes SunnyDay, CicadaSong and HeatWaveWarning in Multi. Day retains its original seven-effect pool; night samples CoolBreeze, Kids, Ambulance and SummerVacation equally. One random draw per selection is preserved.
- CoolBreeze no longer replaces night lighting with its daytime color/intensity override. Wind and gameplay effects remain active.
- No network schema, RPC, item values, turn timing, camera or spawn-anchor changes. Saving the duel scenes serialized existing FPS contact-camera default fields at their unchanged C# initializer values.

## Executed checks (2026-10-06)

| Check | Result | Evidence |
|---|---|---|
| Focused mode selection and scene tests | 6/6 pass; 6,000 draws across three modes, allowed membership/order and exactly one RNG draw; map prefab, anchors and directional light | `output/validation/plan048/editor-tests.json` |
| Full Editor suite after final VFX correction | 1,071/1,071 pass | `output/validation/plan048/editor-tests-final.json` |
| Final Windows development build | Succeeded; 0 errors, 4 warnings | `output/validation/plan048/build-final.json` |
| Initial runtime matrix | Relay duel rematch, three-player input, four-player round rollover and local Solo BT all pass; client map observation incomplete in first probe | `output/validation/plan048/*/report.json` |
| Final runtime checks | Relay duel rematch, three-player input, four-player round rollover, local Solo BT, ordinary four-player four-combat progression: 5/5 pass, 14 peers with expected map observations | `output/validation/plan048/verified-summary.json` |
| Replicated night environment | All four peers observed CoolBreeze; four combat checkpoints synchronized; no authored night-light mismatch | `output/validation/plan048/natural-input/report.json`, per-peer `[MAP048]` logs |
| Visual inspection | Solo and remote duel day captures; host and remote Multi night/wind captures inspected | `final/solo/03-prep.png`, `final/duel-2-w0/client1.map-2.png`, `natural-input/host.map-2.png`, `natural-input/client1.map-2.png` under the same evidence root |

Build warnings: missing optional Player Pipeline configuration, existing TMP font importer inconsistency and two unused fields. No production compile error. The font and bot graph bytes match the task-start backups. Multi and Lobby scene bytes match their task-start backups.

## Corrections during validation

- Corrected namespaces in new tests/probe before the passing runs.
- A dirty-scene confirmation blocked the first test request. Cancelled it, preserved the LobbyScene disk snapshot, saved its identical in-memory content, and restarted tests successfully. No LobbyScene content change.
- First probe required server-only `InitializationState.Complete`, preventing remote client observations. Replaced this with assembled configuration plus spawned TurnManager gating and reran the final player checks.
- A batch-mode ordinary visual run could not begin target hover because it requires a mouse device. Its captures were black; that run is not passing visual evidence. Repeated without batch mode, with hidden startup and unchanged production code: target hover, four completed combats and replicated CoolBreeze passed. Inspected explicit diagnostic camera renders, not black occluded swap-chain captures. The failed attempt remains in `final/natural-four/`; `verified-summary.json` identifies the replacement run.
- Static VFX review identified CoolBreeze's fixed daytime light override. Disabled only that override in Multi; all night peers are now observed against their map prefab's authored directional-light color/intensity every 0.5 seconds.
- Repository-wide whitespace check reports a pre-existing LobbyScene blank-name line; this task's changed tracked files pass their scoped check.

## Deferred validation ledger

| ID | Status | Exact follow-up and closure evidence |
|---|---|---|
| M48-01 | Closed | Final 14-peer mode/map observations; ordinary Relay four-player CoolBreeze announcement and unchanged night-light checks through four combats. See verified summary and natural-input logs/captures. |
| M48-02 | Deferred broader QA | Cross-PC/GPU visual review of day/night readability at other display settings. Current single-PC multiple-process evidence does not establish this. |
| M48-03 | Deferred broader QA | Repeat all four allowed environment presentations across round resets and 4→3 departure in a single long match. Pool membership/reset ownership is covered by code and focused tests; the current natural runtime sample exercised CoolBreeze, not all four effects. |

Completion: Editor restored to LobbyScene, clean/Edit Mode; console reports no current compile/console error. Test players exited. No commit/push performed; unrelated pre-existing work remains.

The original pre-change play videos remain historical evidence and show the former maps. They are not previews of this change.
