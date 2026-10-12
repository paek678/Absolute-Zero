# Fresh media after force-feeding verification — 2026-10-12

## Delivery

The user requested a complete fresh recording after the three force-feeding items passed verification. All delivered screenshots and videos were newly captured during this turn.

Root: `output/media-20261012-feed-verified`.

- `01_스크린샷`: SunnyDay announcement, visible Kids, HeatWaveWarning lighting; three PNGs.
- `02_영상`: seven actual Unity framebuffer MP4s, 1600x900, H.264, 20 encoded fps, silent.
- Fan attack 7s; opponent freezing through match victory 8s; Solo / duel / four-player gameplay 30s each; eight wardrobe combinations 9.6s at 1.5x; joining by room code 28s at original speed.
- ZIP: `AbsoluteZero_스크린샷_영상_분리본.zip`, 30,290,810 bytes.
- Gallery: http://127.0.0.1:8875/index.html.

## Evidence and conditions

- Reused the current fixed-path executable already used for the feeding tests. All entries in the preceding build's source manifest matched. No new build or gameplay edits in this turn. Provenance is recorded in `provenance.json`.
- All capture stages exited normally. The Relay duel fixture passed its state comparison and rematch checks. General gameplay excerpts are not full-match/balance qualification.
- Inspected all three environment PNGs and frame sheets for all requested videos. Final seven MP4s fully decoded without errors; codec, size and duration checks are in `delivery-validation.json`.
- All ten raw encoder reports, including two rejected four-player takes, reported zero queue drops and no encoder errors. Encoded fps can contain repeated frames and is not a claim of 20 unique frames each second.
- Specific environment screenshots and low starting temperature for victory use the existing explicit recording fixtures. Final four-player gameplay uses opt-in gameplay seed 6816, selecting SummerVacation; original gameplay rules/timing and four different outfits remain active. Wardrobe uses an in-memory profile store.
- Heatwave changes pavilion/character lighting, not the background artwork. Development Build watermark remains visible.
- All recording players have exited. Connected Editor verified at LobbyScene, Edit Mode, scene clean. No commit/push.

## Deferred visual issue

**Update:** MEDIA-WIND-01 was subsequently fixed and validated in [PLAN073](PLAN_073_results.md). The table below preserves the observation and status at the time of this media delivery; delivered videos were not replaced by the fix task.

| ID | Observation / stimulus | Status | Evidence and closure |
|---|---|---|---|
| MEDIA-WIND-01 | In two four-player takes, CoolBreeze produced large pale/black wind geometry across the lower screen after the environment announcement. | Open; no production fix in this recording task. Both affected takes excluded from delivery. | `raw/multi-obscured-review/0/player.mp4`, `raw/multi-obscured-review-2/0/player.mp4`, their logs and `review/multi-review.jpg`. Inspect `EnvironmentVFXManager.BuildWindParticle/CreateWindMaterial`, renderer material/blending and camera intersection; then reproduce CoolBreeze in a fresh build at all four views before closing. Association with the environment is observed; exact rendering root cause is not yet established. |

Final four-player take is preserved at `raw/multi/0/player.mp4`; its frame sheet `review/multi-clean-review.jpg` shows unobscured gameplay. Selecting a different recording seed is not a fix for MEDIA-WIND-01.
