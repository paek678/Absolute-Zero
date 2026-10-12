# Fresh media after local fan / opponent inventory adjustment

The user explicitly resumed recording from the beginning after stopping the previous retake. All ten delivered assets are newly captured from the current game; previous deliveries and the interrupted retake remain intact.

## Delivery

Root: `output/media-20261012-latest`.

- `01_스크린샷`: three PNGs (SunnyDay Korean announcement, visible Kids, HeatWaveWarning red lighting).
- `02_영상`: seven H.264 MP4s, 1600x900, 20 encoded fps, silent.
- Fan attack: 7s; freeze through match victory: 8s; Solo / duel / four-player gameplay: 30s each; eight wardrobe combinations: 9.6s at 1.5x; room-code joining through match entry: 28s at normal speed.
- ZIP: `AbsoluteZero_스크린샷_영상_분리본.zip`, 33,263,531 bytes. Contains delivery folders and explanatory text, excluding raw captures/logs.
- Gallery: http://127.0.0.1:8874/index.html.

## Evidence

- Fresh fixed-path Development build succeeded, 0 errors / 4 warnings. Warnings: existing runtime Pipeline config, MalgunGothic importer, and two unused fields. Reports: `build-job.json`, `build-status.json`. Source SHA256 hashes: `source-manifest.json`.
- Includes foreground local fan, 70% duel/Solo opponent item size, rounded targeting arrow asset and the approved possession feed reaction. The natural play driver does not explicitly pause to demonstrate hover arrows.
- All seven capture stages and all child game processes exited successfully. Relay duel fixture passed its state comparison and rematch checks (`raw/duel-fixture/result.json`).
- All eight raw encoder reports have zero queue drops and no encoder error. Encoded fps includes repeated frames to preserve real duration; it is not a claim of 20 unique frames per second.
- Inspected all three environment screenshots and frame sheets for wardrobe, joining, duel action/victory, Solo, duel gameplay and Multi. Four-player footage uses one perspective with different cosmetics.
- All seven final videos fully decoded without errors. Durations/resolution/codec recorded in `delivery-validation.json`.
- No gameplay source/scene modifications made during recording. Editor remains LobbyScene, stopped and clean. No commit or push.

## Recording conditions

Environment types and low opponent starting temperature for the victory clip are explicit recording fixtures. General gameplay uses the existing automatic player with normal game rules and timing; these excerpts are not complete-match or balance validation. Heatwave colors the pavilion/character lighting; the background picture remains unchanged. Development Build watermark remains visible. Original raw captures and review frames are stored separately.
