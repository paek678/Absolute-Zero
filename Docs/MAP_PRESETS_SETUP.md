# Map preset authoring

## Where the maps live

Each map is one `MapDefinitionSO` plus one composed environment prefab. Shared textures, sprites and materials are referenced by GUID, so they do not need duplicate image files.

| Map | Stable ID | Folder |
|---|---|---|
| Day pavilion | `pavilion-day` | `Assets/Maps/PavilionDay/` |
| Night pavilion (current gameplay default) | `pavilion-night` | `Assets/Maps/PavilionNight/` |
| Night forest deck | `forest-night` | `Assets/Maps/ForestNight/` |
| Night pavilion with wider/distant background (preserved preview adjustments) | `pavilion-night-wide` | `Assets/Maps/PavilionNightWide/` |

The authoring catalog and destination scenes are configured in `Assets/Maps/Editor/MapLibrary.asset`. The three registered destinations are the existing 1v1, Multi and Solo game scenes. Map IDs are distinct from scene names and are not currently sent over the network.

## Switch a map

1. Exit Play Mode. Open **Absolute Zero > Maps > Map Library**.
2. Choose a preset from the list.
3. **현재 씬에 적용 (Undo 가능)** replaces the active scene's map. Save the scene to keep the change. Undo/Redo restores both objects and lighting.
4. **등록된 전투 씬 모두 적용 · 저장** applies the selection to all scenes registered in the library and saves them. Save any open scene edits first. Destinations are checked before application; an unsaved result is kept open if application or saving fails.

Alternatively, select the scene's `Map` object and choose **교체할 맵 > 선택한 맵 적용** in its Inspector. Editing the selection alone does not replace anything.

`Assets/Scenes/EnvironmentPreviews/NightPavilionPreview.unity` is the comparison preview. Its original wider backdrop layout is preserved as `pavilion-night-wide`; production game scenes keep `pavilion-night`.

## Bundle contents and ownership

- Prefab: ground/pavilion, distant backdrop, decoration, lanterns, light components, optional global Volume/profile reference, and character placement anchors.
- Definition: ambient colors/mode/intensity, skybox, reflections, fog, shadow tint, and map sun assignment.
- `MapSceneBinding`: the applied definition and its one environment child. This component stores metadata only; it does not spawn maps, modify a ScriptableObject or drive gameplay at runtime.
- Characters, items, cameras, UI, game managers and NGO objects stay outside `Map`. The map owns placement **anchors**, not the gameplay actors. Existing scene spawn markers remain as a legacy fallback; a configured map layout takes precedence.

Apply is an Editor operation. It writes real scene objects and RenderSettings, so existing scene loading works without a new runtime initialization order. The weather presenter discovers the one map Directional Light during `Start` as before. Mid-match replacement is blocked by the authoring tools.

The initial assets use realtime lighting. Baked lightmaps/light probe sets/occlusion data are not packaged by this tool; add explicit ownership and validation before introducing baked map variants.

## Add another map

1. Duplicate an appropriate definition and prefab into `Assets/Maps/<NewMap>/`. Keep the `.meta` files with their respective assets when moving them.
2. Give the definition a new stable ID, display name and the new prefab reference.
3. In Prefab Mode, edit the floor/backdrop/decorations/lights/anchors. Keep the prefab root active at position `(0,0,0)`, rotation `(0,0,0)`, scale `(1,1,1)`. Keep the scene's `Map` binding at the scene root. Check the existing gameplay camera and item interaction area when adjusting seating.
4. Keep exactly one active Directional Light. Do not put cameras, AudioListeners, network objects, gameplay controllers or `MapSceneBinding` in the map prefab.
5. Configure ambient/fog/reflections in the definition's `Lighting` fields. Light color/intensity/position and Volume profile references live in the prefab.
6. Add the definition to `MapLibrary.asset > Maps`. Apply it to the preview first, then the registered game scenes when ready.

