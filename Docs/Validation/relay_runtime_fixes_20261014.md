# Relay/runtime fixes validation — 2026-10-14

Plan: [scoped implementation and tests](../Plans/PLAN_relay_runtime_fixes_20261014.md).
Prior findings: [immutable audit conclusions](relay_runtime_audit_20261014.md).
Evidence: `output/relay-fixes-20261014/`.

## Gates

| Gate | Status | Evidence / remaining |
|---|---|---|
| F01 waiting and cancellation | Passed | Eight actual Relay boundary scenarios and normal UI host-loss/retry |
| F02 authoritative room mode/count | Passed | Both mismatch directions; strict metadata and admission regressions |
| F03 membership recovery | Passed | Same-identity waiting recovery; repeated in-match refusal; new-room reuse |
| F04 random item cap | Passed | Ten focused tests; actual human/bot tick; four natural-game cap postchecks |
| F05 wind and Korean HUD | Passed, scoped | Live audio lifecycle/gain checks; 26 diagnostic UI images; not human audio/native-video certification |
| F06 shared regressions | Passed, scoped | Eight original controlled cases, corrected host-exit, ghost confirmation and four natural games; original failed report preserved |
| F07 ordinary build and preservation | Passed, scoped | 1343/1343 Editor; ordinary build, binary guards, normal startup/exit, 22-check final review and 258-file ZIP integrity |

No gate is passed from implementation alone. Current fixes are uncommitted and not published.

## Implementation and focused evidence — 14:05 KST

- B01/B02/B03: strict current room metadata, same-lease authoritative mode adoption, healthy lobby waiting with host-presence expiry, authenticated exact-membership recovery, and refusal of both recovered/fresh joins to already-started rooms are implemented. A fixed participant roster rejects new NGO connections after initial admission.
- Independent review found a further delayed cleanup/join race. `LobbyMembershipReservations` now retains the same room/identity reservation until the actual SDK request settles; different-room joins remain available. Three deferred-request regression tests cover this addition. The second review found no additional source blocker; actual Relay verification is still pending.
- B04: focused Editor tests **10/10** passed. Normal menu selected the approved Beginner encounter and admitted a real human/bot Solo session; a synchronous controlled fixture then exercised the actual `ApplyDuelTick` with consumed basic items. Both inventories followed **4 → 5 → 7 → 8**, repeated ticks preserved copy identities/counts and RNG, and original state was restored. This is not a natural-game or remote-replication pass. Evidence: `ig02-normal-menu-20261014-050219-989/result.json`, `ig02-fixed-20261014-050221-891.json`.
- B05: original generated mono wind resource imported without normalization. Live Lobby audio checks passed actual clip load, playback cursor advance, six production gain routes, SFX/master mute, replacement, restart, and stop. Settings/preferences/source state restored. No human hearing claim. Evidence: `wind-runtime/attempt-20261014-050115-055/result.json`.
- B06: ordinary Solo/Duel HUD phrases are Korean; phase callbacks retain current phase status rather than being cleared by selection cancellation. Actual new-build captures remain pending.
- Old development Player preserved and SHA256 verified for **449 files** (`previous-player-fingerprints.json`). Desktop/Demo and the previous audit remain unchanged.
- New boundary probe is explicit opt-in and fully guarded by `UNITY_EDITOR || DEVELOPMENT_BUILD`; new release exclusion check is pending. Pure item-cap oracle tests **11/11** passed and correctly rediscovered the old Duel cap violation.
- Full Editor suite started after all source fixes; no runtime gate is promoted from these preparations.

## First fresh runtime attempt and evidence corrections

