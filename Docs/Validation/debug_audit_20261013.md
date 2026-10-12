# Fresh debugging audit — 2026-10-13

Request: rerun tests and detect errors in the current remediation build. This audit changes no gameplay source, assets, rules or network timeouts.

Evidence: `output/validation/debug-audit-20261013`. Previous complete-match evidence remains in [the remediation report](overall_logic_remediation_20261013.md); those matches are not counted as fresh runs here.

## Build and checks

- Verified CLI and MCP target this project, Unity 6000.3.11f1, Editor not playing/compiling.
- Current development player Core DLL SHA-256: `9C94517F864203B368101B3ACDC3AA2880248CCCC3F5E7FB4B47F4F4FCDAA05C`. No newer C# source was found under Assets/Scripts or the embedded animation Runtime directory.
- Fresh Editor tests: **1,178/1,178 pass**, zero failed/skipped/inconclusive (23.18 seconds). Includes the embedded package placeholder test; project tests number 1,177.
- Python harness tests: **46/46 pass**.
- Local multiplayer edge matrix: **23/23 pass**. Winner seats (3p: three, 4p: four), four ghost seats, attack/terminal/idle disconnection, five host-exit variants, initialization failure, minigame actor/target departure and three consecutive duel rematches. Expected deliberately terminated processes are accounted for by the runner, not treated as normal exits.
- Network impairment: **6/6 pass**, local UTP proxy with 100 ms configured one-way delay and 3% probabilistic packet loss. Ghost, inventory (2/4 players), RPC guards, delayed effects, and duel minigame/second round. Proxy logs record 1,290 received packets and 62 dropped (a finite seeded sample, not exactly 3%), with zero proxy errors. This is same-machine simulation, not separate-machine Internet testing.

## Findings and qualifications

| ID | Priority | Trigger / observation | Recommended follow-up |
|---|---|---|---|
| DA-01 | P2 presentation/recovery latency | Abruptly terminating a remote process during AttackPhase produced `PresentationBarrier` timeout after 19.6 s, then continued. Transport reported the disconnected peer at scenario time 38.6 s; Lobby transport timeout is 30,000 ms. The disconnect-attack scenario passed; this is bounded recovery, not a deadlock. | Review the desired abrupt-disconnection wait and communicate reconnect/wait state. Do not globally shorten the barrier: normal long presentations also use it. Normal menu departure is a separate path. |
| DA-02 | P3 lifecycle warning | `DisconnectDispatcher.Awake`, line 27, calls DontDestroyOnLoad on a child under Managers. Unity rejects this request with a warning. Prior mode/departure checks and current edge tests must determine functional impact; warning alone does not prove lost gameplay state. | Clarify persistent root ownership, then remove redundant child persistence or make ownership explicit. Avoid blindly preserving/detaching the whole Managers hierarchy. |
| DA-03 | P2 validation reliability | Existing ghost capture report marks `capture_complete=true` based on screenshot counts. Pixel inspection of the historical hidden-window cohort finds all **32/32** PNGs completely black. | Add decode/dimension/nonblank gates before labeling visual capture success. Historical hidden renders remain invalid visual evidence; previous visible rerun is recorded separately. |
| DA-04 | P3 test invocation | Matrix top-up variants 6/7 and 8/9 require explicit view/descriptor delay, but CLI selection alone does not supply or reject missing stimulus. Prior false failures were corrected by proper invocation. | Validate required stimulus at CLI entry or provide explicit named presets. This is harness configuration debt, not a new gameplay failure. |

Editor errors-only query returned five entries, all expected fault injection (CosmeticRegistry duplicate/undefined parts and LobbyGateway injected removal response). Exact messages are covered by LogAssert.Expect in Plan037EquipmentTests and Plan037LobbySessionTests. Console was not cleared. These are not production errors.

Some matrix scenarios have no presentation checkpoint records, so the generic `checkpoints_equal` field is false even when their scenario assertions pass. Inventory/RPC cases must not be advertised as full presentation-state comparisons from this field. Ghost and delayed-effect stress cases do compare matching checkpoints. A separate terminal-log comparison found matching final state snapshots across both inventory peers (2p), all inventory peers (4p), and both duel-minigame peers; missing snapshots are marked not applicable, not mismatch (`stress-terminal-states.json`).

The other abrupt-exit cohort (`disconnect-terminal`) reached a 14.5-second presentation timeout before successful recovery. Together with DA-01, this supports reviewing perceived waiting time; neither scenario demonstrates an unbounded hang. No automatic timeout reduction was made.

## Deferred validation ledger

| ID | Stimulus | Status / evidence required |
|---|---|---|
| DA-V01 | All selected winner seats, ghost seats, abrupt exits, failed init and repeated duel | Closed: edges/matrix.json 23/23; stress matrix 6/6. Three duel rematch resets completed. |
| DA-V02 | Separate-PC Internet disconnect, loss and latency | Not run; same-PC proxy does not close this gate. |
| DA-V03 | Human visual/input review and statistical balance | Not run in this audit. No new gameplay recordings or balance approval implied. |

## Final outcome

No newly reproduced gameplay assertion failure, crash, null/index/destroyed-reference exception, compilation error or targeted ComputeBuffer disposal warning in the fresh cohorts. The four findings above remain explicit cleanup/validation/latency work, not silently counted as fixed. Final scan evidence: `target-errors.json` is empty. Editor reports playing=false, compiling=false, scriptCompilationFailed=false. All launched test players exited. No gameplay source edits, commit or push.
