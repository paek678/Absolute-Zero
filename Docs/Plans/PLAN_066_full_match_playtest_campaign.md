# PLAN_066 — Full-match playtest and balance evidence campaign

Status: implementation authorized, 2026-10-11. The user authorized proceeding with anomaly recording and final validation after the plan review. Development proceeds through the gates below. Existing fixture passes and instrumentation preflights are not primary-campaign evidence. See [current validation ledger](../Validation/PLAN_066_results.md).

## 1. Scope and completion definition

Goal: investigate gameplay defects and balance warning signs through at least 50 complete games. Proposed primary campaign: **60 completed normal matches**, plus **24 boundary scenarios** reported separately.

A normal match begins through the normal lobby/Solo launch path and ends with the authoritative terminal result and its visible presentation on all required peers. A round, screenshot, forced death, probe PASS, or successful process exit is not a completed match. Return to lobby/rematch is a separately recorded postcondition. Multi winner rules include co-victory; do not require a unique winner.

No production balance values, user BT assets, sprites, maps, menus, or gameplay rules change during this investigation. Findings produce reproduction evidence and proposed remedies, not automatic tuning. Existing dirty work is retained and identified by a source/asset manifest, not merely a Git commit.

## 2. Verified current foundations and gaps

| Owner | Observed foundation | Gap / implication |
|---|---|---|
| `.codex/scripts/run_validation_matrix.py` | Multi-process cases, Relay/loopback, screenshots, peer reports; loopback proxy can impair networking | Bounded scenarios, not an unbiased complete-match campaign. Relay and local proxy are distinct transport modes; do not call proxy runs impaired Relay |
| `Core/Solo/MatrixScenarioProbe.cs` | Actual player RPCs and result snapshots; many focused edge fixtures | Direct temperature writes, inventory fixtures, forced ghosts and forced finishing are present. Reuse as boundary tests, exclude from balance cohort |
| `VisualFourPlayerProbe.NaturalMatch.cs` | Multi ghost-score/end-flow validation | Despite its name, calls `DebugForceGhostForVisual` and sets targets to 2 degrees. Not natural-match balance evidence |
| `Core/Solo/ProbeInventoryCommands.cs` | Selection/Ready routed through owner RPCs with CopyId, epochs and grant transaction | Useful adapter; needs accepted/rejected/settled correlation, other action types and complete-match scheduling |
| `BotTurnController`, `BotDecisionModel`, `TrustedBotCommandAdapter` | Production Solo BT decision flow, seeded policy noise, delayed bot operations, live-key guards | Preserve this real bot. Do not substitute legacy `BotBrain` or `SoloDevelopmentDriver` and call it Solo validation |
| `Core/Turn/TurnResultOperations.cs` | Human-readable temperature/round summaries | Not enough item attribution, action opportunity denominators, rejection reasons or machine-readable match outcomes |
| `TurnManager`, combat result events, `CombatVFXManager.OnPresentationSettled` | Phase/result/presentation observability | Presentation callbacks are not authoritative commits; logging them alone misses immediate/periodic/ghost changes |
| `ItemDropTable`, `EnvironmentRuleService` | Gameplay draws currently use Unity's global RNG at inspected sites | A policy seed is not a guaranteed match replay seed. Inventory/environment and all other authoritative RNG must be mapped before promising deterministic paired replay |
| `GameplayVideoProbe` | Optional framebuffer recording, bounded frame queue and drop count | Use for selected investigations, not all peers for all games; recording overhead must be measured |

No new gameplay bug or balance defect is established by this static audit. The confirmed issue is that existing test evidence is insufficient for a 50-game balance conclusion.

## 3. Rule contract and sources

Source precedence: current user decisions, executable resolved rules/assets, then dated planning documents. Store resolved runtime values in every match manifest.

