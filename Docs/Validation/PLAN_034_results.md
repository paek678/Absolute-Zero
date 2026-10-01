# PLAN 034 implementation validation — 2026-09-25

## Deferred validation ledger — current queue

Use this table as the carry-forward list for PLAN 034. `Deferred` means the exact check below has not passed on the current implementation; it does not undo narrower passes recorded later in this file. After each relevant run, update the row's status and link its build ID, report, logs, and four-view captures. Add a new row for a newly found gap; move a row to `Passed` only when its stated acceptance evidence exists. Older "still open" lists and scores below are historical snapshots; this table and the latest evidence sections take precedence.

| ID | Priority / status | Check to run later | Evidence needed to close |
|---|---|---|---|
| V034-01 | WP5 / Passed | On real Relay Host + three clients, possess a living target and separately use attack, heal, defense, multi-use, unlimited and minigame items. Include successful and failed minigame outcomes. | Seven per-case Relay reports, logs and four-view captures below. Each queued successful use showed one suppression cue; failed minigame consumed its item before queueing and showed no suppression cue. All peers agreed on one ordinary cost and unchanged temperatures. |
| V034-02 | WP5 / Passed | Check the visual schedule with suppressed ordinary actions, suppressed defense alone, defense plus attack, and configured item duration above three seconds. | Three final-build real Relay cases below have four-view captures and timing logs; the synthetic EditMode mixed schedule covers the above-three-second configuration absent from current item assets. Missing-renderer and forced-settlement behavior remains V034-04. |
| V034-03 | WP5 / Passed for current Multi flow | Exercise Grudge same-target contention in both request orders and T/T+1/T+2 cooldown, plus Possession's one-use lifetime through turn, round, revival and a fresh lobby match. Repeat with alternate ghost/target seats. | Three current-build real Relay scenarios and matching local runs below; four peers agree on masks, replies, turn cooldowns and spent charge. Multi currently returns to lobby after a match and has no in-scene rematch vote; that future flow is tracked separately as V034-10. |
| V034-04 | WP5 / Passed for current Multi flow | Remove a visual renderer, delay or lose presentation ACK, force settlement, and disconnect a peer during VFX. Check late ACK and queued-record handling; track any future in-scene Multi rematch separately under V034-10. | Five current-build real Relay reports with four process logs and available captures passed. The host timed out only the intentionally dropped/late ACK; late ACK was ignored and disconnect released the barrier. A focused EditMode test preserved the newer queued record after forced settlement. |
| V034-05 | WP5/WP6 / Passed for current Multi flow | Replace an active minigame with a newer ticket using a same-ID item replacement; deliver the older success/failure late and duplicate the new result. Repeat with two-survivor top-up and across a real round transition. | Three current-build local and real Relay four-peer scenarios below passed with ticket/CopyId and inventory logs plus visible A/B captures. Only B queued. Multi in-scene rematch does not exist; future rematch ticket validation is V034-10. |
| V034-06 | WP6 / Passed for tested local flow | Run 1v1 item, simultaneous defense, minigame, second-round and rematch regression plus a three-player match. | Current fixed-path build and focused tests passed. Local visible 1v1/three-player runs have separate logs and nonblack captures; a three-rematch 1v1 matrix run passed with both peers. Real Relay final-match coverage remains V034-07. |
| V034-07 | WP6 / Passed for scripted five-kill flow | Review a full real Relay Host + three-client match on all views, including ghost HUD, target selection, suppression, lethal fifth kill and results. | Current-build real Relay and local four-peer reports below passed with 17 matching checkpoints and five actual Grudge kills; four distinct UGS profiles, 37 captures per peer, no matched exception or presentation timeout. The fixture staged ghost conversion and each target's pre-hit temperature, but never added kill points directly. |
| V034-08 | Confidence / Passed for application-order injection | Inject both descriptor-before-view and view-before-descriptor delivery on real player processes, including stale input and a delayed recipient. | Both orders passed in local and real Relay four-process fixtures. The reverse fixture delays a client's consumption of the completed descriptor while its complete view arrives, rather than reordering UTP packets; arbitrary packet-level reordering is not claimed. |
| V034-09 | Polish / Passed | Recheck the crown glyph warning in the final results HUD with the intended font assets. | The HUD crown now uses a small graphic and the lobby retains its visible `[호스트]` label. Current-build local and full Relay five-kill runs have no Unicode 265B warning. |
| V034-10 | Future feature / Deferred | If Multi gains an in-scene rematch, repeat the spent-charge and stale-request checks across that transition. | Current Multi result UI intentionally returns to lobby and never enters `RematchVote`; do not introduce a new rematch flow as part of PLAN 034 validation. |

### Current recheck blockers (2026-09-27)

| ID | Status | Required check / correction | Current evidence |
|---|---|---|---|
| V034-11 | Passed / repair scope | Restore lobby transition and cleanup after unexpected host loss through the current coordinator-owned Relay path. Test remaining three clients, late callbacks and a subsequent join. | Repaired: real Relay prep/combat/terminal and 1v1 host loss, same-process fresh match, and final local 4P return passed. See final repair disposition below; historical failing reports are preserved. |
| V034-12 | Passed for captured visibility / prior finding corrected | Prove rendered result text and lobby control visibility on every peer, beyond the Visible log marker. | Fresh local and real Relay four-view captures contain identical lobby-button regions. The previous seat1/seat2 missing-control claim was a mistaken visual review: all 36 audited prior final captures also contain the control. See Remaining-defect follow-up below. Physical pointer interaction is separate deferred coverage. |
| V034-13 | Partial / resource follow-up | Classify and address local Animator warning, legacy overlay ownership, package build warnings and shutdown ComputeBuffer warning. | Local Animator routing and loading UI ownership repaired. Pipeline/unused-field warnings classified. Three A/B pairs isolate the package fallback buffer shutdown warning; a persistent package patch awaits approval. A separate particle-material leak was reproduced and repaired; repeat and real Relay forced-settlement reruns passed. See Extended validation below. |


### Additional current validation debt

| ID | Status | Exact remaining stimulus | Required evidence |
|---|---|---|---|
| V034-14 | Partial: native mouse passed, keyboard pending | While aiming at an eligible target before Ready, press physical Esc; confirm no server item selection commits and the arrow returns to idle. Also manually review result/lobby controls on each local view. | InputSystem Escape event, before/after selected state and visible arrow/control response. Automated key delivery did not generate the event; do not mark this as a product failure. |
| V034-15 | Deferred: independent hardware and long soak | Run Host and three clients on separate machines/networks; repeat joins/matches and controlled disconnects for multiple hours. | Peer checkpoint/log agreement, no stuck loading/presentation, and live resource counts after comparable idle checkpoints. Same-PC Relay and bounded repeat tests do not close this row. |

### V034-01 — Real Relay possession item-category gate

The opt-in development fixture used four distinct anonymous Unity Services profiles on an actual Relay allocation (one Host, three clients). It staged ghost seat 1, possessed living seat 2, then selected one item on seat 2 while the other survivors readied. This isolates the consume-only rule without changing release gameplay. Seven current-build cases passed after the PC restart: attack, heal, defense and multi-use in `wp5_possession_<case>_relay_20260925/`; unlimited, minigame-success and minigame-failure in `wp5_possession_<case>_relay_postreboot_20260925/`. Each directory under `output/validation/plan034/` has `report.json`, four player logs, screenshots and `gallery.html`.

All four peers in each case agreed on one result sequence, one suppressed-use event for a queued item, unchanged before/after temperatures, identical CopyId and use count, and a settled presentation without matched exceptions or timeout. One-use copies were removed (1→0); Smartphone used one charge (3→2); unlimited Fan stayed at 255 uses. The successful Hug T-shirt minigame queued a suppressed action and cue. The failed minigame consumed its item before an action was queued, so it correctly had zero suppressed-use events and no suppression cue. The initial heal run exposed only a fixture assertion error: it expected an exhausted one-use CopyId to remain in inventory. The corrected assertion and rerun passed. A defense-cue screenshot was visually inspected; the visible purple possession marker appeared, while the ghost remained the current placeholder shape.

This closes item-category cost/effect synchronization for the tested single-turn seat pair. Visual timing, alternate seats, lifecycle and failure injection remain separate V034-02 through V034-05 checks.

### V034-02 — Shared presentation schedule and Relay timing

