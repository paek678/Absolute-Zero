# PLAN063 — Ice break and ghost reveal timing

## User decision and diagnosis

On ice break, hide the character immediately. Reveal the ghost approximately
0.5 seconds later. The existing final-death path waited another 1.5 seconds after
particle playback before hiding the character; the ordinary Multi path revealed
the ghost immediately. The freeze clip also keys `freezeice` inactive at 1.1s,
earlier than the coroutine's two freeze ticks plus 1.5s hold.

## Change

- `AZPlayerVisual.DeathRoutine`: emit particles and hide body, held item and freeze
  overlay in the same coroutine step; keep the visual root alive so particles can
  finish. Wait `GhostRevealDelay = 0.5f`, then reveal the existing ghost.
- Preserve existing ghost eligibility: Multi and final-death presentation use the
  ghost; a non-final duel death stays hidden until its existing revive flow.
- LateUpdate keeps clip-keyed freeze visibility under death presentation control.
  Persistent character visibility also covers cosmetic refreshes during the gap.
- Revive restores visibility; despawn resets the retained visibility flag.
  Interrupted-presentation settling keeps its existing immediate cleanup policy.
- Shared death rules, skill timing/authority and the ghost artwork are unchanged.

## Evidence

Root: `output/validation/plan063/`.

- Focused death coroutine tests 2/2: ordinary/final break hides immediately,
  half-second wait, cosmetic refresh stays hidden, final reveal and revive.
- Cosmetic atlas regression 8/8. Total focused/regression tests 10/10.
- Windows fixed-path development build: 0 errors / 4 warnings.
- Actual Relay four-player timing stimulus through `DebugForceGhostForVisual` and
  normal replicated death presentation: all four players exit 0. Three remote
  viewers measured bodyAtBreak=False, bodyDuringGap=False and revealDelay
  0.499s / 0.499s / 0.498s. The local victim uses FPS, so body-plane inspection is
  explicitly performed by the other peers. `runtime/players-4/*.death-result.txt`.
- Reviewed live `0.death-frozen.png`, `0.death-break-hidden.png`, and
  `0.death-ghost.png`. Existing blue rectangular ghost placeholder is preserved.
- The opt-in `Plan063DeathTimingProbe` and nickname runner wait run only in Editor
  or development builds with `--az-death-timing-check`; normal launches are inert.
- Initial probe compile lacked the TurnPhase namespace; corrected. Initial unit
  fixture encountered the existing runtime material-cache cloning warning in
  EditMode; expects the exact two fixture messages and cleans up cloned materials.
  Those failed/empty runs are not counted as passes.

## Verification ledger

- D63-01: PASS. Actual Relay ghost round-end (4 peers) and duel (2 peers), 2/2
  scenarios passed (`relay/matrix.json`). Duel exercised both non-final and final
  death and passed the rematch/reset checkpoint on both peers.
- D63-02: Every cosmetic/attack permutation and another PC — not exhaustively run.

Editor remains LobbyScene / Edit Mode / clean. Test processes exited. Protected
font and Solo graph hashes retained. No commit or push.
