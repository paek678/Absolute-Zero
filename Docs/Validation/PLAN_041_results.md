# PLAN 041 — Post-pull recovery results

Evidence root: `C:/Users/paek6/.codex/backups/absolute-zero/post-pull-fix-20261001`.
Plan: [PLAN 041](../Plans/PLAN_041_post_pull_recovery.md).
Baseline: received commit `12e0d7b` plus the existing dirty working tree.

**Final status (2026-10-01): T1–T5 complete for the scoped post-pull recovery. All final gates below passed.** This is not exhaustive game certification; artwork and external-device checks remain explicitly listed.

## Findings and implementation

| ID | Finding | Action / current evidence |
|---|---|---|
| F1 | Multi manual targeting lacked MatchViewBindings | Restored the exact shelved authored scene, then saved through the Editor with the incoming inactive legacy decoration retained. View validation and four-player input test pass. |
| F2 | HUD cleanup accessed a destroyed alarm RectTransform | Lifetime-aware OwnedPositionOffset adapter, release ownership before cleanup, coherent anchoredPosition3D reads/writes. Destruction/reuse tests and all ten runtime boundary cases pass, including the previously failing minigame actor exit. |
| F3 | Center remote slot alone used a maid outfit | Removed slot-specific default artwork by restoring the authored scene. New tops use player equipment IDs and the existing cosmetic atlas renderer. |
| F4 | Multi map/layout bindings missing | Authored MapSceneBinding/MapCharacterLayout and the previous night pavilion/anchors restored together. One of each and valid view references confirmed in Editor. |
| F5 | Alleged missing winner UI in previous screenshots | **Withdrawn: earlier visual inspection was incorrect.** Reopening original images and pixel analysis confirms winner text and lobby button in both old runs. Identical final winner PNG SHA256: `8da47b7e65523b19f4879c89d775feb6a14a91f8786183f4fd9dd79caa37ed05`. No speculative product UI patch made. |
| F6 | Seven incoming top libraries not selectable | Registered top_01 through top_07 with 32 authored body/arm pose mappings each. Existing wire ID length, registry, persistence and network paths preserved. |
| F7 | Test probes could hide missing bindings/visible UI failures | Explicit input binding/collider preconditions; optional network top fixtures; seven-top wardrobe checks; terminal screenshot pixel gate separate from gameplay PASS. Ordinary-flow captures wait for loading and interaction readiness. Missing west/north/east captures fail the runner. |

The complete restored scene differs from the verified pre-pull local snapshot only by disabling the legacy `EnemyPlayer_1/body/head/Customizing` object, retaining that incoming edit. Incoming textures and SpriteLibraries are untouched. The remote scene before recovery and previous local snapshot remain externally backed up; the stash is not popped or deleted.

## Final validation

Evidence paths below are relative to the root above. Runtime checks use freshly built standalone Windows processes; Relay rows use four distinct anonymous profiles and a real UGS allocation on this PC.

