# Varied promotional gameplay — 2026-10-12

## Cause and change

The previous recording driver used `attacker` for the recorded seat. Its damage-weighted ranking repeatedly preferred the permanent Fan. That excerpt was unrepresentative of the available item interactions.

`FullMatchPlaytestDriver` now supports an explicit recording-only preference, enabled only with BOTH `--az-media timed` and `--az-playtest-variety 1`. It discounts previously selected item types and prioritizes consumables in the actual inventory. Existing eligibility checks, commands, minigame admission, authoritative outcomes and timing remain in place. It grants no items and writes no shared gameplay state. Production Solo BT and ordinary test cohorts remain unchanged. Selection attempts are logged as `media_variety_selection`; actual presentation is independently recorded through choreography cues.

## Delivery

Root: `output/media-20261012-variety`; gallery http://127.0.0.1:8878/index.html.

Three new continuous, original-speed, 30-second gameplay videos:

| Video | Source cut | Actual presentation types inside excerpt |
|---|---|---|
| Solo | 33–63s | Red Card, Hot Americano, Samgyetang, Fan (the production bot still chooses Fan) |
| Duel | 31–61s | Hand Fan, Warm Tea, Cat, Soda, Ice Cream |
| Multi | 18–48s | Iced Americano, Warm Tea, Cat, Hand Fan |

The prior three environmental screenshots and four specific-action/wardrobe/joining videos are copied unchanged into the complete package. Only general gameplay was replaced. All videos remain 1600x900 silent H.264, 20 encoded fps with repeated frames where needed. Development watermark remains.

## Verification and limits

- Unity auto-compile passed. Fixed-path development build job `2cea50ce8a344223a4209cb39e5e2403`: 0 errors, 4 existing warnings.
- Seven fresh player processes (Solo + Relay duel + Relay Multi) exited 0. All three raw recorders reported zero queue drops and no encoder errors.
- All seven packaged MP4s fully decoded; three new excerpts measure exactly 30s. Final frame sheets visually inspected for each mode.
- Actual presentation cue lists and exact cut ranges: `variety-timeline.json`. Limited existing animation checker verified 537 Fan/Hand Fan/Warm Tea/Ice Cream samples with zero clip/sprite/texture mismatch. Other item types have cue/visual evidence, not an exhaustive per-frame automated assertion.
- One ordinary modeled minigame failure occurred in Multi; this is a failed item attempt, not a runtime exception. Gameplay logs had no exception markers. Existing ComputeBuffer shutdown warning remains.
- Minigame results are supplied by the pre-existing diagnostic driver. These promotional excerpts do not establish human input quality, optimal strategy, balance, or full-match regression coverage.
- Editor verified in Edit Mode without compilation failure. No production cosmetic/settings writes, commit or push. Previous captures preserved.