`MultiPresentationSchedule` now stores each action's calculated duration and supplies that same value to the client presentation loop; the server uses its barrier budget. A defense-only suppressed use has zero action slots and a setup cue. A suppressed ordinary item retains its three-second action slot. The mixed fixture suppresses seat 2's Mask while seat 0 attacks seat 2 with Ice Cream: the defense cue and attack start coincide, the attack damages seat 2, and no successful block is scheduled.

The current fixed-path development build succeeded with **0 errors, 4 warnings**. `Plan034GhostLedgerTests` passed **11/11** after Unity refreshed the test assembly. Its mixed synthetic schedule asserts a 4.25-second configured action, a three-second suppressed ordinary action, zero time for suppressed defense, and an exact shared barrier budget; every current item asset has `AnimDuration: 1.5`, so no live item currently exercises a longer configured duration. On the final build, real Relay Host plus three clients passed `wp5_timing_attack_relay_schedulefix_20260925/`, `wp5_timing_defense_relay_schedulefix_20260925/`, and `wp5_timing_defense-counter_relay_schedulefix_20260925/`, each with four distinct UGS profiles and four-view logs/captures. Measured per-peer result-to-settlement times were **3.495–3.502 s**, **0.496–0.502 s**, and **3.497–3.543 s**, respectively. Every case had one ACK sequence per peer, no matched exception and no barrier timeout. The seat-2 mixed-case cue screenshot was visually inspected.

Renderer loss, delayed ACK, forced settlement, disconnect and rematch record cleanup are still V034-04, not implied by these normal-renderer timing passes.

### V034-03 — Ghost ledger contention, cooldown and round lifetime

An opt-in four-process fixture forced ghost seats 1 and 3 while seats 0 and 2 remained alive. `wp5_ledger_contention_relay_20260925/` passed the first request order and three turns: one Grudge on seat 0 was accepted while the competing hit was rejected, Possession contention spent each ghost's charge only on an accepted target, the first Grudge actor was unavailable at T+1 and available at T+2, and both charges stayed spent. `wp5_ledger_contention_reverse_relay_20260925/` passed the reverse Grudge and Possession request order. All four peers agreed on the same damage and possession masks, request outcomes, cooldowns and settlement checkpoints. The corresponding `_local_20260925/` runs passed as well. Ghost HUD screenshots in the galleries show the spent/cooldown controls; text readability still needs the general four-view visual review in V034-07.

`wp5_lifecycle_round_revival_relay_20260925/` passed the same-match lifecycle: ghost seat 1 used Possession, the round ended, all players revived, seat 1 died again, and a second Possession request returned `Unavailable`. Every peer retained the same match epoch, advanced one round epoch, cleared the turn marker and retained the spent bit. Its local counterpart also passed. The fixed-path development build succeeded with zero errors and four warnings, and the focused ledger EditMode suite passed 11/11 including `BeginMatch` resetting the charge. Fresh Relay lobby matches used distinct new authentication profiles and accepted their first Possession use. In the current Multi UI, the match-end path shows a lobby button and auto-leaves; it never enters the 1v1 `RematchVote` state. A same-session Multi rematch would change product flow, so V034-10 preserves that conditional validation instead of silently treating the 1v1 vote as Multi evidence.

### V034-04 — Presentation recovery on real Relay

Five opt-in four-process runs on the fixed-path development build passed with distinct Unity Services profiles and a real Relay allocation: `wp5_force_settle_relay_20260925/`, `wp5_missing_renderer_relay_20260925/`, `wp5_attack-drop-ack_relay_20260925/`, `wp5_attack-late-ack_relay_20260925/`, and `wp5_disconnect_vfx_relay_20260925/`. Each directory under `output/validation/plan034/` contains `report.json`, player logs, captures and `gallery.html` where available. The corresponding local runs also passed. Forced settlement at about 1.036 seconds preserved the authoritative item cost and outcome; a missing remote item renderer did not prevent the attack or cue from settling. The drop and 12-second delayed ACK cases each produced one bounded host barrier timeout, then all connected peers reached turn 2. The late ACK was ignored after that timeout. A peer shutdown during VFX released it from the barrier, and the three remaining peers reached turn 2 without a timeout. No matched exception occurred in these runs.

`CombatVFXManager.ForceSettleMultiPresentation` now settles only the active record, preserves newer queued records and resumes draining in Play Mode. `Plan029CombatRulesTests` passed 38/38, including `ForcedPresentationSettlement_PreservesNewerQueuedRecord`. The development build succeeded with 0 errors and 4 warnings, and the connected Editor reported zero console errors. The ACK fault hook and test fixture are development-only. These runs exercise late ACK while the scene remains active; a future in-scene Multi rematch has its own conditional gate V034-10 because the current Multi result flow returns to the lobby.

### V034-05 — Newer minigame ticket and stale result protection

`MiniGameHub` now accepts only a ticket newer in match/round/turn/attempt order and removes an outgoing result widget before displaying its successor. The comparison lives on the Core `MiniGameTicket`, keeping the Core/UI assembly direction intact. The focused `Plan034GhostLedgerTests` suite passed **12/12** after the new test was discovered; a fixed-path development build succeeded with **0 errors, 4 warnings**.

The opt-in four-process fixture passed locally and on actual Relay for `replacement`, `replacement-topup`, and `round`: `wp5_mini_ticket_replacement_local_capturefix_20260925/`, `wp5_mini_ticket_replacement_relay_20260925/`, `wp5_mini_ticket_topup_local_afterfix_20260925/`, `wp5_mini_ticket_topup_relay_20260925/`, `wp5_mini_ticket_round_local_20260925/`, and `wp5_mini_ticket_round_relay_20260925/` under `output/validation/plan034/`. Each report has four process logs, screenshots and a gallery. The same-slot replacement kept the Hug T-shirt ItemId but allocated a new CopyId and attempt; the server rejected both late A outcomes and a duplicate B result. B remained visible and pending through those rejections, then alone entered the action queue. The top-up variant forced two other seats to become ghosts and waited for the completed grant view before sending stale results; B's CopyId and pending UI survived. The round variant ended the first round with seat 2 alive, staged a fresh B in the next round, and rejected the first-round A outcomes. Four peers agreed on the queued B CopyId in all three Relay cases, with four distinct Unity Services identities, a real Relay allocation/join and no matched exceptions. Screenshots show the A and B minigame views; the UI uses the same art for both attempts, so ticket identity is established by logs rather than visible text.

The current Multi result flow leaves for the lobby, so there is no same-scene Multi rematch ticket to test. V034-10 preserves that future condition. An earlier local capture run had correct server outcomes but fired two screenshot requests in one frame, leaving one image absent; the capture timing was corrected and both final local and Relay reports passed.

### V034-06 — Duel and three-player regression

`Plan029CombatRulesTests` passed **38/38** on the current Editor assembly, including Duel defense metadata and remote reaction checks. The fixed-path development player built with **0 errors**. The local matrix `wp6_regression_duel_special_local_20260925/` passed Duel, three-player special, and three-cycle Duel rematch (three cases, no failed player); its host/client logs show Windbreaker defense activation and reaction during Duel, Hug T-shirt minigame success and Cat/Hug/Ice Cream actions in the three-player case, and three accepted Duel rematches. The first visible Duel minigame fixture ended a round before the ticket could start because its temperature seed was too low; it was corrected to run the Hug minigame and queue its action before inducing the round boundary. `wp6_duel_mini_visible_local_afterfix_20260925/` then passed with a captured active minigame, a server success/queue and both peers in round 2. `wp6_duel_special_visible_local_20260925/` passed visible Duel and three-player special cases with separate logs and 4/4 and 6/5/5 captured frames respectively. The screenshots are rendered game views, not batch-mode black captures. These are local multi-process regressions; they do not substitute for the full four-player Relay match in V034-07.

### V034-07 — Four-view real Relay end-to-end flow