- Full Editor suite: **1335/1335**, zero failed/skipped/inconclusive, 32.53 seconds (`editor-tests-final.json`). Development build `f4ec18bfc99a4b86bfb342f50cdce7b8`: zero errors, 488 warnings. Warnings are 485 shader variant warnings, a disabled Player Pipeline diagnostic, and two unused-field warnings; retained in `dev-build-warnings.json`.
- `runtime-regression-attempt01`: all eight boundary scenarios executed, four raw passes and four failures. Source/runtime fingerprints remained unchanged and every owned Player exited. Preserve this result as failed; it is not an eight-case pass.
- The delayed-start assertion read `{}` during the probe's Delete/Move gap. Duel mode-adoption reached gameplay but the same evidence writer hit a reader sharing lock. These are test-observation defects. The v2 probe uses atomic replacement, bounded asynchronous frame retries, retained critical responses and explicit permanent-write failure; the v2 reader retries only a current file, never a cached success.
- The abrupt-host and in-progress refusal scenarios reached Ready/no lobby/no transport, but the first checker incorrectly demanded no online lease. The existing production contract deliberately keeps an idle online lease for immediate retry. Normal initialization and existing coordinator tests require this behavior. Do not change production ownership to satisfy a faulty oracle. v2 separates retry readiness from strict full-Leave cleanup and requires actual subsequent room connection. See `ready-owned-retry-contract-review.md`.
- Follow-up UI review found a separate actionable issue: after joined-room failure, `ApplyMembership(true)` had hidden Create/Join GameObjects but Ready only restored interactability. A narrow visual membership resync and actual uGUI regression are in progress. No automatic router Stop/End change is made.
- The first development build, its manifest, all 449 files and their verified hashes were preserved under `development-attempt01-player/` before the next build.

## Current implementation and second runtime attempt

- The normal Room view now restores Create/Join/input on Ready without membership, clears the abandoned room code and participant rows, and preserves the typed join code and intentional retry lease. Eight actual uGUI regressions cover empty/joined membership and row rebuilding.
- Current full Editor suite: **1343/1343**, zero failed/skipped/inconclusive (`editor-tests-current-final.json`). Current development build `750f6ec65fb443d88eb3130db4612697`: **zero errors, three warnings**, 401369301 bytes (`dev-build-current-job.json`). The canonical build manifest records this exact build.
- v2 evidence tests: **11/11**; an actual Windows sharing-lock test preserves the old destination and queued response until successful atomic replacement. The probe is development-only; ordinary-build exclusion still requires the final build.
- `runtime-regression-attempt02` is the fresh boundary attempt. Its final `status.json`, per-case checks and process/hash audit are authoritative; intermediate counts are not a completed gate. A separate normal Room UI host-loss/retry test and the shared controlled/natural suite follow it.

### Relay service boundary results

`runtime-regression-attempt02` completed **8/8**, with **180 assertions and zero failures**. Independent review checked 18 usable-game peer snapshots, exact seat/owner/ready/scene consistency and the three surviving identities after the four-player disconnect. The abrupt-host timeout was detected within 58.688 seconds; the surviving client created a new lobby and connected with a new peer through actual Relay. Both wrong-menu mode directions, waiting-room recovery under the same identity, repeated in-match join refusal and subsequent fresh-room use passed.

All 3,706 source/configuration fingerprints, 449 runtime fingerprints and the runner were unchanged. Twenty normal child exits returned zero; three host/client terminations were deliberate scenario inputs. No owned Player remained. See `independent-network-runtime-review.md` for scope and negative-condition limits. These checks do not substitute for normal UI recovery or complete-match outcomes.

### Normal UI test preparation failures (preserved)

- `ui-timeout/attempt01` and `attempt02` ended before Join because the Editor domain reload temporarily moved the CLI endpoint and a readiness query failed/timed out. The next attempt explicitly reuses only the root-owned, already initialized fresh Lobby after the same no-session readiness checks.
- `attempt03` required a raycast immediately after changing panels in the same frame. The test now waits for a rendered layout frame before pressing the next real button.
- `attempt04` reached the real two-member waiting room and the owned host was terminated. Its marker parser incorrectly converted a JSON date token back through a string, losing subsecond ordering. Read the raw timestamp without automatic date conversion; keep the actual ordering requirement. This attempt remains failed, not retrospectively promoted.
- The same attempt's cleanup check wrongly required `OnlineLease == null`. Production retains a stale `_lease` reference after router ownership ends. An independent live read at 05:43:31Z confirmed Ready/None, no lobby, no online ownership, router Current null/not stopping, and transport stopped. The corrected cleanup oracle checks actual ownership/transport. See `ui-timeout-attempt04-review.md`. No production code changed to satisfy these test-oracle corrections.

### Actual normal Room recovery — passed

