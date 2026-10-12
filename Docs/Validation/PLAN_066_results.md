# PLAN066 — Campaign evidence and validation ledger

Status: implementation in progress, 2026-10-11. User authorized proceeding after review. No primary campaign completion is claimed.

## Scope

Implement the recording/driver/assertion foundations, validate each layer, then execute the approved pilot and campaign only after their prerequisites pass. Retain abnormalities and failed attempts. Do not change production balance or the user's BT assets.

## Current evidence

- Unity CLI connected to this exact repository, Unity 6000.3.11f1, LobbyScene clean/EditMode before edits. MCP project root independently matches.
- Existing fixed development executable is covered by Windows Firewall application rules; no permission/firewall change made.
- Recorder/RNG EditMode tests: 8/8 passed, including deterministic bounded-queue overflow retaining partial evidence. Python evidence-gate/invariant tests: 21/21 passed, including explicit censored-pilot follow-up and rejection of non-timeout defects.
- Full current Editor run: 1,140/1,144 passed. All four failures were stale PLAN039 visual-coordinate expectations superseded by approved PLAN059 placement. Updated only test expectations to the recorded approved coordinates; focused PLAN039 recheck passed 15/15. Gameplay map assets were not changed for this repair.
- Existing regression suites: Plan029 38/38, Plan034 28/28, Plan036 186/186, Plan037 652/652 (904 total). These are tests, not games. Injected LobbyGateway errors in the Plan037 tests are expected test diagnostics.
- Production Solo graph and MalgunGothic font hashes remain unchanged from the pre-task snapshot.
- Current telemetry is an explicitly opted-in development observer. Continuous samples are not atomic network snapshots and are not causal item-effect totals.

## Deferred validation ledger

| ID | Required check | Status |
|---|---|---|
| PT-01 | Complete receipt/verdict/effect attribution, copy-transfer lineage, writer overflow/failure, lifecycle; on/off fixture parity | Overflow/file-failure tests pass. Observer on/off joint-win fixture passes with identical winner mask and score assertions on all four peers. Explicit first-turn Fan request/CopyId to commit to 3-degree effect and four settled peer projections reconciled. Complete all-item causal totals remain uncertified |
| PT-02 | Normal Solo/Duel/3p/4p entry, natural end and every peer's visible result, no state forcing | All four modes have successful preflights. Revised 3p completed naturally in 787.8 seconds, final scores 5/2/1, all peers passed lobby return and terminal equality. The older 900-second censored attempt remains retained |
| PT-03 | Full RNG-site audit and frozen counterbalanced manifest | Optional isolated item-drop, duel-steal, multi-steal, environment and environment-kids streams implemented; production BT keeps its existing own seeded RNG. Audio/lobby-name/mini-game visual RNG remain separate. Frozen pilot/primary manifests generated (8/60), same-build/config resume guard tested. No exact schedule replay claim |
| PT-04 | Eight qualified pilot games, capture overhead and calibrated budgets | Initial pilot: 7/8 passed; eighth censored at 1513.5 seconds. Explicit same-build follow-up passed naturally in 1559.7 seconds, seven rounds, scores 4/4/5/4, all four peers returned to lobby and agreed on final state. Eight qualified scenario completions across nine attempts; failed original retained. Actual framebuffer and 14 additional diagnostics pass |
| PT-05 | Primary campaign | Running `primary-v2`; authoritative completion count in `primary-v2/campaign-progress.json`. Two completed passes at this update; target 60 |
| PT-06 | Specified boundary cases | Supplemental fixtures have passing evidence for 24 types; exact original variants have mixed runtime/Editor/pending coverage. See `PLAN_066_boundary_coverage.md`; no 24/24 exact-variant completion claim |
| PT-07 | Human-readable findings and visually reviewed evidence | Pending |

## Findings

