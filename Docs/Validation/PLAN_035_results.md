# PLAN 035 — Cosmetic atlas validation

Date: 2026-09-27. Scope: the existing front character, five equipment sets, per-pose sprite mappings and multiplayer appearance binding.

## Implemented

- Optional read-only atlas mappings per equipment item; empty replacements preserve the current original pose. Existing overlay/swap items remain supported.
- LateUpdate mapping follows Animator/SpriteResolver selection without changing clips or imported sprite libraries. Clearing restores the latest original pose. Overlay layers follow source visibility, tint and flipping.
- Correct nested head anchor (`body/head`), retained network state on late visual binding, separate owner first-person mapping and cleanup on despawn.
- Five templates with 63 prepared entries, including eight first-person entries; four sample hats from existing artwork; isolated preview prefab and exact-name bulk assignment tool.
- Existing compact IDs and server catalog/part validation retained. Sprite pixels are not sent over the network.

## Evidence

Evidence root: `output/validation/plan035/`.

| Check | Result | Evidence |
|---|---|---|
| Atlas pose switching, clear/restore, empty mapping, view filter, overlay visibility, isolation, invalid DTO/path/catalog admission | 8/8 passed | `tests-catalog.json` |
| Existing combat regression | 38/38 passed | `combat-regression.json` |
| Development player build | 0 errors; final incremental build 4 warnings | `build-final.json` |
| Actual Relay, Host + 3 separately authenticated clients, four distinct hats, two turns | Passed; all four agreed on appearance IDs and actual remote overlay sprite binding; two state checkpoints agreed | `relay_final/report.json`, player logs |
| Four-player captures | 32 PNGs captured; representative host target frame visually reviewed | `relay_final/host.0002.turn-01-target-west-P1.png` |
| Local UTP 1v1 combat and rematch reset | Passed; two checkpoints equal, both processes exit 0, no matched errors | `duel_final/duel-2-w0/report.json` |
| Whitespace check | Passed | `git diff --check` |

The final Relay signature was `0:hat_01;1:hat_04;2:hat_03;3:hat_02` on all four peers. Arrival order differs from launch order; appearance follows network identity. An earlier independent Relay run also passed (`relay_hats/report.json`). Representative rendering shows distinct hats attached to opponent heads. Health bars and the debug overlay partially cover hats; these captures are not a claim of final UI/art fitting.

## Failed attempt and warning accounting

The initial Editor authoring class had an `Editor` namespace ambiguity. It was fixed by qualifying `UnityEditor.Editor`. `tests-initial.json` reports zero tests during that compile failure and is not a passing run. Actual nonzero test summaries above were checked after compilation recovered.

The first full build reported 489 warnings, predominantly existing AI Inference compute-shader variants, plus existing unused-field/runtime pipeline configuration warnings (`build-warnings.txt`, `build.json`). The incremental final build's four warnings do not mean cached shader warnings were fixed. No package changes were made for this work.

## Remaining authoring and validation limits

- Replacement clothing/hair/FPS sprites have not been supplied. The blank templates intentionally retain original art. Each new set needs pivot/PPU, outline, animation-pose and first-person visual checks after assignment.
- Head is one equipment set containing optional hair/face/hat mappings; these are not independent equipment categories.
- All players must ship the same registered IDs and assets. Existing first accepted appearance submission is preserved; this is not an in-match wardrobe editing protocol.
- The multiplayer probe equips fixtures without saving and verifies the network/render route. It does not automate every manual wardrobe UI interaction.
- Side-view artwork/animation remains paused. Existing scene layouts and unrelated pending work were preserved.

Usage: [Cosmetic atlas setup](../COSMETIC_ATLAS_SETUP.md).
