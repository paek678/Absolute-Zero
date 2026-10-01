# PLAN_039 - Map bundles and character anchors validation

Date: 2026-09-29. Unity 6000.3.11f1, URP 17.3.0, NGO 2.11.2; connected project verified through Unity CLI. Scope and decisions: [plan](../Plans/PLAN_039_map_presets.md). Usage: [Map Library and anchors](../MAP_PRESETS_SETUP.md).

## Result

**Implementation and focused validation passed.** Four reusable map bundles, Editor map switching, per-map Duel/Multi placement anchors, seat assignment labels and renderer-only previews are available. Production scenes keep the night pavilion. No turn, balance, participant ownership or lobby-selection rules were changed.

| Check | Observed result | Evidence |
|---|---|---|
| Original map migration boundary | 692 gameplay components unchanged across three game scenes; preview's 64 protected components unchanged; original environment transform/render/material signatures preserved | `map-presets_20260929/migration.json` |
| Map swaps, guard failures, batch preflight, save/reload | 64 checks passed; rerun after anchors added | `map-presets_20260929/validation.json` |
| Undo/Redo | Both object references and lighting restored through separate Editor operations | `map-presets_20260929/undo-redo.json` |
| Map visual preview | Four presets captured/inspected; preview Play Mode passed | `map-presets_20260929/*.png`, `play-validation.json` |
| Editor tests | 284/284 passed, including 15 new layout cases and existing spawn/identity/authority/solo tests | `map-anchors_20260929/editor-tests.json` |
| Preview behaviour | Renderer-only, three remote copies, all 2/3/4-player viewer-seat permutations, live moved-anchor following, no serialized temporary objects, original renderer state restored | `map-anchors_20260929/preview-validation.json`, `anchors-default.png`, `anchors-moved.png` |
| Preview lifecycle | Prefab-stage ownership, cleanup on prefab exit, map replacement and Play Mode entry passed | `map-anchors_20260929/preview-lifecycle.json` |
| Remote item placement | 48 checks: all three slots/two rows retain default positions and follow moved/rotated visual anchors | `map-anchors_20260929/item-layout-validation.json` |
| Windows Development build | Succeeded; 0 errors, 489 reported warnings (existing unused fields and package/shader warnings; no new map compilation diagnostics) | `map-anchors_20260929/build.json` |
| Actual local NGO 1v1 | Host/client combat, synchronized settled state and rematch reset passed | `map-anchors_20260929/runtime-visible/duel-2-w0/report.json` |
| Actual local NGO 4-player | Host + three independent clients, synchronized settlements and terminal winner display passed | `map-anchors_20260929/runtime-visible/win-4-w0/report.json` |
| Player render inspection | Settled images inspected for both 1v1 peers and all four Multi peers: characters, night environment, lanterns and item/UI rows visible | `runtime-visible/*/*.settled-1.png` |
| Natural Solo | One match, two rounds, four combats, normal BT and return to lobby; 9 checks, no recorded errors; UGS never initialized | `map-anchors_20260929/solo/report.json` and `summary.json` |
| Preservation | 1,904 existing asset/source/settings baseline files: 1,897 unchanged, 7 intended changes, 0 missing | `map-anchors_20260929/baseline-result.json` |
| Metadata | No missing/orphan metadata in the new Maps runtime/editor/assets folders | `map-anchors_20260929/meta-check.json` |
| Final Editor state | Not compiling; compilation failed=false; current console errors/warnings=0; prior build diagnostics retained in build report | `map-anchors_20260929/console-final.json` |

Evidence folder paths above are relative to `output/validation/`. Test players exited. The map preview scene and Map Library remain available in Edit Mode. Temporary previews are authoring aids, not persistent scene changes.

The seven intended baseline changes are the three gameplay scenes, NightPavilionPreview scene, PlayerSpawnManager, AZPlayerVisual and MultiPerspectiveLayout. New map data/editor/runtime/test files are additions outside that baseline. Existing dirty gameplay/refactor/cosmetic/side-sprite work was retained.

## Issues found and handled

- The existing preview used a wider/more distant background than the game scenes. Preserved it as a fourth preset instead of overwriting it with the standard night map.
- Initial map Undo/Redo restored references before recreated objects existed. Reordered Undo registration so redo recreates the new map before restoring metadata/lighting references, then verified both directions.
- Initial Editor namespace collision with `Editor` was fixed by explicitly deriving custom inspectors from `UnityEditor.Editor`.
- An async Editor test request paused on Unity's scene-save modal. Inspected the actual window and saved the changed preview; all 284 tests then completed. No Editor restart or forced recompilation was used.
- Hidden Player windows produced black framebuffer captures despite passing state checks. Those runs are retained in `runtime/` as logic evidence only. Repeated with visible windows in `runtime-visible/`; inspected real settled frames. Initial `start` captures can still show the loading screen and are not accepted as gameplay visual evidence.
- The Solo result capture has the previously recorded incomplete-background limitation. Solo's state/lifecycle pass is valid; that image is **not** evidence that the full Solo environment rendering passed. See N039-02 below and existing PLAN_037 R02 evidence debt.
- Tool-only retries: build options needed a JSON string array; the legacy `powershell.exe` launcher disallowed a script, so the existing script ran through the current task shell without changing execution policy. A temporary window-framing helper encountered a missing SceneView after maximizing the tool; added its null check. None changed gameplay or security settings.

## Current boundaries and deferred checks

| ID | Status | Exact stimulus / evidence needed |
|---|---|---|
| N039-01 | Deferred visual integration | Run item targeting/arrow, attack/defence, ghosts and every weather/light transition after substantial user-defined anchor moves. Confirm framing and overlays on each viewer. Current tests cover defaults plus isolated moved-anchor/item transforms, not arbitrary camera compositions. |
| N039-02 | Partial | Solo full-match logic passed. Capture the actual visible Solo window during play and at result, with full environment/camera composition. The current probe result image is incomplete and does not close this check. |
| N039-03 | Deferred network coverage | Actual three-client-count match and Relay transport with the new packaged maps were not rerun. Three-player seat mapping was tested in Editor; actual four-peer local UTP was tested. No transport/authority code was changed. |
| N039-04 | Deferred performance | Profile intended target devices/resolutions and shipping build; this pass is a Windows Development build, not a frame-time/memory budget sign-off. |
| N039-05 | Future feature | Runtime lobby map choice/voting, replicated map IDs, loading readiness and mid-match swaps are not implemented. Current map selection happens in the Editor before building/loading scenes. |

Existing [night integration checks](../NIGHT_ENVIRONMENT_SETUP.md#remaining-integration-checks) remain authoritative for broader weather/item/performance work. PLAN_037 remains at R00/R01 passed; this map task does not advance R02–R13 or the wardrobe feature.
