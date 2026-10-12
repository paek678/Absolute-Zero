# Multi duration trials — 2026-10-13

User request: play approximately ten matches with different round/kill limits, select and apply a setting that finishes within ten minutes.

**User revision during execution:** stop at five completed matches and apply the selected setting. Cases 07/08/05/06 completed; case03 continues as the fifth. Case04 is canceled and excluded; cases01/02 and the two planned extra confirmation matches will not run. All ten-match steps below describe the initial design and are superseded by this revision.

**Final explicit user decision:** maximum three completed rounds, first to three cumulative kills; at the end of round three the highest cumulative score wins, with joint victory for tied leaders. This supersedes the agent's provisional 2/2 recommendation. Apply 3/3 without additional complete matches beyond the five requested. The five observed matches do not certify the duration of 3/3.

## Contract

- Four-player Multi only. Preserve item values, temperature rules, preparation deadline, ghost rules, animation timing and timeScale=1.
- Existing KillScore win remains immediate at its committed presentation boundary.
- Previously unused MaxRounds now ends Multi after a fully completed round. Highest cumulative kill score wins; tied connected participants share victory using the existing winner mask. Ghosts remain eligible; departed seats do not. No new sudden-death or temperature tiebreak.
- Reuse the persistent terminal result, server authority, result UI and lobby cleanup. No arbitrary timer cancellation of an active action.
- Trial configuration uses an explicitly opted-in development-only immutable rule decorator, never mutation of shared SO assets. Every peer receives the same launch parameters, configuration is recorded. Default release reads MultiRule.asset.

## Tasks and gates

1. Implement completed-round cap and development trial options; focused winner/eligibility tests, full Editor tests, compile and build.
2. Eight natural Relay matches: (rounds,kills)=(3,3),(2,3),(2,2),(1,2), two seeds/policy lineups each. Two concurrent matches maximum. No forced deaths, accelerated clock or shortened animations. Record exact config, per-round time, turns, kills, ghost actions, winner mask and terminal consistency.
3. Choose a candidate using observed time, ghost opportunity and shared-victory frequency. Apply its production asset values through Editor serialization, rebuild, run two more matches without trial overrides (including cautious Ready timing). Total ten completed matches; failed attempts are retained and excluded from completed count.
4. Report all ten observations and gaps. Ten samples do not guarantee all human matches finish under ten minutes. If no candidate meets the target, report that rather than silently changing damage, timers or tie policy.

## Validation risks

Round count is not a hard wall-clock limit. A defensive round can exceed the target. Modeled participant choices/minigame outcomes differ from humans. Same-machine concurrency can inflate timing; monitor runtime logs/frame timing and retain executable fingerprints. No claims of statistically certified balance.

Canonical evidence/report: `Docs/Validation/multi_duration_trials_20261013.md`; generated evidence under `output/validation/multi-duration-20261013`.