| Requirement | Verified owner / evidence | Check |
|---|---|---|
| Duel round/match ending | `GAME_DESIGN.md` Win Conditions; `OneVsOneRule.asset` has winsRequired=2 | Round death, draw with no score change, first to two wins, rematch reset |
| Multi ending | `MultiRule.asset` has killsToWin=5; design allows co-victory within one committed group | Score attribution, terminal latch, remaining actions canceled; first elimination is not match completion |
| Mode setup | `MatchConfig`, `GameModeRuleSnapshot`, Solo resolved configuration | Capture live scene, rule snapshot, day/night map and bot settings; do not infer Solo settings from legacy bot names |
| Grudge | `GhostSkillService`, `GhostSkillLedger`: damage 3, next legal turn T+2, lethal allowed, target damage mask | T usable, T+1 rejected, T+2 usable; one ghost damage application per living target per turn |
| Possession | `GhostSkillLedger`: spent mask cleared at BeginMatch, retained at BeginRound | One use per match; selected consumable is spent but effects suppressed for that turn; reset only in a new match |
| Two survivors | `DeathmatchGrantCoordinator` and current user decision | Fill only free random slots, no duplicate transaction, no stale selection after grant |
| Departure | `MatchDepartureController.MustEnd` and user decision | Non-host 4→3 continues with stable seats, 3→2 / Duel departure returns all, host departure returns all |
| Presentation | Current PLAN061–065 code and user decisions | Fixed anchors, billboard artwork vs horizontal slot art, death hide at break then ghost after ~0.5 s, Multi environment pan |

Configuration watchpoints, not confirmed bugs: raw `OneVsOneRule.asset` still has `isTarotAllowed=1` despite the user's no-tarot decision; raw `MultiRule.asset` has reconnect/grace settings despite newer departure behavior. Trace actual catalog filtering and effective session policy in T00; report stale flags separately from active behavior. Do not enable tarot/reconnect to satisfy stale assets/docs. Old ghost terminology/numbers in `GAME_DESIGN.md` do not override the user's 3-degree Grudge decision.

## 4. Expected player flows

Common normal path: lobby/name/appearance → mode/room → all participants ready to load → normal deal/environment → Prep input → select/mini-game or bot delay → Ready/deadline → server resolution and presentation → repeat → round or match result → rematch or lobby.

| Flow | Behavior | Expected evidence |
|---|---|---|
| F01 cautious newcomer | Deliberate selection, occasional cancel, modeled mini-game failures, late Ready | No stranded selection; failed attempt follows real item consumption rules; measurable decision time |
| F02 fast attacker | Quick legal damage item, early Ready, target currently vulnerable opponent | FirstReady vs actual action order and resource cost; cannot bypass mini-game/server timing |
| F03 survival player | Heal/defend while low, attack otherwise | Effective healing vs overheal, blocked damage, long-match risk |
| F04 resource saver | Use renewable/basic resources, save stronger items for finishing | Unused-stock and missed lethal opportunities; no infinite free-use behavior |
| F05 utility player | Cat/inventory changes, fan adjustments and scheduled effects when legal | CopyId continuity, delayed cause attribution, useful vs wasted actions |
| F06 reactive finisher | Select legal low-temperature target using visible information | Target switches, kill attribution and overkill; no hidden queued-choice access |
| F07 Multi mixed personalities | Different policies sit around the table | Seat/perspective invariance, targeting concentration, visible UI identity |
| F08 Multi focus pressure | Prefer the visible score leader or weakest living target under a declared policy | Targeted pressure vs intrinsic item strength; no hidden team messaging |
| F09 death to ghost | Die through ordinary play, use Grudge/possession when eligible | Death timing, cooldown, one-use ledger, impact on survivor outcomes |
| F10 two-survivor transition | Continue normal play after two deaths | Top-up only once, only free capacity, still-valid item interactions and terminal outcome |
| F11 deadline/absence | Wait, cancel, fail mini-game, submit near boundary, or intentionally do nothing | Authoritative deadline governs; no late success, stuck Prep or illegal Ready rollback |
| F12 departure and reuse | Leave/cut connection at specified point, or finish then rematch | Correct return/continuation policy and no stale state crossing session/round generations |

F09/F10 are observed naturally in the primary cohort. If rare, add targeted fixtures to the boundary suite; never force them during primary games to achieve coverage.

## 5. Primary 60-game matrix

