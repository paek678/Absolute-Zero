# PLAN047 - Equipped lobby avatar and participant/initiative banners

## Status and scope

- 2026-10-06: planning review followed by user approval and T01-T05 implementation. Focused gates and integration runs completed; see [implementation/results and remaining verification](../Validation/PLAN_047_results.md). The initial review below is retained as historical planning evidence.
- Baseline is the current working tree including PLAN046. Preserve all existing dirty files, including SoloDuelDecision and the dynamic font.
- Requests: apply equipped customization to the main lobby character; make the top participant-name banner readable and identify who acts first.
- Proposed banner scope: Solo, online duel and 3/4-player combat. The main lobby already has a local nickname field; retain it. No new nickname editor or pre-match order prediction.
- Decisions below are recommended presentation behavior. Existing combat, defense reactions, ghost abilities, timing, seat identity and win rules remain authoritative.

## Inspected implementation

| Area | Evidence | Consequence |
|---|---|---|
| Lobby character | Live Editor: `LobbyScene/MeditationGirl`, one SpriteRenderer, zero children/scripts, sprite `Assets/Resources/18.png` | It is a flattened picture, not a customizable layered character. Simply attaching a hat renderer cannot reproduce the shared appearance system. |
| Reusable visual | Live closet references `Assets/Prefabs/UI/Closet/ClosetPreviewVisual.prefab` and `Assets/Data/Cosmetics/CosmeticRegistry.asset` | Reuse the same visual parts/catalog and `PrivateCosmeticPreview` / `CosmeticVisualController`. Do not instantiate the network player prefab in the lobby. |
| Equipment truth | `CosmeticProfileService.Equipment.Snapshot`, `EquipState.OnEquipChanged`; ClosetPresenter separates browsing from Set/Save | Lobby observes equipped state only. Browsing an unconfirmed item must not change it. Save failure retains the current closet flow. |
| Current banners | `GameHudBuilder.BuildProgressHud`: 130x28 panels, font 14, static `Player 1..4`; score placed in the same row | Replace this existing HUD section, not add a second overlay. Separate names, score and initiative so they do not overlap. |
| Nicknames | Local profile and UGS `PlayerName` exist; PlayerState/SeatSnapshot have no replicated display name | A visual redesign alone will still display placeholders. Add one server-owned presentation-name value and carry it through the existing bridge. |
| Identity hazard | ParticipantTable has a legacy fallback whose participant IDs are numeric ClientId strings | Do not join Lobby.Players to battle seats by list position, numeric parsing or an assumed UGS ID match. |
| Initiative hazard | MatchHudPresenter crown uses `FirstReadySeat`; CombatResolver can prefer temperature during HeatWaveWarning; Multi orders defenses separately | First Ready is not reliable evidence of first visible action. Keep ready data unchanged; stop using it as the initiative label. |
| Playback signal | CombatVFXManager emits OnAttackerChanged for actually played actions and clears it at settlement | Reuse playback ownership, add a retained sequence-scoped HUD snapshot for late subscribers. Do not add another turn driver. |

## Proposed visible behavior

### Main lobby

`Launch -> load saved equipment -> show equipped layered avatar -> browse closet -> equip/unequip -> close successfully -> show current equipped avatar`.

- Match existing MeditationGirl placement and apparent base-character size using an explicit display anchor. Preserve menu positions and background framing.
- Head, top, back, existing lower body and tail use the same catalog/application rules as the closet and game. Hidden Bottom tab does not delete an already-equipped bottom.
- No color editor, new arms, animation changes, or new character art. Use the existing idle visual.
- Only show this avatar with the main menu. Hide/release it during closet/room/game screens; reconcile latest equipped state when main returns.
- No UGS/Relay initialization is required for this local visual.

### Battle top banner

```text
Prep:      [P1 nickname · Me] [P2 nickname] [P3 nickname] [P4 nickname]
           Round / score on a separate row; order pending
Playback:  [P1 nickname] [P2 nickname · First · Acting] [P3 nickname] [P4 nickname]
Next act:  [P1 nickname · Acting] [P2 nickname · First] [P3 nickname] [P4 nickname]
Settled:   clear First/Acting; retain names and applicable status
```

