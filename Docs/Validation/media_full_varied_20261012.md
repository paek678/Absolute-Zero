# Full promotional recapture with item variety — 2026-10-12

## Request and delivery

All three requested environment screenshots and all seven videos were freshly
captured using the current build, including the latest feed-role fixes and ghost
emergence code. No old media was copied into this delivery. No gameplay code was
changed for this request.

Root: `output/media-20261012-full-varied`. Delivery folders:
`01_스크린샷` (3 PNGs) and `02_영상` (7 MP4s), plus a ZIP containing both folders.
Gallery: http://127.0.0.1:8883/index.html.

| Material | Duration / contents |
|---|---|
| Sunny Day | Actual announcement text visible |
| Kids | Two children and announcement visible |
| Heat Wave Warning | Actual red floor/character lighting; background artwork remains unchanged |
| Duel Fan attack | 7 seconds |
| Duel freeze victory | 8 seconds, freeze through MATCH WIN |
| Solo gameplay | 30 seconds; Ice Cream, Warm Tea, Cat, Fan |
| Duel gameplay | 30 seconds; Warm Tea, Ice Cream, Smartphone, Cat, Water Gun |
| Multi gameplay | 30 seconds; Cat, Hand Fan, Ice Cream, Warm Tea, Smartphone; different cosmetics, one view |
| Closet | 9.3 seconds; eight combinations at 1.5x |
| Joining | 27 seconds; main menu, mode, code input, lobby, loading and gameplay |

All video is silent H.264, 1600x900, 20 encoded fps. General gameplay excerpts
are continuous at original speed. They are not edited compilations of isolated
actions. Build watermark remains. Multi excerpt focuses on item variety; it does
not include a ghost death/skill demonstration (the separate ghost video remains
available in `output/media-20261012-ghost-growth`).

## Method

Reused existing capture scripts and opt-in media variety driver. It chooses among
actual held items, with existing server rules and bot BT intact. Environment type
and duel victory starting state use existing recording fixtures. Minigame
outcomes are supplied by the existing diagnostic driver; this is promotional
capture, not a human-input or full-match balance qualification. Outfit recording
uses the existing in-memory store. Prior captures are preserved.

Existing latest validated build: `484ea042565d46528129a37c808fa13b` (zero errors /
four existing warnings); it was not rebuilt for a media-only task.

## Verification

- All 13 capture processes exited 0; Relay duel fixture passed synchronized
  checkpoints and rematch reset. General matches ended at the recording deadline.
- Eight raw recorders: zero queue drops and no encoder error.
- Seven final MP4s fully decoded with no errors. Three general gameplay files
  are exactly 30.000 seconds. Screenshot count and video count asserted.
- Environment screenshots and frame sheets for closet, joining, duel actions,
  Solo, Duel and Multi inspected.
- 1,420 animation observation rows reviewed for prop identity. 173 rows were
  empty local FPS props during Cat/Hug world/contact effects; these are not
  rendered item sprites, so were recorded separately rather than treated as
  incorrect visible props. Remaining visible-prop checks found zero sprite or
  texture mismatches. This does not certify every animation frame or pose.
- No searched runtime exception markers in capture logs. Existing ComputeBuffer
  shutdown warning remains; no unrelated fixes made.

Evidence: `capture-audit.json`, `delivery-validation.json`, `variety-timeline.json`,
raw capture metadata/logs and `review/` frame sheets. No commit or push.

## Replacement: include ghost play in the 30-second Multi video

User follow-up supersedes the original Multi excerpt above. A fresh Relay run
with different cosmetics passed growth checks in all three remote views, both
skills (six remote animation checks / two local role checks) and cosmetic
synchronization. Four new processes exited normally; recorder queue drops and
encoder errors were zero. Source: `output/media-20261012-ghost-play/raw`.

The replacement shows freeze, growth into a ghost, Grudge and Possession, followed
by Iced Americano and Cat actions. One waiting interval was removed: source
17.7735772–29.7735772s followed by 40.5195772–58.5195772s, at original speed.
Final duration is exactly 30 seconds; full decode and rendered frame sheet passed.
The ghost's death was triggered by the existing capture fixture. It is one
surviving player's view of the ghost using skills, not the caster's FPS UI.

Updated the existing `02_영상/05_4인_플레이_30초.mp4`, poster, gallery and complete
ZIP. Other deliverables remain unchanged. The previous Multi file is preserved
as `output/media-20261012-ghost-play/previous-multi-30s.mp4`. The original
`variety-timeline.json` describes the superseded Multi cut; these replacement
timings and new raw choreography are the current evidence for that video.
