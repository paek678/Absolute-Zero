# PLAN 036 - Detailed-plan verification

Date: 2026-09-28
Review target: [detailed implementation plan](../Plans/PLAN_036_solo_detailed_implementation.md).
Baseline: working tree at HEAD `f6104bb`, including existing uncommitted changes. Original reviewed plan SHA-256: `15CC124EEBE125608315922332EA3E34F2F97E1CB40BE621778A709334E946FD`.
Revalidated revision R1 SHA-256: `989E3495019810D125E8934D81485E5388D6C4E1F5D17EEA91AF4744E055E88F`.
Task-readiness revision R2 SHA-256: `9CB94A2E8E61F0AFB67DBCA2A12834F17BB679870D54FECCE43AB64DE811D136`.
Scope: static source/plan review plus two independent focused read-only reviews (session lifecycle and action/BT timing). No implementation, package installation, scene change, Unity compilation, Play Mode or player-build test.

Historical planning record: implementation began afterward. Current task states and actual test results are tracked separately in [PLAN_036_results](PLAN_036_results.md); TODO statements below describe the review-time baseline.

## Verdict

**Revision R2 passes plan-level task-readiness revalidation.** R1's D1-D3 and server-side result-flow corrections remain intact. R2 separates stage exit assertions from later integration acceptance, removes task-gate dependency loops and specifies the first implementation package. Two independent focused re-reviews found no remaining concrete contradiction in the changed task contracts. No gameplay decision was changed.

This review does not certify a working Solo mode or a bug-free implementation. All T00-T10 work and the planned runtime cases remain TODO. The user's requests authorized the R1 and R2 plan corrections and revalidation recorded below. Begin implementation with T00A then T00B when authorized, followed by T01-T10 using their stage-specific gates.

## R2 task-entry corrections and final revalidation

The final task-order review found execution-planning gaps, not newly observed runtime failures. These are now closed at the planning level:

| Item | Previous gap | R2 correction / result |
|---|---|---|
| E1 - Gate dependency loops | Early tasks cited complete V scenarios requiring later profile, scene, graph or replay work | Pass: section 8 gives each task independently executable exit assertions. V/L coverage accumulates as PARTIAL until named integration owners complete it; early fixture results cannot certify the feature |
| E2 - Encounter references | T01 required a valid encounter before the final scene/graph existed | Pass: T01 tests real fixture references and keeps the future encounter nonlaunchable; T07 uses an explicit Editor/development validation driver; T08 binds the production graph and passes the strict validator before normal launch |
| E3 - First package and scope | Initial deliverables and optional V18 requirements could imply unavailable prerequisites | Pass: T00A records current 1v1 evidence; T00B proves isolated Behavior compatibility, serialization, cancellation and player build. Unapproved custom-profile policies and shipping delay values remain separate outstanding decisions |

Final evidence:

- Re-read task dependencies against the current package/assembly setup, existing test organization and preserved R1 contracts. No behavior package installation or Solo implementation is inferred from a plan edit.
- Independent action/BT re-review accepted stage gates, fixture/draft progression, immediate shared-path regressions and the first T00A -> T00B package. Independent lifecycle re-review accepted dependency ordering, integration ownership and deferred optional scope. Neither identified a remaining concrete contradiction within this review boundary.
- Structural document check: exactly 11 task rows (T00-T10), every dependency points to an existing earlier task, all 28 V scenarios and 10 L scenarios appear in the integration ledger, 12 local Markdown links resolve, fenced blocks balance and neither edited document has trailing whitespace.
- Focused shared-path regressions occur when T02-T05 modify their respective boundaries; T10 retains the full shipped-path and online acceptance audit. Moving an integration assertion later does not waive an earlier component assertion.
- Only the detailed implementation plan and this review record were edited in this task-readiness pass. Gameplay code, scenes, prefabs, packages and runtime results were unchanged by this pass.