| Scope | Observed result | Evidence |
|---|---|---|
| Editor tests | **986/986 passed**, including eight incoming-top tests and destruction/reuse coverage; focused offset suite 12/12 | `editor-tests-final.json`, `offset-tests-final.json` |
| Final Windows Development build | **Succeeded, 0 errors, 3 warnings**. Final capture-only probe changes compiled and ran | `build-final.json`, build `build_316513161fca` |
| Four-player boundary matrix, local UTP | **10/10 passed**: input, disconnect during prep/attack, host exit, inventory, RPC guards, next round, minigame target/actor invalidation and failure | `matrix-initial/matrix.json` |
| Actual Relay manual-input route | **73 assertions per peer, 292 total**, including bindings, collider eligibility, snap/confirm/cancel and Ready lock | `relay-input/matrix.json`, `relay-input/input-4-w0/` |
| Actual Relay tops and ghost fifth kill | top_01..04 DTOs/render mapping and state checkpoints agree across peers. Controlled ghost terminal result passes; four terminal screenshots pass the targeted pixel gate | `relay-terminal-final/report.json` |
| Actual Relay remaining tops and possession | top_04..07 agree across peers; controlled defense-counter possession consumes the item, suppresses its effect and completes its presentation | `relay-possession-tops/report.json` |
| Actual Relay ordinary visual flow, final build | Two settled presentations, **32 captures**, all four peers pass; **12 directional targeting captures**. Representative Host/client images inspected for night map, outfits and arrows below characters | `relay-visible-final/report.json`, `relay-visible-final/gallery.html` |
| Wardrobe and fresh-process persistence | **270 + 271 = 541 checks passed**; seven preview/equip/unequip sequences, isolated-store save/reload, hats and original profile preserved. Seven top preview images inspected | `closet/write.json`, `closet/read.json`, `closet/write-preview-top_*.png` |
| 1v1 regression | **3/3 scenarios passed**: normal input (32 assertions per peer), three consecutive rematches and minigame/next-round transition | `duel/matrix.json` |
| Natural Solo regression | **1 match, 2 rounds, 29 combats, 12 assertions passed**, production BT and normal rules, seed 4101, mixed human policy. No UGS initialization; normal exit cleans up | `solo/report.json` |
| Capture gate verification | Final ordinary-flow evidence passes; earlier run with skipped hover images is correctly rejected; terminal fixture remains valid | `capture-gates-final.json` |

The full Editor suite and boundary/mode runs preceded the final **development-only capture timing** adjustment. The final build and ordinary Relay run validate that adjustment. No gameplay code changed after the full suite. Controlled ghost/possession/input fixtures are not natural full-match evidence; the Solo run is natural gameplay. Pixel gates check specific rendered regions, not arbitrary visual quality or OCR.

The final three build/console warnings are the existing missing Pipeline runtime configuration and unused fields `NetworkSessionCoordinator.maxPlayers` / `SoloMatchStarter._sceneLoaded`. The initial build additionally emitted 485 inference shader warnings. No new runtime exceptions appeared in the passing scenarios.

## Issues encountered during repair

- Initial descriptive top IDs exceeded the existing eight-character wire contract. Changed IDs to top_01..07 without widening networking data. Refreshed Editor catalog lookup after authoring referenced items.
- A new offset reuse test exposed dimensional mismatch when a Vector3 offset was written through anchoredPosition (Vector2). Read/write now both use anchoredPosition3D; focused 12/12 and the full Editor suite pass.
- A combined test filter using `|` selected zero tests; that run is not evidence. Subsequent named/full runs actually executed tests.
- Existing result screenshot files are correct. The prior external audit's visual anomaly diagnosis is superseded by `terminal-visual-correction.json`.
- The first ordinary visual run passed gameplay but captured too early and silently skipped arrows. Waiting for loading to close and retrying until input is ready produced all 12 arrow images. The runner now requires those images and visibility records; the old `relay-visible-flow` report is deliberately failed for incomplete capture evidence.

## Deferred validation ledger

| ID | Status | Exact stimulus / closure evidence |
|---|---|---|
| P41-NET | Passed | Actual Relay all seven tops across two four-client runs; matching DTOs, rendered mapping checks and captures. |
| P41-BOUNDARY | Passed | All ten scenarios pass; no unexpected exceptions. See `matrix-initial/matrix.json`. |
| P41-CONTROL | Passed | 1v1 input/rematch/minigame, natural Solo match, Relay manual input and final directional captures. |
| P41-ART | Asset limitation | Incoming libraries deliberately still reference original defense/hug sprites in several mappings; first-person replacement artwork is not supplied. New art for those poses is needed to extend outfit coverage. Do not invent sprites or change animation geometry. |
| P41-EXTERNAL | Not run | Other physical PCs, long sessions and broad packet-loss/latency coverage. Local processes using real Relay do not establish these. |

