# PLAN 034 — Validation repair plan

Status: extended validation in progress, 2026-09-27. V034-12 captured visibility is closed after correcting the prior image review. V034-13 package cleanup and repeat-play resource evidence remain open. See the canonical [repair results](../Validation/PLAN_034_results.md).

## Evidence and scope

Source: [current validation ledger](../Validation/PLAN_034_results.md), especially V034-11 through V034-13 and the fresh 2026-09-27 recheck. Baseline: 57/57 EditMode tests; 15/17 process scenarios passed; local and Relay host-loss recovery failed. Full Relay state progression passed, but one final screenshot did not show result text.

Follow the approved coordinator/gateway/scene-adapter ownership in `AI_TARGET_ARCHITECTURE.md` and PLAN_018. Those documents' historical two-player baseline does not supersede current Multi implementation.

Preserve all approved behavior: Grudge -3 degrees, T/T+1/T+2 availability, Possession once per match and consume-only for its turn, one ghost damage per target per turn, empty-slot top-up, action-level fifth-kill settlement, and existing 1v1 rules. No host migration, reconnect feature, Multi rematch, new ghost art, side-sprite work, item rebalance, scene repositioning, dependency upgrade or firewall change.

## Findings and decision map

| Finding | Confidence / priority | Owner | Planned task |
|---|---|---|---|
| V034-11 / R034-06: clients remain in GameScene_Multi after host loss on local and real Relay | Confirmed / high | NetworkSessionCoordinator; legacy SessionManager compatibility | T2 |
| V034-12 / R034-07: seat 2 final screenshot is black despite Visible log | Observed image discrepancy; root cause unresolved / medium | RoundResultPresenter, visual probe, capture runner | T1, T3 |
| R034-01: generic Multi action calls the local owner's unbound remote Animator | Confirmed redundant call / low | CombatVFXManager, AZPlayerVisual, FPSVisualController | T4 |
| R034-02: scene synchronization overlay references are empty; separate loading UI exists | Confirmed ownership ambiguity / low | SceneLoadSyncManager, LoadingScreenManager | T5 |
| R034-04: accelerated report overstates synchronization; local report can say relay_verified=true | Confirmed evidence defect / medium | Python runners and probes | T1 |
| R034-03/05: 485 inference shader warnings, one Pipeline runtime-config warning, shutdown ComputeBuffer warning | Confirmed warnings; runtime impact/allocator not established | Installed packages and build/runtime resource owners | T6 |

Central visual overlap is a polish candidate, not a confirmed rule violation. Record it for later art/layout review instead of changing transforms during this repair.

## Execution order and gates

Each task records changed files, focused test evidence, failures and disposition in the existing validation ledger. Fix failures within that task before claiming its gate passed. Independent warning investigation may proceed while a runtime issue remains open, but no overall acceptance is issued until required gates pass. No invented readiness score substitutes for these gates.

### T1 — Make test evidence reliable

- Keep process/authority checks, checkpoint equality, transport verification and rendered UI verification as separate report fields. A process PASS must not imply all four screens passed.
- Compare normalized checkpoint signatures in accelerated full-match mode too. For local transport, report Relay verification as not applicable; require actual allocation, joins and distinct authenticated identities for Relay.
- Preserve logical seat mapping before despawn. The host-loss client capture was named `seat0.failure.png` after identity reset; use cached seat plus process role/run ID to prevent mislabelling or overwriting.
- Add terminal diagnostics scoped to the current result sequence: result text, active hierarchy, text alpha, CanvasGroup alpha, canvas sorting, button activity/interactability, scene and frame number. Avoid exposing production state mutation through diagnostics.
- Capture multiple distinct rendered frames after result completion, within the existing auto-leave window. Validate PNG existence and content, not merely a log marker or file count. Do not lengthen production result timing just for the test.
- Tests: replay saved reports with deliberately mismatched checkpoints/missing captures; local transport cannot pass the Relay-specific field; duplicate/old sequence cannot satisfy current-result readiness.
- Gate: old black capture remains inconclusive/failed visual evidence; mismatched states cannot produce synchronized=true. No gameplay source behavior changes in this task.

