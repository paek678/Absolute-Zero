# Absolute Zero — Claude Working Guidelines

> Unity 6 (6000.0.73f1) | C# | 2.5D 1v1 Multiplayer Turn-Based Deathmatch | Korean comments in code

---

## Core Principles (ALWAYS FOLLOW)

1. **Server-authoritative: all game state mutations must happen on Host/Server** — clients send action selection only via Rpc; temperature, turn state, and win/loss are computed server-side. Direct client-side state mutation is forbidden — causes desync.
2. **Temperature and turn state must use NetworkVariable** — all synchronized values (temperature, turn phase, timer, action selections) must be NetworkVariable so all clients see consistent state. Do not use local fields for shared state.
3. **Never modify NetworkVariable.Value on client** — only the server/host may write to NetworkVariable. Client writes cause silent failure or exception depending on NetworkVariable permissions.
4. **Rpc direction must match authority model** — use `[ServerRpc]` for client→server (action selection), `[ClientRpc]` for server→client (result broadcast, VFX triggers). Reversing causes runtime error.
5. **Turn phase transitions must be atomic** — PrepTurn→AttackTurn→Resolution must go through a single state machine. Direct enum assignment from multiple call sites causes race conditions in networked context.
6. **Cache `WaitForSeconds`** — repeated `new WaitForSeconds()` forbidden (GC pressure in coroutines).
7. **Check `Docs/SAFETY_RULES.md` before any code modification**
8. **All .md documentation must be written in English** — Korean is only for direct communication with the user.
9. **3-strike retry limit** — if the same action fails 3 times in a row, stop retrying and switch to an alternative approach.
10. **Follow SOLID principles** — apply pragmatically for core systems (turn manager, network, UI).
11. **ScriptableObject configs are runtime read-only** — SO field modification at runtime permanently corrupts Editor asset data. Read at init, copy to runtime class.
12. **Never call `mcp__unityMCP__recompile_scripts`** — breaks MCP WebSocket connection, requires manual restart. Rely on Unity Editor auto-recompile.
13. **Invoke `unity-*` skills when modifying Unity C# code** — 22 Unity reference skills are installed (lifecycle, state-machines, async-patterns, npc-behavior, procedural-gen, etc.). Before writing or refactoring Unity systems, invoke the matching skill to check correct patterns and avoid common mistakes.
15. **Prefer MCP automation over manual instructions** — when Unity Editor settings, scene config, or component setup needs changing, use MCP tools (`execute_code`, `manage_components`, `manage_gameobject`, etc.) to do it directly. Only give manual instructions for things that genuinely cannot be automated (e.g., Unity Cloud Dashboard web UI).
16. **Namespace final segment must not collide with imported type names** — `AbsoluteZero.UI.Lobby` conflicts with `Unity.Services.Lobbies.Models.Lobby`, causing CS0118. Suffix with category instead (e.g., `LobbyUI`, `PlayerVisuals`).
18. **Design-first development** — before implementing or modifying gameplay systems (items, combat, temperature, turn flow, mini-games), read `Docs/GAME_DESIGN.md` and verify target values/behavior match the design spec. Implementation must not diverge from design without explicit user approval.
17. **End-of-phase harness verification** — after completing each work phase:
    - `Docs/Plans/PLAN_NNN_*.md` — all completed tasks marked `[x]`
    - `Docs/RECENT_CHANGES.md` — change list recorded at top
    - `Docs/ACTIVE_CONTEXT.md` — status + last modified files updated
    - `Docs/CHANGES.md` — high-level entry added
    - `Docs/SAFETY_RULES.md` — new rules added if lessons learned

---

## Reference Table