| Mode | Construction | Games |
|---|---|---:|
| Solo vs real production BT | Four human-side policies (attacker, survival, resource, cautious) × four seeds; lock currently selected real bot configuration | 16 |
| Online Duel | Four policy pairings × two seeds × swapped seat/policy assignments | 16 |
| Three-player Multi | Two lineups (all-balanced; attacker/survival/utility) × two seeds × three cyclic seat assignments | 12 |
| Four-player Multi | Two lineups (all-balanced; attacker/survival/resource/utility) × two seeds × four cyclic seat assignments | 16 |
| **Total** | 16 local Solo + 44 real Lobby/Relay matches | **60** |

Duel pairings: balanced/balanced, attacker/survival, resource/utility, cautious/balanced. Rotate who occupies the host seat, and record first-ready/initiative separately: host identity, seat and initiative are not interchangeable. Policy and mini-game performance are separate factors; primary comparisons use a common modeled skill level except the explicitly labeled cautious cohort.

Use predeclared blocks and retain every attempt. Process restarts/reproductions do not count as new independent games. A timeout/crash/desync is a failed or censored attempt, never a draw or hidden exclusion. Target 60 completed games; if failures require replacement, add complete counterbalanced blocks and keep originals in reliability/attrition reports. Stop for review at 90 attempts or a repeated blocker instead of silently generating endless replacement games. Report fewer completions if blocked.

This is a screening sample, not evidence of final balance. Split by mode and policy. The all-balanced symmetric cohort can check directional seat bias against 1/N; mixed-policy games and Solo have no assumed 50% target. Co-winners, exposure denominators and small independent seed counts must be explicit.

## 6. Boundary suite: 24 cases, excluded from normal-game win rates

Eight groups × three manifest variants each. Reuse proven Matrix fixtures where possible; retain explicit fixture labels.

1. Deadline/mini-game: just before deadline, just after, cancel then resubmit old ticket.
2. Input identity: duplicate request, invalid/dead target, stale CopyId after inventory compaction.
3. Grudge: T+1 rejection/T+2 recovery, two ghosts on same target, lethal score/terminal crossing.
4. Possession: consume without immediate effect, consume without delayed effect, reuse after next round rejected.
5. Top-up: zero/one/four free slots, verify exactly the allowed grant and stale-command handling in each variant.
6. Special effects: Cat inventory mutation, food vs defense, simultaneous periodic deaths/co-victory. Pin concrete item IDs from current catalog in T00.
7. Departure: non-host 4→3 continue, subsequent 3→2 return, host departure return. Vary triggering phase in the manifest; additional phase combinations are coverage debt, not implied by three cases.
8. Lifecycle/transport: Duel same-process rematch, Solo exit/re-enter, loopback impaired-network run (delay/loss preset). Name that run loopback, not Relay.

Add reproductions for actual findings outside these 24. Do not claim this sparse matrix covers every item combination, phase or disconnect timing.

## 7. Data that must be recorded

Local output only: `output/playtests/<campaign>/<run>/<peer>/`. Never log authentication tokens, Relay join codes or real account IDs. Use campaign-local peer/participant aliases and stable seat/generation mappings.

### Manifest and event envelope

- Manifest: schema/campaign/run/attempt/scenario/block IDs; commit plus dirty source/asset hashes; executable hash; Unity/package versions; mode/transport/map/rule snapshot; item catalog IDs/GUID/hash and drop weights; bot graph/settings hash; policy version; RNG seeds/stream identifiers; timeScale=1; resolution/frame cap; recording settings; planned injection flags; completion/termination reason.
- Envelope: schemaVersion, runId, peerAlias, origin(server/client), monotonic local event index, server result sequence where available, session generation, match/round epochs, turn/phase, local monotonic time and sampled server time, logical actor/target seats, request/operation/transaction/attempt IDs.
- Never correlate actions by array slot or nickname alone. Preserve ItemId + CopyId + inventory revision and snapshot epoch.

### Event families

