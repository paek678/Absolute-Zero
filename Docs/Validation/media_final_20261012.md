# Previous video cleanup and fresh recording — 2026-10-12

## Authorized scope

User requested deleting all previous project recordings and recording them again after PLAN070 corrections. Searched project video files including ignored output, excluding generated Library/Temp/Git/build trees. Located all previous captured videos under `output/`; no source video assets were selected.

- Deleted **56 old video files and one delivery ZIP**, approximately **388.4 MiB**. Exact absolute paths, sizes and pre-deletion SHA-256 hashes: `output/media-20261012-final/deleted-videos.json`.
- Checked every deletion target against the resolved project output root, then removed individual files. No recursive directory deletion or cross-shell file operations.
- Rechecked all 57 manifest paths: none remain.
- Preserved screenshots, test logs, recording helpers and unrelated font ZIPs. Old gallery pages now point to the replacement gallery.
- This supersedes earlier reports saying that prior video deliveries were retained. Earlier video links intentionally no longer resolve.

## Fresh build and capture

- Verified connected Editor project and Unity 6000.3.11f1.
- Build job `1f8b31cc305d43c29332e192eb3822ef`: success, **0 errors / 2 warnings**; fixed firewall-compatible executable path reused.
- Includes PLAN070 room layout, floor contact, diagnostic item text removal, Korean phase headings and unified modeled mini-game result presentation. Source hashes recorded in `source-manifest.json`.
- Reused existing opt-in recording fixtures and actual Unity framebuffer capture. No production source, balance, scene or asset changes for this recording task.
- Environment fixture, eight wardrobe combinations, real Services room-code joining, Relay duel victory fixture, normal solo, normal duel, and normal four-player recordings completed. Every owned player exited 0. Duel fixture's rematch/reset assertions passed.
- Each of the eight raw capture reports has zero queue drops and no encoder error.

## Delivery

Root: `output/media-20261012-final`.

| Clip | Seconds |
|---|---:|
| Duel fan attack | 8 |
| Duel freeze and match victory | 10 |
| Solo bot gameplay | 10 |
| Normal duel gameplay | 10 |
| Four-player gameplay, one perspective with distinct cosmetics | 30 |
| Wardrobe, eight combinations | 9.65 |
| Join by room code | 25.5 |
| Extra: continuous attack-to-victory section | 20 |
| Extra: host creates and starts room | 29 |

- Three fresh environment screenshots: SunnyDay announcement, Kids, HeatWaveWarning.
- `deliverables/`: nine H.264 MP4s, posters, three PNGs.
- `AbsoluteZero_촬영자료.zip`: **28.57 MiB**, final media and local gallery.
- Local gallery: `http://127.0.0.1:8872/index.html?version=final-20261012`; previous server was verified by PID/command before replacing it with the same-port server pointing at the new root.
- Final videos: **1600x900, encoded at 20 fps, silent**. Recorder duplicates frames when necessary to preserve elapsed time; this does not assert 20 unique frames per second.
- `delivery-validation.json`: **9/9 full ffmpeg decodes passed**, durations/codec/dimensions recorded. Representative frame sheets and detailed freeze-to-win frames were visually inspected. Real 4p full frame confirms distinct cosmetics, slot placement and absent quantity labels.
- `raw/` retains only new source recordings and logs; `review/` contains fresh inspection frames/contact sheets.

## Presentation limits

Environment types and the victory fixture's low opponent temperature are staged using existing development fixtures. General gameplay clips use existing game rules and automated participants; they are not balance-test full-match evidence. Wardrobe clip alone is 1.5x speed and does not save the demo outfit to the user's profile. Development Build watermark remains. Current heatwave colors pavilion/characters, not the background photograph. Some other existing English labels remain outside the translated phase heading scope.

Final Editor: LobbyScene, clean, EditMode, not compiling. Capture players exited; only the local gallery server remains. No commit/push.
