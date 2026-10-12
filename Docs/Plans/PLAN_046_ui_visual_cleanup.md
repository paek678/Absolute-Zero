# PLAN046 - Lobby, wardrobe and targeting visual cleanup

## Status and scope

- 2026-10-06: user authorized implementation and task-by-task validation. T01-T04 are implemented; actual evidence and remaining acceptance checks are tracked in [PLAN046 results](../Validation/PLAN_046_results.md).
- Baseline: `0109dd8` (`UI 디자인 리소스 추가`). Existing dirty SoloDuelDecision graph and MalgunGothic font are unrelated and must remain untouched.
- Evidence: current C# and serialized assets, plus visual inspection of the referenced background and alarm textures in the preceding investigation. No fresh Play Mode verification is claimed.
- Apply only missing changes. Recheck each owner and the actual scene instance before implementation; skip an already-correct item rather than recreate it.
- Interpret “옷장 하의 배너” as the lower-body category tab, consistent with the preceding proposal. If a different banner is identified, adjust that task before editing.
- 2026-10-06 review: feasible for implementation after the bounded corrections below were incorporated. This is a design-readiness verdict, not approval of unimplemented runtime behavior. Evidence and deferred gates: [PLAN046 review](../Validation/PLAN_046_review.md).

## User request to implementation map

| Task | Requested outcome | Current evidence | Preserve/reuse |
|---|---|---|---|
| T01 | Remove Gemini mark from main lobby background | LobbyScene/LobbyBackground references GUID `93de78a17ae79ff4fb40d854e461cb78`, the Resources background PNG; lower-right mark is baked into the image | Existing composition, dimensions, import metadata and lobby layout |
| T02 | Remove the timed “공격 시간” banner | GameHudBuilder creates TimeAlarm with `attacktime_0`; MatchHudPresenter shows it for remaining preparation time 1–5 seconds | Countdown, clock, phase progression and existing warning audio behavior |
| T03 | Remove wardrobe lower-body tab from the screen | ClosetPanel/Tab_Bottom is active; ClosetView casts tab-array index to CosmeticPart; bindings require five entries | Saved appearance, enum values, network serialization, base lower body and existing catalog |
| T04 | Replace long targeting ribbon with a small red downward marker above the target | TargetingArrowPresenter draws a yellow curved ribbon; GameUIManager owns Multi single-target aim/snap/confirm | Existing eligibility, seat mapping, input state, RPC validation, cancellation and inventory identity |

## Ordered implementation tasks

### T01 - Background mark

1. Verify the sprite used by the actual LobbyScene renderer, including any scene override; do not edit the similarly named unused image under Assets merely because its name matches.
2. Locally repair only the lower-right logo region using the existing image as the reference. Keep the rest of the composition unchanged to the greatest extent possible; reject a broad regenerated background.
3. Preserve texture size, sprite pivot, pixels-per-unit and `.meta` GUID. Compare the edited image and lobby before/after.
4. Inspect at 1920x1080 and 1280x720, plus a non-16:9 window: no repair seam, framing shift or menu overlap.

Pass gate: the mark is absent and the lobby retains its framing. An already-clean referenced texture needs no change.

### T02 - Timed attack banner

Owners: `GameHudBuilder`, `GameHudRefs`, `MatchHudPresenter`.

1. Remove the banner creation/binding and banner-only jitter handling together, after checking all references.
2. Separate countdown warning audio activation from the banner object. Simply deleting the object would hit the existing null guard and suppress the clock sound too.
3. Retain the existing 1–5-second countdown warning condition, timer color, clock and normal turn deadline. Keep unrelated SummerVacation timer movement intact.
4. Ensure warning audio stops on phase exit, disable/destruction and scene departure; do not create duplicate tick playback on repeated snapshots.
   - `MatchHudPresenter.OnDisable` currently clears shake offsets only. Add explicit warning stop/reset and prevent still-subscribed snapshot callbacks from restarting audio while disabled. On re-enable, reconcile once from the current snapshot, with initialization guards.