| Family | Required fields |
|---|---|
| Session/phase | Join/load/ready/leave/result/rematch, expected participants, deadlines and phase transitions |
| Policy decision | Visible observation version, legal candidates, choice/reason, expected think/ready delay, own policy RNG draw |
| Command lifecycle | Intent sent, authority receipt, accept/reject reason, actual commit/cancel/expiry; distinguish expected rejection tests from unexpected failures |
| Mini-game/bot use | Ticket/copy identity, start/deadline, modeled or real input origin, success/failure/cancel, observed time, bot delay start/end |
| Inventory | Grant/spend/remove/swap/reorder, before/after copies and uses, mutation cause, transaction and display anchor assignment |
| Effect | Source actor/item/copy/committed action, effective damage/heal/defense, overkill/overheal, immediate/scheduled/environment/ghost cause, due/applied turn |
| Death/ghost/score | Killer/victim, cause, ghost availability/spent/cooldown, possession effect suppression, death and score transaction |
| Synchronization | Comparable replicated-state hash with exact sequence/version, roster/life/temperature/score/inventory/grant ledger; host/client arrival and settlement timestamps |
| Presentation | Result received/start/impact/end/ack, selected target visual slot, arrow/UI visibility, expected vs actual anchor, camera return, body hidden/ghost revealed |
| Diagnostics | Exceptions, warnings classified, timeout/progress heartbeat, frame-time sample, queue high-water mark, write errors, event loss, screenshot/video path |

Files: `manifest.json`, append-only `events.jsonl`, `summary.json`, aggregate `matches.csv`, `item_metrics.csv`, `findings.json`, screenshot index and optional videos. Every summary total must reconcile to its raw events. Duplicate network deliveries are recorded but not double-counted as commits.

### Recorder design

Proposed new components, not existing APIs:

Activation requires an explicit development-run flag (for example `--az-playtest`) and a validated output/manifest path. Automation and fault injection remain under `UNITY_EDITOR || DEVELOPMENT_BUILD`; no automatic activation in normal/release sessions. Only scoped recorder hooks may be inert no-ops outside the opted-in campaign. Reuse the existing fixed firewall-authorized executable path.

- `PlaytestEventRecord` / `PlaytestManifest`: plain immutable DTOs with versioned schema.
- `MatchTraceRecorder`: scene/session-scoped observer; existing subscriptions first, narrow authority-side hooks where accept/reject/commit reasons are otherwise lost. No state mutation or second phase driver.
- `JsonlPlaytestSink`: bounded queue of copied DTOs; main thread reads Unity state, background writer handles only data/file IO. Batch flush plus terminal flush, `.partial` recovery and explicit dropped-event accounting. Logger IO failure must not alter gameplay; it invalidates telemetry completeness and is reported.
- `PeerStateObserver`: snapshot after the same committed sequence and relevant inventory/grant revisions. Compare public/shared projection, not hidden per-peer fields; wait for propagation/ack window before classifying desync. No same-frame wall-clock equality assumption.
- `PlaytestEvidenceCapture`: screenshots at critical lifecycle points and anomalies; representative short videos. Measure capture overhead against an unrecorded pilot. No synchronous full-video encoding on the gameplay thread.

Hook map: `PlayerState.SelectItemServerRpc` / `SubmitMiniGameResultServerRpc` / `PressReadyServerRpc` for request verdicts; `BotTurnController` / `TrustedBotCommandAdapter` for real bot use; `TurnManager`, `MultiAttackOperation`, combat applicators and periodic effect owners for committed effects; `GhostSkillService` and `DeathmatchGrantCoordinator` for ledger/transaction changes; `MatchNetworkState` / inventory read model for versions; presentation settled/death callbacks for visuals. The observer must cover Duel and Multi separately; one client combat event does not cover every authoritative mutation.

## 8. Automated player design and reproducibility

Proposed `FullMatchPlaytestDriver` controls only its own human-designated peer via existing public command/UI paths. A `TestPolicy` strategy chooses among legal observations; typed command adapters translate policy intent into current client commands. Retain the real `BotTurnController` for the Solo opponent.