### T2 — Repair unexpected host-loss recovery

Files: `Core/Session/NetworkSessionCoordinator.cs`, existing `ISceneTransitionService` / `SceneTransitionService`, with narrow changes to `Core/Network/SessionManager.cs` only if duplicate legacy ownership requires it.

1. Make the coordinator own one idempotent teardown operation for explicit leave and unexpected stop. Reuse existing adapters and state machine; do not add a second session manager or event bus.
2. Capture the old lobby ID, role, connection/operation identity and origin scene before clearing state. Invalidate pending operations and close new session admission while teardown owns the session.
3. Handle repeated OnClientStopped, explicit Leave, transport failure and scene unload without duplicate cleanup or scene loads. Pair subscriptions with the exact subscribed NetworkManager instance and cleanup lifetime.
4. Preserve LoadingGame entry-operation ownership: its cancellation/compensation path handles failed entry. Joining/loading failures, ordinary remote-player disconnect, and an intentional host shutdown must not be mistaken for unrelated host loss.
5. Clean local network/approval callbacks, roster/session references, lobby polling/heartbeat, compatibility state and loading UI. Return to LobbyScene once using the existing scene adapter. A slow/failed UGS cleanup must not indefinitely hold the local scene; use bounded cleanup and observe late exceptions.
6. Scope remote cleanup to the captured old lobby. A delayed old operation must never clear or shut down a new lobby/match. Publish Ready/Failed coherently after local teardown/transition; expose existing failure feedback if transition itself fails.
7. Direct local test entry must establish an explicit test session context or a supported local recovery contract. Do not broadly convert every stopped editor/local network into a production lobby transition just to make the fixture green.

Tests: duplicate stop/leave, already-disconnecting, pending entry, stale completion after new join, cleanup exception/timeout and single scene transition using existing adapter seams. Real Relay: Host + three clients, stop Host in prep, combat and terminal presentation; each survivor returns to lobby, no active loading overlay, then joins/starts a fresh match successfully. Repeat shared-path 1v1 host-loss/normal leave/rematch checks; verify ordinary client departure does not eject remaining peers.

Gate V034-11: host-loss detection is logged, local recovery completes within a measured bounded deadline after detection, no three-client timeout, no duplicate scene load, and fresh join works. Measure transport detection separately from teardown latency; do not reduce transport timeouts to hide missing recovery.

### T3 — Resolve the result-screen discrepancy

- Run the T1 diagnostics on every seat, including local winner, living loser and ghost loser, after a real fifth kill. Preserve the server result and ACK gate.
- If capture timing is at fault, repair only the fixture and verify successive frames show the intended text and lobby control.
- If UI state is at fault, trace RoundResultPresenter coroutine cancellation, sequence handling, fade state, canvas ordering/occlusion and scene lifecycle. Correct the specific owner; do not globally raise every canvas or skip cinematics.
- Gate V034-12: four views visibly show the correct winner/loser result and active lobby control, retained result appears once per sequence, late subscription works, automatic/manual leave still works, and 1v1 rematch UI remains correct. Logs alone cannot close the gate.

### T4 — Separate local and remote animation calls

- Generic Multi local actor uses FPSVisualController; remote actor uses its bound AZPlayerVisual Animator. Audit duration lookup as well as trigger routing; avoid hiding legitimate missing-remote-Animator warnings.
- Preserve special Cat/Hug/Feed sequences, effect/impact timing, shared action duration and simultaneous counter-defense. No new sprites, rigs or animation clips.
- Test Warm Tea/generic use from each seat, remote observation on all peers, defense-counter and one special sequence; retain 1v1 regression.
- Gate: no missing-Animator warnings caused by intentional local FPS ownership; no duplicate/missing remote action or changed combat schedule.

### T5 — Clarify loading-overlay ownership

