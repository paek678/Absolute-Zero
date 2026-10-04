# PLAN 043 — Escape menu, departure and circular seating

Scope/decisions: [PLAN043](../Plans/PLAN_043_escape_departure_seating.md). Status (2026-10-04): **implementation and scoped automated validation passed**. This is not a claim of exhaustive testing across physical PCs or network conditions.

## Changes

- All game modes receive a Korean Escape modal with Continue / Return to Lobby. No time-scale or server timer changes. Local input suppression includes the closing frame, item/Ready/ghost intents and in-progress mini-game drags; mini-game clocks/results keep running.
- `MatchDepartureController` delegates cleanup to the existing session router. Host departure ends the session (explicitly approved); Duel drops below two, Multi below three connected human participants end for everyone. Connected ghosts count. Four-to-three remote departure continues.
- Departed Multi seats become inactive/timed-out, rather than offline ghosts revived by next-round reset. Despawn hides the still-owned scene character and its attachments; remaining IDs/positions retain holes.
- Multi projection now uses fixed cyclic offsets; Duel/Solo explicitly retain opposite slot zero. Character, targets, inventory and temperature/HUD routes share that mapping. No authored map coordinates changed.

## Executed evidence

Root: `output/validation/plan043/`. Dirty baseline preserved in `baseline-sha256.json`; user's modified font backed up before building.

- Focused Editor suite: 9/9 initially. Final full suite including the added alive/ghost archived-seat tests: **1017/1017**, failed/skipped/inconclusive zero (`editor-tests-final.json`). The final diagnostic-only result-screen scenario was subsequently imported, compiled and run in build5; product code did not change after that full suite.
- Local standalone probes: Solo ESC/open/clock/continue/close/leave passed; four-to-three remote departure, next combat/turn, then three-to-two all return passed (`local-first/`). Probe injects InputSystem keyboard/mouse events into actual UI. Scene starts through existing lobby or local validation session, not a mocked router. Final Solo repeat also passed (`relay-final/solo-2-exit0`, local host, no Relay).
- Actual Relay: **11 menu/departure runs, 39 peer-process observations**, all passed. Allocation, client Relay joins and distinct anonymous authentication IDs are checked from logs. Host is authoritative in every run. Files only coordinate diagnostic actors; they do not synchronize gameplay state.
- Actual Relay input regressions: **Duel, 3P and 4P passed**, 9 peer-process observations and **589 input assertions** (`input-final/matrix.json`). Covers item click/arrow target identity, Escape/menu/cancel, same-item and right-click cancel, duplicate item copy identity, mini-game retry, confirmation and Ready lockout. `checkpoints_equal=false` in these input reports means no completed-combat comparison checkpoint was collected; this case's pass criteria are the actual input checks, not combat state equivalence.
- Menu probes across local and Relay runs recorded **1062 assertions** (`execution-summary.json`). This count includes repeated observations and reruns, not 1062 distinct gameplay features.
- Captures are actual camera and Canvas **diagnostic renders** from the standalone, not native desktop screenshots. Menu image reviewed for Korean readability and buttons; departure image confirms the empty character slot.
- Final development build: `build_addb99f31816`, **Succeeded, 0 errors, 1 warning** (`build-5.json`). The warning is missing Pipeline RuntimePipelineConfig (runtime CLI disabled); tests operate through the built-in opt-in probes. Build4 executed mini-game, round reset, host exit, Solo and input cases; build5 adds only the result-screen probe. Build3 executed the first Relay suite. No stale/uncompiled-code warning in these three accepted builds.

### Relay menu/departure cases

| Evidence folder | Cases | Result |
|---|---|---|
| `relay-first/` | Duel client exit; initial 3P client exit; 4P remote exit then 3→2; attack-phase remote exit; ghost remote exit; host exit | 6/6 passed |
| `relay-final/` | Mini-game exit at 3P and 4P; 4→3 then natural deaths/next round and 3→2; host exit repeat | 4/4 passed |
| `result-final/` | Real ghost skill reaches fifth kill, result UI becomes visible, host opens Escape modal and leaves | 1/1 passed |

Every menu test verifies live clocks, Continue and Escape toggle, blocked item/Ready input while open, usable lobby and released session/input locks after exit. Four-player continuation checks inspect every remaining remote visual's seat marker and expected cyclic slot, then complete combat and a later turn. The round case confirms the departed seat remains absent after reset. Seat 1/2/3 departures are all represented; process arrival order is never treated as seat identity.

### Final workspace state

- Original `LobbyScene` retained, clean/Edit Mode; no compilation failure and current console errors zero (`final-editor.json`, `final-console.json`). Existing console history was not manually cleared.
- Baseline-hash comparison identifies only the intended 19 existing code/test files and `Docs/ACTIVE_CONTEXT.md` as changed. No baseline files missing; authored scenes, assets, map coordinates, existing font edits and unrelated dirty work preserved. Font backup and current font SHA256 are identical; no restoration was necessary.
- New production/probe/test C# files have Unity-generated `.meta` files. `git diff --check` passed. Test player processes ended; no commit or push performed.

## Retained failures / limits

- First diagnostic source used a method named `Key` conflicting with the input enum; fixed to `PressKey`. Product code compiled; final tests rerun after correction. An attempted test call while compilation was failed is excluded from final evidence.
- Build options initially passed as a comma string were rejected before building; corrected to a JSON array.
- Build2 warned of uncompiled code because a diagnostic change overlapped its build. It is excluded; explicit asset import/automatic compilation and subsequent builds resolved it.
- Pre-existing console errors from 06:55 (before this task) report `Lobby scene could not load` during Editor stop. They are not new test failures and were not cleared. Current compile status after changes is clean.
- Runtime logs retain an existing missing `Audio/SFX_wind` warning and a shutdown `ComputeBuffer` disposal warning in diagnostic-render runs. These did not prevent the tested flows; this change does not supply the missing audio or establish the cause of the renderer shutdown warning.
- Failed leave now keeps the modal in retry mode; Escape/Continue cannot resume an already-stopping match. Real cleanup-failure injection was not executed.

## Remaining gates

| ID | Status | Required evidence |
|---|---|---|
| V43-EDITOR | Passed | 1017/1017, final Unity compile and successful build |
| V43-MODES | Passed | Solo/Duel/3P/4P menu inputs and approved departure rules |
| V43-BOUNDARY | Passed for listed cases | Attack, ghost, mini-game, result UI and next-round reset runs |
| V43-RELAY | Passed | 14 actual-Relay runs including input regression, 48 peer-process observations |
| V43-PRESERVE | Passed | Baseline hashes, no missing files, meta pairs, original scene/font and dirty work retained |
| V43-EXTERNAL | Deferred | Repeat on different physical PCs/resolutions, native desktop visual review and a long session with actual packet loss |
| V43-FAILURE | Deferred | Inject session cleanup/load failure; verify retry stays modal and eventually releases session. Also exercise simultaneous exits during initial scene loading; current automated exit cases start after gameplay readiness |
| V43-DIAGNOSTICS | Open, outside this feature | Supply/verify wind audio and isolate the diagnostic renderer shutdown ComputeBuffer warning with a minimal render reproduction |
