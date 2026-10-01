# PLAN 034 — Inventory transaction and presentation architecture

Status: implemented on 2026-09-25; current-build local and actual Relay grant/input logic gates passed, including deliberately delayed grant-view adoption. Exact evidence and remaining visual acceptance are in [the validation report](../Validation/PLAN_034_results.md). This document preserves the design and acceptance contract.

This addendum refines WP4/WP4-A of [PLAN 034](PLAN_034_multi_ghost_skills_and_deathmatch_refresh.md). It does not mark that package accepted. Existing test results remain in [the validation report](../Validation/PLAN_034_results.md).

## 1. Scope and decisions

Use the latest user decisions: Multi grants missing random capacity up to four when exactly two eligible survivors remain; preserve occupied copies, remaining uses, basic items, selection, Ready and valid minigame attempts. Grant once per round, exclude Tarot, and preserve the existing duplicate limit. A latched five-kill winner prevents a new grant. Ghost skills and 1v1 rules stay governed by PLAN 034.

The architectural target is `AI_TARGET_ARCHITECTURE.md`, with incremental migration per PLAN 018. Current executable code has Multi support; older two-player baseline statements in those documents are historical.

This scope includes server grant failure handling, Multi inventory presentation, and the command identity needed to make that presentation safe. It does not introduce another authoritative inventory, phase driver, DI framework, global event bus, or package upgrade. Scene layout, assets, sprite work and item balance are outside scope.

## 2. Evidence and current gaps

| Finding | Current evidence | Consequence | Planned correction |
|---|---|---|---|
| Failed grant can resume ordinary interaction | `TurnManager.TryGrantDeathmatchItems` publishes Failed and returns; `MatchNetworkState.IsDeathmatchGrantViewBlocked` releases a matching Failed descriptor. Production retry is dependent on another trigger; the matrix invokes a debug retry explicitly. | A recoverable rollback can be followed by play without the required top-up. This is a static failure-path finding, not a reproduced live service outage. | One owner defines bounded recovery and terminal technical failure; Failed never means Ready. |
| A transient committed inventory can be skipped by clients | Multi attack loop calls the grant between actions; a later action can consume or steal an item before network publication. Current client gate requires an exact historical fingerprint until its first resolution. | A client that only receives the later state can wait for a state that no longer exists. The prior post-grant test waited one second and does not cover this case. | Publish a self-contained final view snapshot carrying the last successful grant watermark, not a requirement to observe every intermediate inventory. |
| Gate may start after raw inventory changes | Gate uses separately replicated life state or grant metadata; presenters otherwise consume independent NetworkLists. | A changed list can be rendered before the client knows a grant is pending. Existing packet delay/loss tests do not inject this ordering. | Multi views consume one atomic presentation envelope; raw list changes no longer drive those views. |
| Responsibilities are mixed | `MatchNetworkState` polls the registry, compares inventories, changes resolution state inside a property getter and routes timeouts. `TurnManager` contains planning, commit, rollback and publication. | Harder isolated tests and lifecycle reasoning. | Match-scoped coordinator, small network bridge and side-effect-free read model. |
| Display selection identity is weaker than inventory identity | `InventoryPresenter.ResolvePendingSlotIndex` / confirmed-item paths search ItemId; `SelectItemServerRpc` accepts a slot without expected CopyId. | Duplicate items or slot compaction can make a click refer to a different copy, especially during view migration. | Preserve CopyId from displayed item through target confirmation and server admission. |

The current implementation does have valuable safeguards: server ownership, staged before/after plans, synchronous two-recipient commit, restore-only-if-applied, per-round grant flag, bounded diagnostics and dirty-view coalescing. Preserve these rather than rewriting item rules.

## 3. Alternatives considered

| Option | Benefits | Cost or weakness in this project | Decision |
|---|---|---|---|
| Keep fingerprint polling and add more guards | Smallest diff | Cannot prove receipt of a transient state; separate object arrival can bypass the initial gate; more mixed responsibilities | Reject as the final protocol |
| Publish Pending, wait for all-client ACK, then commit | Clients can close views before writes | Adds asynchronous wait points to Prep ticks, attack actions, ghost RPCs and disconnect callbacks; requires timer/minigame-deadline compensation; couples gameplay progress to slow clients | Reject for this task |
| Dedicated synchronous grant coordinator plus one retained Multi inventory view snapshot | Preserves immediate authoritative mutation, removes cross-object joins from inventory UI, supports late subscribers and skipped intermediate states | Requires migrating all Multi inventory reads and protecting displayed-copy identity; duplicates bounded presentation data on the wire | Selected, subject to serialization and consumer-coverage tests |
| Event sourcing / full inventory-store replacement / general Saga framework | Broad replay and workflow features | Changes ownership and many unrelated item paths; unnecessary for at most four players and twelve slots each | Reject |