| ID | Category | Evidence | Status |
|---|---|---|---|
| F066-01 | Existing test harness | NicknameFlowProbe waits CameraProbe.Completed but does not aggregate its PASS/FAIL | New campaign must independently aggregate required checks; old tool not reused as final verdict |
| F066-02 | Measurement contract | Multi inventory revision does not atomically version temperatures/scores; Duel lacks that view | Separate stable checkpoints from continuous diagnostic samples |
| F066-03 | Measurement contract | Human RPCs have silent returns; CopyId changes/merges on steals | Explicit verdict/lineage hooks required before balance attribution is certified |
| F066-04 | Recorder defect, fixed | First Solo preflight omitted inventory arrays because NGO ItemSlotNetData is not JsonUtility serializable | Added explicit serializable DTO and regression test; second Solo/Duel preflights retained full inventory data |
| F066-05 | Driver confound, corrected for subsequent builds | All policies initially waited four seconds before Ready; equal-temperature targets used engine enumeration tie order | Separate declared policy Ready delays and stable seat ordering with seeded tie selection. Retain earlier attempts; do not treat their natural deaths as unbiased balance evidence |
| F066-06 | Match-length warning | First 3p Relay run: 913 seconds process runtime, 57 observed prep turns, six rounds started, scores 2/2/1 at stop; 10 deaths, five natural. No natural terminal within the 900-second match budget | Progress continued; this is a censored attempt, not a deadlock or draw. Requires improved-policy reproduction and pilot budget calibration |
| F066-07 | Evidence gate improvements | Malformed summary could crash analysis; closure alone did not prove lobby return | Analyzer now fails closed on malformed envelopes; schema 2 driver explicitly waits for LobbyScene and stopped NGO. Runtime verification pending latest build |
| F066-08 | Confirmed unfinished artwork | `AZPlayerVisual.GetGhostPlaceholderSprite` fills an 8x8 texture solid white, then renders it cyan; visible as a rectangle after death in the 4p remote view | Existing intentional placeholder, not a newly introduced synchronization error. Needs approved ghost art. Evidence: `output/validation/plan066/preflight-multi4-01/1/visual-12-settled-6.png`; no art substitution made |
| F066-09 | Visual readability candidate | Opponent item icons/counts and banner metadata are small at 1280x720; forward item rack appears almost edge-on | Screenshot inspection only; keep current user-approved layout and request visual confirmation before redesign. Evidence: 3p/4p attack captures |
| F066-10 | Existing shutdown resource warning | Completed player logs report `GarbageCollector disposing of ComputeBuffer` after Input System shutdown | Previously tracked in PLAN040/043/046/051. No gameplay exception here; not claimed leak-free or fixed. Project diagnostic probe identifies the 2D Animation fallback-buffer path as a candidate, not a new proven allocation cause |
| F066-11 | Configuration/termination gap | `MaxRounds` is stored in GameModeRuleSO and copied into snapshots but has no production consumer; active Multi rule says 99. Natural deaths award no kill score | Static finding: the field currently does not enforce a round cap. Unbounded non-scoring/AFK play is a hypothesis to reproduce, not yet a demonstrated infinite execution. Changing termination policy requires a design decision |
| F066-12 | Existing fixture teardown race, corrected | RPC guard actors passed and exited before 12-second observer checks; the real 3-to-2 departure policy then shut down the match | Added a run-scoped all-seat completion barrier only to the development `rpc-guards` fixture. First failed attempt retained; recheck passes every peer. No gameplay departure rule changed |
| F066-13 | Stale regression expectations, corrected | Full Editor suite had four map-layout failures expecting PLAN039 coordinates, although approved PLAN059 moved all four maps outward | Updated expectations and test name against the recorded user-approved placement; 15/15 focused tests pass. Do not revert map art/layout to satisfy historical tests |
| F066-14 | Balance/design drift candidate | Resolved `Hand Fan`/HandFan.asset is one-use, Damage=2, temperature filter, DropWeight=12; permanent basic Fan has Damage=3 with the same filter. GAME_DESIGN's item table still says Hand Fan=4 | In ordinary unblocked play, the consumable has weaker direct damage than the permanent basic attack and Windbreaker's 2 block fully absorbs it. A random-slot item still has a niche when basic items are blocked, so this is not unconditional dominance. Confirm desired value/role before tuning; no balance asset edited |
| F066-15 | Confirmed diagnostic capture artifact, not reproduced in actual framebuffer | Hidden camera re-render shows world items/fans over the result overlay because it changes canvas render mode. The visible Solo follow-up completed naturally in 248.4 seconds and its actual framebuffer `qualification/framebuffer-solo/0/result.png` was inspected: clean black overlay, result label and buttons, no world-item overlap | Actual result-screen check passes for this run. Keep earlier diagnostic images labeled and do not change gameplay UI sorting to repair a capture artifact. No full-screen/UI-wide visual certification implied |
| F066-16 | Test-policy interpretation limitation | Living target temperature ties use sorted seats and seeded choice; ghost target ties currently use engine enumeration after sorting by temperature. Ready delays also deliberately differ across policies | Do not attribute mixed-policy initiative advantages or equal-temperature ghost target pressure directly to game balance. Current frozen cohort can screen functional errors and conditional outcomes, but cannot certify unbiased seat win rates. No mid-cohort driver change or exact replay claim |
| F066-17 | Confirmed censored long-match observation | `pilot-008-p4`: 94 observed preparation windows, six rounds, scores 4/3/3/4, 16 deaths including two natural deaths; 1500-second budget elapsed before a winner | All four telemetry streams closed healthy, server had 623 valid temperature checks and 42 observed Grudge checks, no observer/gameplay error before timeout. Progress continued through sequence 95. Retain as censored, not a draw or deadlock. A longer-budget follow-up is required before primary qualification; no score/turn/timeScale tuning |
| F066-18 | Conditional Solo difficulty signal | First eight primary Solo games (four attacker, four survival) all ended 0:2 for the modeled human seat. Of 270 logged FIRST main-item executions, 222 were bot seat 1 and 48 human seat 0 | These are log-scoped first main actions, not a count of all initiative opportunities. Human policies wait 1.25/2 seconds before Ready and use simplified decisions; bot uses the real BT and item delay policy. Candidate tuning axis: think/Ready/item delays and tactical choices, not just starting stats. Do not infer actual human win rate or nerf the bot from this sample |

