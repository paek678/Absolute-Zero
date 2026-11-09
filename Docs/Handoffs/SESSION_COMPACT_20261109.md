# Absolute Zero session compact — 2026-11-09

## Workspace and source baseline

- Active project is `C:/Users/paek6/Absolute Zero` and active branch is `codex/webgl-build`.
- Origin is `https://github.com/paek678/Absolute-Zero.git`.
- Pre-handoff HEAD and fetched remote branch both point to `8e719e8`, the WebGL planning commit. `main` is locally recorded at `f2e7e08`; solo integration branch at `b42649d`.
- `C:/Users/paek6/AbsoluteZero-SoloIntegration` is the earlier integration worktree. Later WebGL development and current deployment use the active root project.
- Installed project version is Unity `6000.3.11f1` (`3000ef702840`).
- This handoff commits continuity documents. Substantial implementation changes remain local and dirty. `WORKTREE_20261109.txt` records their pre-handoff status listing. Directory entries in Git status can contain several files.
- `Docs/ACTIVE_CONTEXT.md` is local-only in the inspected checkout. Build output and detailed raw evidence are also local. A clone of this documentation commit requires the existing workspace or separately transferred source changes to recreate the deployed build.

## Latest completed request and live release

The user requested default nickname `player1` and a fresh itch.io upload. Completed 2026-11-07.

| Property | Last verified value |
| --- | --- |
| Public game | https://paek678.itch.io/absolute-zero |
| Editor | https://itch.io/game/edit/5235487 |
| Account / project | paek678 / 5235487 |
| Browser upload | 20337395 |
| ZIP | AbsoluteZero_Web_20261107.zip |
| ZIP size | 52,755,086 bytes |
| SHA256 | ccaa889ca00873b6dd7aa7a3512f0589a21303b02af235ea439d608ef0687288 |
| Release build | Succeeded, 0 errors, 357 warnings, 437.13 seconds |
| Visibility and price | Public, No payments |
| Display | HTML embed 1280 × 720, fullscreen enabled |
| Prior uploads | 20298819 and 20046547 hidden as backups |

Release folder is `output/webgl/itch-release-20261107/`. Relevant files are `validation.md`, `package-review.json`, `artifact-review.json`, `build/build-result.json`, the ZIP, and the temporary `validate-nickname.cs` evidence script. These files are local output artifacts.

`Assets/Scripts/Core/Cosmetic/CosmeticProfileService.cs` now loads `player_nickname` with `player1` as its default and handles a blank saved name the same way. Previously seen `배포검증` was entered during browser testing and was absent from source defaults. Existing saved names continue to load.

Actual live Editor verification covered absent key, empty value and an existing Korean name. The temporary check restored the original Editor preference. Browser bridge tests passed 21/21, HTTP server tests 5/5, and Brotli, JS parsing, Wasm header and ZIP hashes passed. After upload and Save, the real public page showed the new ZIP banner and `player1` on the lobby screen. The latest nickname change received startup/deployment smoke coverage.

## Presentation corrections and prior validation

The user reported client-side damage particles appearing on the attacker, missing outgoing effects, and offset local impact effects in Multi. Corrections are local in the current workspace.

- `CombatPresentationContext.cs` resolves local presentation through the MapCharacterLayout seat-zero anchor, with spawn fallback. `GetImpactPosition` uses viewport position (0.5, 0.4) at local anchor depth for a local human and combined remote body/head sprite bounds for remote players. Cat/contact movement continues through its world-position path.
- `ItemPresentationSequence.cs` routes damage through shared target impact positioning, includes outgoing local-attacker particles, and applies overlay/shake to the local victim. First impact includes ice break. Recovery particles use their existing world-position path.
- `Plan036PresentationProbe.cs` corrected the food trigger and coordinate assertions.
- `MatrixScenarioProbe.cs` corrected an obsolete fixture score to `KillsToWin - 1`.

Canonical detailed record is `Docs/Validation/client_hit_particle_20261105.md`, with local raw evidence under `output/particle-client-review-20261105/` and deployment evidence under `output/webgl/itch-release-20261105/`. The detailed record itself is currently untracked and appears in the worktree inventory.

Observed 2026-11-05 coverage consisted of 46 passing controlled process scenarios plus one normal beginner bot match, 75 presentation assertions, and two particle lifetime checks. The process scenarios covered 2/3/4-player input and environment, inventories, real Relay contact and cat effects, all four victim seats, ghosts, special items, disconnects, host exit, round reset, rematches, minigame failure, winning threshold, and delayed inventory/descriptor ordering. The normal bot match completed 30 turns, three minigames and result/lobby return in 306.42 seconds. These counts describe different test units and must retain their stated scope.

Original invalid fixture attempts and failed runs remain in the evidence folders. An old score seed and omitted artificial-delay flags caused test failures, followed by corrected passing runs. Client process numbering differs from match seats; use the recorded seat field when reviewing captures.