Selected patterns are **Application Service/Coordinator**, **staged transaction with compensation**, **explicit state machine**, **read model**, **scoped Observer**, **Adapter**, and existing **Composition Root**. The read model is a small command/read separation; it is not a new writable inventory or an event-sourced system. One failure policy suffices; a Strategy hierarchy is unnecessary unless genuinely different policies emerge.

## 4. Ownership and proposed seams

These ownership seams were implemented within the existing match composition and turn ownership.

| Owner | Responsibility |
|---|---|
| `TurnManager` | Detect eligible transition, invoke coordinator, inspect typed outcome and decide whether its existing phase routine may continue. It remains the only phase driver. |
| `DeathmatchGrantCoordinator` | One active transaction; eligibility/context capture, staging, revalidation, synchronous commit, bounded recovery, grant success watermark and typed fault result. No scene lookup, UI construction or RPC serialization. |
| `PlayerInventory` / existing roster inventory adapter | Authoritative slots, CopyId allocation, exact captured-value checks and guarded mutation/restoration. Existing rule-aware ItemManager supplies legal candidates. |
| `MultiInventoryViewPublisher` | Subscribe to server inventory/roster changes; coalesce dirty state outside active mutations; derive immutable view data from current authoritative state. Own no gameplay decisions. |
| `MatchNetworkState` | Hold/serialize retained network snapshot and transport typed status/requests. Map pure values at the boundary. Remove inventory traversal, getter side effects and view timeout policy after cutover. |
| `MultiInventoryReadModel` / `IReadOnlyInventoryView` | Validate and atomically adopt a complete snapshot, expose cached reads and a scoped Changed event. Never mutate NetworkLists. |
| Presenters and command adapter | Render one adopted revision; carry displayed CopyId/context with commands. Server validates actual current state. |
| `MatchCompositionRoot` | Retain the existing match scope and error reporting. `TurnManager` owns the coordinator lifecycle; `MatchNetworkState` owns the publisher and read model lifecycle. No new singleton chain. |

Introduce interfaces only at actual test, authority or presentation boundaries. Keep small state transitions in ordinary C# values; if extracting pure planner data, map to an `InventorySlotValue` without NGO dependencies rather than calling the current INetworkSerializable slot type pure Domain code.

## 5. Server transaction and failure contract

Identity is `(matchEpoch, roundEpoch, transactionId)` with nonzero monotonic IDs and explicit overflow failure. Use roster eligibility, then resolve that exact set of seats to inventories; do not independently count via roster and collect via all spawned Alive objects. A binding unavailable during disconnect processing must settle through the existing roster path before proceeding; it cannot silently omit a survivor.

State flow:

`Idle -> Prepared -> Committing -> Committed`

`Committing -> Restoring -> Prepared (one bounded retry) -> Committed or Faulted`

`Preparation/revalidation failure -> Superseded or Faulted`

- Prepare both complete addition plans before writes. Stage exact values and destination slots; capture full before-state, selected CopyId/ticket identity, round, roster and winner context. Basic items, uses and threshold flags are not reset.
- Commit both recipients synchronously in the existing server action. Hold the coordinator guard across both writes, rollback and any immediate retry. Never yield or wait for clients between recipients. Synchronous list callbacks only mark views dirty and cannot start another grant or gameplay mutation.
- Revalidate the same context immediately before the first write and before continuing after callbacks. Both zero-addition plans are successful.
- Retain the exact prepared additions for at most one immediate retry only after verified exact restoration and unchanged context. Do not reroll, allocate different copies or restore an untouched stale recipient. If the original plan is invalid, do not force a retry.
- Invalid configuration/pool, missing required service, changed state during partial application, second write failure after retry or restoration failure produces a typed Faulted outcome. Finish safe compensation where possible, keep gameplay admission closed and use the existing session-error presentation. Do not continue underfilled or loop indefinitely.
- Before any write, a genuine terminal winner or no-longer-eligible transition may supersede the request; return a typed Superseded result to the existing turn/death owner. Do not grant after a winner or introduce a second round-ending path.
- Mark the round grant complete only on successful commit. Snapshot publication does not control success. A technical fault is not a new gameplay outcome or new winner.
- Route selection, cancellation, Ready, minigame completion, ghost commands and bot/server entrypoints through the same authoritative admission predicate where applicable. A client-only disabled button is insufficient.
- `FailInitialization` currently records/displays an error; do not assume that call stops coroutines. Every production trigger must propagate Faulted and exit before further effects, phase advancement or grant retries. A single idempotent fault latch rejects later gameplay intents while leaving leave/disconnect cleanup usable.

