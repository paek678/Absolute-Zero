# PLAN 066 — Boundary evidence ledger

This ledger tracks the originally planned 24 boundary variants. Supplemental fixture counts are not a substitute for these variants. Normal-match, fixture, Editor-unit and visual evidence are separate levels.

Evidence root: `output/validation/plan066/`. Current executable is the build recorded by the pilot manifests. All injected fixtures are excluded from primary win rates.

| Group | Variant | Current evidence / remaining qualification |
|---|---|---|
| Deadline | Just before the effective deadline | `Plan037ActionAttemptTests` verifies the existing item/preparation deadline and 0.5 s grace. Network timing at this exact boundary remains untested. |
| Deadline | Just after the effective deadline | Editor tests reject deadline + 0.000001 and NaN. Exact network arrival boundary remains untested. |
| Deadline | Cancel and submit the old ticket | Editor cancel/retry identity test passes. Queued replacement/round network fixtures cover stale identity, not exactly explicit user cancellation. |
| Identity | Duplicate request | `rpc-guards-recheck` passes all four peers; queued mini-ticket fixtures also send duplicate completion. |
| Identity | Invalid/dead target | `rpc-guards-recheck` and minigame-target/actor fixtures pass. |
| Identity | Stale CopyId after inventory change | Inventory/RPC fixtures pass; queued mini-ticket-replacement gives dedicated UI and server evidence. |
| Grudge | T+1 denial / T+2 recovery | Recorder invariants and normal 3p/4p preflights pass. Ghost ledger fixture supplies dedicated request/reply evidence. |
| Grudge | Two ghosts / same target / same turn | `qualification/ghost-contention` passes on actual Relay; request/reply ledger checked across all four peers. |
| Grudge | Lethal hit / score / terminal crossing | Four winner-seat ghost fixtures pass; normal preflight has two GhostFrost-attributed deaths. |
| Possession | Consume without immediate effect | `qualification/possession-attack` passes; multi-use Smartphone also passes as additional coverage. |
| Possession | Consume without delayed effect | Dedicated delayed-effect runtime qualification still needed. Smartphone multi-use alone does not prove a scheduled effect was suppressed. |
| Possession | Next-round reuse denied | `qualification/ghost-round-revival` passes; explicitly retains the spent bit and receives Unavailable after revival. |
| Top-up | Zero free slots | Variant 1 stages seat 0 with four random slots; grant and capacity assertions pass. |
| Top-up | One free slot | Variant 0 stages seat 0 with three random slots; grant and capacity assertions pass. |
| Top-up | Four free slots | Variant 1 stages seat 1 with zero random slots; grant and capacity assertions pass. Variant numbers are fixture selectors, not free-slot counts. Variant 5 additionally checks a pending mini-game and Ready selection. Delayed stale-command variants 6–9 pass over Relay in `lifecycle-extra-02`. |
| Special | Cat inventory mutation | Special/inventory fixtures pass; per-action normal-game attribution remains incomplete. |
| Special | Food against defense | Special fixture and combat unit tests pass; pin the actual item/defense pair before claiming this exact runtime variant. |
| Special | Simultaneous periodic deaths / co-victory | Joint/delayed fixtures pass; observer on/off repeats preserve the joint winner mask. |
| Departure | Non-host 4→3 | Disconnect preparation/attack fixtures pass. |
| Departure | Subsequent 3→2 | Existing lifecycle assertions cover termination; dedicated same-session chained departure evidence should be pinned before closure. |
| Departure | Host leaves | Host-exit 2p/4p fixtures pass. |
| Lifecycle | Duel same-process rematch | Duel-repeat fixture passes three rematches. |
| Lifecycle | Solo exit / re-enter | `lifecycle-extra-02/solo-transitions` passes all four peer reports: actual Solo/Relay mode transitions and re-entry, same process. |
| Transport | Delay/loss | `qualification/impaired-input` passes: loopback UTP proxy, 100 ms one-way delay and 2% loss; not Relay impairment. |

## Interpretation

- A passing unit test does not prove live input, animation, or network timing.
- Pilot/primary summaries compare a settled shared terminal projection; continuous telemetry is not an atomic snapshot and cannot certify every transient frame.
- Primary human mini-game outcomes are modeled; these runs do not certify pointer/touch usability.
- Screenshots from hidden normal runs are diagnostic camera re-renders. Final framebuffer qualification is tracked separately because overlay sorting can differ.
- Pending cases are retained as coverage debt, even if the supplemental regression runner reports 24 passing fixture types.