- Test policy cannot see another player's hidden selected action, unrevealed random outcome, or recorder-only server data. Freeze permitted public information in T00. Main comparison can use the narrower own/public observation used by the real bot.
- Wait actual deadlines and item delays. Do not write temperatures, scores, life states, inventories or phases. Do not call DebugForce helpers, shorten turns or increase timeScale in primary games.
- Modeled human mini-game outcome profiles are declared test assumptions, not measured human skill. Their reports test server acceptance/consumption and conditional balance; they do not prove the mini-game UI is playable. Separate representative pointer/UI flow and screenshot checks from direct command driving.
- Policy seeds and RNG streams are separate from gameplay. Inventory/environment RNG requires an audited narrow optional source at existing draw boundaries, with unchanged production default and independent test streams; do not repeatedly reset Unity global Random during play. Map all authoritative draw sites before declaring a run reproducible.
- Record actual draws and accepted action schedule. A seeded online rerun may still differ due to scheduling. Classify reproduction as exact trace match, same trigger reproduced, or divergence; no exact-replay claim from seed equality alone.
- Watchdog reports phase/no-progress/overall timeout with last valid events. It never forces a win to make a run finish. Thresholds are pilot-calibrated and published before the main campaign.

## 9. Bug checks and balance interpretation

### Functional invariants

Authority changes only through valid commands; one spend/effect per committed use; legal temperature bounds; no stale-round ticket accepted; fixed CopyId ownership; Ready/deadline cannot be rolled back illegally; top-up capacity and transaction consistency; ghost rules above; no additional actions after terminal latch; stable seat→visual mapping; equivalent shared state on peers after settlement; expected result/exit/rematch completion.

Frame-based checks use explicit tolerances: ice break and body hide in the same rendered update, ghost reveal around 0.5 seconds with frame/capture tolerance; camera return and UI alignment sampled at rendering boundaries. An image existing is not a visual pass. Human-like naturalness remains a review item.

### Metrics

- Mode/policy/seat/host/initiative-stratified win or co-win counts; match/round duration and turn count; AFK/deadline fraction; abort/timeout/desync rates with all attempted-run denominators.
- Per item: draws, turns held and legal-use opportunities, selected/accepted/executed counts, success/cancel/reject rates, effective damage/heal/mitigation per use, cost, delay and opportunity cost, kills/assists, stock left unused.
- Immediate, scheduled, ghost and environment damage reported separately; possession suppression and Cat replacement attributed to their original action. Healing is actual recovered temperature, not advertised healing.
- Target pressure and first-elimination rates; ghost time/impact; survival after top-up; lead changes and score-leader focus. Do not equate attack targeting with an overpowered item.
- Mini-game success assumptions, bot delays, strategy blind spots and item availability are confounders. Policy estimate errors are not automatically combat bugs.

Balance signals are hypotheses: repeated positional advantage in counterbalanced blocks, one strategy dominating alternatives with similar resources, frequently available items almost never useful, defense/healing stalemates, excessive unavoidable focus/ghost pressure. Report raw counts and uncertainty/seed-block dependence; no pooled "balanced" score and no hard nerf from win-rate-with-item correlation. Predeclare screening rules in the pilot, then freeze them; avoid cherry-picking thresholds after seeing main results. Follow-up controlled A/B or larger samples are required before tuning.

## 10. Implementation tasks and gates

| Task | Deliverable | Gate before next task |
|---|---|---|
| T00 contract freeze | Runtime rule/catalog/RNG map, primary manifest, permitted observations, completion definition and coverage ledger | Current user rules mapped, stale doc/asset flags classified, no unresolved rule alters verdicts |
| T01 recording | DTO/sink plus scoped authoritative and presentation hooks | One action trace reconciles request→commit→effect→peer state; no dropped events; lifecycle/file failure behavior checked; telemetry on/off preserves deterministic fixture outcome |
| T02 complete-match driver | Normal lobby/Solo entry, own-peer input, deadlines, terminal/result/rematch handling, watchdog | One actual Duel and one real-BT Solo complete without state forcing; attempted run always gets a final or partial record |
| T03 policy/mode coverage | Six policy strategies and ghost-target logic; 3/4-seat rotation | No hidden-state reads; one full 3-player and one full 4-player match; legal and invalid-command cases remain separated |
| T04 analysis/assertions | Event reconciliation, sequence-aware hashes, summaries and evidence index | Known synthetic broken logs are detected; correct recordings do not create false mismatches; expected denials excluded from bug counts |
| T05 pilot | Eight runs, two per mode, plus targeted recorder and UI/capture checks | No critical blocker, complete telemetry, measured run cost, capture overhead and timeout budgets fixed. Pilot excluded from the 60 |
| T06 campaign | 60 normal completed matches and 24 separate boundary cases | One match-group at a time by default; preserve all failed/censored attempts, stop/reproduce serious defects, avoid resource-induced false timing results |
| T07 findings review | Korean report, flow/coverage tables, match CSVs, linked screenshots/clips, ranked reproductions and tuning hypotheses | Every claim links to a run/turn/action and marks evidence level; untested and unresolved checks remain visible |

