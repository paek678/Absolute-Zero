# Possession: consume-only reaction

## Requested behavior and implementation

Reuse the existing food recipient motion when possession suppresses an item. The user's follow-up replaces the initially selected damage reaction. Consume the item normally, with no item effect.

- `CombatVFXManager` now triggers `feed` / `playerA_feed`, matching the recipient in `ContactItemChoreography.PlayFeedSequence`. It is a different clip from `damage` / `playerA_damage`; both are 0.3 seconds. No food sprite, damage flash, sound, contact movement, or temperature mutation is added.
- Attack/recovery suppression uses a 0.7-second reaction inside the existing action budget, then returns to idle.
- Suppressed defenses react together during the existing 0.5-second intro, returning to idle before attacks. One frame yields before cues so batch observers receive the result first. The shared server budget and action classification are unchanged.
- Existing execution cancellation restores poses. Existing possession status and short suppression cue remain.
- The local human has no third-person Animator. Their FPS item use remains idle; existing possession feedback remains. The arm reaction is visible from the other players' perspectives. The actual feeding sequence's FPS `use` plus food presentation is intentionally not invoked for a consume-only failure. No new animation assets were made.
- Authoritative consumption, possession expiry, and combat resolution were not modified.

## Initial damage-version evidence (superseded motion)

- Unity project verified: `C:/Users/paek6/Absolute Zero`, Unity 6000.3.11f1.
- Editor `Plan034GhostLedgerTests`: 12/12 passed (consume-only attack/defense/category rules and timing budget included).
- Final Windows Development build: succeeded, 0 errors, 4 warnings. `output/possession-motion-build-final.json`.
- Three fresh Relay runs, each Host plus three remote clients, all passed with synchronized checkpoints and no reported runtime errors:
  - `output/validation/possession-motion/attack-final/report.json`
  - `output/validation/possession-motion/defense-counter-final/report.json`
  - `output/validation/possession-motion/attack-force-final/report.json`
- Each run verified one-use consumption from 1 to 0 on all peers. Attack and forced-settlement runs reported unchanged temperatures. The counter-attack run verified the suppressed defense did not protect against the subsequent real attack.
- Development probe samples 0.12 seconds after suppression: three remote views confirmed `playerA_damage`; the actor's own view confirmed FPS had no held item. All 12 final observations passed.
- Inspected actual attack captures: `host.0007.possession-cue.png` shows raised arms and possession feedback; `host.0009.possession-settled.png` shows restored idle pose.

## Follow-up: food recipient motion verified

- Final build `output/possession-feed-build.json`: succeeded, 0 errors / 4 warnings.
- Repeated `Plan034GhostLedgerTests`: 12/12 passed; `output/possession-feed-tests.json`.
- Fresh four-peer Relay runs `output/validation/possession-motion/attack-feed/report.json` and `defense-counter-feed/report.json`: both passed, checkpoints synchronized and consume-only rules verified.
- All eight peer observations passed: six remote observations explicitly sampled `feed` / `playerA_feed`; two local FPS observations confirmed no held item use. Each peer observed the selected copy decrease from 1 to 0.
- Inspected `attack-feed/host.0007.possession-cue.png`: wide eyes and alternating raised/lowered arms match the feeding recipient, visibly different from the previous damage reaction.
- No new forced-settlement run for this clip-only replacement; the preceding run covers the unchanged cleanup code.

## Initial failure and correction

The first run (`attack/`) failed a probe assumption requiring the local human's hidden third-person rig to have an Animator. Remote views already played the correct clip. Corrected the check to distinguish FPS from remote views, skipped absent Animator triggering, and reran all final scenarios above successfully. The failed evidence is retained.

## Limits and final state

- Existing blue ghost placeholder is unrelated and unchanged.
- This is Multi-only possession presentation. No fresh 1v1/Solo run or new FPS reaction asset was required/created.
- No full disconnect/late-join matrix in this focused change; forced settlement was exercised.
- Editor left in clean LobbyScene, stopped, not compiling; test players exited. Recording remains stopped. No commit/push.