The November 5 published WebGL smoke covered beginner Solo, outgoing Fan, incoming Ice Cream, round 2, settings and lobby return. The user directly confirmed fullscreen. A published-CDN host and local same-build participant connected through Relay, entered Duel, resolved a Fan attack and returned after host exit. Later 3P/4P browser deployment matches were not repeated during that smoke. Prior Windows 3P/4P validation remains the available evidence for those scopes.

## Earlier project decisions and history

These summarize prior user decisions. Verify current source/configuration before changing related behavior.

- Solo bot integration adapted imported bot logic to the project's rules. I00–I08 were completed with documented staged checks, and the user approved beginner/advanced configuration for the build. Earlier Editor totals and the old 60-match collection are dated evidence, separate from later real-bot validation.
- The user chose three rounds and first to three kills for Multi. At the round limit, highest score wins and ties allow shared victory.
- Starting inventory has four random items. Temperature thresholds grant additional items immediately on temperature decrease, while fast Ready/turn progression remains enabled. Earlier thresholds were 30/20/10; inspect current source for exact semantics.
- Duplicate items occupy distinct slots in acquisition order. Item quantity/debug text was removed as requested.
- Only Fan should use the swing presentation. Food attacks use the opponent mouth presentation. Cat moves around the opponent item area. Attacker/target markers follow requested local/remote visibility.
- Ghost sprites/animations were integrated, enlarged by 20%, with bottom-up uniform-scale entrance. Skill/debuff markers are anchored under thermometer UI and expire with their effects. Temperature numeric labels were removed.
- Environment camera rotation applies when an environment character appears. Cicada-only environment behavior was removed per planning decisions.
- Hand fan damage was explicitly requested as 6 degrees. Inspect current asset and effect code when working on balance.
- Settings include game exit; the in-match Esc menu can open sound settings. User confirmed audio working.
- Web work includes Relay transport adaptation, browser lifecycle/return handling, native browser nickname entry for Korean IME, fullscreen/audio/exit behavior, build profiles and packaging. Relevant implementation and validation plans are listed below.

## Web test boundaries and operational facts

- The user manually confirmed Korean composition/deletion, 125% browser zoom input, fullscreen, and several menu/room-entry checks.
- Historical two-PC Chrome tests confirmed Relay battle/items, disconnect return and subsequent room reuse. Several requested tab-hidden durations lacked trustworthy event/timing evidence. Consult `Docs/Validation/webgl_20261019.md` before treating these as closed.
- Automated short canvas clicks intermittently missed. User clicks succeeded in the reported cases. Exact automation reliability remains a separate concern.
- WebGL QoS fallback warnings occurred while actual connections succeeded. Shader/platform warnings exist. A zero-error build does not imply a zero-warning build.
- Fresh browser/iframe behavior, uncommon item combinations, other browsers and network stress should be tested when relevant to a new change.
- Last observed Editor startup for the nickname build used PID 10008 and Pipeline port 7800. They are historical and must be rediscovered. Local temporary port 8961 was stopped after the preceding deployment tests.
- Use `unity status --format json` and verify the project path before live commands. Never force script recompilation. Only launch the project after confirming no existing Editor owns it.
- Existing build helper is `WebBuildValidation.Inspect()` followed by `WebBuildValidation.QueueBuild(absoluteOutputPath, false)` through the verified Editor's eval command. It checks the expected WebGL branch/template/scenes and disabled runtime Pipeline. Use a new output directory and retain older evidence.
- Approved scenes are LobbyScene, GameScene, GameScene_Multi and GameScene_Solo in that order. Web template is `PROJECT:AbsoluteZero`, IL2CPP configuration Release.
- Browser UI automation used the discovered Chrome extension via CUA. Reacquire current tabs and documentation. Login may expire and require the user to sign in. Current browser handles and room codes are ephemeral.

## Documents to consult for the next task

1. `AGENTS.md`, `.codex/PROJECT_GUIDE.md`, `.codex/skills/az-session/SKILL.md`.
2. `Docs/ACTIVE_CONTEXT.md` if present, with this compact as the Git-backed continuity checkpoint.
3. `Docs/Plans/PLAN_webgl_implementation_20261019.md` and `PLAN_webgl_code_design_20261019.md`.
4. `Docs/Validation/webgl_20261019.md` and `webgl_plan_review_20261019.md` for historical Web gates.
5. `Docs/Plans/PLAN_particle_all_modes_validation_20261105.md` and `Docs/Validation/client_hit_particle_20261105.md` for presentation and mode coverage.
6. Earlier integration plan/ledger in the SoloIntegration worktree when investigating imported bot design.

## Latest conversation and next action

The user asked for a team group-chat announcement containing the game URL, delayed publishing due to overlapping competition schedules, and an invitation to send future requests/fixes immediately. The text was drafted and spelling polished. No external message was sent.

The latest operational request is this Git-backed handoff. There is no pending gameplay bug fix or upload request after the successful nickname deployment. Next session should read this record, inspect current local changes and follow the user's next instruction. Committing implementation changes or another deployment should follow the current requested scope and actual reviewed diff.
