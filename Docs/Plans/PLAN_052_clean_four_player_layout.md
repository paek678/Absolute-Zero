# PLAN052 - Wide cardinal layout and manual four-player launch

## Contract
User reference 2026-10-07: local south, opponents west/north/east spread apart,
central icebox, side inventories inward. Hide debug labels (including item names)
in ordinary Multi play; retain temperature, participant banners, uses and actual controls.

## Implementation
- MultiPerspectiveLayout + four map prefabs: widen only Multi RemoteVisual anchors;
  place inventories in the viewer's map frame. Preserve cyclic seat mapping and server spawns.
- GameplayDebugDisplay: explicit --az-debug-ui 1 opt-in. Shared ItemWorldView preserves
  names for Duel/Solo; Multi shows counts only. GameHudBuilder keeps diagnostic references
  but hides phase/status objects in Multi.
- VisualFourPlayerProbe: --az-manual-play 1 performs real Relay lobby startup only.
  No automated actions, fixture RNG, scenario timeout or automatic success quit.
- Separate PowerShell launcher starts four interactive windows with distinct auth profiles
  and session coordination, writes logs, and supports explicit diagnostic display.

## Non-goals
No gameplay balance, network identity, authority, spawn, character-art, or Duel/Solo placement changes.
No new editor instances, services, authentication secrets or automatic commits.

## Completed validation (2026-10-07)
- [x] Unity compile and focused EditMode tests: 7 anchor/icebox, 5 clean HUD/labels,
  3 banner tests passed. Reran affected 12 after final placement/cleanup changes.
- [x] Final real Relay input + 8-item layout + 12-item layout: 3/3 scenarios, 12 peers.
  Every seat had matching inventory signatures and a captured view. All renderer
  bounds remained inside the 960x540 frame. Character art's padded bounds overlap
  some item bounds, so bounding-box intersection is not proof of visible overlap;
  rendered screenshots were inspected instead.
- [x] Manual Relay bootstrap: four separate visible 1280x720 players connected as seats
  0/1/2/3. All captured startup, no exception/probe failure, no automated success exit,
  logs showed ready=False/selected=False until player input. Normal temperature decay remains.
- [x] Final build succeeded, 0 errors / 489 warnings (existing package shader and build warnings).

Evidence: `output/validation/plan052/` (tests, final-relay, build-final, manual-smoke).
Manual captures/logs: `output/manual-four-player/20261007-085446-5a1aa7/`.

## Final placement
- West/east visual anchors: X=-7.4/+7.4, Y=0, Z=5.1; north remains (0,0,8.8).
- Side inventory offsets from those anchors: (+3,0.85,-3.6) / (-3,0.85,-3.6).
- Initial +/-6.4 placement still looked crowded and item icons touched the legs;
  final wider positions and lower inventory rows were rebuilt and retested.
- Only eight Multi presentation anchors in four map prefabs changed. Duel and all
  authoritative spawn anchors passed preservation tests. Existing character art retained.

## Launch
Double-click `Play-4P-Clean.cmd` at the project root. It starts Host + 3 clients through
real UGS Lobby/Relay with separate anonymous profiles, and leaves all four windows manual.
Use Alt+Tab/window snapping to arrange them. ESC retains the existing return-to-lobby flow.

Optional command arguments:
```powershell
.\Play-4P-Clean.cmd -DebugUi
.\Play-4P-Clean.cmd -Width 1280 -Height 720
```
PowerShell implementation: `.codex/scripts/launch_four_player.ps1`.
Default development player: `%LOCALAPPDATA%/Temp/AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe`.
If that temporary build is removed, rebuild it or pass `-PlayerPath` with a development player.
`-LocalTransport` explicitly selects local UTP; default is real Relay.
Close the previous four windows before starting another manual match.

## Limits / preserved work
- The Unity development player's small built-in `Development Build` watermark remains;
  gameplay debug panel, English phase/status text and Multi item names are hidden.
- No full-match manual input marathon or cross-PC/resolution matrix was rerun in this task.
- Eight-item and twelve-item northern rows remain small at 960x540; 1280x720 or larger
  is preferable for visual review. No art or gameplay timing was changed.
- Editor ended at LobbyScene/EditMode, scene clean. Prior dirty font and bot-graph
  SHA256 hashes preserved. No commit/push. Manual four-player windows left running.
