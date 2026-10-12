# Item motion review recordings — 2026-10-12

## Delivery

Root: `output/item-motion-review-20261012`.

| File | Content | Duration | Resolution |
|---|---|---|---|
| videos/01_duel_item_motions.mp4 | Same item use from the actor's and other player's views, side by side | 44.35 s | 1920x624 |
| videos/02_multi_item_motions.mp4 | Four-player observer view showing actor and target | 44.20 s | 1280x782 |

Both are silent, real-time-speed, 20 fps MP4s with Korean item labels. Each contains
21 edited segments covering 18 active-use item types plus Windbreaker and Mask
defense reactions, for all 20 enabled items. Disabled Tarot is excluded.
The local index page has per-item seek buttons; timelines and a ZIP are included.
Gallery server at capture completion: `http://127.0.0.1:8876/index.html`.
Local MP4s remain usable independently of that server.

## Method and scope

Opt-in development `item-showcase` fixture grants predetermined items and resets
starting temperature/fan speed on the server each turn. It supplies successful
minigame results, then uses real item-selection commands, combat resolution and
presentation. This is an animation inspection reel, not natural gameplay or a
minigame/balance certification. No production combat rule was changed for filming.

The fixture is in MatrixScenarioProbe.Animation.cs and dispatched only by its explicit
case. GameplayVideoProbe now records its initial unscaled game time, allowing each
client's presentation cues to be aligned with its framebuffer video. Recordings
use the existing fixed allowed executable path. Two views are recorded in each mode.
Four-player source selection uses verified local seat IDs, not process-name assumptions.

Sources and evidence: `raw/2/item-showcase-2-w0` and `raw/4/item-showcase-4-w0`.
Original movies/logs/cues are retained. The edited version removes waits, labels
the item, and shows both duel perspectives simultaneously. A first edit included
part of the next action in some tails; final cuts stop before the next action.

## Validation

- Unity project connection verified; build job `d5c9cb626ecd45aaa7f6a13000662f81`
  succeeded with 0 errors / 4 existing warnings.
- Actual Relay duel and four-player fixture runs passed with matching settlement
  checkpoints, all six processes exiting normally and no runner errors.
- Four raw encoders completed without queue drops or encoder errors. Capture uses
  repeated frames where necessary to preserve wall-clock timing; 20 fps is the
  encoded frame rate, not a promise of 20 distinct sampled frames every second.
- Final MP4s both passed full FFmpeg decode. `validation.json` contains measured
  dimensions/durations. Coverage checks confirm the 18 action names plus both defenses.
- Reviewed contact sheets and direct Hand Fan frames, checked original/local seat
  identity, title alignment and remote forward-use gesture. All motion quality is
  intentionally left visible for user review, including the existing remote Mask
  defense fallback and authored shared tool poses.
- HTML returned HTTP 200. Gallery opening was queued to the task's browser panel.
- No recording players left running; previous recordings preserved. No commit/push.

## Follow-up

User visual approval and any requested item-specific art/motion improvements remain
separate from successful recording and playback verification. Sound is not captured.
