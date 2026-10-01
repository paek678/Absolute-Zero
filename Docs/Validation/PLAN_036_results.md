# PLAN 036 - Solo implementation results

Date: 2026-09-29
Scope: T00-T09 implementation gates completed. T10 automated integration and final evidence reconciliation are recorded below; offline-environment and shipping acceptance are separate remaining gates.
Plan: [detailed task contracts](../Plans/PLAN_036_solo_detailed_implementation.md#8-ordered-implementation-tasks-and-gates).
Reviewed planning baseline: R2, SHA-256 `9CB94A2E8E61F0AFB67DBCA2A12834F17BB679870D54FECCE43AB64DE811D136`.
Source baseline: HEAD `f6104bb` plus the pre-existing dirty working tree; no commit created.
Evidence roots: `output/validation/plan036/`, with dated task directories T00-T10 (repository-relative; each task section identifies its runs).

## Task status

| Task | State | Scope / next gate |
|---|---|---|
| T00A | READY_FOR_NEXT | Current executable 1v1 baseline, existing tests and bounded player scenarios captured |
| T00B | READY_FOR_NEXT | Authoring/reload, Editor/player lifecycle, package and existing-game regression gates passed |
| T01 | READY_FOR_NEXT | 61 new configuration/asset tests and 76 existing tests passed; draft remains nonlaunchable |
| T02 | READY_FOR_NEXT | 157 Editor tests, 23 standalone lifecycle assertions and actual Relay duel passed |
| T03 | READY_FOR_NEXT | 182 Editor tests, 58 standalone assertions, 8 existing-game scenarios and actual Relay duel passed |
| T04 | READY_FOR_NEXT | Editor 200/200, production presentation fixture 52 checks/14 valid camera captures, 8 online regressions and actual Relay duel passed |
| T05 | READY_FOR_NEXT | Editor 219/219, local fixture 56 checks, 8 online cases and actual Relay duel passed |
| T06 | READY_FOR_NEXT | Editor 238/238, 234 runtime checks/21 catalog items, 8 online scenarios and actual Relay duel passed |
| T07 | READY_FOR_NEXT | 238 Editor tests; two resolutions each pass 20 runtime assertions and five camera/canvas captures |
| T08 | READY_FOR_NEXT | 252 Editor tests; 31 actual graph/runtime checks and six valid camera/canvas captures |
| T09 | READY_FOR_NEXT | 252 Editor tests; four natural Bo3 matches/eight rounds, three replays, 31-second result idle and exit passed |
| T10 | AUTOMATED_PASS / FINAL_ACCEPTANCE_PENDING | All available checks passed; physical offline QA, final release data and manual presentation/input acceptance remain |

**Current verdict: T00-T09 READY_FOR_NEXT; T10 AUTOMATED_PASS / FINAL_ACCEPTANCE_PENDING (2026-09-29).** This record tracks execution evidence; planning reviews remain historical reviews, not runtime results.

## Extended debugging and extension audit - 2026-09-29

Status: **AUTOMATED_PASS - bounded extended scope**. Three production defects and one test-budget limitation were repaired and revalidated. Evidence root: `output/validation/plan036/extended_20260929/`; machine-readable rollup: `final-summary.json`. Baseline: 916 source/Editor/test/configuration/scene/settings/package files hashed before edits. This run extends, rather than silently relabels, the earlier T10 evidence. Required physical-offline, final tuning, release and manual acceptance remain open.

| ID | Demonstrated problem | Repair | Evidence / current state |
|---|---|---|---|
| E036-01 | Neutralize/Red Card has real gameplay utility, but the bot estimator assigned zero to every score; even as the only legal item it could never be chosen | Include NeutralizesTarget alongside other existing sabotage utility; no damage/effect/balance change | New regression failed at Utility=0 before repair; full Editor suite after repair **262/262 PASS**; actual graph replans a removed CopyId and selects Red Card in `profile-lifecycle.json` |
| E036-02 | A new unsupported ItemDataSO subtype passed Solo configuration validation, then ItemEffectRuleSnapshot.From could throw during BT evaluation | Resolve the shared effect mapping at catalog validation; reject with catalog index/reason before starting the network | New invalid-catalog test failed because launch validation returned true; after repair **PASS** |
| E036-03 | During an 8-second optional thought the bot crossed from a healthy temperature to 4. The wait ended, but the previously chosen Fan attack was still submitted despite available healing | Crossing into the existing survival threshold triggers the graph's bounded observation/choice retry; no hidden opponent state, extra turn or item-delay bypass | Real pre-repair trace in `urgency-before.json` shows Resource/Fan -> Pending -> Ready and the healing assertion fails; repaired trace in `profile-lifecycle.json` selects Recovery and preserves its full delay |
| E036-04 (test harness) | The old fixed 300-second match timeout ended seeds 73 and 911 after 33 actual combats each while their second rounds were still progressing, without a Unity error | Expose a bounded 30-600 second per-match diagnostic budget; derive player/runner watchdogs from the same budget and record it in reports | `mixed-73/` and `mixed-911/` retain failed 300-second attempts. Same-seed 600-second retries both **PASS**, completing in 311.43 and 310.45 seconds; no phase, item, time scale, damage or winner change |

Before/after unit reports are `editor-before.json` (260 passed, two new reproducible failures) and `editor-after.json` (262 passed). Current build `build.json`: 0 errors/489 existing warnings. Test-runner unit checks: 8 passed. The urgency repro is an explicitly injected temperature boundary, separate from natural gameplay evidence.

Only diagnostic time-budget reporting changed after the first development build; `build-budget-retry.json` succeeded with 0 errors/4 incremental warnings and identical repaired production logic. The four warnings do not mean the prior full-build warning debt disappeared. The PowerShell runner parses successfully and enforces the same bounded duration; `python-tests.txt` records eight passing runner-unit tests.

Profile/lifetime fixture: **43 checks across 12 fresh entries passed**, including Red Card-only and long-item-delay/zero-retry fallback. Every stopped session retained zero runtime BehaviorGraph instances, no bot/player/controller, one original NetworkManager and one transport. The original tuning asset was unchanged. Four-seat winner regressions under **80 ms one-way delay and 1% configured loss passed 4/4**, with identical peer checkpoints and eight actual dropped datagrams. A fresh **real Relay 4P** case passed using four distinct anonymous profiles and verified allocation/joins. Original duel and duel-mini scenarios passed **2/2**. Evidence: `profile-lifecycle.json`, `network-stress/matrix.json`, `relay-four/matrix.json`, `duel-regression/matrix.json`.

### Natural mixed-item corpus

| Seed / final evidence | Bo3 matches | Rounds | Turns / combats | Elapsed | Result |
|---|---:|---:|---:|---:|---|
| 11 / `mixed-11/` | 1 | 2 | 26 / 26 | 257.17 s | PASS |
| 73 / `mixed-retry-73/` | 1 | 2 | 33 / 33 | 311.43 s | PASS |
| 911 / `mixed-retry-911/` | 1 | 2 | 34 / 34 | 310.45 s | PASS |
| Completed total | **3** | **6** | **93 / 93** | **879.04 s** | **27 assertions, 0 unexpected errors** |

All three final runs use ordinary owned-item selection/Ready commands and the production bot graph, without forcing inventory, temperature, phases, winners or time scale. They exercise normal combat, round reset, results, explicit menu return and graph cleanup while UGS stays uninitialized. The scripted human samples **non-mini-game** items; bot penalty and human mini-game coverage belongs to the separately recorded T05/T06 and duel-mini runs. A Tarot request can legally reject before the opponent is Ready; the report preserves that rejection rather than claiming an effect. These are varied, bounded examples, not exhaustive balance or physical mouse acceptance. Starting seeds aid diagnosis but real-time cooling/recovery means full-match bit-identical replay is not asserted.

Both original timeout reports remain failed evidence. Their logs show continuing combat rather than a stalled phase. Rapid Ready, defense and passive recovery can extend a match; the successful retries justify increasing the **test budget**, not changing approved game rules. Match length and bot difficulty still require V036-08 play-feel review.

Final preservation: **916 baseline files, six intended modifications, two new source/meta files, zero missing**. Three modifications are production Solo logic; the others are one development probe and two regression-test files. No baseline scene, tuning asset, ProjectSettings or package changed in this extension (`preservation.json`, `source-provenance.json`). New fixture metadata has one unique GUID and a matching source (`meta-audit.json`). Targeted whitespace and PowerShell parse checks passed. Editor remains stopped, not compiling/failing, with the original clean comparison scene restored (`editor-state-final.json`); all test players exited.

Future extension ownership and decisions are mapped in [Solo setup](../SOLO_BOT_SETUP.md#connecting-future-design-work). Existing supported item types, delays and graph branches have extension points. New effect types need shared effect/AI/validation work. Starting stat/loadout overrides, extra-action/reveal-aware behavior, pause/save and online bots are not implemented by this repair. No new package or balance decision was introduced.

## T10 - Final integrated validation

Evidence: `output/validation/plan036/t10_20260929/`. Setup guide: [Solo bot setup](../SOLO_BOT_SETUP.md).

### Repairs found by final integration

1. **Persistent NetworkManager accumulation:** returning to LobbyScene instantiated another NGO manager. NGO 2.11.2 makes every enabled manager persistent, even when it is not the Singleton. The first real Solo -> Relay duel test failed its single-manager assertion (`transitions/`). Added `PersistentNetworkRoot` to the lobby network root, ordered before NGO initialization; it deactivates/destroys only the new duplicate. The retry retains one original manager throughout all transitions. Original duel/Multi scenes and player prefabs are unchanged.
2. **Normal host-departure cleanup reported a spurious error:** a client could remove itself after the host deleted the lobby. Delete/RemovePlayer now accept only the specific LobbyNotFound response as already-completed cleanup. Other operations/errors still fail. Real Relay retry has no unexpected errors; deliberate invalid join remains an explicitly recorded expected error.
3. **Urgency during optional thinking:** an already-running think delay now ends when the bot's live temperature reaches 5 or lower. The item delay remains enforced. An 8-second thinking fixture lowered temperature to 4, then observed prompt submission, full item delay and Ready.
4. **Validation diagnostics:** full-match combat events are resubscribed for each generation; seed/configuration stamps, elapsed duration and turn watchdog are recorded. The Editor fixture retains its temporary asset references across Unity's unused-asset sweep; its initial 22-check partial run failed in the fixture's fourth setup, not in gameplay. Retained alongside the successful four-case retry. Missing diagnostic namespaces/assembly access were repaired during compilation and are not counted as passed attempts.

### Executed final evidence

| Check | Result | Evidence within T10 |
|---|---|---|
| Full Editor assembly | **260/260 passed**, no failed/skipped cases; includes actual same-time/heatwave initiative and decision boundary tests | `editor-final-suite.json` |
| Actual Solo/online transitions | **128 assertions, four processes**, one persistent manager each; real Relay duel/3P/4P, online host/client -> Solo, invalid online join -> Solo, unexpected local shutdown | `transitions-repaired/summary.json` and per-peer reports/logs |
| Natural match corpus | **6 Bo3 matches / 12 rounds / 26 turns and combats**, seeds 3609/42/1337; three replay cycles; 31-second result idle; no forced inventory, temperature, phase or winner | `replay-final/`, `seed42/`, `seed1337/` |
| Graph/round boundary fixtures | **39 assertions passed**: invalid graph/scene/config, zero-think second Prep, deadline Ready, temperature-4 urgency, 5 FPS, human Bo3 victory, simultaneous-death draw and exit during thinking | `editor-boundary.json` |
| Exit at each gameplay phase | **14 assertions passed**: actual graph item-delay pending, Attack, Resolution, RoundOver exit; one manager/no old controller or players; fresh generation acts exactly once | `editor-phase-exits.json` |
| Existing online regressions | **12 distinct cases passed**: eight extended gameplay cases plus Prep disconnect, 1v1/4P host exit and initialization failure | `online-extended/matrix.json`, `online-cleanup-final/matrix.json`, `host-exit-repaired/matrix.json` |
| Non-development guard | **4 checks passed**, exit 0: development entry absent; real compiled graph present; provisional balance rejected by normal button; usable menu without host/UGS | `release-guard/release-guard.json` |
| Builds | Final development: **0 errors / 487 warnings**. Instrumented non-development Windows Mono, stripping Disabled: **0 errors / 488 warnings** | `build-final.json`, `release-build-final.json` |
| Preservation | **956 baseline files, 20 intended changes, 0 missing**; original duel/Multi scenes, player prefab and existing unrelated assets preserved | `preservation-final.json` |

Final Editor is stopped in the original clean `SideFrontComparison` scene, script compilation is not failing, and test players have exited (`editor-state-final.json`). New asset metadata audit: no missing/orphan meta or new duplicate GUID (`meta-audit-final.json`). Targeted source/document whitespace check passed; Unity-generated scene trailing spaces were not hand-edited.

The three natural runs took about 489, 114 and 112 seconds. Reports record actual seeds, configuration IDs/versions, bounded-turn watchdog, per-generation graph traces, results and camera/canvas captures. Their cumulative 80 assertions are not 80 independent gameplay scenarios. The corpus samples three seeds; it is not exhaustive balance proof.

The transition test uses four distinct anonymous profiles (each initialized once), three real Host Relay allocations and client allocation joins 3/2/1. Actual UTP protocol is verified. Cold Solo leaves UGS uninitialized; returning from online can retain the already-authenticated SDK while Solo returns to local transport. Only the deliberately invalid join is an expected logged service error.

The extended online cases cover ghost victory, duel/rematch, duel/Multi inventory, RPC guards, mini-game target, duel mini-game and deathmatch top-up. The first 1v1 host-exit run was a **test timing failure**: the runner killed the host at its Prep marker before the client's scene probe armed. The client did return to Lobby normally. The runner now waits for every remote game-state marker; a regression test and both 1v1/4P retries pass. Original failed reports are retained, not counted as passes.

Additional fixture-only corrections: temporary assets now remain rooted across scene unload; a draw replays the **same round number** under existing 1v1 rules; Summer Vacation can shorten Prep, so the assertion checks the actual committed deadline rather than assuming 20 seconds. No gameplay rules were changed to satisfy those assertions. Failed attempts remain alongside the passing 39-check report.

Camera/canvas images are rendered captures, not physical mouse/native-window evidence. A legacy ComputeBuffer disposal warning and existing shader/Pipeline warnings remain recorded; zero build errors does not mean zero project warnings. The non-development test includes an explicit diagnostic define and verifies rejection of provisional tuning, **not a shipping-approved playable release**. Final gameplay player tests preceded only a diagnostic scope-text correction; the final development build includes that text correction and unchanged gameplay.

## T09 - Solo result and replay

**Verdict: READY_FOR_NEXT.** Evidence: `output/validation/plan036/t09_20260929/`.

- Solo retains MatchComplete after its existing result cinematic. It never enters online rematch voting. The result presenter offers replay/return, guards repeated clicks and ignores disposed UI continuations. Online duel voting and Multi results remain separate.
- **252/252 Editor tests**, build 0 errors/4 incremental warnings, and **8/8 direct-network regression scenarios** passed.
- Actual Relay duel also passed with verified Relay and matching Host/client checkpoints (`relay-duel/matrix.json`).
- `full-match/report.json`: **four natural Bo3 matches, eight rounds, three result-button replays**, 31-second result idle and final menu return passed; exit 0, unexpected errors 0, six camera/canvas renders. The human selects its existing Fan and readies late; real cooling, bot choices and combat decide results. No temperatures, inventory, phases or winners are forced. Initial gameplay RNG seed is 3609. This is automated button-event invocation, not physical mouse input.
- Each generation starts with neutral temperatures/zero wins, two logical seats/one connection/one bot controller. Prior player/manager objects are destroyed. Local cosmetics persist. UGS stays uninitialized through all matches/replays.
- The diagnostic combat-event counter only retains the initial generation subscription because TurnManager clears static events on teardown; it is not a total-combat claim across replays. Round/match counts and per-generation bot traces are independent. Final acceptance will correct this diagnostic subscription.
- Camera/canvas captures verify controls/content, but their temporary canvas projection can alter overlay compositing; they do not certify native-window pixel equivalence. Final shipping tuning and integrated mode transitions remain T10.

## T08 - Production visual behavior tree

**Verdict: READY_FOR_NEXT.** Evidence: `output/validation/plan036/t08_20260929/`.

- Saved 19 editable Unity Behavior nodes: own/public observation, legal candidates, five ordered tactical branches, bounded thinking, submit/await item operation, Ready timing/submission, bounded retry and no-item fallback. `BotTurnController` owns graph lifetime; `TurnManager` still owns phases. No opponent inventory or unrevealed selection enters observations; estimates use copied rules and never call item effects or gameplay RNG.
- Every committed Prep key runs once, including subscribe-then-query late binding. Stop/disable cancels an unqueued request; re-enable cannot restart the canceled key. A new key can run normally. The graph instance and scalar debug Blackboard are per-controller; a bounded trace records seed/key/time/reason/outcome.
- **252/252 Editor tests** pass, including 14 additional decision/cancellation assertions. Actual player: **31 checks**, exit 0, errors 0, six non-black renders (`runtime/report.json`). Production graph performs a normal first-turn attack/Ready; second-turn late catch-up, repeated notification, pending cancellation, same-key re-enable rejection and third-turn empty-inventory bounded fallback pass. Inventory clearing and direct cleanup Ready are explicitly test stimuli, not production bot behavior.
- Build succeeded, 0 errors/489 existing warnings. Fixed missing Behavior SerializableGUID assembly reference and ambiguous Editor authoring registry name during compile integration.
- Default encounter now has the production graph and all 21 catalog delay entries. **Provisional test tuning**: 0.75 seconds for ordinary items, 1.5 seconds for mini-game items, think 0.2-0.45 seconds, Ready reserve 0.25 seconds, at most two retries. These are not final balance approvals. Editor/Development button uses a named `StartDevelopmentAsync` entry that accepts Ready configurations with provisional tuning, but rejects Draft and ValidationFixture. Non-development `StartAsync` retains strict approval checks; its unchanged provisional rejection is tested. This separates functional implementation from shipping tuning approval.
- Explicit scripted slice remains off in normal launch. Original fixture entry remains separate. Final zero-think integration corpus, full Bo3/replay and shared online/release acceptance remain T09-T10.

## T07 - Dedicated scene and additive lobby entry

**Verdict: READY_FOR_NEXT.** Evidence: `output/validation/plan036/t07_20260929/`.

- Dedicated `GameScene_Solo` derives from the existing 1v1 scene, with its own GUID/build entry and no duplicate application/network root. Existing duel/Multi scenes are not repurposed.
- The additive lobby button invokes the local coordinator, disables repeated entry and restores interaction after exit. The normal encounter stays strict/draft until T08. The separate validation encounter/driver requires the explicit development flag `--solo-scripted-slice`.
- Unity automatic compilation passed after fixing a missing namespace in the development probe. Full Editor suite: **238/238 passed** (`editor-final.json`). Final capture build: 0 errors/4 incremental warnings; earlier full game build: 0 errors/489 warnings.
- Real lobby-button event -> localhost -> two participants/one connection -> one ordinary attack combat -> second Prep -> explicit exit passed at **1280x720 and 800x600**, each **20 assertions**, no unexpected errors (`camera-*/report.json`). Repeated click retains one generation/app/host; UGS stays uninitialized.
- Ten valid camera/canvas renders cover lobby, loading, Prep, combat and return. Lobby bounds and overlap checks passed at both resolutions. The first swap-chain captures were black and are invalid visual evidence (`run1280`); the retry explicitly renders the actual camera and temporarily projects live overlay canvases for the capture, restoring them afterward. This verifies rendered layout and scene content, not an OS screenshot or physical mouse input. Prep capture includes the existing loading fade; combat capture confirms dismissal.
- Full-match replay, production BT, normal strict entry and final shared online regressions remain T08-T10 gates.

## T06 - Bot delay and trusted command gate

**Verdict: READY_FOR_NEXT (2026-09-29).** Evidence: `output/validation/plan036/t06_20260929/`.

### Implementation

- `BotItemUseOperation` owns only a pending request and a bounded 64-record history. Request IDs increase within the adapter lifetime; identical payload retries preserve the outcome/due time, conflicting or evicted old IDs cannot execute again. Pending records survive history pressure. A synchronous queue callback cannot recursively complete the operation.
- `TrustedBotCommandAdapter` captures the exact registered bot binding/identity and resolved Solo settings. Item delay comes from the resolved item catalog, never a client payload or human mini-game timeout. Its production clock reads NGO server time; the clock replacement entry is Editor/Development-only.
- `PlayerState.GetBotCommands()` supplies the per-player adapter. Its Update advances pending work; despawn disposes it. Every advance/poll checks current session/binding/turn, committed input window and current item/target legality. Pending Ready/cancel/second-use and raw queue attempts are rejected. Forced Ready, input closure, death, missing/replaced copy and stale session cancel or invalidate pending work without consumption.
- At due time strictly before the Prep deadline, the shared queue resolves the physical CopyId again and validates current CanUse. Effects and consumption remain in the existing CombatResolver. Cancelling a queued selection permits a new request only with a fresh full delay; historical queued outcomes do not requeue cancelled items.
- No BT, production Solo scene, lobby button, balancing decision or new phase owner was introduced. T08 must use the adapter rather than call the lower-level T05 test seam. IDs must remain monotonic for that adapter's lifetime.

### Executed evidence

- Full Editor suite: **238/238 passed**, 0 failed/skipped (`editor-final.json`), including 19 operation cases for due-time boundary, duplicate/conflicting/evicted requests, bounded history, pending bypass, invalid/non-monotonic clock, cancellation, stale turn/disposal, reentrancy and fresh delay after cancellation.
- Standalone local NGO fixture: **234 assertions passed**, exit 0, no unexpected errors (`runtime-retry.json`, `exit-retry.json`). All 21 actually registered duel items were granted individually and checked against the resolved delay, target policy, no early consumption/effect, no bot mini-game, duplicate completion and queue cancellation. Tarot additionally rejects a not-Ready opponent.
- Controlled-clock cases cover removed/replaced copy, target/actor death, forced Ready, basic-item blocking, deadline equality and actual slot movement by compaction. These are deliberate fixture state stimuli, not natural AI decisions.
- A separate normal NGO-clock delay queues a real combat attack. Existing combat then resolves attack/recovery and consumes one use; stale queued CopyId replacement produces no effect/debit. Pending shutdown/restart reuses the same local application, produces a new generation, and old callbacks cannot modify the new inventory/selection. UGS stays uninitialized during Solo.
- Final fixture build: 0 errors/3 incremental warnings. Existing-game build: 0 errors/486 existing warnings. Editor remains stopped on the original clean comparison scene (`editor-state.json`). No warning debt is claimed fixed.
- Preservation against the 1,057-file T04 baseline: 17 intended changes, no missing files; original production scene/prefab/data/settings/NGO prefab registry preserved (`preservation.json`). New sources have Unity-generated metadata.

- Final source existing-game regressions: **8/8 direct-network cases passed**, including duel/rematch/mini-game, 3P/4P victory, client/host departure and initialization failure. Ordinary game/mini-game checkpoints match; departure/failure tests passed their own cleanup criteria, not checkpoint equality (`online-regressions/matrix.json`).
- Final actual Relay duel: **passed**, `transport=unity-relay`, `relay_verified=true`, Host/client checkpoints equal (`relay-duel/matrix.json`). This is regression evidence for existing online play; Solo remains local-only.
- Resume audit confirms source timestamps precede the tested game build, all test players have exited, and the correct Editor is connected/stopped/compiling=false/compilationFailed=false with its original scene clean (`editor-state-resume.json`).

### Repair / scope notes

The first expanded fixture incorrectly tested basic-item blocking against Water Gun, a non-basic item. That failed assertion is preserved in `runtime-final.json`; it was corrected to use Fan and the retry passed. The earlier 227-check run did not include the later pending restart/compaction refinements and is superseded by the 234-check run. The test clock changes only operation time; TurnManager still owns real phase/combat progression. Rendering is deliberately omitted in this component fixture; T04 owns presentation evidence.

T07 dedicated scene/button, T08 actual BT/late consumer binding, T09 Bo3/replay and T10 release/full integrated mode-transition acceptance remain open. Provisional fixture delays are not final approved game balance.

## T05 - Shared action and committed Prep gate

**Verdict: READY_FOR_NEXT (2026-09-29).** Evidence: `output/validation/plan036/t05_20260928/` (resumed after midnight).

- Full Editor assembly: 219/219 passed, including 15 Prep window boundary/baseline cases and four bot authority boundary cases (`editor-tests-final.json`).
- Real local NGO fixture: 56 checks, exit 0, no unexpected errors (`runtime-resume.json`, `exit-resume.json`). One human connection/two participants, actual human mini-game success/failure and replay rejection, bot role isolation, queue/cancel/Ready parity, committed snapshots across three turns, emote-tail closure and shutdown tested. Actual CombatResolver resolved two ordinary combats and rejected a same-SO replacement with a different CopyId without consumption.
- Existing game: all eight direct network cases passed; actual Relay duel passed with equal checkpoints and verified Relay (`online-regressions/matrix.json`, `relay-duel/matrix.json`).
- Builds: fixture 0 errors/489 warnings; existing game 0 errors/486 baseline warnings. Temporary fixture compilation error from NetworkList LINQ use was repaired with explicit iteration; automatic compilation then succeeded. No forced recompile.
- Shared validation remains under the human RPC guards; bot commands require the exact live Solo binding and committed Prep key. Effects/consumption stay in CombatResolver. Online duel legacy CopyId=0 remains supported.
- This fixture deliberately omits rendering and sends only the real human ACK through the existing RPC; T04 owns presentation evidence. Final dedicated Solo scene/button, graph and full match remain T07-T10. No 4P scene is used for Solo.

## T04 - Presentation gate

**Verdict: READY_FOR_NEXT (2026-09-28).** Final source review found no remaining T04 dependency-blocking defect. The Solo fixture copies the existing **1v1** scene; it does not use the Multi scene. Final dedicated Solo scene/button remain T07.

| Executed check | Result | Evidence |
|---|---|---|
| Fresh full Editor assembly after presentation repairs | 200/200 passed, no skipped/failed cases | `editor-tests-final.json` |
| Real local NGO + production player/views | 52 assertions, exit 0, unexpected errors 0 | `fixture-runtime-camera.json`, `fixture-exit-camera.json` |
| Camera rendering | 14 non-black captures; human/bot, defense, special reactions, death and re-entry. Camera stack captures do not certify final screen-space UI | `fixture-runtime-camera-screenshots/`, per-image pixel statistics in runtime report |
| Existing online game | 8/8 passed: 1v1/rematch/mini-game, 3P/4P victory, client/host departure, initialization timeout | `online-regressions/matrix.json` |
| Actual Relay duel | Passed, verified Unity Relay, matching Host/client checkpoints | `relay-duel/matrix.json` |
| Builds | Final camera fixture: 0 errors/1 incremental warning; game: 0 errors/486 existing warnings | `fixture-build-camera-retry.json`, `game-build.json` |
| Preservation | 1,057 baseline files: only 16 intended source/test/fixture changes, none missing; production scenes/prefabs/data/settings preserved | `preservation-checkpoint.json` |

Runtime checks include delayed visual binding holding Prep, authored bot hat applied to an active renderer, real-human-only ACK including rejected bot/wrong-sequence ACK, four attack/defense order combinations, fan, actual bot Hug movement, both feeding viewpoints, duel death/revive, stopping during camera motion, fresh views after restart, and missing required FPS Animator causing bounded initialization failure/rollback. UGS stays uninitialized in the Solo fixture.

The first runtime report passed the logical checks but its 14 backbuffer images were black; those images are invalid visual evidence. The camera-render retry above replaces that evidence. One build was attempted during automatic compilation and returned `Unknown`; only the subsequent successful build is counted. A final optional 300ms defense-frame capture mode was added to the fixture after the full run; that additional diagnostic mode has not been executed and is not part of the passed claims.

Evidence: `output/validation/plan036/t04_20260928/`. The interrupted write left the presentation fixture builder referencing a runtime namespace whose source was not saved. Restored the real probe, then Unity automatic compilation exited Safe Mode. CLI verified project `C:/Users/paek6/Absolute Zero`, Editor 6000.3.11f1, PID 22068. No forced script recompile was called.

- First fresh Editor suite: **200/200 passed**, 0 failed/skipped (`editor-tests-first.json`), including 10 perspective and 8 UI/cosmetic cases. Further presentation repairs require fresh validation before the task gate.
- A startup `PlayModeUserSettings.asset` load error was observed after the PC interruption; Unity regenerated a readable settings file. Current compilation is successful. This is recorded separately from game-source failures.
- Runtime fixture creation initially failed because recovery opened an untitled scene. The original clean comparison scene was reopened; partial fixture authoring is being resumed without changing production assets.
- Source review repaired stale InventoryPresenter bindings, required-view readiness, shutdown UI identity, incorrect Hug actor viewpoint, and missing local/remote feed reactions. The completed runtime and regression evidence is listed above.

## T01 - Configuration gate

**Verdict: READY_FOR_NEXT (2026-09-28).** Evidence: `output/validation/plan036/t01_20260928/`.

- Added five authoring SO types with stable configuration IDs/versions, an immutable resolved snapshot, and a strict resolver. Runtime decisions never write these SOs. Shared 1v1 rule reference and current catalog identity are preserved.
- Saved five shipping draft assets under `Assets/Data/Solo/` and five explicit fixture assets under `Assets/Tests/Plan036Behavior/Configuration/`. The draft has no production graph or approved delay values and cannot launch. Fixture numbers are provisional tests only.
- Configuration and asset tests: **61/61 passed**. Full Editor assembly: **137/137 passed**, none skipped (`all-tests-final.json`), including the existing 76 tests. Tests cover missing/invalid scene and graph references, undefined policy values, NaN/infinite/negative/zero delays, duplicate/foreign items, actual 21-item catalog reordering, disabled unapproved stat/loadout overrides, cosmetics, inheritance, source JSON preservation and defensive collection copies.
- Unity automatic compilation passed. Editor asset creation/reimport resolved the real T00 graph; an empty graph was rejected. Two independent source reviews found no remaining T01-scope defect. Original asset/source hashes remain unchanged except the two intended Behavior assembly references; new assets/scripts have metas (`final-audit.json`).
- Tool attempts: the first asset-test request used an unsupported filter type and was corrected to `testName`; one full-suite request overlapped a domain reload/port change and did not start. Its stale status was not counted. The subsequent explicit assembly run completed with the 137 results above.
- Deferred: player-side resolver/property-bag execution is included in T02's runtime fixture; actual neutral game parity remains T07/T10, final scene/production graph T07/T08, and approved balance/custom profile policy remains separate. This gate certifies configuration behavior, not a playable Solo match.

## T02 - Local and online lifecycle gate

**Verdict: READY_FOR_NEXT (2026-09-28).** Evidence: `output/validation/plan036/t02_20260928/`.

- Local application readiness no longer requests UGS authentication. Explicit online commands initialize through one shared SDK flight. An application session router owns one mode/generation and invalidates work before asynchronous shutdown; failed cleanup retains exclusive ownership for retry.
- Solo uses a temporary loopback transport, suppresses automatic player creation, rejects additional clients, restores the original online configuration after shutdown, and resolves explicit fixture configuration before starting. Production encounter data remains nonlaunchable.
- Review repairs: complete the router's stopping state before notifying awaiters; retain ownership until a pending native gameplay scene load settles (both Solo and online); await actual menu activation before releasing online ownership; guard callbacks and late service results by their captured owner/manager.
- Full Editor assembly **157/157 passed**, including 20 new lifecycle/online tests (`editor-tests-gate.json`). The standalone Windows fixture passed **23 assertions**, exit 0, no unexpected errors (`fixture-runtime-menu-await.json`, `fixture-player-menu-await.log`). It exercises cold UGS state, compiled/empty graph resolution, duplicate start/stop, actual extra UTP endpoint rejection, occupied-port rollback, native scene activation deliberately held during cancellation, online cancellation and Solo restart on the same NetworkManager.
- Actual UGS/Lobby/Relay 1v1 regression passed: Host/client checkpoints equal, both exit 0, rematch reset verified, `transport=unity-relay`, `relay_verified=true` (`relay-duel/matrix.json`). This is bounded scenario evidence, not a complete natural-match or release-backend certification.
- Existing-game build succeeded with 0 errors/488 warnings (`game-build.json`); fixture builds also succeeded. Baseline was 486 warnings, and incremental fixture counts differ; no claim that existing warning debt was cleared. The first runtime fixture failed because a dynamically added NetworkManager lacked an explicitly created NetworkConfig. The fixture was corrected and rerun; original failed report/log remain preserved.
- T02 does not claim two logical seats, a playable bot, production Solo scene/UI, final BT, visual perspective, or full repeated mode-transition coverage. Those remain assigned to T03-T10.

## T03 - Logical participants and initialization gate

**Verdict: READY_FOR_NEXT (2026-09-28).** Evidence: `output/validation/plan036/t03_20260928/`. T02 passed before T03 implementation began. No T04 implementation was started.

### Implementation

- Added an explicit participant descriptor: stable participant ID, seat, human/bot controller kind, nullable real client ID and session generation. The human is a real PlayerObject; the bot is an ordinary server-owned NetworkObject without a fabricated client connection.
- Registry pending entries use NetworkObject identity. Ready entries require matching participant metadata, seat and live object; late removal from an old binding cannot remove a new session's binding. Remote clients consume the server's participant generation. Human input/cosmetic ownership checks wait for ready identity.
- Match composition captures the current session and the actual shared 1v1 rule. Turn initialization consumes the complete ready set, waits for spawned item/match services, binds both contexts, grants through the existing 1v1 inventory code and starts one round. Duplicate notifications do not repeat grants. A partial initialization failure latches failure and cleans up instead of running a partial match.
- Match roster distinguishes a locally available bot from a connected human. Its live bot state/inventory are authoritative; connection snapshots and online vote identities remain human-only. MatchManager separately counts logical participants and real human connections.
- Persistent spawn/disconnect managers rebind to the exact current NetworkManager, NGO SceneManager and session lease. Spawn markers come from the current loaded scene; old callbacks cannot open a new scene's spawn gate.

### Executed evidence

| Check | Result | Evidence |
|---|---|---|
| Full Editor assembly | **182/182 passed**, 0 failed/skipped; includes 25 new participant/spawn cases and all earlier tests | `editor-tests-final.json` |
| Standalone participant fixture | **58 assertions passed**, exit 0, no unexpected errors; rerun after fixture asset isolation also passed all 58 | `fixture-runtime-isolated.json`, `fixture-exit-isolated.json`, `fixture-player-isolated.log` |
| D1 identity/ready handoff | Both ready before discovery; a real ready binding returned to pending; reversed actual spawn order; repeated assignment/discovery; exactly one initialization and unchanged inventory fingerprint | Same standalone report; `Plan036ParticipantProbe` |
| D2 lifecycle component | Same persistent spawner across Solo A -> online-host A -> Solo B -> Solo A; different marker positions and new NGO SceneManagers; old same-name callback invoked while new native scene activation is held cannot open the gate | Same standalone report |
| Failure atomicity | Injected failure after the first real inventory grant; 200-character Korean reason preserved locally and safely truncated for replication; no round start/retry and complete cleanup | Same standalone report |
| Existing-game direct UTP | **8/8 passed**: 3P/4P victory, duel/rematch, duel mini-game, client exit during Prep, host exit in 1v1/4P, missing participant initialization timeout | `online-regressions/matrix.json` and per-peer logs |
| Actual UGS/Lobby/Relay duel | **Passed**; `relay_verified=true`, Host/client checkpoints equal, both exit 0, rematch reset verified | `relay-duel/matrix.json` and per-peer logs |
| Final fixture / game builds | Both succeeded, **0 errors / 486 warnings** each; existing shader/Pipeline warning debt remains | `fixture-build-isolated.json`, `game-build-isolated.json` |
| Source/asset preservation | 929 baseline files: 899 unchanged, 30 intended source/test/assembly changes, 0 missing. Original scene/prefab/data/settings/packages hashes preserved; missing/orphan metas and new duplicate GUIDs: 0 | `final-preservation.json` |
| Final Editor | Stopped, original clean comparison scene, compilation not failing; production prefab list contains only the original Player prefab | `editor-state-final.json`, `console-final.json` |

The standalone fixture explicitly creates its own minimal presentation-free player prefab and uses provisional configuration. It verifies real NGO spawning and lifecycle, not production bot visuals. The online-host segment is a lifecycle component check, not a complete two-player online match on that persistent manager. Separate-process 1v1/3P/4P regression scenarios cover current remote entry/metadata/game progression. Full combined mode-transition coverage remains T07/T10. Disconnect/failure cases passed their own teardown/result assertions; their checkpoint lists are not claimed equal. The ordinary victory/duel/mini-game cases and Relay duel do have equal completed-presentation checkpoints.

### Findings repaired before completion

1. **Re-entry scene subscription gap:** the initial standalone run passed 19 checks, then showed a new scene load could start before the spawner subscribed to the new NGO SceneManager. Refreshing only in Update was too late. Spawn/disconnect managers now rebind immediately on session-router changes. Retry passed 58 checks, including the deliberately held new load and stale callback (`fixture-runtime.json` preserves the failure; `fixture-runtime-retry.json` the repaired run).
2. **Long initialization-error cleanup:** a long UTF-8 reason could exceed the replicated fixed string capacity. Preserve the full local reason, truncate only the network field, and retain failure/cleanup. The injected long-error runtime case passed.
3. **Fixture prefab entered the production registry:** NGO automatically added `ParticipantPlayer.prefab` to `Assets/DefaultNetworkPrefabs.asset`. The first hash baseline omitted Assets-root files, so a separate Git/status audit caught this. The builder now removes only this exact fixture entry through the public list API after relevant imports and before builds, deferring asset edits during Play Mode. Reimport, fixture execution and final game/Relay build were rechecked; the production registry is restored to its original Git content. No global NGO setting was changed.
4. Tool/fixture setup failures were corrected rather than counted as passes: missing fixture imports/assembly reference initially prevented compilation; a status response from that time contained stale 157-test results and was discarded. Fresh runs returned 182 results. A reimport eval first referenced an internal constant inaccessible to its separate assembly; the corrected explicit path succeeded. Original attempt reports remain alongside successful reports.

Two independent bounded source reviews found no remaining actionable T03-scope defect. These reviews did not substitute for the executed tests. Current tests do not certify T04 visuals/ACK, final Solo scene/UI, actual BT decisions, human/bot action parity, delay cancellation, full Bo3/replay, final balance, separate-PC networking or release stripping.

## T00A - Current 1v1 baseline

Verified Unity Editor project: `C:/Users/paek6/Absolute Zero`, Unity `6000.3.11f1`. CLI discovery and MCP initially agreed. Pipeline changed its port during domain reload; subsequent CLI commands explicitly selected this project. The original clean `SideFrontComparison` scene was restored after testing.

Versions: NGO `2.11.2`, UTP `2.7.2`, Test Framework `1.6.0`, Pipeline `0.7.0-exp.1`. Windows target uses Mono and the project's existing `NET_Standard_2_0` API profile. Package baseline snapshots and original tracked diff are in the evidence directory.

| Contract | Effective executable baseline | Source |
|---|---|---|
| 1v1 item registry | 21 entries; basic IDs 0-3 are Fan, Windbreaker, WarmTea, Cat | `Assets/Scenes/GameScene.unity`, ItemManager |
| Initial grant | Four basic types plus four random grant attempts, with duplicate stacking; not necessarily four distinct random slots | `ItemManager.InitializePlayerInventory`, `PlayerInventory.GrantSpecificItem` |
| Round reset | Temperature 37, fan baseline 1, restore basics/remove random items, then four random grants | `RoundLifecycleService.ResetPlayersForNewRound/GrantStartingItems` |
| Threshold grant | 30/20/10 degrees trigger 1/2/3 grants once per threshold per round | `TemperatureSystem.CheckThresholds` |
| Preparation | Default scene duration 20 seconds; environment rules can override it; first cooling tick skipped | `TurnManager.PrepPhaseRoutine`, `EnvironmentRuleService` |
| Selection | One selected queued action; existing unused free/sub-action methods stay inactive | `PlayerState.SelectItemServerRpc/ServerQueueItem` |
| Human mini-game | Server ticket, CopyId/turn/deadline checks; success queues, failure consumes one use | `PlayerState.SubmitMiniGameResultServerRpc` |
| Consumption | Combat applies/consumes normal actions; defenses are applied/consumed first | `CombatResolver.ExecuteMain/ApplyDefense` |
| Ready / initiative | Ready ends pending human mini-game, records time and stops fan; normal order uses Ready time, then temperature, then seat; heatwave prioritizes temperature | `PlayerState.PressReadyServerRpc`, `CombatResolver.DetermineOrder` |

Registry order by ID: 0 Fan, 1 Windbreaker, 2 WarmTea, 3 Cat, 4 HandFan, 5 IceCream, 6 IcedAmericano, 7 WaterGun, 8 HugTshirt, 9 HotAmericano, 10 Smartphone, 11 HotPack, 12 Mask, 13 Samgyetang, 14 Soda, 15 BuldakNoodles, 16 Screwdriver, 17 TarotCard, 18 ClawMachine, 19 BlueTape, 20 RedCard. Scene GUIDs were matched to current asset metadata.

Human mini-game flags are enabled on nine assets: WaterGun (5s), HugTshirt (10s), Smartphone (5s), HotPack (7s), BuldakNoodles (10s), Screwdriver (7s), ClawMachine (7s), BlueTape (5s), RedCard (5s). Soda/TarotCard have `RequiresMiniGame=false`. These times are human limits, **not approved bot delay values**. `OneVsOneRule.initialRandomItems=0` also does not override the currently executed four-grant 1v1 initialization.

## T00B - Implemented tooling fixture

- Installed and pinned only `com.unity.behavior@1.0.16` through the Editor Package Manager API. Existing dependency versions did not change. UPM reordered the existing Pipeline manifest entry without changing its value.
- Added [fixture runtime assembly and probe](../../Assets/Tests/Plan036Behavior/Runtime/Plan036BehaviorProbe.cs), a [custom lifecycle action](../../Assets/Tests/Plan036Behavior/Runtime/Plan036LifecycleAction.cs), and [Editor authoring/build helper](../../Assets/Editor/Plan036BehaviorFixtureBuilder.cs).
- Created the editable [authoring graph](../../Assets/Tests/Plan036Behavior/Plan036BehaviorFixture.asset) and isolated [fixture scene](../../Assets/Tests/Plan036Behavior/Plan036BehaviorFixture.unity) through Unity APIs. The graph is Start (repeat disabled) -> custom action, with the generated runtime graph and Self blackboard reference.
- The helper uses Behavior 1.0.16's explicit `InternalsVisibleTo("Assembly-CSharp-Editor")` authoring access. This is a version-pinned Editor tooling dependency, **not a public authoring API**. No reflection or package-source changes were needed. Runtime uses public BehaviorGraphAgent methods in its separate assembly.
- Fixture entry requires explicitly loading its scene. No runtime initialization hook, production scene/build-list entry, gameplay command, lobby button or new network session was added. Original Core/UI assemblies remain untouched.

Compatibility references: [Unity 6.3 Behavior package listing](https://docs.unity.com/en-us/engine/6000.3/manual/packages-list/packages-all/pack-safe/com-unity-behavior), [custom node documentation](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/create-custom-node.html), and the installed 1.0.16 package source. Actual Editor/player results below decide compatibility, not documentation alone.

## Evidence

| Check | Observed result | Evidence under the root above |
|---|---|---|
| Focused pre-change combat tests | 38/38 passed | `combat-baseline.json` |
| Full existing Editor test assembly before package | 76/76 passed, none skipped | `editmode-baseline.json` |
| Fresh existing-game build before package | Succeeded, 0 errors, 486 warnings | `build-baseline.json` |
| Bounded local UTP duel and mini-game cases before package | 2/2 scenarios passed; Host/client checkpoints agree, both processes exit 0 | `duel-baseline/matrix.json` and per-peer logs |
| Graph save/reimport and scene close/reopen | Passed initially and after compilation/build; authoring nodes, runtime types and serialized scene reference retained | `fixture-authoring.json`, `fixture-reloaded-final.json` |
| Editor Play Mode lifecycle fixture | 9 assertions passed; setup 1, start 4, completion 2, end 4, teardown 1 | `behavior-editor.json` |
| Windows fixture build after retry | Retry: 0 errors/1 warning; final external-output build: 0 errors/486 warnings | `behavior-build-retry.json`, `behavior-build-final.json`, `behavior-build.txt` |
| Windows fixture execution | Same 9 assertions passed, exit 0 in both runs | `behavior-player.json`, `behavior-player-final.json`, `behavior-player-final-exit.json`, matching logs |
| Full existing Editor test assembly after package | 76/76 passed, none skipped | `editmode-after-behavior-final.json` |
| Fresh existing-game build after package | Succeeded, 0 errors, 486 warnings | `build-game-after-behavior.json` |
| Existing-game duel after package | Passed; two Host/client checkpoints agree, rematch reset and both exits 0 | `duel-after-behavior/matrix.json` |
| Dependency/preservation audit | Only Behavior added; no existing dependency version change; original source/asset hashes preserved | `package-diff.json`, `files-before.json`, `preservation-check.json` |

The nine lifecycle assertions cover serialized graph load, a distinct runtime instance, node start/update/success, a pending second run, idempotent cancellation without late updates, no revival on re-enable, explicit restart, destruction of a running agent, and the source graph remaining unstarted. They do **not** prove future bot async item cancellation, complete blackboard immutability or gameplay decision correctness.

The duel probes deliberately stage temperature/items and force a round-ending condition. They provide bounded combat, mini-game, rematch and synchronization evidence; they are not a natural full-match simulation or proof of future neutral Solo parity. No live Relay allocation was used for these local cases.

## Failed attempts and warning accounting

1. The MCP build schema exposed array parameters as strings; the first direct call was rejected before a build ran. The existing CLI/run-script builder handled real arrays correctly. The rejected call's console entry is retained.
2. The first fixture build used eval's short internal timeout and reported a main-thread timeout. That build continued and failed in Burst with empty diagnostic stdout/stderr (2 build errors). The exact Burst cause is unconfirmed; do not claim the timeout caused it. `behavior-build-first-failed.txt` and `build-first-errors.json` preserve the failure.
3. Retried the unchanged fixture through `run_script` with `timeout_ms=180000`: build succeeded, and its executable passed. The final existing-game build also succeeded. Use the long-running builder entry for future build checks; no package downgrade or Burst-disable workaround was used.
4. The 486 existing-game warnings also occur in the pre-package baseline, primarily cached AI Inference shader warnings and Pipeline configuration warning. A fixture incremental build's smaller count does not mean those warnings were fixed. Existing injected error/timeout tests and failed tool attempts are not cleared from the console to manufacture a clean history.
5. Moved the generated fixture player out of the repository and updated the builder output path to `%TEMP%/AZPlan036BehaviorFixture/Player/`. Rebuilt and reran there successfully. Runtime fixture sources were unchanged; generated binaries are not pending project assets.

Read-only independent code review found no reproducible T00-scope defect in the fixture and confirmed that only Behavior was added. This supports, but does not replace, the executed checks.

Final Editor state is stopped, original clean comparison scene restored, and script compilation is not failing. Historical console/build-attempt records remain in the evidence. No original source, item, prefab, scene or ProjectSettings file differed from its T00A hash; only the intended manifest/lock changes differed among the captured original files.

## Integration and remaining validation ledger

These statuses supersede historical future-tense notes within earlier task sections. `PASS` means the named automated contract has evidence at the stated component/integration levels; it does not certify human visual quality or untested platforms.

| Plan assertions | Status | Executed evidence / limit |
|---|---|---|
| V01, V02 | PASS - development integration | T00 authoring/reload/build; T01 configuration; T07 dedicated scene; T08 real 19-node graph; T10 player and missing graph/scene rejection |
| V03-V06 | PASS | T03 reordered identity/spawn/cleanup; T05 authority; T10 real two-seat/one-connection and online 2/3/4P transitions |
| V07, V22-V23, L08 | PASS - rendered evidence | T04 both defense directions/initiative, ordinary server-owned bot fan/death, recovery/special presentation and real-human-only ACK; T07/T08/T09 production scene captures |
| V08, L01 | **PARTIAL - physical offline environment** | Cold actual button/BT/full matches start with uninitialized UGS and no second client. Internet-unavailable execution was not performed; it remains required before claiming full offline acceptance |
| V09-V10, L02-L04,L06 | PASS | T02 occupied port/double entry/load cancellation/stale async callbacks; T07 double button and restoration; T10 missing graph/scene and unexpected shutdown/failed online entry recovery |
| V11-V13 | PASS | T05 real human mini-game success/failure and shared queue/Ready; T06 234 runtime assertions across all 21 items, CopyId, deadlines, cancellation, pending stop/restart; unused free-action APIs remain inactive |
| V14-V15 | PASS at tested graph/lifecycle boundaries | T08 late bind, duplicate Prep, rejected/empty candidates, bounded fallback, graph disable/pending cancellation; T04 presentation stop/restart; T06 pending operation stop; T10 thinking exit |
| V16-V17 | PASS - neutral profile | T00 effective baseline; configuration/source preservation; T05 shared actions; T09/T10 natural rounds/replays and neutral reset; T10 actual initiative ordering tests |
| V18 | EXCLUDED - unapproved custom profiles | Non-neutral starting fan/temperature/loadout and replace/add/reset semantics remain disabled until defined and approved |
| V19-V21 | PASS | T08 decision legality, hidden-input/RNG/Blackboard isolation; T10 zero-think second turn, near-deadline Ready, low-temperature interrupt, 5 FPS and same-time initiative |
| V24 | PASS | T10 six natural Bo3s, injected human two-win and draw-to-same-round fixtures; T09/T10 replay/results/idle |
| V25 | PASS at recorded lifecycle boundaries | T02 pending native scene-load stop; T04 presentation; T06 item pending; T08 disabled graph; T09/T10 result/replay and T10 thinking stop. T10 final 14-check phase sweep adds pending real-graph item, Attack, Resolution, RoundOver and a fresh unaffected generation |
| V26, L05,L10 | PASS - same-PC real peers | T10 direct UTP gameplay/disconnect cases plus real Relay 2/3/4P and persistent manager Host/client -> Solo. Separate-PC adverse networks remain outside this run |
| V27, L07 | PASS - development; shipping gate open | Real lobby button and explicit Editor context use dedicated Solo. Repeated Solo/online transitions pass. Non-development provisional tuning rejection is intentional; release playable acceptance waits for final approved data |
| V28 | PASS - bounded corpus | Three seeds, six natural matches, timing/config stamps/watchdog/traces and rendered result captures |
| L09 | PASS - layout/callback | T07 1280x720 and 800x600: button bounds/overlap, callback and render checks. Physical mouse/manual feel not certified |

Stable carry-forward IDs:

| ID | Current status | Closing action |
|---|---|---|
| V036-01 | DEVELOPMENT_PASS; RELEASE_DATA_PENDING | Review final item/think/Ready delays and difficulty values; then mark only approved assets non-provisional and rerun normal non-development entry |
| V036-02 | PASS | T06 operation and T08 production BT/late binding/cancellation evidence recorded; do not reopen without a relevant change/failure |
| V036-03 | **PARTIAL - REQUIRED_OFFLINE_QA** | On an Internet-disconnected PC, launch from a fresh profile -> lobby button -> playable Solo combat -> return; retain log showing no UGS initialization. Do not equate uninitialized UGS with a disconnected physical network |
| V036-04 | PASS - bounded corpus | T10 six natural matches/three replays and win/draw fixtures; extended run adds three mixed-item Bo3s/six rounds/93 combats and 12 entry/cleanup cycles. Two initial 300-second timeouts passed same-seed longer-budget retries; match duration remains a manual balance judgment |
| V036-05 | PASS - tested network matrix | Actual Relay 2/3/4P and persistent host/client transitions; extended run adds real Relay 4P, four winner seats under local 80 ms/1% loss and duel/mini-game. No separate-PC adverse-network certification |
| V036-06 | MONITOR | Initial fixture Burst failure did not recur in later builds; collect diagnostics if it returns. Existing build/ComputeBuffer warnings remain separate debt |
| V036-07 | PARTIAL - release gate | Instrumented non-development Mono/stripping Disabled guard passed. Validate playable approved release data and the actual chosen backend/stripping; IL2CPP not tested |
| V036-08 | MANUAL_QA | Physical mouse at reduced/default resolution; attack/defense/mini-game/camera continuity; difficulty/penalty feel; extended soak. Automated camera/canvas renders are supporting evidence |

A remaining required offline check prevents marking the entire feature fully accepted. T00-T09 completed their implementation gates; T10 automated checks and final manual/release gates are distinguished explicitly. Obsidian uses these exact Docs files as the current ledger.

## Reproduction and rollback

- Editor creation is explicit and refuses to overwrite existing fixture assets: `Plan036BehaviorFixtureBuilder.Create()`. Re-check serialization with `ValidateSavedFixture()` while the fixture scene is closed.
- Open the fixture scene and enter Play Mode; report: `behavior-editor.json`. Stop Play Mode afterward.
- Build using a long-running Pipeline `run_script` entry that calls `Plan036BehaviorFixtureBuilder.BuildPlayer()`; the recorded wrapper is `build_fixture.cs` under the evidence root. Run the resulting executable with `--plan036-result <absolute report path>` and inspect its exit code plus fresh report/log. Player binaries are kept outside the repository at `%TEMP%/AZPlan036BehaviorFixture/Player/`; the initial generated build was moved there, with its original evidence retained in `build-output-relocation.json`.
- To roll back this task, remove only the new fixture/helper assets and their metadata through the Editor, remove `com.unity.behavior` through Package Manager, then inspect manifest/lock differences against the saved baseline. Preserve pre-existing files/dependencies and never reset the whole working tree.

## Obsidian usage

`Docs` is the registered Obsidian vault. These Markdown files are the originals; there is no parallel Obsidian copy to synchronize.

| Document | Role |
|---|---|
| `Plans/PLAN_036_solo_detailed_implementation.md` | Task scope, dependency order and exit contracts |
| `Validation/PLAN_036_detailed_plan_review.md` | Historical planning review and corrections |
| This file | Actual task states, run evidence, failures and deferred validation |
| `ACTIVE_CONTEXT.md` | Short handoff and next task, linking to this file |

At this task's read-only check, Obsidian was not running and its local MCP port was not listening; no active Obsidian MCP success is claimed. Files remain editable directly and visible when that vault is opened. Use Obsidian links/search/backlinks to navigate requirements -> tasks -> evidence, not as a game execution or Unity test engine. No plugin/authentication settings were changed.
