# PLAN074 — Combat prop and animation identity

Date: 2026-10-12. User reported Multi attacks showing a damage gesture or swinging a tea cup instead of the selected Fan.

## Findings and correction

1. **Confirmed wrong prop:** the actual remote `item` object has a SpriteResolver/SpriteLibrary configured for `player_drink_6`. When the animation enabled that object, SpriteResolver.OnEnable resolved the authored tea cup over the sprite selected by combat code. The baseline four-peer run recorded Fan + `playerA_swing1` + `player_drink_6`/`player_drink` texture, with screenshots. This was an actual sprite overwrite, not an item ID or network inventory mismatch.
2. **Confirmed interrupted/queued animation:** actions only exit through the controller's `end` transition. Old triggers and delayed damage completion could interfere with a subsequent action. An isolated Play Mode copy of the shipped character, stimulated with damage then Fan, and Tea then Fan, showed queued/idle samples instead of the requested clip. The exact original user's timing is unknown; this reproduces the ownership defect rather than claiming a recording of that original occurrence.

Changes:

- `AZPlayerVisual` disables only the held-item SpriteResolver when taking/clearing item ownership. Character/body/cosmetic resolvers remain active. The foreground texture is bound after assigning the requested sprite.
- New action start clears pending triggers and enters the authored idle entry before requesting the action. Animation versions prevent an older damage/short-animation timer from ending a newer action. Death/revival also invalidate old completion callbacks.
- FPS begin/return clears pending triggers so local presentation cannot retain an older request.
- Core references the already installed `Unity.2D.Animation.Runtime` assembly for the typed resolver API; no package install or upgrade.
- Added opt-in development observation (`--az-animation-check`) and a deterministic matrix `animation` case. The probe records the requested item, actor, clip, sprite and per-renderer texture. Normal gameplay does not launch it.

No authoritative item effect, consumption, target, phase, damage, animation asset, or network timing policy was changed. The test fixture seeds items/temperatures on the host only and uses production selection/ready/result paths.

## Validation

Evidence root: `output/validation/combat-animation/`.

| Check | Result | Evidence |
|---|---|---|
| Baseline actual Relay four-peer sequence | Gameplay fixture passed, but prop observation caught wrong remote sprites. This demonstrates why state-only checks were insufficient | `before/role-cat-4-w0/*.animation.jsonl`, `before/animation-analysis.json` |
| Targeted Play Mode transition regression | 75/75 clip/sprite/texture samples passed, including the old delayed-end window | `transition-before.json`, `transition-after.json`, `transition-assertions.json` |
| Fresh Windows development build | 0 errors / 4 existing warnings | `build-after2.json`, job `fc2ef506a63d4ebb88729e75078fc2d4` |
| Actual four-peer Relay sequence | Passed, equal checkpoints, all actors 0–3 observed in every view; **957 samples, 0 mismatches** | `after/animation-4-w0/report.json`, `after/animation-analysis.json` |
| Actual duel Relay regression | Passed, equal checkpoints; **251 samples, 0 mismatches** | `duel-after/animation-2-w0/report.json`, `duel-after/animation-analysis.json` |
| Editor errors after final build/test baseline | 0 new entries, same console session; compilation not failed | `console-after.json` |
| Scoped whitespace | Passed | `git diff --check` |

The matrix sequence uses six turns: Fan, Warm Tea, Hand Fan, Fan against Windbreaker, Ice Cream, then Fan again. It exercises alternating actors, recipients becoming actors, local FPS and remote third-person presentation. Mini-game success is supplied by the existing diagnostic fixture; this is not a new input-UI or balance qualification. Prop/clip assertions cover Fan, Warm Tea, Hand Fan and Ice Cream; the defense path ran but is not included in the actor prop sample count.

The first added matrix script missed the `TurnPhase` namespace import; that diagnostic-only compile error was fixed before the final build. Earlier failure reports are retained. Final build size reported 399505778 bytes at the existing allowed executable path.

## Visual evidence

- Before: `before/role-cat-4-w0/client1.animation-33-seat0-Fan.png` — remote Fan action holds the authored cup.
- After: `after/animation-4-w0/client1.animation-01-seat0-Fan.png` — Fan sprite in the swing pose.
- Another actor: `after/animation-4-w0/host.animation-03-seat1-Fan.png`.
- Tea: `after/animation-4-w0/client1.animation-09-seat0-Warm-Tea.png`.

These are actual Unity framebuffer captures. Baseline and final fixtures differ in item sequence/seat assignment; they are not pixel-aligned before/after frames.

## Scope limits and handoff

Both observed presentation defects are fixed within the tested paths. The complete item catalogue, every cosmetic set, Solo mode and every death/disconnect timing were not re-run in this task. Existing build/shutdown warnings remain as recorded by PLAN073. All test players exited; Editor left in clean LobbyScene/Edit Mode. No commit/push and no full promotional-video re-recording in this task.
