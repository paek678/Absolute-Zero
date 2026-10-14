# Solo integration Relay and runtime audit — 2026-10-14

## Scope and evidence

User requested fresh verification of Relay connectivity and broad real-play defects in `AbsoluteZero-SoloIntegration`. This is an audit, not approval to change gameplay or publish a build. Preserve the dirty integration and Desktop/Demo submission binary.

- Evidence: `output/relay-audit-20261014-1241/`.
- Verified Editor: Unity 6000.3.11f1, project `C:/Users/paek6/AbsoluteZero-SoloIntegration`, CLI endpoint initially7800 and finally7801; every operation explicitly targeted the integration project.
- Branch: `codex/solo-bot-integration-20261013`; pre-existing dirty status saved.
- Fresh Editor tests: 1278/1278 passed, 24.38 seconds; full response retained.
- Fresh development Player: job `e306040f64bf40a49680983422d39f1c`, errors 0, warnings 486, fixed previously allowed executable `C:/Users/paek6/AppData/Local/Temp/AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe`. Previous Player copied before building. All 449 output files pinned; effective Solo profiles freshly read from approved source. Ordinary build settings remain unchanged.
- Before testing, Editor errors 0 and two warnings: deprecated legacy input and Codex executable signature collection. Build warnings require grouping separately from gameplay failures.

## Current results

| ID | Stimulus | Status / evidence |
|---|---|---|
| R01 | Actual Relay Duel, two independent UGS identities, combat and rematch reset | Passed: `relay-smoke/duel-2-w0/report.json`; both peers exit 0, matching checkpoints, `relay_verified=true`. Controlled initial state. |
| R02 | Relay Multi four seats, inventory/rendering, RPC guards, pending target invalidation, ghost and round reset | Five controlled cases passed. The minigame case also kills its target, so it proves pending-target/stale-ticket handling, not an ordinary live-target failure. Pure bot failure passed separately in R07. |
| R03 | Relay host departure and new three-player lobby; normal menu departure | Normal remote departure in Prep/Attack and normal host departure passed (three cases). Abrupt-host fixture failed because it restarted while the coordinator was still Disconnecting; subsequent Ready is logged. Preserve this exact4→3 failure. Separate actual two-player recovery and new-room admission passed in R08. |
| R04 | Natural Duel and Multi, results and lobby return | Duel313.5 game seconds and Multi745.6 seconds completed with every peer's result, terminal agreement, lobby return and exit0. Actual Relay verified. Duel has a genuine cap violation (B04) omitted by the raw checker. Multi ended3R8T when seat1 reached3kills, with15 ghost-damage observations; random inventory peaks4/4/4/3. No round/kill/balance overrides. |
| R05 | Current approved Beginner/Advanced natural Solo | Both passed with actual approved identity and results-to-lobby. Beginner609.865 game seconds, three rounds,62 turns,3615 events; Advanced408.116 seconds,two rounds,46 turns,2512 events. Beginner bot used13 item types. One game per profile is not a difficulty/balance estimate. |
| B01 | Join a lobby and wait at least 35 seconds before host starts | Reproduced on actual UGS lobby: client reports `Relay code not received in time`, host population falls 2 to 1 without host start. `join-wait-35sec/host-state.json` and client log; helper cleanup complete. |
| B02 | Join a two-player room while client selected Multi | Reproduced with actual Relay: host and remote enter GameScene, but remote later fails `Timed out waiting for game scene synchronization` and departs. `mismatched-mode/host-state.json` and client log; helper cleanup complete. |
| B03 | Abruptly terminate a running Multi client, then use the same account and room code | Confirmed re-entry failure: `player is already a member of the lobby`. SDK PlayerId equality verified. Three remaining players continue; lobby membership still counts4 while NGO/roster count3. The departed seat is TimedOut. Normal owned cleanup and original mode restoration completed. `rejoin-abrupt-4p/` contains all evidence. The suspected unassigned-seat path was NOT reached. |
| B04 / IG02 | Consume base items, then receive threshold grants via ApplyDuelTick | Controlled Solo reproduction: human and bot random counts4→5→7→10 despite cap8, with complete restoration. Also naturally reproduced in Relay Duel: fan11→10°C triggers three random grants, taking count6→7→8→9. Player.log traces CheckThresholds→ApplyDuelTick and all three additions; nine persist at terminal. See `ig02-20261014-034815-802.json` and `natural-duel-ig02-evidence.json`. Existing direct threshold tests and the natural checker omit this production-call cap regression. |
| B05 | Cool Breeze environment sound | Confirmed missing Resources/Audio/SFX_wind asset and fresh warning in both Duel peers. This specific loop is absent; this is not an all-audio failure. |
| R06 | Settings from remote in-game Escape menu, audio and input restoration | Passed31 checks on an actual Relay remote client: Escape/menu/settings input ownership, normal volume persistence and actual BGM/SFX source gains, close/continue, and settings after normal lobby return. The match clock continues while settings is open. All settings/PlayerPrefs/input state restored; no human-audible claim. `remote-settings/attempt-20261014-043449-776/result.json` and `launcher-state.json`. Diagnostic rendering requires the separate presentation qualification below. |
| R07 | Pure live-target bot minigame failure and operation replay | Passed on the unchanged new build in6.75s: actual production graph/session, consume once, no effect/temperature change, no re-roll or duplicate debit, normal cleanup. Controlled initial state/request ID; no natural-match claim. `pure-mini-failure-no-video/mini-failure/report.json`. |
| R08 | Same remote Editor after abrupt host loss joins a new normal Relay room | Passed63.8615s: first actual Relay host was terminated, remote automatically returned to fully Ready with no lease/listener/input block, then normally joined a new Relay room with a fresh session generation. Normal final Leave and cleanup passed. `host-exit-reentry2p/result.json`. Does not substitute for exact4→3 fixture or same-match reconnect. |

