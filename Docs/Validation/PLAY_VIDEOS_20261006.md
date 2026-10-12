# Customized gameplay recordings - 2026-10-06

## Scope

User requested actual four-player and solo gameplay videos with varied customization. Outputs are local under `output/play-videos-20261006/`; no upload, commit or push is included.

- Four-player: real Unity Relay, host plus three separately authenticated clients, existing visual-flow driver, seed 29031, four completed combat presentations. Each process equips a distinct hat/top plus back/tail in memory, without saving the user profile.
- Solo: normal lobby launch, dedicated Solo scene, local NGO host and production behavior tree. The existing natural-match probe drives the human's legal item selection with the mixed policy. No gameplay balance values are changed for recording.
- Format: H.264 MP4, 20 fps output, no audio. Recording runs at real time; repeated frames preserve elapsed time when rendered capture cadence is slower than 20 fps.
- Source: Unity's actual rendered framebuffer including overlay UI. The four-view grid approximately aligns the ends of the four recordings; it is not a frame-accurate latency measurement.

## Capture implementation

`Assets/Scripts/Core/Solo/GameplayVideoProbe.cs` is opt-in and compiled only for Editor/development builds. It does not install in the Editor or in normal player launches. Required arguments:

```text
--az-record-video <absolute-output.mp4> --az-ffmpeg <existing-ffmpeg.exe>
```

The recorder captures after rendering, uses a bounded four-frame queue, writes raw frames to an encoder worker, and finalizes the encoder at normal application shutdown. It pads odd-sized frames for H.264 and stops recording with evidence if the window changes resolution. It never changes selection, temperature, phase, cosmetics, saved preferences or camera configuration.

Final recording build `build_c7b8b4f9ee2e`: succeeded, zero errors, four warnings. Existing fixed firewall-approved executable path was reused; no firewall or package changes.

## Validation and rejected attempts

- An initial external window capture included an occluding window; those video/image files were deleted and excluded. Switched to game-internal capture.
- Internal capture smoke: natural Solo launch/BT test passed, 22.55 seconds of 1280x720 video; decoded and inspected a real combat frame.
- First multi capture used window placement that produced 944x501. H.264 rejected the odd height. Added encoder padding and retained requested 960x540 resolution; interrupted that run, excluded its empty movies, then reran.
- First full Solo attempt combined a name-changing HUD test with the natural-match name-preservation assertion. The two test fixtures raced, and the preservation assertion correctly failed. Removed the optional name-changing probe from the Solo recording and reran. No production nickname logic changed.
- Final four-player run: `multi-final/report.json` passed; Relay connection, customization agreement, four settled checkpoints, target-capture checks all passed across four peers. All four encoder reports have empty error fields and zero queue drops. Representative video frames and the combined grid were visually inspected.
- Final Solo passed: one natural match, two rounds, 33 combats, 306 seconds of video, encoder errors empty and zero queue drops. `delivery-validation.json` confirms both scenario passes and full decode success for the four individual multi movies, the grid and Solo.
- Extra exports: a 90-second Solo highlight reel (source 2-57s, 135-155s, 291-306s) and a 22.25-second 1920x1080 lobby customization showcase. Both passed full decode. The showcase used an explicitly created temporary Editor recorder and five in-memory outfits (base plus four costume sets); it was destroyed before stopping Play. A sampled frame verifies the closed-eye expression with the maid outfit, hat, wings and tail.

## Files

- `index.html`: Korean viewing gallery with downloadable individual files.
- `four-player-grid.mp4`: four participant views in a 1920x1080 grid.
- `multi-final/host.mp4`, `client1.mp4`, `client2.mp4`, `client3.mp4`: 960x540 individual recordings.
- `solo-final/solo.mp4`: 1280x720 natural Solo match recording.
- `solo-highlights.mp4`: three selected excerpts, 90 seconds total.
- `customization.mp4`: base plus four outfits, 22.25 seconds, 1920x1080.
- `*.capture.txt`, scenario reports and logs: actual capture/test evidence.

Other open development work is preserved. No claim is made that recording these scenarios completes the wider project QA ledger, separate-machine testing, or all visual issues.

Final Editor state: LobbyScene, Edit Mode, clean scene. Test players/encoders exited. Original dirty font preserved from this turn's baseline; no character art, prefab, scene or gameplay changes for the recordings.
