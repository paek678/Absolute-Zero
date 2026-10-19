# Immediate temperature-threshold rewards — 2026-10-14

## User decision and scope

The user reproduced missing/delayed bot-match rewards when both participants finish Prep quickly. They approved granting at the temperature decrease and explicitly chose to preserve fast turn progression. Keep the current 30/20/10 thresholds, per-mode reward counts, initial random4, capacity and once-per-round claims. No minimum Prep time, bot tuning, item-value change or distribution rewrite is part of this task.

## Cause and implementation

`TurnManager.PrepPhaseRoutine` checks rewards inside the one-second `TemperatureSystem.ConsumeTick` loop. Every new Prep resets the timer, and all-Ready can close the phase before a tick. Attack damage is therefore not rewarded until a later sufficiently long Prep; recovery can also hide a previously reached threshold.

Install a match-scoped `TemperatureThresholdRewards` observer after the authoritative participant/inventory initialization. Observe each seat's committed temperature decreases synchronously and reuse `TemperatureSystem.CheckThresholds` and its existing flags/cap. Require a live current server round, an authoritative inventory and a living positive-temperature target. Dispose every subscription with the TurnManager network lifetime. Existing tick checks remain an idempotent compatibility fallback; timing no longer depends on them.

Multi mutations are prepared/applied before temperature assignment. The observer grants separate copies without compaction or overwriting the selected usable slot, so pending selection/consumption and prepared plans retain their identities. Verify this with unit and actual network regressions.

## Gates

1. Focused immediate-damage, early Ready, repeated/recovered crossing, caps, client/stale-session exclusion, ghost/death, selected-copy consumption and lifetime checks.
2. Complete Editor regression and compilation.
3. Real Solo session: normal Advanced graph/menu, human RPC, fast Ready, real hit, immediate target claim, presentation ACK and normal lobby return. Initial temperatures are controlled; do not label this a natural-match sample.
4. Fresh development build and separate host/client Duel and host+3-client Multi inventory/replication regression; preserve the prior development player and the user's running release.

The validation ledger owns results and remaining gaps: [immediate rewards](../Validation/immediate_threshold_rewards_20261014.md). Source is synchronized into the open integration Editor for validation. Git publication and replacement of the user's EXE/ZIP are separate actions.