- `First` (Korean label `선공`) means **the first main action actually presented in this combat sequence**, including a non-attacking item if it is the first action. It is not a promise made during Prep.
- Gold badge for first action; a separate frame/text for the current action. Color alone must not carry the meaning. A defense reaction follows its incoming attack and does not acquire an invented standalone action.
- Derive first eligibility from the exact existing playback predicates. Multi uses the received batch's ActionOrder and `MultiPresentationSchedule.HasAction`; duel uses the result's order plus the same current item/skip predicates as playback. Do not independently re-sort readiness/temperature in UI or assume ActionOrder[0] always plays.
- If a sequence has no presented main action, show no initiative badge. Suppressed-but-consumed item actions follow current playback; early terminal settlement clears indicators. Instant ghost skills in Prep do not replace this turn's initiative badge.
- Keep stable logical order P1/P2/P3/P4 on all clients. Add a local `나` mark without reordering. On 4 -> 3 departure, preserve seat numbers; hide a vacant card or show an inactive placeholder, never renumber participants. Ghost status remains visible; ghosts are not treated as disconnected.
- Show actual validated nicknames; Solo bot uses `ResolvedSoloSettings.BotDisplayName`. Duplicate names remain distinguishable through P-number and local mark. Unknown names use `플레이어 N` until accepted, never a raw authentication/client ID.
- Keep phase, timer, local HP, target marker and modal/result UI readable. All decorative graphics/text have raycastTarget=false. Do not reactivate the disabled Multi design-board Canvas from PLAN046.

## Ownership and implementation tasks

### T01 - Lobby equipped visual

Owners: new `LobbyCharacterPresenter` + serialized `LobbyCharacterBindings`; narrow wiring in AZLobbyUI/LobbyMainView and LobbyScene.

1. Bind the existing profile service after it is ready. On subscribe, immediately read Equipment.Snapshot; on return/enable, read again. Unsubscribe from the exact prior EquipState on hide/dispose/rebind.
2. Create a local visual-only copy using PrivateCosmeticPreview and the existing closet visual prefab/catalog. Parent under an explicit lobby display anchor, use the original lobby render layer/sorting setup, not the camera-isolated ClosetPreview layer. Reuse the existing scene camera; no extra RenderTexture/camera is needed for a world-sprite replacement.
3. Fit the base idle character once using saved base bounds/feet anchor; do not change body scale every time a tall hat or tail is equipped. Allocate sufficient framing margin for registered parts and verify outline/layer order.
4. Apply the equipped snapshot transactionally. Disable the original flat renderer only after a valid clone is ready. On application failure retain the last valid avatar; if no valid clone exists, keep the original fallback. Avoid a blank avatar/duplicate pair.
5. Make main-menu visibility explicit via existing view transitions. Never mutate/save the source prefab, profile or network state merely to draw the preview. No duplicate NetworkObject, PlayerState, AudioListener or runtime Animator is cloned.

Gate: cold offline start, preview-only browse, equip, unequip, save failure/retry, close/reopen, return from Solo/online and restart show the correct equipped state. Repeated transitions do not leak visuals/subscriptions or change original placement.

### T02 - Participant names through the existing authority path

Owners: PlayerState, a small pure display-name validator, SeatSnapshot and GameDataBridge. Reuse the same owner/participant guards as SubmitCosmeticRpc.

1. Add a bounded `FixedString64Bytes` display-name NetworkVariable with server write/everyone read. Keep ParticipantMetadata identity/seat/generation serialization separate and unchanged. All test peers must use the same new build.
2. Local human submits the loaded nickname on its own spawned participant object after dependencies are ready. Do not accept a caller-provided seat or another participant's ID. Server requires current spawned participant, human role and matching RPC sender/owner.
3. Use the existing human nickname rules (NFC, Korean/ASCII letters/digits, 2-8 characters) through shared pure validation. Empty/invalid input leaves the deterministic seat fallback and does not consume the first-valid submission. The first valid name is immutable for this spawned participant; identical retry is idempotent. A delayed initial submission may succeed after Prep begins; it must not block gameplay. No in-match rename feature.
4. Solo host sets its human name through the same guarded path; server assigns the bot's validated configured display name separately. Bot labels may follow the existing bot configuration format; bound UTF-8 bytes safely, strip control characters, disable TMP rich text, and do not truncate bytes mid-codepoint.
5. Bounded client retries stop at acknowledgement/despawn/session change. Failure is a harmless fallback, not an infinite retry or game-entry failure.
6. GameDataBridge populates the name only on the matching bound PlayerState/seat and subscribes to later name changes. Immediate reads cover values that arrived before subscription. Unregistration clears old names; never reuse a stale label for a new session or compact sparse seats.

Gate: owner rejection, bot impersonation, empty/malformed/oversized name, same-value retry, late metadata/name arrival, duplicate names, reordered spawns, all seats and second-session reset. No UGS dependency added to Solo.

### T03 - Correct first/current action presentation state

Owners: CombatVFXManager/its existing execution lifetime, GameDataBridge/IGameDataBridge; small immutable `CombatPresentationHudSnapshot` with execution identity, first seat, current seat and active flag.

1. Build the initial snapshot when the accepted server result begins playback, not on phase-NetworkVariable notification. Include match generation and result sequence; turn number alone is insufficient across rounds/rematches.
2. Use the same actual action eligibility as playback. Share a small selector if needed instead of duplicating ordering predicates. Preserve every existing yield, ACK, barrier budget, impact and defense cue.
3. Update current seat at the existing OnAttackerChanged point; retain first seat until settlement. Provide read-current plus change event so re-enabled/new HUD consumers recover without waiting for the next action.
4. Publish to the existing bridge; reject stale execution callbacks. Normal completion, abort, replacement, disable, scene exit and terminal victory all clear the current snapshot only if that execution still owns it.
5. Clients consume the same authoritative result order but highlight the action currently playing on their own screen. Do not send a second animation-timing RPC merely to synchronize a decorative frame to wall-clock time.

