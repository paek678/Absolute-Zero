# PLAN072 — Foreground interactions validation

Date: 2026-10-12. [Plan](../Plans/PLAN_072_foreground_interactions.md).

## Implementation

- Packaged `Resources/UI/WorldInteractionForeground` shader/material: overlay render queue, `ZTest Always`, `ZWrite Off`, premultiplied transparent sprites. Shared `WorldInteractionLayer` defines item/selection/ban/button sorting orders (20000/20001/20002/20010).
- Inventory item cards, hover outlines, ban tape, selection arrow, duel and Multi opponent icons use the foreground policy. Animated held props escape parent actor/FPS sorting groups through a child `SortingGroup.sortAtRoot`; animation parenting remains intact. Feeding and borrowed/temporary cat props also use the policy.
- Ready world canvas overrides sorting and its Image uses the foreground material. Existing screen-space UI buttons/modal ordering remain intact; no physics/input layer or network state change.
- Positions, scales, item eligibility and disabled-item alpha remain unchanged.

## Defect found and corrected during validation

The first Solo Editor diagnostic render reused a hand-fan texture across several local sprites with the shared material. Explicit per-renderer texture binding corrected the visual defect. `ForegroundSpriteTexture` preserves the property block, updates `_MainTex` on sprite replacement and subscribes/unsubscribes on enable/disable; this also covers changing cat sprites and item reuse. Final captures and the replacement/reactivation regression use the corrected implementation.

## Evidence

All paths below are under `output/`.

| Check | Result |
|---|---|
| Existing display/floor checks | `plan072-polish-tests.json`: 11/11 passed before the texture callback follow-up |
| Different textures, sprite replacement and reactivation | `plan072-texture-tests.json`: 1/1 passed on final callback code |
| Final player build | `plan072-build-final.json`: Succeeded, 0 errors / 4 existing warnings |
| Real Relay duel selection/use | `validation/plan072/duel-final/result-2.json`: passed; Cat, Ice Cream, Fan and Warm Tea presentations, both peers exited successfully |
| Real Relay four-player layout | `validation/plan072/layout-final/layout-4-w1/report.json`: passed across all four peers, 12 local/36 remote views each |
| Solo opaque occlusion comparison | `validation/plan072/occlusion-final.png` versus `occlusion-control.png`: a temporary opaque cube sits between the camera and all world objects. Final material preserves item art, X tapes and Ready; ordinary sprite/UI materials hide them. Same live positions and high sorting orders in both captures demonstrate the depth-test fix rather than just a sorting change |
| Ordinary visuals | Final four-player seat0 capture and duel Warm Tea/Cat captures inspected; correct sprite textures and UI remain visible |

The Solo occlusion fixture was paused during Resolution and temporarily activated the Ready canvas solely to include the button in the rendering comparison. This does not claim normal Ready availability in that phase. Diagnostic lookup initially missed the inactive button; the capture command was corrected to find the inactive component. Temporary blocker/material/UI state were removed by exiting the owned Solo session before stopping Play Mode. No scene was saved.

The first full shader build emitted 489 warnings: 485 in installed inference shaders plus the four existing warnings. None named the new shader. The final incremental build has four warnings; no engine/package changes were made to silence them.

## Limits and final state

- Normal foreground ordering remains deterministic when items overlap each other; this is not a layout-spacing change. Modal screen-space UI retains precedence over gameplay, as required for its existing operation.
- Focused tests cover the current Windows/URP configuration. Other graphics backends, unusual aspect ratios and a full all-item animation sweep were not separately tested.
- Existing unrelated work preserved. Editor returned to Lobby/Edit Mode. Filming remains stopped; only validation screenshots were captured. No commit/push.
