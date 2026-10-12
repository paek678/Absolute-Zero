# PLAN054 — Casual UI skin results

## Outcome

Reusable artwork was generated first, retained separately, imported, then applied
to the audited rough UI. This is an implemented review candidate; human art
approval remains open. No commit or push was requested.

## Artwork and ownership

- `Assets/Resources/UI/Casual/Panel.png`: cream rounded panel.
- `Assets/Resources/UI/Casual/Pill.png`: button, title and status surface.
- `Assets/Resources/UI/Casual/Card.png`: catalog and selection card.
- All three have transparent outer margins and Unity nine-slice borders.
- Original exports and prompts: `output/ui-style-20261011/components/`.
- Review gallery: `output/ui-style-20261011/index.html`.

`CasualUiSkin` owns the shared palette, native Gmarket Sans Bold labels and
resource loading. `CasualLobbySkin` explicitly styles the existing views.
Builders retain their existing callbacks, state and visibility ownership.

Applied: closet, settings, mode selection, room, bot selection, match departure,
result actions, ghost skills, environment notice, loading progress and mini-game
common entrance/result/timer surfaces. Existing illustrated thermometer, clock,
ready button, participant banner and authored lobby artwork remain owned by
their existing presentation paths.

## Defects found and corrected

1. First panel export had a hollow center: regenerated an opaque cream center.
2. Room controls overlapped at runtime: reassigned panel-local layout and spacing.
3. Slider's driven anchors stretched the thumb: zero-height travel area keeps it
   circular as values change.
4. Reapplying a title plaque could cover the title: keep plaque as first sibling.
5. Closet preview camera concealed the new backing color: transparent camera
   clear now exposes the intended mint surface.
6. Offscreen Edit Mode preview omitted dynamic fallback glyphs. Final component
   board was captured in Play Mode with the real builders; all Korean text is
   visible. No font asset or game logic workaround was introduced.

## Verification

| Check | Evidence | Result |
|---|---|---|
| Final focused Editor tests | `output/validation/plan054/tests-final.json` | 9/9 pass |
| Full Editor suite | `output/validation/plan054/editor-full.xml` | 1100/1104 pass |
| Settings, closet, modes, room, bot selection | `live-review-final.json`, `output/ui-style-20261011/live/01-05*.png` | Real Play Mode screenshots; existing callbacks |
| Solo loading, continue, reopen menu, leave | `live-solo.json`, `live/06-loading.png`, `live/07-exit-dialog.png` | Returned to LobbyScene |
| Actual Unity Relay, four peers, ghost scenario | `relay/summary.json` | Pass; all peer checkpoints equal; exits 0; classified errors empty |
| Actual Unity Relay, two peers, mini-game and round 2 | `relay/summary.json` | Pass; peer checkpoints equal; exits 0; classified errors empty |
| Development build | `build-report.json`, `build-errors.txt` | Succeeded; 487 warnings; one CLI request-timeout error |

Evidence paths without a prefix in this table are under
`output/validation/plan054/`. The build's one reported error was the CLI request
dispatcher timing out after 30 seconds while the build continued to successful
completion. The resulting executable was used by both Relay checks. This is not
reported as a zero-error build log.

The four full-suite failures are existing map-layout expectation drift:
`Plan039MapLayoutTests.EveryMapHasValidDuelAndMultiAnchorsWithOriginalDefaults`
for PavilionDay, PavilionNight, ForestNight and PavilionNightWide. They expect
west X=-4.8; the already changed map prefabs use -7.4. Neither those map changes
nor those tests were changed in this UI task.

## Evidence limits and review items

- `live/` contains seven actual Editor framebuffer screenshots at 1920x1080.
- `after/combat-components.png` uses the actual builders, rendered in Play Mode,
  with sampled available/used/result states. It is not an organically played
  battle screenshot. Temporary review objects were removed.
- Relay harness screenshots are direct camera/canvas captures, not desktop
  framebuffer captures; their black background/compositing is unsuitable for
  final visual approval. Use them as harness evidence only.
- Older static `before/` and `after/` lobby previews are not the final runtime
  evidence. The gallery uses final `live/` captures.
- Human approval of art, spacing, and feel is pending. Different resolutions,
  other PCs, every mini-game and long matches were not exhaustively retested.
- Existing loading-tip content includes a tarot example; this skin task does not
  add a tarot mechanic or revise the existing tip catalog.

## Preservation

Initial worktree had 110 changed/untracked entries. Unrelated work was retained.
Protected pre-existing Malgun font and SoloDuelDecision graph hashes match the
initial snapshot. Original touched source copies are retained under
`output/validation/plan054/source-before/`. Editor restored to LobbyScene,
Edit Mode; no scene save, force recompile, commit or push was performed.

## Loading face follow-up — 2026-10-11

Replaced the kid placeholder with all 14 user-provided PNGs from the Downloads
loading-face set. Byte-identical originals are in `Assets/Resources/UI/LoadingFaces`.
Unity sprite metadata trims only transparent margins (two-pixel padding); image
pixels are unchanged. Each Show chooses one expression, fitted with preserveAspect
inside the existing 140x140 bottom-right area. Repeated Show during the same load
keeps the expression; subsequent loads sample again, with repeats allowed. A
private System.Random keeps face selection separate from gameplay Unity.Random.

Verified through the live Editor CLI in Lobby Play Mode: 14 resources imported,
all 14 expressions seen and captured over 57 Show/ForceHide cycles, duplicate Show
stability, canvas cleanup, size/aspect and non-raycast behavior, and no global
Unity RNG advancement by face selection. Evidence: `output/validation/plan054/
loading-faces/verify.json`; screenshots: `output/ui-style-20261011/loading-faces/`.
The first probe ran while Play Mode retained the previous compiled method; it
was repeated after exiting Play Mode and normal AssetDatabase.Refresh, then passed.
The Editor was returned to the previously running Lobby Play Mode. Existing
standalone executable was not rebuilt for this face-only change.

### Loading face visual recheck

User-requested recheck: actual Lobby Solo button → start → loading → GameScene_Solo
→ match menu → Lobby, repeated three times. All passed; Face03, Face03, Face04
were randomly selected. Consecutive repeats are permitted. Face bounds remained
inside the framebuffer and above the Loading label. Captured actual framebuffer
images in `output/ui-style-20261011/loading-faces/live-entry/`. No Error, Exception
or Assert logs were observed by the test callback during these three cycles.
The separate repeated Show/Hide check was rerun: PASS: 14 source sprites, 14 random expressions captured in 44 shows; preserveAspect 140x140; duplicate Show stable; hide cleanup; separate RNG verified.

Evidence: `output/validation/plan054/loading-faces/integration.json` and
`verify-recheck.json`. Standalone rebuild and remote-client retest were not run
for this cosmetic-only recheck. Editor remains in Lobby Play Mode.
