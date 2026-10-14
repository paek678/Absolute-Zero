# Balance campaign flows — 30 matches (2026-10-13)

## Status and scope

**REPEAT EXECUTION AUTHORIZED — RUNNING (2026-10-13).** The user selected Solo vs Bot, Duel and four-player Multi, ten matches each; three-player matches are excluded. The earlier planning-only stop was superseded by execution authorization. The first cohort produced sixteen complete records before process interruption; a fresh thirty-match repeat retains all observation requirements. Development-only draw instrumentation is present. No production balance values were changed. See the execution and repeat records below.

Baseline: main commit `b74ec19`. Preserve the real Solo BT, item values, drop weights, minigame rules and timeScale=1. Multi uses maximum three rounds, first three cumulative kills, then highest cumulative score with joint victory for tied leaders. Do not use promotional item-variety overrides, forced deaths or forced drops in the primary cohort.

## Existing policy vocabulary

| Policy | Actual preference | Distinct risk to observe |
|---|---|---|
| balanced | Damage, healing and utility | Ordinary match baseline |
| attacker | High damage preference, faster Ready | Burst, initiative and early elimination |
| survival | Healing and defense preference | Stalling, wasted recovery and long matches |
| resource | Extra preference for permanent items | Permanent-item dominance and unused consumables |
| utility | Extra utility preference | Disruption, theft, denial and ghost Possession |
| cautious | Balanced scoring, slower action/Ready, lower modeled minigame success | Slow preparation and failed minigames |

These are existing diagnostic policies, not verified human personas. Cautious bundles multiple variables; do not attribute its outcome to any one variable. Utility is a preference, not a guarantee that every utility item will be drawn or used.

## Solo vs Bot — ten complete matches

Keep the opponent's existing BT and stats fixed. The listed policy drives only the simulated human seat. Use two different frozen gameplay/policy seeds per style.

| Match | Human policy | Observation focus |
|---|---|---|
| S01–S02 | balanced | Ordinary tempo, recovery and bot response |
| S03–S04 | attacker | Early aggression and first-action advantage |
| S05–S06 | survival | Healing/defense chains and match length |
| S07–S08 | utility | Disruption/utility value against the real BT |
| S09–S10 | cautious | Failed minigames, slow Ready and bot penalties |

Do not swap the real BT into a human policy for symmetry. Compare human/bot outcomes separately from Duel seat comparisons.

## Duel — ten complete matches

Each consecutive pair reuses a frozen gameplay seed. Swap the policy seats in the second match and retain policy identity seeds where a future runner supports this. Current runner seeds by seat, so any run without that support is only a seat-sensitivity comparison, not a fully matched causal experiment.

| Match | Seat 0 | Seat 1 | Observation focus |
|---|---|---|---|
| D01 | balanced | balanced | Baseline |
| D02 | balanced | balanced | Independent baseline seed |
| D03 | attacker | survival | Burst versus recovery |
| D04 | survival | attacker | Swap roles/host seat |
| D05 | resource | utility | Permanent items versus disruption |
| D06 | utility | resource | Swap roles/host seat |
| D07 | attacker | cautious | Speed and modeled minigame-failure pressure |
| D08 | cautious | attacker | Swap roles/host seat |
| D09 | survival | utility | Defensive stalling versus disruption |
| D10 | utility | survival | Swap roles/host seat |

## Four-player Multi — ten complete matches

| Match | Seats 0 / 1 / 2 / 3 | Observation focus |
|---|---|---|
| M01 | attacker / survival / resource / utility | Mixed baseline |
| M02 | utility / attacker / survival / resource | Rotate all policies by one seat |
| M03 | resource / utility / attacker / survival | Rotate by two seats |
| M04 | survival / resource / utility / attacker | Rotate by three seats |
| M05 | attacker / attacker / utility / resource | Aggressive lobby, rapid deaths and kill concentration |
| M06 | utility / resource / attacker / attacker | Aggressive roles moved to opposite seats |
| M07 | survival / utility / survival / resource | Recovery-heavy lobby and round cap |
| M08 | survival / resource / survival / utility | Utility/resource role swap |
| M09 | cautious / attacker / utility / resource | Slow/failed actions under multiplayer pressure |
| M10 | utility / resource / cautious / attacker | Slow/aggressive roles moved to opposite seats |

Use one shared gameplay seed for M01–M04, then one seed per M05–06, M07–08 and M09–10 pair. Shared seeds do not guarantee identical inventories: differing decisions alter eligibility and RNG consumption. Retain the actual per-draw context when it becomes available.

