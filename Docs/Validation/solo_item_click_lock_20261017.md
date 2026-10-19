# Solo item click lock — 2026-10-17

## Latest conclusion — 2026-10-19

The user narrowed the failure to selecting a mini-game item: its UI sometimes does not start, and inventory input remains blocked. A concrete round-rollover defect was reproduced through the live Solo server/RPC/UI path, without destroying or disabling any UI:

1. In round 1, turn 2, start and finish a Hot Pack mini-game. The UI remembers attempt 1, turn 2.
2. End that round through the normal TurnManager transition. In Solo and ordinary Duel, mini-game network match/round epochs remain zero, while the turn resets to one and the bound player's attempt counter continues increasing.
3. In round 2, turn 1, select another mini-game item. The server starts attempt 2. `MiniGameTicket.IsNewerThan` formerly compared turn before attempt, so the UI rejected the newer request as older than turn 2. The inventory stayed `AwaitingServer` with no running mini-game.

**Fixed:** compare match/round epochs first, then the monotonically increasing attempt ID. Do not use the resettable turn number to order starts within the same epoch pair. Server ticket validation still checks turn, copy, ownership, scope, deadline and one-time result consumption. No RPC format, balance value, asset, or game rule changed. The earlier expiry recovery remains in place.

| Requirement / source | Owner | Validation |
|---|---|---|
| User report; `Docs/MINIGAME_SYSTEM.md` server-start → owner UI flow | `PlayerState.MiniGameTicket.IsNewerThan`, `MiniGameHub.HandleStart`, `MiniGameAttemptTracker.Begin` | Actual missing UI and pending input reproduced before the change; same round transition opens the UI afterwards |
| Reject duplicates and stale starts, retain Multi epoch isolation | Ticket comparator / existing server attempt tracker | Six new regression cases; old code fails five, guarded code passes all; existing epoch tests retained |

### 2026-10-19 results

Evidence: `C:/Users/paek6/AbsoluteZero-SoloIntegration/output/solo-item-click-audit-20261019`.

| Check | Observed result | File |
|---|---|---|
| Pure ticket order before fix | New-round attempt incorrectly rejected; stale prior-round ticket incorrectly considered newer | `ticket-order-before.json` |
| Focused regression before fix | 13/18 pass; **5 expected failures** demonstrate the ordering defect | `focused-before.json` |
| Real Solo rollover before fix | Round 2 / turn 1: server pending 2, UI inactive, selection AwaitingServer; client still remembers attempt 1 / turn 2 | `round-rollover-before.json` |
| Full Editor regression after fix | **1,363/1,363 passed**, no skipped/inconclusive tests | `all-editor-after.json` |
| Real Solo rollover after fix | Round 2 / turn 1: UI opens for attempt 2; its modeled result clears pending and returns selection to Idle | `round-rollover-after.json` |
| All enabled mini-game factories after rollover | **9/9** actual UI factories start and server results release input: Water Gun, Hug T-shirt, Smartphone, Hot Pack, Buldak Noodles, Screwdriver, Claw Machine, Blue Tape, Red Card | `round-rollover-after.json` |
| Visual check | Captured round 2, visible Hot Pack mini-game overlay, progress counter and timer; inspected directly | `round2-mini-visible.png` |

The runtime checks are controlled live Editor Solo sessions: item grants, modeled mini-game failure results, temperature resets, and a temperature-zero round-ending stimulus are explicit. Round/turn fields and mini-game UI were not directly reset or destroyed. This is not a natural full match or remote-client verification. Both original/main and SoloIntegration have the same gameplay fix and equivalent test source (existing line endings differ). Current `PlayerState.cs` SHA-256: `C33CEC49CEC385FE23D47A533C07513E4EC372D36160D2F64F6951EF5A8FFB79`.

The connected SoloIntegration Editor was initially in Edit Mode; only this audit's owned test sessions were started/stopped. Existing dirty font/harness/threshold work was retained. Existing Desktop builds/ZIPs were not replaced; no commit, push, merge, or deployment was performed.

## Earlier conclusion — 2026-10-17

The user's report is that every item sometimes becomes unclickable for approximately one turn. Ordinary selection/cancellation and mini-game completion did not reproduce that uncontrolled occurrence. A controlled missing-result case reproduced the same lock: removing the active mini-game UI without its result leaves the server attempt pending and the inventory in `AwaitingServer`, even after the ticket expires. The next turn used to release it.

The expired-attempt recovery is fixed. The reason the user's original mini-game result/UI might have been lost is **not established** by this fault-injection test.

User follow-up: the reported lock occurs during a particular turn and is **not immediately after a mini-game**. The expired-mini-game fix is therefore a separate demonstrated recovery defect, not an established explanation or resolution of the user's report. SIC-01 remains open. The connected Editor was verified again as `C:/Users/paek6/AbsoluteZero-SoloIntegration`. A bounded read-only live observer captured RoundOver/lobby activity, not a blocked Prep occurrence. A read-only pause capture is armed for the user's requested handoff; it preserves recent Dynamic mouse events and all current item/input gates without synthetic input, automatic pausing, or stopping the user's session.

