# PLAN044 — Contact item presentation

Scope: [PLAN044](../Plans/PLAN_044_contact_item_presentation.md). Status: **implementation, scoped automated tests and AI visual review passed** (2026-10-04). Remaining art/HUD and human-playtest limits are listed below.

## Resulting behavior

- Hug T-shirt, Ice Cream, Iced Americano and Samgyetang share contact choreography across Duel, Solo and Multi. Authoritative item resolution, consumption, defense filters, turn order and mini-game rules remain unchanged. Displayed results use the existing recorded result at contact time.
- Hug: 1-second approach, existing approximately 1.017-second hug gesture, 1-second return. Local actor camera aims toward the recipient and FPS hands follow the authored Duel camera-relative pose. Remote actor keeps the assembled idle silhouette during a shallow root-position arc; existing hug plays at contact, then idle is restored before return. No new arm, sprite, rig or clip.
- Incoming remote hug is centered for the local recipient. Observer views stop beside the target, preserving two distinct silhouettes. Temporary group order places the actor before the icebox and local FPS/defense before the actor. Internal sprite/cosmetic ordering stays intact. Fan remains at the original seat.
- Feed: giver uses the existing feed gesture (remote Samgyetang retains its authored attack trigger). At 0.5 seconds, recipient uses item-only FPS `use` or world `feed`, and receives the recorded result. Food is placed near the mouth, not the forehead. Wait until giver and recipient clips finish (currently at least 1.85 seconds); Multi retains its existing minimum action duration.
- Actual defense takes priority over receiving motion. Mask matches food; current Windbreaker does not. No balance changes were made.
- Existing presentation execution/resources own cleanup. Position, rotation and temporary sorting are restored before settlement; replacement/forced cleanup/disable and repeated disposal are covered. Tests/probes are opt-in development tools.

## Defects found and corrected during validation

1. Duel could run the generic item effect before the later contact choreography; Multi used a different route. Contact items now enter one shared route directly.
2. Recipient replayed giver hands, and receiving animation could be overwritten by damage/defense. Separate giver/receiver cues and defense priority prevent conflicting poses.
3. Multi FPS anchor was far from the camera, making forearms float in view. Contact sequences temporarily borrow the authored Duel framing and restore the scene pose.
4. Actor-camera movement did not preserve the FPS relationship/rotation. Camera and hands now move and restore together.
5. A hug trigger issued while the controller remained in Jump did not actually play hug. Later visual refinement removed Jump, revealing a second controller edge: an unused `end` trigger in Idle immediately exited the new hug. The entry helper clears that trigger; a test uses the real `playerA.controller`, not only trigger counters.
6. Frame-delta accumulation could report impact before the intended half-second pose. Elapsed-time measurement now waits for the actual contact interval.
7. Remote sprites could overlap each other, or enter a cropped recipient view; the local icebox could cover incoming hands. Role-specific placement and scoped sorting address those observed cases.
8. Motion-frame review exposed torso/leg separation in the old jump and a frozen hug pose sliding home. Contact now moves the complete idle silhouette on a shallow arc and returns to idle before moving home.

## Evidence

Root: `output/validation/plan044/`. Earlier `relay/` runs checked cues but did not yet check actual Animator clips, so they are not sufficient evidence that remote hug played.

- Full Editor tests: **1035/1035 passed**, zero failed/skipped/inconclusive (`editor-tests-verified.json`). Includes 18 new cases for pose/resource lifetime, sorting restoration, real-controller gesture entry and authored clip/timing budget.
- Actual Relay allocation, join logs and distinct anonymous profiles are verified by the runner. Solo uses its normal local NGO entry and trusted bot-command boundary. Captures come from the running camera and Canvas diagnostic render, not the native desktop framebuffer.
- Tests check ordered begin/gesture/impact/end once per peer, actual Animator clip sampling through at least 90% of duration, FPS role, pose/sorting restoration, authoritative temperature/defense agreement where applicable, presentation settlement, next turn and normal lobby return.
- `visual-review/` contains time-labeled contact sheets and GIFs from captured frames. These are visual inspection aids, not new art or hand-authored replacement animations.
- Final development build: `build_f86e4d1928d9`, succeeded with **0 errors / 3 warnings** (`build-verified.json`). Missing Pipeline RuntimePipelineConfig and existing unused fields remain warnings; probes use their existing opt-in startup path.

### Accepted contact cases — 11/11, 32 peer-process observations