**First implementation handoff:** T00A must produce a timestamped baseline with current dirty scope, versions, effective item/grant/Ready/consumption behavior and relevant existing-test evidence. T00B then validates the minimal graph/custom-node fixture and records the exact package/build results. Stop this first package before production lobby, player identity or TurnManager behavior edits. Unavailable required evidence remains unresolved; it cannot be replaced by this review's verdict.

Each subsequent task follows implement -> focused validation -> repair -> affected revalidation -> READY_FOR_NEXT. Results belong in `PLAN_036_results.md` when implementation begins, including deferred assertions and their owners. Once the user authorizes end-to-end implementation, a fresh approval is not required between passing tasks within that scope.

## R1 corrections and revalidation

All passes in this section mean **planning-contract closure**, not execution of the future tests.

| Item | R1 plan location / owner | Revalidation result |
|---|---|---|
| D1 | Section 3.3 Ready-binding handoff; T03; V03/V05/V16 refinement | Pass: seat promotion is independent of discovery, match binding reads validated ready participants, missing seats block initialization, and initial grants/StartRound have a once-only owner |
| D2 | Section 3.2 persistent PlayerSpawnManager contract; T03/T04/T07; V25/L05/L10 refinement | Pass: old/new actual scene-manager instances and generations are tracked, stale cleanup is rejected, and fresh markers are required before spawning |
| D3 | Section 6 Committed Prep opening; T05 producer -> T08 consumer; V14/V19/V21 refinement | Pass: initialized server snapshot precedes the bot event, late subscription queries current state, turn keys deduplicate, and closure invalidates the same input source |
| Solo result flow | Section 7 server routing; T09; V24/V25 refinement | Pass: Solo exits the server result branch before vote creation, keeps MatchComplete, disables vote-driven UI return, and tests at least 30 seconds idle before replay |

Revalidation evidence:

- Re-read PlayerRegistry promotion, TurnManager discovery/Prep/result paths and persistent PlayerSpawnManager registration; checked installed NGO SceneManager creation/disposal against the specified lifecycle.
- Independent action/BT review accepted D1/D3 and identified one API-name typo. Corrected `PublishPhaseChangedClientRpc` to the actual wrapper `PublishPhaseChanged` and called method `OnPhaseChangedClientRpc`, verified at TurnManager lines 168 and 1942. No runtime API was added or changed.
- Independent lifecycle review accepted D2 and the server-side Solo result routing, including repeated-session position checks and the result idle-time gate.
- Documentation checks: local Markdown link targets present, fenced blocks balanced, no trailing whitespace; all 11 T00-T10 tasks retained, all 10 L01-L10 entry cases retained, four targeted regression refinements added. Declared task references point to earlier tasks; T05 provides the snapshot before T08 consumes it.
- Edits are limited to the detailed plan and this review record. No gameplay source, prefab, scene, package, balance value or runtime test result was changed.

Remaining planned work: exact package compatibility in T00, all implementation/runtime gates, final shipping item delays and optional non-neutral starting-profile semantics. These remain explicitly open; this review does not replace their evidence.

## Original findings and source evidence (closed in R1 at plan level)

Plan line numbers in the historical findings below refer to the pre-R1 document. Use the section/owner mapping above for the current correction locations. Source findings remain evidence of required future code changes, not claims that those changes have been implemented.

### D1 - High: promoted bindings must populate TurnManager independently of the pending queue

**Plan:** section 3.3, especially line 123, promotes identities before the existing match initialization. T03 calls for discovery changes but does not specify the source from which the TurnManager player array is then populated.

**Evidence:** [TurnManager.cs](../../Assets/Scripts/Core/Turn/TurnManager.cs), lines 371-400, constructs a list from `registry.EnumeratePending()` and populates `_players` and turn contexts only from that list. [PlayerRegistry.cs](../../Assets/Scripts/Core/Player/Identity/PlayerRegistry.cs), `PromoteToReady`, removes each binding from pending. TurnManager lines 437-445 skip missing players in non-Multi rather than treating that case as an initialization failure.

