# Authored ghost avatar integration — 2026-10-12

## Source and scope

Imported the eight resource files (four assets plus their original `.meta` files)
from `origin/ghost_Animation` commit `b8738cd`: `ghost.png`, `GhostA.controller`,
`Ghost_Idle.anim`, `Ghost_Attack.anim`. The PNG's LFS content was downloaded and its
three sliced sprites and animation sprite references resolve in Unity.

The branch's temporary scene object and unrelated scene sprite replacement were
not imported. Current scene layout, bot graph and font work remain intact. This
is a selective asset integration, not a merge of the branch.

## Behavior

- Each remote participant's existing ghost lifecycle now displays the authored
  sprite at its own seat, replacing the generated square. The parent keeps the
  existing `GhostPlaceholder` name for lifecycle compatibility; `Ghost_Sprite`
  is its child so the original animation binding paths remain valid.
- Uniform scale 0.78 (user-requested 20% increase from 0.65) preserves aspect ratio. Parent local height 0.25 keeps the
  authored stretch below the thermometer while floating above the seat. Original sprite colors are retained, with the character's
  material and existing camera-facing visual root.
- Idle loops. The imported controller had no transitions; added one exit-time
  transition from the 0.683-second attack back to idle, without rewriting curves.
- Both accepted ghost skills restart the attack state for their logical caster
  seat only. Existing sequenced skill event drives this on every peer. No extra
  RPC, shared-state mutation, cooldown, damage, or possession rule was added.
- The local player's view remains first-person, as before. Their ghost is visible
  to the other three players. Round revival hides the ghost; despawn unsubscribes
  the skill callback and clears the visual reference. Death shatter/reveal timing
  remains unchanged.

## Verification ledger

| ID | Status | Evidence / exact scope |
|---|---|---|
| GA-1 | Passed | Editor imported all 3 sprites; both clips' object references non-null; idle loops, one attack exit transition |
| GA-2 | Passed | Automatic C# compilation after correcting the test fixture's int-to-byte seat conversion |
| GA-3 | Passed on final build | Four Relay scenarios / 16 processes, every caster seat, both skills; 24 remote cast checks plus 8 local first-person role checks passed |
| GA-4 | Passed after correction | Initial height 1.05 placed the head behind the thermometer; lowered to 0.25. Final left/front/right attack and idle framebuffer captures inspected: head clear of bar, tail above floor |

Opt-in fixture extensions allow a chosen ghost seat and inspect the authored
sprite/controller, attack playback, sprite changes and idle return on each remote
view. No release gameplay input automation was added. Evidence will be stored in
`output/validation/ghost-avatar`.

This is targeted presentation validation; it does not certify every customization,
interrupted cast, or full-match balance. Existing unrelated work is preserved.

Final build: `e35297bd2e1f40dfa7cf1d1666d4dc43`, zero errors / four existing warnings.
Evidence: `output/validation/ghost-avatar/final/summary.json`, per-seat reports and
logs. The initial run is retained separately. Final scenarios verify all peers
observe matching skill events through Relay, all three sprite frames appear,
attack returns to looping idle, and all 16 processes exit normally.
The imported PNG matches its source LFS SHA-256; Ghost_Attack's Git blob also
matches the branch exactly. Only idle loop setting and controller exit transition
were changed in the imported animation assets. No new pose curves were authored.

Editor remains LobbyScene/Edit Mode, scene clean. No commit or push. The exact
0.5-second death delay and full round-revival lifecycle were not separately rerun
by their dedicated fixtures in this task; their production paths were preserved.

## Follow-up: 20% size increase and four-player recording

- User requested a 20% increase before filming: root scale 0.65 -> 0.78;
  parent height and authored animation curves unchanged.
- Fresh build `e3ea37f12fcb423e9814715f9e9ce082`: zero errors / four existing warnings.
- One fresh Relay scenario / four processes passed; both skills had six remote
  animation checks and two local-role checks. Left, front and right captures were
  inspected: no thermometer occlusion or floor penetration in these frames.
- Added opt-in `--az-ghost-media 1` fixture: wait until loading clears before
  forcing the showcase death, then resume ordinary inventory commands for 32
  seconds after the two skills. Existing non-media showcase remains unchanged.
- Delivered `output/media-20261012-ghost4p/ghost-four-player-30s.mp4`:
  30.000 seconds, 1600x900, 20 fps, silent, actual host framebuffer. Trim begins
  0.25 seconds before the forced death; includes both skills and subsequent play.
  The forced death is a recording fixture, not a natural elimination claim.
- Raw capture reports zero queue drops / no encoder error. Final file fully
  decoded without error; representative gameplay frames inspected. Evidence:
  `output/media-20261012-ghost4p/summary.json`, `raw/report.json`, raw peer logs.
- This is targeted presentation validation, not a new full-match balance test.

## Follow-up: bottom-anchored emergence

Current user decision: the ghost should grow from below while preserving its
original shape. `AZPlayerVisual` now reveals the idle artwork at 5% of its final
scale, then uniformly grows to 0.78 over 0.6 seconds with smooth easing. The idle
sprite's lower edge stays fixed. Animator bobbing pauses during growth and resumes
afterward; imported clips and their curves remain unchanged. The existing 0.5s
post-shatter delay is unchanged.

Repeated visibility synchronization does not restart growth; initial snapshots
show an existing ghost at full size. Hide/revive/despawn restore scale, position
and animator speed. An accepted skill immediately completes any remaining growth
before playing its animation, so cosmetic emergence cannot delay skill playback.
No network state or gameplay timing was changed. The shared visual path also
applies to final-death ghosts in duel modes; this run exercised Multi only.

Verification: Unity auto-compile succeeded; build
`484ea042565d46528129a37c808fa13b` succeeded with zero errors / four existing warnings.
One fresh Relay scenario with four processes passed. All three remote views
observed monotonic uniform growth, fixed lower edge, unchanged artwork proportions
and full-size idle return (7/17/17 intermediate samples). Both skills passed six
remote animation and two local-role checks. Rendered start/mid/end frames were
visually inspected. Raw recording had no encoder error or queue drops; final
30-second 1600x900/20fps video fully decoded without errors.

Evidence: `output/media-20261012-ghost-growth`, including `raw/report.json`,
peer logs with `[GHOST_GROW] PASS`, sequential captures, `ghost-emergence.mp4`
and `ghost-four-player-30s.mp4`. Gallery `index.html` shows the actual render.
Interrupted growth through revival/disconnect and initial-snapshot suppression
were reviewed statically, not independently stimulated in this recording.
