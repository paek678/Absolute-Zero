# Item ability audit — 2026-10-14

## Subsequent user-approved Hand Fan tuning

On 2026-10-14 the user explicitly changed Hand Fan damage to **6**. The shared `HandFan.asset` Damage field changed2→6, with all other item fields and its meta/GUID preserved. `GAME_DESIGN.md` now lists6. Solo/Duel compute actual damage6; a target20 becomes14, or16 with Windbreaker reduction2. Multi3/Multi4 outputs were independently checked across24 item-rule samples. The bot reads actual damage from this SO; its worked-example expected damage score was updated to5.4 (6−0.3×2). The approved baseline migration changed only Hand Fan24 entries, preserving all other456. A fresh Editor run passed **1343/1343** and the focused verification passed79 checks. Evidence: `output/handfan-six-20261014/result.json`.

The following audit results and Hand Fan2 references describe the **earlier frozen build** and remain as historical evidence. The prior 4-versus2 decision is superseded by the user's explicit6. Existing EXE/ZIP files were not rebuilt by this data-edit request and still contain2; rebuild the integration project before distributing damage6. A first test launch raced the owned Editor's normal exit and hit the project lock; that non-game failure was retained before the successful run.

The user requested an audit of every item for missing abilities and specifically asked whether the basic Windbreaker outfit works. This audit used `C:/Users/paek6/AbsoluteZero-SoloIntegration` and the existing frozen development Player from build job `196b213a54a64169bf8b14908b008bb0`. No gameplay code, item assets, balance values, build output or submission package was changed.

## Result

- Current Editor suite: **1343/1343 passed**, zero failed/skipped/inconclusive, new NUnit report `output/item-ability-audit-20261014/editor-tests.xml`.
- Independent expected-ability checks: **265 passed**. Actual item `ComputeEffect` and Multi3/Multi4 resolver outputs were compared with explicit expected damage, healing, defense, inventory disruption, fan speed and scheduled effects. Smartphone's three uses were checked as 4/6/7 healing. Temporary objects were destroyed and each item SO was verified unchanged. This is calculation evidence, not a live network session.
- Fresh actual Lobby/Relay cases: **10/10 passed**, synchronized checkpoints, all 31 owned Player processes exited normally, zero source/build drift. These are controlled fixtures with server-granted items and supplied mini-game outcomes.
- All **20 enabled items** were observed in live action/defense execution. The stable catalog has 21 entries; Tarot/reveal is intentionally disabled by `ItemAvailability` (D37-01).
- A separate two-peer native-rendered Windbreaker case passed. Both videos were fully decoded, capture errors and queue drops were zero, and sampled actual framebuffer images were inspected from attacker and defender perspectives.

Canonical machine-readable result: `output/item-ability-audit-20261014/item-review.json`. Per-item asset values: `asset-audit.json`. Live traces and unmodified case results: `runtime/` and `runtime-status.json`. Native outfit evidence: `windbreaker-visual/review.json` and its `*-defense-*.png` images.

## Ability coverage

| Item | Current ability verified |
|---|---|
| Fan | Opponent −3; matching Windbreaker reduces actual damage to1 |
| Windbreaker | Select as defense; tool/Temperature filter reduces incoming damage by2 |
| Warm Tea | Self +7 |
| Cat | Target random-item reroll; live selection plus real writer/RNG/copy-identity checks |
| Hand Fan | Opponent −2 in the current build; unresolved design-table discrepancy below |
| Ice Cream | Opponent −5, food filter |
| Iced Americano | Opponent −5, food filter |
| Water Gun | Opponent −8 after accepted mini-game result |
| Hug T-shirt | Equalize target to actor temperature; damage and healing directions checked |
| Hot Americano | Self +5 |
| Smartphone | Successive uses heal +4/+6/+7 |
| Hot Pack | Self +12, clamped at37 |
| Mask | Food defense, including preventing the scheduled Samgyetang debuff in matched rule cases |
| Samgyetang | Target +3 immediately; target −7 at next Attack start |
| Soda | Self −5 immediately; self +15 at next Attack start |
| Buldak Noodles | Self +20 at next Attack start |
| Screwdriver | Target fan speed set to2 |
| Claw Machine | Transfer a finite copy, preserving remaining uses and separate copy identity |
| Blue Tape | Target basic/Main-item block |
| Red Card | Neutralize the target's pending main action in the current turn |
| Tarot Card | Intentionally disabled; no drop or ordinary selection expected |

Defense order, repeated inventory mutations, successful selection, failed-mini-game consumption without effect, delayed source/target attribution, and counterpart synchronization are covered by the fresh suite and current Editor tests. Passing a frozen old-output corpus alone was not treated as proof that the design table is current.

## Windbreaker evidence

Both fresh two-peer and four-peer traces record requested Fan damage3, mitigated damage2 and temperature **37→36**. In four-player mode the normal initialization grants one Windbreaker use; Solo/Duel initializes it as reusable. The catalog-granted animation fixture tests the defense effect rather than the normal limited-use grant policy.

Native screenshots show the remote defender wearing the white jacket during its defense reaction and the local defender's first-person outfit gesture. It is a selected defense action for that turn, not automatic protection from simply owning the item. Food, sabotage and debuff filters are not generally blocked by Windbreaker.

## Findings and limits

1. **IA-HANDFAN-01 — Existing balance/design difference remains unresolved.** `GAME_DESIGN.md` says Hand Fan damage4, while the current SO/build uses2. This was already recorded as PLAN066 F066-14. Its ability executes correctly at2, so Windbreaker's reduction2 fully absorbs it. The audit did not retune it. The original explicit4 expectation failure is retained in `expected-rules-command-attempt04.json`; the current authorized2 was then checked separately.
2. **IA-TAROT-01 — Intentional omission.** The disabled Tarot/reveal entry is preserved for serialized/network compatibility. It is not a newly missing playable ability.
3. No newly demonstrated missing ability or critical item execution failure was found in the exercised paths. This does not certify every simultaneous item/target permutation or human puzzle input skill. Mini-game success/failure outcomes in the fixtures are supplied through the existing diagnostic flow; the nine configured UI routes were also inspected.
4. Solo and Duel share the two-player ability resolver. This new suite tested that live two-peer path; it did not run a separate new full Solo match for every item. Existing approved Solo runtime evidence is supporting context, not counted as a new game here.
5. Native visual inspection specifically covers Windbreaker. All-item live effect coverage does not claim frame-by-frame visual approval for all twenty animations.

## Actual live cases

`animation`2/4, `item-showcase`2/4, `item-delay`2/4, `inventory`2/4, `special`3, and `minigame-failure`4 all passed actual Relay verification. The additional native two-peer `animation` case also passed.

Preparation/export failures were preserved: two CLI startup503 responses; a temporary checker nullable-bool compile mistake; the explicit Hand Fan4 discrepancy; and a screenshot exporter that initially used an incorrect cue label (`item-impact` instead of actual `impact`). These did not change production code or invalidate the successful raw gameplay traces. The same finalized native videos were reused after correcting the export timeline grouping; no screenshot was generated from a guessed scene render.

No merge, commit, push, deployment, email or submission ZIP replacement was performed.
