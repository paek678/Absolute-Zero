# Full promotional retake after shared item choreography — 2026-10-12

## Delivery

User selected the complete promotional package. All media was newly captured using the current PLAN075 build, including the Fan-only swing correction and shared duel/Multi choreography.

Root: `output/media-20261012-current`; gallery: http://127.0.0.1:8877/index.html.

- `01_스크린샷`: SunnyDay announcement, visible Kids, HeatWaveWarning lighting (3 PNGs).
- `02_영상`: Fan attack 7s; freeze-to-victory 8s; Solo, duel and four-player gameplay 30s each; eight wardrobe combinations 9.3s; room-code joining through gameplay 24.8s (7 MP4s).
- ZIP: `AbsoluteZero_스크린샷_영상_분리본.zip`, 28,542,858 bytes.
- Actual Unity framebuffer, 1600x900, H.264, 20 encoded fps, silent. Wardrobe is 1.5x; other clips run at original speed. Encoded frames include repeats to preserve wall-clock time.

## Capture and verification

- Reused successful build job `d5c9cb626ecd45aaa7f6a13000662f81`; managed assembly hashes saved in `provenance.json`. No gameplay or asset edits and no new build in this task.
- Environment, wardrobe, joining, duel fixture, Solo, duel and Multi capture stages all completed. All 13 player processes exited with code 0.
- Duel fixture passed host/client checkpoints and rematch reset. General excerpts are promotional automated play, not complete-match or balance qualification.
- Eight raw encoder reports: zero queue drops and no encoder errors (`capture-validation.json`). Seven final videos fully decoded successfully and all three gameplay clips measure exactly 30s (`delivery-validation.json`).
- Visually inspected all environment screenshots, wardrobe/join frame sheets, each gameplay frame sheet, and a dense freeze-to-result sequence. The four-player final excerpt uses one view of four customized participants.
- No exception/error/failure markers found in this capture's logs. Existing ComputeBuffer disposal warning remains at application shutdown; this task did not repair it.
- Environment types and low opponent temperature for victory are opt-in capture fixtures. Wardrobe changes use an in-memory store. Joining runs the actual lobby/Relay flow. General play uses existing automated input and current game rules.
- Heatwave changes floor/character lighting; background artwork remains unchanged. Development Build watermark remains. Victory transitions to the game's black result screen; the short clip does not establish visibility of the complete ice-break effect under that transition.
- Source videos/logs and review images remain in separate `raw`/`review` folders. Previous deliveries preserved. All recording players exited; Editor verified on this project in Edit Mode, not compiling. No commit/push.