Production trigger audit: `OnSeatDisconnectedForceGhost`, Prep periodic death/threshold handling, scheduled effects in Attack, each Multi action, and `UseGhostSkillRpc`. Development force-ghost helpers must call the same owner. Already accepted ghost damage/kill credit remains immediate and is never rolled back by an inventory fault.

## 6. Snapshot contract and client behavior

Use one retained `NetworkVariable<MultiInventoryViewSnapshotNetData>` on the existing match network object. It carries a complete bounded inventory view for all configured Multi seats plus grant status in the same serialized value. Keeping status and arrays in separate NetworkVariables would recreate the join problem.

Envelope fields: match/round epochs; monotonic view revision; last committed grant transaction; typed fault status; seat presence mask; stable seat identities; per-seat inventory revisions/counts; slot values `(ItemId, RemainingUses, Flags, CopyId)`. Represent absence separately from an empty inventory. Include only inventory information already exposed to all players; no extra hidden intents or minigame outcomes.

- Maximum payload content is four seats times twelve slots; raw slot fields currently total eight bytes each (384 bytes for slots alone). Measure the complete serialized message and allocations on installed NGO; this estimate is not a measured packet size or an MTU guarantee.
- Use bounded value storage and validated counts, deterministic equality and explicit serialization. Do not rely on mutable array aliases, fingerprints for exact equality, or undocumented serializer behavior. Parse/validate the whole envelope before adopting it; reject malformed, duplicate, older or wrong-session messages.
- Publisher listens to NetworkList and relevant roster/transaction changes, marks dirty and captures a stable view only outside a mutation guard. Flush once at the selected server frame/network-update boundary; verify that boundary in NGO 2.11.2. No per-frame registry hashing when nothing changed.
- A grant followed by consumption in the same frame publishes the later inventory plus the successful grant watermark. The client must not wait to see the consumed grant state. Existing immutable combat presentation records still supply the appropriate action visuals.
- Raw NetworkLists remain authoritative gameplay replication. **All Multi inventory presentation and targeting reads** must move to the read model together: item creation, remaining-use labels, availability, target confirmation, pending/confirmed copy resolution and public presenter accessors. A partially migrated reader is a release blocker.
- Apply the whole read-model revision first, then notify. Schedule local and remote view refreshes for the same presentation pass; respect existing combat/icebox locks and retain the newest pending revision. No presenter may rebuild from a newer raw list while another uses the envelope. Old coherent views may remain visible during network delay.
- A displayed click carries expected CopyId and match/round context through `GameUIManager`, the command adapter and server admission. Resolve that exact surviving copy on the server or reject with a typed stale-view result; never substitute another copy with the same ItemId. Recheck phase, owner, target and uses. Do not reject solely because an unrelated seat's global view revision advanced.
- Ready validation checks the actor's relevant view/selection version, not unrelated inventory churn. A stale rejection preserves the existing selection and requests a fresh view; it does not silently Ready the player later. Ghost requests retain their own existing ledger/context validation.
- Initial binding reads the current retained snapshot and subscribes to Changed; do not require a fresh OnValueChanged event to initialize a late subscriber. Session/round epochs reject stale snapshots independently of arrival order.
- Treat this envelope as one context stream within the bound network-object spawn/session. Do not wait for separately delivered GhostRoundEpoch or life-state variables to adopt its inventory revision. Validate monotonic epochs inside this stream; server command admission still checks the authoritative current context.
- If an expected initial/resync snapshot is absent, make one rate-limited current-state request; allow at most one pending request per client and cap server responses. After a bounded total wait (initial target: ten seconds), use the explicit local session-failure flow. Do not declare a fault merely because no inventory changes occurred for ten seconds.
- New match, despawn and scene exit clear read-model revision, transaction state, queued rebuilds, subscriptions and pending resync. A new round resets round data but preserves the separately owned match-long possession charge.

This removes the need for the old client cross-object fingerprint gate after all consumers have migrated. Keep 1v1 on its existing adapter path. Any signature bridge for legacy/1v1 callers must not create a Multi path that bypasses CopyId validation; audit bot and development callers too.

## 7. Implementation packages and gates

| Step | Changes | Required check before proceeding |
|---|---|---|
| P0 — failure containment | Typed outcomes and fail-closed propagation at every grant trigger; production recovery policy, exact same-plan bounded retry | Inject missing pool/service, first/second write failure and failed restoration. No later action, Ready acceptance or phase advance after Faulted; no changed random draw on retry. |
| P1 — extract ownership | Coordinator and injected adapters; TurnManager remains phase owner; extract pure slot/transaction values where useful | Existing 3/1, 4/0, zero-addition, stale recipient and rollback tests pass; server results unchanged. |
| P2 — retained envelope | Implement bounded serializer, server publisher and read model in validation/shadow mode | Serialization/deep-copy/count/epoch tests, late binding and same-frame grant+consume. Compare final envelopes to server slots. Old and new publishers must not both own gameplay. |
| P3 — identity and UI cutover | Migrate Multi presenters/accessors/target flow and commands atomically; remove old polling gate | Consumer search finds no unintended Multi raw-list view reads; duplicate ItemId with different CopyIds, compaction, stale click, target confirmation, Ready and active minigame pass. |
| P4 — fault and ordering validation | Scripted envelope/list order injection plus four-process runs | See matrix below; scene exit/rematch dispose once; 1v1 regressions pass. |
| P5 — Relay and presentation acceptance | Current build, actual Relay Host + three distinct clients; capture all views and logs | No mixed inventory revision, underfilled successful grant, duplicate cost, timeout or misbound click; required artifact evidence recorded. |

