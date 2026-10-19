# Immediate threshold reward validation — 2026-10-14

## Decision and root cause

User-approved scope: grant at committed temperature decreases and retain fast progression. Old grants ran only on the one-second Prep tick, with the timer reset each turn. See the [scoped plan](../Plans/PLAN_immediate_threshold_rewards_20261014.md).

Implementation: `TemperatureThresholdRewards`, installed/disposed by the authoritative TurnManager. Existing thresholds, per-mode counts, starting four random items, capacity and claim flags are preserved. Check decreases from damage, fan ticks, Multi roster commits and ghost skills; exclude client/noncurrent/ghost/zero-temperature callbacks. Existing tick checks cannot grant a claimed threshold again.

## Evidence

Evidence root: `C:/Users/paek6/AbsoluteZero-SoloIntegration/output/immediate-threshold-20261014/`.

| ID | Check | Status and evidence |
|---|---|---|
| TR01 | Focused production-method regression | Passed24/24, `focused-tests-command.json`; first10/10 response was before Editor automatic import and is retained separately, not counted as new-code coverage |
| TR02 | Complete Editor regression | Passed1357/1357, zero failed/skipped/inconclusive, `all-editor-tests-command.json` |
| TR03 | Actual rapid Solo session | Passed8 checks: Advanced menu/graph, human Fan RPC, both Ready at0.123544s, real target31→28 receives random4→5 synchronously during AttackPhase, no duplicate next Prep, ordinary presentation and normal return; `solo-runtime-result.json` |
| TR04 | Fresh development build | Succeeded `build_6f5a47c04c03`, errors0, warnings488,59.076s; explicit Development/DetailedBuildReport at the existing permitted fixed test path; complete report retained in `development-build-status-latest.json` |
| TR05 | Remote Duel/Multi regression | Passed2/2 on the fresh build using actual Lobby/Relay: host+remote Duel and host+3remotes Multi inventory/threshold/mutation regression; all six processes exited0, no classified runtime errors and matching released state checkpoints; `relay-inventory/matrix.json` and raw peer logs |

TR03 controlled only initial temperatures. It uses an isolated Editor local coordinator on port18877 and does not stop/change the user's Desktop release process. The owned Editor test returns to Lobby and exits Play Mode. Console ground truth after that session reports compilationFailed=false and consoleErrors=0; retained counters include historical/negative-test entries and are not cleared.

The previous fixed development Player was copied to `frozen-player`; matching EXE SHA256 is recorded in `frozen-player-backup.json`. The existing release executable/submission ZIP has not been rebuilt or replaced by this source fix. Main and integration source edits are currently uncommitted.

## Final scope and limits

The reproduced short-Prep delay is fixed and the scoped checks passed. Editor tests are production-method tests; TR03 is a controlled real Solo session; TR05 is a controlled remote regression rather than new natural complete matches or a frame-by-frame visual review. A new ordinary build is still required to deliver this correction. The development build's retained warnings include Pipeline runtime-disabled notice, pre-existing unused fields and installed Sentis shader warnings; do not report warnings0 or a newly verified release build. No user Player was stopped, no minimum Prep duration was added, and no auto-commit/push occurred.
