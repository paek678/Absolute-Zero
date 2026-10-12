# Overall logic remediation — 2026-10-13

Plan: [task sequence and acceptance](../Plans/PLAN_overall_logic_remediation_20261012.md).
Evidence root: `output/validation/remediation-20261013` (local generated evidence).

**Final outcome: T01–T04 pass within the approved scope.** Plan suitability was rated 95/100 before implementation; this is not a bug-free probability. All required functional gates below now have current-build evidence. No commit/push was requested or performed. Test players exited; Editor is back in LobbyScene/Edit mode.

## Confirmed corrections

- T01: obsolete English duel caption assertion updated to the approved Korean caption. Threshold fixtures now verify independent nonzero copy identities, per-copy uses, grant counts, preservation, reset and environment rules with a varied finite pool. Focused room tests 6/6; temperature-env 2/3/4-player fixtures 3/3.
- T02: generated grants, direct/staged top-up, immediate duel reroll and prepared Multi reroll share a three-copy eligibility calculation. Filtered weighted selection cannot exhaust retries on a dominant ineligible candidate. Reroll excludes its replacement slot; staged replacements count immediately; empty eligible pools preserve state and RNG. Prepared Multi reroll revalidates the entire original inventory because untouched copies also affected eligibility. Direct setup and steal/transfer admission remain unchanged.
- T02 validation: original production-picker reproducer now rejects copy four; focused calculation tests 25/25; full Editor suite 1,177/1,177; temperature-env (2/3/4), inventory (2/3/4), special (3) and real Relay full duplicate layout (4) pass. The layout cohort checks matching copy signatures and actual rendered slot positions across all four clients. Inventory fixtures exercise authoritative writers; they are not natural matches.
- T03 baseline: current-build observe-only runs 3/3 emit the ComputeBuffer disposal warning, diagnostic shutdown-release runs 0/3. The sampled fallback is fixed at 4,096 bytes. This demonstrates a missing shutdown release, not progressive gameplay memory growth.
- T03 implementation: UPM `Client.Embed` retains 2D Animation 13.0.5. Comparison against all 1,987 original package files finds one changed source: `GpuDeformationSystem.cs`, with three event-subscription/unsubscription lines. No package upgrade, cache edit, production reflection or warning suppression. Manifest unchanged; lock source becomes embedded.
- Development-only transition harness accepts 1–5 same-process cycles and retains default one-cycle behavior. Resource diagnostics remain alive during transition validation instead of auto-quitting as a standalone probe.

## Evidence qualifications

An initial focused test invocation ran the old loaded assembly while compilation failed due to NetworkList not implementing IEnumerable. Fixed with a bounded iterator adapter and reran: 25/25 on the new assembly. The earlier 17-test result is not evidence for the new code. Initial T01 build was interrupted by domain reload; only the subsequent successful build is counted.

