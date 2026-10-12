# PLAN067 — Balance instrumentation evidence

## Current status

Instrumentation implemented; all five renewed normal games passed using the same executable. Runtime evidence is in `output/validation/plan067/runtime/report.json`; reanalysis and per-run metrics are in `balance-summary.json`. Two supplemental delayed-effect fixtures also passed. `output/validation/plan067/final-verification.json` records the final analyzer fingerprint and checks. This is not a completed 60-game balance study.

## Preserved baseline

PLAN066 old executable: 19 completed passes, zero completed failures. Run primary-020-p2 was intentionally interrupted for the user-requested instrumentation upgrade. Original partial files and `primary-v2/instrumentation-upgrade-stop.json` are retained. Do not count the interrupted attempt as a gameplay defect, pass, or completed game.

## Added observations

- Host-local action identity, source/target seat, item and physical CopyId; immediate and delayed temperature effects retain their source.
- Raw temperature request, realized change, defense mitigation, cap/overkill clipping. Self/opponent damage and healing are separate.
- Delayed schedule creation, due processing, seat/round/unavailable-target cancellation. Pending at terminal is exposed rather than assumed applied.
- Actual preparation opening and Ready acceptance for human drivers and production BT, authoritative order, queued selections, item consumption and inventory list changes.
- Per-item/per-seat metrics, passive fan/recovery totals, ghost damage, Multi death causes, round durations/kill-score gain, target pressure.
- Opt-in human-driver Ready override for controlled timing comparisons; no production BT or gameplay timer changes.

## Checks

- New Editor metrics tests 8/8; recorder/RNG tests 8/8.
- Existing combat/authority suites: Plan029 38/38, Plan034 28/28, Plan036 186/186, Plan037 652/652.
- Initial Plan037 failed 480 frozen output hashes because the new observational field was public. Made the field internal; did not update expected hashes. Recheck 652/652 proves the existing compared gameplay outputs remain unchanged.
- Existing Python evidence gates 21/21; new balance analyzer tests 17/17. The analyzer rejects mismatched physical-copy consumption/end identities, duplicate events, inverted damage signs and missing delayed origins. Blocked healing is separated from prevented damage; repeated possession snapshots do not inflate skill-use counts.
- Build job bb61b23f5e2745a4a4e772e52789f0e2: succeeded, zero errors, four warnings, fixed firewall-authorized executable.
- CLI briefly disconnected during normal assembly reload; retry succeeded. No forced recompile or security configuration changes.

## Renewed scenarios

All normal games use production rules, timeScale 1, actual Solo BT or Lobby/Relay. Human mini-game outcomes are modeled. New cohort is `balance-validation`, separate from the old primary cohort.

1. Solo attacker, seed 667101, minimum human Ready wait 1.25 seconds.
2. Same policy/seed, minimum human Ready wait 5 seconds. Scheduling and downstream actions can diverge; not exact replay or statistically powered causal proof.
3. Duel attacker versus survival.
4. Three players attacker/survival/utility.
5. Four players attacker/survival/resource/utility.

## Limits and pending gates

- Held-at-preparation counts are not legal-use opportunities. Inventory inserts include initialization/transfers, not only random draws.
- First order slots can include defenses/skipped actions; distinguish order allocation from successful attacks.
- Temperature zero crossings do not equal awarded kills; Multi authoritative death logs are separate.
- Item effectiveness is not causal win contribution. Five qualification games cannot certify balance or actual-human difficulty.
- Original PLAN066 remaining games and exact boundary coverage remain open. Old and new builds are not silently pooled.
- Normal hidden-run images are diagnostic re-renders; no renewed all-animation visual certification.
- Existing compute-buffer disposal warnings and all-animation visual inspection are not resolved by this telemetry task. Do not describe the targeted gameplay passes as a clean rendering/performance audit.

## First observed outcomes

