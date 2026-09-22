# Side-view sprite atlas and prefab

## Desktop archive (2026-09-22)
- Moved 988 obsolete output files (214.78 MiB) into `C:/Users/paek6/OneDrive/Desktop/AbsoluteZero_SideArchive_20260922_225950`, preserving relative paths. Includes old side animation frames, draft atlases, V3, redundant V4 backups/captures/review ZIP, old concept images and the earlier cleanup backup.
- Every moved file was verified against a SHA256 manifest stored in the archive. No Unity assets were moved. Current V4 source files, approved assembled reference and front animation reference remain.
- Removed the report generator's required V3 comparison dependency. Verified retained atlas reconstructs the assembled image exactly. Historical output and backup links below now resolve within the desktop archive where moved.

## Obsolete imported art cleanup (2026-09-22)
- Removed 35 obsolete draft/capture PNGs from Assets, together with their old preview scenes, prefabs, animation assets and builders. The retained SideDrinkPreview content is SideAtlasV4 and PreviewSprite.mat. Historical paths below may now refer to archived assets.
- Before removal, checked AssetDatabase dependencies from retained Assets: zero references into the removal set. No outside C# references to old preview classes were found.
- Restorable backup, including original meta GUIDs: `output/cleanup/side-obsolete-20260922.zip` (118 files; archive integrity checked). Exact removed roots: `output/cleanup/removed_targets.json`.
- After cleanup: Editor ready, not compiling; latest side prefab has nine renderers and zero missing sprites; original front prefab and current comparison scene remain available.

## V4 hem cleanup (2026-09-22)
- Rounded the rear shirt hem tip, removed the stray shorts trim triangle over the near thigh, and joined the far shorts edge into the leg contour. Retained stroke thickness and nine independent parts.
- Reimported atlas and rebuilt the same V4 prefab with preserved sprite IDs and updated crop offsets. Inspected Editor camera screenshot `Assets/output/imagegen/side-complete-parts-v4/unity_seams_cleaned.png`; atlas reconstruction remains pixel-identical to the offline assembled image. This pass did not retest animation.

## V4 outline and joint refinement (2026-09-22)
- Increased drawn stroke widths by 45% with rounded ends. Joined ear/hair and neck/collar paths; aligned shirt hem fill and stroke and reduced stray lowerbody outlines. Independent nine-part structure is retained.
- Updated the V4 atlas and prefab through the Editor importer, preserving sprite IDs. Captured the comparison scene in Play Mode: `output/imagegen/side-complete-parts-v4/unity_outline_refined.png`. Playback stopped afterward.
- Static assembly and appearance were inspected. Animation remains unverified. Editor-focus automation briefly failed during Play entry; subsequent screenshot and stop succeeded. Console buffer reported no new entries, but ground-truth counter showed one error; therefore no clean-console claim is made for this pass.

## V4 Unity comparison (2026-09-22)
- Imported v4 as nine Multiple sprites through the Editor data provider with capability checks. New prefab: `Assets/Tests/SideDrinkPreview/SideAtlasV4/SideCharacterV4.prefab`.
- New scene: `Assets/Tests/SideDrinkPreview/SideAtlasV4/SideFrontComparison.unity`. Original front prefab instance is on the left; side v4 is on the right. Both are normalized to the same visible height and baseline. Front Animator is disabled on this comparison instance; both use the preview material. Original front prefab is unchanged.
- Play Mode verified: nine side SpriteRenderers, zero missing sprites, and camera-rendered screenshot at `output/imagegen/side-complete-parts-v4/unity_comparison_play.png`. One initial automation eval timed out after Play entry; the focused Editor retry succeeded. No new console warning/error entries since baseline cursor 189.
- This validates static in-Editor assembly and rendering, not rigging, drinking motion, or multiplayer gameplay. Side head proportions and line sharpness remain visibly different from the front art. Stopped Play Mode after capture and left the comparison scene open.

## Reference-coordinate complete parts v4 (2026-09-22)
- Continued offline refinement in `output/imagegen/side-complete-parts-v4/`. Nine complete parts are drawn as editable curves with hidden surfaces, using shared reference coordinates and common material color fields. This revision does not extract reference pixel fragments or generate a new atlas image.
- Hair attachments omit internal ink boundaries; both sleeves and arms have independent complete shapes. Placement metadata preserves original coordinates when packing and reconstructing the atlas.
- Saved atlas reconstruction is pixel-identical to the offline assembled output. Reference silhouette intersection-over-union improved from 0.93539 (v3) to 0.96839 (v4), without registration or rescaling. This is a silhouette metric, not an overall art quality score.
- Deliverables: comparison, layer-hidden proof, atlas, individual parts, OpenRaster document, editable path data, placement metadata and review ZIP.
- Remaining: face details, hair tips, leg contours and reference paint texture still differ. Static layer inspection is not proof of joint motion, skinning or drinking animation. No Unity assets were changed.