## Retained preflights (excluded from pilot/primary totals)

| Run | Observed result | Evidence |
|---|---|---|
| preflight-solo-01 | Natural game completed, telemetry invalid: missing inventories | `output/validation/plan066/preflight-solo-01/review.json` |
| preflight-solo-02 | PASS, actual production BT, 17 turns / 4 modeled mini-games, about 199 seconds | `output/validation/plan066/preflight-solo-02/report.json` |
| preflight-duel-01 | PASS, real Relay, both peers completed and saw result; stable terminal projection matched; 25 turns, about 314 seconds | `output/validation/plan066/preflight-duel-01/report.json` |
| preflight-multi3-01 | FAIL: natural terminal timeout; telemetry healthy; progression continued | `output/validation/plan066/preflight-multi3-01/report.json` |
| preflight-multi3-02 | PASS, real Relay, 787.8 seconds, scores 5/2/1; all three peers saw result, returned to lobby and agreed on final shared state | `output/validation/plan066/preflight-multi3-02/report.json` |
| preflight-multi4-01 | PASS, real Relay, 44 turns / four rounds, about 761 seconds; final scores 1/1/2/5. Every peer returned to LobbyScene with NGO stopped. Final shared projections match | `output/validation/plan066/preflight-multi4-01/report.json`, `audit-current.json` |

The current 4p trace passed 301 authoritative temperature-effect checks and 15 observed Grudge checks (range, target agreement, same-target limit, source cooldown, possession-spent monotonicity). All recorded command scopes have explicit verdicts. These totals describe exercised operations, not exhaustive skill/item coverage. The host's 12 measured screenshot captures took approximately 67–78 ms each; no on/off frame-time equivalence is claimed.