### Within-match observation checkpoints

1. Initial grant: starting inventory, duplicate count, attack/recovery/utility mix.
2. First preparation/action: Ready latency, first actor, accepted/rejected selections.
3. Middle of each round: permanent versus consumable use, healing/defense, unused inventory and target pressure.
4. First death: score attribution, freeze/ghost transition and remaining seats.
5. Ghost play: Grudge cooldown, actual damage, Possession use, suppressed item consumption and status expiry.
6. Two survivors: remaining-slot refill versus excess grant, duel tempo and comeback opportunity.
7. Round boundary: round duration, temperature/inventory/buff reset, persistent score.
8. Match end: first-three-kill or round-cap reason, winner mask, ties, remote agreement and lobby return.

A checkpoint that never naturally occurs is unobserved, not passed. Do not force it to make the primary-match count look complete.

## Data and balance questions

| Area | Required measures | Decision supported |
|---|---|---|
| Pace | Gameplay duration separate from connection/loading, per-round duration, turns, prep/action/presentation time | Whether 5–8 minute target / under-ten-minute tendency is plausible |
| Initiative | First-action share, Ready latency, seat/host and policy outcomes | Whether speed or a seat dominates |
| Items | Draws, actual holdings, accepted selections, completed actions, consumption, suppression, damage/heal/block/clipping | Underused items, excessive value, wasted recovery |
| Drop probabilities | Configured weight, eligible candidate weights for each draw, actual selected item and draw context | Conditional expected versus observed frequency |
| Inventory | Initial grants versus refill/replacement/transfer, duplicates, full-slot exclusions | Crowding, permanent-item pressure, copy-limit effects |
| Targets and ghosts | Per-source/target actions, deaths, awarded kills, ghost duration and skill outcomes | Focus fire, runaway leader and post-death influence |
| Results | Win/tie masks, round cap versus kill threshold, policy and seat | Observed outcome distribution, not causal item win contribution |

Existing balance telemetry covers many action/effect/order/Ready and inventory events. **It does not separately prove genuine random drops:** insertion can also mean initialization or transfer. Per-draw eligible-weight telemetry and attribution must be designed and validated before claiming spawn-probability results. That implementation is deferred by this stop request.

Existing target selection prefers the coldest alive opponent (seeded tie breaking). These flows therefore examine concentration on vulnerable targets; random-target, retaliation and leader-target strategies are NOT implemented by these policy names. If broader targeting is required, add opt-in test strategies before execution and freeze them in the manifest. Do not label current runs as those strategies.

## Expanded observation contract — 60 checkpoints in 15 areas

User follow-up expands observation planning only. The 30-match allocation remains unchanged; implementation and execution are still stopped. More measurements must explain potential problems, not automatically justify more balance changes.

Readiness labels below describe evidence requirements, **not completed implementation**:

- **E**: relevant raw events already exist; verify field completeness against a fresh trace.
- **D**: new derived analysis/joins needed, even where raw events exist.
- **N**: new opt-in observation fields or event boundaries needed before a trustworthy measurement.
- **V**: genuine rendered frames/video needed; state logs alone are insufficient.
- **H**: human observation required for perception, difficulty or enjoyment claims.

### 1. Match pace and idle time

| ID | Measurement / question | Evidence |
|---|---|---|
| B01 | Active gameplay duration from first preparation opening to committed terminal result; separately measure joining, loading and return-to-lobby | E/D |
| B02 | Per-round and per-turn duration, displayed as match-level values plus median/range; expose unusually long tails | E/D |
| B03 | Preparation, authoritative resolution and presentation elapsed time; distinguish overlapping intervals to avoid double-counting | E/D/N |
| B04 | Time with no available player action versus time voluntarily waiting; who holds preparation open and for how long | N/D |

### 2. Initiative and response opportunity

| ID | Measurement / question | Evidence |
|---|---|---|
| B05 | Ready latency and first-order position by policy, seat, host/client and alive-player count | E/D |
| B06 | First **executed** action versus first queued position; classify defenses and skipped/dead actors separately | E/D/N |
| B07 | Deaths before the victim can execute its queued response; available defense/heal options at selection time | E/D/N |
| B08 | Consecutive first actions and opponent response opportunities before lethal damage; distinguish speed from item strength | E/D/N |

### 3. Random drops and luck

