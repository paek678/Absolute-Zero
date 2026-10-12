# Food giver / recipient animation roles — 2026-10-12

## Cause and correction

The shared Feed choreography selects `OpponentAnimTrigger` for a remote giver,
and separately plays `playerA_feed` on the recipient. Ice Cream and Iced Americano
incorrectly specified remote `feed`, so both roles used the receiving/flinching
clip. This explains the giver appearing to take a hit; it is not evidence of an
extra authoritative damage event. Samgyetang already specified remote `attack`.

Changed only the two food assets' `OpponentAnimTrigger` from `feed` to `attack`
through the verified Unity Editor. The local FPS feeding gesture remains `feed`;
the remote recipient retains `feed`, food at the mouth, and the existing impact
timing. No new animation, balance, inventory, RPC, or damage rule was introduced.

The opt-in `ContactPresentationProbe` now asserts an independent role contract:
remote giver = `playerA_attack`, remote unblocked recipient = `playerA_feed`.
It also rejects any giver `playerA_feed` or `playerA_damage` samples during the
action. Previously it derived expectations from the trigger under test, allowing
the incorrect asset mapping to pass. The reusable historical animation analyzer
was updated for the three foods; historical report outputs remain unchanged.

## Validation

- Unity 6000.3.11f1 at this repository, auto compilation successful.
- Build job `196455535b8f409bb0d3930f332267c7`: succeeded, zero errors, 489 warnings
  (shader variant warnings plus existing warnings; not a warning-free build).
- Reused the fixed Windows executable path `AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe`.
- Evidence root: `output/validation/feed-giver-roles`.
- Fresh matrix: **9/9 scenarios, 21/21 processes**, 498 checks passed. All processes
  exited normally. Fifteen remote-giver views sampled 551 frames of `playerA_attack`
  and zero giver `feed` / `damage` frames. Recipient views retained `playerA_feed`;
  local FPS giver/receiver clips were also checked. Total role/clip samples: 1,690.
- Fixtures use actual item selection, authoritative resolution and presentation
  acknowledgements. Solo bot actions pass through the trusted bot delay boundary;
  online cases use separate processes and actual Relay connections. Fixtures grant
  one item and stabilize temperature; they are not natural full matches or a test
  of minigame puzzle input.

## Current evidence ledger

| ID | Status | Scope / evidence |
|---|---|---|
| FGR-1 | Passed | Solo bot seat 1 feeds human seat 0, all three foods |
| FGR-2 | Passed | Relay 1v1 remote seat 1 feeds host seat 0, all three foods / both peers |
| FGR-3 | Passed | Relay four-player: ice 1→2, coffee 2→3, soup 3→0 / all peers |
| FGR-4 | Passed | Three additional visible-window scenarios / seven processes passed; framebuffer screenshots inspected, three short clips fully decoded |

The initial hidden-window run produced black framebuffer video files even though
the camera-render diagnostic PNGs and runtime reports were valid. Those MP4s in
`cases/` are explicitly rejected as visual evidence; separate visible-window
captures are required for FGR-4.

FGR-4 final evidence: `visible-summary.json` and `review/01-solo.mp4`,
`review/02-duel.mp4`, `review/03-multi.mp4` (3.1 seconds each). The adjacent PNGs are
actual framebuffer images extracted after impact, not generated illustrations.
They show the remote giver keeping its giving pose while the designated recipient
reacts. Host recordings reported zero queue drops and no encoder error.
`index.html` provides a Korean review page. All test players exited; Editor stayed
in LobbyScene/Edit Mode with the scene unmodified. No commit or push.

## Limits

This targets normal unblocked giving/receiving. It does not certify every cosmetic,
defense/possession combination, interruption, or natural full-match outcome. Existing
shutdown ComputeBuffer disposal warnings remain. Previous promotional videos use
the older mapping and are not automatically replaced by this focused repair.