Python harness: 46/46 passed. Final embedded-package Editor run: 1,178/1,178 (1,177 project cases plus the package's pre-existing placeholder test). Build: errors 0, warnings 4. Remaining warnings: disabled Player Pipeline without RuntimePipelineConfig, existing MalgunGothic SDF importer inconsistency, and two unused-field CS0414 warnings. These do not indicate new compilation failures.

The first repeated-transition run failed because the concurrently launched Editor Solo session held local port 18777. Its bind errors and reports are retained (`t03-transitions`). After stopping Editor Play, the identical build completed all five same-process cycles, four peers, 670 checks (`t03-transitions-isolated`). Each cycle includes Relay 1v1/3p/4p, normal lobby return and multiple Solo entries, including unexpected local shutdown recovery. Four peers each sampled the same fallback identity throughout 13 valid samples, always 4,096 bytes; at shutdown it was null/invalid. Targeted warning count: zero.

## Deferred gates

| ID | Stimulus | Status / closure evidence |
|---|---|---|
| R03-A | Post-fix observe/release A/B, explicit double release | Pass: six normal exits, zero targeted warnings/errors; diagnostic second release stays safe (`t03-after`) |
| R03-B | Five same-process transitions through Solo, Relay 1v1, 3p and 4p; fallback identity/size | Pass: 670 checks/four peers/five cycles; fixed identity and size, zero targeted warnings (`t03-transitions-isolated`) |
| R03-C | Editor Stop/reenter | Pass: Lobby -> Solo -> Stop releases fallback; reenter recreates valid 4,096-byte fallback -> Stop. Current game visuals use no active SpriteSkin in the sampled Solo scene; this does not validate every GPU-skinned custom shader (`t03-editor-*.json`) |
| R04-A | Final full Editor suite, build warnings, 23-case local matrix | Pass: 1,178 Editor cases, build errors 0/warnings 4, local matrix 23/23 (`t04-matrix/matrix.json`) |
| R04-B | Relay ghost/customization and four departure menu scenarios | Pass: 5/5; ghost checkpoint equality, cosmetics/back/tail, Solo exit, 4->3 continues, 3->2 returns, host exit returns (`t04-relay-menu-summary.json`) |
| R04-C | Complete natural Solo and Relay 1v1 through result/lobby | Pass: Solo 227.2 seconds/23 prep windows/5 modeled minigames; Relay 1v1 325.4 seconds/32 prep windows/7 modeled minigames. Result visible, lobby returned, no analyzer problems, shared terminal states agree |
| R04-D | Natural Relay 4p through ghosts, next rounds, result/lobby; 30-minute budget | Pass: 1,348.7 seconds (22m29s), six rounds, 80 preparation windows, 31 ghost skill uses (30 Grudge / one Possession), 21 modeled minigames across peers. Five-kill victory, all four result states and lobby returns observed, terminal shared state equal, four exit codes zero, no analyzer problems (`t04-natural-4p/report.json`) |

Additional top-up transition boundary coverage: variants 0–5 pass in `t04-topup-boundaries` (capacity, rollback/recovery/fault latching, selected/Ready/minigame preservation). Variants 6–9 initially failed their stimulus assertion because the runner was invoked without the required inventory-view/descriptor delay; no stale command was actually exercised. Retained those failures. Reran 6/7 with 2,000 ms inventory-view delay and 8/9 with 2,000 ms descriptor delay: all four pass, including explicit stale-command/cancel rejection and ordering evidence (`t04-topup-ordering`). Together this supplies 10/10 correctly configured boundary scenarios; it is not a claim that the first ten-case invocation passed.

## Package rollback

The original registry dependency remains `com.unity.2d.animation: 13.0.5` in the manifest. To roll back intentionally, move the embedded `Packages/com.unity.2d.animation` directory outside the project, then let UPM resolve the unchanged registry version. Do not perform this during an active session. Preserve the three-line patch and validation record. Original manifest, lock and package hashes are in the evidence root. Retain package license and metadata with the embedded source.

## Limits

Live Editor console after Stop/reenter: one existing `DisconnectDispatcher.Awake` warning at line 27 (`DontDestroyOnLoad` called on a child of the `Managers` root), no returned Error entries in the bounded 100-entry query. The same warning exists in baseline logs. Five mode cycles and departure checks pass despite it; it is retained as non-blocking lifecycle cleanup debt, not silently counted as a clean console. No scene ownership change is included in this remediation.

The first extra ghost-render cohort used hidden Windows players: gameplay assertions passed but all framebuffer captures were black. Those files (`t04-ghost-render`) do not qualify as visual evidence. The visible-window rerun (`t04-ghost-render-visible`) passed Relay checkpoint/cosmetic checks and produced valid framebuffer images. Inspected Grudge and Possession impact frames: the ghost avatar, human avatars, hats and item racks remain visible, without deformation from the fallback cleanup. The zero-error resource observations apply to shutdown; active gameplay buffers are retained.

## Inspected renders

- Generated-copy/capacity regression uses authoritative inventory assertions; the separate full-copy Relay layout render checks slot presentation at maximum physical capacity: `t02-relay/layout-4-w1/seat0.layout-full.png`.
- Normal four-player post-action frame: `t04-natural-4p/0/visual-12-settled-6.png`.
- Ghost Grudge: `t04-ghost-render-visible/host.0005.ghost-grudge-impact.png`.
- Ghost Possession and status icon: `t04-ghost-render-visible/host.0007.ghost-possession-impact.png`.
- Some matrix snapshots deliberately occur during loading/result teardown. They are not used to claim a completed gameplay visual review. Natural-match captures and the visible ghost rerun supply that evidence instead.
- Hidden-player result captures use `SoloLaunchProbe.WriteRender`, which temporarily converts overlay canvases to camera-space. Foreground world items can consequently appear over loading/result UI in that diagnostic composite. These images prove neither real framebuffer layering nor a production UI defect. The natural-match result gate additionally checks the active result UI and exact terminal state; the visible ghost cohort supplies genuine framebuffer visual evidence.

## Final interpretation

- The observed generated-copy defect is fixed without converting transfers into capped grants, changing item uses, merging duplicates or shifting existing anchors.
- The obsolete assertions now test the accepted behavior. Invalid first invocations and missing-stimulus runs remain explicitly separate from passing reruns.
- The fallback-buffer warning is resolved by its package owner at shutdown; repeated normal sessions retain one fixed-size buffer. No warning filtering or arbitrary-buffer disposal is used.
- The 22-minute four-player match is a duration observation under four modeled policies and five-kill victory. It is not a deadlock, because combat/rounds progressed to a winner, nor is it enough data to approve balance. Game rules and timeScale remained unchanged.
- Build identity: successful current development build `0a9f1292be5c48ecb23d66acb2a89b4f`, 400,762,542 bytes; Core DLL SHA-256 `9C94517F864203B368101B3ACDC3AA2880248CCCC3F5E7FB4B47F4F4FCDAA05C`. Package originals/hash comparison, logs, raw JSON and captures remain under the evidence root.

No separate-machine/packet-impairment campaign, human minigame usability study, all-item audiovisual pass or statistical balance claim is implied. Full-match human decisions/minigame outcomes are modeled by the existing development driver; Solo uses its actual BT. Existing promotional videos are unchanged.
