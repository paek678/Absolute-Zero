# Thirty-match repeat campaign — 2026-10-13

Status: running. First Solo match S01 passed (299.05 seconds active gameplay; 56 actions; 32 draws); Duel D01 is in progress. This is an interim checkpoint; live `progress.json` supersedes these counts. Scope: ten Solo vs Bot, ten Duel and ten four-player Multi matches.

The previous cohort stopped with sixteen completed matches (ten Solo, six Duel) and an incomplete D07. The runner and owned player processes were absent when checked. The cause is unknown. Preserve the original evidence under `output/balance30-20261013`; incomplete D07 is excluded from completed-match counts. This repeat is a new thirty-match cohort, not fourteen remaining games appended to the old cohort.

## Conditions

- Evidence root: `output/balance30-20261013-repeat`.
- Runtime build and gameplay source fingerprints match the previous cohort. Build `build_f2c3106d494a`; no gameplay balance changes.
- Original thirty policy lineups, Ready/minigame behavior and B01–B60 observation requirements retained.
- Policy/gameplay seeds receive an explicit +10000 offset. Existing within-cohort seed groupings retained; this remains a sensitivity comparison, not a matched causal experiment.
- Sequential Solo/Duel/Multi rotation exposes every mode early. Each mode still has ten full matches.
- Time scale 1; production Solo BT and production Multi 3-round/3-kill rules retained. No forced deaths, forced items or promotional item-choice overrides.
- Full and per-mode JSON, derived tables and coverage update after every completed attempt. This cohort uses JSON export; the original Excel workbook remains with the previous cohort.

## Checks and boundaries

Python balance tests 17/17 and full-match tests 21/21 passed. Preflight verified exactly ten cases per mode, thirty unique IDs, rotation order, all eleven JSON table groups and sixty coverage IDs. Unity CLI reported the correct project ready on port 7800. Successful matches require recorder closure, valid authority/state invariants, synchronized terminal state, visible result and lobby return. A failure retains evidence and stops the campaign.

Raw peer events preserve selection, effects, consumption, passive fan/recovery, minigames, draw eligibility/weights, environment, ghosts, death/round/result progression and anomalies. Existing optional fields remain available. Candidate scores do not certify legal-opportunity denominators; planned draws do not prove grants; hidden re-renders do not certify UI ordering; human experience and observer-performance overhead remain unqualified. Prior unidentified +10 ambulance impacts remain explicit attribution gaps, not silently credited to an item.

Live status: `progress.json` plus actual runner process. `completion.json` confirms all thirty passing records only after the campaign ends. A running flag alone is insufficient after process interruption.

## Final collection status

Stopped/incomplete: preserve the recorded matches and the current attempt; no thirty-match completion claim.

| Mode | Recorded | Passing |
|---|---:|---:|
| Solo | 2 | 2 |
| Duel | 2 | 2 |
| Multi | 2 | 1 |

See `final-status.json` and `progress.json` for the observed terminal status. Data interpretation remains a separate review.