`wp6_full_match_relay_baseline_20260925/` passed four ordinary turns on a real Relay allocation with four separate Unity Services identities and matching checkpoints. The opt-in `--full-match` development fixture then continued after those four turns: it changed seat 1 to a ghost, used Possession on seat 2, queued seat 2's Fan against seat 0, checked that the Fan was consumed and suppressed with a cue on all views, and used Grudge for the fifth kill and result. `wp6_full_match_tail_local_20260925/` and `wp6_full_match_tail_relay_20260925/` under `output/validation/plan034/` each passed; the latter has seven identical authoritative checkpoint signatures across the four peers, 22–23 screenshots per peer, one Relay allocation and three joins, four unique authenticated player IDs, zero matched errors and no presentation timeout. The captured ghost HUD, cast/impact, suppression cue, death and final result were visually inspected. Seat 1 shows **VICTORY**, while seat 0 shows **DEFEAT**.

The fixture deliberately staged seat 1's first four kills and seat 0 at 2°C after the suppression turn. This verifies the fifth-kill authority and ordered presentation in a connected match; it is not evidence that four earlier kills arose through unassisted play. The visual HUD still uses small development text and the temporary ghost presentation; these are art/readability follow-ups, not synchronization failures. The fixed-path development build succeeded with zero errors before these runs.

The stronger follow-up `wp6_five_actual_kills_local_20260925/` and `wp6_five_actual_kills_relay_20260925/` also passed. It preserved the same ordinary four-turn and Possession-suppression opening, then used **five accepted Grudge attacks** for five actual kill-score increments. The fixture converted seat 1 to a ghost at each new round and set each living target to 2°C before the hit; it did not call `ServerAddKill`. Cooldown turns, the two-survivor top-up and two round transitions occurred in between. The real Relay run had 17 matching checkpoint signatures, 37 captures on each of four unique authenticated peers, and score observations 0→1→2 in round 1, 3→4 in round 2, and 5 in round 3. Seat 1's result capture visibly says VICTORY, seat 0's says DEFEAT, and no matched error or presentation timeout appeared. This demonstrates the complete authoritative score progression under scripted target setup; free-form human play and art acceptance remain separate.

### V034-08 — Both client application orders around a grant

The existing `--az-delay-inventory-view-ms` fixture supplied descriptor-before-view evidence on real Relay for stale selection, Ready and cancellation. To exercise the opposite application order, a development-only `--az-delay-grant-descriptor-ms` fixture now keeps the client's grant-read gate at Pending while its complete, committed inventory snapshot is adopted. It logs descriptor hold, view adoption, stale command rejection and descriptor release in that order. This controls what the client read model consumes, not Unity Transport packet order; the authoritative server still publishes and validates the real completed descriptor.

`wp4_reverse_cancel_local_final_20260925/`, `wp4_reverse_select_local_20260925/`, `wp4_reverse_cancel_relay_20260925/` and `wp4_reverse_select_relay_20260925/` passed. The two Relay reports show four authenticated peers, an actual allocation and joins, matching completed inventory checkpoints, and no matched error. In the reverse window, a preselected seat's stale Cancel and Ready were rejected; an unselected seat's stale Select and Ready were rejected in the other case. The complete new view remained blocked until the descriptor release, then all four peers completed without a permanent lock. `MultiInventoryReadModel.NotifyGrantChanged` now prompts view presenters to reevaluate when a descriptor changes after a view. A first local runner report was a false failure because it assumed `client1` always owned seat 1; the runner now locates the seat by its logged identity, and the final rerun passed.

### V034-09 — Crown glyph polish

The old `♛` text produced Unicode 265B missing-glyph warnings in `LiberationSans SDF` on both the lobby label and the match HUD. The lobby already displays `[호스트]`, so its duplicate crown prefix was removed. The HUD keeps its existing text reference and visibility/placement behavior, with an empty label and a child `Image` drawing a small crown silhouette; this avoids a font dependency and preserves the serialized HUD reference layout. The current fixed-path player build succeeded with **0 errors, 3 warnings**. `wp5_crown_local_20260925/` passed a visible four-peer turn with synchronized captures and `missing_crown_glyph: 0` on every peer. No 265B warning appears in its logs.

The later full Relay five-kill run used the same crown change and also has no 265B warning in any of its four process logs.

## WP4 final logic gate and WP5 handoff — 2026-09-25

The WP4 grant/input synchronization gate now passes the focused checks below. This is acceptance of the tested transaction and Multi inventory view behavior; human review of presentation and the broader WP5/WP6 visual scenarios remain separate.

Two command paths needed the same server-side protection: after a grant committed, a client still displaying the old view could directly submit a previously valid CopyId to select an item, or cancel a preserved selection. Selection and cancellation now send the observed grant transaction from the adopted inventory view; the server rejects an older transaction as Ready already did. The UI remains closed while the view is blocked. The development-only delay hook in `MatchNetworkState` holds the first committed grant view before read-model adoption; it does not alter release builds or claim arbitrary packet-level reordering.

| Current-build check | Result | Evidence |
|---|---|---|
| Unity EditMode | **56/56 passed** | `output/validation/plan034/final_gate_20260925_editmode_after_fix.json`; includes both synthetic descriptor/view orders. |
| Development build `build_bc41472e3b60` | **Succeeded, 0 errors, 4 warnings** | `output/validation/plan034/final_gate_20260925_build_after_fix.json`; Editor compilation and current console errors: zero. |
| Local four-peer delayed view | **Passed** | `final_gate_20260925_ordering_local/matrix.json`: 2-second withheld grant envelope, completed descriptor and raw slots observed first; stale selection and Ready rejected before the envelope was released; final checkpoints agreed. |
| Local 1v1/Multi regressions | **6/6 passed** | `final_gate_20260925_regression_after_fix/matrix.json`: 1v1 duel, 2/4-player inventory, RPC guards, Multi round, minigame target. |
| Actual Relay four peers | **4/4 current-build cases passed** | `final_gate_20260925_relay_combined_after_fix`, `final_gate_20260925_relay_ordering`, `final_gate_20260925_relay_40_after_fix`, `final_gate_20260925_relay_retry_after_fix`: 3/1 with selected+Ready+active minigame, withheld view and stale commands, 4/0, and first-write failure with same-plan recovery. Each report verifies four distinct UGS IDs, Relay allocation/join, matching completion and zero matched exceptions. |
| WP5 initial visible Relay check | **Passed within nonlethal showcase scope** | `wp5_ghost_showcase_relay_20260925/report.json` and `gallery.html`: Grudge and Possession cast/impact records aligned across four visible peers, captures complete, no presentation timeout or matched exception. |

The selected/Ready/minigame fixture keeps a valid Hug T-shirt ticket through the grant and submits it afterward; the server queues that action. The delayed-view fixture proves descriptor-before-view admission rejection on real peers. View-before-descriptor is exercised in the EditMode read-model test; this suite does not control actual UTP packet ordering in both directions. The initial WP5 showcase is nonlethal; the subsequent lethal check is recorded below.

### WP5 first ordered death check

The current development build `build_be6d0d571df4` succeeded with zero errors and four warnings, and Unity EditMode passed **56/56** again. In `wp5_lethal_relay_capture_20260925/`, an opt-in test fixture staged ghost seat 1 at four kills and living seat 0 at 2°C. Its accepted Grudge caused the fifth kill. Actual Relay connected four distinct profiles. Each peer logged one Grudge cast, the VFX impact signal, death animation start, and winner mask `2` result visibility in that order. Four screenshots per phase plus final result were captured; all peers completed without matched exceptions or presentation timeout. The corrected final capture waits for the result UI to become visible, and the seat-1 screenshot visibly shows VICTORY. The report and gallery are `report.json` and `gallery.html` in that directory.

This passes the focused lethal impact→death→result ordering check for one ghost/target pair and one normal renderer setup. WP5 still needs live possession suppression timing across item categories, alternate seats, renderer absence, delayed ACK/forced settlement, disconnect and rematch cleanup. The showcase's crown-glyph warning is a UI font finding and did not affect this result.

### Final selection-cancellation follow-up

The cancellation watermark fix was built as `build_5a64208c144c` (**Succeeded, 0 errors, 4 warnings**); Unity EditMode passed **56/56** (`final_gate_20260925_editmode_cancel_fix.json`). Both `final_gate_20260925_stale_cancel_local/matrix.json` and `final_gate_20260925_stale_cancel_relay/matrix.json` passed: with the committed view withheld for two seconds, the pre-grant selected item remained selected after a stale cancellation RPC. The Relay report verified four distinct authenticated peers and coherent final inventories. The latest build also passed 1v1 duel, 2/4-player inventory and RPC guard regressions **4/4** in `final_gate_20260925_cancel_regression/matrix.json`. No matched exception or current Editor compile error remained. This closes the observed stale selection/Ready/cancellation input gap for the tested transition.

