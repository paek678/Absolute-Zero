# PLAN 068 — Requested game media

Capture three environment screenshots (SunnyDay text, Kids, HeatWaveWarning) and seven individual MP4 clips (duel fan attack, duel freeze/win, solo bot, duel gameplay, four-player gameplay, wardrobe combinations, joining by room code).

Use the current dirty project, shipped assets/UI and actual Unity framebuffer. Reuse GameplayVideoProbe (20 fps H.264, no audio), existing Relay match drivers, and an explicit development-only MediaCaptureProbe for environment announcements and menu automation. Do not change balance, player positioning, art or persistent cosmetic settings. Staged environment/low-temperature victory footage is presentation material, not natural-match balance evidence.

Deliver under output/media-20261012 with a gallery and individual files. Validate build/console, expected visible events, playable streams, duration and representative frames. Keep original recordings and timing logs separately. Restore Editor state and terminate only owned capture processes.

Status: completed. See [capture results](../Validation/PLAN_068_media_capture.md).

Additional narrow production change: corrected the existing SunnyDay announcement typo to the requested `햇살이 쨍쨍해집니다.`. No visual/balance rule was changed.

## Latest retake

All media recaptured after the thermometer-icon fix; see [2026-10-12 retake delivery](../Validation/media_retake_20261012.md). The original capture above remains preserved.