The revised 3p trace passed 296 temperature-effect checks and 10 observed Grudge checks. Pilot budgets are Solo 600s / Duel 900s / 3p 1800s / 4p 1500s, frozen before pilot execution from retained preflight observations. A current 3p resource sample showed about 5,872 MiB free system RAM and 559–562 MiB working sets per test player; this is one resource observation, not a parallel-load qualification. Continue one match-group at a time.

Runtime fixture regressions use `.codex/scripts/run_plan066_regressions.py` (24 explicitly named existing cases). They use local UTP, not Relay, and forced fixtures. They supplement rather than silently replace every exact PT-06 scenario: ghost possession/cooldown, precise deadline variants, Solo re-entry and impaired transport still require their specified evidence.

Initial batch: 23/24 passed; remaining RPC guard case passed after F066-12 fixture repair (`rpc-guards-recheck`). Latest build job `72f1d96e69614d658eb4d24b15987d77` succeeded, 0 errors / 4 warnings. These fixture passes do not qualify any natural match.

Observer parity evidence: `observer-parity/report.json`, four finalized and closed JSONL streams in the `on` case. Scope is deterministic joint-win fixture assertions, not full frame-time or random-inventory equivalence. Explicit request/effect/settled reconciliation: `preflight-multi4-01/action-lineage-example.json` (seat 2, CopyId 1, action receipt 10, sequence 1).

Machine-readable screening summary and visual index: `output/validation/plan066/screening-summary.json`, `.csv`, and `screening-report.html`. Counters distinguish Multi death-service events from unmeasured Duel/Solo death causes. Automatic mini-game outcomes do not test pointer/UI playability.

No gameplay/balance defect has yet been established by the new campaign. Instrumentation preflights do not count toward the requested 60 games.

## Post-pilot qualification and budget amendment

- Initial pilot: seven natural completions and one retained censored attempt. `pilot-008-p4` remains failed; a successful follow-up must not rewrite it.
- Independent diagnostics: `qualification/report.json`, nine passed cases. Actual framebuffer Solo, ghost contention, ghost round revival, possessed attack, possessed multi-use, stale-ticket replacement, stale-ticket round transition, impaired loopback input, and services. The report explicitly keeps `pilot_gate_passed=false`.
- Visually inspected actual Solo result framebuffer (clean), ghost impact and possession cue. Cyan ghost placeholder and small opponent rack remain visible; no gameplay UI sorting repair was made.
- First lifecycle launcher failed before launching the game because PowerShell file execution is restricted. Retained in `lifecycle-extra/progress.json`. A Python launcher now performs the same four-peer mode-transition procedure without changing execution/security settings; fresh evidence goes to `lifecycle-extra-02`.
- A bounded same-build, same-seed/scenario pilot follow-up will use a 2700-second watchdog in `pilot-followup/pilot-008-p4`. This changes only the external test budget, not timeScale, preparation duration, rule assets, BT, scores or item effects. Online scheduling can diverge, so this is trigger-class reproduction, not exact replay.
- Before any primary execution, Multi watchdogs are amended to 2700 seconds in a new `primary-v2` manifest; the old unused `primary` manifest is retained. Solo/Duel remain 600/900 seconds. Initial pilot failures stay in reliability/censoring denominators. Qualification requires the other seven original passes and a passing same-build follow-up, explicitly recorded in `pilot-qualification.json`.
- No primary games have run at the time of this amendment. The runner stops on a new failure; no replacement match is silently counted.

Follow-up outcome: `pilot-followup/pilot-008-p4/report.json` passed in 1559.7 seconds; both original and follow-up remain in the summary. All five `lifecycle-extra-02` cases passed, including 134 assertions across the four same-process transition peers. `primary-v2/pilot-qualification.json` records the explicit evidence selection. Primary execution has now started; gameplay rules and the compiled runtime remain unchanged.