## Repeat verification — 2026-09-25

Fresh verification of the current inventory architecture completed without gameplay source changes:

- Unity EditMode: **54/54 passed**, no skipped or failed tests (`output/validation/plan034/recheck_20260925_tests.json`).
- Fixed-path development build `build_d4a88cb64986`: **Succeeded, 0 errors, 1 warning**. The warning is the absent Pipeline `RuntimePipelineConfig`; Player Pipeline automation is disabled. Evidence: `recheck_20260925_build.json` in the same directory.
- Separate local Host/client processes: **11/11 passed** in `recheck_20260925_matrix/matrix.json`: 1v1 duel, 2/4-player inventory, RPC guards, Multi round reset, minigame target invalidation, and all five 4→2 grant variants.
- Four-peer grant under **120 ms one-way proxy delay and 2% packet loss passed** (`recheck_20260925_stress/matrix.json`).
- Runner reports contain zero matched exceptions/failure markers. Completed duel, round-reset and successful grant checkpoints matched across peers. Cases without presentation CHECK markers report `checkpoints_equal: false`; this field is not a mismatch assertion for those cases. The deliberately failed grant reports matching `TOPUP_FAULT_LATCHED` inventories on all four peers and passes its restoration/admission checks.
- Editor ground truth after the build: no compilation failure, zero console errors, one warning. `git diff --check` found no whitespace errors.

No new gameplay defect was reproduced in this run. This rerun used local transport, not Relay, and does not close the actual Relay 4→2, explicit descriptor-before-view injection, combined selected/Ready/minigame-through-grant, or human presentation acceptance gaps below. No additional code fix was required by these results.

## Latest inventory architecture pass

The WP4-A vulnerability fixes are implemented. `DeathmatchGrantCoordinator` now owns the synchronous two-survivor grant, exact-plan retry and latched technical fault; `TurnManager` retains phase ownership and stops on that fault. `RandomTopUpPlan` verifies the applied values before compensation, so an independent write cannot be overwritten during rollback. Multi inventory UI reads one retained, complete network snapshot through `MultiInventoryReadModel`; the publisher coalesces server changes and carries a successful grant watermark with the latest slot values. The item-selection command and queued action validate the displayed CopyId. The existing 1v1 inventory path remains in place.

| Current check | Observed result | Evidence boundary |
|---|---|---|
| Unity EditMode | 54/54 passed | Includes installed NGO serialization of a full and default view, skipped revisions, invalid envelopes, queued CopyId, ghost/PLAN 029 regressions. |
| Development build `build_7a8c9feb6452` | Succeeded; 0 errors, 4 warnings | Current v9 source. Unity editor auto-compilation had zero current errors. |
| `inventory_architecture_final_v9` | 5/5 cases passed | Four local peers: 3/1, 4/0, automatic same-plan recovery, same-frame post-grant change, and two injected failures. In the last case each peer saw a latched fault, exact original inventories, no turn advance, and rejected selection/Ready. |
| `inventory_architecture_final_v6` | 7/7 cases passed | Host plus clients: 4/2-player inventory including guarded rollback, 1v1 duel, four 4→2 top-up variants (3/1, 4/0, injected first-recipient failure with automatic same-plan retry, and same-frame post-grant slot change). Supporting regression evidence before the final view gate. |
| `inventory_architecture_final_regression` | 4/4 cases passed | Host plus clients: 1v1 duel, Multi round reset, invalid RPC guards, and minigame target invalidation. |
| `inventory_architecture_final_v9_stress` | Passed | Current v9 build, four local peers, 120 ms one-way proxy delay and 2% packet loss on 4→2 grant. The proxy does not inject arbitrary reordering. |
| `inventory_architecture_relay_v9` | Passed | Current v9 build, actual Unity Relay allocation; four distinct anonymous identities connected, one turn settled with matching checkpoints, screenshots on all peers, no logged exceptions. This run does not trigger 4→2 top-up. |

The original cross-object fingerprint wait has been removed from Multi UI. A single snapshot can contain the completed grant watermark and a later same-frame item change; the test and four-peer variant cover this skipped intermediate state. The input gate remains closed if the completion descriptor arrives before the snapshot carrying its transaction ID. During implementation, the new publisher initially read a despawned inventory and the UI initially rebuilt during teardown; both were corrected and the affected four-peer regressions passed on the corrected builds. A transient v8 build reported an internal CLR error during an overlapping edit; the unchanged-source v9 rebuild completed with zero errors.

**Still open for final WP4/WP5 acceptance:** an actual Relay 4→2 grant; explicit descriptor-before-view delivery injection; a combined live selected/Ready/minigame-through-grant case; and human review of the captured presentation. The tests above establish the implemented paths, not those remaining cases. Prior scores below are historical and must not be treated as current final acceptance.

## Prior WP4 decision

The following section records the earlier fingerprint-based implementation and scores. The latest inventory architecture pass above supersedes its current-state claims.

Architecture review follow-up (plan only): [the application plan](../Plans/PLAN_034_inventory_architecture_application.md) identifies two additional static gaps: Failed currently releases interaction without guaranteed production recovery, and a same-frame post-grant mutation may skip the historical inventory fingerprint a client waits for. The prior passes and 89/100 WP4 assessment below describe the tested cases, not clearance of these newly identified paths. WP4 final acceptance remains blocked; P0 failure containment and the snapshot/identity cutover must pass their own gates before dependent acceptance.

WP4 now publishes a two-recipient transaction result and holds Multi inventory presentation and commands until both replicated inventories match it. Actual four-peer 4→2 transitions passed at 3/1 and 4/0 random-item starting counts, as did a forced failure after the first recipient followed by rollback and retry. WP5 implementation can proceed; final WP4 sign-off still requires an explicitly reordered descriptor/list test and a fresh Relay run. Scores below are review judgments about evidence coverage, not measured defect rates.

## Fixes in this validation pass

1. `RandomTopUpPlan.Restore` now restores only a recipient whose application began. Previously, a stale second plan could fail before writing, then its unconditional restore could overwrite an intervening inventory change. The four-client inventory probe now injects this sequence and checks both recipients.
2. `MiniGameHub` now replaces an active view when a newer server ticket arrives. Previously, the new ticket was ignored while the old view existed. The old view is disabled and its completion handler removed without submitting a result. This screen replacement itself still needs a targeted visual check.
3. The existing ghost-victory matrix fixture now places the target at 2°C. Its earlier 12°C fixture could not exercise a lethal 3°C Grudge hit or fifth-kill terminal result.
4. The inventory matrix now covers 3/1 and 4/0 random-item starting counts, exact preservation, and rollback after a stale second-recipient plan.
5. `MatchNetworkState.DeathmatchGrant` now carries a transaction, round, two seat identities, slot counts and full-slot fingerprints. The server publishes pending/completed/failed outcomes; client views and commands wait until both inventories match the final outcome, with a 10-second explicit synchronization failure path. A resolved transaction stays resolved when ordinary later item use changes a slot.
6. The local and remote inventory presenters defer their rebuilds while the grant is unresolved. The remote presenter retains dirty seats until rebuilding is safe; input commands are also gated. The development matrix has an actual 4→2 transition and a forced mid-commit rollback/retry variant.

## Executed evidence

