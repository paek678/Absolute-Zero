# Pixel-extracted side character reference

Status: comparison/intermediate only. The production-shaped single-atlas replacement is documented in `PLAN_side_sprite_atlas.md`.

## Source and intent
- Source: `output/imagegen/player_side_20260922/player_side_assembled.png`, the high-resolution transparent version of the user's approved side composite.
- Preserve the source's visible pixels, proportions and coordinates. Do not regenerate body parts.
- Follow the front prefab's nine visual categories: backHair, head, frontHair, two side locks, body, two arms and lowerbody, plus an empty item slot.

## Implementation
- Every extracted layer keeps the complete 1157 x 1359 source canvas, center pivot, 180 PPU, local position zero and local scale one.
- Visible source pixels are assigned to one semantic layer by color-connected seeds and spatial ownership. The parts therefore reconstruct the approved idle composite without manual scaling or positioning.
- Internal joins receive a three-pixel copy of the same source pixels to prevent bilinear sampling cracks in Unity. No newly generated drawing is introduced.
- `SideExtractedBuilder` validates pivot-edit capability, imports the layers, creates the local SpriteLibrary, prefab and comparison scene.

## Outputs
- `Assets/Tests/SideDrinkPreview/ReferenceExtracted/Art/`
- `Assets/Tests/SideDrinkPreview/ReferenceExtracted/SideCharacterExtracted.prefab`
- `Assets/Tests/SideDrinkPreview/ReferenceExtracted/SideExtractedSpriteLibrary.asset`
- `Assets/Tests/SideDrinkPreview/ReferenceExtracted/SideExtractedTest.unity`
- `output/imagegen/side-reference-extracted/layer-map.png`
- `output/imagegen/side-reference-extracted/unity-assembled-final.png`

## Validation and limits
- Unity 6000.3.11f1 imported all nine sprite layers. The prefab has all transforms at local position zero and scale one, expected sorting orders, and SpriteResolvers on head, body and both arms.
- Unity compilation succeeded. The final Game-view capture has no visible inter-layer gaps.
- The source extraction represents the approved idle pose. Areas hidden in the composite do not exist yet; they must be painted before large independent limb/hair motion or bone deformation.
