# PLAN 070 — Visual feedback validation

Date: 2026-10-12. Project: Absolute Zero, Unity 6000.3.11f1.

## Changes and causes

| Request | Finding and change |
|---|---|
| First-turn opponent fan | Observed rotation in the first prep phase in duel and solo. Ready legitimately switches off `IsFanActive`. No first-turn-only state defect reproduced. `FanBladeSpinner.Bind` now captures the authored blade rotation immediately after runtime assignment; this fixes initialization ordering and preserves tilt, without overriding authoritative fan state. |
| Duel room spacing | Fixed layout reserved hidden join controls and four player rows. `CasualLobbySkin.RoomLayout` now lays out visible sections top-to-bottom and sizes the panel/list for 2–4 seats. `LobbyRoomView` reapplies on membership/player-count changes. |
| Items intersect floor | Duel/solo anchors represented sprite centers at Y=0.3 without accounting for artwork extents. `InventoryFloorContact` uses sprite vertices and the map anchor plane, leaving 0.04 units clearance. Local and remote inventory views bind it; render-facing correction applies it after rotation, including enlarged poses. Existing horizontal slot coordinates remain unchanged. |
| Failure followed by item use | Server failure already consumes one use and returns without queueing. The automated playtest driver submitted its modeled success independently of the live mini-game UI; cancellation subsequently displayed a false failure. Modeled results now finish the matching UI ticket and use its existing single RPC path. Cancellation cleans up silently. Release gameplay authority and failure consumption rules are unchanged. |
| Diagnostic text | Normal duel/solo item names removed; explicit diagnostic launch can still show names. Multi local and remote quantity labels removed. Duel/solo remaining-use counts retained as requested. |
| Phase heading | Waiting, preparation, action, resolution and round-over headings use Korean. This is scoped to the requested phase heading, not a full localization pass. |

## Executed validation

- Editor connection/project verified before operations. Normal asset refresh only; no forced compilation.
- Final development build `e8a0a91345e7434d9d9cdc5a616adfc9`: succeeded, 0 errors / 4 warnings. Existing fixed player executable reused.
- `Plan070VisualPolishTests`: **8/8** passed (2/4-seat room geometry before/after joining; floor contact under camera yaw, rack roll, enlarged sprite scale and raised floor; horizontal coordinates and idempotence).
- Existing `MiniGame` filtered Editor checks: **2/2** passed (ticket freshness and server-owned human bot-command bypass guard).
- Actual player runs: solo 1 process, duel 2 peers, Multi 4 peers, plus a second duel 2 peers using real UI timeouts. Each gameplay observation window was about 90 seconds, not a full-match certification.
- Render-time floor measurements: **368 samples, 0 penetrating items**, minimum observed clearance 0.04. All nine gameplay processes included. Geometry tests cover enlarged poses; manual hover of every individual item was not performed.
- First-prep fan observation: active fans show nonzero blade rotation; ready/stopped fans settle to zero. Both perspectives were observed in duel, plus human/bot states in solo. Multi intentionally has no stationary fans.
- Live mini-game timeout check uses `--az-playtest-minigame-input none`: prioritizes eligible mini-game items but sends no modeled result and does not ready early. **Four real UI failures** across both duel peers: remaining uses 1→0, selected=false, no matching action execution in that turn. Authoritative receipt/copy correlation saved in `results.json`.
- Modeled-success path was exercised in the earlier duel/Multi runs (three modeled submissions); no misleading failure log or new runtime exception detected.
- Real Services room creation/join/start succeeded with compact lobby layout; screenshot inspected. Client-only start button remains hidden by existing permissions.
- Final Editor state: LobbyScene, EditMode, not dirty, not compiling; current console ground truth reports 0 errors. Historical error entries were not cleared.

## Evidence and reproduction

Root: `output/validation/visual-polish/` (local evidence, outside committed asset content).

- `results.json`: measured floor and authoritative failed-action checks.
- `editor-tests.json`, `mini-tests.json`: Editor test results.
- `raw/solo`, `raw/duel-natural`, `raw/multi`: logs, command manifests, telemetry, actual framebuffer screenshots.
- `timeouts/raw/duel-natural`: real mini-game timeout traces, both peers.
- `raw/join-retry`: real lobby flow logs and captures.
- `lobby-after.png`, `solo-after.png`, `multi-after.png`: selected actual runtime captures.
- `run.py 1|2|4|join`: normal observation; `run-timeout.py 2`: real failure observation. Use a fresh evidence directory for reruns to avoid old coordination files. These wrappers reuse the existing media runner; timed captures may leave `.jsonl.partial` on intentional timed quit, so they are not certified completed-match balance datasets.
- Opt-in `VisualPolishProbe` is compiled only for Editor/development builds and installed only with `--az-polish-check`. It observes transforms and captures frames; it does not alter match state.

## Deferred validation ledger

| ID | Status | Exact remaining scope / closing evidence |
|---|---|---|
| P070-V1 | Not reproduced | Original report of opponent fan stationary before first turn. Current first-prep active/ready/rotation observations pass. If seen again, record whether the opponent was already ready, perspective, and first loading/prep transition. Do not force fan rotation against `IsFanActive`. |
| P070-V2 | Optional coverage | Manual hover/click of every item and non-flat/new map floors. Current geometry tests cover expanded scale; all present modes share the existing flat map plane. New stepped maps need per-anchor floor definitions. |
| P070-V3 | Optional coverage | Actual timeout on every mini-game type and packet-loss conditions. Four genuine timeouts passed in duel, shared authority/ticket guards passed; this is not a claim that every mini-game was exhaustively played. |

No commit/push. Unrelated dirty work and prior recordings preserved. Test player processes exited.
