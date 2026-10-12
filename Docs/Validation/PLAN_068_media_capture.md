# PLAN 068 — Media delivery, 2026-10-12

## Delivery

`output/media-20261012/index.html` is the Korean gallery. `AbsoluteZero_촬영자료.zip` contains the gallery, three PNG screenshots, nine MP4 videos and their thumbnails. Original recordings, command manifests, timing logs and contact sheets remain outside the ZIP under `raw` and `review`.

All delivered video is actual Unity framebuffer, 1600x900, H.264/yuv420p, 20 fps, no audio. Repeated frames preserve elapsed time when framebuffer readback runs below 20 fps. There were zero encoder queue drops and no encoder errors in selected recordings. This does not assert every frame is unique.

| Requested clip | Length |
|---|---:|
| 1v1 fan attack | 8 s |
| 1v1 freezing and match victory | 10 s |
| Solo bot gameplay | 10 s |
| 1v1 gameplay | 10 s |
| Four-player gameplay, one viewpoint | 30 s |
| Wardrobe, eight combinations | 9.5 s, 1.5x speed |
| Join by room code through game entry | 27 s |
| Extra: host creates and starts a room | 28 s |
| Extra: continuous attack-to-win section | 21 s |

Screenshots: SunnyDay announcement with corrected Korean text; two Kids beside the pavilion; HeatWaveWarning with red floor/character lighting. The photographic background stays green in the existing implementation; this is not a fabricated full-screen red overlay.

## Execution and scope

- Existing fixed firewall-compatible executable path was reused. Editor verified as this project, Unity 6000.3.11f1, port 7800.
- Build `4c45ac0b0611421aab1ad2af4898ee19`: zero errors, two warnings. Environment, menu joining, duel fixture, normal duel and four-player recordings use it.
- Final build `7aadd0a65679449096dfd7d112842a70`: zero errors, three warnings. Final wardrobe and solo recordings use it; only opt-in recorder improvements differ.
- Development-only `Assets/Scripts/UI/Game/MediaCaptureProbe.cs` invokes real lobby buttons, real environment RPC presentation, or a recording stop timer. It is inactive without `--az-media` and excluded from release builds. It is disabled in Editor.
- Environment announcements are explicit staging stimuli on the local authority. Victory footage reuses the existing Relay duel fixture, which starts the opponent at 6 degrees. It is presentation evidence, not naturally achieved balance evidence.
- Normal solo, duel and four-player recordings use FullMatchPlaytestDriver selection commands with existing rules and real BT/Relay. They deliberately stop after a short recording interval, not a completed match. Do not count them toward PLAN066/067 completed-game totals.
- Four-player cosmetic verification agreed on all four head/top/back/tail combinations across peers. Only host viewpoint is delivered.
- Joining footage uses actual main menu, mode selection, code typing, Join/Create/Start buttons, services, room list, loading and game entry. Nickname commit is restored immediately after navigation; wardrobe uses an in-memory store. User persistent outfit is not saved.

## Validation and corrections

- Existing Relay duel fixture passed, including peer state agreement and rematch reset; original result is `raw/duel-fixture/result.json`.
- Environment, wardrobe and both join participants exited 0. Normal duel and four-player recording participants all exited 0. Final solo exited 0 at the recording deadline.
- All nine delivered MP4 files fully decoded with ffmpeg, no decode errors. Durations/codecs/dimensions recorded in `delivery-validation.json`. Contact sheets and selected full-size frames were visually inspected.
- Corrected production SunnyDay typo from `햇살이 더 쩍쩍해집니다.` to `햇살이 쨍쨍해집니다.` and recaptured.
- Initial capture helper was placed in Core, which cannot reference UI/TMP. Moved it with its meta into UI/Game and added the Session import; compilation passed afterward.
- One build request raced automatic compilation and failed; waited for compilation to finish and rebuilt successfully.
- Initial solo recorder waited for a lobby already hidden by fast scene entry and timed out. Timer mode now runs independently of menu readiness. Initial failed take is retained in `raw/solo-initial`; final `raw/solo` succeeded.
- Wardrobe selection now avoids template entries when choosing the eight demonstrated outfits; initial take remains in `raw/closet-initial`.
- Final Editor ground truth: compilationFailed=false, consoleErrors=0, warnings=3, LobbyScene clean, Edit Mode. Old tool-buffer errors are retained, not cleared.
- Capture players and ffmpeg exited. Local-only gallery server remains on 127.0.0.1:8871 for review; PID is in `http.pid`.
- Existing BT graph and Malgun Gothic asset hashes preserved. Unrelated dirty changes retained. No commit or push.

## Visual limitations retained

Development Build watermark is present. Existing duel phase/item debug labels remain as currently implemented. The wardrobe catalog still contains visible template cards, although they are not selected in the final take. No gameplay or art cleanup was bundled into this capture request.
