# PLAN 036 - Suitability review and verification gates

Date: 2026-09-28
Target: [Solo duel BT plan](../Plans/PLAN_036_solo_duel_bt_graph.md), current working-tree source rather than HEAD alone.
Scope: static plan/source review. No gameplay implementation, package installation, scene modification, or Solo runtime test was performed for this review.

Decision update, 2026-09-28: the user confirmed no bot mini-game participation; use per-item penalties such as delay under existing 1v1 progression. References below to bot mini-game settlement mean a pending bot item-use delay operation replacing that requirement, not a simulated mini-game or success-probability policy. Existing source findings still apply; they have not been fixed by this documentation update.

## Verdict

**Proceed to detailed implementation planning. Do not treat this as implementation acceptance.**

One executable with an internal NGO host, two logical participants, shared 1v1 rules and a server-controlled visual BT is a suitable direction. It avoids a second rules engine while retaining current combat and presentation infrastructure. The plan already identifies the main architectural seams, but its broad contracts are not yet sufficient to implement safely without further specification.

The detailed plan must close R1-R8 below with explicit owners, state transitions, file targets and acceptance tests. Remaining gameplay decisions may be recorded as pending while independent infrastructure is planned; dependent implementation must wait. No bug-free guarantee or runtime pass is implied.

## Prioritized findings

### R1 - High: a connectionless bot currently becomes a disconnected human

- Evidence: `PlayerRegistry.RegisterPending` keys registration by ClientId; `PlayerState.OnNetworkSpawn` registers OwnerClientId. `MatchRoster.Hydrate` gives participants without a connection `Disconnected` state. `IsTurnEligible` requires Connected; `IsAutoReady` explicitly selects disconnected living seats. `TurnManager.WaitForPlayersRoutine` maps seats through OwnerClientId; `MatchManager.FixMatchRoster` keys its map by OwnerClientId.
- Trigger: spawn a server-owned bot under the host's ownership, or add a participant with null ClientId using the existing hydration path.
- Risk: duplicate/overwritten registration, waiting forever for the second player, or treating the bot as an absent player and automatically readying it without its action.
- Required detail: separate controller role (human/bot), logical participant/seat, transport ownership and connection status. Specify spawn -> register -> bind -> ready -> despawn ordering and rollback. A bot is locally available without pretending to be a connected client. Preserve online disconnect/reconnect semantics.
- Gate: V03-V06, V26.

### R2 - High: owner-based behavior extends beyond camera selection

- Evidence: `AZPlayerVisual.OnNetworkSpawn` chooses FPS behavior using IsOwner. `CombatVFXManager` uses IsOwner throughout attack, defense, recovery and impact paths (e.g. lines 579, 713, 760, 835, 966, 1462). `PlayerState.OnNetworkSpawn` also submits the local cosmetic profile when IsOwner; bot mini-game RPC is owner-directed.
- Trigger: a host-owned bot is mistaken for the local human even after its logical seat is corrected.
- Risk: bot overwrites its intended cosmetics with the user's profile, opens human mini-game UI, or plays first-person effects instead of opponent animations.
- Required detail: classify every relevant ownership check as authorization, network routing or viewing perspective. Change only perspective decisions to an explicit human-seat query; preserve authorization checks. Route bot mini-game settlement internally and cosmetic configuration through the bot definition. Include local hit feedback and HUD/command binding.
- Gate: V07, V12, V22-V23.

### R3 - High: offline startup requires lifecycle isolation, not only skipping login

- Evidence: `AppBootstrapper` awaits online initialization. `NetworkSessionCoordinator.InitializeAsync` awaits authentication and then subscribes callbacks/sets Ready without an operation-generation check. The coordinator routes non-Multi scenes to GameScene; the old SoloMatchStarter also hard-codes GameScene and launches a bot executable.
- Trigger: enter Solo while online initialization is pending, return during startup, double-click launch, or finish authentication after the mode changed.
- Risk: stale callbacks mutate the new session, duplicate spawn/start, incorrect scene load, port conflict or cleanup owned by two coordinators.
- Required detail: define the sole lifecycle owner for each mode and session generation; stale completions cannot subscribe/mutate a new session. Local bootstrap must initialize required shared services without UGS. Specify all scene routing, build registration, cancellation, partial-start rollback and socket release. A failed online login must not block Solo entry.
- Gate: V02, V08-V10, V25-V27.

