# Contact approach audit and two-view recording — 2026-10-12

## Scope and findings

Read-only investigation of item approach motion and new recordings of the outgoing and incoming perspectives. No gameplay, scene, asset or balance edits.

- Current ItemPresentationCatalog maps only HugTshirt to ItemChoreography.Hug. Both duel and Multi sequences dispatch to ContactItemChoreography.PlayHugSequence.
- Local hug moves the gameplay camera 40% toward the target contact point. Remote hug moves the actor visual root. Both restore their original transforms.
- Feed choreography displays foreground hands/props or a world mouth prop without moving the camera or actor toward the recipient. Ordinary item FPS presentation is also separate from approach motion.
- Cat moves its own sprite through the inventory area. TurnManager's environment announcement rotates the camera and restores its rotation; hit shake adds a temporary offset. Neither is a player-to-player approach sequence.

## Fresh runtime evidence

Unity CLI verified one ready 6000.3.11f1 Editor for this repository at port 7800. Reused the latest development build from media_final_20261012; its Core assembly postdates both unchanged contact presentation sources.

Used existing run_contact_presentation.py with a recording-only wrapper under output/media-20261012-final/contact. Real Relay two-player session, actor seat 0 to target seat 1. Fixture supplies a single original Hug T-shirt, synthetic mini-game success and fixed starting temperatures; original commands, server resolution and presentation acknowledgements execute.

- First hidden-window attempt passed runtime assertions but framebuffer videos were black; excluded from delivery.
- Visible-window retry: both peers exited 0, Relay identity/connection check passed, host 30 + recipient 22 assertions passed.
- Actor maximum camera travel: 4.81792593 world units; recipient camera travel: 0.
- Ordered begin/gesture/impact/end and camera, FPS root, remote roots and foreground sorting restoration passed on both peers.
- Both recording reports: zero dropped queue frames, no encoder error.
- Inspected contact sheets showing approach, gesture and return on both views; checked both final MP4s by full ffmpeg decode with no error.

## Delivery

Root: output/media-20261012-final/contact.

- 01_my_hug.mp4: actor perspective, 7 seconds.
- 02_opponent_hug.mp4: recipient perspective, 7 seconds.
- 1600x900, H.264, 20 encoded fps with recorder frame duplication, real-time speed, no audio.
- index.html: two video players and simultaneous playback control.
- result.json and raw-visible/online-2-hug-0to1/*.contact.json: verification evidence.
- Gallery URL: http://127.0.0.1:8872/contact/index.html

This is presentation evidence for the requested action, not a new general gameplay/balance campaign. Capture players completed normal lobby return and exited; the Editor was not placed in Play mode or modified.
