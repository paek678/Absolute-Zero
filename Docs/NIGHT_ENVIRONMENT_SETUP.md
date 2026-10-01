# Night Forest Environment

Created and visually checked in Unity 6000.3.11f1 on 2026-09-29. Scope: [PLAN_038](Plans/PLAN_038_night_environment.md).

## Current result: original pavilion with night dressing

**Current authoring route:** use [Map Library and character anchors](MAP_PRESETS_SETUP.md). PLAN_039 packages the existing dressing, pavilion, lighting and placement into reusable presets; the manual integration details below describe the original migration. Production uses `pavilion-night`, while the preview's wider backdrop is preserved as `pavilion-night-wide`.

The follow-up request keeps the original pavilion and floor, moving only the lanterns, night backdrop and lighting into the existing map. This version is applied to `Assets/Scenes/GameScene.unity`, `GameScene_Multi.unity` and `GameScene_Solo.unity`.

- Current preview: `Assets/Scenes/EnvironmentPreviews/NightPavilionPreview.unity`.
- Decoration-only prefab: `Assets/Prefabs/Environment/NightPavilionDressing.prefab` (four backdrop planes and two lanterns; no floor or new directional/spot light).
- Final current capture: `output/validation/night-pavilion_20260929/play-pavilion.png`.
- The original pavilion/floor transforms, sprites, materials and colliders remain intact. Each scene's eight old daytime backdrop objects are disabled through scene overrides. The shared `BackGround.prefab` itself is unchanged.
- The six existing pillar/roof SpriteRenderers per scene use Unlit materials. Their color multiplier is now RGB 0.52/0.62/0.78 so they match the night lighting without replacing their art or material.
- Existing `Directional Light` and `Spot Light` objects are retuned to the moon/fill values below. The fill's world position is (0,6,-3.5), rotation (25,0,0). There is exactly one active directional light, preserving `EnvironmentVFXManager`'s existing light discovery and default restoration. Weather-event light changes remain unchanged; this is a night base appearance, not a forced lock on lighting during weather events.
- Lantern centers: world X=+/-9.2, Y=0, Z=9.7. No decorative colliders are added. The preview reference characters are not inserted into gameplay scenes.

### Follow-up validation

- Scene reload checks passed for all three gameplay scenes: 0 missing scripts, 0 invalid dressing materials, 0 active old backgrounds, 4 new backdrop walls, 2 lanterns and exactly 1 directional light in each scene.
- Before/after comparisons: 698 protected non-environment/light components unchanged; all 108 original environment transforms unchanged; all 6 original floor renderers unchanged (36 transforms and 2 floor renderers per scene). The pre-existing duplicated pavilion/floor instances were preserved.
- Of 1,900 baseline files, only the three intended gameplay scenes changed. No existing prefab, material, source file, metadata, package or project setting changed.
- New pavilion preview entered/exited Play Mode; captured at 1600 x 900, 800 x 600 and an overview in Edit and Play modes. Main views inspected. No new console warnings/errors.
- Evidence and before-scene backups: `output/validation/night-pavilion_20260929/` (`application.json`, `reload-validation.json`, `baseline-result.json`, `play-pavilion.png`). Full network matches and weather transitions were not rerun for this visual-only change; the pending integration checks below still apply.

## Original standalone prototype

- Preview scene: `Assets/Scenes/EnvironmentPreviews/NightForestPreview.unity`
- Reusable environment: `Assets/Prefabs/Environment/NightForestEnvironment.prefab`
- Forest image: `Assets/Art/Environments/NightForest/Textures/NightForestPanorama.png` (2172 x 724)
- Floor albedo: `Assets/Art/Environments/NightForest/Textures/NightDeckAlbedo.png` (1254 x 1254)
- Materials: `Assets/Art/Environments/NightForest/Materials/`
- Final camera capture: `output/validation/night-map_20260929/play-main.png`

Open the preview scene and press Play to view the environment. Its three front-character references are static, render-only copies for scale and lighting. The preview does not start a match, connect to services, or animate the characters. `Overview Camera (disabled)` is an optional inspection camera; only `Main Camera` is enabled.

## Prototype structure

```text
NightForestEnvironment (position 0, 0, 3.5)
  Ground_20x20                  One solid floor; top surface Y=0
  DistantBackdrop_64x64         Four inward-facing, unlit image planes
    Backdrop_North / East / South / West
  DeckBorderAndLanterns         Thin raised border and two warm lanterns
  Lighting                     Moon key and character fill
```

The backdrop walls sit 32 units from the environment center, with width 64 and height 40 (Y=-20 to 20). They simulate distant scenery with a single shared image texture. Each wall displays the full image; alternating walls mirror U so adjoining edges use the same image edge. This is a four-wall backdrop, not a unique 360-degree panorama or a six-face skybox. The floor and sky are not baked into the forest image.