**Trigger:** Solo composition correctly assigns/promotes both seats first, then reuses the old pending-only initialization loop. Registry reports ready participants, but the loop sees no pending bindings and does not populate the match player array.

**Impact:** incomplete turn context/inventory initialization or progression with missing player references. This is a concrete integration trap, not evidence that the future implementation necessarily contains it; T03 already intends discovery changes but must make this contract explicit.

**Minimum refinement:**

1. Separate seat assignment/promotion from match binding. Build `_players` from a validated snapshot of registered ready bindings by the roster's expected seats, regardless of whether this frame performed promotion.
2. Require exactly the configured valid unique seats, live objects and matching participant generation before initial inventory grants or StartRound. Fail initialization on a missing Solo seat, rather than inheriting the non-Multi null skip.
3. Bind turn context and grant initial inventory exactly once per match initialization. Repeated ready notifications cannot duplicate grants.

**Gate:** extend V03/V05/V16 at T03 with all-ready-before-discovery, mixed pending/ready ordering, reversed spawn order, missing seat and duplicate notification fixtures. Expected: two non-null authoritative players, two valid contexts, one initialization/grant per seat.

### D2 - Medium: PlayerSpawnManager needs the same SceneManager rebind contract as the loading screen

**Plan:** line 106 explicitly fixes LoadingScreenManager re-subscription after restart. T03 and general cleanup do not name the persistent PlayerSpawnManager equivalent.

**Evidence:** [PlayerSpawnManager.cs](../../Assets/Scripts/Core/Network/PlayerSpawnManager.cs), lines 55-56 and 83-87, uses `sceneCallbacksSubscribed` to skip registration. Reset/unsubscription is tied to OnDestroy (90-121), while the component persists with DontDestroyOnLoad. Spawn marker refresh occurs at initial Start (47) and OnSceneLoadComplete (217). Installed NGO `Runtime/Core/NetworkManager.cs` creates a new NetworkSceneManager at line 1261 and disposes/clears it at lines 1655-1657 on shutdown.

**Trigger:** finish Solo and enter an online game using the same persistent spawn manager but a new NGO scene manager.

**Impact:** Update may still spawn players, so this is not proof of a guaranteed hang. However, the missing new-scene event can leave stale/destroyed spawn-marker references and send players to fallback positions (`GetSpawnPositionBySeat`, lines 275-280).

**Minimum refinement:** track the exact subscribed NetworkManager/NetworkSceneManager instances, detach from old instances, reset registration flags at shutdown and bind the new instances. Refresh scene markers after the new scene loads and before allowing automatic spawn. Clear session-specific pending/spawn records through the owning cleanup, without destroying unrelated editor scene objects.

**Gate:** extend L05/L10/V25 at T03/T04 with Solo -> online duel -> Solo and online three/four-player entry. Assert actual spawn coordinates correspond to the new scene markers, exactly one callback is active, and destroyed prior-scene references are absent.

### D3 - Medium: first BT observation needs an initialized Prep snapshot

**Plan:** lines 231/241/243 start one graph run on Prep notification and include remaining preparation time in observations, but do not define a committed opening snapshot.

**Evidence:** [TurnManager.cs](../../Assets/Scripts/Core/Turn/TurnManager.cs), lines 562-568, sets start time/duration and then CurrentPhase/phase notification. ResetTimer and RemainingTime assignment happen afterward at 573-577. The previous Prep ends with RemainingTime zero (701). `PublishPhaseChanged` (168-169) invokes an RPC whose body (1941-1944) does not provide an existing server-side ready-snapshot subscription API.

**Trigger:** a zero-think BT starts synchronously on phase value change, captures RemainingTime before the rest of initialization, and observes zero seconds from the previous turn.

**Impact:** false no-time fallback or immediate no-item Ready. This is conditional: deferring evaluation until a later frame may hide it, but frame scheduling is not a sufficient data contract.