## Preservation / final gate

- Compared all **2,386 baseline files**: 12 intentional existing-file changes, none missing. New task files: authoring tool/test with `.meta` pairs, 14 top data assets with `.meta` pairs, and plan/results documents. Lists: `preservation-final.json`.
- Restored `MalgunGothic SDF.asset` from the exact pre-task dirty-file backup after build-generated glyph updates, imported it and cleared the Editor dirty flag. Its hash matches the baseline, preserving the user's earlier font work.
- Incoming art/libraries, other scenes, project settings and unrelated dirty work retain baseline hashes. Recovery stash remains untouched.
- Editor verified as this project, `LobbyScene`, clean and Edit Mode; no compile or console errors. Test player processes exited.
- Existing Unity-serialized whitespace and unrelated dirty-file warnings from repository-wide `git diff --check` were not treated as new defects or reformatted.

No commit, push, balance change, permission change or firewall change was performed. Next: supply missing costume poses for full outfit coverage, then perform external-PC/long-session checks under P41-EXTERNAL.

## Follow-up — Complete wardrobe atlas registration (2026-10-01)

Request: compare pulled sprites with the wardrobe and register missing entries.

- Latest received commit remains `12e0d7b`: all seven incoming tops were already registered and selectable (apron, maid, shirt, swimsuit, tanktop1, tanktop2, training; IDs top_01..07).
- The older `Customizing/hat.png` contains ten independent authored slices. Four were registered; six were missing. Added those six through the existing head-overlay cosmetic atlas path. No sprite slicing or new artwork was required.

| New ID | Wardrobe label | Source slice |
|---|---|---|
| hat_05 | 산타 모자 | hat_0 |
| hat_06 | 방울 산타 모자 | hat_1 |
| hat_07 | 별 고깔 | hat_3 |
| hat_08 | 고양이 귀 | hat_7 |
| hat_09 | 두건 | hat_9 |
| hat_10 | 천사 링 | hat_10 |

Registration: `Assets/Editor/RemainingHatRegistration.cs`, menu **Absolute Zero / Cosmetics / Register Remaining Hats**. The tool creates missing items and preserves existing fitting values on rerun. Data: `Assets/Data/Cosmetics/AtlasBindings/hat_05..10.asset` and matching `_atlas.asset` files. Existing hat_01..04 fits remain unchanged. All ten slices are represented exactly once; templates are not additional supplied costume artwork.

Validation evidence: `C:/Users/paek6/.codex/backups/absolute-zero/wardrobe-catalog-20261001`.

- Focused Editor suite **9/9 passed**: all authored hat slices covered, unique short wire IDs, valid head bindings, all seven top pose maps and unequip behavior.
- Fresh standalone Development build succeeded, **0 errors**. Existing inference/Pipeline warnings remain (486 warnings in this build).
- Actual wardrobe write/restart-read **370 + 371 = 741 checks passed**, including every hat's preview/equip, preservation of the original user save, framing and resource cleanup. All six new hat preview screenshots visually inspected: head attachment, no clipping; halo intentionally floats above the head.
- Existing approved hat fitting values and original font bytes preserved; Editor returns to clean LobbyScene/Edit Mode. No new Relay session was run for this data-only extension; prior networking verification remains narrower evidence.

## Commit preparation gate (2026-10-01)

After the complete hat registration, the full current Editor suite passed **987/987** and the Python validation-runner suite passed **8/8**. This supersedes the earlier 986-test count without changing the recorded scope of runtime runs. Current evidence: `wardrobe-catalog-20261001/full-editor-before-commit.json` under the external backups folder above.

The consolidated commit includes authored source/assets/settings, automated fixtures and shared plans/results. New local `output/` artifacts, Unity recovery snapshots, Python bytecode and machine-specific MCP configuration are excluded; local files are retained. The previously tracked Python bytecode is removed from the index. Artwork files use the repository's existing Git LFS rules.