- Trace normal 1v1 and Multi lobby entry, late loading, entry failure, timeout and host loss. LoadingScreenManager is the current active loading UI; SceneLoadSyncManager still owns load synchronization.
- Treat its absent legacy overlay as optional only after proving the active owner covers these paths. Preserve synchronization/counters/RPCs. If an actual entry path needs legacy UI, wire that exact path through verified Editor operations rather than create duplicate overlays.
- Gate: one loading screen, no blocked invisible raycast panel, all tested success/failure exits dismiss it. Suppressing a warning alone is insufficient.

### T6 — Diagnose package and resource warnings

- Group the current BuildReport by package/path/category and inspect project use of inference models and Pipeline player runtime. Distinguish optional editor tooling from required runtime functionality.
- Capture allocation stacks/profiler evidence for ComputeBuffer, compare ordinary exit and repeated scene/match reuse, identify allocator and disposal owner before changing code.
- Correct project-owned leaks if proven. For package-owned defects, document installed version, reproducer and impact; propose a separate dependency change if required. Do not edit package caches, disable required features or blanket-filter warnings.
- Gate: each warning has an owner, impact assessment and correction or explicit deferred rationale. Zero warnings is not achieved by hiding messages. Unresolved repeatable resource growth remains a release blocker; an unproven shutdown warning is not labelled a gameplay leak.

### T7 — Final regression and handoff

- After relevant fixes, run the 57-test baseline plus focused new lifecycle/report tests, current development build and console classification.
- Run 1v1 rematch/minigame/defense, three-player specials, four-player inventory/RPC/round transition/client departure, both grant application orders, and 120ms/2% impairment proportional to changed owners.
- Actual Relay: four distinct profiles, ordinary turns, Possession consumption/suppression, Grudge cooldown and five real kills across rounds, synchronized checkpoints, four-view result captures, host loss and successful fresh join.
- Preserve raw failures and corrected reruns separately. Update V034-11/12/13 and ACTIVE_CONTEXT; do not rewrite prior test results as if they had passed.
- Final acceptance: V034-11 and V034-12 closed, no new rule/1v1 regressions, project-owned warning issues resolved, package/resource limitations explicitly assessed. Human visual acceptance and independent-machine/network testing remain separately stated limits.

## Plan review

The plan uses the existing session state machine, coordinator, gateway adapters, presenter boundaries and operation-generation protection. Shared cleanup is justified; a new DI framework or broad architecture migration is not. The highest risks are duplicate teardown, stale async completion affecting a new match, and mistaking a screenshot for rendered-state proof. T1/T2/T3 gates specifically cover these risks.

No new gameplay decision is required to implement this scope. If investigation demands changed result timing, host migration/reconnect semantics, combat rules, scene layout or dependency changes, stop that dependent change and request the user's decision with the concrete proposed difference.

## Execution disposition — 2026-09-27

- T1: implemented and report-semantic checks passed; state checks and visual acceptance are explicitly separate, including gallery labels.
- T2: implemented; real Relay prep/combat/terminal host-loss, 1v1 host loss, same-process fresh Relay re-entry, local 4P recovery, and seven focused lifecycle tests passed. Entry compensation captures old lobby IDs and does not mutate a superseding operation.
- T3: OPEN. All four final texts are now captured, but some seat1/seat2 frames omit lobby controls despite active and unculled UI diagnostics. End-of-frame texture capture and a dedicated result Canvas did not resolve it; the unproven Canvas/sorting change was removed. No result timing/game rule change was retained.
- T4: implemented local/remote routing for generic Multi/1v1 and Multi defense/Cat. Verified tested Multi sequences and final generic 1v1 have no missing-Animator warnings. This does not establish every legacy 1v1 special animation path.
- T5: implemented current Multi loading coverage and coordinator cleanup; synchronization ownership unchanged.
- T6: project animation/legacy-overlay warnings addressed. Pipeline runtime config and unused-field warnings classified; ComputeBuffer shutdown allocator/growth remain unproven. Dependency changes remain separate scope.
- T7: 64 focused EditMode tests and nine selected process regressions passed; real Relay state progression and recovery passed. Four-view visual acceptance is still blocked, independent-machine/network and prolonged resource profiling remain deferred.

