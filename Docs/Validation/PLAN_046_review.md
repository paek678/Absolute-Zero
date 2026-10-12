# PLAN046 - Design feasibility review

Date: 2026-10-06. Scope: review and plan amendments only.

Historical planning snapshot. Implementation was subsequently authorized and completed; current evidence and the single deferred-acceptance ledger are in [PLAN046 results](PLAN_046_results.md). Correction to R05: OpponentBarPresenter actually positions bars in Update, so the implemented marker refresh uses LateUpdate.

## Verdict

**Design readiness: pass after plan corrections.** The four requested visual changes fit existing ownership boundaries without changing combat timing, item rules, cosmetic serialization or RPC contracts. Implementation and runtime acceptance remain pending. No numerical score substitutes for these gates.

## Findings incorporated into the plan

| ID / impact | Evidence and trigger | Required correction | Status |
|---|---|---|---|
| R01 / medium | MatchHudPresenter.SetAlarmActive returns if the graphic is null; OnDisable only clears offsets. Removing the banner alone can silence warnings, while disabling a presenter can leave audio active. | Separate audio lifecycle, guard callbacks while disabled, reconcile on enable. | Plan corrected; code not changed |
| R02 / medium | ClosetView indexes CosmeticPart from the tab array; ClosetViewBindings requires five references. Removing/reindexing the entry would remap Tail or fail validation. | Retain all five internal entries, hide only Bottom, preserve navigation and prefab overrides. | Existing plan strategy confirmed |
| R03 / medium | GameUIManager uses a collider-based target snap anchor. Moving it to the new marker position would change target acquisition. | Separate input anchor from display anchor; use actual head/hat and thermometer screen bounds. | Plan corrected |
| R04 / medium | GameUIManager input-blocked Update returns before marker refresh; OnDisable does not cancel targeting. Merely replacing the renderer does not establish cleanup. | Explicit disable/menu/block handling and fresh target reconciliation; no stale graphic or hidden click target. | Plan corrected; runtime reproduction not claimed |
| R05 / medium | OpponentBarPresenter updates world-space bars in LateUpdate; old arrow uses its own screen canvas and discards projected depth. | Order display update after bar layout; preserve depth/viewport checks and use the same eligibility for rendering and confirmation. | Plan corrected |
| R06 / medium, validation | InventoryInteractionProbe.Choose uses modulo RequiredPlayerCount to select a target. This does not model sparse seats after a departure. | Select a real live seat for departure scenarios; cover all local/remote pairs, real mouse input and server target result. | Plan corrected; existing probe not modified |
| R07 / low | The imported UI_target resource depicts a bullseye; full closet setup replaces existing objects. | Use a downward marker matching the request, and narrow prefab edits instead of rerunning full setup. | Plan clarified |

These are implementation pitfalls and validation gaps identified during planning, not seven newly reproduced gameplay failures.

## Checks actually performed

1. `git status --short`: unrelated graph/font modifications retained; only PLAN046 documentation changed by this task.
2. Source review: HUD builder/refs/presenter and audio lifecycle; closet view/bindings/navigation/setup; targeting state/presenter; opponent-bar mapping; LocalPlayerCommandAdapter -> PlayerState.SelectItemServerRpc -> PlayerActionAdmission.
3. Server admission retains owner/sender checks, Prep/Ready restrictions, physical item CopyId and match/round/grant guards, and current live registered target checks. No changes to those contracts are needed for the proposal. This is source evidence, not an adversarial network test.
4. `unity status --format json`: one ready Editor for `C:\Users\paek6\Absolute Zero`, Unity 6000.3.11f1, PID 17936, port 7800.
5. Read-only MCP eval verified the exact project and loaded `LobbyScene`, Edit Mode, scene not dirty, not compiling, compilationFailed=false.
6. Read-only live closet inspection: one ClosetPanel linked to `Assets/Prefabs/UI/Closet/ClosetPanel.prefab`; Validate=true; five activeSelf=true tabs; Automatic navigation. Thus hiding Bottom is still required and the existing binding contract is valid.
7. Called existing pure `AZPlayerVisual.GetRemoteVisualSlot` for every distinct pair of four seats: **12/12** returned valid, unique slots per local seat. No scene mutation. This confirms reusable seat mapping only, not arrow rendering, replication or disconnection behavior.
8. MCP console status: errors=0, warnings=6, compilationFailed=false. Warning bodies were not reclassified in this review; this is current baseline health, not compilation of the proposed changes.

No Play Mode switch, game build, Relay allocation or gameplay test was performed for this planning request. No product assets/code were changed.

## Deferred acceptance ledger

V01-V09 moved to [the implementation results](PLAN_046_results.md#remaining-acceptance-ledger-single-source). That ledger distinguishes observed passes from outstanding edge cases; do not use this historical review as runtime evidence.