### R4 - High: action settlement and Ready need an explicit transactional contract

- Evidence: `PlayerState.PressReadyServerRpc` cancels pending mini-games, records server tick/Time.time, then stops the fan. The proposed graph awaits settlement but does not yet specify timeout/turn-expiry ordering or duplicate request handling.
- Trigger: delayed bot mini-game completion races with Prep timeout, Ready, restart or an inventory slot change.
- Risk: item consumed twice, effect silently canceled, late action applied to a new turn, or a bot waiting forever on a canceled ticket.
- Required detail: specify request identity and deduplication, item CopyId, immutable turn/session key, accepted/pending/rejected/stale results and terminal ticket states. Shared authoritative operations validate both human and bot requests; bot APIs are internal and bound to a registered Solo bot. Preserve current main-action/reselection/cancel rules rather than interpreting 'once' as a new restriction on human controls. Define precedence when ticket settlement and Prep expiry coincide, consistent with effective 1v1 rules.
- Gate: V11-V15, V19.

### R5 - Medium: starting overrides need a lifecycle matrix

- Evidence: `RoundLifecycleService.ResetPlayersForNewRound` resets temperature to 37 and fan to 1; `ResetForNewTurn` only clamps temperature and restarts the fan. `RevertFanUpgrade` restores global default speed. Starting grants are implemented in runtime methods as well as configuration.
- Trigger: apply all 'initialization' overrides before every Prep, or restore speed to 1 after an upgrade on a differently configured bot.
- Risk: unintended healing/restocking each turn, lost difficulty baseline, duplicated starting grants or unexpected threshold bonuses.
- Required detail: tabulate match start, first round, subsequent round, new turn, upgrade expiration and replay. Starting temperature/loadout must never be reapplied every turn. User must decide first-round-only vs per-round loadout/temperature semantics before implementation of those overrides. Record baseline-plus-modifier composition and actual current 1v1 grant timing.
- Gate: V16-V18, V24.

### R6 - Medium: graph failure branches and clocks are underspecified

- Evidence: PLAN_036 flowchart sends 'validate Ready' directly to 'Ready once'; its fallback path proceeds to next-Prep waiting even when it says to await pending settlement. Text guards are stronger than these edges.
- Trigger: validation fails after thinking, repeated Prep notification arrives, pending settlement stalls, or phase changes during a delay.
- Risk: skipped turn, duplicate graph runs or a retry loop that exceeds Prep.
- Required detail: explicit states Observe/Think/Submit/AwaitSettlement/WaitReady/Done/Cancelled; branch on rejected Ready; leave a turn only after accepted Ready or authoritative phase exit. Bound retries and free-item actions. Choose a consistent authoritative deadline source, preserving existing tick/initiative behavior. Watchdog reports failure and uses only a currently legal fallback; it must not force an outcome.
- Gate: V13-V15, V19-V21.

### R7 - Medium: package and rule compatibility are gates, not established facts

- Evidence: `com.unity.behavior` is absent from manifest. MatchCompositionRoot uses exact TargetMode matching; the existing GameScene registers a OneVsOne rule, not a Solo rule.
- Required detail: validate exact installed graph APIs/assembly requirements in S0, with a player-build fixture and rollback limited to introduced changes. Specify how Solo binds the shared 1v1 rule without changing its mode to online OneVsOne or duplicating mutable balance definitions. Capture effective runtime rules, not only SO fields.
- Gate: V01-V02, V16, V27.

### R8 - Medium: deterministic evidence needs more than a random seed

