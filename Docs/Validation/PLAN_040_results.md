# PLAN 040 — Follow-up implementation and validation

Canonical scope: [PLAN 040](../Plans/PLAN_040_followup_features.md). Predecessor R00–R13 and C01–C03 passed within [PLAN 037's evidence and limits](PLAN_037_results.md). No score here is independent-agent approval or a zero-defect guarantee.

## Current progress — 2026-09-30

| Task | Status |
|---|---|
| UI01 local settings | PASS — 95/100 bounded review; standalone, Solo and Relay checks completed |
| UI02 Korean/English | DEFERRED_BY_USER — 2026-09-30; no localization implementation; does not block BT/QA |
| BT01-A authoring preparation | PASS — 94/100 bounded review; 962/962 Editor tests; authoring workbench |
| BT02-A selection preparation | PASS — 95/100 bounded review; 976 Editor tests, 288 menu checks, 4 Solo matches, 134 transition checks |
| BT01-B / BT02-B / BT03 | WAITING for approved tuning and initialization decisions; do not invent values |
| QA01–QA09 / FINAL | PENDING; prior deferred debts remain open |

## UI01 — Local settings

Evidence root: `output/validation/plan040/ui01_20260930/`. Baseline: 1,260 source/test/scene/prefab/data/settings/package files; intended source/scene checkpoints in `before/`. The original NightPavilionPreview remains the user's scene; Lobby changes are made additively and saved only to the existing Settings subtree.

### Implementation

- One application-local `LocalSettingsService` snapshot/store, owned by `LocalSettingsRuntime`; existing `bgm_volume`/`sfx_volume` retained, master defaults1, shake defaults on, absent fullscreen preference leaves launch mode intact. No UGS/RPC/gameplay state added.
- Six `GameAudioManager` sources use base × channel × master exactly once. Existing volume API delegates to the same service. Listener volume is not changed.
- Fullscreen requests preserve render dimensions/refresh, apply through Unity's end-of-frame platform API, coalesce to the newest request and save only confirmed actual state. Standalone verification is required; Editor state is not proof. API reference: [Unity Screen.SetResolution](https://docs.unity3d.com/cn/6000.0/ScriptReference/Screen.SetResolution.html); compiled against installed 6000.3.11f1.
- `OwnedPositionOffset` prevents stale camera/layout restoration. Camera and HUD shake use private randomness, and clearing/disable removes only a still-owned offset. Alarm text/timer/sound remain active when decorative shake is off.
- Existing Settings panel retains navigation and BGM/SFX controls; master, fullscreen, shake, visible values, save failure/retry and listener disposal added. Explicit serialized bindings are required.

### Checks and retained failures

- First Editor suite **939/939**, including ten new settings cases: legacy keys/defaults/ranges, duplicate commands, save failure/retry, reentrant writes, rollback failure and externally moved transforms.
- Preflight read-only hierarchy serialization initially used Unity Vector2 directly and hit JSON self-reference recursion. Retried with scalar arrays; scene was closed in `finally`, no asset mutation.
- New standalone diagnostic initially referenced the UGS Core assembly from UI directly, causing two compile errors. Replaced with the existing Core gateway's read-only `IsInitialized` boundary; no assembly dependency added. Recompile and runtime acceptance pending at this checkpoint.
- No UI01 PASS or final score until standalone UI/audio/fullscreen/lifecycle, affected gameplay and final preservation checks finish. Development diagnostics use opt-in `--plan040-settings` with a GUID-scoped preference prefix, preserving the user's real keys.

## Carried-forward quality work

Keep the canonical `V37-UI-LAYOUT`, `V37-UI-ASPECT`, `V37-COSMETIC-HUD`, native/package warning, other-device/soak, actual offline and release-backend debts in [PLAN 037](PLAN_037_results.md#deferred-validation-ledger) and [PLAN 036](PLAN_036_results.md). A settings test pass does not close them.

### UI01 resumed checks (2026-09-30)

- Build `build_63a5da3b6b1b` succeeded, errors 0 / warnings 486. Settings write/restart checks **179/179** with six actual framebuffer captures at 1920x1080, 1280x720 and 800x600. Representative 720p/600p captures inspected: controls and Korean labels visible.
- First standalone attempt failed a cross-frame RNG assertion after 79 checks. Reproduction shows global RNG changes even with shake disabled during idle lobby frames; the old assertion could not attribute that to CameraShake. The replacement invokes 60 actual camera update methods in one controlled block and verifies unchanged global state, plus visual movement and disable/external-position cleanup. This does not identify the unrelated idle-frame RNG consumer.
- Missing checkmark font glyph replaced with an explicit Image. No font regeneration. Save failure/retry, mute/recovery, all six sources, fullscreen/windowed latest-request confirmation and unchanged render resolution/monitor refresh passed.
- Next: actual in-game HUD/camera and per-peer different settings through Solo/Duel/Relay; final source/asset preservation and Editor suite. No task PASS yet.

### UI01 exit gate — PASS / ready for UI02

- Evidence: `output/validation/plan040/ui01_20260930/exit-evidence.json`. Self-review **95/100**, scope limited to this task; not an independent review or a defect-free probability.
- Editor **939/939**. Final development build `build_77b9967d52c3`, errors **0**, warnings **1**.
- Standalone settings write/restart **179 checks**, **42 decoded screenshots** including 6 settings views, 32 network captures and 4 Solo frames. Representative settings and multiplayer views inspected.
- Actual Relay **Duel rematch / 3P victory / 4P multi-round 3/3**, **9 peers**, **90 settings/HUD checks** with different process-local master/shake values. All peers complete normal gameplay and accepted cosmetics agree.
- Natural Solo **one match, 11 scenario checks + 10 HUD/settings checks**. UI01 production source/assets match the final build; this Solo run used the previous diagnostic build (only later change: tolerant comparison of fixture peer 1/3 volume).
- HUD shake off retains alarm text, red countdown and clock playback. 240 actual HUD updates preserve the global game RNG and real weighted-drop result; camera's 60 controlled updates preserve global RNG. Disable/external layout writes restore only owned offsets. Steal implementation and game-side random draw remain unchanged; this is not a claim that pre-refactor shared visual/game RNG seed streams are identical.
- First Relay diagnostic failed strict equality for a computed 1/3 float, although captured volume was 0.333333343 and shake=true as requested. Replaced diagnostic comparison with Mathf.Approximately, rebuilt, all 3 cases passed. No gameplay workaround introduced.
- Required settings references valid; missing scripts **0**, broken references **0**, EventSystem **1**. Of 1,260 baseline files, only seven intended existing files changed, missing **0**. All 94 altered/additional serialized scene objects are under MainUI/SettingsPanel. Existing map/world positions, other scenes, assets and user-edited font are preserved.
- Original NightPavilionPreview remains clean/EditMode; test players ended. Current compile/errors=0. Retained console buffer includes expected old Editor negative-test errors, classified from their test stacks rather than erased. Existing package ComputeBuffer shutdown warning remains QA05 debt.
- Limits: physical mouse/audio perception, another PC, alternate monitor/OS and final release backend remain separate gates; existing QA04 layout debts are not closed here.

## UI02 localization — deferred by user

UI02.1 display call-site inventory started at `output/validation/plan040/ui02_inventory/display-call-sites.json`. The user deferred language switching on 2026-09-30. No implementation or PASS is claimed. Continue with BT01-A, then BT02-A; localization is no longer a prerequisite.

## BT01-A — authoring preparation exit gate (2026-09-30)

**PASS / ready for BT02-A**, bounded self-review **94/100**. This completes authoring preparation only; BT01-B/BT02-B/BT03 remain pending tuning, start/reset and initial-grant decisions. UI02 is deferred by the user.

- Added Editor-only `SoloAuthoringPreview`, UI Toolkit `SoloBotAuthoringWindow`, and configuration Inspector guidance. Menu: **Absolute Zero > Solo > Bot Authoring**. Shared docs: [BT authoring guide](../SOLO_BT_AUTHORING.md).
- Uses all three existing launch resolver routes; development readiness does not become release/fixture readiness. Reports source assets, IDs/versions, effective policy/weights, timing budgets, duplicate library IDs and disabled catalog entries. Inherit-only guards remain unchanged.
- Timing findings account for the existing runtime thinking clamp. Nominal overflow is a diagnostic warning, not a new gameplay rejection. No balance values, graph topology, initial grants, scenes, catalogs or network owners changed.
- Unity auto-compilation passed; Editor **962/962** tests, including **23 new authoring cases**. Invalid/null/duplicate references, graph/policy absence, nonfinite/zero delays, reserve boundary, start overrides, fixture separation, effective policy and immutable source data checked.
- Actual workbench reads saved lobby bindings, enumerates **17 configuration assets** with no duplicate IDs, and preserves the original clean NightPavilionPreview scene. **20 repeated refreshes** retain the preview-scene count and active scene/dirty state.
- Preservation: **2,169 existing files**, exactly **3 tooltip-only C# changes**, missing **0**; new **3 source/meta pairs** verified. No fresh player build or Solo/Relay run was necessary for this Editor-only task; historical runtime tests are not recounted as new evidence.
- Two diagnostic script failures (eval body does not accept using directives; missing extension-method import) were corrected with qualified calls; they were not product compile failures. UTF-8 output and nested test-result decoding were corrected in evidence scripts.
- Screen capture was covered by other user applications, so it was excluded and removed. Full unobstructed visual and physical mouse review remains pending; structural UI/labels/references were inspected.
- Retained console errors trace to expected negative equipment/lobby tests, not the workbench. The console was not cleared. See `console-errors.json` for stack traces.
- Evidence: `output/validation/plan040/bt01a_20260930/exit-evidence.json`, `editor-test-status.json`, `authoring-window-final.json`, `window-checks.json`, `final-console.json`.

| Deferred ID | Check | Status / closure evidence |
|---|---|---|
| BT-A-VIS | Workbench and inspectors at wide/narrow sizes using actual mouse and unobstructed screenshots | Pending visual review; read-only tool UI, does not block BT02-A implementation |
| BT-A-CUSTOM | New authored graph progress, every tactic and balance | Pending actual BT data; resolver pass alone is not gameplay validation |

## BT02-A — explicit Solo profile selection (2026-09-30)

Status: **PASS / ready for independent QA**, bounded self-review **95/100**. Evidence root: `output/validation/plan040/bt02a_20260930/`. BT01-B/BT02-B/BT03 remain blocked on actual tuning/start/reset/grant decisions, not on this screen.

### Implementation and guide

- Separate [Bot Authoring Workbench user guide](../BOT_AUTHORING_TOOL_GUIDE.md), linked from the Editor tool and the existing BT guide. Covers launch gates, field ownership, saved lobby references, timing warnings, profile registration and troubleshooting. `Docs` remains the shared Obsidian copy.
- Lobby flow: **봇 대전 → profile information → 대전 시작 / 뒤로**. The explicit saved list contains the baseline only; fixtures are excluded even in development. Invalid/duplicate entries cannot start. Viewing a profile does not start NGO or UGS.
- `SoloSelectionPresenter` owns selection/request lifetime; `SoloSelectionView` owns reusable cells/listeners; existing session coordinator/router retains network and cleanup ownership. No new turn driver, RPC, balance, graph or initial-grant policy.
- Start revalidates; double clicks do not duplicate sessions. Back requests cleanup only for that menu's captured lease. Failure remains retryable; stale async completions cannot update an unloaded view. Successful scene unload does not stop the transferred session.
- Replay now reuses captured `ResolvedSoloSettings`, including copied scalar values, delays and configuration versions; a new lobby launch resolves the newly selected profile. Rule/graph/item asset references remain read-only identity handles under the existing configuration contract.

### Completed checks and retained failures

- Editor **976/976**, including **14 new selection cases**: selected profile identity, preview vs start validation, duplicate/fixture/empty data, double start, cancel/cleanup failure and retry, stale completion/disposal, hidden input and online ownership.
- Development build `build_49a37120a313`: **0 errors / 488 warnings**. Product/probe source matches this build; the last later change adds only an Editor test and documentation.
- Standalone menu runs at **800x600, 1280x720, 1920x1080**, each **96 checks**, total **288** and **9 framebuffer screenshots**. Per run: 20 open/back cycles, explicit 16-label presentation-only scroll stress, and 3 actual local-start/cancel cycles. UGS remains uninitialized, session released, menu usable. Automation invokes the real button events, not physical mouse input. Wide/narrow views and list clipping visually inspected.
- Saved LobbyScene: complete bindings, one baseline entry, template/panel initially inactive, both scroll areas connected, one EventSystem, no missing scripts. The addition audit isolates 76 new serialized objects under the selection panel plus its parent child entry and AZLobbyUI fields; existing owner fields/parent layout remain unchanged.
- Unity compilation initially failed because one generated Bee response-file path contained `Edit<DEL>r/Data/` instead of `Editor/Data/`. The real framework DLL exists. One generated byte sequence was backed up and repaired; normal save/refresh compilation then passed. Root cause is **unknown**; no forced recompile, package change or source workaround. Evidence: `compiler-path-repair.json`.
- Legacy Windows PowerShell script execution was denied by its execution policy; no policy was changed or bypassed. A Python runner launches the same explicit validation arguments through the existing fixed executable path.
- The first mixed-item replay run was deliberately stopped after reaching a replay because healing prolonged the scenario; its runner also had an insufficient total watchdog for four possible 600-second matches. It is recorded as **ABORTED_NOT_A_PASS** in `solo-replay/interrupted.json`. The rerun uses the existing late-fan human policy and a corrected total watchdog, without changing game rules, bot tuning or forcing wins.

- Natural Solo rerun passed **4 matches / 8 rounds / 18 combats / 3 replays**, **74 checks**, errors **0**, exit **0**. Every replay retains the same captured settings and version stamps; human and bot cosmetics stay separate. Completed results remain stable for 31 seconds, explicit exit removes the host/controller, and UGS stays uninitialized. Evidence: `solo-replay-late-fan/report.json`, 12 actual framebuffer captures; final preparation/result views inspected.

- Actual Relay mode transitions passed **4 processes / 134 checks**: cold Solo, Relay 1v1, Solo, Relay 3P/4P, invalid online entry recovery and unexpected local-stop cleanup. Transport/controller/session ownership was restored correctly. One deliberate invalid-code error is recorded separately; unexpected errors **0**, all process exits **0**. Evidence: `solo-transitions/summary.json` and peer reports.
- Non-development build scheduling initially remained in `EditorApplication.delayCall` while the Editor processed CLI commands. Only this task's uniquely identified callback was removed from delayCall and dispatched through a one-shot update; other Editor callbacks were preserved. A queued response is not a completed build.

- Instrumented non-development guard build succeeded (**0 errors / 488 warnings**, Mono, stripping disabled). Runtime **5 checks** passed, exit **0**, no native crash: provisional selection disabled/revalidated, no UGS/network/session start, usable Back/menu, compiled graph retained and **13 development diagnostic types absent**. This is a launch-protection check, not the final target-platform release gate.
- First development-build restoration failed in the compiler with **Internal CLR error (0x80131506)**, not a source-location C# diagnostic. Failure retained in `final-development-failed-clr.json`; Editor compilation was normal afterward, about 12.5 GiB physical memory was free, and the same source was retried. No claim of a proven root cause or permanent repair. Existing ComputeBuffer and DontDestroyOnLoad runtime warnings remain separate from error-free scenario results.

### Exit evidence and remaining scope

- Final ordinary development build restored: **build_a56dba26685e**, **0 errors / 486 warnings**. The same-source retry after the CLR failure succeeded; monitor recurrence rather than claiming its cause fixed.
- Final Editor: original **NightPavilionPreview**, clean, Edit Mode, one loaded scene, no compilation failure and current console errors **0**. Test players have exited. Earlier five retained console errors were traced to deliberate equipment/lobby negative tests; no console clear was issued.
- Preservation: **2,175 baseline files**, **10 intended existing changes**, **0 missing**, **6 new source/meta pairs**. Other scenes, map positions, profiles, graph, initial grants, catalogs, packages and project settings remained unchanged from this task's starting state. Changed C# passes whitespace checks; Unity's generated empty YAML fields retain serializer whitespace.
- **21 decoded framebuffer screenshots**, with wide/narrow menu, scroll and final Solo preparation/result samples inspected. Relay transition reports prove the mode/lifecycle assertions; no transition screenshot count is invented.
- Evidence summary: `exit-evidence.json`. Score rubric: requirements **20/20**, authority/lifetime **20/20**, tests/runtime **20/20**, preservation/trace **20/20**, visual/manual coverage **15/20**. This is a task-scoped self-review, not independent approval, release readiness or a zero-bug guarantee.

| Deferred ID | Check / next action | Status and closure evidence |
|---|---|---|
| BT-S-MANUAL | Real mouse, keyboard focus, wheel scrolling and Back/Start during loading on another PC | Pending physical-input/device test; automated button events and captured layouts already pass |
| BT-S-PROFILES | Final difficulty names, values and bot start/reset/grant policy; then BT01-B/BT02-B/BT03 | Waiting for actual authored/approved data. Baseline menu readiness does not approve new tuning |
| BT-S-BUILD | Monitor CLR compiler failure and malformed generated response-file path | Same-source build now passes; causes remain unknown. Collect exact compiler/build logs on recurrence |
| BT-S-RELEASE | Approved playable non-development build with chosen backend/stripping | QA02 pending. This run only proves provisional launch rejection under Mono/stripping disabled |

Next independent task: **QA01 actual offline execution**, beginning with isolation feasibility. UGS remaining uninitialized on this connected PC is supporting evidence, not proof of disconnected operation. Do not change global NIC/firewall settings to manufacture a pass. Remaining QA01–QA09/FINAL and the earlier BT-A-VIS debt are still open.

## QA04-HAT-FIT — Floating wardrobe hats (2026-10-01)

- User screenshots showed caps sitting too high and a visible gap beneath the party hat. All four registered hat atlas bindings reused local Y `0.83`, despite different brim shapes. `PrivateCosmeticPreview` and gameplay `AZPlayerVisual` both apply these shared atlas bindings through `CosmeticVisualController`.
- Changed only `LocalPosition.y` in `Assets/Data/Cosmetics/AtlasBindings/hat_01_atlas.asset` through `hat_04_atlas.asset`: caps 01–03 to `0.73`, party hat 04 to `0.55`. Saved through the verified Unity Editor with Undo. Per-file comparison against task backups confirms only these four scalar values changed; sprites, pivots, scale, sorting, IDs and gameplay/network code were preserved.
- Verified saved-data renders of all four wardrobe hats, then entered Play Mode in the existing LobbyScene, opened the real wardrobe, and captured its party-hat render target. Instantiated the actual animated player visual prefab four times in an isolated runtime display and applied each hat through the same DTO/controller route used for remote visuals. All four overlays retained their expected local position after Animator/SpriteSkin updates; final rendered hats touch the hair without the former gap.
- Evidence: `output/validation/hat_fit_20261001/before.png`, `after_closet.png`, `runtime_closet_party.png`, `runtime_player.png`; original asset backups in `before/`. The initial Edit Mode actual-player capture was blank before skinning updates and is excluded from successful evidence. The first runtime comparison allowed animation to overwrite display positions; independent display parents corrected the capture fixture. Neither diagnostic issue required product-code changes.
- Temporary players and render target released; returned to Edit Mode. Console query (errors/warnings, maximum 30) returned no errors and one existing `DisconnectDispatcher.Awake` DontDestroyOnLoad warning at line 27. No console clear or forced recompile. No new tests were added for this data-only fitting adjustment.
- **Scope:** this visual defect is fixed for the current four hats and front character. This was a local Play Mode visual/DTO check, not a new Relay match or full animation matrix. Existing large-hat/HUD overlap, other QA04 issues and FINAL remain open. Existing standalone executables need a rebuild to include the changed assets.

### User fit revision — caps worn deeper (2026-10-01)

**Latest fit — width revision:** the user requested caps just wider than the hair while preserving eyebrow coverage. Hats 01–03 now use uniform scale **1.08** instead of 1.20 (10% smaller). Local Y is respectively **0.44308326 / 0.44279468 / 0.4426264**. Each Y compensates for scaling about the sprite pivot: `newY = oldY + (oldScaleY - newScaleY) * sprite.bounds.min.y`. Asserted lower-brim height delta below 0.00001. Party hat 04 remains Y **0.55**, scale **1.20**. Backups in `width_fit_before/`; comparison shows only scale and Y changes to three atlas assets. Inspected preview candidate and actual animated player prefab in Play Mode: `output/validation/hat_fit_20261001/width_fit_runtime.png`. Eyebrows remain covered, eyes visible, and cap sides now sit close to the hair silhouette. Temporary instances/texture released; no gameplay, save, prefab or network code changes. This is a local visual check, not a Relay match.

**Previous eyebrow-height revision:** the user requested that cap brims cover the eyebrows. Hats 01–03 used local Y **0.50** (previous 0.60), while the party hat stayed at **0.55**. Only three Y values changed in that revision. Play Mode evidence: `output/validation/hat_fit_20261001/eyebrow_runtime.png`; backups: `eyebrow_fit_before/`. An Edit Mode preview capture returned a blank frame and was excluded. The older 0.60 captures below remain historical.

- User accepted the party hat but requested the other three hats sit more deeply on the head. Lowered only hats 01–03 from local Y `0.73` to `0.60`; party hat remains `0.55`.
- Compared a temporary `0.58` candidate, then selected `0.60` to retain a little more eyebrow clearance. Re-rendered all four hats from the saved assets using the actual wardrobe preview path; inspected `output/validation/hat_fit_20261001/caps_lower_final.png`. Previous values backed up in `caps_revision_before/`.
- This follow-up checks the saved-data rendering; the preceding Play Mode/controller check establishes the shared fitting path, but was not rerun at the new value. No sprite, scale, sorting, prefab or runtime code changed.
- Subsequent user-requested actual-prefab capture completed in Play Mode at the final `0.60/0.55` values. Instantiated `Assets/MainFolder/Sprite/player.prefab` four times with Animator/SpriteSkin active, applied serialized equipment IDs through `CosmeticVisualController.ApplyFromDto`, and asserted each overlay position equals its saved binding. Captured each hat and the combined comparison in `output/validation/hat_fit_20261001/final_prefab/` (`all_hats.png`, `hat_01.png` through `hat_04.png`, `capture.txt`). Visually inspected the combined capture. Temporary instances removed and Edit Mode restored; no equipment save or source prefab changes. This is real prefab rendering, not a multiplayer-match test.
