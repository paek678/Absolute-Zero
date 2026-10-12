# Overall logic remediation plan

Date: 2026-10-12. Status: **implemented and validated 2026-10-13; T01–T04 approved within the documented scope**.
Baseline: [overall review](../Validation/overall_logic_review_20261012.md).

## Scope and source of truth

- GAME_DESIGN's weighted drop rule limits identical generated items to three copies. PLAN071 requires independent slots and CopyIds; its explicit three-copy mention concerns deathmatch top-up. Apply the limit to generated random copies without interpreting it as authorization to cancel an existing steal/transfer.
- Preserve each item's MaxUses, fixed inventory capacity, mode-specific random capacity, acquisition ordering, slot anchors, server authority, target rules, ghost cooldown/one-use policy, existing map restrictions and camera presentation.
- Preserve the Korean lobby caption `1 대 1 대결`.
- The user authorized implementation on 2026-10-13 if the plan passes review; proceed task by task within these boundaries.

## Task order and gates

Each task follows: reproduce and save baseline → implement scoped correction → focused tests → repair any new regression → rerun the affected tests. Do not count skipped, interrupted or obsolete-fixture failures as passes. Update the review's OVR IDs with fresh evidence rather than erasing old failures.

### T01 — Restore trustworthy regression expectations (OVR-02 / OVR-03)

Owners: `Plan050NicknameRoomTests`, `MatrixScenarioProbe.TemperaturePolicies`.

1. Update the expected duel room caption to the approved Korean text; retain row bounds and actual nickname checks for 2/3/4 participants and both layout configurations.
2. Replace the old single-stack threshold assertion with physical-copy invariants: independent nonzero CopyIds, exact per-copy MaxUses, expected grant count within capacity and cap, and no mutation of existing copies.
3. Use a sufficiently varied finite drop pool for the 1+2+3 duel threshold grant test. A single-item pool conflicts with the three-copy cap after the third copy. Keep a separate deliberately exhausted/capped pool test.
4. Retain threshold inclusivity, once-per-round flags, no repeated grant, reset behavior, Kids removal, Ambulance eligibility/ties and recovery/fan assertions. A failure early in a fixture must not be reported as coverage of later assertions.

Gate: focused Editor tests and all three temperature-env runtime fixtures pass without reinstating stacking or weakening assertions. Full Editor baseline is then green apart from independently documented new regressions. This task does not establish OVR-01 as repaired.

### T02 — One eligibility policy for generated copies (OVR-01)

Owners: `PlayerInventory`, `SeatInventoryMutator` prepared Multi reroll, existing `ItemDropTable.Roll(predicate)` and inventory calculation tests.

Implementation:

- Extract a small, pure generation-eligibility calculation; do not introduce a new inventory manager or a broad framework. Both live and staged inventory paths provide their copy count to the same rule.
- Ordinary initial/threshold random grants: exclude disabled/unregistered candidates, existing unlimited copies and finite items already at three copies. Keep free-slot/capacity checks before drawing.
- Use the existing filtered weighted draw over eligible items, preserving relative configured weights. Do not draw up to twenty disallowed candidates and silently lose a grant while a valid candidate exists. If no candidate is eligible, return no grant without an infinite retry loop or RNG draw.
- Preserve separate slots/CopyIds on each acquisition. Count nonempty physical copies; never combine uses or replace a surviving item to make space.
- Bring direct fill and staged deathmatch top-up onto the same eligibility calculation while retaining their all-or-nothing commit/revalidation contracts. Count pending staged copies as well as committed ones.
- For random reroll, evaluate the inventory excluding the slot being replaced; count earlier staged replacements. Keep the old item if no replacement is eligible. Preserve existing copy invalidation/queue rules on a committed replacement.
- Do not place a blanket cap inside `GrantSpecificItem` or `TryReceiveSeparateCopy`: direct setup/transfer paths have distinct contracts. Stealing keeps remaining uses and capacity checks, and must not remove a victim's item if destination admission fails. Any proposed change to the allowed result of stealing needs a separate design decision.
- An already over-cap inventory is not truncated. Stop further generated copies until consumption/removal drops the count below the cap.

Tests:

| Stimulus | Expected |
|---|---|
| 0/1/2 copies, space available | Eligible; third copy receives a new ID and slot |
| 3 or more copies | Candidate excluded from generated drops |
| Dominant capped item plus one legal item | Legal candidate selected; no artificial failed grant |
| Every candidate capped or disabled | Clean no-grant; no random draw/retry loop |
| Full inventory / reusable empty slot | No overwrite; hole reused only when capacity allows |
| Batch grants and staged top-up | Newly staged copies count immediately; never create copy four |
| Consume one of three then grant | Existing copies keep uses/IDs/anchors; replacement uses free anchor |
| Reroll at cap / no eligible replacement | Correct effective count; old item preserved if necessary |
| Steal into full destination | Both inventories unchanged |
| Same state replicated to peers | Copy IDs, uses, order and inventory fingerprint agree |