## Evidence limits

Actual Relay must have distinct authenticated player IDs, allocation/join logs and NGO-connected peers; lobby creation alone is insufficient. Tests on one machine do not cover other physical PCs, routers or restrictive networks. Controlled scenario results are separate from natural matches. Diagnostic camera re-renders are not framebuffer/video proof. Old I07/I08 and the historical 60 matches are not counted as fresh results. No automatic fixes, merge, push, deployment, or changes to the submission binary.

## Log and capture review

- The first 34 completed peer logs contain no C# exception. Expected invalid-input RPC rejection, shutdown after PASS and injected disconnections are not counted as defects. See `log-review-interim.json` for exact lines and classifications.
- `DisconnectDispatcher.Awake` calls DontDestroyOnLoad on a child object and emits a warning on each peer. Gameplay impact is not established by that warning alone.
- BuildReport has 486 warnings; 485 observed shader warning lines group under Sentis shaders (Pad1, ConvTranspose6, ScatterND9, GridSample3, ScatterElements2, SliceSet4, ConvGeneric460). The remaining report warning is not classified; these are not 486 gameplay bugs.
- Actual camera/canvas diagnostic renders of Duel combat, Multi full inventory from two seats, Beginner combat, and remote/lobby settings were inspected. They establish the captured rendered layout only. The Beginner capture exposes an English `Resolving...` status in the normal HUD; production source ownership is recorded in B06 and `additional-static-review.md`.
- The current immutable suite includes four natural games (Beginner, Advanced, Relay Duel, Relay Multi) after nine controlled cases. Preserve each raw failure and distinguish a runner verdict from the interpreted production defect list.

## Confirmed findings and proposed corrections (not applied)

