# PLAN047 - Implementation and validation

Date: 2026-10-06. Scope: equipped main-lobby avatar and participant/initiative banners. Evidence root: `output/validation/plan047/` (local ignored artifacts).

## Delivery

| Task | Implemented behavior | Evidence |
|---|---|---|
| T01 | MainPanel owns a visual-only equipped avatar; base art bounds keep feet/scale stable; transactional replacement, original fallback, unsubscribe/release on hide | Focused lifecycle/equipment test; live Editor base/equipped captures and hide/reopen; natural Solo return |
| T02 | Server-owned participant name; owner/current-session/human checks; shared nickname validation; bounded retry and immutable first valid name; bot configuration name; seat-bound bridge | 9 validation tests; actual Relay roster readback, reordered client arrivals; Solo human/bot names |
| T03 | Retained execution-owned first/current action snapshot, updated at actual playback callbacks, cleared on settlement/abandonment | 2 ownership/late-read tests; actual two-action Solo and Multi traces; first-action kill, ghost, rematch/round reset |
| T04 | Stable seat/name/status/action rows, separate score row, gold first badge and current-action frame, no input interception | 3 layout/binding tests; long Korean names; actual two/four-player captures; sparse seats after departure |
| T05 | Focused tests after each task, complete Editor suite, builds, Solo and actual Relay regression | Results below; extended acceptance remains explicit in the ledger |

Implementation refinements: the first-action badge is established at the first actual main-action callback, after the intro, rather than predicting it with duplicated eligibility rules. This exactly follows skipped defenses, canceled/suppressed actions and early kills without changing combat ordering or durations. MainPanel's component lifecycle owns lobby visibility directly; no additional lobby state machine or UGS dependency. Banner layout dimensions remain in the existing builder as one owner; no extra art/style asset or package was added.

## Executed checks

- Full Editor suite: **1065/1065** passed after T01-T04 integration.
- Final focused `Plan047` suite: **15/15** passed after the text-height correction (including Korean font mesh/overflow assertions).
- Final development build: `build_5949d6596989`, **0 errors / 4 warnings**. Warnings: absent optional Pipeline runtime configuration; dynamic TMP font inconsistent importer result; two preexisting unused fields (`SoloMatchStarter._sceneLoaded`, `NetworkSessionCoordinator.maxPlayers`).
- Actual Unity Relay, `relay-final/summary.json`: **7/7 scenarios**, 23 peers: input 2/3/4 players, duel/rematch, ghost, 4-to-3 departure, next round. Input runs also equip head/top/back/tail and verify appearance replication.
- Final text-height build: `final-captures/summary.json`, duel/rematch and four-player next-round checks passed. Additional `final-departure/summary.json` passed actual first/current labels across two actors and the 4-to-3 roster. Combined: **10/10 successful Relay runs, 33 peers** (excluding interrupted preliminary diagnostic runs).
- Natural offline Solo at **1280x720**: `solo/report.json`, **33 checks passed**, no reported errors. Real main-menu button -> profile -> dedicated Solo scene -> production BT -> combat -> next turns -> usable lobby. UGS remained uninitialized; bot name `기본 봇`, human test name `솔로검증`.
- `relay-final/banner-audit.json`: all six complete-roster capture scenarios have identical accepted seat/name maps across peers, including reordered connection arrival. In the departure scenario one exiting process had no complete name capture; the three remaining peers agree before/after removal. This missing capture is not marked a name mismatch or silently counted as four-peer visual evidence.
- No sustained `[BANNER] Name mismatch persisted` failures in final runs. The opt-in development probe changes only its process's in-memory nickname, never saved PlayerPrefs.

## Captures

- `lobby.png`: real Editor composited screen, default layered avatar.
- `lobby-equipped.png`: real Editor composited screen, shared hat and top applied without saving user equipment.
- `label-check.png`: deliberate long-name/badge stress fixture over the lobby, **not a normal gameplay screen**. Korean badge preferred height 18.63, allocated height 26, no overflow.
- `final-captures/duel-2-w0/host.banner-4.png`: actual duel, first and current badges.
- `final-departure/disconnect-prep-4-w0/host.banner-5.png`: actual four-player combat, P1 retains first, P3 currently acts.
- Standalone `*.banner-*.png` use the established diagnostic camera/canvas render, not native desktop framebuffer captures. Logs provide seat/sequence/actor correlation.

## Findings fixed during implementation