## Change

- `Assets/Scripts/Core/Player/PlayerState.cs`, `Update`: expire pending human mini-game attempts using the existing authoritative ticket deadline. Use the existing cancellation/owner-rejection path to release input.
- Preserve the existing inclusive deadline, 0.5-second grace, ownership checks, Ready behavior, valid result handling, and item-use rules. An absent result is invalidation, not an invented failure that consumes another use.
- Identical source was applied in the original main checkout and `C:/Users/paek6/AbsoluteZero-SoloIntegration`. Historical 2026-10-17 `PlayerState.cs` SHA-256: `86D5D5F5AF5177EBAFFC563A584D5B99BC6D2A806A43376B59E6EE4C06885908`.
- Existing immediate temperature-threshold reward changes and unrelated dirty files were preserved. No build package, commit, push, or deployment was produced.

## Evidence

Evidence directory: `C:/Users/paek6/AbsoluteZero-SoloIntegration/output/solo-item-click-audit-20261017`.

| Check | Observed result | Evidence file |
|---|---|---|
| Editor tests on the guarded source | **1,357/1,357 passed**, no skipped/inconclusive tests | `all-editor-tests-after.json` |
| Loaded compiled guard | `PlayerState.Update` calls `IsWithinDeadline`; compilation complete | `compiled-guard.json` |
| Pre-fix Advanced Solo input | 21 virtual mouse clicks through production input/raycast, selection/cancel, injected mini failure, reward, menu and 3 turn transitions | `mouse-probe-result-02.json` |
| Pre-fix Beginner Solo input | 28 virtual mouse clicks, injected failure, natural timeout, reward, menu and 5 turn transitions passed | `mouse-probe-result-03.json` |
| Rendered artwork centers | 8/8 sampled item centers hit their own item collider | `art-hit-probe-02.json` |
| Missing-result baseline, original code | Actual 7-second ticket; after waiting 8.2 seconds, pending attempt remained, Fan remained blocked in Prep; next turn restored input | `orphan-probe-before-02.json` |
| Missing-result regression, guarded code | Same fault releases in the **same Prep turn** after expiry; Fan selectable/confirmed again; item uses retained; stale result cannot clear a new selection or a fresh same-copy retry; ordinary retry failure releases input; next turn works | `orphan-probe-after-02.json` |
| Post-fix normal input regression | 28 virtual mouse clicks, ordinary UI timeout, injected failure, reward during selection, menu close and 5 turn transitions passed | `mouse-probe-result-04.json` |

The input probes use real Unity input-system mouse events and the product raycast/selection path. They are controlled live Editor Solo sessions, not complete natural matches or human mouse recordings. Hot Pack copies and temperature changes are explicitly injected in relevant probes. The orphan probes intentionally destroy the mini-game view without delivering its callback; they do not prove that normal gameplay destroys that view.

The first guarded full Editor run preceded the corrected baseline comparison. The guarded source was restored byte-identically afterwards, compiled again, and used for the passing runtime regressions.

## Retained unsuccessful/inapplicable attempts

- `selection-probe-result.json`: handler-level probe only. Its `mini-failed` label is invalid as failure evidence: the first reflection lookup omitted the internal method and the mini-game was still active.
- `mouse-probe-result.json`: virtual input did not reach an unfocused Editor game view. Repeated after focusing the game view; this is not a product lock reproduction.
- `art-hit-probe.json`: inspection started after the preceding session had already returned to the lobby. Repeated as its own session.
- `orphan-probe-before.json` and `orphan-probe-after.json`: the initial fixed 6.5-second wait was shorter than the actual asset's 7-second limit plus grace. Their `past-item-deadline` label must not be used as expiry evidence. The `-02` probes read the actual ticket duration and wait beyond it.
- Failed diagnostic snippet compilations were corrected before execution. No production state was mutated by those compilation failures. Original logs/results were retained.

## Remaining evidence limits

| ID | Status | Exact remaining observation |
|---|---|---|
| SIC-01 | Partially resolved | The 2026-10-19 report now identifies mini-game selection. A real round-rollover start-suppression cause is reproduced and fixed, rather than simulated UI loss. The exact original uncontrolled user occurrence was not frozen; capture it if a different lock still recurs. |
| SIC-02 | Not run for this patch | Repeat expired human-ticket recovery with a remote owner in Duel and Multi. The shared server method changed, but this audit's runtime evidence is Solo only. |
| SIC-03 | Not built | Rebuild the user's executable to include this source fix. Existing Desktop/ZIP/release executables were not replaced. |
| SIC-04 | Closed for controlled rollover | Actual Solo round 1 / turn 2 → round 2 / turn 1 mini-game start and all nine enabled mini-game factory/result paths pass after the ordering fix. Remote Duel/Multi and a natural full match are not claimed. |

No conclusion of “all possible item click bugs eliminated” is made.