Editing an instantiated map is an ordinary prefab override. Reapplying replaces the instance from the definition; save intended prefab edits before reapplying. Scene objects outside the map must not serialize references into disposable map children. Application rejects such dependencies instead of silently leaving broken references.

## Adjust character placement

1. In **Map Library**, choose the map and click **선택한 맵 프리팹 열기 · 앵커 편집**. Editing the prefab makes the placement reusable by every scene using that map.
2. Under **캐릭터 배치 미리보기**, choose `Duel` (1v1 and Solo) or `Multi` (3/4 players), participant count, and **내 좌석 (0부터)**. `0` is P1, `1` is P2, and so on.
3. Turn on **캐릭터 미리보기 켜기**. Each remote display slot shows its current occupant in the selection button and Scene-view label. Enable Scene-view Gizmos to see the labels/rings.
4. Click **표시 슬롯 ... 앵커 선택**, then move the selected anchor with the Move tool or Inspector Position. The yellow ring is the floor/feet reference; the renderer-only character follows it. Use anchor Rotation for facing and `Visual Scale` for sprite size. `Visual Pivot Offset` is the offset from floor to sprite pivot (default Y=1.6); Transform Scale is not character size.
5. If needed, enable **서버 스폰 앵커 표시** to select the cyan logical-seat anchors. These set actual server-created player object **positions**. Player object rotation remains identity, as before; presentation facing belongs to the yellow visual anchors.
6. Save the prefab and return to the game scene/preview. Map Library can apply the map to the current scene or all registered scenes. Play to check item interactions and camera framing after substantial moves.

### Two placement roles

| Anchor group | Count | Purpose |
|---|---:|---|
| `Duel_CharacterAnchors/ServerSeat_*` | 2 | P1/P2 authoritative spawn positions, shared by 1v1 and Solo |
| `Duel_CharacterAnchors/RemoteView_0_Feet` | 1 | Opponent/bot's visible position from the local player's view |
| `Multi_CharacterAnchors/ServerSeat_*` | 4 | P1–P4 authoritative spawn positions |
| `Multi_CharacterAnchors/RemoteView_*_Feet` | 3 | Local west/north/east opponent presentation slots |

Each map has 10 anchors. The local player keeps the existing first-person hand/camera presentation. Remote seats occupy slots in seat order excluding the local seat: local P1 sees P2/P3/P4; local P3 sees P1/P2/P4. With three participants the final unused display slot is empty. The editor selector previews this existing identity rule; it does not change player ownership or assign a new network prefab.

Changing **only a blue server anchor** does not move the visible opponent: the game intentionally has separate network objects and local presentation roots. Move the yellow anchor to change the screen composition. Multi remote item rows follow that visual anchor's displacement and rotation; item spacing remains unchanged. Camera/FPS hands/local item row/icebox positions are not automatically moved.

Do not change `Mode`, `Role`, `Index`, disable or remove required anchors just to hide their gizmos. Use the Map Library display options. Missing/duplicate/inactive configured anchors reject match preparation instead of silently assigning another seat. Keep both complete profiles when duplicating a map.

The preview template is `MapLibrary.asset > Character Preview Prefab`, currently the main front-facing character prefab. It is a position/scale reference, not an animated or per-player cosmetic preview. The renderer-only copies contain no gameplay or network behaviours and are excluded from scene/build serialization. They are removed on preview stop, map replacement, prefab/scene close, Play Mode entry and assembly reload; temporarily hidden existing comparison renderers are restored.

## Scope and remaining integration work

This implements one-operation **Editor** map selection. Runtime lobby selection, server-authoritative map IDs, client loading readiness, map voting, and mid-match transitions are separate future features. The IDs/data structure provide an extension point but those flows are not implemented here.

Current evidence and deferred checks: [PLAN_039 results](Validation/PLAN_039_results.md). Earlier full-match/night-weather visual checks remain in [night environment integration checks](NIGHT_ENVIRONMENT_SETUP.md#remaining-integration-checks); focused placement tests do not close every weather/item/performance check.