| ID | Measurement / question | Evidence |
|---|---|---|
| B09 | Per-draw eligible item IDs/weights, selected item and conditional expected probability; compare initial/refill/replacement draws separately | N/D |
| B10 | Initial attack/heal/defense/utility mix, per-seat variety and matched-case inventory differences | E/D/N |
| B11 | Consecutive identical draws, duplicate-limit exclusions, full-capacity skipped grants and time/turns without a legal heal or attack option | N/D |
| B12 | Acquired loadout versus later performance, conditioned on mode/policy/round; identify apparent lucky starts without claiming causal item win value | E/D/N |

### 4. Inventory circulation and scarcity

| ID | Measurement / question | Evidence |
|---|---|---|
| B13 | Occupied/free usable slots by turn; time at capacity and incoming items blocked by capacity | E/D/N |
| B14 | Physical-copy path: random grant, fixed grant, transfer, replacement, use, expiration and round reset; maintain CopyId lineage | E/D/N |
| B15 | Turns between acquisition and use; unconsumed copies at death/end; distinguish unusable copies from deliberate saving | E/D/N |
| B16 | Refill at two survivors: capacity before/after, awarded copies, skipped copies and changes in subsequent pace; exclude transfers from supply totals | E/D/N |

### 5. Choice quality and meaningful alternatives

| ID | Measurement / question | Evidence |
|---|---|---|
| B17 | Number of legal item/target choices at decision time; zero-option and permanent-item-only turns | N/D |
| B18 | Selection, acceptance, action execution and effect occurrence as distinct stages; rejection, invalidation, death and suppression reasons | E/D/N |
| B19 | Item chosen when legally available, versus merely held; policies repeatedly ignoring specific items and their recorded candidate estimates | E/D/N |
| B20 | Repeated permanent-item use despite legal consumables; choice concentration and diversity per policy, not just aggregate item popularity | E/D/N |

### 6. Item value and waste

| ID | Measurement / question | Evidence |
|---|---|---|
| B21 | Requested versus realized damage/heal, mitigation and temperature-cap clipping per accepted action and completed action | E/D |
| B22 | Actual defense prevented, defense left unused and duration expired without benefit; do not value defense only by activations | E/D/N |
| B23 | Overhealing, damage beyond lethal, canceled delayed effects, suppressed consumption and target-lost uses as separate waste reasons | E/D/N |
| B24 | Utility-specific outcomes: copies stolen/replaced, useful restrictions applied/expired, information actually exposed; do not rank every item by damage | E/D/N |

### 7. Sequences, combinations and counterplay

| ID | Measurement / question | Evidence |
|---|---|---|
| B25 | Consecutive action chains such as damage→damage, heal→defense, suppression→item use; actual effects and opponent responses in between | E/D |
| B26 | Buff/debuff overlap, stacking/replacement, refresh duration and expiration; flag unexpected persistence across round boundaries | E/D/N |
| B27 | Recovery/defense loops: consecutive turns without meaningful progress, zero-kill rounds, temperature oscillation and match length | E/D |
| B28 | A strong sequence with/without an available counter at the victim's decision point; report association, not a proven combo multiplier | N/D |

### 8. Minigames and readiness cost

| ID | Measurement / question | Evidence |
|---|---|---|
| B29 | Start, result, server acceptance, timeout, stale ticket and cancellation by minigame/item; modeled versus actual input explicitly marked | E/D/N |
| B30 | Time spent per attempt and opportunity cost before Ready; overlap with preparation and any skipped action | E/D/N |
| B31 | Success/failure/suppression branches: exact copies consumed, effects emitted and no duplicate application; keep distinct reasons | E/D/N |
| B32 | Observed payoff and time cost across modeled success rates; perceived difficulty and input comfort remain human-only | E/D/H |

### 9. Four-player targeting and score distribution

| ID | Measurement / question | Evidence |
|---|---|---|
| B33 | Directed source→target action/damage matrix; share of pressure on the coldest player and repeated victim streaks | E/D |
| B34 | Damage contributors versus awarded kill owner; last-hit concentration and deaths with no awarded score | E/D/N |
| B35 | Leader changes and score gaps each settled turn; early leader versus eventual winners, with joint wins separately identified | E/D |
| B36 | Chances for a behind player to act, select a legal counter and regain score before match end; no invented catch-up success for ties | E/D/N |

Current test policies all favor the coldest alive target. B33 describes that strategy, not the expected behavior of every human lobby. Retaliation, leader targeting and randomized targeting require a separately approved test-driver extension or later cohort.

### 10. Ghost participation