| Task Type | Read First | Path |
|-----------|-----------|------|
| **New session start** | **SESSION_SETUP.md** | **`Docs/SESSION_SETUP.md`** |
| Before code changes | SAFETY_RULES.md | `Docs/SAFETY_RULES.md` |
| **Session resume** | **ACTIVE_CONTEXT.md** | **`Docs/ACTIVE_CONTEXT.md`** |
| Recent code changes | RECENT_CHANGES.md | `Docs/RECENT_CHANGES.md` |
| Plan history | Plans/ | `Docs/Plans/PLAN_NNN_*.md` |
| **Before gameplay changes** | **GAME_DESIGN.md** | **`Docs/GAME_DESIGN.md`** |
| Game design & rules | GAME_DESIGN.md | `Docs/GAME_DESIGN.md` |
| Game systems | GAME_SYSTEMS.md | `Docs/GAME_SYSTEMS.md` |
| Network architecture | NETWORK_ARCHITECTURE.md | `Docs/NETWORK_ARCHITECTURE.md` |
| Known bugs | KNOWN_ISSUES.md | `Docs/KNOWN_ISSUES.md` |
| Network code reference | ArenaCombat_server | `C:\Users\paek6\Unity Project\ArenaCombat_server` (source project for network migration) |

---

## Architecture Summary

- **Network model:** NGO 2.11.2 (Netcode for GameObjects), Unity Relay (DTLS), Host-authoritative
- **No DI** — singleton managers + `GetComponent<T>()` pattern (Unity standard)
- **Namespace:** `AbsoluteZero` for all code
- **Turn system:** `AbsoluteZeroTurnManager` — single state machine controlling PrepTurn / AttackTurn / Resolution / GameOver
- **Data sync:** `NetworkVariable<float>` for temperature, `NetworkVariable<TurnPhase>` for turn state, `NetworkVariable<int>` for timer
- **Action input:** `[Rpc(SendTo.Server)]` from client → host stores in local buffer → simultaneous resolution on AttackTurn
- **UI:** All UI is **runtime-built** (no Inspector wiring) — `AZGameUI` and `AZLobbyUI` construct Canvas/buttons/text in code. Uses TMP (TextMeshPro)

### Scenes
- **LobbyScene** (build index 0) — start scene, has NetworkManager + Managers (DDOL)
- **GameScene** (build index 1) — loaded via `NetworkManager.SceneManager` after Relay connect

### Key Singletons (all DDOL, on "Managers" GameObject in LobbyScene)
- `LobbyManager` — lobby CRUD, heartbeat, polling, auto-inits Unity Services in `Start()`
- `RelayManager` — Relay allocation + join
- `SessionManager` — scene transition + disconnect handling
- `PlayerSpawnManager` — spawns Player prefab on client connect

### Game Systems (in GameScene)
- `AbsoluteZeroTurnManager` (NetworkBehaviour) — server-authoritative state machine
- `AZGameUI` (MonoBehaviour) — runtime-built Canvas, no Inspector wiring
- `AZPlayerVisual` (NetworkBehaviour on Player prefab) — capsule with temp-based color

### NetworkManager Config
- UnityTransport component (DTLS via Relay)
- `DefaultNetworkPrefabs.asset` → contains Player prefab
- PlayerPrefab = null (PlayerSpawnManager handles spawning)
- EnableSceneManagement = true

---

## Project Layout

```
Assets/
├── Scenes/
│   ├── LobbyScene.unity           # Lobby + NetworkManager (build index 0)
│   └── GameScene.unity            # Turn-based game scene (build index 1)
├── Scripts/
│   ├── Core/
│   │   ├── Game/                  # AbsoluteZeroTurnManager
│   │   └── Network/              # LobbyManager, RelayManager, SessionManager, PlayerSpawnManager
│   └── UI/
│       ├── Game/                  # AZGameUI
│       └── Lobby/                 # AZLobbyUI
├── Prefabs/
│   └── Player.prefab             # NetworkObject with AZPlayerVisual
├── Settings/                     # URP render pipeline settings
└── TextMesh Pro/                 # TMP Essential Resources
```

---

## Session Continuity Protocol (ALWAYS FOLLOW)

> Mandatory workflow to minimize re-exploration on token limit / session restart.

### On Session Start (Required)
1. **Read `Docs/ACTIVE_CONTEXT.md`** — current state, in-progress work, last modified files
2. If work is in progress, read the active plan file (`Docs/Plans/PLAN_NNN_*.md`)
3. If needed, read `Docs/RECENT_CHANGES.md` for recent code changes