**Minimum refinement:** define a server Prep-ready event/snapshot after turn ID, timer state, input-open flag and initial observation values are ready. Alternatively define observation time from the freshly assigned PrepStartServerTime + PrepDuration and explicitly gate the other initialization fields. Do not assume a currently available server event merely from the RPC method name. Catch-up subscription for a graph bound during an already-open Prep must read the same snapshot and start only once.

**Gate:** extend V14/V19/V21 at T08 with zero think delay, second-turn opening, late graph subscription and repeated notifications. The initial snapshot must have the current key and correct positive remaining time; each turn starts at most one run.

## Existing requirement to trace explicitly, not a new defect

Solo result/replay without a vote is already specified in the plan's section 7 and T09. When mapping T09 edits, include [TurnManager.HandleRoundEnd](../../Assets/Scripts/Core/Turn/TurnManager.cs) lines 1399-1417 and `WaitForRematchDecision` at 1730. The current non-Multi server path enters a vote after the result; changing only result buttons would leave the server vote and timeout running.

For Solo, retain MatchComplete until explicit replay/exit and bypass the online vote coroutine. Keep the online path intact. Extend V24 with an idle result screen beyond the existing 15-second vote timeout; assert no Solo RematchVote/RematchDeclined transition or automatic lobby return, then replay successfully. This verifies an already approved requirement, not a new game rule proposal.

## Contracts already adequately covered

| Area | Review result |
|---|---|
| One-button offline entry | Local readiness, lazy online services, no room or second process are explicit |
| Transport and lifecycle | Loopback, command-line override protection, session generation, duplicate-start and cleanup owner are explicit |
| Participant authority | Separate seat/connection/ownership; host-owned bot rejected by human RPC adapters; no fabricated ClientId |
| Perspective | Deferred role-ready initialization, FPS/opponent split, cosmetics, ordinary NetworkObject visual lookup and real-viewer ACKs are specified |
| Item replacement | Bot mini-game removed; delay queues current single action; existing combat owns consumption/effects; unused free/sub paths remain inactive |
| Pending operations | CopyId, duplicate/stale requests, cancel/reselect delay, forced Ready, expiry and late callbacks are covered |
| AI fairness | Allowed observation, separate AI RNG, immutable configuration and no live-state mutation for prediction are specified |
| Validation scope | T00-T10, V01-V28 and L01-L10 distinguish fixtures from shipped entry flow and offline tests from online regressions |

## Review exit checklist

- [x] Re-read detailed plan against current source rather than previous summaries.
- [x] Independently review lifecycle and item/BT contracts.
- [x] Verify installed NGO scene-manager recreation evidence.
- [x] Separate uncovered details from already documented risks; no duplicate finding for general pending-item or vote behavior.
- [x] Incorporate D1 into T03 implementation contract and its future acceptance tests.
- [x] Incorporate D2 into T03/T04 lifecycle and repeated-session test specifications.
- [x] Incorporate D3 into T05 snapshot production and T08 graph-start observation contracts.
- [x] Include server vote bypass call sites in T09 and specify the result-screen idle-time test.
- [x] Separate current-stage task exits from complete V/L scenario acceptance and assign every integration owner.
- [x] Remove T01 scene/graph readiness dependency through strict fixture/draft/development/production boundaries.
- [x] Define the independently executable T00A -> T00B first package and preserve optional-profile/tuning exclusions.
- [x] Re-review R2 independently for action/BT and lifecycle task readiness; validate all 11 task dependencies and 38 scenario references.
- [ ] Execute implementation/runtime gates only after code exists and implementation is authorized.

No additional user gameplay decision was required to resolve D1-D3 or E1-E3. Final shipping delay values and optional non-neutral starting-profile semantics remain the previously recorded balance decisions. Revision R2 closes the recorded logic and task-order planning gaps for neutral-baseline implementation; full-feature acceptance still requires the mapped runtime evidence.
