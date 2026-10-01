# PLAN_038 - Night Forest Environment

## Initial scope

Create a reusable night environment: one wooden floor, a distant four-wall image backdrop, and lighting that keeps the existing 2.5D characters readable. Deliver an independent preview scene and environment prefab. Keep gameplay scenes, camera/seat rules, shared materials, networking and game balance unchanged.

## Implementation and verification

1. Inspect the current Multi floor, backdrop, camera and sprite shader. Preserve hashes of existing assets/settings/source.
2. Generate one night forest panorama and one neutral wooden-floor albedo with built-in image generation. Save project-local textures and record prompts.
3. Author Unity materials and a reusable prefab through the connected Editor. Use unlit distant walls, lit ground, cool moonlight and restrained warm light around the deck.
4. Create a separate preview scene with render-only copies of existing front characters. Match the current gameplay camera and provide an overview camera for the enclosure.
5. Inspect actual renders, adjust readability/geometry, verify all four directions, enter Play Mode, check console and references, and compare the preserved baseline.

## Initial acceptance

- The floor and four backdrop walls render without pink/missing materials or visible gaps at the corners.
- Main-camera characters remain legible against a clearly nighttime background.
- No background colliders or new gameplay behaviours; decorative objects stay out of the playable center.
- Prefab, preview scene, texture paths, lighting values and integration limits are documented in `Docs/NIGHT_ENVIRONMENT_SETUP.md`.
- Existing scene, prefab, C# and project-setting hashes remain unchanged. Preview is visual validation, not a new network match test.

## Initial result

Authoring and scoped visual validation complete (2026-09-29). Prefab and preview scene saved; final Edit Mode/Play Mode renders, four camera directions, 16:9/4:3, console and asset-reference checks passed. Existing 1,873 baseline files unchanged. Gameplay scenes are preserved. Setup, evidence and deferred integration checks: [Night environment setup](../NIGHT_ENVIRONMENT_SETUP.md).

PLAN_037 remains at R00/R01 passed, R02 not started.

## Approved follow-up: preserve the existing pavilion

The user requested moving the new lanterns onto the original pavilion and changing only the backdrop and lighting. Apply this appearance to the existing shared map configuration in GameScene, GameScene_Multi and GameScene_Solo. Preserve all original environment transforms, floor/sprite resources and gameplay objects. Keep the original shared BackGround prefab intact; use scene overrides and a decoration-only prefab. Reuse the existing directional light so the weather-effect owner remains connected.

Completed 2026-09-29: `NightPavilionDressing.prefab` and `NightPavilionPreview.unity` saved; all three gameplay scenes updated. Original pavilion Unlit sprites receive a night color multiplier. Scene reload checks, preview Edit/Play captures and console checks passed. All 698 protected gameplay/other components and 108 original environment transforms unchanged; only the three intended scenes changed among 1,900 baseline files. No code, gameplay camera, player/item placement or balance changes. Full match/weather checks remain in the setup guide's N038 ledger.