`ui-timeout/attempt05` passed the actual main menu → 1v1 menu → code input/Join → two-member Lobby → deliberate owned-host termination → exact host-presence timeout → restored entry controls → Create-new-room flow. Normal button raycasts, both actual membership identities, timestamp ordering, eight recovery UI checks and ownership-preserving new-room creation passed. The timeout took approximately 60.28 seconds from WaitingRelayCode. Normal Leave then released actual ownership/transport; no Player remained and all source/runtime pins were unchanged. The Editor was returned to Edit Mode.

Root and independent reviewer both inspected `recovery-room-ui-20261014-054525-014/room.png`: Create/input/Join and the Korean failure explanation are legible, old participant rows and the abandoned room code are gone. This image is the diagnostic camera/canvas render, not a native screenshot. See `ui-timeout-final-review.md`; subsequent Relay gameplay is established by the separate boundary/full-match runs, not this new-room-only UI case.

## Complete-match and shared runtime results

`runtime-suite/status.json` executed all 13 cases and retains **12 passes / one failure, overall=false**. Its 5,031 source/configuration and 449 runtime fingerprints were unchanged, and all owned Players exited. The failed host-exit case is preserved below; it is not retrospectively converted to a pass.

| Natural mode | Actual gameplay | Rounds / total turns | Item kinds used | Generation cap |
|---|---:|---:|---:|---|
| Approved Beginner | 609.9 s | 3 / 62 | 14 | 8, passed |
| Approved Advanced | 329.0 s | 2 / 35 | 13 | 8, passed |
| Duel, actual Relay | 303.1 s | 2 / 27 | 10 | 8, passed |
| Multi, actual Relay | 746.6 s | 3 / 44 | 16 | 4, passed |

All four naturally resolved games passed terminal-state, result/return and normal process-exit checks. Solo used the real approved production bot plus an automated human policy; remote matches used automated player policies. TimeScale was 1, with no forced match ending or balance override. These are functional regression matches, not population-level balancing certification. The Multi duration (12 min 27 s) remains a pacing observation; no tuning was changed.

The original Duel cap defect was independently exercised again: at 11→10 degrees with six random items and three eligible threshold rewards, new inserts stopped at **6→7→8**, without a ninth draw/insert. See `natural-duel-ig02-regression-evidence.json` and each natural case's `inventory-cap-postcheck.json`.

Eight controlled shared cases passed layout, RPC guards, failed-minigame handling, ghosts, round reuse and three menu-departure variants. A separate reviewer inspected 26 diagnostic PNGs; Multi winner/loser labels matched the replicated winner mask. Diagnostic result images have known re-render layering/background limitations and do not certify native framebuffer composition. See `independent-runtime-final-verdict.json` and `natural-gameplay-visual-review.md`.

One original ghost-case client logged `Curl error 23` before successful authentication/Relay/game completion. There is no request URL/stack for attribution; do not assume storage failure, authentication retry, or analytics origin. All peers passed and exited zero. Preserve this unattributed diagnostic and inspect a fresh ghost confirmation run.

### Host-exit oracle correction and controlled rerun

The failed original `host-exit-4-v1` waited for `OnlineLease == null` after production had already released router ownership and reached Ready/None/no lobby/stopped NGO. `OnlineLease` can retain a stale reference after release. Thus the original run did **not** reach or verify its subsequent three-player re-entry; its failure remains intact.

After all natural runs and Players ended, the exact 449-file tested Player was preserved and hash-verified under `natural-suite-player/`. Only the development `MatrixScenarioProbe` oracle changed: require `!HasOnlineOwnership` while retaining Ready/None/no-lobby/router-empty/not-stopping/NGO-stopped checks, and include concrete ownership diagnostics on timeout. No production rule or ownership implementation changed to satisfy the test.

Fresh full Editor tests passed **1343/1343**, zero failed/skipped/inconclusive, 32.90 seconds (`editor-tests-fixture-final.json`). Development rebuild `196b213a54a64169bf8b14908b008bb0` succeeded with zero errors and three warnings (`dev-build-fixture-job.json`). The corrected exact host-exit case and an independent ghost confirmation are the remaining runtime checkpoints before the ordinary build.

The corrected host-exit rerun passed actual four-player Relay admission, deliberate host process termination, all three survivors returning through Lobby/Ready with released ownership, then a new Relay allocation/join and identical three-player Prep state. All survivors exited zero. The generic `checkpoints_equal=false` is not a state mismatch here: this case emits no completed-combat `CHECK` records; the independent review instead requires identical `HOST_EXIT_REJOINED` digests from all three survivors. It does not claim completion of that new three-player game. See `host-exit-corrected-review.json`.