| Finding | Priority / consequence | Proposed correction and regression |
|---|---|---|
| B01: normal lobby waiting is bounded by the 30-second Relay-code timeout | P1: a legitimate participant is removed while the host is still waiting to start. | Separate ordinary lobby waiting from the connection-start deadline. Keep cancellation, host loss and normal Leave responsive. Re-test waiting beyond30s, subsequent host start, and cancellation. |
| B02: room metadata does not correct/reject the client's selected mode | P2: a valid code for the other mode can initially connect, then fail scene synchronization after60s. | Validate/apply authoritative room mode and player count before scene admission. Test both mismatched menu directions and ordinary matching rooms. |
| B03: abrupt-close membership recovery / same-match reconnect limitation | P2 recovery/UX limitation: same account gets a generic already-member error. Current production disconnect logic immediately times the old seat out; same-match rejoin is not safely implemented. | Resolve the authenticated user's joined lobby and exactly match its code; recover waiting-room membership only. For an in-progress TimedOut seat, clearly explain non-rejoinability and clean up only that user's stale membership. Full match reconnect requires a separate identity/grace/state policy. Do not bypass Relay admission or revive the seat merely to make this test pass. |
| B04 / IG02: shared Solo/Duel threshold caller omits MaxRandomItems | P2: consuming basic slots permits random grants above the configured eight-item cap. | Pass the rule cap through ApplyDuelTick, and test that production caller with freed basic slots and all thresholds. Preserve legitimate starting four random items and one-time grant flags. |
| B05: missing wind loop | P3: Cool Breeze lacks its intended environment sound. | Supply the intended resource or correct its resource key; verify that environment start/stop plays the right loop. |
| B06: ordinary Solo/Duel HUD includes English status strings | P3: inconsistent presentation; no gameplay block proven. | Localize/hide the bottom status according to intended UI and clear obsolete phase status. See `additional-static-review.md` for call sites and Multi visibility distinction. |

The redundant child DontDestroyOnLoad warning is recorded as maintenance noise, not a proven fatal lifecycle defect. Its parent Managers root is already persistent. No source/asset correction has been applied during this audit.

## Completed suite interpretation

All13 planned suite cases executed:12 raw checks passed and the abrupt-host4→3 fixture failed before proper Ready gating. The final `completed` display counter remains12 because it is not incremented after the last case; `complete=true` and the13 result entries establish execution count. Original status is preserved. Source/runtime hashes are unchanged and no suite players remain. Separate Duel smoke also passed. Natural matches used automated human policies at time scale1 with production rules and one sample per profile/mode; they do not certify human balance, all action combinations, or all network conditions. Multi's12m26s is a duration observation exceeding the earlier10-minute target, not a deadlock or a statistically sufficient tuning result.

## Final preservation and console review

- `final-preservation.json`: full SHA-256 comparison of5019 source/configuration/tool/evidence inputs and449 development Player files found no content changes. Existing dirty work remains intact. Only this report was added at that check; the final continuity note in `Docs/ACTIVE_CONTEXT.md` is an intentional documentation update.
- All owned test players finished or were cleaned up by their exact owning process handle. No `AbsoluteZeroVisual4P.exe` remains. The remote-settings helper deliberately terminates its owned waiting host after the remote's successful normal Leave; its exit1 is cleanup, not a successful natural host exit claim.
- Editor restored to its initial stopped Play Mode state, correct integration project, not compiling. Tests restored mode/count, original settings, original five PlayerPrefs keys including absent keys, and input ownership. No source, asset, balance, production harness, commit, merge, push or submission-binary change.
- Final CLI ground-truth console reports0 errors/1 warning. Its retained historical stream contains7 errors:3 deliberately injected TestLeave removal failures and4 EditMode `Renderer.material` diagnostics from `Plan063DeathTimingTests`. The latter are test-fixture/EditMode cleanup noise, not proof of a runtime material leak. Logs were not cleared. Full historical responses are retained in `console-final-errors.json` and `console-final.json`.
- Four complete natural games contained no observed unhandled C# exception. This does not override B01/B02/B04 or certify every gameplay combination.

## Final disposition

Actual Unity Lobby/Relay allocation and joining, two-/four-player combat, ghost actions, termination, and ordinary return paths are functional in the freshly built integration development Player. The audit identifies three important confirmed defects (B01/B02/B04), two lower-impact presentation/resource defects (B05/B06), and one recovery/UX limitation requiring an explicit reconnect policy (B03). This is not a defect-free release verdict. Fix and regress B01/B02/B04 first; production changes are not part of this read-only audit.

The diagnostic remote-settings render shows foreground items crossing the panel. Source review finds that `SoloLaunchProbe.WriteRender` temporarily converts ScreenSpaceOverlay canvases to ScreenSpaceCamera, exposing item sorting20000/Ready20010 over menu2000. Normal production MatchMenu uses ScreenSpaceOverlay and URP's final overlay UI pass. Therefore this diagnostic image is not sufficient evidence of an ordinary modal-occlusion bug; no additional production defect is counted from it. The settings functional checks passed, while native framebuffer modal presentation remains outside this capture's proof.
