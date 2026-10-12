# Fresh promotional capture — 2026-10-13

## Delivery

User requested a fresh capture of the current game, exported to Desktop with separate screenshot/video folders. No gameplay changes were made for this task; previous media was preserved.

- Desktop: `C:/Users/paek6/OneDrive/Desktop/AbsoluteZero_최신촬영_20261013`
- Sources, logs, review sheets and packaging: `output/media-20261013-final`
- Current build: `2e76fa9cbb004c1a850c0a4aeeba5681`, with production Multi rules of three rounds / first three kills. The capture does not qualify full-match duration.

| Output | Duration/content |
|---|---|
| Sunny screenshot | Korean announcement visible |
| Kids screenshot | Two children and announcement visible |
| Heatwave screenshot | Actual red floor/character lighting; background artwork remains unchanged |
| Duel fan attack | 7 seconds |
| Duel freeze victory | 8 seconds, freeze through MATCH WIN; excludes rematch |
| Solo | 30 seconds, Samgyetang, Smartphone, Hug T-shirt and Fan |
| Duel | 30 seconds, Warm Tea, Hug T-shirt, Hand Fan, Smartphone, Cat and Fan |
| Multi | 30 seconds, ghost emergence, Grudge, Possession, Warm Tea, Hot Americano and Fan |
| Closet | 9.3 seconds, eight outfit combinations at 1.5x |
| Joining | 32 seconds, main menu through lobby and gameplay at original speed |

All videos are silent H.264, 1600x900, 20 encoded fps. Development Build watermark remains. Solo and duel excerpts are continuous. Multi removes one waiting interval: game-time 19–40 then 50–59 (source offset 3.9529830994898 seconds). This is one surviving player's view, including remote ghost skills. Ghost death, environment selection and duel victory setup use existing capture fixtures. Automatic inputs/minigame outcomes are diagnostic inputs, not human play qualification.

## Verification

- All 13 launched game processes exited 0; capture campaign completed.
- Relay duel fixture synchronized checkpoints and rematch reset passed.
- Relay four-player cosmetic/checkpoint checks passed; six remote ghost skill checks, two local-role checks, and three growth checks passed.
- Eight raw recorders: zero queue drops and zero encoder errors.
- 828 animation observations: zero checked active sprite/texture mismatches. Empty local Cat/Hug world effects are classified separately. This is not proof of every visual frame.
- No searched runtime exception markers in capture logs.
- Environment images and representative frame sheets for every video reviewed.
- Seven final MP4s fully decoded successfully; the three general gameplay durations are exactly 30 seconds.
- All ten delivered media files and the readme matched source SHA256 after Desktop copy.
- No capture game processes remained. No commit or push.

Evidence: `capture-audit.json`, `delivery-validation.json`, `desktop-copy-verification.json`, `ghost-summary.json`, `ghost-cut.json`, `variety-timeline.json`, `capture-console.txt`, raw reports and `review/`.