## Complete drawn components follow-up (2026-09-22)
- The user rejected visible-region extraction as a substitute for complete art parts. New independent components are in `output/imagegen/side-complete-parts-v3/`; the earlier pixel-equal extraction is not the chosen production method.
- Built-in image generation and targeted edits drew a complete head, shirt base, both sleeve/arm/hand parts, four hair pieces and seated lowerbody. Same-color attachment outlines were reduced/removed by art edits.
- Actual atlas pieces were assembled offline and inspected with hair and arms hidden. Deliverables include the atlas, individual parts, assembled/comparison views, an OpenRaster layer document and explicit placement/shoulder metadata.
- Current state is an art review candidate. Reference likeness and seam color/alpha-edge cleanup remain incomplete; do not mark it production-ready or animation-validated. Unity assets remain unchanged.

## Offline reference-matched revision (2026-09-22)
- Current user request: preserve the approved side reference at rest, remove overlapping internal outlines, produce independently stored layers and an atlas outside Unity without further image generation.
- New output: `output/imagegen/side-atlas-reference-match/`. The prior transform-only atlas is superseded for visual reference; its Unity assets have not been replaced.
- Visible paint is retained from `player_side_20260922/player_side_assembled.png` at its original coordinates. Semantic ownership is traced again; covered surfaces are filled from the corresponding material colors. Nine cropped parts are packed into `side_idle_atlas.png`; `layout.json` retains source offsets and draw order.
- Source export alpha was normalized: nearly opaque paint made opaque, interior alpha holes filled, and stray low-alpha exterior pixels removed. The raw source is preserved. Therefore byte identity is claimed only against `reference_clean.png`, not the original PNG.
- Validation: reconstruction from the packed atlas equals `reference_clean.png` exactly (0 differing pixels). Raw source versus reconstructed image on the same gray background: mean RGB absolute error 0.3383/255, PSNR 39.66 dB. Evidence: `comparison.png`, `reference_toggle.gif`, `validation.json`.
- Editable delivery: `side_character_layers.ora`, individual layer PNGs, assembly coordinates, and the reproducible offline builder. No Unity edits or live Editor validation occurred in this revision.
- Scope of validation: static idle assembly. Independent animation, large rotations, skinning, and exposed hidden-surface art quality still require pose-specific review; these are not certified by the idle pixel comparison.

## Goal
- Match the main front character's asset structure: one PNG imported as Multiple sprites, one prefab containing separate renderers, transform offsets for assembly, and SpriteLibrary/SpriteResolver categories for replaceable parts.
- Keep the prior full-canvas pixel extraction only as comparison evidence; it is not the production-shaped atlas.

## Source structure referenced
- `Assets/Art/testProject/LastPrefab2/player_idle.png` contains nine independently drawn sprites.
- `Assets/MainFolder/Sprite/player.prefab` assembles body, arm1, arm2, head, frontHair, frontHair (1), frontHair (2), backHair and lowerbody. It also owns an item slot.
- Head, body and both arms use SpriteResolver; hair and lowerbody are direct renderers.

## Side atlas implementation
- `Assets/Tests/SideDrinkPreview/SideAtlas/side_idle.png`: one 2048 x 2048 transparent atlas with nine complete side-view parts.
- `side_idle.layout.json`: the atlas rectangles and their intended source-space anchors.
- `SideAtlasBuilder`: imports `side_idle.png` in Multiple mode, validates Sprite Editor create/name/rect/pivot capabilities, creates nine SpriteRects, and builds the prefab/scene through Unity APIs.
- `SideCharacterAtlas.prefab`: follows the front hierarchy names, sorting orders, item slot and resolver ownership.

## Art provenance
- Complete isolated part shapes reuse the previously generated ReferenceV2 art; no additional image-generation call was made for this atlas pass.
- The assembled composite was used to set target sizes and anchors. Because the original composite did not contain hidden pixels, the atlas is a reconstructed independent-part version rather than a literal extraction.

## Validation
- Unity 6000.3.11f1 imported nine sprites from the same `side_idle.png` path.
- Prefab inspection verified nine renderers, expected sorting orders, non-zero assembly positions, and four SpriteResolvers.
- Unity compilation succeeded with zero current Console errors/warnings.
- Game-view evidence: `output/imagegen/side-atlas-v1/unity-assembled-final.png`.

## Remaining art limitation
- The reconstructed hair cap still differs from the approved composite around the crown and forehead notch. This requires a targeted art edit to the atlas, not another transform-only adjustment.