| Evidence | Cases | Observations | Result |
|---|---|---:|---|
| `hug-verified/summary.json` | Relay 4P actor seats 0, 2, 3; Relay Duel remote actor; Solo bot actor | 15 | 5/5 passed, final assembled-silhouette motion |
| `final/summary.json` (only `solo-2-ice-0to1`, `online-4-soup-2to3`) | Solo Ice Cream; Relay 4P Samgyetang | 5 | 2/2 passed |
| `motion-final/summary.json` (only `online-3-coffee-2to0`) | Relay 3P Iced Americano with motion frames | 3 | 1/1 passed |
| `defense-mask/summary.json` | Relay Duel Ice Cream + Mask; Relay 4P coffee + Mask | 6 | 2/2 passed |
| `defense-wind/summary.json` | Relay 3P Ice Cream + non-matching Windbreaker | 3 | 1/1 passed |

The food/defense cases preceded the final Hug-only foreground/motion refinements; their production feed path did not subsequently change. All five affected Hug cases were rerun on the final build. Failed `hug-release/` cases exposed the Idle `end` trigger bug and are explicitly superseded by `hug-verified/`; they are not passing evidence.

Final-build unchanged-path regressions also passed through real Relay: `special-3-w0` and `duel-mini-2-w0`, **2/2 runs / 5 peer-process observations**, with equal result checkpoints (`regression/matrix.json`). These cover the existing special-item/defense flow and Duel mini-game flow. Final selected evidence is summarized in `exit-evidence.json` (11 contact cases plus 2 regressions, not all intermediate reruns).

### Visual review

Reviewed giver, recipient and observer views at approximately 0.125–0.15-second capture intervals, plus full-size impact frames. An independent agent reviewed the same artifacts. This is AI visual inspection, not a human participant study or a check of every rendered frame.

| Aspect | Observed result |
|---|---|
| Body/outline continuity | Final approach and return preserve assembled torso/waist/legs; no jump-induced opening seen |
| FPS arms | Rise from and withdraw below the frame; detached floating forearms not seen in reviewed motion |
| Hug return | Existing gesture finishes, changes to idle, then returns; frozen hug-pose sliding removed |
| Occlusion | Incoming face/hands and body visible before icebox; fan stays at seat; observers distinguish both silhouettes |
| Feed role/readability | Giver cup + hands changes to empty hands; receiver sees cup only; world cup near mouth leaves eyes visible |
| Remaining presentation limits | Front-facing hug graphic and its white shirt; no side-embrace art; enlarged temperature bars may overlap other bars |

Representative final outputs: `visual-review/hug-final-actor.gif`, `hug-final-recipient.gif`, `coffee-actor.gif`, `coffee-recipient.gif`, plus `hug-final-*-sheet.jpg` and `coffee-*-sheet.jpg`. Earlier `hug-*` and `hug-fixed-*` files without `final` in their name are superseded comparison evidence.

## Workspace preservation

- Baseline SHA256 comparison covers 1,674 files: only seven intended existing code files and two continuity/flow documents changed; no baseline files are missing. Scenes, prefabs, item data, animation clips, the user's font and unrelated dirty work retain their baseline bytes.
- New C# files have Unity-generated `.meta` files. Focused `git diff --check` and Python compilation passed.
- Editor remains in original `LobbyScene`, clean/Edit Mode; script compilation is healthy and current console errors are zero. Historical console counts were not cleared. Test player processes ended. No commit or push was performed.

## Retained limits / follow-up

- **QA044-01 — Human feel:** AI frame inspection is not a human playtest. Native framebuffer, high-refresh playback and additional aspect ratios remain manual checks.
- **QA044-02 — Art:** existing world feed/hug use front-facing authored poses. A true side-view embrace or strongly differentiated giver/receiver world animation needs separately approved art/clips. No procedural arm deformation was added.
- **QA044-03 — HUD:** close character positions can still overlap temperature bars. This does not overlap the character silhouettes and is a separate HUD spacing task.
- **QA044-04 — Networks:** runs use multiple processes on one PC through real Relay. Separate physical PCs, long sessions and adverse networks are not exhaustively tested here. Resource interruption is covered in Editor; a new live mid-hug disconnect scenario was not run.
- **QA044-05 — Test scope:** fixtures select inventories and simulate mini-game success through the real result/ticket path. They do not validate puzzle difficulty, natural BT decisions or all future cosmetic silhouettes.

Initial probe compilation and a transient Burst build failure were fixed/retried before accepted tests. A CLI call during automatic domain reload lost its connection; dynamic project discovery recovered without restarting or forcing compilation. Failed intermediate visual variants and their logs remain available; they are excluded from final passing evidence.