Gate: original three-copy reproducer no longer selects a fourth generated copy, new boundary tests pass, and Solo/1v1/3p/4p inventory scenarios pass. Exercise real Relay host + three clients; compare IDs/uses and capture stable refill positions. Expected seeded results may change after eligibility filtering; update only expectations justified by this explicit rule change, not broad frozen golden outputs.

### T03 — Correct shutdown resource ownership (OVR-04)

Historical evidence: [PLAN034 extended validation, 2026-09-27](../Validation/PLAN_034_results.md). Three observe-only runs warned once each; three shutdown-release runs warned zero times. The observed 2D Animation 13.0.5 fallback buffer was a fixed 4,096 bytes. Current manifest still uses 13.0.5. The latest review independently re-observed the warning but did not capture its allocation stack or growth profile.

1. Reuse the existing `ResourceLifetimeProbe` and retained patch proposal to reproduce the A/B result on the current build. Record buffer identity/size, startup/scene-transition/quit timing and other warnings. Do not infer an accumulating leak merely from a quit warning.
2. Preferred bounded repair, if reproduced: retain version 13.0.5 and make its own fallback-buffer lifecycle release idempotent at application shutdown, with paired event subscription/removal. Review whether the existing cleanup already covers domain reload and recreation.
3. A persistent package fix requires a version-controlled embedded/local package, not edits in `Library/PackageCache`. Prepare the exact package/source diff and rollback before changing dependency layout. Do not silently upgrade Unity or other packages. Reassess if the current source differs from the retained proposal.
4. Avoid production reflection to dispose arbitrary buffers, blanket scene-unload cleanup or warning filtering. These can free graphics data while sprites still need it.

Gate: repeat the same A/B scenarios and at least five enter/leave/reenter cycles for each mode; inspect character/ghost deformation, custom sprites and scene teardown. No buffer-double-release/use-after-release errors, no targeted quit warning, and no persistent growth attributable to the repaired buffer. Also test Editor Stop/reenter. If the owner cannot be safely repaired, leave OVR-04 explicitly open with the evidence; do not declare it fixed through log suppression.

### T04 — Integrated validation and handoff

- Unity auto-compilation and development build pass; classify every remaining warning.
- Rerun the full Editor suite (baseline 1,169, plus new tests) and Python harness tests (baseline 46). Require zero unexplained failures; raw test count alone is not the gate.
- Rerun the 23 local matrix cases and the Relay ghost/cosmetic plus four menu cases from the review, including the three repaired temperature-env scenarios.
- Run complete natural Solo and Relay 1v1 matches through result and lobby return. Add one complete natural Relay four-player match including death → ghost → skills → next round → match end → lobby return. Human driver minigame outcomes remain modeled and must be labeled.
- Retain a finite four-player budget (initially 30 minutes at timeScale 1). If progress continues at the budget boundary, classify it as an incomplete/long-match observation, not a pass, draw or proven deadlock; preserve the trace for follow-up.
- Check minigame failure consumes without applying effects; possession consumes without effects; Grudge cooldown and per-target limit; 4→3 continuation; 3→2 and host-exit return; nickname/cosmetic agreement; generated copy limit and stable slot positions.
- Spot-check actual renders of duplicate acquisition/refill, temperature rewards, ghost skill use and room list. Repeat the character-only camera policy check if touched by the implementation.
- Save reports, screenshots, build identity and unresolved gates. No new promotional recordings or 50-match balance campaign are included.

Final acceptance: T01/T02/T04 pass with no new unexplained gameplay failures; T03 is either independently verified fixed or explicitly listed as an unresolved resource gate. Only when all four tasks pass may the remediation be called fully complete.

## Remaining validation, not proven game defects

Separate-machine/network-impairment coverage, broad human minigame input/usability, all-item visual/audio review, long-term performance profiling and statistically meaningful balance data remain distinct work. One additional full four-player game closes a flow gap, not those broader claims.

## Plan self-review

Implementation evidence and remaining coverage limits: [2026-10-13 task results](../Validation/overall_logic_remediation_20261013.md). T01–T04 passed, including six-round natural Relay four-player completion, result and lobby return. Existing non-blocking warnings and broader coverage limits remain documented.

2026-10-13 bounded review score: 95/100 (design/behavior preservation 24/25, authority/identity 24/25, edge cases 19/20, validation/recovery 19/20, change scope 9/10). This assesses plan suitability, not defect-free runtime behavior. No blocking design change was identified. Rechecked reroll effective counts, zero-eligible fallback and package idempotent ownership; those contracts above are mandatory. Remaining uncertainty is actual execution and graphics lifecycle evidence. T01 proceeds first.

- Prevents changing production behavior just to satisfy obsolete tests.
- Keeps shared writes with the server and includes committed + staged copy accounting.
- Avoids accidentally changing steal/transfer gameplay under the random-drop cap fix.
- Defines zero-eligible, full inventory, over-cap legacy state and reroll fallback behavior.
- Reuses historical resource evidence and requires a scoped persistent repair with rollback.
- Separates implementation gates, runtime coverage and balance claims; no guarantee of zero bugs.
