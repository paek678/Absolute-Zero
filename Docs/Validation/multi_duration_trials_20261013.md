# Four-player duration trials — 2026-10-13

Status: complete to revised five-match scope; **user-selected 3 rounds / 3 kills** applied, final Editor tests and build passed. [Plan and unchanged mechanics](../Plans/PLAN_multi_duration_trials_20261013.md).
Evidence root: `output/validation/multi-duration-20261013`.

**Scope revision:** user requested five completed games instead of ten. The scheduler was stopped, case04's four owned processes were terminated, and an observer adopted the four continuing case03 processes to retain exit codes and finish analysis. Partial case04 is marked canceled, never a completed/failed balance sample. Cases01/02 and planned extra confirmation games will not be launched. Selection will use cases03/05/06/07/08 only.

## Implementation and validation

- Activated the previously unused Multi MaxRounds at completed-round boundaries. Highest cumulative kills among connected participants wins; equal leaders share victory. Ghost participants remain eligible. Existing kill-threshold victories still terminate immediately through settled presentation handling.
- Round-cap termination uses the same persistent MatchNetworkState terminal result and RPC as kill victories. No new turn driver, client-authoritative decision, forced temperature change, shortened animation, or wall-clock cutoff.
- Development-only immutable trial rule decorator activated only with opt-in full-match driver plus both trial flags. Default rule asset remains unchanged while comparing candidates.
- Editor suite 1,187/1,187 (nine new round-limit tests); Python harness 46/46. Initial test command hit domain reload and was retried after reconnection; it is not counted as a test failure or pass.
- Build job `2a3b86a7d3aa4cc8a379c9c2814fdc32`: errors 0, warnings 4, 400,765,674 bytes. Existing allowed fixed executable path retained.

## Method

Ten requested completed matches: eight candidates followed by two confirmation matches using the selected production asset. Eight natural NGO clients run as two independent Relay matches at a time. The driver selects items/targets through existing gameplay RPCs; minigame results and human choices are modeled. No forced deaths or altered timeScale (1 throughout).

Candidate A: attacker/balanced/resource/utility, policy and gameplay seed 101320, normal per-policy Ready delays.
Candidate B: survival/cautious/utility/resource, seeds 101321, six-second human Ready delay. Policy placement follows logical seat rather than connection arrival.

Times reported below include launch, lobby/scene setup, match, result observation and return to lobby, so they are conservative relative to gameplay alone. Per-round and in-game durations are separately retained in summary.json. Paired seeds improve comparability but asynchronous arrival/inputs can still produce different action histories; this is not a deterministic counterfactual or statistical certification.

## Five completed observations

| Case | Round cap / kill target | Profile | Total time | End reason | Ghost Grudge damage actions |
|---|---|---|---|---|---|
| 07 | 1 / 2 | A | 3m56s | 2 kills | 9 |
| 08 | 1 / 2 | B | 2m57s | Round cap, sole leader | 2 |
| 05 | 2 / 2 | A | 8m36s | 2 kills | 14 |
| 06 | 2 / 2 | B | 6m27s | Round cap, two tied leaders | 6 |
| 03 | 2 / 3 | A | 8m43s | Round cap, sole leader with two kills | 14 |

All five completed cohorts pass peer assertions, persistent winner/state equality, visible-result state and lobby return. Case03's adopted observer captured four normal exit codes and used the same inspect_peer/compare_terminals analyzers. Its elapsed duration starts at earliest player-process creation; the original runner starts slightly before launching peers. Hidden camera composites are not used to certify actual framebuffer layering.

## Selection

**Final user decision applied to MultiRule.asset: killsToWin=3, maxRounds=3.** First to three kills wins at the settled action boundary. If the third completed round ends first, the highest cumulative kills wins; tied highest scores use existing joint victory. OneVsOneRule remains unchanged; Solo inherits the unchanged duel rule. Preparation stays 20 seconds, initial random items two, random capacity four, top-up four, ghost system enabled.

The agent provisionally recommended 2/2 and briefly serialized that asset before the user's explicit 3/3 decision superseded it. Its two samples were 515.6 and 386.5 seconds including launch/result/return; mean 451.1 seconds (~7m31s), range ~6m27s–8m36s. One round was shorter (~2m57s–3m56s). The 2-round/3-kill sample took 523.4 seconds and ended by round cap at two kills. These observations remain comparison evidence, not final 3/3 duration measurements.

All five samples are below ten minutes, but **none uses the final 3/3 combination**. Do not describe this as five confirmations of the final setting or a measured under-ten-minute 3/3 result. One 2/2 sample produced joint victory; that is the specified tie rule, not a synchronization failure. No guarantee of a hard ten-minute maximum: a long round is still possible.

## Open gates

- MT-01: closed to revised scope; exactly five completed games, case04 canceled/excluded, cases01/02 not run.
- MT-02: closed. User selected 3 rounds/3 kills; serialized through verified Editor. Final tests and build passed as detailed below.
- MT-03: extra full-match confirmations canceled at the user's five-game limit. Final 3/3 complete-match timing remains unmeasured. No assertion of a sixth production-default complete match.
- MT-04: separate-PC human timing and larger balance sample remain outside these five modeled matches. Round/kill caps alone do not guarantee a strict ten-minute wall-clock maximum.

## Final rule verification

- Final asset readback: Multi, killsToWin=3, maxRounds=3 (`applied-config-final.json`).
- Final Editor suite: **1,191/1,191 pass**, including 13 round-limit/kill-threshold cases. Explicitly checks round two does not reach a three-round cap, round three does, tied leaders share, departed leader excluded, no-eligible and missing state cannot publish, third-kill threshold crossing and repeat rejection.
- Python harness suite: 46/46 pass earlier in this work; no subsequent Python harness changes except local evidence helpers.
- Final build job `2e76fa9cbb004c1a850c0a4aeeba5681`: succeeded, errors 0, warnings 487, 400,765,850 bytes. Warning messages grouped as 485 shader warnings, one existing MalgunGothic importer warning, and one disabled Player Pipeline notice. Full messages retained in `final-build-warnings.json`; not a warning-free build. Core DLL SHA-256 `C155E0707C42AE7E71626649F592165EE7783B400BE47FC3184349C4C6C3C320`.
- Five complete cohorts (20 participant processes) passed their result/terminal/return checks. Four extra case04 processes were explicitly canceled at the scope change and excluded; no subsequent matches launched. No test players left running, Editor not in Play mode. No commit/push.
- Older scripted fixtures in MatrixScenarioProbe/MultiScenarioProbe/VisualFourPlayerProbe still include explicit five-kill setup/assertions. They were not rerun or counted as final-setting validation here. Before using those old synthetic victory/video fixtures with the new asset, migrate their threshold assumptions (and paired Python validators) to the configured rule or a clearly labeled fixed fixture. FullMatchPlaytestDriver used for these five games records actual rules and does not rely on those five-kill setups.