| ID | Measurement / question | Evidence |
|---|---|---|
| B37 | Death-to-ghost delay, alive/ghost time share and usable skill opportunities versus cooldown/unavailable periods | E/D/N/V |
| B38 | Grudge requests, acceptance, actual temperature loss and kills; one intervening cooldown turn and same-turn target damage limit | E/D |
| B39 | Once-per-match Possession state, affected actions, copies consumed without effects and expiry; round transitions must not restore the spent skill | E/D/N |
| B40 | Ghost contribution to leader changes and target pressure; time with no meaningful ghost action and perceived participation remain separate | E/D/N/H |

### 11. Round and terminal rules

| ID | Measurement / question | Evidence |
|---|---|---|
| B41 | Number of rounds, turns and awarded kills; first-three-kill versus round-cap ending and unfinished/timeout attempts | E/D/N |
| B42 | Highest-score winner set, joint winners and all-equal-score results; ghost score retention and departed-seat exclusion | E/D |
| B43 | At round boundaries: temperature, inventory, statuses, cooldowns and one-time-skill state follow their individual rules, not one blanket reset | E/D/N |
| B44 | Distribution of 4→3→2 alive-player intervals, duel-endgame duration and effects of the refill; 3-alive in Multi is not a separate three-player match cohort | E/D |

### 12. Environment and passive systems

| ID | Measurement / question | Evidence |
|---|---|---|
| B45 | Environment selection/exposure by mode, map, round and active duration; disabled/day-only entries in night matches are rule violations | E/D/N |
| B46 | Temperature contribution from items, fans, environment, recovery and ghosts kept separate, including attribution gaps | E/D/N |
| B47 | Temporal overlap of environment effects with minigames, lethal transitions, healing and round reset; interrupted effects classified | E/D/N |
| B48 | Environment camera behavior, sprite facing and visibility of decision UI during an event; distinguish functional effect from presentation | V/D |

### 13. Solo BT and automation bias

| ID | Measurement / question | Evidence |
|---|---|---|
| B49 | Real bot decision/action delay by item, preparation readiness and compliance with configured penalties; no invented bot minigame attempts | E/D/N |
| B50 | Bot action repetition, unused useful items and response to visible temperature/defense; BT decision reasons need explicit tracing | E/D/N |
| B51 | Invalid actions, retries, no-action turns and decisions invalidated before execution; separate BT limits from item-rule rejection | E/D/N |
| B52 | Human-driver versus real-bot behavior and result splits; candidate scoring and Ready/minigame policy biases retained in each match record | E/D |

### 14. Presentation and human readability

| ID | Measurement / question | Evidence |
|---|---|---|
| B53 | Item identity and action pose match the actual actor/target/effect; eating, fan attack and suppressed-use reactions remain distinct | V/E/D |
| B54 | Inventory-slot/character/UI overlap, floor clipping, camera-facing sprites and stable status icons at each occupied seat | V |
| B55 | Accepted input→visible feedback→impact→control-return latency; perceived responsiveness needs a human check | N/V/H |
| B56 | Can a viewer identify who acts, who is targeted, why an item failed and why the match ended? Readability/confusion notes require human confirmation | V/H |

### 15. Synchronization, performance and evidence integrity

| ID | Measurement / question | Evidence |
|---|---|---|
| B57 | Host/client agreement on stable action/round/terminal checkpoints, health, inventory copies, statuses, scores and winner set | E/D/N |
| B58 | Missing/duplicate command receipts, repeated effects/consumption, illegal targets and phase-crossing stale requests | E/D/N |
| B59 | Frame-time spikes, network response delay and observer/capture overhead; separate local multi-process machine contention from product behavior | N/D/V |
| B60 | Recorder closure, event sequence continuity, lost entries, timestamp quality, build/seed identity and incomplete cases; refuse unsupported metrics | E/D/N |

## Measurement definitions and interpretation