- Evidence: the plan correctly separates AI RNG from combat/drop RNG, but reaction delays and changing temperatures influence decision inputs and initiative.
- Required detail: fixtures use a controlled clock and observation sequence. Runtime traces record seed, resolved configuration, graph version/node path, session/round/turn, observation revision, request/result, authoritative times and terminal outcome. Same seed alone is not a promise of identical real-time matches. Prediction uses copied state/pure estimators and cannot call live mutating combat operations.
- Gate: V20-V21, V28.

## Pattern suitability

| Proposed pattern | Fit and constraint |
|---|---|
| Composition root + mode-specific session coordinator | Fits local bootstrap; coordinator owns lifecycle only, TurnManager retains phase ownership |
| Participant identity separated from transport mapping | Necessary for two seats on one connection; no fabricated ClientId and no global rewrite of all multiplayer services |
| Validated command service + human RPC/bot adapters | Reuses legality and authority; adapters must not duplicate consumption or resolution logic |
| Immutable SO configuration + per-match runtime context | Fits difficulty presets; no shared mutable Blackboard or runtime SO edits |
| Visual BT policy + narrow runner adapter | Fits designer-editable behavior; small nodes and explicit branches, no second combat/state machine hidden in a single node |
| Observation snapshot + pure scoring | Supports fair, testable decisions; no hidden human intent or live-state mutation for prediction |
| Explicit local perspective | Necessary for host-owned opponent rendering; must not replace ownership checks used for security |

## Required validation checklist

All items below are **TODO** for implementation. This table defines tests; it does not report them as executed. 'Unit' means focused pure/EditMode fixtures where feasible; 'Play' means real Editor or player execution.

| ID | Stage / method | Scenario and required result |
|---|---|---|
| V01 | S0 / Editor + build | Exact Unity Behavior version loads a minimal graph, executes custom nodes and builds for the intended player target; no unrelated upgrades |
| V02 | S1,S3 / unit + Play | Solo resolves the shared effective 1v1 rules; missing rule/graph/config fails clearly; dedicated scene included in build |
| V03 | S2 / unit + Play | One connection, two distinct logical participants/seats; neither registration nor cosmetics overwrite the human |
| V04 | S2 / unit | Bot is turn/target eligible as appropriate, never disconnected auto-Ready; dead bot is ineligible |
| V05 | S2 / unit | Registration/despawn reordered or repeated; cleanup idempotent, no stale seat or binding, no startup hang |
| V06 | S2 / unit | Wrong seat, wrong mode, forged role, invalid CopyId, expired turn and duplicate request rejected with no state mutation |
| V07 | S3,S6 / Play + screenshots | Human FPS/HUD/input bind only to human; bot uses opponent view, configured cosmetics and correct target presentation |
| V08 | S3 / offline build | Launch with internet unavailable and no cached UGS login; start a playable Solo turn without Relay or second process |
| V09 | S3 / Play | Double start, occupied port, missing scene/config, cancellation during startup; one session or actionable failure, complete rollback |
| V10 | S3,S6 / controlled async + Play | Online auth completes/fails after entering/leaving Solo; no stale callback changes mode, subscriptions or session state |
| V11 | S2,S4 / unit | Main/free/defense/recovery/self/opponent actions use shared validation, slot capacity and consumption semantics |
| V12 | S5 / unit + Play | Human mini-games unchanged; bot applies the configured per-item delay without human UI or success rolls; no Ready bypass, double-counted delay, duplicate consumption or early queued combat effect |
| V13 | S4,S5 / controlled clock | Pending mini-game overlaps Ready, expiry, death, round change and restart; explicit terminal result, no late effect or duplicate consumption |
| V14 | S4 / graph fixture | Repeated Prep events, rejected item/Ready, removed copy and all candidates invalid; bounded replan and legal fallback |
| V15 | S4,S6 / Play | Exit during thinking, command wait, mini settlement and presentation; no delayed work affects a subsequent turn/session |
| V16 | S1,S5 / unit + baseline trace | Neutral profile matches actual 1v1 grants, temperature, fan, initiative and round rules; SO assets unchanged |
| V17 | S5 / unit | Start/new turn/new round/replay follow explicit stat matrix; no per-turn heal/restock; invalid values/catalog entries rejected |
| V18 | S5 / unit + Play | Custom fan baseline with upgrade/buff expiration; approved loadout replacement and lower-temperature thresholds occur once at defined points |
| V19 | S4 / controlled clock + Play | Ready near Prep limit, low-temperature thinking, same-tick Ready ordering and slow frames follow authoritative rules without deadlock |
| V20 | S4,S5 / unit | Survival, finish, counter, resource and fallback branches select legal explainable actions; hidden human choice changes alone cannot change an identical permitted observation decision |
| V21 | S4,S5 / unit | Isolated Blackboards and RNG; extra graph evaluation does not change drop/combat RNG; scoring does not mutate state; fixed inputs/clock/seed reproduce trace |
| V22 | S6 / Play + screenshots | Human attacks/bot defends and reverse, both initiative orders; defense simultaneous, no duplicate animation or bot FPS misrouting |
| V23 | S6 / Play | Attack, recovery, special-item impact and completion; expected ACK set contains real viewing connections only; absent bot ACK never causes a timeout |
| V24 | S6 / Play | Full Bo3 win/loss/draw, next round, result once, replay; all state resets according to approved contracts |
| V25 | S6 / repeated Play | Menu exit/replay at every phase; no graph, coroutine, event, network object or socket accumulation |
| V26 | S7 / network Play | Online 1v1 match/rematch/disconnect plus Multi Host+3 clients, ghosts/targeting/inventory; identity/perspective changes preserve current behavior |
| V27 | S7 / build + Play | Editor direct scene and shipped menu entry; Solo -> online -> Solo, failed online entry -> Solo; no leaked stats/roles/configuration |
| V28 | S7 / automated matches | Fixed seed corpus and recorded timing, bounded-turn watchdog; stalls retained as failures; logs, config/graph IDs and representative screenshots attached |

