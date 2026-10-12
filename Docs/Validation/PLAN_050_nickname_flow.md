# Main-menu nickname -> room -> battle banner

Date: 2026-10-06. User decision: use the nickname entered in the main menu in the room roster and the battle's upper round/initiative banner.

## Findings and changes

- PLAN047 already provides the server-owned name, owner-validated submission, seat snapshot and banner binding. Live Editor testing confirmed that a valid main-menu end-edit saved the name. The Editor's saved nickname was empty at investigation time; an unconditional networking failure was **not** reproduced.
- `LobbyPresenter` now explicitly commits the current `LobbyMainView.NicknameText` before arena/Solo navigation, removing reliance on input deselection event order. Invalid input remains editable with a Korean 2–8-character explanation and prevents navigation with an unintended old name. Blank input retains the previous optional-name behavior; unchanged names do not rewrite PlayerPrefs.
- `ParticipantDisplayName.Lobby` applies the shared human-name rules to both `NetworkSessionCoordinator` and the legacy `LobbyManager` create/join routes. The legacy path previously always generated a `Player_...` label. Short/missing participant IDs are safe.
- Actual room capture exposed a separate visibility problem: four 55-unit rows overflowed the 180-unit list into connection logs. `LobbyRoomView` now fits rows to the authored rectangle, supporting either layout-controlled or explicit child height. Log text is truncated within its rectangle. The mode heading follows the selected/actual player count.
- Existing server name authority, immutable accepted match name, seat identity, scores, initiative timing, combat and bot display-name configuration are unchanged. No new NetworkVariable or RPC was added in this follow-up.

## Executed verification

| Check | Result |
|---|---|
| Live Editor input end-edit | Valid Korean test name saved; original key/value restored immediately |
| Focused `Plan047` tests | 20/20 passed: validation, lobby-name fallback, name banners, presentation state and avatar regression |
| `Plan050NicknameRoomTests` | 6/6 passed: 2/3/4 rows with automatic and explicit child height; every label stays within the list |
| Final development build | `build_620ac4b4332e`: succeeded, 0 errors, 2 warnings |
| Actual UGS Lobby + Relay 4 players | Four unique names entered through real menu input/buttons; every peer's room roster, NGO state, bridge and battle labels agree despite reordered connection arrival |
| Actual UGS Lobby + Relay 1v1 | Both peers agree on names in room and battle |
| Local Solo | Same menu input -> real Solo selection button -> human nickname and configured `기본 봇` banner |
| Invalid-input/navigation regression | All seven final processes rejected a one-character name without erasing it, then saved valid input on navigation without invoking end-edit |
| Visual inspection | Final 4-player room has all four unobstructed names; remote player's battle capture shows the same seat/name mapping with the correct local marker |

Final runtime result: **3/3 scenarios, 7 processes** passed. Local evidence: `output/validation/plan050/verified/summary.json`, per-process `.log`, `.room.png`, `.game.png`, `.game-ok`; row tests: `output/validation/plan050/room-tests.json`. Captures use the project's diagnostic camera/canvas render, not desktop screenshots. Initial data checks passed but captures were taken behind the loading screen; the development probe was corrected to wait for PrepPhase and loading dismissal, then rerun. Final room-layout changes were rebuilt and all three scenarios rerun again.

The new `NicknameFlowProbe` runs only in Editor/development builds with explicit `--az-nickname-flow <output>`; it is not normal gameplay. The runner serializes profile save/restore checks so same-PC processes do not overwrite one another's saved nickname. Every test process restored its original saved key/value in a `finally` block while retaining the test name in memory for its session.

Build warnings are the existing absent optional Pipeline runtime configuration and dynamic TMP font importer inconsistency. No `NullReferenceException`, `Exception:` or nickname-check failure was found in the final player logs. This is not a claim that every historical warning is resolved.

## Remaining verification ledger

| ID | Status | Remaining stimulus / evidence |
|---|---|---|
| N50-01 | Partial | 3-player row geometry passed; a separate actual 3-player name run was not needed for the shared path and was not executed in this follow-up. |
| N50-02 | Deferred | Different PCs, physical Korean IME/keyboard focus behavior, cold relaunch after a user-saved name, and unusual screen aspect ratios. Automated tests used the real TMP field/button events, not OS keyboard input. |
| N50-03 | Carry-forward | PLAN047 authority injection, pooled respawn and late-name/deferred HUD lifecycle cases remain in that plan's ledger. This change did not alter their RPC implementation. |

Final Editor state: clean `LobbyScene`, Edit Mode. Test players exited. Original dirty font and bot graph hashes match the task baseline. No scene/prefab edits, commit or push were performed in this follow-up.

## 2026-10-11 — Current-build recheck after UI and layout changes

User requested room nickname verification and a fix only if synchronization is
broken. No synchronization defect reproduced; gameplay code was left unchanged.
Latest PLAN055 build: actual UGS Lobby/Relay four-peer and two-peer runs plus local
Solo passed (3 scenarios, 7 processes, every process exit 0). Each test entered a
distinct Korean nickname through the real main-menu field and navigation buttons.
Every peer agreed on the room labels, server-backed match name, bridge and battle
banner. Connection order differed from launch order without losing identity.
Host and local-user suffixes were visually checked on Host and Client room images.
All seven saved nickname values were restored by the existing probe. Invalid input
was also rejected without silently navigating with the old saved name.

Room row geometry tests passed 6/6 for 2/3/4 players. No nickname-check failure or
Exception line was found in the seven logs. Evidence: `output/validation/plan055/
nickname/recheck/summary.json`, peer `.room.png` and `.game.png`, and
`output/validation/plan055/nickname/editor-tests.json`. Images are the existing
probe's actual camera/canvas render captures, not OS desktop captures. Remaining
cross-PC, physical keyboard/IME and cold relaunch coverage above is unchanged.