Gate: earliest Ready loses initiative under HeatWaveWarning; defense-only/no-action; suppressed action; first-action kill; three/four participants; ghost instant win; late HUD subscription; old sequence completion after new sequence. Gameplay result/order/duration/ACK must remain identical.

### T04 - Readable participant banner

Owners: GameHudBuilder/GameHudRefs, new focused ParticipantBannerPresenter/View bindings; narrow integration in GameUIManager/MatchHudPresenter.

1. Replace current ProgressHud name/score/crown sublayout with responsive two/four-card layout. Prefer a supported imported banner sprite if suitable; inspect its borders before 9-slicing. Keep layout values in one serialized style/bindings owner instead of more scattered x-position arrays.
2. Split banner responsibility from MatchHudPresenter's timer/audio/environment responsibility. Give the new presenter current seat/name/status and presentation snapshots via the bridge. Avoid a second subscription path directly into TurnManager from the view.
3. Use stable seat-keyed bindings; separate the round/score line from names. Reserve badge space instead of shifting nicknames during actions. Keep longer Korean names readable with bounded autosizing/ellipsis and rich text disabled.
4. Show waiting/ghost/disconnected states without implying a new gameplay phase. Clear placeholders only after the corresponding current participant exists. Bind/reconcile on initialization and re-enable.
5. Remove only the replaced old crown/name presenter path. Keep unrelated modal/result UI and PLAN046 red targeting marker independent. No new input capture.

Gate: 960x540, 1280x720, 1920x1080 and 4:3 captures; all modes; P1-P4 local views; longest nickname, duplicate nicknames, large hats, first/current badge on different seats, menus/results and 4 -> 3 roster holes.

### T05 - Integrated validation and handoff

- Each task: implementation -> focused test -> fix observed failure -> rerun -> next task. No automatic pass by score.
- Full current Editor suite after changes; existing tests are a baseline, not evidence for unimplemented PLAN047.
- Fresh same-version development build, actual Relay 1v1/3/4-player and natural offline Solo. Equip different head/top/back/tail sets per peer; compare lobby/closet/battle appearances and replicated names.
- Capture cold lobby, changed outfit return, Prep name row, first action, second action, defense reaction, ghost/departure and round reset. Correlate banner seat/sequence with authoritative result logs.
- Record exceptions, captures and remaining checks in one results ledger. Existing PLAN046 Solo-button overlap assertion and Editor-stop exceptions remain separate known validation blockers; do not relabel them as passed. Gameplay source/order regression can still be inspected independently.

## Feasibility review and corrections

**Verdict: suitable to implement this scoped plan. Runtime acceptance is not yet established.** No unresolved game-rule change is required; the visible behavior above is the proposed presentation contract.

| Finding | Plan correction | Review result |
|---|---|---|
| Flat lobby art cannot support shared equipment | Replace display with visual-only layered copy, retain fallback and anchor | Feasible using inspected prefab/controller |
| Closet browsing versus actual equipment | Observe EquipState, never the temporary selected-preview snapshot | Ownership consistent with ClosetPresenter |
| FirstReady can disagree with real action order | Consume accepted result + existing playback eligibility | Prevents false initiative without changing rules |
| ClientId, UGS ID, seat and list order are not interchangeable | Self-owned name submission, server admission, seat-bound bridge | Avoids unsafe roster joins |
| Event-only HUD loses current actor on late subscription | Retained execution-scoped snapshot + cleanup guard | Covers late enable and obsolete callbacks |
| Four-player name row conflicts with center score | Separate rows and reserve badge width; stable seat keys | Layout strategy ready, visual sizing still requires captures |
| Live scene preview versus closet isolation | Lobby layer/camera preserved; no shared hidden preview camera | Avoids hiding lobby or leaking private closet models |
| Existing validation debt | Keep PLAN046 Solo layout/stop findings separate and visible | No false whole-project pass |

Checks actually performed for this review: git status; source trace through profile/equipment, lobby, player metadata/roster, HUD bridge, server ordering and playback; Unity CLI connection verification (project matched, ready); read-only Editor inspection of LobbyScene and the linked visual/catalog. No code/scene/prefab mutation, Play switch, build, Relay allocation or new feature test occurred in this planning turn.

## Non-goals

No wardrobe redesign, color customization, new character animation, balance/order changes, ghost-skill changes, speculative early order reveal, host migration, mixed-version protocol compatibility, global architecture migration or new package. Lobby button-layout repair remains a separately tracked issue unless required to complete the later Solo integration gate.