### On Work Start
1. **Create plan file:** `Docs/Plans/PLAN_NNN_short_description.md` (sequential numbering, never delete)
2. Write checklist (`- [ ]`) in the plan — detailed per-step items
3. **Update `Docs/ACTIVE_CONTEXT.md`** — change status to "In Progress", reference active plan
4. **Work Scope Declaration** — before writing any code:
   - Grep to confirm target symbol/file exists at expected location — never trust conversation history for file paths
   - Declare in the plan: files/symbols to modify AND "Will NOT touch" list
   - If grep reveals target moved or changed — update plan before proceeding

### During Work
1. **After each step completion** — update plan checkboxes (`- [ ]` → `- [x]`)
2. **After each code change** — add record at top of `Docs/RECENT_CHANGES.md`:
   - File path, change type (modified/created/deleted), change summary
   - For significant code changes: include before/after code snippets
3. **Mid-save:** periodically update ACTIVE_CONTEXT.md during complex work (next steps, blockers, etc.)
4. **Scope Guard** — do not expand scope without approval:
   - When discovering unexpected issues during work, STOP expanding scope
   - Record in plan under a "Discovered Issues" section
   - Report to user: ask whether to fix now or track for later
   - Continue with original task only — exception: blocking issues that prevent current task

### On Work Completion
1. Mark plan file status as `✅ Complete`
2. Update `Docs/ACTIVE_CONTEXT.md` — record completion + set status to "Idle"
3. Add high-level entry to `Docs/CHANGES.md`
4. If errors/lessons learned — add rules to `Docs/SAFETY_RULES.md`

### File Structure
```
Docs/
├── ACTIVE_CONTEXT.md      # Current work state (read first on session start)
├── RECENT_CHANGES.md      # Recent code change details (file/line/content)
├── CHANGES.md             # High-level change history (commit message style)
└── Plans/
    ├── PLAN_001_*.md      # Completed plans (never delete)
    ├── PLAN_002_*.md      # Next plan
    └── ...                # Plans accumulate → full work history
```

### RECENT_CHANGES.md Record Format
```markdown
## [Date] Session: Work Description

### Summary
One-line summary

### Change List
| # | File | Type | Description |
|---|------|------|-------------|
| 1 | `path/file.cs` | Modified | Change description |

### Code Change Details (for modifications)
**file.cs line 42:** `oldCode` → `newCode` (reason)
```

---

## Knowledge Placement Decision Tree

> Where to write new knowledge — single source of truth, no duplicates.

| Question | Destination |
|----------|-------------|
| Violating this breaks the system? | `Docs/SAFETY_RULES.md` |
| Changes between sessions? (status, progress) | `Docs/ACTIVE_CONTEXT.md` or `Docs/RECENT_CHANGES.md` |
| Only relevant to a specific task type? | `.claude/skills/{skill}/SKILL.md` |
| Otherwise (permanent project knowledge) | `CLAUDE.md` |

**Single source of truth is required** — if information exists in one location, do not duplicate it in another. Reference the source instead.

---

## Knowledge Proposal Protocol

> Protect harness integrity — propose changes, don't silently inject.

- **CLAUDE.md and SAFETY_RULES.md modifications require user approval** — propose as a diff, wait for explicit confirmation before writing
- **Exception: agent-updated files** — `Docs/RECENT_CHANGES.md`, `Docs/ACTIVE_CONTEXT.md`, plan checkboxes (`- [ ]` → `- [x]`), and `Docs/CHANGES.md` are updated directly without approval
- Format proposals as: "Proposed addition to [file]: `[content]`" — user responds approve/reject/modify

---

## Deterministic Rule Writing Standard

> All rules in CLAUDE.md and SAFETY_RULES.md must follow this format.

- **Format:** `[Action] is forbidden/required — [concrete consequence]`
- **Rules must be:**
  - **Measurable** — can be verified by grep, read, or test (not subjective)
  - **Consequential** — states what breaks if violated
  - **Actionable** — clear what to do or not do

**Bad:** "Be careful with network state"
**Good:** "Client-side NetworkVariable.Value assignment is forbidden — causes silent desync in host-authoritative model"
