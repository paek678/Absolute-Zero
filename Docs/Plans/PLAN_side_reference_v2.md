# Rejected generated side-character attempt

This generated-parts approach was rejected after visual review. It did not extract the approved composite's pixels and must not be used as the production reference. The replacement is `PLAN_side_reference_extracted.md`.

## Source and scope
- Inspected `Assets/MainFolder/Sprite/player.prefab` and `Assets/Art/testProject/LastPrefab2/player.prefab` through the Editor.
- Both use the `LastPrefab2/player_idle.png` atlas: body, arm1, arm2, head, frontHair, frontHair (1), frontHair (2), backHair, lowerbody plus an item slot. Head/body/arms use SpriteResolver and SpriteLibrary rather than SpriteSkin.
- Create a separate right-facing idle assembly using this part contract and the user's approved assembled side reference. Original gameplay assets and the previous drink rig are preserved.

## Deliverables
- `Assets/Tests/SideDrinkPreview/ReferenceV2/Art/`: nine transparent parts.
- `SideCharacterReference.prefab`, `SideSpriteLibrary.asset`, `SideReferenceTest.unity` in ReferenceV2.
- Reproducible assembly in `Editor/SideReferenceBuilder.cs`.
- Generation source/prompt and actual Unity screenshot under `output/imagegen/side-reference-v2/`.

## Validation and limits
- Actual front prefab renderer order/components inspected. Side prefab has nine sprite-backed parts and an inactive item slot; head, body and both arms resolve idle through a local SpriteLibrary.
- Generated using the built-in image tool, with front atlas as structural reference and approved side image as visual reference. Alpha bounding-box cropping only was used to split output; no procedural drawing.
- Imported and assembled in Unity; first capture revealed undersized face and oversized rear hair, corrected in the prefab and builder and recaptured.
- This is an idle art assembly, not a completed animated or multiplayer-integrated replacement. No new drink animation or SpriteSkin weights are claimed.
- Generation is approximate: hair attachment colors/edges and the overall silhouette still differ from the approved image. Do not describe this as pixel-exact extraction.
