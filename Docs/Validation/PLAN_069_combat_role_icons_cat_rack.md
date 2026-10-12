# PLAN 069 — Combat role icons and cat rack validation

Date: 2026-10-12. Scope: item playback indicators and inventory-relative cat presentation.

## Result
Requested presentation checks passed. A separate environment VFX occlusion was observed and remains open (D01 below); this is not a claim that all game visuals passed.

### Changes
- Existing Resources/UI_attacker and UI_target sprites are shown during an actual non-self item presentation. The context retains logical actor/recipient bindings; selection changes or item consumption cannot retarget the cue.
- Self-use items are excluded by TargetMode. Scoped cleanup and current binding checks cover normal completion and cancellation. No network state, RPC, damage, inventory rules or targeting policy was changed.
- Visible opponents receive overhead icons. The follow-up decision hides both roles for the local FPS participant (superseding the initial local health HUD marker). Health bars, scorecards and clock are avoided; the constrained front seat can put the icon beside its overhead bar.
- Cat destinations previously used the character visual position when a Multi impact callback existed. They now use the same stable inventory anchors as the rendered relative seat layout. The sweep follows that row instead of global X; jump height is reduced, new cat art is camera aligned, and Multi cat feet clear the painted item-slot plane.

## Follow-up: opponent-only role visibility (2026-10-12)

- Local attacks remote: target icon only. Remote attacks local: attacker icon only. Two remotes interact: both icons. Self-use and completed actions: neither.
- Updated the runtime probe to assert each viewer's local seat independently. No gameplay/network change.
- Build job `560958a4e96d479a8d6c6d60d9684625`: success, 0 errors, 2 warnings.
- `output/validation/plan069/local-hidden/result-4.json`: real Relay host + three clients, six turns, all exit 0, checkpoints equal. **228 checks passed, 0 failed** (57 per viewer), including role cleanup and cat regression.
- First Cat action (actor 0, target 1): host/local 0 sees target only; client2/local 1 sees attacker only; client1/local 2 and client3/local 3 see both. Actual frame `local-hidden/role-cat-4-w0/client1.role-02-Cat-cat-rummage.png` visually inspected: front attacker and right recipient icons both visible, no local HUD marker.
- Earlier duel/Solo evidence below predates this visibility-only follow-up; these modes were not rerun in this follow-up. Deferred D01-D03 remain unchanged.

## Original evidence
All paths below are relative to the project root.

| Check | Evidence | Result |
|---|---|---|
| Unity 6000.3.11f1 automatic compilation | output/validation/plan069/console-final.json | Current compiler errors 0. The retained tool history includes an initial fixture missing-using error, fixed before builds. |
| Final Windows Development build | CLI job 64614f6eb8e94c72a7c1ab856d3f7313 | Success, errors 0, warnings 4, bytes 399260254. Fixed allowed executable path reused. |
| Four-player Host + 3 real Relay clients | output/validation/plan069/final-v5/result-4.json | All exit 0, six completed turns, peer checkpoints identical. Cat from seat 0 to seats 1/2/3, ice cream feeding, Fan, Warm Tea. |
| Multi role visibility, self-use exclusion, cleanup, cat row and clearance | final-v5/role-cat-4-w0/*.log under the same folder | 228 checks passed; all 12 cat path samples have zero horizontal distance to their viewer-relative row. All 12 clearance samples are above the cell plane. |
| Duel Host + client | output/validation/plan069/final-v3/result-2.json | Both exit 0, four completed turns, checkpoints identical, 36 checks passed. Same final production icon placement; later changes only isolated diagnostic events and Multi cat clearance. |
| Solo original feed choreography regression | output/validation/plan069/solo/solo-2-ice-0to1/report.json | Passed on final build: original ordered begin/gesture/impact/end cues, one FPS gesture and restored transforms. Its captures are diagnostic re-renders, not used as final framebuffer proof. |
| Actual displayed frames | output/validation/plan069/screenshots/ and index.html | 1600x900 ScreenCapture frames from real standalone players. |
| Existing unrelated data | SoloDuelDecision.asset and MalgunGothic SDF.asset SHA256 | Same as session baseline: 96FECF586F3162C3980138F4E98EDC0EB09B958204CA97822BC4D880E0D65603 and 6FF22FD50D1267210C171BE27408DAD03C0B250ECC5239B8650A28D411D601F1. |

Combined targeted role/cat observations: **264 passed, 0 failed**, plus the separate Solo contact regression. These are observations in focused fixtures, not 264 full matches or a balance test. Fixtures seed required items/temperature and disable fan cooling; production selection and resolution still run.

## Issues found and corrected during review
1. Initial icons intersected the health bar. Measure the actual HP slider rect (600px), not the parent Canvas rect (100px), and avoid the scorecards/clock. The local role moved from beneath the name badge to its own health HUD because middle badges overlapped the remote bar.
2. Cat centre lay on the painted slot plane, letting the cell border cut through the sprite. Lift Multi cat feet above the plane using sprite bounds and scale; final-v5 verifies the clearance.
3. Initial instrumentation reused the contact test event and would have changed its exact cue sequence. Use a separate development-only item presentation event. The existing Solo contact regression passed afterward.

Earlier intermediate runs are preserved in first/, final/, final-v3/ and final-v4/. Their visual shortcomings are not substituted for the final-v5 screenshots.

## Remaining observations / deferred verification ledger

| ID | Status | Exact stimulus and needed evidence |
|---|---|---|
| D01 | Observed, open, outside current change | Four-player CoolBreeze active, Fan item presentation in final-v5 turn 5. A large pale/black diagonal effect obscures the scene. See screenshots/07-observed-breeze-occlusion.png and final-v5/role-cat-4-w0/client1.role-08-Fan-item-start.png. Trace EnvironmentVFXManager breeze assets/materials, scale and camera-facing behaviour; capture before/after at this phase. The current task did not edit environment VFX. |
| D02 | Not exercised live | Disconnect or disable the presentation owner midway through a non-self action, then re-enter. Capture no stale icons. Static review confirms identity/current-execution guards, resource cleanup, OnDisable and paired render callback subscription; normal per-action cleanup was tested live. |
| D03 | Coverage limit | Unusual aspect ratios and extreme cosmetic head sizes. Current visual review used 1600x900; verify overhead icons against the authored HUD and frame edges for other target resolutions. |

The Solo process also logged a ComputeBuffer disposal warning during shutdown; no source attribution or leak fix is claimed in this scope. First build emitted many existing inference shader variant warnings; subsequent builds returned 2–4 warnings. No commit or push was performed.
