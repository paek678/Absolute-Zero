# Multi inventory slot places — 2026-10-11

## Current: original Duel sprite revision

Follow-up contact adjustment: `BorderContactScale = 1.08` enlarges the painted sprite width/depth by 8% (increased from 5% at the user's request) around each unchanged slot center. This slightly overlaps irregular brush edges to close visible gaps. Final build zero errors/four warnings; actual Relay four-player launch and all four camera probes passed (3-degree pan, return 0, plane error 0). Verified the connected borders in `output/validation/plan065/contact108/runtime/players-4/0.game.png`; evidence and run summary are under `contact108`. Test players exited; Editor LobbyScene clean/EditMode.

The user rejected the artificial straight mesh borders and requested the actual Duel sprite. This supersedes the procedural mesh revision below.

- `InventorySlotGrid` now creates SpriteRenderers with the original `Resources/ItemSprite/itemPlace` sprite and `sprite3DMat`; removed generated meshes, custom materials and stroke geometry.
- Preserved all current anchor positions and cell dimensions. Fits the measured painted alpha bounds of the unchanged 256x256 original (242x245 pixels) so its transparent margin does not leave gaps between cells. Full white color matches Duel.
- Original sprite/material assets and Duel scene are unchanged. Horizontal slot sprites are excluded from screen-aligned artwork as before.
- Updated opt-in `Plan056CameraProbe`: ItemPlace cells are checked for horizontal orientation, whereas icons/characters still require camera alignment. Initial runtime result failed because the old probe classified new floor sprites as standing icons (68 degrees); final four peers all pass, pan 3 degrees, restore 0, orientation error 0.
- Camera EditMode tests 19/19; final build zero errors / four warnings. Real Relay four-player startup and all four camera probes passed. Final actual capture: `output/validation/plan065/duel-sprite/final/runtime/players-4/0.game.png` (1600x900).
- Evidence: `duel-sprite/tests.json`, `duel-sprite/build-final.json`, `duel-sprite/final/runtime/summary.json`, each peer's `.camera-result.txt`, under `output/validation/plan065`.
- Source texture borders remain very thin at the far opponent's perspective; no substitute artwork was generated. Latest executable updated; test processes exited, Editor LobbyScene clean/EditMode. Protected font/bot graph hashes preserved. No new 12-item or disconnect scenario for this sprite-only revision.

## Approved joined-grid revision

The user approved the generated preview with touching cells. This revision supersedes the individual marker sizes below.

- `InventorySlotGrid` builds one horizontal mesh per rack. Shared borders and intersections are drawn once, avoiding double opacity/thickness. The owned mesh/material are released on destruction.
- `MultiPerspectiveLayout` derives cell width from the unchanged anchor spacing. Local racks: 8x1, 2x1, 2x1, preserving the Ready button gap. Side opponents: 6x2; north: 12x1. Empty slots remain visible.
- Local widths are 1.0 / 1.5; depths 1.0 / 1.2. Remote widths follow their anchor step; side depth is 0.60. White alpha 0.85, stroke 0.045 local / 0.055 remote to improve distance readability.
- Old local ItemPlace sprite renderers are disabled only in Multi runtime. Grid heights follow item anchor height. Existing item/character positions, 1v1 art, and network state remain unchanged.
- Validation: fixed-slot tests 5/5, final build zero errors / four warnings, final Relay 8-item and 12-item scenarios both passed with four peers each and layout verification. Evidence: `output/validation/plan065/joined/release/matrix.json`, `joined/build-release.json`, `joined/tests.json`.
- Inspected host/client actual captures: `joined/release/layout-4-w0/seat0.layout-full.png` and `seat2.layout-full.png` under the same evidence directory. Remote rows remain perspective-compressed at the 960x540 capture resolution; this is the actual game render, not the generated mockup.
- Editor remains LobbyScene / clean / EditMode. No dedicated disconnect or death replay in this revision. Font and bot graph hashes preserved.

## Request and implementation

Show the Duel white square place artwork under Multi item spawn positions.

- `MultiPerspectiveLayout` reuses `Resources/ItemSprite/itemPlace` and `sprite3DMat`.
- Enables/resizes the 12 existing local anchor children; creates the same markers under each remote rack's 12 fixed anchors, including empty places before dealing.
- Markers share rack heading toward the icebox. Local width is 0.86; side racks 0.50; north rack 0.36. Alpha is 0.65.
- Uses the inventory presentation height, like Duel. Initial map-floor placement left remote markers visibly below their icons; corrected to local anchor height and remote anchor height minus 0.20.
- Markers stay horizontal and are excluded from the existing artwork billboard discovery. No colliders, item position changes, gameplay state, RPC or inventory allocation changes.
- Remote marker roots hide when no local perspective or corresponding participant remains. Scene ownership cleans them up on scene exit.

## Validation

- Fixed inventory slot tests: 5/5 passed (`output/validation/plan065/tests.json`).
- Camera facing tests: 19/19 passed (`facing-tests.json`).
- Final player build: succeeded, zero errors, four warnings (`build-final.json`).
- Final real Relay layout scenarios: 8 items and 12 items, four peers each, both passed including layout verification (`final/matrix.json`).
- Inspected host and client captures with empty places, plus full host capture. Squares align under items without changing character or inventory positions.
- Editor left in LobbyScene, clean, EditMode, not compiling. Test players exited. Existing font and bot graph hashes unchanged.

## Evidence and limits

- Empty places: `output/validation/plan065/final/layout-4-w0/seat0.layout-full.png`.
- Full places: `output/validation/plan065/final/layout-4-w1/seat0.layout-full.png`.
- Remote perspective: `output/validation/plan065/final/layout-4-w0/seat2.layout-full.png`.
- The original thin border becomes small at remote viewing distance. Duel scene/art assets were not edited.
- Dedicated disconnect and death/ghost marker visibility were inspected in code, not separately replayed for this presentation change.
