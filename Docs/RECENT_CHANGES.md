# RECENT_CHANGES — Recent Code Change Details

> Record after every code change. Most recent changes at top.
> **Includes document changes, not just code.**

## [2026-07-13] Session: Lobby UI Integration (PLAN_003)

### Summary
Created runtime-built lobby UI (AZLobbyUI) and fixed missing dependencies.

### Change List
| # | File | Type | Description |
|---|------|------|-------------|
| 1 | `Assets/Scripts/UI/Lobby/AZLobbyUI.cs` | Created | Runtime-built lobby UI with full lobby flow (create/join/start/leave) |
| 2 | `Assets/Scenes/LobbyScene.unity` | Modified | Added LobbyUI GameObject with AZLobbyUI, added UnityTransport to NetworkManager |
| 3 | `Assets/TextMesh Pro/` | Created | Imported TMP Essential Resources (fonts, shaders) |

### Code Change Details
- **AZLobbyUI.cs:** New MonoBehaviour in `AbsoluteZero.UI.LobbyUI` namespace. Builds Canvas with 2 panels (Main: create/join, Lobby: code/players/start/leave). Ported lobby flow from LobbyTestUI.cs.
- **LobbyScene.unity:** NetworkManager now has UnityTransport component + NetworkTransport wired. LobbyUI object with AZLobbyUI added.
- **Namespace note:** Used `AbsoluteZero.UI.LobbyUI` instead of `AbsoluteZero.UI.Lobby` to avoid conflict with `Unity.Services.Lobbies.Models.Lobby`.

## [2026-07-13] Session: Network Infrastructure Migration from ArenaCombat_server

### Summary
Migrated NGO + Relay + Lobby multiplayer infrastructure code from ArenaCombat_server project. 7 packages added, 12 C# files created with `AbsoluteZero` namespace.

### Change List
| # | File | Type | Description |
|---|------|------|-------------|
| 1 | `Packages/manifest.json` | Modified | Added 7 network packages: netcode.gameobjects 2.11.2, transport 2.7.2, services.relay 1.0.5, services.lobby 1.3.0, services.authentication 3.6.1, multiplayer.playmode 2.0.2, multiplayer.tools 2.2.8 |
| 2 | `Assets/Scripts/Core/Network/NetworkConstants.cs` | Created | Enums: MatchState, MatchEndReason, GameMode, TeamId + NetworkTickRate constants |
| 3 | `Assets/Scripts/Core/Network/PlayerSpawnPoint3D.cs` | Created | Spawn point marker with order + gizmo drawing |
| 4 | `Assets/Scripts/Core/Network/LobbyServiceHelper.cs` | Created | Lobby API exception wrapper (ExecuteAsync pattern) |
| 5 | `Assets/Scripts/Core/Network/RelayManager.cs` | Created | Relay allocation, host start, client join via DTLS |
| 6 | `Assets/Scripts/Core/Network/LobbyManager.cs` | Created | Full lobby lifecycle: create, join, leave, heartbeat, polling, player data, relay integration |
| 7 | `Assets/Scripts/Core/Network/SessionManager.cs` | Created | Game scene transition, disconnect handling, network callbacks |
| 8 | `Assets/Scripts/Core/Network/PlayerSpawnManager.cs` | Created | Server-side player spawn/despawn with spawn point resolution |
| 9 | `Assets/Scripts/Core/Network/SceneLoadSyncManager.cs` | Created | NetworkBehaviour with loading overlay, progress sync, timeout |
| 10 | `Assets/Scripts/UI/Utility/ScrollableLogDisplay.cs` | Created | Serializable log display with TMP, timestamps, rich text |
| 11 | `Assets/Scripts/UI/TestUI/OutgoingDataLog.cs` | Created | Debug outgoing data logger (ServerRpc, input, position) |
| 12 | `Assets/Scripts/UI/TestUI/LobbyPlayerSlotsUI.cs` | Created | Runtime-built player slot UI with slot colors |
| 13 | `Assets/Scripts/UI/TestUI/LobbyTestUI.cs` | Created | Main lobby UI controller: create/join/leave/start/disconnect flow |

### Namespace Substitution
All `[새프로젝트]` references replaced with `AbsoluteZero`:
- `namespace AbsoluteZero.Core.Network`
- `namespace AbsoluteZero.UI.Utility`
- `namespace AbsoluteZero.UI.TestUI`
- `AddComponentMenu("AbsoluteZero/...")`

---

## [2026-07-13] Session: Harness System Setup

### Summary
Full Claude Code harness migrated from AbyssNode. Infrastructure, documents, skills, and universal Unity safety rules set up.

### Change List
| # | File | Type | Description |
|---|------|------|-------------|
| 1 | `CLAUDE.md` | Created | Agent entry point — 13 core principles (6 network + 5 universal + 2 Unity), session continuity protocol, knowledge management protocols |
| 2 | `.claude/settings.json` | Created | Model, permissions, 4 hooks configured |
| 3 | `.claude/statusline-command.ps1` | Created | Status bar (model name + context % + token count) |
| 4 | `.claude/hooks/pre-compact-save.ps1` | Created | PreCompact hook — captures modified files before context compaction |
| 5 | `.claude/hooks/block-md-creation.ps1` | Created | PreToolUse hook — blocks .md creation outside allowed paths |
| 6 | `.claude/hooks/pre-commit-check.ps1` | Created | PreToolUse hook — commit safety checks (NetworkVariable client write, Debug.Log excess, WaitForSeconds caching) |
| 7 | `.claude/hooks/post-edit-check.ps1` | Created | PostToolUse hook — sync alerts (TurnManager↔UI, Network↔docs) |
| 8 | `.claude/skills/` (25 dirs) | Copied | 22 unity-* reference skills + console-check + prefab-audit + scene-snapshot from AbyssNode |
| 9 | `Docs/SAFETY_RULES.md` | Created | Verification system + 6 universal Unity rules (RULE-001~002, 010~011, 020~021) |
| 10 | `Docs/GAME_DESIGN.md` | Created | Temperature turn-based rules from design doc |
| 11 | `Docs/GAME_SYSTEMS.md` | Created | TurnManager, UI, network infrastructure specs |
| 12 | `Docs/NETWORK_ARCHITECTURE.md` | Created | NGO + Relay host-authoritative architecture |
| 13 | `Docs/KNOWN_ISSUES.md` | Created | Empty bug tracker |
| 14 | `Docs/ACTIVE_CONTEXT.md` | Created | Session state hub |
| 15 | `Docs/CHANGES.md` | Created | High-level change history |
| 16 | `Docs/Plans/` | Created | Empty directory for sequential plan files |