Start sequentially; allow at most two simultaneous match groups only if the pilot proves adequate CPU/GPU/frame-time headroom. Estimate time/storage from pilot median and slow-tail measurements, not an invented promise. Separate baseline and repaired-build cohorts if later fixes are authorized; never silently pool different executable/rule hashes.

## 11. Reporting and validation debt

Finding format: ID, severity, category (confirmed bug / balance signal / test-driver or recorder fault / undecided rule / visual review), observed vs expected, run/build/seed/seat/turn/CopyId, minimal reproduction, event range, screenshot/video, repeat rate, recommended next action and verification status.

| ID | Required evidence | Current status |
|---|---|---|
| PT-01 | Recorder completeness, serialization, final/partial output and no gameplay side effects | Implemented foundations; detailed qualification gaps in validation ledger |
| PT-02 | Complete-match natural driver for Solo/Duel/3p/4p with terminal evidence | All four mode preflights passed; retain earlier censored/invalid attempts; not primary evidence |
| PT-03 | Audited gameplay RNG/replay and counterbalanced scenario manifest | Isolated optional streams and source/config hashes implemented; 8-pilot and 60-primary manifests frozen. No exact schedule replay claim |
| PT-04 | Eight-run pilot and measured resource/timeout budget | Seven completed pilots passed, eighth running; actual framebuffer and extra lifecycle qualification pending. See validation ledger for current progress |
| PT-05 | Sixty normal completed matches with all attempts retained | **0/60 in this campaign** |
| PT-06 | Twenty-four labeled boundary cases | Exact variant mapping in `Docs/Validation/PLAN_066_boundary_coverage.md`; supplemental 24 fixture types do not imply all planned variants passed |
| PT-07 | Visual evidence review and balance hypothesis report | Pending actual runs |

## 12. Review corrections and implementation contract

- Completion aggregates every required peer/check, telemetry closure and explicit outcome. Process exit, callback completion and screenshot existence cannot override an individual failure. Persist match outcome, visual outcome, telemetry outcome and teardown outcome separately.
- Multi inventory revisions describe inventory only, not an atomic whole-match revision. Duel/Solo have no equivalent retained inventory view. Continuous samples are diagnostic only; compare stable terminal checkpoints or explicitly aligned mutation boundaries. Missing comparable checkpoints mean inconclusive, not desync.
- Current human RPCs lack a universal request ID. Instrument authority receipt IDs and explicit verdicts without interpreting a normal return as acceptance. Keep command driving serialized. Do not modify production RPC payloads just to make evidence correlation easier; ambiguous correlations invalidate that measurement.
- CopyId is inventory-local. Steal can allocate a new copy or merge uses into an existing copy. Track (match, inventory owner, CopyId) plus source/destination/use deltas; do not equate copies across seats.
- Duel/Solo currently use MatchManager's hardcoded two-win threshold; Multi uses the resolved kill threshold and retained terminal release. Record effective thresholds, not only SO values. No balance refactor here.
- Mini-game deadline has an existing 0.5-second grace in MiniGameAttemptTracker, combined with live Prep/identity checks. Boundary expectations must follow both; do not introduce a stricter rule through the checker.
- SoloMatchProbe is an existing natural full-match foundation. Its current policy excludes mini-game items and seeds global RNG once; this is not sufficient for the planned policy/causal-analysis matrix.
- First implementation runs are instrumentation preflights, explicitly excluded from both pilot and primary counts until T01–T04 gates pass. Unimplemented checks must remain visible; no automatic promotion of an MVP trace to balance-certified evidence.
