# Cosmetic atlas setup

## Ready-to-use assets

- Reference character: `Assets/MainFolder/Sprite/player.prefab`.
- Isolated example prefab: `Assets/Prefabs/CosmeticAtlasPreview.prefab`. Its `CosmeticPreview.Items` selects example equipment. Enter Play in your own preview scene, or invoke the component's **Refresh Cosmetic Preview** context menu. The gameplay prefab/scene hierarchy was not moved or replaced.
- Mapping assets and equipment templates: `Assets/Data/Cosmetics/AtlasBindings/`.
- Runtime catalog: `Assets/Data/Cosmetics/CosmeticRegistry.asset`.
- Existing selectable hat: `Assets/Data/Cosmetics/Items/hat_01.asset`; three additional examples are in AtlasBindings. All four reuse slices from the existing `hat.png`; no new art was generated.

| Mapping | Scope | Prepared entries |
|---|---|---:|
| `head_ref_atlas.asset` | Head poses, three front-hair layers, back hair, optional hat overlay | 20 |
| `top_ref_atlas.asset` | Body, arm1, arm2 plus separate first-person hand poses | 40 (8 first-person) |
| `low_ref_atlas.asset` | Current lower-body sprite | 1 |
| `back_ref_atlas.asset` | Optional overlay attached to body | 1 |
| `tail_ref_atlas.asset` | Optional overlay attached to lower body | 1 |
| `hat_01_atlas` through `hat_04_atlas` | Four fitted sample overlays | 1 each |

The five equipment slots remain Head / Top / Back / Bottom / Tail. A Head **set** can include hair, face and hat together; this change does not introduce independently equipped hair/face/hat slots.

## Add your artwork

1. Import the new texture as **Sprite (2D and UI), Multiple** and slice it into individual parts/poses. An atlas here means the source sprite sheet; a Unity SpriteAtlas packing asset alone is not the equipment definition.
2. Keep pixel density, pivot and pose geometry compatible with the original slice shown in **Source**. Animation transforms and bones remain unchanged. Joint seams and missing poses need authored artwork, not automatic rig generation.
3. Duplicate an appropriate `*_ref_atlas` and equipment `*_ref` asset for each new set. Give the equipment a unique ID of 1–8 lowercase letters, digits or underscores; keep its Part consistent with the intended slot. Assign the mapping asset to **Atlas** and add the equipment asset to the catalog's **All Items** list.
4. In the mapping asset, assign **Replacement** to each desired slice. **Source** is the original animation sprite; preserve it. **Note** records the original atlas path and pose. Empty Replacement retains the original sprite, including during animations.
5. Optional bulk fill: choose **Original atlas** and **Replacement atlas**, then **Fill empty replacements by exact sprite name**. The replacement texture must already be sliced. Only exact, unique sprite-name matches fill empty entries; existing edits, unknown names and ambiguous matches remain unchanged.
6. For added accessories, enable **Overlay**. Set **Renderer Path**, Replacement, Local Position, Local Scale, Rotation and Sort Offset. Null Source means the overlay follows all poses. A specified Source limits it to that original pose. Swap entries preserve the original renderer's transform/order; offset fields apply only to overlays.
7. Select the new equipment in the existing Closet, save using its normal flow, then enter a new multiplayer match. All peers must use a build containing the same catalog and sprite assets.

**Original paths:** `body`, `body/arm1`, `body/arm2`, `body/head`, `body/head/frontHair`, `body/head/frontHair (1)`, `body/head/frontHair (2)`, `body/head/backHair`, `lowerbody`. Arm1/arm2 are the actual prefab names; inspect the reference sprite to determine anatomical left/right instead of guessing from screen orientation. The combat `item` renderer is not clothing.

**First-person hands:** use entries with View=FirstPerson and their prepared hand paths/source sprites. Third-person arm slices do not automatically become first-person hands. Leaving these empty keeps existing first-person artwork.

## Animation and multiplayer behavior

Animator/SpriteResolver chooses the original pose. The atlas renderer remaps that pose's final sprite in LateUpdate without editing imported SpriteLibraries or clips. Removing equipment restores the latest original pose. Overlays inherit parent transforms, tint and visibility, including death/ghost transitions.

The existing owner RPC submits a compact set of equipment IDs. The server validates version, byte length, catalog ID and equipment part, then replicates retained state. Remote visuals read that retained state when they bind, in addition to observing later changes. Owner first-person visuals consume the same accepted state. No texture pixels or client-authored transform data travel over the network. Appearance remains fixed after the existing first accepted in-match submission; live wardrobe changes during a match are not added here.

## Closet screen and authoring handoff

- Screen prefab: `Assets/Prefabs/UI/Closet/ClosetPanel.prefab`, referenced by `AZLobbyUI` in `Assets/Scenes/LobbyScene.unity`.
- Visual-only preview prefab: `Assets/Prefabs/UI/Closet/ClosetPreviewVisual.prefab`. It contains sprite transforms/renderers, without a network player or gameplay initialization.
- `ClosetViewBindings` owns the explicit UI references. `ClosetPreviewSurface` references the visual prefab and the same cosmetic registry as the game. Keep its reserved `ClosetPreview` layer separate from gameplay objects.
- Selecting a card changes only the temporary preview. **Equip** commits that slot; **Unequip** clears it. Changing tabs or resetting the preview discards the temporary selection. **Close** saves committed equipment on this PC; save failure keeps the screen open for retry. Online publication is reported separately.
- The existing Main-menu button opens the Closet. Editing inside a room or match is not enabled. Enter a new match to submit the saved set; Solo keeps the bot's own configured appearance.
- The screen owns one camera, render texture and disposable visual. Closing/disabling/unloading releases them and restores camera masks. Do not place the preview camera in the gameplay prefab or persist it across scenes.
- New catalog items appear in their existing five-part category without a UI code change. Assign `Sprite` to a meaningful card icon; an empty icon shows a neutral placeholder. Empty replacement poses continue using the original art.
- Frame sizing measures the supplied catalog when the preview opens, keeping zoom stable while selecting items. After replacing atlas references, close and reopen the screen, then inspect idle, drink, defense and first-person mappings in a new game. Oversized overlays and seams need artwork/atlas fitting; the preview does not repair them.

`ClosetLayoutSetup.BuildCandidate` is a layout-authoring tool, not a routine runtime refresh: it rebuilds the candidate prefab. Preserve existing overrides and reconnect the feature with `ClosetFeatureSetup.BuildPreview` before intentionally regenerating it. Do not regenerate either prefab merely to add a catalog item.

## Validation and limits

See `Docs/Validation/PLAN_035_results.md` for current compile, automated tests and actual Relay evidence. Final replacement sprites supplied later need animation-by-animation visual inspection. The sample hats and reference mappings establish the pipeline; they do not certify future art fit or revive the paused side-view animation work.