The fresh ghost confirmation passed all four peers and equal combat checkpoints, with no exception/FAIL/Curl entries. The original unattributed Curl23 remains preserved (original one / confirmation zero). `fixture-rerun-provenance.json` links the exact job, reports, logs and a clearly labeled post-run 449-file runtime snapshot. It does not invent launch-time hashes. The original canonical natural-build manifest remains unchanged; the sole source change after those games is the development fixture oracle.

## Ordinary build and packaging checks

Ordinary build job `2fa9f9580183470f9cf45f4479793813` succeeded: **zero errors**, 308703584 bytes, 34.33 seconds, Windows64 / Mono, product `AbsoluteZero_Demo`, four intended scenes in order, no Development/Script Debugging/Profiler. Output is the new `release-player/` folder; Desktop/Demo was not overwritten.

All 488 build warnings were retained in `release-build-warnings.json`: 485 shader warnings, one diagnostic that Player Pipeline is disabled, and two unused-field compiler warnings. A warning count is not represented as zero errors or a functional failure without evidence.

`release-boot/result.json` passed uninstrumented graphics/bootstrap startup and normal CloseMainWindow/exit, with no runtime exception/fatal error in the complete startup/shutdown log. All release-file hashes remained unchanged. This is a startup smoke test, not an ordinary-build complete multiplayer match.

The new Relay regression probe is absent from ordinary managed assemblies and present in the development positive control. The broader ordinary exclusion tool initially rejected the historical BehaviorFixture's generated `__GEN.NetworkVariableSerializationHelper::InitializeSerialization`; `ordinary-exclusion-20261014-063100-443.json` remains a failed inspection. Exact initializer IL and enabled-scene dependencies must be reviewed before changing this checker or approving packaging. No production behavior is changed simply to satisfy an overbroad test predicate.

### Final exclusion review and release disposition — 15:47 KST

Independent exact IL inspection found the generated initializer contains **only `ret`**, with no static constructors or diagnostic calls. The historical fixture code/assets are genuinely included through Lobby's serialized development encounter reference; do not claim all test assets are absent. The normal Beginner/Advanced/fallback selections use production DefaultBot. The only override that reads this development reference is compiled out of ordinary builds, and the actual diagnostic Probe component is placed only in the excluded fixture scene. See `behavior-fixture-release-review.md` and its unchanged-binary IL report.

The reviewed output-only checker permits only the exact named/static/void/no-parameter/no-generic/no-local/no-handler initializer with complete `nop*; ret` IL and null operands. Every other automatic initializer and static constructor remains rejected. `ordinary-exclusion-20261014-064354-596.json` passed; the original failed checker/report remain preserved. Game source and release binaries did not change for this inspection correction.

`final-checkpoint-review.json`: **22/22 checks passed**. `package-manifest.json`: **258 shipped files**, every copied file and every ZIP entry SHA256/CRC verified against the inspected and smoke-tested release; source files and evidence remained unchanged during packaging.

- New delivery folder: `C:/Users/paek6/OneDrive/Desktop/AbsoluteZero_Demo_수정본_20261014`.
- Archive: `AbsoluteZero_Demo.zip`, **89,160,601 bytes** (about 85.0 MiB).
- SHA256: `fb947c0a38958dea0cff984d23ee1e7137cdb9f9ecd253f2654cc58fc9b16406`.
- An unpacked complete game folder and Korean execution note are beside the ZIP. Old Desktop/Demo and prior submission files were not overwritten.
- All online participants must use this corrected version; older room metadata is intentionally rejected with feedback.
- No owned test Player remains. Editor returned to Edit Mode. No commit, merge, push, email or deployment occurred.

The scoped C# whitespace check passed. A full dirty-worktree check still lists existing serialized graph/meta trailing whitespace; those unrelated assets were preserved rather than reformatted.

Remaining limits: actual Relay traffic was exercised using separate processes on this single Windows host, not independent external devices/networks. Full matches used the development Player; the ordinary Player received binary exclusion and real startup/normal-exit checks. Diagnostic renders are not native framebuffer certification. One original Curl23 warning is still unattributed but did not recur in confirmation. Multi's 12m27s sample is a pacing observation, not a balancing change. No confirmed blocker remains within the completed validation scope.
