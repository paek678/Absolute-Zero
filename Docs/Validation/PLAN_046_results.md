# PLAN046 - Implementation and validation results

Date: 2026-10-06. Baseline: `0109dd8`. Scope: approved lobby/wardrobe/HUD/target-marker cleanup.

## Task results

| Task | Implemented change | Observed evidence |
|---|---|---|
| T01 | Remove the baked lobby watermark, retaining sprite GUID/import settings | Source and output remain 2752 x 1536. Pixel differences are confined to `(2445,1229)-(2577,1365)`; all pixels outside that rectangle are identical. Unity lobby capture inspected. |
| T02 | Remove TimeAlarm creation/references/jitter; decouple countdown sound and handle disable/re-enable | Six focused countdown/lifecycle cases pass. In live Solo, the actual Clock AudioSource was stopped at 6, playing at 5 and 1, stopped at 0 and on HUD disable. Synthetic snapshots were applied to the presenter only, not the authoritative clock. |
| T03 | Hide only Bottom tab; retain all five binding entries and enum IDs; update prefab builder | Production Lobby has four evenly spaced visible tabs, valid bindings and Back/Tail navigation. Isolated equipment write/read processes pass, including accessory persistence and protection of the real user save. |
| T04 | Small local red downward arrow above character/HUD, no mouse ribbon | Real Relay 2/3/4-player input scenarios pass after the design-board fix. Four-player run verifies all 12 hover pairs; cyclic target confirmation, cancellation, minigame retry and Ready restrictions pass. |

The arrow is presentation-only. Input snap thresholds, item identity, RPC admission, authority, turn deadlines and appearance serialization are retained. Solo/1v1 automatic targeting and the separate ghost selector are not redesigned.

## Debugging findings and repairs

1. **Multi target clicks blocked by a design board.** First 3/4-player runs failed at minigame start. Logs show `BattleResult` and `Profile (1)` GraphicRaycaster hits. Reproduction without diagnostic capture failed too. The active scene root Canvas contains only draft graphics/text, not the runtime result presenter. Its zero serialized scale is not a reliable hiding mechanism: Unity restored scale to one on load. Set that root inactive; retained every child/resource. Multi scene diff is exactly one active flag. Subsequent Relay input runs pass.
2. **North marker versus existing hat sample.** First corrected screenshots showed overlap with the SpriteResolver object named `Test`. Excluding a renderer solely by that generic name was wrong. Its visible bounds now participate in placement; the sample itself is preserved. Final visual rerun recorded below.
3. **Countdown sound was coupled to the deleted banner.** Replaced that coupling with an idempotent sound state. Disabled presenters cannot restart sound from subscribed snapshots; enable reconciles current state.
4. **Validation robustness.** The input probe now selects actual live registered seats, not modulo participant count, and checks every available hover pair plus input blocking. Diagnostic capture preserves root Canvas transforms and skips zero-scale canvases. The design-board fault was a product scene fault, not attributed to capture after the no-capture reproduction.
5. **Edit Mode fixture correction.** The first new disabled-presenter test assumed ordinary MonoBehaviour callbacks run automatically in Edit Mode. It now explicitly invokes the lifecycle callback; product behavior was then checked in Play Mode.

## Executed checks and evidence

Evidence root: `output/validation/plan046/` (local ignored artifacts).

- Full Editor suite: **1050/1050 pass**, including eight new focused cases; `editor-tests-final.xml`. After the last placement adjustment, focused cases **8/8 pass** again.
- Development Windows build: succeeded, **0 errors / 487 warnings**; `build-summary.json`. Warnings include package Sentis shaders, absent Pipeline runtime configuration, and the existing dynamic TMP font importer. An intermediate build warned about pending imports; the final build does not contain that warning.
- Actual Relay: `relay-fixed/matrix.json`, **5/5 scenarios pass**: input 2/3/4, preparation departure 4 -> 3, multiple rounds. Nine input peers recorded **678 input assertions**; these include looped checks and are not 678 independent scenarios. Departure is a gameplay regression, not proof of hovering a departed target at the exact disconnect frame.
- Wardrobe: `closet/write.json`, `closet/read.json`, both passed. These use a private production UI copy and isolated save key; actual Lobby entry was exercised. Counts include repeated readiness checks.
- Live Solo HUD audio: 6/5/1/0 -> false/true/true/false; disabling while active stopped the source. This tests the real source through synthetic presentation snapshots, not every mode's natural countdown.
- Screenshot examples: `t01-lobby-background.png`, `t03-real-screen.png`; final targeting evidence under `final/input-4-w0/`.
- Initial failure evidence retained under `relay-input/` and `relay-no-capture/`. Do not use their occluded captures as final visuals. The older `t03-closet.png` diagnostic-camera capture is also rejected; `t03-real-screen.png` is the actual composed UI.
- Final accessory/hat Relay input rerun passed; the broader natural Solo run stopped at a lobby button-overlap assertion before reaching combat. See completion entry below.