5. Check Solo, online 1v1 and Multi countdowns: 6 -> 5 -> 1 -> 0, early Ready and exit while the warning is active.

Pass gate: no attack-time graphic or its shake; countdown/audio behavior and transition timing match the baseline. Do not delete the source texture unless a separate unused-asset cleanup is requested.

### T03 - Hide wardrobe lower-body category

Owners: `ClosetPanel.prefab`, the actual lobby closet instance, `ClosetLayoutSetup`; inspect `ClosetFeatureSetup` and `LobbyUISetup` only for active regeneration paths.

1. Keep all five serialized tab references and their existing CosmeticPart indices. Disable the `Tab_Bottom` GameObject; show Head / Top / Back / Tail.
2. Let the horizontal layout distribute the four visible tabs. Do not shorten/reorder the array or renumber CosmeticPart: Tail must remain Tail.
3. Existing keyboard navigation already filters inactive buttons. Reuse this behavior; verify both Tab traversal and arrow-key navigation skip the hidden tab.
4. Ensure the supported prefab/setup path reproduces the hidden state. Check scene overrides so changing only the prefab does not leave an active duplicate.
   - Live inspection confirmed the Lobby instance is linked to `ClosetPanel.prefab`, with five valid references and Automatic navigation. Patch that prefab narrowly. Do not rerun the full `InstallIntoLobby` replacement or recreate the whole panel, which could overwrite unrelated overrides.
5. Preserve existing lower-body saves and rendering, unequip semantics for other categories, preview/equip distinction and multiplayer appearance data.

Pass gate: four evenly spaced visible tabs; head/top/back/tail previews and equipment are correct after reopen/restart; hidden lower-body controls receive no user focus. An explicit part-to-button mapping migration is unnecessary for this bounded visibility change.

### T04 - Local red target marker

Owners: `GameUIManager`, `TargetingArrowPresenter`, existing target/visual bindings.

1. Replace only the ribbon rendering with a small red downward arrow, with a contrasting outline. Reuse a suitable existing visual only if its appearance matches: imported `UI_target.png` is a bullseye, not the requested downward arrow.
2. Retain the existing aim state and snap thresholds. Show the marker only while a valid target is snapped; outside that target's release area, hide it. Remove the item-to-mouse line.
3. Anchor the marker above the target's displayed head/hat and HUD area with a small screen-space gap, pointing down. Inspect/reuse existing anchors first. Avoid collecting item/VFX renderers into head bounds; compute bounds/anchors without allocating renderer arrays every frame.
4. Render in screen space for stable apparent size across north/east/west seats; do not intercept clicks. Hide for behind-camera/off-screen/invalid targets rather than pointing at an unrelated location.
5. Use existing item selection emphasis and aim instructions. On confirm, cancel, phase change, menu interruption, target death/disconnect, despawn or scene exit, clear the marker through the existing lifecycle.
6. Keep hover feedback local to the selecting client. Do not broadcast the tentative target; server-confirmed gameplay continues through existing commands/validation.
7. Keep attack-order indicators separate. Inspect the recently imported Multi Canvas and character Test objects for duplicate display, but do not remove unrelated mock UI as part of this task.
8. Scope is the existing Multi item-targeting ribbon. Solo/1v1 automatic target behavior and the separate GhostSkillPresenter selection path remain unchanged; do not introduce new selection steps there.

Review corrections required for T04:

