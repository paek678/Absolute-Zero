# PLAN075 — Shared duel/Multi item choreography

> Later food-role correction: the earlier configured-clip checks accepted the
> incorrect remote `feed` mapping for Ice Cream / Iced Americano. Their giver now
> uses `attack`; the recipient keeps `feed`. See [fresh role-specific validation](feed_giver_roles_20261012.md).

## Change and reason
The user requested the actual duel performance in Multi, beyond PLAN074's prop
identity correction. Normal actions previously had separate execution loops.
`ItemPresentationSequence.PlayAnimatedItem` now executes both modes: authored FPS
or remote animation, the prop, item sound, effect delay/hit intervals, full action
tail and idle restoration. Outcome callbacks retain each mode's authoritative
results and actual participant seats. Feed/hug/cat already used shared choreography.

Multi now also uses the duel's delayed Buldak sound and the hit-screen feedback
only for the actual local victim. Spectators do not receive victim camera shake
or the victim screen overlay. No new motions or general-purpose zoom were invented.

Cancellation cleanup checks that the execution and participant bindings are still
current, so a late old coroutine cannot reset a newer animation. The manager
retains ownership of cleanup after invalidation.

## Evidence
Evidence root: `output/validation/combat-animation/`.

- Unity 6000.3.11f1, connected project verified at this repository; auto compile passed.
- First shared build: job `bb29f42388c5434395376ffeec43603c`, 0 errors / 4 existing warnings.
- Actual Relay 4-player sequence `shared-multi/animation-4-w0`: all four peers passed,
  identical six settlement checkpoints; 957 clip/sprite/texture observations, zero mismatches.
- Actual Relay duel `shared-duel/animation-2-w0`: both peers passed, identical six
  settlement checkpoints; 250 observations, zero mismatches.
- Both runs exercised fan, tea, hand fan, defense, feeding, and fan after reaction.
  Developer fixture grants items/resets temperature and supplies successful minigame
  input; production selection, combat presentation and network settlement paths execute.
- Continuous pose samples found motion in all 68 Multi and 18 duel ordinary action
  observations. First-impact medians were about 0.767 seconds in both modes for the
  three checked items (configured delay 0.75 seconds plus frame scheduling).
- Frame 02/05/08/11 captures show the actual rendered gestures, and were inspected
  for the remote fan swing, target impact, and cup at the mouth. These are framebuffer
  screenshots, not generated artwork.
- Final lifetime guard build: job `9fc049172e4842f59c06caa2f00956c7`, 0 errors / 4 existing
  warnings. Final four-peer runtime recheck is recorded below when completed.

## Remaining validation ledger

| ID | Status | Exact scope/evidence still needed |
|---|---|---|
| 075-V1 | Passed before follow-up | `shared-final/animation-4-w0` passed all four peers and matching checkpoints; subsequent Hand Fan correction is tracked below |
| 075-V2 | Deferred | Full item/cosmetic combinations, including Buldak audio listening; current gameplay fixture covers four animated item types and defense |
| 075-V3 | Deferred | Interrupt this exact shared gesture during a disconnect or scene exit; ownership guard reviewed, forced cancellation not replayed here |
| 075-V4 | Deferred | Fresh Solo match; Solo shares the duel path, but this task's live regression uses Relay duel and Multi |

This is focused choreography validation, not a complete balance, minigame, camera
framing, or all-animation quality certification. Prior warnings remain outside scope.

## Follow-up: only Fan swings

The user's later instruction supersedes the initial Hand Fan expectation. Its SO
requested remote `swing` even in duel. Changed only `OpponentAnimTrigger` to
`attack` through AssetDatabase and changed AZPlayerVisual's `fan` fallback likewise.
The local authored `FPS_fan` is preserved. Ordinary execution remains shared.

`catalog-probe.cs` exercised disposable copies of the real GameScene and GameScene_Multi
animators: 42 item/scene audit rows (21 assets including disabled Tarot; 20 enabled).
Every animated row matched its explicit local/remote clip expectations, with no
non-Fan swinging trigger. Cat's separate choreography was classified rather than
treated as an animator clip. Mask's remote `mask` intentionally falls back to
`playerA_defence`: dedicated remote mask art/animation is not present.

The first diagnostic run failed because the cloned FPS singleton destroyed the
duplicate. The fixture was corrected to disable/remove that singleton before
activation; the subsequent complete audit had zero failures. Production gameplay
was not modified to accommodate this fixture error.

Fresh build/network validation after the Hand Fan correction is recorded below.

### Catalog mapping after correction

| Item family | First-person clip/gesture | Remote character clip/gesture |
|---|---|---|
| Fan | FPS_swing | playerA_swing1 (only swinging item) |
| Hand Fan | FPS_fan | playerA_attack (forward use) |
| Warm Tea, Hot Americano, Soda | FPS_useItem | playerA_drink |
| Buldak Noodles | FPS_eat | playerA_eat1 |
| Ice Cream, Iced Americano | FPS_feed + shared feeding sequence | playerA_feed + mouth prop/recipient reaction |
| Samgyetang | FPS_feed + shared feeding sequence | playerA_attack for giver; playerA_feed for recipient |
| Hot Pack, Smartphone | FPS_useItem | playerA_heal |
| Water Gun | FPS_gun | playerA_attack |
| Screwdriver, Claw Machine | FPS_useItem | playerA_attack |
| Blue Tape | FPS_tape | playerA_attack |
| Windbreaker | FPS_defence | playerA_defence |
| Mask | FPS_mask | playerA_defence fallback |
| Red Card | FPS_card | playerA_card |
| Hug T-shirt | FPS_hug with shared approach/return | playerA_hug with shared approach/return |
| Cat | Shared cat choreography, no actor clip | Shared cat choreography, no actor clip |

Tarot Card remains disabled; its presence in the asset audit does not enable it.
The raw audit is `catalog-audit.json`; disabled assets and special no-clip cases are explicit.

Latest build after the Hand Fan change: job `b3b3db3cc08643fb9c383f931fface5f`,
0 errors, 489 warnings: 485 shader warnings plus the existing Pipeline runtime-config,
MalgunGothic importer, and two unused-field warnings. No shader, font, or unrelated
warning cleanup was attempted in this task.

### Final follow-up runtime result

- `no-swing-multi/animation-4-w0`: PASS, all four peers exited normally, six matching
  settlement checkpoints, 957 sprite/texture/clip observations with zero mismatches.
- `no-swing-duel/animation-2-w0`: PASS, both peers exited normally, six matching
  settlement checkpoints, 250 observations with zero mismatches.
- 68 Multi and 18 duel ordinary performances had changing recorded poses, with no
  first-impact timing violations. Hand Fan is now `playerA_attack` remotely and
  `FPS_fan` locally, while only Fan uses swinging clips.
- Compared actual Hand Fan frame05 screenshots from both runs: the character
  presents the fan forward instead of using the basic fan's swinging clip.
- The earlier check expected Hand Fan's incorrect `playerA_swing1`; this was a
  validation specification gap. The catalog audit now explicitly asserts that only
  Fan may request a swing, and live expectations were corrected accordingly.
- Editor left in clean LobbyScene/Edit Mode; all test players exited. No commit/push.

075-V1 is closed by this final run. 075-V2 remains deferred only for full live
item/cosmetic combinations and audio quality: catalog clip routing itself was audited
for all item assets. 075-V3 and 075-V4 retain their stated scope.
