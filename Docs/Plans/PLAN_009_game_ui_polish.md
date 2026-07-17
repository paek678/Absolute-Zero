# PLAN_009 — Game UI Polish (banner / timer / score / slot layout / Korean font)

> Status: 🔄 In Progress
> Branch: `feature/minigame-greybox` (continues PLAN_008 branch)
> Source: user mockup + 6 instructions (2026-07-17)

## Tasks (user)

1. Remove persistent top-center phase text — show as a title banner that appears on phase start then fades ("준비 시간"/"공격 시간")
2. Bigger timer (clock + number)
3. Bigger round score + panel image, shown as 나|상대 (local perspective)
4. Visualize item slot positions (translucent floor pads — empty random slots visible from start, Q16)
5. Ready button moved inward, same z-row as items
6. Random item slots (max 8, 4×2) placed next to an icebox (greybox — no icebox exists in scene, spawn client-local)
7. Fix broken glyphs (□□□) — "턴 결과" title: TMP default font lacks Korean → OS-font (맑은 고딕) runtime TMP font helper applied to all runtime-built texts

## Approach

- Scene has only PlayerItem1-4 / EnemyItem1-4 markers → `ItemSlotLayout.Build(prefix)` derives at runtime: basics = markers, randoms = 4×2 grid beside the row (icebox side), icebox pos, ready-button pos. No scene edits.
- `UiFont` static helper (malgun.ttf via `Font.GetPathsToOSFonts`, dynamic TMP font) — applied in AZGameUI.CreateText + MiniGameUIBase.CreateText.
- Slot pads: SpriteRenderer white-sprite quads (alpha ~0.16) lying flat at every slot position, both sides; icebox = greybox cubes, my side only (mockup).
- Consumers: ItemWorldDisplay (my slot positions), AZGameUI (enemy item spawn positions, ready canvas position, decor spawn).

## Checklist
- [x] 1. UiFont.cs (malgun via OS font paths, dynamic TMP font) + applied in AZGameUI/MiniGameUIBase CreateText + **registered as global TMP fallback** (covers texts not routed through CreateText)
- [x] 2. ItemSlotLayout.cs — marker-derived positions (basics = markers, randoms = 4×2 grid outside the row), slot pads (translucent, random slots brighter — Q16 상시 노출), greybox icebox (body+lid, colliders removed to not block item raycasts)
- [x] 3. AZGameUI: phase banner ("준비 시간"/"공격 시간", 1.1s hold + 0.6s fade, replaces persistent phase text), timer 120→180 (icon 160 / font 62), score panel 260×100 orange with 나|상대 local-perspective mapping, ready button at row z between grid and basics, floor decor spawn, enemy items placed via layout (all 12 incl. randoms)
- [x] 4. ItemWorldDisplay: my slot positions via layout (randoms near icebox), marker/line fallback kept
- [x] 5. Compile 0 errors + docs + staged
- [ ] 6. Play-test (user): banner fade, Korean rendering (턴 결과/나|상대 — was □□□, needs fresh play session after this compile), slot pads/icebox placement, ready button position, random items appearing in the 4×2 grid

## Note — □□□ 원인
Confirmed: TMP default font (LiberationSans SDF) lacks Korean glyphs (console: "나(나)... replaced by □ in [ScoreText]"). The broken 3 glyphs the user saw = "턴 결과" result-panel title. Fixed via UiFont (per-text apply + global TMP fallback). A play session started mid-edit will still show boxes — restart play after the clean compile.
