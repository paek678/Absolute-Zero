# PLAN072 — Foreground item and button rendering

2026-10-12: User requests all item/button objects to remain visible over other scene objects.

- Share a packaged unlit sprite material with transparent alpha, no depth writes and unconditional depth testing. High sorting order alone cannot defeat opaque map depth.
- Apply to local/remote inventory in all modes, selection/ban decorations, held item props and temporary feeding/cat props. Preserve sprite flip/tint and existing transforms, animations and alpha.
- Ready world canvas sorts above item sprites and uses the same material for its unmasked button Image. Other screen-space buttons already draw after world geometry; keep modal/input hierarchy intact.
- Scope is rendering only. No physics layer, RPC, effect, slot position/scale or inventory authority changes.
- Validate actual rendering against an opaque foreground blocker, ordinary Solo UI, fresh player build and duel/four-player layouts/interaction captures. Preserve unrelated dirty work.