- Solo minimum Ready wait 1.25 s: natural completion 222.2 s, human 0:2 bot. 45 linked actions/45 impacts, zero unlinked. Actual mean Ready 1.574 s human / 1.194 s bot; bot first-order slot 18/23. Human fan change -4 degrees; bot recovery +3 degrees.
- Solo minimum Ready wait 5 s: natural completion 149.1 s, human 0:2 bot. 19 actions/19 impacts, zero unlinked. Human mean Ready 5.045 s; human fan change -43 degrees; bot recovery +35 degrees. One failed mini-game spend is separate from effects.
- Both Solo games selected HeatWaveWarning and CicadaSong. HeatWave's low-temperature priority means first-order counts are not simply Ready-time wins. This pair demonstrates a readiness/temperature mechanism under the sampled trace, not a population win-rate estimate.
- Duel: natural completion 308.3 s, both peers passed final-state/lobby checks. 58 actions/45 temperature impacts, zero unlinked; 13 Windbreaker activations prevented 26 degrees. One failed mini-game spend recorded.
- Three players: natural completion 1282.0 s (21.37 minutes), final kill scores 5/4/2; all three peers passed. 206 actions/189 temperature observations, 205 explicitly linked consumptions and five mini-game failure spends. Seven rounds lasted approximately 157–223 s; no closed round had zero kill-score gain. Death origins: Item 10, Natural 2, GhostFrost 1. Ghost grudge produced 41 actual degrees across 14 hits.
- Three-player non-item observation: host event 7142 (round 7 turn 3, ResolutionPhase) has no item action and reports P1 31→37, request +10, clipping 4. The host log's `[ENV] Ambulance` and `ApplyHeal` entries confirm the environmental recovery. Retain this as one unlinked-to-item observation; do not assign it to Fan/item id 0 from the default identity. Raw observations now expose their full event/round/turn details for review.
- Three-player Ready means: 1.355/2.299/2.793 s; first action-order slots 72/9/0 across 81 allocated turns; passive fan totals -3/-87/-79 degrees. Strategies and seats differ, so these are sampled mechanism observations rather than causal seat advantage estimates.
- Three-player delayed sources: Samgyetang 1 scheduled/1 applied; Soda 3 scheduled/2 applied/1 cleared by round reset, with no dangling schedules. One Hot Pack use was suppressed. These are observed normal-game paths, not forced fixture substitutions.
- Four players: natural completion 1294.5 s (21.57 minutes), final kill scores 4/5/3/2; all four peers passed. 232 actions/210 temperature observations, zero unlinked-to-item effects; 223 explicitly linked consumptions and one mini-game failure spend. Five rounds lasted approximately 236–282 s, each with positive kill-score gain. Death origins: Item 10, Natural 1, GhostFrost 4. Ghost grudge: 39 hits/115 actual degrees; utility seat spent possession once. No scheduled effects remained pending.
- Five normal games total: 560 action records, 508 temperature-effect observations, 550 explicitly linked item consumptions, eight mini-game failure spends. The only non-item temperature observation is the audited Ambulance recovery above. All normal game gates and latest-analyzer gates passed; build hashes match across the five manifests.

## Additional targeted qualification

After normal games, existing `item-delay` fixtures at 2 and 4 players passed with opt-in telemetry over Relay. Each staged Soda, Buldak Noodles and Samgyetang and verified three actual delayed applications retained their originating item action, with zero unlinked observations or analyzer errors. Peer checkpoints agreed. They are forced fixtures, excluded from normal-game counts and balance win rates.

Final state: owned test players exited; connected Editor verified at this repository, `LobbyScene`, Edit Mode, not compiling and not dirty. No balance/BT values were changed, and no commit/push was performed.

## Commands and outputs

- `python .codex/scripts/run_plan067_balance.py <fixed-exe> output/validation/plan067/runtime`: frozen five-scenario cohort; stops on a new failure. Never overwrite an existing attempt.
- `python .codex/scripts/analyze_balance_playtest.py <completed-run>`: analyze final host JSONL after normal-match gates. CSVs: items, seat-items, seats, rounds.
- `python .codex/scripts/summarize_balance_playtests.py output/validation/plan067/runtime`: Korean HTML review and current JSON; reanalyzes completed raw evidence with the recorded analyzer SHA256.
- `python .codex/scripts/run_plan067_delayed.py <fixed-exe> output/validation/plan067`: waits for the five-game gate, then sequential delayed fixtures. No parallel match groups.

Review artifact: `output/validation/plan067/runtime/balance-review.html`. Original raw JSONL/manifest/game reports remain the primary evidence.

## Balance follow-up supported by these observations

1. Compare Ready delay while rotating the same policies through every seat and controlling environment effects. The observed temperature/initiative differences justify prioritizing this experiment; they do not yet justify a numerical nerf.
2. Agree on the intended Multi match duration before changing victory targets. The completed three-player game progressed and scored in every closed round, yet required approximately 21 minutes.
3. Collect actual use samples for held-but-unused items. Automated policy preference is not proof that an item is weak. The coverage CSV reports these gaps per run rather than fabricating use rates.
4. Keep the existing Hand Fan data/design mismatch and human mini-game timing/skill validation in the open balance backlog. This task does not change item values or certify real-player difficulty.