- Keep `TargetSnapScreenPosition` as the existing input acquisition/release anchor. The new head marker uses a separate display anchor; moving the hit region above the head would make the character harder to click.
- Project using Vector3 until checking positive camera depth and viewport bounds. Both acquisition and retained snap must reject off-screen/behind-camera targets. A renderer that merely hides the arrow while `_snappedTargetSeat` stays selectable is insufficient; require the same visible, eligible target for click submission.
- Determine marker vertical position from the maximum of the projected head/hat top and the *actual active* opponent-bar rectangle top, then add scaled UI padding. Resolve `OppBars` using the existing stable seat-to-visual-slot mapping, never PlayerIndex-as-list-index. Validate each candidate with the current view binding; never compact seats after 4 -> 3.
- Implementation inspection corrected the earlier review: `OpponentBarPresenter` positions bars in **Update**, not LateUpdate. Calculate the marker in `GameUIManager.LateUpdate`, after bar positioning. Reuse cached corners/anchors, refresh on visual replacement, camera/resolution changes and binding changes. Hide pending valid references rather than aiming at screen origin.
- Current `GameUIManager.OnDisable` does not cancel aiming, and its input-blocked Update exits before updating the marker. Add explicit, idempotent hide/cancel coverage for owner/presenter disable and menu interruption. A temporary input block must at least suppress the marker and submission; on unblock, recompute from current pointer/bindings rather than show a stale target. Preserve existing inventory-state ownership when deciding whether to cancel or suspend aiming.
- Define the presenter API around a single target display point and explicit visibility. Preserve `DebugTargetHoverVisible` as an observation of the actual visible graphic. Do not retain the old item-tail renderer solely to satisfy old tests.
- Existing `InventoryInteractionProbe.Choose` calculates its target with `(local + 1) % RequiredPlayerCount`, which is insufficient for a sparse 4 -> 3 roster. The new departure regression must select from live registered eligible seats and assert authoritative target identity, not reuse that formula. Include real pointer input; debug hover injection alone does not validate snapping or click interception.
- Keep marker canvas order below modal/result overlays; do not raise it globally above every UI element to solve a local overlap. No gameplay-phase change is needed for this presentation replacement.

Proposed visual flow:

```text
Select item -> existing selected-item emphasis
Hover/snap valid opponent -> small red downward marker above that opponent
Leave snap area -> marker hidden
Hover another opponent -> marker moves to that opponent
Click -> existing selection submission; marker hidden
Cancel -> existing cancellation; marker hidden
```

Pass gate: all four local-seat perspectives point to the correct logical target; no head/hat/thermometer overlap; existing same-item/Esc/right-click cancellation and Ready restrictions still work. Aiming one client must not display its marker on another client's screen.

## Verification and task progression

- For each task: inspect current state -> change only missing behavior -> focused verification -> repair failures -> recheck before the next task.
- Assets/UI: before-and-after lobby/closet screenshots at the stated sizes; no visual-change test that merely mirrors implementation.
- HUD: existing Unity compilation/console checks and focused countdown/audio lifecycle checks across all modes.
- Targeting: Host plus three clients, each local seat, switch/cancel/confirm/Ready, UI interception, target departure and phase transition. Include a three-player continuation and a second round to catch stale markers.
- Network regression: equipment Back/Tail identity and authoritative target match the selected logical player; no appearance schema or gameplay RPC changes are planned.
- Capture final lobby, four-tab closet and west/north/east target previews. Treat screenshots as visual evidence, not proof of network correctness.
- Existing graph/font changes and unrelated gameplay, cameras, map layout, timing, item/ghost balance are outside this plan.
- No runtime or visual pass has been earned by writing this plan. Record actual checks and remaining human visual review during implementation.

## Current checklist

- [x] Identify current owners and existing reusable behavior.
- [x] Identify preservation constraints and bounded implementation tasks.
- [x] T01 background repair and focused visual check.
- [x] T02 banner removal, countdown tests and live Solo audio lifecycle.
- [x] T03 four visible wardrobe tabs and saved-equipment regression.
- [x] T04 red marker implementation; runtime input evidence is recorded in the results ledger.
- [ ] Close every extended acceptance case in the results ledger; unrun cases remain explicitly open.

Implementation finding: real 3/4-player input exposed an unrelated active design-only Canvas in Multi. Its BattleResult/Profile graphics intercepted target clicks, including without diagnostic capture. All children contained only UI graphics/text, with no gameplay presenter. Disable that root (one serialized active flag), retaining all design assets and the separate runtime result presenter. This is a necessary input-blocker correction, not a result-screen redesign.