| Check | Observed result | Scope |
|---|---|---|
| Unity EditMode | 49/49 passed; zero failed | Ledger cooldown/lifetime, consume-only item categories, defense, schedule, existing PLAN 029 regressions. |
| Latest development build `build_5fb398b8da3a` | Succeeded; 0 errors, 3 warnings | Includes the latest fixture and gameplay/UI changes. |
| Editor console ground truth | Compilation succeeded; 0 current errors | Connected Editor project path was `C:\Users\paek6\Absolute Zero`. |
| `matrix_local_2` | 5/5 cases passed | 2/4-player inventory and three 4-player minigame invalidation cases after correcting the probe's ticket capture. |
| `matrix_local_3` | 4/4 cases passed | 1v1 duel, 2/4-player inventory including 3/1, 4/0 and stale-recipient rollback, 4-player ghost service. |
| `matrix_local_4` | 3/3 cases passed | Latest UI change included: 1v1 duel, 4-player minigame target replacement and failure retry. This matrix checks server outcomes; it does not assert the visible minigame screen replacement. |
| `matrix_ghost_lethal_1` | 4/4 cases passed, no peer errors | Local Host plus three clients; ghost winner seats 0, 1, 2 and 3 each reached a fifth kill and the four peers received the terminal result. |
| `ghost_showcase_relay_1` | Passed; Relay verified, four distinct UGS identities, synchronized captures, no errors | Actual Relay Host plus three clients; nonlethal Grudge and Possession cast/impact visuals. This run predates the final top-up and minigame-view fixes. |
| `matrix_topup_sync_4` | 3/3 cases passed, four peers each | Actual 4→2 deathmatch grants at 3/1 and 4/0; injected failure after first recipient, exact rollback and successful retry. |
| `matrix_topup_stress_1` | Passed | Four peers with 120 ms one-way loopback delay and 2% packet loss; final inventories coherent. The proxy preserves packet order and does not prove arbitrary cross-object reorder safety. |
| `matrix_topup_postgrant_1` | Passed | Same impaired four-peer setup; a new item-use value after grant replicated to all peers without reopening the transaction gate. |
| `matrix_topup_regression_1` | 3/3 cases passed | Final gameplay build: 1v1 duel and 2/4-player inventory checks. |
| Latest development build `build_51d1a5f45da4` | Succeeded; 0 errors, 3 warnings | Includes the transaction gate, post-grant fixture and stale remote-binding cleanup. |
| `matrix_topup_final_1` | Passed, four peers | Final build smoke check for the 3/1 transition and post-grant item-value change. |
| `git diff --check` | No whitespace errors | Includes unrelated pre-existing worktree changes; those files were preserved. |

Local multi-process evidence: `output/validation/plan034/matrix_local_2/`, `matrix_local_3/`, `matrix_local_4/`, and `matrix_ghost_lethal_1/`. Relay captures and logs: `output/validation/plan034/ghost_showcase_relay_1/`. The original `matrix_local_1` minigame retry failures came from the probe reading a server-only ticket on a client; the corrected probe captures `OnMiniGameStart` and the rerun passed. These reports should remain available when reviewing this working tree.

## Task scores and next gate

| Package | Score | Evidence boundary |
|---|---:|---|
| WP1 — state lifetime | 85/100 | Domain tests pass; live second-round/revival/rematch reconstruction is open. |
| WP2 — ghost commands | 82/100 | Four local lethal winner seats and one real Relay nonlethal showcase pass; same-target contention, delayed retry and reconnect paths remain open. |
| WP3 — possession resolution | 76/100 | EditMode item categories and defense pass; live possession consumption, minigame success/failure, and timed suppression are open. |
| WP4 — two-survivor top-up | 89/100 | Four-peer 3/1 and 4/0 transitions, mid-commit rollback/retry and impaired delivery pass; explicitly reordered descriptor/list delivery and fresh Relay remain open. |
| WP5 — HUD and visuals | 62/100 | Relay cast/impact visuals pass; lethal impact-before-death, suppression cue timing, missing renderer and forced settlement remain open. |
| WP6 — end-to-end evidence | 70/100 | Focused local matrix and one Relay showcase pass; full four-peer Relay scenario matrix and human visual review remain open. |

Overall readiness: **82/100, conditional go for WP5 implementation**. The WP4 replication contract is implemented and passed targeted four-peer runs. Do not mark WP4/WP5 finally accepted until explicit cross-object delivery order and fresh Relay evidence are complete. The approved gameplay rules remain unchanged.

## Remaining technical work in order

1. Inject descriptor-before-list and list-before-descriptor delivery explicitly, including a delayed recipient, and verify no partially rebuilt view or accepted command. Repeat the 3/1, 4/0 and rollback/retry paths on actual Relay; the local impairment proxy only delays/drops packets.
2. Check selected item, Ready, threshold flags and an active minigame ticket through an actual 4→2 transition on all peers. The existing isolated inventory/minigame probes cover these separately, not all together.
3. Run possessed attack, healing, defense, multi-use, unlimited, and minigame cases in a full match; assert exactly one normal cost, zero new effect, and no extra defense interval.
4. Verify lethal Grudge cast impact precedes death and result on all four views, including renderer absence, timeout/disconnect, delayed ACK and rematch. Keep gameplay authority immediate even while visuals wait.
5. Repeat the critical scenario set on actual Relay with four distinct profiles, then inspect captured HUD/VFX frames and logs before marking WP5/WP6 complete.

## Fresh sequential recheck — 2026-09-27

Requested scope: run a bounded current-build validation sequence and report defects without changing approved gameplay or the paused side-sprite work. Evidence root: `output/validation/plan034/recheck_20260927/`.

- [x] Verify CLI project and existing fixed-path firewall rules; inspect diff whitespace.
- [x] Rerun focused EditMode suites: inventory 7/7, ghost ledger 12/12, combat 38/38. Raw JSON saved in evidence root.
- [x] Build current development player: succeeded, errors 0, warnings 486. BuildReport messages saved as `build-warnings.txt`: 485 AI Inference shader warnings and one missing Pipeline runtime configuration warning.
- [x] Sequential local matrix executed (11/12 passed; host-exit failed): 1v1, three-player special items, three 1v1 rematches, 1v1 minigame, two/four-player inventory, RPC guards, next round, client disconnect during prep/attack/terminal, host exit.
- [x] Local network impairment: 120 ms one-way delay and 2% configured packet loss; 3-player and 4-player state checks passed.
- [x] Actual Relay Host + three clients executed (state PASS; visual gap V034-12): four ordinary turns, Possession, five actual Grudge kills over rounds, synchronized checkpoints and captured views.
- [x] Classify warnings, inspect final screenshots/console and publish limitations; unresolved defects remain explicit.

Initial findings to verify against fresh runs: generic Multi action presentation calls the local owner's unbound third-person Animator even though the FPS controller renders that action; both gameplay scenes leave SceneLoadSyncManager.overlayRoot unassigned. These warnings are not proof of a failed attack or a loading deadlock. The separate LoadingScreenManager must be considered before changing overlay ownership. No gameplay fixes have been made in this recheck.

Observed findings (initial classification; final runtime results below):

| ID | Severity | Evidence and impact | Follow-up |
|---|---|---|---|
| R034-01 | Low | `CombatVFXManager.cs:653` calls `AZPlayerVisual.PlayCombatAnimation` even for the local owner; `AZPlayerVisual.OnNetworkSpawn` intentionally initializes FPS and returns without binding the remote Animator. The FPS call still executes. This produces a misleading warning, not evidence that remote animation is missing. | Route local and remote generic action animation explicitly; retain FPS playback. |
| R034-02 | Low / ownership cleanup | Both GameScene and GameScene_Multi serialize `SceneLoadSyncManager.overlayRoot` as zero. Its overlay warning is real; the separate `LoadingScreenManager` owns the active loading canvas. | Decide whether the legacy overlay is optional or should be wired; do not create a second overlay blindly. |
| R034-03 | Build warning debt | Current BuildReport contains 485 AI Inference shader warnings and one Pipeline runtime configuration warning; no build errors. | Audit actual inference/runtime Pipeline usage before package or configuration changes. No package upgrade/removal is authorized by this check. |
| R034-04 | Validation limitation | The older accelerated `--full-match` branch assigns synchronization from event checks without comparing full state signatures. The stronger `--full-natural` branch compares all peer checkpoint signatures. Local visual reports also set `relay_verified=true` even with `metadata.transport=local-utp`. | Use actual transport metadata and the stronger scenario; tighten report semantics before relying on the older branch. |
| R034-05 | Unclassified resource cleanup | Player shutdown logs include a ComputeBuffer garbage-collection warning. No ComputeBuffer allocation appears in Assets/Scripts; package ownership is not established by this log. | Capture allocation stack/profiler evidence before calling this a gameplay memory leak. |

The disconnect-prep run's intermediate raw inventory checkpoint differs while top-up replication is in flight, but all surviving peers have identical terminal state and winner. Its report explicitly has `checkpoints_equal=false`; its PASS means recovery/result delivery, not equality at every callback. A separate coherent-view/top-up test remains the correct evidence for UI admission during that window.