Next bounded investigation: retain a result window for interactive observation; compare an OS-window capture with the end-of-frame capture and inspect Button/Image mesh, material, clip state and raycast results on the affected client. Correct the proven rendering/capture owner, then repeat four-view result and 1v1 rematch checks. Do not approve solely from `[MatchResult] Visible` or `active=True`.


## Follow-up disposition — 2026-09-27

- T3 captured-result gate now passes. The later seat1/seat2 missing-button finding was an image-review error, corrected by historical pixel audit and fresh local/real Relay four-view evidence. See the current results ledger; no additional layout workaround is warranted.
- T1 additionally rejects empty matrices and nonzero proxy errors. The Windows impairment proxy now explicitly configures its socket, with closed-endpoint recovery regression coverage.
- T6 remains incomplete: opt-in native allocation diagnostics reproduce the shutdown ComputeBuffer warning without an allocator stack. Do not implement speculative package cleanup or treat warning absence/cache changes as a fix.
- T7 fresh focused tests: 64/64, final build errors 0, real Relay winner/checkpoint and result capture gate passed. Remaining interaction/soak coverage is recorded in the results ledger.


## Extended validation run — 2026-09-27

Authorized request: execute remaining necessary debugging and tests. Evidence root: `output/validation/plan034/extended_20260927/`.

- [x] Broaden player-process edge cases: all ghost winner seats, joint/delayed terminal, minigame actor/target invalidation, idle disconnect, initialization failure, RPC guards, multi-round and repeated 1v1 rematches.
- [x] Reproduce shutdown ComputeBuffer warning in isolated lobby-only players; compare explicit test-only fallback disposal, and record allocator size/identity. Never install reflection cleanup in production.
- [x] Record Unity memory, managed memory and object/material/texture counts during repeat matches and an extended four-player Relay session. Separate warm-up/reservation from live growth; this bounded run is not an hours-long soak.
- [ ] Use actual mouse input for item selection/cancel/Ready after autonomous player fixtures finish; do not interfere with the user's open Editor scene.
- [x] Repeat ACK-loss/latency and delayed inventory-view checks where required; update the single current results ledger with failures as well as passes.
- [x] Confirm final build/console and scope of previously passed EditMode tests after diagnostic-only changes.

A three-line package-owned shutdown cleanup proposal is saved under the evidence root. Applying it would require embedding the unchanged 2D Animation 13.0.5 package for a persistent patch; the approval question is pending. Other verification continues independently.


### Additional repairs from execution

- Fixed particle pool ownership: prevent authored auto-destruction of pooled instances, explicitly dispose copied runtime materials, and return active instances during forced settlement. Three consecutive 1v1 rematches no longer accumulate orphan HitEffect/IceBreak materials (24 before, zero after). Imported assets remain unchanged.
- Corrected obsolete joint-win fixture: it no longer calls Possession as if it were removed Chill Aura. Synthetic simultaneous deaths exercise the shared terminal mask/presentation path; final four-player run passed.
- Fixed evidence filenames across rounds and generic capture validation for death-only presentations. The first extended Relay run matched all 20 checkpoints but failed its capture gate; retain that failure. A corrected actual-player rerun supplies the replacement evidence.
- 66 focused EditMode checks and seven Python harness checks passed. Twenty-five-second uplink hold and real Relay forced settlement passed. Twelve-second hold is retained as insufficient injection, not gameplay failure.
- Native mouse selection/cancel/Ready lock passed. Escape delivery, independent machines and multi-hour soak remain explicit ledger items V034-14/15. The package-owned shutdown fix remains pending the user's decision; no dependency edits were made.

Final repeat: `ordinary20_fixed` passed actual Relay, 20 matching presentations (17 combat, three death) across two rounds, 164 unique captures. Native pointer coverage is partial; deferred IDs V034-13/14/15 remain open as described in the canonical results ledger.