1. **Opportunity denominator:** an item opportunity is one `(match, round, turn, seat, ItemId)` decision snapshot where at least one usable physical copy AND legal target are available. Count duplicate physical copies separately only for inventory/consumption metrics. If the legal predicate is not captured, report held exposure and leave legal-choice rate unavailable. Client driver candidates are not automatically server-certified opportunities.
2. **Choice funnel:** legal opportunity → attempted selection → server acceptance → action executed → effect applied. Record conditional ratios at each boundary. A harmless utility action or valid defense need not change temperature to count as successful.
3. **Damage/heal efficiency:** sum realized magnitude / sum requested magnitude for matching signed effect kinds; report mitigation and clipping separately. Do not divide by zero or treat non-temperature utility as zero-value damage.
4. **Drop expectation:** for each actual random draw `d`, expected probability for item `i` is `weight(i,d) / sum(eligible weights,d)`, or zero when excluded. Expected count is the sum over eligible draws. Fixed grants, copy transfers and failed capacity checks are separate. Report context, draw count and deviations; dependent/stateful draws do not justify a naive independent-binomial pass/fail threshold.
5. **Timing boundaries:** use host monotonic time for authoritative events, each client monotonic time for its own UI intervals. Compare peers using action/checkpoint IDs; never subtract unsynchronized client clocks to claim network latency. Distinguish game-start, first controllable frame, final state commit and result-visible moments.
6. **Kill/terminal attribution:** a lethal temperature crossing is not necessarily an awarded kill. Read authoritative score events/state and final winner mask. Report outright wins, joint wins and losses separately; winner memberships can exceed total matches.
7. **Counterfactual limits:** an opponent owning a defense does not prove it could have avoided death. Legal timing/target/opportunity must be established; even then do not claim the unplayed alternate result. Item ownership versus win is correlation only.
8. **Scope of summaries:** split by mode, policy, item, seat, alive-player count and grant context when evidence exists. Each summary includes number of matches, events and distinct seats. Avoid pooled turn counts posing as independent match samples. Present median/range and individual matches for ten-match groups, not precise population claims or reliable extreme percentiles.
9. **Visual evidence:** the current recorder has a limited screenshot budget; it cannot prove B53–B56 for every action. Plan bounded event-triggered screenshots/short clips on unusual events and representative turns, preserve context and add explicit uncaptured status. Human comfort/fun remains unmeasured until reviewed.
10. **Missingness:** distinguish `not applicable`, `not observed`, `not instrumented`, `invalid evidence` and `measured zero`. Timeout/failure attempts are retained in an attempt table and excluded from successful-match denominators, with reasons. An observed invariant violation is still a bug even in an incomplete run.

## Evidence implementation priorities — deferred, no code changes

| Priority | Needed work before execution | Owner / relevant existing path |
|---|---|---|
| P0 | Preserve one stable match/action/copy identity across selection, effect, outcome and reset; evidence closure and build lock | `PlaytestTelemetry`, `PlaytestBalanceObservation`, `run_full_match_playtest.py` |
| P0 | Genuine random-draw context, eligible weights and grant outcome; never reevaluate mutable eligibility merely for logging or advance RNG for observation | `ItemDropTable`, `PlayerInventory`, grant/replacement callers |
| P0 | Legal-choice snapshot, stage/rejection reason and selection-to-execution lineage | `FullMatchPlaytestDriver`, existing server admission paths; read-only observation only |
| P1 | Phase/control/presentation intervals, passive-effect attribution, terminal reason and bot-delay provenance | `TurnManager`, combat presentation owners, Solo BT/coordinator and balance analyzer |
| P1 | Per-round/seat/item aggregation, funnels, conditional probability totals and coverage report | `analyze_balance_playtest.py`, a scoped campaign report layer |
| P2 | Bounded real-frame capture, frame-time/network instrumentation and human review prompts | Existing capture probes plus opt-in observation; no production scheduling changes |

Observers must be optional, authority-aware, bounded and non-mutating. Instrumentation may inspect already-computed results, but must not choose items, recalculate gameplay predicates with side effects, change target order, modify timing or submit extra commands. Verify observation-on/off determinism for the same controlled inputs before using a newly instrumented build. New fields/analyzers need focused malformed/missing/double-event tests before the 30-game cohort starts.

## Planned review outputs

- **Match sheet:** one row per attempt with completion state, mode/policies/seed, timing, rounds, kills, winner set and anomalies.
- **Item sheet:** opportunity/selection/use/effect funnel, realized value, waste reasons, supply/transfer context and missing denominators.
- **Probability sheet:** configured/conditional expected versus observed draw counts, eligible opportunities and sample size.
- **Turn timeline:** preparation → choice → acceptance → action → effect → death/ghost → refill → round/result, linked to actual frames where captured.
- **Coverage matrix:** B01–B60 × mode × relevant scenario; evidence path, implementation readiness and observation status kept separate.
- **Issue list:** distinguish confirmed rule/sync bugs, suspected balance skew, presentation defects, driver bias and insufficient evidence. Each entry carries reproduction IDs, impact, owner and recommended next check/change.