R034-06 (candidate high, awaiting real Relay reproduction): local `host-exit` peers detect ProtocolTimeout and network stop but remain in GameScene_Multi. `SessionManager.cs:233,244` requires legacy `RelayManager.IsRelayConnected`; the new NetworkSessionCoordinator uses RelayGateway/NgoNetworkRuntime instead. `NetworkSessionCoordinator.OnNetworkStopped` resets session state but contains no scene transition. This invalidates treating the historical host-exit pass as current evidence; actual Relay reproduction is queued after the local matrix.

Local matrix complete: **11/12 passed**, with host-exit failed (three client timeouts, no automatic LobbyScene return). All other selected cases passed: special-3, duel-2, disconnect-prep/attack/terminal, inventory-4/2, rpc-guards-4, multi-round-4, duel-repeat-2 (three cycles), duel-mini-2. See `recheck_20260927/matrix/matrix.json`. These cases used batchmode, so they establish process/log/state behavior rather than visual quality. Fresh current-build Relay and visible capture remain pending below.

Additional fresh results:
- `network_stress/matrix.json`: 3-player and 4-player wins both passed with matching checkpoints, 120 ms one-way proxy delay and configured 2% loss. Four-player proxy counters observed six dropped packets, zero proxy errors.
- `topup_delayed/topup-transition-4-w8/report.json`: actual Relay passed with matching checkpoints, distinct identities and verified reverse application order. A 2500 ms held descriptor rejected stale Cancel/Ready and released the view lock.
- `full_relay/report.json`: actual Relay scripted five-kill state progression passed; 17 matching checkpoints, four distinct authenticated identities, 37 existing captures per player (148 total), zero matched errors/ACK timeouts/crown warnings. Fresh missing-Animator warnings totaled 13, overlay warning totaled one.
- Visual inspection: north-target arrow, client preparation, possession cue, and all four final-result captures were opened. Seat 1 displays VICTORY; Host and seat 3 display DEFEAT. **Seat 2's final capture is black except for the debug overlay** despite its preceding `[MatchResult] Visible` log. The image cannot close all-four-view result readability. `Finish` waits one frame/end-of-frame after the log, but that alone does not establish a correct rendered frame. This remains R034-07 (medium, unresolved presentation versus capture timing); do not call the four-view visual gate fully passed from report.json alone. Central arrow/item/icebox overlap and small result controls are visual polish candidates, not proven gameplay failures.
- Real Relay host-exit reproduction: all clients logged `[SessionCoordinator] External network stop detected` followed by `[MATRIX] NET_STOP host=False scene=GameScene_Multi`. This confirms R034-06 affects the production Relay entry path, not only direct local UTP. Await final bounded timeout/report before closing this run.

No production code, scene placement, package, firewall rule, or approved gameplay rule was changed by this validation request. Residual scope: independent physical machines/networks, human mouse/Ready/cancel interaction, arbitrary packet reordering, prolonged soak/profiling, and visual art acceptance are not proven by these bounded fixtures. Future in-scene Multi rematch remains intentionally outside current product scope (V034-10).

### Final recheck disposition

Follow-up implementation plan (plan only): [PLAN_034_validation_repair.md](../Plans/PLAN_034_validation_repair.md). Tasks T1–T7 map these findings to owners and acceptance gates; no repairs are claimed by creating that plan.

All scheduled bounded runs finished. **57/57 EditMode tests passed; 17 player-process scenarios executed: 15 passed and 2 failed.** The two failures are the same host-loss recovery defect reproduced once locally and once on real Relay, not two unrelated gameplay bugs. `host_exit_relay/host-exit-4-w0/report.json` verifies actual Relay and records client timeout. No subsequent gameplay task should inherit an unconditional final approval while V034-11 is open. The full-match automated report passed state checks, but V034-12 still prevents claiming complete four-view visual validation.

Repair order: (1) coordinator-owned unexpected-stop cleanup and lobby transition, with idempotent handling and three-client/fresh-join regression; (2) instrument result canvas/text state plus delayed/repeated captures to distinguish UI occlusion from capture timing; (3) local/remote animation routing and legacy loading-overlay ownership; (4) tighten older report semantics and investigate package/resource warnings. Keep the approved ghost, item top-up and 1v1 rules unchanged.

## Repair execution — 2026-09-27 (in progress)

Scope authorized by user: implement the repair plan, test each task, then assign an evidence-based acceptance score. Acceptance threshold: 90/100 AND no unresolved high-severity defect or failed required four-view result/recovery gate. Rubric: session recovery/re-entry 35; result/animation/loading presentation 20; gameplay regression 25; report correctness 10; warning/resource disposition 10. This is a scoped engineering score, not a probability of being bug-free or a release guarantee.

Completed so far:
- Report semantics corrected: local Relay verification is N/A; accelerated full-match compares checkpoint signatures; visual review is explicitly separate. Three saved-log report checks passed (baseline, local transport, altered peer rejection).
- Coordinator unexpected network stop now uses bounded, generation-guarded LeaveAsync; exact NetworkManager subscription cleanup. Four new lifecycle tests cover duplicate stop, entry ownership, superseded cleanup and stop while Ready. Final suites currently 23/23 Plan034 + 38/38 combat = 61 passed; an initial test waited only two Editor frames and was corrected to await the bounded completion condition.
- First repaired real Relay visible host-exit passed: `repair_20260927/host_exit_relay/`. Same-process three-survivor re-entry fixture and attack/terminal variants added, pending next build tests.
- Local generic Multi animation no longer triggers the remote-only Animator. LoadingScreenManager now recognizes GameScene_Multi and clears on coordinator failure/leave; SceneLoadSyncManager's unassigned legacy overlay is explicitly optional. Its load synchronization remains intact.
- First repaired full Relay state scenario passed, 17 checkpoints. Three UI snapshots per client show active text, alpha 1 and enabled lobby button, but asynchronous PNG captures still omit some elements. Capture changed to CaptureScreenshotAsTexture at end-of-frame with explicit texture disposal; four-view visual gate remains OPEN until fresh images are inspected.
- Inference models/Worker usage were not found in Assets. Installed 2D Animation GpuDeformationSystem creates a static fallback ComputeBuffer at runtime and clears it through GPU-system cleanup; this is a candidate source of shutdown warning, not an allocation-stack-confirmed diagnosis. No package edits/upgrades or warning filtering performed. Latest incremental build: errors 0, warnings 4; the previously recorded 485 shader warnings are not claimed fixed merely because cache reduced the count.

Current output root: `output/validation/plan034/repair_20260927/`. Ongoing task: build the synchronous capture/re-entry fixture, run same-process Relay re-entry, host-loss phases/1v1 regression, final four-view Relay and focused regression. No changes to approved combat rules or side assets.

### Additional repair evidence (current build)

- Real Relay host loss passed in prep, AttackPhase, and terminal-result pending ACK, plus shared 1v1 prep loss. All intended survivors returned to LobbyScene; the stopped host is intentionally excluded from success checks. Evidence: `host_exit_relay`, `host_exit_attack`, `host_exit_terminal`, `duel_host_exit` under the repair output root. Empty/different checkpoints during forced interruption are not a normal-combat synchronization failure.
- `reentry_relay`: after Host termination, the same three surviving processes returned to LobbyScene, created/joined a fresh Relay match and reached PrepPhase. This is a new match after return, not host migration or reconnect to the abandoned match.
- `plan034-scope-tests.json`: 26/26 passed, including seven recovery tests. Together with `combat-final-tests.json` (38/38), current focused EditMode coverage is 64/64. Cleanup exception, 3-second remote timeout, late completion after a new session, and stale entry compensation are now covered. An intermediate test assembly lacked the Lobby reference; it was corrected before these tests and the successful build. The earlier 23-test run used the previously loaded assembly and is not evidence for the added tests.
- Entry compensation now captures the old lobby ID, checks operation generation before changing shared state, and avoids shutting down a newer network runtime. This fixes an additional stale-callback path found during repair review.
- `full_relay_rendered` passed state checks (17 matching checkpoints, 156 PNG files, four independent Relay identities), with zero missing-local-Animator or legacy-overlay warnings. All result texts render, but seat1/seat2 controls remain absent in sampled frames despite active/alpha/culling diagnostics. Synchronous capture alone did not resolve it. Do not close V034-12 on this run.
- `result_diagnostics` reproduced the missing controls locally. The modal result now has a dedicated ordered child Canvas and an explicit layout flush after activating its controls. This is a targeted attempted correction, pending fresh four-view visual verification; global Canvas sorting and gameplay timing were not changed.
- Latest player build (`build-modal.json`) succeeded with 0 errors and 4 warnings. Package shutdown ComputeBuffer allocation ownership remains unproven; no package/cache edits or blanket warning suppression were used.

