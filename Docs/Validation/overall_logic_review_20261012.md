# Overall logic review — 2026-10-12

## Scope and verdict

Read-only gameplay review of the current working tree and latest Windows development player after the environment-camera and CicadaSong changes. No production code, assets, balance values, or test expectations were changed in this review. Diagnostic scripts and evidence were added under `output/validation/overall-20261012`.

The global gate is **not fully passed**. Most exercised gameplay flows pass, but the ordinary item draw does not enforce the documented three-copy maximum, two Editor assertions are stale, and three runtime fixtures still assume stacked inventory copies. A previously known shutdown buffer warning also recurs.

Build under test: preceding successful build `31896cd119d549b182ccfccac4c07d40` (0 errors / 4 warnings), fixed `AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe`. A Core DLL SHA256 is saved in `build-core-hash.json`; full-match runners also retain build/source manifests. No new build was needed because this review changes no gameplay sources.

## Findings and required follow-up

| ID | Priority / classification | Evidence and trigger | Required follow-up |
|---|---|---|---|
| OVR-01 | P2, demonstrated selection defect | GAME_DESIGN specifies at most three copies. `PlayerInventory.RollExcludingOwned` only rejects unlimited owned items. With three finite Ice Cream copies, the actual picker still returns Ice Cream and a fourth free slot exists. `GrantRandomItems` calls this picker, then `GrantSpecificItem`, which checks capacity but not duplicate count. Starting/threshold grants can therefore exceed the cap. Top-up paths already filter by `MAX_DUPLICATE_COPIES`. | Make ordinary random eligibility enforce the same cap, verify refill/reroll rules, and regress cap boundaries without changing separate-copy presentation. |
| OVR-02 | P2, obsolete runtime gate | `MatrixScenarioProbe.TemperaturePolicies.cs` still requires exactly one slot with accumulated uses after repeated grants. The user-approved separate-copy implementation creates multiple slots. All 2/3/4-player temperature-env cases fail at threshold index 1, after which later assertions in those cases never run. Logs show normal separate grants before the assertion. | Update the fixture to verify separate IDs, per-copy uses and valid pool/cap behavior, then rerun the full temperature/environment fixture. Do not restore stacking merely to pass it. |
| OVR-03 | P3, obsolete UI assertion | `Plan050NicknameRoomTests.cs:42` expects `1 vs 1 대결`; `LobbyRoomView` correctly displays the current Korean `1 대 1 대결`. Both two-player layout variants fail at that text assertion; their preceding row bounds/name checks pass. | Update the expected approved caption and rerun the full Editor suite. |
| OVR-04 | Existing resource warning; impact not quantified | Some completed player logs emit `GarbageCollector disposing of ComputeBuffer` after Input System shutdown. Also tracked as F066-10. It does not interrupt the completed scenarios here. These logs do not include the allocation stack, so they do not establish the owning allocation or a growing gameplay leak. | Trace allocation/lifetime and validate repeated sessions; do not call the game leak-free or add an unverified blanket disposal workaround. |

OVR-01 evidence: `duplicate-cap-retry.json`, an isolated Edit Mode call to the real production picker with a one-item test pool. It demonstrates eligibility, not a recorded natural four-copy drop. The initial diagnostic attempt (`duplicate-cap.json`) failed because Edit Mode did not run Awake; the retry explicitly initializes its temporary NetworkList. No live player inventory or authored asset was modified.

## Executed checks

| Area | Current evidence |
|---|---|
| Entire Editor suite | 1,169 tests: 1,167 passed / 2 failed, both OVR-03. No skipped/inconclusive cases. `editmode-results.json`, grouped `editmode-suites.csv`. |
| Test/telemetry harness | Python unittest discovery: 46/46 passed. `python-harness-tests.txt`. |
| Local network scenario matrix | 23 scenarios: 20 passed / 3 failed, all OVR-02. Real separate player processes, local UTP. `matrix/matrix.json`. |
| Matrix coverage | 2/3/4-player delayed items and input; 4-player ghost, preparation/attack disconnect, host exit, inventory, RPC guards, round reuse, failed minigame, delayed effects; 3-player special items; duel combat, inventory, host exit and actual minigame scenario. |
| Real Relay four-player | Ghost scenario passed across host + three clients, matching settled state, visible terminal results, distinct authenticated clients, full cosmetic/back/tail observations and publication checks. `relay/ghost-4-w0/report.json`. Inspected actual diagnostic capture `seat1.settled-1.png`. |
| ESC/menu and departures | Four cases passed: Solo return; 4→3 ordinary departure continues; 3→2 ends and returns; host departure returns everyone. Multiplayer cases use real Relay. `relay-menu-summary.json`, `menu/*/report.json`. |
| Solo full game | Passed in 227.24 seconds: actual production BT, 23 observed prep windows, 5 modeled human minigames, 89 valid authoritative temperature-effect observations, result visible, lobby return and telemetry closure. `solo/report.json`. |
| Relay duel full game | Passed in 323.33 seconds: 32 observed prep windows, 7 modeled human minigames, 151 valid authoritative temperature-effect observations. Both peers exited 0, matched final shared state, displayed results and returned to LobbyScene. `duel/report.json`. |
| Latest environment rules | Full Editor run includes all three pool checks (6,000 draws total) and current mode/map tests. CicadaSong absent from both eligible pools. Earlier camera runtime validation is linked separately, not counted as a new run here. |

## Static path review

Final tally: 30 runtime cases (23 local matrix + one Relay ghost/cosmetic + four menu/departure + two full games), 27 passed / 3 obsolete-fixture failures. Full Editor and Python totals remain separate; cases are not counted as matches. All review-owned player processes ended. Editor is stopped in clean LobbyScene, no compilation failure.

- Ghost RPC checks server execution, sender-to-seat mapping, connection/life eligibility, phase, match/round/turn scope, request replay and target eligibility. Ledger keeps possession spent across rounds; Grudge schedules availability at turn + 2 and caps ghost damage per target per turn.
- Minigame result admission checks sender/owner, ticket, physical CopyId, scope/deadline, target eligibility and preparation phase. Failure consumes one use and returns before queueing an effect. The runtime failure fixture is part of the passed matrix.
- Ordinary departure policy uses connected participants: duel requires two, Multi requires at least three. Host departure still depends on server loss and returns clients to lobby.
- Inventory uses individual copy identity and fixed visual slots; broad tests and live inventory cases pass, independently of the missing duplicate eligibility cap.

## Limits / deferred gates

- OVR-01 through OVR-04 remain open; this review does not change the code to make a gate green.
- Human policies and most full-match minigame success/failure are automated models, not human usability or anti-cheat certification. The standalone duel-mini fixture separately exercises its configured minigame path.
- Four-player tests include staged terminal/ghost cases and a round-reuse case; they are not a new complete natural four-player match or 50-game balance study.
- Single-machine multi-process Relay verifies real service/transport paths but not separate-device, WAN impairment, or all disconnect timings.
- Captures are diagnostic camera/UI renders. No all-item animation sweep, audio listening review, unusual aspect ratios, performance soak or profiler leak attribution was completed.
- Preexisting dirty work is preserved. No commits, pushes, promotional recaptures or source fixes performed.