Zero tolerance applies to established invariants (duplicate consumption, illegal state writes, contradictory terminal results, forbidden active environment selection). Duration and item usage preferences are exploratory review targets, not automatic pass/fail rules. Do not invent acceptable win rates or nerf an item merely because it wins in a small sample. Additional rare-case fixtures, if later authorized, stay outside the agreed 30 natural matches.

## Execution and analysis gates (not executed)

- Freeze build/source fingerprints, exact case table, seeds and policy version before starting.
- Separate readiness/mini-game confounds; do not pool Solo, Duel and Multi item/win totals.
- Run one complete match at a time; save per-peer telemetry and incremental progress after each case.
- Count a primary match only after healthy recorder closure, terminal synchronization, visible result and lobby return. Retain failed attempts separately; never quietly replace them.
- On a new error or observation integrity failure, stop, diagnose and preserve evidence. Changed builds form a new cohort.
- Include genuine framebuffer checks for representative cases; hidden camera re-renders cannot qualify UI layering.
- Run sufficient independent drop samples if probability calibration needs more power; report those separately from the 30 complete games and do not call them human gameplay.
- Report item usage/effect totals with denominators and unobserved cases. Ten matches per mode are exploratory; neither a narrow win-rate estimate nor a final balance certificate.
- Recommend candidate changes with the responsible asset/logic owner, predicted effect and uncertainty. Keep baseline and any later A/B tuning runs separate. Do not alter values mid-cohort.

## Source mapping / resume point

- `Assets/Scripts/Core/Solo/FullMatchPlaytestDriver.cs`: existing policy scoring, target choice, modeled minigame outcomes, Ready timing.
- `.codex/scripts/run_full_match_playtest.py`: full-match runner, build fingerprints, terminal/closure checks.
- `Assets/Scripts/Core/Solo/PlaytestBalanceObservation.cs` and `.codex/scripts/analyze_balance_playtest.py`: current observation coverage and limitations.
- `Assets/Scripts/Core/Item/ItemDropTable.cs`, `Assets/Scripts/Core/Player/PlayerInventory.cs`: weighted eligible draws and grant/replacement contexts.
- `Assets/Data/GameModeRules/MultiRule.asset`: current 3-round/3-kill rules.

The later user request authorizes implementation and all 30 matches. Earlier stopped/planning paragraphs describe the prior state and are superseded by the execution record below. Interpretation and tuning remain deferred to Astra.

## Execution record — 2026-10-13

- New opt-in `balance_drop_draw` observation records original eligibility-pass results, weights, selected item, seat and grant/reroll context. It does not add RNG calls or extra predicate evaluations. Planned draws are explicitly not certified as committed grants.
- `balance_campaign_evidence.py` exports complete JSON references, descriptive tables, per-mode JSON and an Excel workbook after each completed attempt. B01–B60 evidence gaps remain explicit; no blanket coverage pass.
- `run_balance_30_campaign.py` freezes all 30 scenarios/build hashes and runs sequentially. Seat-seeded policy pairing is a sensitivity comparison; the real Solo BT remains unchanged.
- Preflight: existing weighted/filtered draw tests 25/25; balance Python tests 17/17; full-match Python tests 21/21; campaign/drop validation and empty-workbook reopening passed. Build `build_f2c3106d494a` succeeded with zero errors and four warnings.
- Cohort root: `output/balance30-20261013`. First match S01 started. Results and remaining observations are recorded in `Docs/Validation/balance30_20261013.md`.
- No balance tuning or Astra interpretation is performed during collection.

## Repeat authorization — 2026-10-13

The user requested a fresh ten matches per mode with all observation points retained. The first cohort is preserved with sixteen completed matches and an interrupted D07; absent processes mean its old running flag is stale. The repeat uses `output/balance30-20261013-repeat`, the unchanged runtime build/policies, +10000 seed offset and sequential Solo/Duel/Multi rotation. JSON retains all original raw/derived observation groups and B01–B60 coverage qualifications. See `Docs/Validation/balance30_repeat_20261013.md` for preflight and live-state boundaries.

## Additional authorization — 2026-10-13

The user requested ten more matches per mode. `output/balance30-20261013-additional` queues thirty additional cases after the repeat certifies completion. Unchanged build/policies and B01–B60 requirements are retained, with a +20000 seed offset. No concurrent gameplay campaign is started; prerequisite failure or fingerprint drift blocks the queue. See `Docs/Validation/balance30_additional_20261013.md`. The repeat and additional cohorts together plan twenty matches per mode; incomplete older evidence is separate.