## Final repair disposition — 2026-09-27

**Score: 87/100. Full acceptance NOT granted.** Threshold remains 90/100 plus closed required recovery and four-view visual gates. V034-11 is closed for the tested recovery scope; V034-12 remains open. No score can override that visual gate. This is an evidence/coverage score for this repair, not a probability of correctness.

| Area | Score | Evidence / deduction |
|---|---:|---|
| Session recovery and fresh entry | 34/35 | Real Relay host termination in prep, attack and pending terminal; shared 1v1 loss; same three survivors start a fresh match; local 4P return; seven lifecycle tests. Independent physical machines and exhaustive entry-failure permutations remain outside this run. |
| Result, animation and loading | 12/20 | Tested animation/overlay warnings resolved and all result texts visible; some rendered lobby controls remain absent, so V034-12 blocks acceptance. |
| Gameplay regression | 25/25 | 38 combat + 19 existing Plan034 tests + seven new recovery tests; nine selected process regressions; actual Relay 17 matching checkpoints, Possession/Grudge/five real kills; final impaired 4P win. Bounded fixtures, not every item/input permutation. |
| Report accuracy | 10/10 | Accelerated path compares peer checkpoints, local Relay is N/A, altered peer report is rejected; visual acceptance explicitly separated in JSON/gallery. |
| Warning/resource disposition | 6/10 | Four final build warnings classified. Shutdown ComputeBuffer warning remains; allocator stack and prolonged resource growth were not established. |
| **Total** | **87/100** | **Do not call the complete repair approved yet.** |

### Final evidence

All paths below are under `output/validation/plan034/repair_20260927/`.

- `plan034-scope-tests.json`: 26/26 passed. `combat-release-tests.json`: 38/38 passed. Total **64/64**.
- `regression/matrix.json`: **9/9** passed: 3P specials, 1v1 rematch, 4P client loss during attack, 4P/2P inventory, RPC guards, multiple rounds, three consecutive 1v1 rematches and 1v1 minigame.
- `host_exit_relay`, `host_exit_attack`, `host_exit_terminal`, `duel_host_exit`: real Relay recovery passed. `reentry_scope`: repeat fresh Relay re-entry passed after stale-operation guards. Transport detection delay is distinct from the bounded 3-second remote cleanup wait; this is not instantaneous host migration.
- `full_relay_rendered` and `full_relay_modal`: real Relay state checks passed, 17 matching checkpoints and 156 image files each. The modal run has zero matched errors, missing-Animator, legacy-overlay, duplicate-minigame and presentation-timeout findings. **Visual control failure remains**. The attempted dedicated result Canvas/forced layout change was removed after failing to correct it; do not inherit a visual PASS from either report.
- `build-final.json`: final source builds successfully, errors 0, warnings 4 (Pipeline runtime config absent, three existing assigned-but-unused fields). Runtime Pipeline tooling was not enabled merely to silence its warning; production Relay does not require it. Previously observed inference shader warnings are not claimed fixed by incremental cache.
- `final_local_host_exit`: final build local 4P return passed. `final_duel`: final build 1v1 match/rematch passed, missing-Animator warnings 0 for this tested path. Final VFX edit only avoids an unused local remote-Animator call; FPS rendering and timing remain intact.
- `final_network_stress`: final build 4P winner/checkpoint test passed with configured 120ms one-way proxy delay and 2% loss. This is local UTP impairment, separately from actual Relay tests.
- `report-semantics.json`: three saved-log checks passed. PNG placeholders were used only to exercise file-presence checks in a temporary directory; they are not rendering evidence.
- Editor console queried after final build: 0 returned Error entries. `git diff --check` passed (line-ending conversion warnings only). No commits/pushes, package upgrades, scene repositioning, firewall modifications, or changes to approved ghost/top-up rules.

### Remaining work, in priority order

1. **V034-12 — mandatory:** compare OS-window pixels against end-of-frame captures while retaining the affected result screen, inspect control mesh/material/clipping and raycasts, fix the proven owner, then verify all four controls plus manual/automatic leave and 1v1 rematch. Logs showing active/alpha/cull are insufficient.
2. **V034-13 — resource follow-up:** capture native allocation/profiler evidence for the shutdown ComputeBuffer warning and repeated scene/match memory growth. Installed 2D Animation fallback buffer is a candidate, not a confirmed cause. Do not patch package caches or disable features blindly.
3. Independent machines/networks and manual pointer/Ready/cancel/visual acceptance remain deferred. Future in-scene Multi rematch remains intentionally outside the approved scope.

The host-loss gameplay blocker is repaired. Progress on independent gameplay tasks may use the passed state/recovery evidence, but full PLAN_034 repair acceptance remains blocked by item 1.

### Final proxy diagnostic clarification

The first final impairment report contained three proxy socket errors despite matching game checkpoints. Added bounded error details to `matrix_udp_proxy.py` rather than hiding the counter. `final_network_diagnostic/win-4-w0/report.json` repeated the same 120ms/2% test: state PASS, six deliberately dropped packets, one receive-side Windows 10054 connection-reset event, other peers zero proxy errors. This is a proxy/socket observation; exact timing relative to player shutdown was not captured, so it is not claimed fixed or proven harmless teardown. The gameplay progression passed despite it. A future proxy reliability/soak investigation should timestamp reset events and peer exits. Do not summarize this as zero transport errors.


## Remaining-defect follow-up — 2026-09-27

This section supersedes the earlier missing-control claim and its 87/100 acceptance deduction. Historical reports above are retained as an audit trail; they must not be used as the current V034-12 status.

### Corrections implemented

