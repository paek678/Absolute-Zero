# PLAN_039 - Map presets and one-operation authoring

## Contract

The user wants each map retained as one data bundle and switched in one operation. Preserve the approved night pavilion as the current default and retain day pavilion / night forest deck as separate selectable presets. Character, item, camera, turn and network objects remain outside map ownership.

| User decision | Owner | Validation |
|---|---|---|
| Keep multiple complete maps | `Assets/Maps/<Map>/` definition + environment prefab | Four independently loadable bundles, including the preserved wide preview variant |
| Swap floor, pavilion, backdrop and lights together | MapSceneBinding + Editor application transaction | Repeat swaps and Undo, one environment/directional light |
| Keep original gameplay layout | Environment-only migration boundary | Protected scene component comparisons |
| Preserve existing weather presentation | Exactly one map directional light | Start-time discovery remains valid; no new phase/light driver |

## Choice

Use immutable ScriptableObject configuration + a composed environment prefab + an explicit Editor apply command. Each definition includes ID/name, prefab, ambient/sky/fog/reflection settings. A scene binding identifies only the owned map instance. A configurable Editor library lists presets and target gameplay scenes; the authoring window can apply to the current scene or all registered gameplay scenes.

Considered runtime spawning/switching through a new manager: deferred because mid-match replacement would invalidate the existing weather presenter's cached light and would require server-selected map IDs, replication and a loading gate. This request does not change multiplayer selection. Also rejected duplicated whole game scenes per map because they duplicate gameplay wiring. No packages, DI framework, Addressables or RPC changes are needed.

The runtime binding is metadata only. All instantiated map objects and RenderSettings are baked by the Editor and loaded normally by the existing NGO scene pipeline. No OnValidate auto-replacement, Update loop or runtime ScriptableObject writes.

## Steps

1. Preserve current dirty worktree and scene backups; inspect current environment references and lighting ownership.
2. Implement map definition, metadata binding, library and Undo-aware authoring UI. Reject invalid prefabs, unsafe external scene references, non-owned roots and Play Mode operations before deleting anything.
3. Package exact original day pavilion from the saved pre-night scene, current night pavilion, independent night forest deck and the preview's existing wide/distant-background variant. Shared art remains referenced by GUID, not copied into every map folder.
4. Migrate only environment/lights/global volume into the map ownership boundary in the three game scenes and preview. Default remains night pavilion.
5. Verify repeated swaps, serialization/reload, invalid requests, external-reference rejection, Undo/Redo and all registered scenes. Capture all four looks; run preview Play Mode. Compare non-map scene components and baseline source/assets.

## API evidence

Unity 6000.3.11f1 / URP 17.3.0 already installed. Use built-in [PrefabUtility.InstantiatePrefab](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/PrefabUtility.InstantiatePrefab.html), [Undo](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Undo.RegisterCreatedObjectUndo.html), and [RenderSettings](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/RenderSettings.html). The installed Editor exposes its RenderSettings serialized singleton through nonpublic GetRenderSettings; the Editor-only Undo adapter checks for it before mutation and fails explicitly if unavailable. No runtime reflection. No package-specific newer option is necessary for this authoring-only feature.

## Status

Implementation complete; final focused integration checks are recorded in [PLAN_039 results](../Validation/PLAN_039_results.md). Runtime lobby/map voting, server-chosen map synchronization and mid-match map changes are future scope, not implemented by authoring presets. Authoring instructions: [MAP_PRESETS_SETUP](../MAP_PRESETS_SETUP.md).

## Follow-up: editable character/spawn anchors (user approved 2026-09-29)

The user additionally requests map-owned spawn anchors with a visible indication of the character assigned to each location and adjustable placement. Current authoritative PlayerSpawnPoint3D positions are separate from client-local EnemyPlayer visual slots; MultiPerspectiveLayout currently overwrites those visual positions with constants. Both paths must consume the map layout without confusing ClientId, logical seat and local visual slot.

- Add one composed map layout with two profiles: Duel (1v1/Solo) and Multi (3/4 players). Each profile keeps explicit server-seat anchors and local remote-visual anchors. Preserve existing positions as defaults. Anchor rings mark feet; visual pivot offset and scale remain explicit data.
- PlayerSpawnManager resolves ordered server-seat anchors in the prepared gameplay scene. Keep server guards, session generations, NGO ownership, participant configuration and legacy no-map fallback. Invalid configured layouts fail preparation, never compress/reassign seats.
- AZPlayerVisual and MultiPerspectiveLayout use local display anchors while preserving stable seat-to-slot mapping. Remote item rows follow the character displacement. Camera, FPS hands, UI and unrelated gameplay roots remain under their existing owners.
- Extend the existing Map Library editor with scene/prefab anchor selection, viewer seat/player count controls, assignment labels and render-only temporary character previews. Previews contain no gameplay/network scripts, do not save into scenes/builds, and clean up on play, scene close and assembly reload.
- Verify default parity, moved-anchor resolution, invalid/duplicate/missing seats, 1v1/Solo and all Multi viewer-seat permutations, map swaps and preview lifecycle; compile and perform available focused runtime checks. Keep unexecuted Host/client/full-match scenarios in the results ledger.

This does not introduce per-seat network prefab types, runtime map switching, bot logic, cosmetic selection or camera relocation. Actual character cosmetics/ownership still come from the match participants; an editor preview model only shows placement.