Evidence per test: code/build revision, configuration, test scenario/seed, expected vs actual, pass/fail, log/capture path and any deferred condition. Runtime console status alone is insufficient evidence of correct gameplay.

## Decisions and detailed-plan exit criteria

Confirmed and retained: local NGO host in one executable; no Relay/UGS dependency; visual BT; starting temperature, base fan speed, starting items and AI temperament as initial tuning scope; existing 1v1 combat/presentation rules.

Still requires a design answer before the affected implementation:

1. Exact per-item delay values and any additional penalty types. The no-mini-game policy is settled; random mini-game outcomes are not part of it.
2. Optional custom starting items: replacement versus addition, retention of basic items, and application on first round versus every round. Define optional starting-temperature reset scope alongside this. Baseline grants/progression follow existing 1v1.
3. Ordinary temperature threshold rewards remain the baseline under the latest 1v1-preservation decision. Any exception needs a separate explicit design change.

Bot names, final difficulty count and numeric balance presets can follow neutral-fixture infrastructure work. Do not silently assume the proposals above are approved.

The detailed plan is ready for implementation review when it contains:

- [ ] Exact participant/controller/connection/perspective data contract and migration call sites (R1-R2).
- [ ] Startup/shutdown ownership, session generation, scene routing and rollback sequence (R3).
- [ ] Command/result/ticket state table, expiry precedence and duplicate-handling contract (R4).
- [ ] Stat/loadout lifecycle matrix and recorded user decisions (R5).
- [ ] BT state transitions including failed Ready, bounded retries and cancellation (R6).
- [ ] Package compatibility task and explicit Solo-to-1v1 rule binding (R7).
- [ ] Deterministic fixture inputs, trace schema and non-mutating observation/scoring boundary (R8).
- [ ] S0-S7 split into bounded file-level tasks, each mapped to V01-V28; regressions after shared identity/command edits, not only at the end.

Static review completed: plan/source consistency, identity/roster eligibility, owner-based presentation, pending-mini/Ready flow, reset/grant methods, async initialization and scene routing. Runtime tests above remain unexecuted because the planned Solo architecture is not yet implemented.