T01 retouch method: built-in imagegen edited a cropped logo patch with an instruction to remove only the sparkle and reconstruct matching bark/grass without reframing. A broad generated candidate was rejected. The accepted patch was feather-composited into the original, then compared pixel-for-pixel outside its small repair region. No new background composition was substituted.

## Remaining acceptance ledger (single source)

| ID | Current evidence/status | Additional evidence needed |
|---|---|---|
| V01 | Focused pass: pixel-local repair, preserved dimensions/GUID, live lobby capture | Non-16:9 and 1920x1080 human review of menu framing |
| V02 | Focused pass: common presenter tests, real Solo audio source; online input/phase regressions | Natural 5-second audio observation in all three modes; early Ready/exit at the exact warning boundary |
| V03 | Pass for four visible tabs, automatic Back/Tail directional navigation, persistence/restart | Direct human Tab-key traversal and preferred visual spacing |
| V04 | Pass for 12/12 four-player hover identities and cyclic real click/server confirmation | All 12 pairs separately submitted to server, if exhaustive pair acceptance is required |
| V05 | Pass: outside-snap hide, target switching, Esc/right/same-item cancel, Ready, invalid-target rejection, temporary input block | Human modal overlap/rapid input stress |
| V06 | Gameplay departure/next-round regressions pass; behind-camera/viewport and disabled callbacks tested | Marker-specific hover during target death/departure and sparse-roster re-aim; camera/resolution switch while aiming |
| V07 | 960x540 west/north/east captures inspected; final 4-hat run passes with north marker above the preserved sample | Other resolutions, all future/custom hat extents and human judgement of arrow size |
| V08 | No tentative-target RPC introduced; duel input passes; all four peers verified matching Head/Top/Back/Bottom/Tail snapshots in the final Relay run | Independent idle observer proving another client's hover is invisible, dedicated ghost-input visual regression; broader Solo gate below |
| V09 | Build and tests pass; original Lobby restored clean/Edit Mode | See separate diagnostics below; do not equate test pass with zero historical console entries |

## Diagnostics and preservation

- During Editor stop after a Solo session, existing shutdown paths logged `Lobby scene could not load` while the Editor was leaving Play Mode. Earlier reload/stop overlap also produced NGO NetworkSceneManager disposal exceptions. These paths were not changed by this UI task; report them as unresolved Editor-stop diagnostics, not fixed or proven baseline-free.
- Full-suite negative tests intentionally log invalid cosmetic/failed lobby removal cases. Preserve console history; distinguish those expected test messages from runtime failures.
- Player termination logs include native allocation/ComputeBuffer disposal warnings. No claim of a leak-free application is made from these short checks.
- The preexisting dirty SoloDuelDecision graph remained byte-identical (SHA256 `96FECF586F3162C3980138F4E98EDC0EB09B958204CA97822BC4D880E0D65603`). The already-dirty MalgunGothic SDF font was automatically regenerated during Unity validation. Its initial bytes were not backed up; it was not reset to HEAD, because that would erase the user's prior edits. Review that generated font separately before committing.
- No commit/push performed. No gameplay rules, item balance, map positions or network schema changes.

## Completion entry

- Final build `build_90f2c3d57beb` succeeded. Final accessory Relay input: **1/1 scenario, all four peers pass**, another 356 input assertions and all 12 hover pairs. Each peer logged `[COSMETIC] VERIFIED` with the same authoritative outfit mapping. Evidence: `final/matrix.json`, per-peer `*.appearance.json` and logs/captures. Combined successful Relay runs: **6 scenarios**; do not add failed initial runs to this total.
- Final north capture `final/input-4-w0/host.input-aim-2.png` confirms the arrow clears the hat/sample. West/east captures are `host.input-aim-1.png` and `host.input-aim-3.png`.
- **Separate issue, not passed:** the broader natural Solo smoke run failed `Solo button does not overlap ArenaBtn` at 1424x714, before entering combat. Evidence: `solo/report.json`. The test does not establish a Solo combat failure. Lobby layout was not edited by PLAN046; preserve it for a dedicated button-layout correction. Do not waive the assertion or report natural Solo regression as passed.
- Verdict: the four requested changes are implemented and their focused gates pass; whole-project/extended acceptance is not complete. In particular the separate Solo lobby-layout assertion, Editor-stop diagnostics and V01-V09 remaining cases remain open.