The floor is a 20 x 20 x 0.4 cube, centered 0.2 units below its top. Its new lit material tiles the neutral wood albedo four times per axis. The edge trim top is 0.015 units above the floor to prevent coplanar flicker. Lanterns are at local X=+/-9.2, Z=6.2, outside the central character/item area. Only the floor has a collider; background and decorative meshes have none.

## Lighting

| Light | Color RGB | Intensity | Other settings |
|---|---|---:|---|
| Moon_Key | 0.68, 0.78, 1.00 | 0.70 | Directional; rotation 48, -28, 0; soft shadows, strength 0.65 |
| Character_Readability_Fill | 0.95, 0.94, 1.00 | 65 | Spot; local position 0, 6, -7; rotation 25, 0, 0; outer/inner angle 115/80; range 28; no shadows |
| Lantern WarmPool x2 | 1.00, 0.55, 0.20 | 3 each | Point; range 5.5; no shadows |

The blue directional light establishes night. The more neutral front fill keeps faces and clothing recognizable with the existing `Sprite3DLit` material. Only the directional light casts shadows. Background planes use URP Unlit, receive/cast no shadows, and have no light or reflection probes.

Scene settings (stored in the preview scene, **not** in the prefab):

- Ambient mode: Trilight. Sky RGB 0.14/0.20/0.31; equator 0.10/0.14/0.22; ground 0.05/0.065/0.10.
- Ambient intensity 1; reflection intensity 0.2; no skybox or fog.
- Main camera matches the inspected Multi view: position (0,5,-5), rotation (22,0,0), vertical FOV 65. Far clip 150, clear color RGB 0.035/0.065/0.13. No postprocessing in the preview.
- The prefab adds no custom runtime behaviours. Its four MonoBehaviours are URP `UniversalAdditionalLightData` components.

## Gameplay integration notes

The follow-up has already applied the decoration-only prefab and lighting to the existing 1v1, Multi and Solo scenes, as described above. Both preview scenes stay outside Build Settings.

For additional scenes, use `NightPavilionDressing` at world (0,0,3.5) with unit scale, preserve the existing pavilion/floor, disable only the daytime backdrop sprites, and retune the existing lights and ambient values. Do not add the standalone prototype's replacement floor or a second moon/fill rig. Review the scene's Global Volume and runtime environment controller. Keep the gameplay camera, player seats, item roots and networking objects owned by their current systems. Do not copy `PreviewOnly_CharacterScaleReferences` into a match scene.

The present fill is tuned for the project's south-facing local camera. A physically rotated or free-moving camera needs its own lighting/readability check. Four directional captures below verify backdrop coverage, not multiplayer seat presentation or character billboarding.

## Validation and adjustments

Observed in the connected Absolute Zero Editor:

- Initial render: excessive blue on characters, oversized background trees, lanterns hidden behind character silhouettes, coplanar deck trim. Adjusted fill color/intensity, full-image mapping and wall height, lantern position and trim height.
- Final Edit Mode and Play Mode: main view at 1600 x 900 and 800 x 600; four camera directions at 960 x 540; overview at 1440 x 900. Seven captures per mode, under `output/validation/night-map_20260929/`.
- Main and all four directional images inspected. Backdrop corners show no open gaps in these views; sky and forest are visible, and front-view character outlines remain readable.
- Prefab round-trip validation: 0 missing scripts, 0 missing/unsupported materials; 4 background walls; 23 environment renderers; 1 collider (floor); 27 reference-character sprite renderers in the preview scene.
- Play Mode entered, rendered and exited. Unity console: 0 errors, 0 new warnings since the pre-task cursor; automatic compilation not failed. The temporary CLI disconnect during entering Play Mode was the Editor's domain reload; subsequent capture succeeded without changing packages or forcing recompilation.
- SHA-256 comparison: all 1,873 existing scene/prefab/material/script/settings/metadata baseline files unchanged. Existing source PNG files were not edited. Temporary capture files accidentally normalized into `Assets/output` by the tool were copied to `output/validation/...` and the newly created empty staging directories removed through AssetDatabase.

Evidence: `edit-validation.json`, `play-validation.json`, `asset-validation.json`, `baseline-result.json` in the same output folder.

### Remaining integration checks

| ID | Status | Check |
|---|---|---|
| N038-01 | Scenes integrated; match check pending | Exercise actual item selection, arrows, UI, VFX and runtime lighting/environment controllers in the updated game scenes. |
| N038-02 | Scenes integrated; match check pending | Compare each player's actual local view in a match; confirm the environment substitution and scene settings on all clients. |
| N038-03 | Deferred | Profile the target player build/device; this task verified Editor rendering, not a frame-time or shipping-build budget. |

## Art provenance

Both final textures were created using the built-in `image_gen` tool, then copied into the project. No API key, downloaded art, or model-name override was used. The prompts are saved in [PLAN_038 image prompts](Validation/PLAN_038_image_prompts.md). Unity UV mapping, materials and lighting produce the final appearance; the PNG artwork was not edited in Python.