1. Flat lobby art could not display equipment. Reused the existing visual-only closet clone and catalog; kept original art as fallback.
2. Original art includes large transparent margins. Fitting the full square would enlarge the avatar incorrectly; serialized its actual visible rectangle and aligned feet once from unequipped bounds.
3. Unity's missing-component object did not behave as a CLR null for `??`. Used explicit Unity null checking before adding SortingGroup.
4. UI asmdef did not directly reference Unity.Collections. Kept the FixedString network value in Core and exposed plain-string/event presentation access instead of expanding assembly dependencies.
5. Existing FirstReady crown could disagree with actual action order. Replaced it with execution-owned playback state; removed the old crown rendering path.
6. Small TMP label rectangles silently ellipsized Korean seat/action labels. Increased their heights, verified mesh overflow and added a regression assertion.
7. Initial validation sampled replicated state before the bridge's LateUpdate flush. It now tolerates that defined frame delay and reports only a mismatch persisting for two seconds; no production synchronization delay was added.

## Remaining verification ledger

| ID | Status | Exact remaining check / evidence needed |
|---|---|---|
| P47-V01 | Partial | 960x540 standalone and 1280x720 Editor/Solo captured. Native 1920x1080 and 4:3 captures with long names, hats, world HP bars and menus still needed. |
| P47-V02 | Partial | Authority guards inspected; valid remote names/reordered arrival exercised. Deliberate raw forged-owner RPC, delayed first name after the full retry deadline, and same-object network pooling need dedicated tests. |
| P47-V03 | Partial | Execution stale-owner/late-read tests pass. Disable/re-enable the actual HUD mid-action on a remote peer and explicitly inject an old execution completion while the new one plays. |
| P47-V04 | Partial | Equipment lifecycle and live outfit application pass; natural Solo return passes. Newly injected closet disk-save failure/retry plus cold restart with every registered part were not rerun specifically for the lobby replacement. Existing closet/full-suite checks are supporting evidence only. |
| P47-V05 | Deferred, preexisting | PLAN046 Solo button overlap at 1424x714. The 1280x720 Solo pass does not close that other aspect-ratio failure; no button-layout edits made here. |
| P47-V06 | Deferred, preexisting | Editor forced-stop/network shutdown diagnostics and long-session resource warnings from PLAN046; not claimed fixed. Separate-machine Relay/latency testing and subjective readability remain human/extended checks. |

## Preservation and handoff

- LobbyScene edit is a narrow new MainPanel binding plus its prefab/catalog/fallback/visible rectangle. Original MeditationGirl transform and sprite are preserved; it is hidden only at runtime once a valid replacement exists.
- No gameplay balance, ready ordering, damage, ghost behavior, camera choreography, item durations, map positions, RPC ACK/barriers or source art changes in PLAN047.
- Name NetworkVariable changes require all multiplayer peers to run the same new build.
- Existing PLAN046 and unrelated dirty changes preserved. Original dirty font and graph hashes are checked against the turn baseline; the dynamic font is restored from that baseline after validation rather than Git HEAD.
- No commit or push requested/performed. Final acceptance is for the implemented feature and executed checks, not a claim that every extended ledger item is complete.

Final state: LobbyScene, clean/Edit Mode; test players exited. Dirty graph SHA256 `96FECF586F3162C3980138F4E98EDC0EB09B958204CA97822BC4D880E0D65603` and restored dirty font SHA256 `6FF22FD50D1267210C171BE27408DAD03C0B250ECC5239B8650A28D411D601F1` match the turn baseline. Aggregate local evidence: `output/validation/plan047/summary.json`.

## Follow-up: preserve original lobby expression (2026-10-06)

- User requested the original closed-eye smiling lobby expression while retaining the regular in-game face.
- LobbyScene now binds existing `Assets/MainFolder/Sprite/player_heal.png` / `player_heal_3` as a lobby-only face. Presenter applies it to the private visual copy after equipment, including each equipment refresh. Shared closet/game prefab and source art are unchanged.
- Focused `Plan047LobbyTests`: **1/1 passed**, including every registered head item, atlas reapplication, unchanged source head, scale and disposal checks.
- Live Editor LobbyScene: `player_heal_3` remains after equipment changes and subsequent frames; source preview prefab still uses `player_idle_3`. Captured `output/validation/plan047/lobby-expression.png` and `lobby-expression-equipped.png` at 1280x720. Equipment changes were in-memory only.
- Initial test attempt was accidentally made during Play Mode and failed in Unity TestRunner scene setup; stopped Play Mode and reran successfully. This was a validation setup failure, not a passing run.
- Live console ground truth: compilation succeeded, zero errors, one existing `DisconnectDispatcher.Awake` / `DontDestroyOnLoad` root-object warning. Historical tool buffer counts were not treated as new failures.
- Finished in clean LobbyScene / Edit Mode; restored the existing dirty font baseline after dynamic TMP updates. No new build or multiplayer run was needed/performed for this local visual override.
