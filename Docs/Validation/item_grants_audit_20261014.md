# Initial and temperature-threshold item grant audit

Date: 2026-10-14 (KST). Scope: read-only investigation of additional item grants after temperature drops. No gameplay source, assets, tuning, or running match was changed.

## Authoritative clarification and corrected verdict

The user explicitly confirmed that **four random items at game start are correct**. The requested check concerns later temperature-threshold rewards. The earlier IG01 classification and proposed change to zero initial random items are withdrawn. `OneVsOneRule.initialRandomItems=0` and older design text do not override this user decision. Preserve the current initial four-item behavior; no initial-grant fix is requested.

**Observed Solo bot threshold grants work:** the actual Editor log records successful additional grants at all three thresholds. This confirms the inspected match, not every possible inventory boundary or every game mode.

## Actual Solo evidence

Verified running project: `C:/Users/paek6/AbsoluteZero-SoloIntegration`, graphical Editor PID30360, mode Solo. Source log: `C:/Users/paek6/AppData/Local/Unity/Editor/Editor.log`. Seat1 is the bot; seat0 is the local human. These are observations from the user's ongoing match, not a newly launched controlled test.

| Bot reward threshold | Observed bot temperature at check | Requested | Actually granted |
|---|---:|---:|---:|
| 30 degrees or below | 28 | 1 | 1/1 |
| 20 degrees or below | 12 | 2 | 2/2 |
| 10 degrees or below | 10 | 3 | 3/3 |

The same log records initial four-item grants for both participants and repeats those grants at the next round. Both are preserved as current intended behavior.

Additional observed cases:

- Human seat0 reached 8 degrees while the 20- and 10-degree rewards were unclaimed. The same check granted 2/2 and 3/3 items. Multiple newly crossed thresholds can legitimately produce several items together.
- In the following round, human seat0 received 1/1 at 30 degrees and 2/2 at 20 degrees, but only 1/3 at 10 degrees. The grant loop stops when inventory slots are unavailable. Unfilled rewards are not queued for later: the threshold flag is marked before the grant attempt.
- Opponent presentation binds the actual replicated inventory. It does not independently create future temperature rewards.

## Timing, repeat protection, and mode comparison

`TemperatureCalculations` uses inclusive thresholds 30/20/10. `TemperatureSystem.CheckThresholds` grants each newly reached threshold once, and new-round reset clears the flags. Recovery followed by crossing an already claimed threshold does not award it again.

The production calls occur during preparation's one-second temperature checks, after fan/recovery operations. They are not synchronous with every attack-damage frame. A temperature drop during an attack can therefore be reflected by a later preparation check. This is the current implementation; no timing change was made.

| Mode | Initial random items | Additional rewards at 30/20/10 | Evidence in this audit |
|---|---:|---|---|
| Solo, human and bot | 4 | +1 / +2 / +3, once per round | Live state, actual logs, source |
| Duel, both participants | 4 | +1 / +2 / +3, once per round | Same production path as Solo; source inspection |
| Multi, living participants | 2 | +1 / +1 / +1, random capacity 4 | Source inspection; two survivors fill remaining random capacity to 4 |

No new Duel or Multi match was run for this audit. Existing integration test passes remain historical evidence with their original scope.

## IG02 - Separate random-capacity boundary still to validate

`PrepTurnOperations.ApplyDuelTick` calls `CheckThresholds` without the configured `MaxRandomItems`, leaving the default `int.MaxValue`. Its grant loop therefore enforces total inventory capacity 12, not the separate random-only capacity 8. After consuming basic items, freed total slots can permit more random copies than the configured random-only limit. Multi passes its random capacity 4 explicitly.

This is a source-level boundary finding, not a reproduced overflow in the inspected match and not evidence that the observed threshold rewards failed. A focused future check should consume basic items, approach the random-only limit, and test additional threshold grants while preserving initial random4. Legal theft/transfer capacity must be considered separately. No capacity or gameplay change was made in this audit.
