# Requested media retake — 2026-10-12

> Superseded later on 2026-10-12: the user explicitly requested deletion of prior videos. This report's video files and ZIP were removed; screenshots/logs remain. See [replacement delivery and deletion manifest](media_final_20261012.md).

## Scope

Fresh capture of all seven requested video subjects plus the three associated environment screenshots. Keep the earlier delivery intact. New root: `output/media-20261012-retake`.

- Latest build `712008f306b042bf9ce18ea761e95327`: succeeded, zero errors, three warnings.
- Current thermometer-attached attack/recipient/possession icons and removed numeric thermometer labels are included. Source hashes are recorded in `source-manifest.json`.
- Reuse existing opt-in media/recording probes and Relay/BT drivers. No production code, game balance, scene layout or assets changed for this retake.
- Actual Unity framebuffer, 1600x900, H.264/yuv420p, 20 fps timeline, no audio. The recorder may duplicate frames to preserve elapsed time; this is not a claim of 20 unique frames per second.
- Environment stimuli and low starting temperature in the victory fixture are staged for presentation. Normal gameplay excerpts are not completed balance-test matches.

## Capture correction

The first join client raced the host's temporary room-code write and exited with a file sharing violation. The owned waiting host was stopped; failed raw footage remains under `raw/join` and is excluded from delivery. The capture launcher now spaces host/client startup by four seconds and uses `raw/join-retry`. Production lobby logic is unchanged.

## Validation

- Environment and wardrobe processes exited 0. Environment screenshots visually inspected, including the Korean SunnyDay announcement, two children and red pavilion lighting.
- Relay duel victory fixture passed. Both normal duel participants and the solo recording exited 0.
- Four-player recording exited 0 on all four peers; four cosmetic entries were observed in each peer log. Only the host viewpoint is delivered.
- Retried joining host/client both exited 0 and reached IN_GAME. Actual frame contact sheet confirms main menu, mode selection, code entry, room, loading and gameplay.
- All nine final MP4s passed full ffmpeg decode. `delivery-validation.json` records codecs, dimensions, frame rate and durations. All selected raw recorder metadata reports zero queue drops and no encoder errors.
- Representative frames/contact sheets inspected for every source, including freeze-to-match-win, eight wardrobe combinations and four-player appearance/layout.
- Final Editor: LobbyScene, clean, EditMode, not compiling. Owned capture players exited. Local gallery server is `http://127.0.0.1:8872/index.html` (PID/port saved in delivery root).

## Delivery

- `output/media-20261012-retake/index.html`: Korean video gallery.
- `output/media-20261012-retake/AbsoluteZero_촬영자료.zip`: gallery, nine videos, thumbnails, three PNGs and decode report; approximately 29.5 MiB.
- `deliverables/`: 8-second fan attack; 10-second freeze/win; 10-second solo; 10-second duel; 30-second four-player; 9.65-second wardrobe (1.5x); 27-second join; extras 31-second host create/start and 21-second attack-to-win.
- Original recordings, failed first join, manifests and contact sheets remain separately in `raw/` and `review/`. Earlier delivery was not overwritten.

## Retained limitations

Current Development Build watermark and duel debug labels are visible. The existing HeatWave presentation colors pavilion/characters while retaining the background photograph's color. Wardrobe template cards remain visible in the catalog, although the capture selects non-template entries. This is a recording task; these unrelated presentation details were not changed.
