# PLAN 034 — Multi ghost skills and two-survivor top-up

## Status and authority

Implementation and validation in progress as of 2026-09-25. The WP4 transaction and Multi inventory input/view logic passed its current-build local and actual Relay gate. WP5 visible four-peer Relay checks passed for casts, lethal Grudge impact→death→result order, possession item categories and timing, ghost state lifetime, presentation recovery, and minigame newer-ticket replacement in the current Multi flow. Local 1v1/three-player regressions passed; full-match acceptance remains. The confirmed rules below remain the contract; unchecked items outside the tested scope are not accepted as complete. See [PLAN_034 results](../Validation/PLAN_034_results.md) for executed checks and remaining gates. This plan supersedes the earlier inventory replacement and non-consuming possession proposals.

Latest user decisions govern over older plans and Sheet wording. Keep click targeting, immediate ice break and the square ghost placeholder. Replace Frost Strike/Chill Aura with the Sheet's ghost skill types. Preserve 1v1 behavior; side sprite work remains paused.

Sources inspected: GhostSkillService, TurnManager, CombatResolver, MultiCombatApplicator, PlayerInventory, PlayerState, ghost presenters, and PLAN 031/032/033. Sheet sources read on 2026-09-24:

- [Four-player design](https://docs.google.com/spreadsheets/d/1kqMeBDvIThoJ7hVKTqn0WI6V_9wLEjHnRHLRf42dK0s/edit?gid=14199085#gid=14199085).
- [Items and skills](https://docs.google.com/spreadsheets/d/1kqMeBDvIThoJ7hVKTqn0WI6V_9wLEjHnRHLRf42dK0s/edit?gid=618047176#gid=618047176).

### Current work-package position (2026-09-25)

This table reflects the implemented code and recorded validation, not the unchecked original task boxes below. The current carry-forward checks are [V034-01 through V034-09](../Validation/PLAN_034_results.md#deferred-validation-ledger--current-queue); executed evidence is in the same report. Package acceptance is narrower than the mere presence of code.

| Package | Current position | Remaining gate |
|---|---|---|
| WP1 — state lifetime | Implemented; focused tests and real Relay next-turn, next-round, revival and re-death checks passed (V034-03). | A future in-scene Multi rematch needs its own validation if introduced (V034-10); current Multi returns to lobby. |
| WP2 — authoritative ghost commands | Implemented; local lethal checks and real Relay casts, both same-target request orders, T/T+1/T+2 cooldown and alternate ghost/target seats passed (V034-03). Presentation ACK loss/delay and VFX disconnect passed for the current Multi flow (V034-04). | Future in-scene rematch, if introduced, requires V034-10. |
| WP3 / WP3-A — consume-only possession and minigame identity | Resolver, suppression event, ticket identity and invalidation paths implemented; focused EditMode and real Relay item-category checks passed (V034-01). Same-slot replacement, stale/duplicate results, top-up preservation and round-boundary ticket replacement passed locally and on Relay (V034-05). | A future in-scene Multi rematch needs V034-10 if introduced. |
| WP4 / WP4-A — two-survivor top-up | Grant transaction, recovery, coherent Multi inventory view and stale Select/Cancel/Ready guards implemented. Current-build local and actual Relay logic gate passed, including 3/1, 4/0, selected/Ready/minigame and both client application-order injections (V034-08). | Arbitrary transport packet reordering was not injected; the reverse case delays client descriptor consumption while the full view is adopted. |
| WP5 / WP5-A — HUD and ordered visuals | Scheduling and suppression routing are implemented; four-peer Relay casts, lethal Grudge impact→death→result order, item categories, suppression/counter timing, ghost state lifetime, presentation recovery and newer minigame UI replacement passed for the current Multi flow (V034-01 through V034-05). | Scripted four-view Relay end-to-end visual review passed V034-07 with five actual skill kills; free-form player art/readability review remains separate. |
| WP6 — end-to-end acceptance | Focused tests, V034-05 Relay ticket scenarios, V034-06 local visible Duel/three-player regressions, and a V034-07 four-view Relay flow with 17 synchronized checkpoints and five actual Grudge kills are recorded. | The fixture staged ghost conversion and low target temperatures. Unassisted human play was not exercised. |

The implementation sequence's design-document update is not fully closed: `Docs/GAME_DESIGN.md` still lists Frost Strike/Chill Aura as the implemented ghost skills. The confirmed Grudge/Possession rules in this plan and the current code take precedence for this work; reconcile the older canonical text before final handoff without changing the agreed rules.

## Confirmed behavior

| Rule | Contract |
|---|---|
| Two survivors | Preserve existing items and fill only vacant random-item capacity, up to four random items. Three existing items receive one; four receive none. Preserve basic items and remaining uses. |
| 귀신의 한 | Apply temperature damage immediately on server acceptance; lethal damage awards ghost kill credit. Allow reuse after a one-turn cooldown. |
| 빙의 | One successful use per player per match, never replenished at a round boundary or on becoming a ghost again. For the affected turn, the target's item uses pay their normal consumption cost but produce no item effects, regardless of item type. |
| Ghost damage limit | Each living target can receive ghost damage once per turn across all ghosts. |
| Victory | Check five kills after each authoritative action/effect group, including ghost damage. Latch winners immediately; simultaneous winners within one group share victory. |
| Existing choices | Preserve click/aim/click targeting, cancellation rules, immediate freeze/break transition and separate ghost-skill controls. |

### Confirmed lifetime and balance baseline

- Damage baseline: 3 degrees, using the Sheet's `3(?)` value. Timing and kill behavior are confirmed; the numeric balance uncertainty remains disclosed.
- Cooldown timeline: use in T, skip T+1, reusable T+2. Display the next usable turn explicitly.
- Possession lifetime is confirmed: one successful use per player for the entire match. Preserve the spent flag through revival, subsequent death, and round reset. Initialize one charge only for a genuinely new match/rematch; never grant on each ghost spawn. This is match state, not a permanent account unlock or account-wide consumable.
- Both Sheet skills have 100% success for a valid server-accepted use; do not add an accuracy roll. Invalid requests remain rejected without spending a use.
- Preserve Prep-only use and existing ghost-skill defense bypass. Possession may affect an unselected or already-Ready living target without changing Ready order. Reject duplicate possession that turn without charge loss.
- Possession causes no damage and does not spend the ghost-damage allowance. It does not undo previously committed persistent/delayed effects. Infinite-use items retain their ordinary infinite-use bookkeeping.

## Source findings

1. At planning time, ItemManager.GrantDeathmatchItems used FillRandomSlotsWithSeparateCopies, matching the confirmed top-up rule. Implementation now prepares both addition plans before commit; replacement and wholesale inventory identity migration remain unnecessary.
2. At planning time, TurnManager.TryGrantDeathmatchItems marked completion before validating the manager/success. Implementation moved completion after both plans apply; the remaining client-coherence gate is tracked in the results report.
3. CombatResolver's ActionNeutralized skips item consumption. **Do not reuse it for possession.** Preserve Red Card's existing behavior.
4. Defense resolves in a prepass. Consume a possessed defense item once there and suppress its effect; do not consume it again in the main loop.
5. The Multi main loop skips EventCount == 0 results. Consume-only resolution needs an explicit suppressed-use event so its inventory delta reaches the applicator.
6. Minigame failure already consumes an item. Do not charge again. Success queues the ordinary action, which resolves as consume-only while possessed.
7. Ghost hit VFX and death have independent sequencing. Correlate impact, temperature display, death and settlement while keeping server damage/winner decisions immediate.
8. Remove old Chill modifiers, reapplication and periodic kill attribution together; preserve unrelated fan effects.

## Implementation sequence

### 1. Record decisions and scope

Update canonical design/decision documents with top-up, consumption, match-long possession charge and explicit cooldown examples. Preserve historical validation evidence. Keep the external Sheet read-only unless separately requested.

### 2. Server-owned state and request admission

Extend GhostSkillService and MatchNetworkState; do not create another turn driver. Maintain stable actor/target seats, next usable turn, possession charge/marker and per-target damage allowance.

Requests carry match epoch, round epoch, turn and request ID; derive sender from RPC metadata. Match epochs distinguish same-connection rematches. Validate Multi, ghost actor, connected living target, input-open state, context, cooldown/charge and terminal state before mutation. CurrentPhase alone is insufficient during Prep closure.

Commit allowance, temperature, cooldown/charge and death/winner result without yielding. Retried requests never mutate twice: retain bounded correlated results and reject evicted old request IDs. Old replies cannot clear newer UI requests. Rejections spend nothing. Different targets remain eligible while earlier nonterminal visuals are pending.

Initialize the ghost UI from existing authoritative availability and gate input on its snapshot; becoming a ghost does not replenish possession. Reset turn markers before opening inputs. Keep the possession spent flag in match-lifetime state keyed by the existing participant/seat identity. Separate new-match initialization from round reset even if both currently call BootstrapNewMatch: clear transient markers at round reset, but preserve the spent flag. A genuinely new match/rematch initializes one charge. Disconnects and reconnects of the same participant within that match cannot refund accepted uses or target damage allowance.

### 3. Consume-only possession

Add distinct item-effect-suppression state to the combat snapshot. Preserve other neutralization sources and their precedence. Do not revive an otherwise cancelled/invalid action just to consume an item.

- Keep normal selection, targeting, minigame participation and Ready available. Possession alone does not cancel them.
- Valid ordinary use: apply its normal remaining-use cost once, emit a suppressed-use event, and omit damage, healing, defense, buffs, sabotage, stealing, rerolls and newly scheduled item effects.
- Defense prepass: use the same consume-only rule once; retain its event in the aggregate without replaying or charging it again.
- Minigame failure: existing failure cost once. Success: queue normally, then consume-only on resolution. Pre-use cancellation remains ordinary cancellation.
- No selected item means no invented item loss. Unlimited/basic items retain ordinary consumption semantics. Do not add costs or cooldown policies.
- Suppression feedback must not play successful item-effect VFX or introduce a separate three-second defense action; follow the existing timing contract.
- Clear possession next turn. Preserve previously committed fan/delayed effects and Ready timestamps.

### 4. Harden existing top-up

Preserve Multi drop filtering, Tarot exclusion, duplicate limits, basic items, occupied slots and remaining uses. Do not compact/replace occupied slots, cancel valid minigames/selections or reopen Ready because of top-up.

Preflight missing capacity and legal candidate additions for both survivors before publishing a grant. Commit additions without yielding, then mark completion after both inventories are validly filled, including zero missing slots. Prevent callbacks from observing half-applied authoritative state. Preserve prior state on recoverable failure; use bounded diagnostics rather than per-frame rerolls. Never retry in a way that duplicates an already successful recipient grant.

Use existing inventory replication and defer/coalesce UI rebuilds during committed animation. Client notification order is not atomic. Preserve threshold flags and enforce capacity when threshold rewards and top-up occur together.

Grant once per round at exactly two eligible survivors, including 3→2. No grant after winner latching or when a grouped death leaves one/zero survivors. Slot-only stale-minigame result protection is mandatory in WP3; it does not require replacing the existing inventory architecture.

### 5. Ordered visuals and feedback

Use the existing CombatVFXManager presentation owner for an ordered stream of ghost, combat and death records. GhostSkillVFXPresenter renders and reports completion. Include match epoch, presentation ID, actor/target, skill, before/after temperature and associated deaths.

Present cast → impact → freeze/break → settlement. NV arrival must not independently start death before impact. Latch winners immediately; display results after the deciding presentation. Reject subsequent gameplay. Nonlethal ghost casts add no item interval and do not pause the authoritative Prep timer. Close input at its ordinary boundary and drain accepted records before subsequent combat/results.

ACKs validate epoch, presentation ID and participant. Timeout settlement cleans only the requested prefix, preserving newer accepted records. Reset/despawn cancels the old stream and temporary objects. Bound queues/caches and cleanup paths. PLAN 031 timing/forced-settlement fixes remain a prerequisite to full visual acceptance, not assumed complete from older Relay evidence.

Replace old labels, tuning, payload routing and fixtures together. Show target damage immunity, next usable turn, possession marker/charge and correlated rejection feedback. Preserve placeholder art and explicitly mode-gate changes.

## Detailed implementation work packages

The checkboxes below retain the original detailed acceptance contract and have not been maintained item by item. Use the current work-package table above and the validation ledger for actual progress; an unchecked box alone does not mean its code is absent. Implement in dependency order and complete each package's focused checks before integrating the next package. No new dependencies, scene rearrangement, character art, reconnect feature, or general architecture migration is in scope.

### WP1 — State lifetime and protocol foundation

Files: `Assets/Scripts/Core/Match/GhostSkillService.cs`, `MatchNetworkState.cs`, `GhostCooldownNetData.cs`, and `Assets/Scripts/Core/Turn/TurnManager.cs`.

- [ ] Introduce explicit Grudge/Possession skill IDs and immutable tuning (Grudge damage 3, usable-turn offset 2). Update serialization consumers together; all test peers must run the same build.
- [ ] Add server-owned match epoch and per-seat possession availability (one initial charge). Keep it outside the round-reset cooldown list. Add round/turn-scoped target damage and possession state under the same existing network owner.
- [ ] Use an explicit Grudge available-turn value with its round context instead of a decrement that accidentally permits T+1 use. Preserve existing round cooldown cleanup; a new round starts a new cooldown context while the possession spent flag survives.
- [ ] Separate new-match initialization, round cleanup, and turn cleanup. `BootstrapNewMatch(clearTerminalResult)` currently serves both rematch and `StartNextRound`; audit every caller and pass an explicit lifecycle reason rather than infer possession reset from ghost spawn or cooldown clear.
- [ ] New match/rematch: initialize possession once and clear request history under a new epoch. Next round: preserve spent flags, reset transient markers/cooldowns. Next turn: expire possession and target damage allowance before opening input.
- [ ] Subscription setup must read current state as well as future changes; pair NetworkList/event subscription with despawn cleanup and disposal.

Gate: lifecycle tests cover use → next turn → next round → revived player dies again → same-match state reconstruction → rematch. Only rematch replenishes a spent charge. Do not implement new reconnect functionality; ensure any existing reconstruction cannot replenish it.

### WP2 — Authoritative ghost command pipeline

Files: `TurnManager.UseGhostSkillRpc`, `GhostSkillService`, `UI/Game/Bridge/ILocalPlayerCommands.cs`, `LocalPlayerCommandAdapter.cs`, and the existing network snapshot/reply bridge.

- [ ] Extend ghost intent with context and monotonic per-sender request identity. Keep client intent separate from authoritative result; clients never decrement charge or temperature.
- [ ] Validate sender-to-seat mapping, context, input-open state, ghost eligibility, skill, target, charge/cooldown, target damage allowance and winner latch in that order before mutation.
- [ ] Define typed rejection reasons for stale context, closed input, invalid actor/target, cooldown, spent charge, target already damaged/possessed, and terminal match. Cache bounded results for retries without re-executing the command.
- [ ] Grudge: atomically record allowance and next usable turn, apply damage, flush authoritative deaths, award scores and latch winners. After winner latching, skip top-up and reject subsequent intents.
- [ ] Possession: atomically spend the actor's charge and mark the target for the current turn. No accuracy roll, temperature change or damage-allowance consumption.
- [ ] Publish a correlated result and immutable visual record only after a successful commit. Ready/timeout closure must use the same server-owned admission window; no request can enter merely because CurrentPhase still says Prep.
- [ ] Remove Frost/Chill behavior, modifier reapply/expiry and old kill-source routing consistently. Search all callers, fixtures and serialized references before deleting compatibility names.

Gate: same-target contention, different-target concurrency, double click, retry, late request, old-match reply and winning-hit tests. Rejected commands leave all gameplay state unchanged.

### WP3 — Possession in combat resolution

Files: `Core/Combat/CombatResolver.cs`, `MatchCombatSnapshot` and its builder, `MultiCombatResolution.cs`, `MultiCombatApplicator.cs`, `CombatEventNetData.cs`, the `CombatEventType` declaration, and `Core/Player/PlayerModifiers.cs` only if needed for snapshot projection.

- [ ] Add a distinct snapshot suppression field from authoritative possession state. Do not overwrite or reuse ActionNeutralized, ActiveDefense or unrelated modifiers.
- [ ] Share a small consume-only result builder between `ResolveMultiAction`, `ApplyDefenseMulti` and the aggregate `ResolveMulti` path, so tests/probes do not use different rules from live sequential combat.
- [ ] Apply existing action eligibility checks first. For a valid possessed action, create the ordinary consumed inventory delta and a `SuppressedItemUse` event, with unchanged temperatures, no death mask, no defense reaction and no newly scheduled effects.
- [ ] Preserve pure resolver → authoritative applicator separation. Only the existing inventory mutation path spends the use; the presenter and suppression helper do not directly mutate inventory.
- [ ] In TurnManager, retain defense suppression events in the aggregate. Ensure the main loop cannot replay a defense action or drop a consume-only result due to the EventCount guard.
- [ ] Verify event serialization, aggregate capacity (currently MaxEvents = 16), VFX event filtering and result-temperature handling for the new event. Fail explicitly on overflow; never silently truncate authoritative results.
- [ ] Audit normal-use costs for every item category, including basic/unlimited and multi-use items. Possession blocks new effects without inventing extra depletion.
- [ ] Keep PlayerState selection and minigame gameplay rules intact, but implement the mandatory ticket contract below before accepting possession/minigame validation. Include `Core/Player/PlayerState.cs`, `UI/MiniGame/MiniGameHub.cs` and inventory mutation notifications in the implementation scope.

#### WP3-A — Mandatory minigame ticket contract

1. The server allocates a ticket containing match/round/turn context, monotonic attempt ID, actor, original item binding, target and deadline. The start RPC and completion RPC carry that identity. Multi rejects legacy slot-only completion; Duel keeps its gameplay semantics and receives regression coverage if shared signatures change.
2. The client callback captures its original ticket, not a mutable current-ticket variable. A cancelled old widget or delayed failure callback can therefore never submit as a new minigame. The server validates sender, ticket identity and current context **before clearing any pending state**.
3. Bind the active ticket to the actual item instance through server-owned mutation notifications: removing, consuming to exhaustion or overwriting that item invalidates the ticket, even when the replacement has the same ItemId. Compaction remaps a surviving binding; adding unrelated items does not cancel it. Cover every live writer in PlayerInventory and SeatInventoryMutator. Do not rely on slot plus ItemId equality, or introduce a second inventory store.
4. Once identity matches, check the existing phase, deadline, life, Ready, item and target eligibility. Close only that matched ticket before normal failure consumption or successful queueing. Replayed completion is inert. A rejected old ticket never clears a newer one; an expired current ticket can be closed without spending an item.
5. Ready, death, phase close, round/match reset and removal of the bound item cancel the matching ticket idempotently. Possession alone does not cancel it. Cancellation is not a minigame failure and adds no consumption. Preserve existing accepted-failure and success-then-possession costs.
6. Required tests: attempt A ends, B starts in the same slot, then A success/failure arrives; duplicate B result; prior-round/rematch result; same-ID item replacement; unrelated top-up; slot compaction; deadline and Ready boundaries. Assert both inventory and pending-ticket state.

Gate: data-driven item-category tests assert exactly one normal cost and zero new effects; defense prepass/main-loop integration verifies no double cost. Existing Red Card and 1v1 expectations remain unchanged.

### WP4 — Two-survivor inventory top-up

Architecture application and static suitability review: [inventory transaction and presentation addendum](PLAN_034_inventory_architecture_application.md). It supersedes the fingerprint-join implementation approach in WP4-A steps 5–7 with a retained, atomic presentation envelope. Preserve the synchronous server transaction, approved gameplay and package acceptance gates below.

Files: `TurnManager.TryGrantDeathmatchItems`, `Core/Item/ItemManager.cs`, `Core/Player/PlayerInventory.cs`, `Core/Inventory/InventoryPresenter.cs` and existing inventory command/rebuild callbacks for transaction visibility.

- [ ] Preserve occupied slots and selected-item identity; draw only missing random entries with the existing legal pool and duplicate policy.
- [ ] Return an explicit completed/failed outcome from the grant operation. Zero missing capacity is successful completion, not a failed grant.
- [ ] Prepare additions for both recipients before changing either inventory; detect impossible legal capacity without indefinite retries. Use a bounded authoritative commit with no yield or external gameplay callback between recipients.
- [ ] Mark `_deathmatchGranted` only after success; ensure terminal checks run before grant. If commit fails, restore the staged affected state and report failure, never reroll the first recipient on retry.
- [ ] Preserve threshold grant flags, existing uses, Ready and in-flight valid selections/minigames. Do not add a new inventory store or migrate all copy identities for this top-up.

Gate: inventories with 0/1/3/4 random items, threshold reward overlap, invalid pool, missing service, recipient failure and repeated transition. Test 3→2 as existing Multi regression, without adding new 3-player-specific rules.

#### WP4-A — Concrete top-up transaction and recovery

1. Allocate one round-scoped grant transaction ID. Capture each recipient's full slot values/count, inventory revision and expected life/roster state. Capture affected selection/ticket bindings and the current grant flag for recovery; never reset Ready or threshold flags.
2. Build pure addition plans against temporary slot copies. Store exact rolled ItemSlotNetData and destination slots; enforce capacity and duplicate limits against these temporary copies. If either recipient cannot be completed, change neither live inventory and report a bounded failure. Zero additions is a valid plan.
3. Immediately before commit recheck context, winner latch, both recipients and captured inventory revisions. If stale, discard the uncommitted plan and retry only at a controlled subsequent trigger, not every frame. Never reroll after partially applying a plan.
4. Apply the prepared plans synchronously under a scoped mutation guard owned by existing inventory/turn services. NetworkList callbacks can fire synchronously: handlers must coalesce rebuilds and defer gameplay reactions while guarded; the implementation must not assume callbacks can be disabled by merely avoiding yield. Reject/requeue reentrant commands until the guard exits.
5. On success publish completed inventory revisions and expected final slot content/count for both recipients, then mark the grant complete. Clients stage list changes and enable interaction only when local contents match their completed descriptor. Separate NetworkObjects may deliver metadata and list changes in either order; never assume an RPC is a global atomic barrier.
6. On recoverable write failure, restore both snapshots and affected bindings under the guard, leave the grant incomplete, then publish restored stable contents under a new completion revision. Do not try to retract already queued network deltas; clients reconcile to the restored final state. If restoration fails, close input and enter the existing session-error path instead of continuing with one recipient changed.
7. Exit guards in finally blocks. A client missing coherent contents uses a bounded current-state resync, then an explicit failure outcome, never an indefinite disabled UI. Committed combat visuals keep immutable item data and defer their ordinary rebuild until settlement.
8. Inject failures before first write, between recipients and during restoration; reorder descriptor/list delivery. Assert final slot values, selected item, active ticket, Ready, threshold flags and grant state. Test non-yielding reentrant list callbacks as well as ordinary remote delivery.

### WP5 — HUD and ordered visual integration

Files: `UI/Game/Presenters/GhostSkillPresenter.cs`, `GhostSkillVFXPresenter.cs`, HUD builder/refs and bridge snapshots, `Core/Combat/CombatVFXManager.cs`, TurnManager presentation RPC/barrier paths.

- [ ] Replace buttons/tooltips and availability logic: Grudge available/cooldown, Possession available/used for this match. A round reset must not visually re-enable spent possession.
- [ ] Reuse current target selection. Preview target damage eligibility locally, but always validate on the server. Correlate reply to pending intent so stale rejection cannot dismiss a newer selection.
- [ ] Show the possession status through the affected turn and minimal suppressed-use feedback. Do not display successful item effects for suppression; preserve existing item timing and simultaneous defense policy.
- [ ] Route accepted ghost casts, impacts and deaths through the existing presentation owner with context and sequence identity. Queueing visuals must not consume gameplay authority or reject independent valid skills.
- [ ] Integrate PLAN 031 duration/forced-settlement requirements before full visual acceptance. Derive waits from actual scheduled presentation units rather than raw event count, because suppression adds metadata/events without necessarily adding an item interval.
- [ ] Ensure timeout, disconnect, scene exit and rematch cleanup do not discard newer accepted records or ACK a different sequence. Late UI subscription reconstructs current status rather than replaying old casts.

Gate: all four views agree on targeting, cooldown/spent state, suppression feedback and impact-before-death. Neither extra defense waiting nor silent barrier timeout counts as success.

#### WP5-A — Suppression routing and shared timing contract

1. Extend the Multi presentation scheduler to classify `SuppressedItemUse` explicitly. The current MainEffect/DefenseActivated filter must not silently skip it. Never route this event through the successful item-effect sequence.
2. A suppressed non-defense item occupies its ordinary action position and existing action-time budget (at least three seconds, retaining configured longer timing). Show the possession/failure cue within that budget, without damage/heal/defense effect VFX. No additional failure interval is appended.
3. A suppressed defense is a nonblocking cue shown at sequence setup with its possession marker; it has zero standalone action budget and no successful counter/block animation. Display it even if no opponent attacks. If the batch contains only defense/suppression metadata, render the cues and settle normally without manufacturing item-action slots.
4. Use one immutable schedule/duration calculation for server barrier budgeting and client scheduling: intro, actual non-defense actions, inter-action pauses, associated deaths and a bounded network margin. A defense marker or extra event does not increment action count. Clamp cue duration inside its hosting interval and keep the scene's existing timing constants rather than add a new game rule.
5. Migrate PLAN 031's older event-based action detection together with this scheduler. Update both server and client consumers in the same package; changing only a timeout constant is insufficient. Ghost records use their own existing cast/impact duration and associated deaths, not the item three-second rule.
6. Test zero ordinary actions, suppression-only non-defense, defense-only suppression, mixed successful/suppressed actions, configured durations above three seconds, deaths, missing visual objects and forced settlement. ACK each record once; absence of a renderer must not stall gameplay.

### WP6 — Automated and real-player evidence

Files: existing `Assets/Tests/Editor/Plan029CombatRulesTests.cs` as regression reference; add focused PLAN 034 Editor tests under the same test setup. Adapt `Core/Solo/MatrixScenarioProbe.cs`, `MatrixInventoryChecks.cs` and the existing four-player probe without leaving test injection active in ordinary builds.

- [ ] Run focused domain/lifecycle tests for WP1–WP4, then existing affected regression tests. Record case names and actual results; no inferred passes.
- [ ] Let Unity compile automatically, inspect console, and verify the connected project before live actions. Never force recompile_scripts.
- [ ] Build identical development players and run actual Relay Host + three clients using the previously authorized test setup. Verify relay transport selection and four distinct authentication profiles; label any direct/local fallback accurately.
- [ ] Execute the scenarios below, capture each peer at checkpoints, compare authoritative state summaries and collect errors/timeouts. Repeat failures after fixes; stop acceptance on any unresolved correctness failure.
- [ ] Test 1v1 item use, simultaneous defense, minigame result and round restart; no ghost feature should alter Duel behavior.
- [ ] Save results under `Docs/Validation/PLAN_034_results.md` and captures/logs under a dedicated `output/validation/plan034/` run directory during implementation. Update plan checkboxes only with evidence.

#### Visible four-player scenarios

Use seats A/B/C/D mapped explicitly to Host/Client identities. Any staged inventory, temperature or score is test-only and must be labeled in the report.

| Scenario | Actions | Checkpoints |
|---|---|---|
| S1 — Damage cap/cooldown | Stage C and D as ghosts, A/B alive. In T both ghosts request Grudge on A. In T+1 the accepted caster retries; in T+2 retry on a legal target. | One T hit, rejected caster spends nothing, accepted caster blocked at T+1 and eligible at T+2; repeat with request order reversed. |
| S2 — Possession consumption | C possesses A in Prep. A uses each staged category across separate fresh scenario runs: attack, heal, defense, multi-use, unlimited, minigame. | One normal cost, no effect, correct Ready order, no extra defense interval, all four HUDs agree. |
| S3 — Match-long use | C uses possession, advance turn and round, revive C and eliminate C again. Attempt reuse; then begin a genuine rematch. | No reuse within the original match; new match starts with one charge. |
| S4 — Top-up | Stage survivor inventories at 3 and 1 random items, then reach exactly two survivors. Repeat with 4 and 0. | Preserve original items/uses; additions 1/3 and 0/4; only one grant. |
| S5 — Ghost victory | Stage C at four kills and A within Grudge lethal range. Accept C's hit, then send another action. | C's fifth kill latches immediately; later command rejected, no top-up/next round, impact→death→results once. |
| S6 — Network boundaries | Delay/retry requests and ACKs; close Prep, disconnect a peer during VFX, and rematch with old messages pending. | No duplicate cost/hit, stale mutation, stuck barrier, wrong ACK or discarded newer accepted visual. |
| S7 — Minigame identity | Delay attempt A's result until B starts in the same slot; repeat across rounds and with a same-ID replacement. | B remains pending, no stale consumption or queueing; unrelated top-up keeps a valid ticket. |
| S8 — Grant recovery | Inject failure between recipient writes and reorder inventory completion/list notifications. | Both inventories restore or complete coherently; no duplicate grant, false Ready reset or permanent input lock. |
| S9 — Suppression timing | Run defense-only, mixed defense/attack and suppressed ordinary-item batches. | Failure cue always visible, normal action timing retained, no extra defense interval or silent timeout. |

### Package dependencies and acceptance

WP1 → WP2 → WP3 → WP4 → WP5 → WP6 is the working order. PLAN 031 presentation work is integrated in WP5 and must pass before WP6 visual acceptance. Each package remains a reviewable change; do not combine unrelated side-art or harness edits.

Implementation readiness means this contract can guide coding; delivery acceptance requires all critical domain, lifecycle, four-peer synchronization and 1v1 regression checks to pass. Visual-only limitations must be listed explicitly. If resolving a failure would change the confirmed gameplay rules, pause that dependent change and ask the user; technical fixes within these rules do not require another design approval.

## Validation matrix

| Case | Acceptance |
|---|---|
| Top-up at 0/1/3/4 random items | Add 4/3/1/0; preserve existing items, uses, selections and Ready. |
| Invalid pool/service, recipient failure, repeated trigger | No loss or double grant; coherent failure/recovery. |
| Threshold plus top-up; 4→2/3→2/3→1 | Correct capacity and one-time transition; no terminal grant. |
| Two ghosts attack one target in either order | One accepted damage; rejected ghost spends nothing. |
| Different targets, duplicate/stale RPC, rematch | Independent valid hits; no replay mutation or stale UI clearing. |
| Grudge cooldown, lethal fifth kill | Explicit T/T+1/T+2; one kill, fixed winner, no later action. |
| Possessed attack/heal/defense/sabotage/basic item | Normal cost once; no new effect; correct suppression feedback. |
| Multi-use/unlimited item | Decrement one normal use or retain unlimited status respectively. |
| Possessed minigame failure/success/cancel | Failure cost once; success consume-only; cancellation preserved. |
| Unselected/Ready target; duplicate possession | Normal selection/Ready, no charge for rejection. |
| Possession plus Grudge, either order | Independent damage allowance and charge. |
| Preexisting delayed/fan effect | Existing effect remains; possessed new use adds no effect. |
| Defense prepass and zero-damage result | Delta applied once, event retained, no skip/double charge. |
| Prep close, delayed NV/RPC/ACK, timeout/disconnect | Correct admission, impact-before-death, bounded settlement. |
| Turn/round/rematch possession lifecycle | Turn marker expires; spent charge stays spent through later rounds, revival and another death; new match/rematch starts with one charge. |
| Possession disconnect/reconnect and rejected/retried request | No charge refund or duplicate consumption; valid use has no random miss roll. |
| 3-player and 1v1 regression | No hardcoded four-seat requirement; Duel unchanged. |
| Real Relay Host + 3 clients | Matching inventories, temperatures, life, scores, cooldowns and markers. |

After implementation: focused domain tests, automatic Unity compile/console checks, then visible four-player runs with all-view captures and context-correlated logs. Exclude auth tokens/join codes. Older one-turn Relay results do not certify these changes.

## Review outcome

Source-based review in this task, not independent-agent review or runtime execution.

- R1–R3 identified request identity, epoch, input closure, visual ordering and timeout risks; relevant safeguards remain above.
- R4 incorporates the user answers: keep top-up; replace non-consuming neutralization with distinct consume-only possession; preserve normal selection/minigames.
- R4 retires replacement-specific inventory migration and possession-triggered ticket cancellation proposals. R6 below promotes stale minigame-result protection to mandatory work without reinstating possession-triggered cancellation.
- R5 records the user's explicit correction: possession is once per match, never once per round; Grudge is usable at T, blocked at T+1, reusable at T+2. Separate transient round cleanup from the persistent possession spent flag, including shared bootstrap and ghost respawn paths. Both skills use the Sheet's 100% success for valid accepted requests.
- Technical disposition: the previously blocking possession lifetime decision is resolved. The plan records an implementation contract and lifecycle regression cases; Sheet damage `3(?)` remains a disclosed 3-degree implementation baseline, not a claim of finalized balance. No further lifetime/cooldown confirmation is needed. Executed evidence and acceptance gaps are recorded in the results report.

### R6 — Detailed-plan corrections and re-review

Planning changes only. Rechecked against PlayerState's slot-only completion and MiniGameHub callbacks, the Multi VFX MainEffect filter, PlayerInventory's incremental top-up writes, and the shared round/rematch bootstrap.

| Finding | Mandatory correction | Re-review disposition |
|---|---|---|
| Old minigame result can consume/clear a newer attempt | WP3-A: end-to-end captured ticket, identity-before-clear, actual item binding and explicit invalidation | Plan gap addressed; implementation tests S7 remain pending. |
| Suppressed event can disappear or distort time budgets | WP5-A: explicit event route, ordinary-action timing, zero added defense interval and shared schedule | Plan gap addressed; renderer/timeout cases S9 remain pending. |
| Top-up rollback and notification atomicity were abstract | WP4-A: staged exact additions, guarded commit, restoration, completion descriptors and bounded resync | Plan gap addressed; injected failures and reordered delivery S8 remain pending. |

Second consistency pass: stale deferral clauses removed; possession does not cancel a minigame; cancelled tickets do not consume; ordinary suppression costs/timing differ from defense cues; failed grants do not complete or reroll partial writes; match-long charge survives round resets. No additional design change is required by these corrections. No independent-agent review or runtime execution occurred.

Plan readiness assessment: 94/100 (design trace 24/25, implementation mapping 24/25, networking/failure contracts 23/25, verification plan 23/25). This is a qualitative document-completeness rubric, not a defect probability or test result. The three known planning blockers are resolved at specification level; implementation may begin in WP order with mandatory package gates. Runtime correctness, fault injection feasibility and actual Relay evidence remain to be established during implementation.