Files in scope: `TurnManager`, `PlayerInventory`, `ItemManager`, `MatchNetworkState`, `MatchCompositionRoot`, new scoped coordinator/publisher/read-model value files, `InventoryPresenter`, `MultiPerspectiveLayout`, `GameUIManager`, `ILocalPlayerCommands`, `LocalPlayerCommandAdapter`, `PlayerState`, relevant bot/debug entrypoints and existing matrix/tests. New Unity source files require normal GUID/meta generation. Preserve unrelated dirty assets.

## 8. Required validation matrix

| Case | Acceptance assertion |
|---|---|
| 4→2 and 3→2; counts 3/1, 4/0, 4/4 | Fill missing capacity once; preserve all preexisting values and bindings. No 3→1 or terminal grant. |
| Missing pool/service; failure before write, between recipients, during restore | Typed failure; one allowed same-plan recovery only; no underfilled normal continuation or permanent waiting. |
| Grant + consume/steal/threshold change before one network send | Latest envelope is renderable and carries successful grant identity; no historical fingerprint wait. |
| Envelope before raw lists; lists before envelope; arbitrary list delays | Old or new coherent inventory views only; zero dependence on cross-object event order. |
| Pending/committed versions skipped; newer snapshot before older/duplicate | Newest valid epoch/revision remains adopted; no regression or repeated grant. |
| Stale displayed CopyId, duplicate ItemId, compaction and cancellation | Correct physical copy selected or rejected without cost; no substitution. |
| Already Ready, selected item and active valid minigame during top-up | Ready/order, CopyId, ticket and deadline preserved; no new synchronization pause or timer compensation introduced. |
| Ghost accepted fifth kill / grouped deaths | Winner latches immediately; skip top-up and later gameplay. |
| Recipient/observer disconnect, replacement binding, round/rematch/scene exit | Existing roster policy governs; no missing seat silently ignored, old epoch adopted or leaked callback. |
| Packet delay/loss plus explicit message-order simulation | Both tested and labeled separately; packet loss proxy alone is not reorder coverage. |
| 1v1 regression | Initial distribution, attack/defense, minigame, Ready, round reset and cancellation retain behavior. |

Do not score production correctness from pattern names or the old 49/49 tests. This implementation has separate serializer, EditMode, multi-process, and network evidence in the validation report.

## 9. Static suitability review

Pass at proposal level: the selected design retains synchronous authoritative transactions, supports NGO retained state, uses existing match composition and observer seams, avoids client ACK delays in phase/timer logic, and keeps one authoritative inventory. The added projection is bounded and is necessary to remove the cross-object UI join without changing immediate gameplay ordering.

Review corrections incorporated: (1) Failed is not an unlocked successful state; (2) retry retains exact additions rather than rerolling; (3) same-frame post-grant changes are represented by a watermark plus latest snapshot; (4) all Multi consumers migrate together; (5) displayed CopyId reaches server validation; (6) cached read properties have no state-transition side effects; (7) error display alone is not assumed to stop gameplay; (8) match/round/reset and initial subscription are explicit.

The selected patterns were applied without adding an authoritative store, asynchronous gameplay wait, or changing item/skill rules. Final acceptance is evidence-based and remains separate from implementation completion; see the validation report for the exact gates passed and open.

Inspected API baseline: installed NGO 2.11.2 `NetworkList.cs` queues dirty events and invokes list callbacks; `NetworkVariableDeltaMessage.cs` addresses a NetworkObject and behaviour. Neither provides a project-wide multi-inventory transaction guarantee. Existing `CombatResolutionBatchNetData` demonstrates project-local custom serialization, but the proposed retained envelope still needs its own tests. Existing `PresentationBarrier` is for visual ACKs; do not reuse its active sequence/state for inventory transactions.

Official [NetworkVariables documentation](https://docs.unity.cn/Packages/com.unity.netcode.gameobjects%402.13/manual/basics/networkvariable.html) was consulted for retained-state and initial-binding/OnValueChanged patterns. That page describes 2.13; executable API choices must compile against the installed 2.11.2 source. A package upgrade is not part of this plan.