- Windows proxy configuration previously depended on `socket.SIO_UDP_CONNRESET`, which this installed CPython does not expose. The branch silently did nothing. The loopback test proxy now applies the documented Winsock IOCTL explicitly, validates its result, and retains reporting of other socket errors. This changes the impairment harness, not the production Relay transport. Reference: [Microsoft Winsock IOCTLs](https://learn.microsoft.com/en-us/windows/win32/winsock/winsock-ioctls).
- Added a regression that sends ten packets to a closed endpoint, reopens that endpoint, and proves a bidirectional roundtrip with zero errors. A second test proves unrelated socket errors remain recorded. Both passed.
- Matrix reports now fail overall when the impairment proxy has errors; matching game checkpoints alone cannot turn a broken harness into PASS. Empty scenario selections now exit with code 2 instead of reporting vacuous success.
- Detailed UI geometry logging is development-only and opt-in with `--az-render-diagnostics`. Native allocation tracing is likewise opt-in for matrix runs with `--leak-diagnostics`; neither is a production gameplay feature.

### Visibility finding corrected

The earlier claim that seat1/seat2 result controls were absent was an incorrect image interpretation. Pixel analysis of all 36 saved terminal captures from `full_relay_rendered`, `full_relay_modal`, and `result_diagnostics` finds the brown control in every image. Fresh local and actual Relay four-view captures have identical button-region SHA-256 values and 2,047 brown button pixels in the rectangle `(477,269)-(575,291)`. Geometry confirms a four-vertex button mesh, normal UI material/color, and correct depth. No further Canvas/layout workaround was added.

This closes the captured-visibility defect; it does not claim that an OS mouse click or independent-machine display was tested. Earlier incomplete-frame observations must be distinguished from the corrected later button claim.

### Fresh evidence

Root: `output/validation/plan034/remaining_20260927/`.

- `tests-plan034.json`: 26/26; `tests-combat.json`: 38/38. Total 64/64 EditMode checks.
- `build-final.json`: successful player build, errors 0, warnings 2. Warning count varies with incremental compilation; the previously observed package warnings are not claimed repaired by cache reuse.
- `proxy-roundtrip.json` and `.codex/scripts/test_matrix_udp_proxy.py`: closed-endpoint recovery and preserved error reporting passed.
- `empty-selection-test.json`: unmatched scenario rejected before any player launch.
- `network_stress/matrix.json`: four-player attack-time client termination with 120ms one-way delay and 2% configured loss passed; 43 packets deliberately dropped across proxies, zero proxy errors.
- `result_geometry/win-4-w1`: local four-view winner/result state passed. `four-view-buttons.json` verifies the button region on all four captures.
- `result-pixel-audit.json`: 36 historical frames audited, correcting the earlier review.
- `final_relay/matrix.json`: final-build real Relay, four distinct authenticated identities, matching winner/checkpoints, four result captures passed. `final-relay-buttons.json` verifies identical button regions.
- `leak_probe/matrix.json`: four-player state scenario passed with native allocation stack tracing enabled. Shutdown ComputeBuffer warnings still occur in all four logs; no allocator stack was produced. This diagnostic did NOT repair the warning.

### Still unresolved

V034-13 remains open for ComputeBuffer allocation/disposal ownership and repeated-session memory profiling. Installed 2D Animation's fallback buffer remains a source-code candidate, not a confirmed allocator. No package-cache modifications, dependency upgrades, disposal-by-reflection workaround, or warning suppression were introduced. Independent machines/networks, physical pointer interaction and prolonged soak remain deferred. No new combat-rule defect was reproduced in these bounded tests; this is not a guarantee that every gameplay bug has been removed.


Final follow-up checks completed: `final_duel/matrix.json` passed the two-player match/rematch regression. `final_proxy_gate/matrix.json` passed the final four-player win with 120ms/2% impairment under the stricter proxy gate (`proxy_healthy=true`, all socket-error counters zero). Editor console returned zero Error entries; `git diff --check` returned no whitespace errors (existing line-ending warnings only). No commit or push was performed.


## Extended validation — 2026-09-27 (bounded execution complete)

Evidence root: `output/validation/plan034/extended_20260927/`. A/B lobby-only experiments identify 2D Animation 13.0.5's static fallback buffer (64 x 64 bytes = 4,096 bytes) as the shutdown warning source: three observe-only runs each warn once; three runs calling that package's cleanup only at process exit warn zero times. The buffer identity/size stays fixed during each observed run. The test hook is development-only, explicitly opted in, and is NOT production cleanup. A persistent three-line package patch proposal is prepared; embedding the package awaits the user's answer. No dependency was changed.

Expanded matrix initially passed 13/14. `joint-4-w0` timed out because its obsolete fixture called skill 1 (now Possession) and awaited the removed Chill Aura cooldown. It is a stale-test failure, not evidence to change Possession or make sequential Grudge requests simultaneous. The replacement development-only fixture applies two attributed deaths in one synthetic effect group, then uses production winner-mask calculation, terminal presentation, ACK and result publication. `joint_fixed/matrix.json` passed and both scores reached five. This synthetic input tests joint-result plumbing; the existing EditMode subset tests separately cover the threshold rule.

The Relay full-match fixture passed with 17 synchronized checkpoints and actual Grudge kills. Its ordinary lead-in is hardcoded to four turns; passing `--turns 20` did not extend that lead-in. Metadata now explicitly records this distinction, and a separate ordinary-turn run is planned. Do not report that full-match run as 20 ordinary turns.

Initial physical-input attempt was interrupted by the normal preparation deadline and automatic temperature deaths. Its idle-connection PASS does not prove item input. A separate development-only slowed-time input session is being prepared, with native mouse-click logs; production timers remain unchanged.

### Confirmed particle-resource defect and repair

Repeated 1v1 rematches reproduced orphaned particle materials: client `material_binding/duel-repeat-2-w0/client1.log` ended with 18 HitEffectMat instances and six IceBreakEffectMat instances absent from every renderer's shared-material bindings. Authored particle StopAction.Destroy bypassed pool return; per-instance material copies had no owner.

Runtime pool instances now override stopAction to None. RuntimeMaterialOwner tracks only explicitly cloned materials and provides idempotent disposal; pool eviction/destruction invokes disposal, with OnDestroy as a lifecycle fallback. Forced presentation settlement returns checked-out particles after cancelling return coroutines. Imported materials and prefab assets are unchanged.

The first two new EditMode tests failed because they expected runtime MonoBehaviour destruction callbacks in edit mode. The corrected tests exercise the explicit pool-disposal contract, repeated disposal, shared-source preservation and stopAction override; they do not claim to exercise Play Mode lifecycle. Final `tests-particle-repair-final.json`: 28/28; `tests-combat-final.json`: 38/38. Development build `build-particle-final.json`: success, zero errors, four warnings. Actual player teardown and repeat counts are verified separately.

### Native pointer coverage

`pointer_slow` records real mouse target selection and same-item server cancellation. `pointer_ready` records target selection, Ready and a subsequent same-item click without server selection cancellation, proving the tested Ready lock. Both are standalone development players with timeScale=0.1 and a bounded 180-second input fixture. An idle-session PASS alone is not input evidence. Automated Escape key delivery produced no InputSystem Escape event even after foreground activation: keyboard cancellation remains unverified, not a confirmed gameplay defect. The regular game timing was not changed.

### Resource rerun and ACK injection results

- `material_fixed/matrix.json`: three consecutive 1v1 rematches passed. `material-comparison.json` preserves before/after census. Final client unbound particle copies: 18 HitEffect + 6 IceBreak before, zero after. Final client material count: 67 before, 47 after; host 44 in both. No claim that all Unity memory must return to its pre-load baseline.
- `ack_fixed`: twelve-second uplink hold did not reach the actual 19.6-second presentation budget. Gameplay/checkpoints passed, but the expected timeout/late-ACK markers were absent, so the report correctly failed the injection gate. This is insufficient stimulus, not a reproduced gameplay defect.
- `ack_timeout/matrix.json`: twenty-five-second hold passed; host timed out, ignored late ACKs and all four peers reached the same winner state. Proxy error counters remained zero.
- `ordinary20`: real Relay state/checkpoints passed (20 presentation sequences over two rounds, including death-only sequences; **not 20 combat turns**). All four final resource samples had zero unbound particle copies. Overall report failed the capture gate: preparation filenames repeated when the round reset, and its old fixed two-images-per-presentation formula incorrectly counted death-only sequences as turns. Preserve this failed report.
- Capture filenames now include a monotonic per-process index. Ordinary-flow capture verification checks unique recorded files, nonempty files, preparation/final frames and each settled sequence. Five Python contract tests cover the valid death-only case and overwritten/missing/empty evidence. Placeholder bytes in these tests are not rendering evidence. A fresh Relay run verifies actual images separately.

### Final bounded-run disposition

- `ordinary20_fixed/report.json`: PASS, real Relay with four distinct authenticated identities. **20 synchronized presentation checkpoints: 17 combat and three death-only presentations across two rounds**; approximately 376 seconds on Host. All 164 image filenames are unique within their peer capture records, and final helper revalidation passed on all four peers. The running command had loaded the earlier equivalent inline capture validator; the current extracted helper also passed against its saved records. Four final views were inspected: game scene, relative character/ghost placement, item rows and HUD are present. This is sampled image review, not approval of every animation frame.
- Zero matched exceptions, missing-local-Animator, crown glyph, loading overlay, duplicate minigame-result or presentation-timeout findings in that ordinary run. All four final resource samples show zero unbound HitEffect/IceBreak copies. Total allocations can increase with loaded assets and diagnostic captures; this is not a proof that all resource types are leak-free.
- `force_final/report.json`: real Relay forced-settlement Possession test passed with matching suppression/state/timing checks and four-view captures. `final_joint/matrix.json`: joint winner terminal gate passed on the final player build.
- Latest development build `build-evidence-final.json`: zero errors, four classified warnings. Final source EditMode scope remains 28 Plan034 + 38 Plan029 = **66/66**. Python capture/proxy contracts: **7/7**. Editor console read returned zero Error entries; no console clear was used. Tests and players finished; no test processes intentionally left running.
- `final-summary.json` indexes the final status. Existing failed reports are retained: obsolete joint fixture, EditMode lifecycle assumption, insufficient 12-second ACK hold and capture overwrite/count assumptions. Corrected runs are separately named; they were not rewritten as initial passes.

Project-owned fixes in this bounded scope pass their regressions. **No unconditional bug-free/release claim.** V034-13 still needs a decision on the prepared package-owned shutdown patch; V034-14 needs physical Escape/manual acceptance; V034-15 needs other machines and multi-hour soak. No package/cache, scene layout, art, firewall, or approved gameplay-rule changes were made. No commit/push.
