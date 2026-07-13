# PLAN_003: Lobby UI + Full Flow Integration

> **Status:** ✅ Complete
> **Created:** 2026-07-13

---

## Context

LobbyTestUI.cs was migrated from ArenaCombat_server with all lobby flow logic intact (create/join/leave/start + Relay integration). However, the UI scene objects (Canvas, buttons, panels, InputField) were NOT migrated — only the code. LobbyTestUI requires 13 Inspector-wired serialized fields that currently point to nothing.

**Goal:** Get a working end-to-end flow: LobbyScene (create/join lobby) → Relay connect → GameScene (turn system with temp bars).

## Approach: Runtime-Built Lobby UI

Convert LobbyTestUI into a **runtime-built UI** (same pattern as AZGameUI) that creates all Canvas/Button/InputField elements in code. This eliminates Inspector wiring entirely.

**Why not MCP scene wiring?** Creating 15+ UI elements via MCP and wiring serialized references is fragile and hard to verify. Runtime-built is self-contained, portable, and proven (AZGameUI already works this way).

## Implementation Steps

### Step 1: Create `AZLobbyUI.cs`
**File:** `Assets/Scripts/UI/Lobby/AZLobbyUI.cs`

New MonoBehaviour that builds lobby UI at runtime. Reuses the lobby flow logic from existing `LobbyTestUI.cs`:

**Panel 1 — Main (create/join):**
- Title: "ABSOLUTE ZERO"
- "CREATE LOBBY" button → `LobbyManager.CreateLobbyAsync()`
- Join code TMP_InputField + "JOIN" button → `LobbyManager.JoinLobbyByCodeAsync()`
- Status text for errors/loading

**Panel 2 — Lobby (waiting room):**
- Lobby code display (large, copyable)
- Player list (dynamically updated from lobby polling)
- "START GAME" button (host only) → Relay allocation → `SessionManager.StartGame()`
- "LEAVE" button → `LobbyManager.LeaveLobbyAsync()`
- Log/status area

**No Game Panel needed** — once game starts, `SessionManager.StartGame()` transitions to GameScene where `AZGameUI` takes over.

**Key logic ported from LobbyTestUI:**
- `OnCreateLobbyClicked()` → create lobby with random name
- `OnJoinClicked()` → join by code
- `OnStartGameClicked()` → Relay host → set join code in lobby → start game
- `CheckForGameStart()` → client detects game start → join Relay
- Event subscriptions: `OnLobbyCreated`, `OnLobbyUpdated`, `OnLobbyLeft`, Relay events

### Step 2: Add AZLobbyUI to LobbyScene
**Via MCP:**
- Create "LobbyUI" GameObject in LobbyScene
- Add AZLobbyUI component

### Step 3: Verify Full Flow
1. Play LobbyScene → UI appears with Create/Join buttons
2. Console: no errors
3. MPPM or build test: host creates → client joins → host starts → both transition to GameScene → turn system runs

---

## Task Checklist

- [x] Create folder `Assets/Scripts/UI/Lobby/`
- [x] Create `AZLobbyUI.cs` (runtime-built lobby UI with full flow logic)
- [x] MCP: Add LobbyUI GameObject to LobbyScene with AZLobbyUI component
- [x] Fix: namespace conflict (`UI.Lobby` → `UI.LobbyUI`)
- [x] Fix: TMP Essential Resources import (fonts missing)
- [x] Verify compilation (0 errors)
- [x] Play test LobbyScene (UI renders, services init, no errors)
- [x] Update harness docs

## Scope Declaration

### Will Create
- `Assets/Scripts/UI/Lobby/AZLobbyUI.cs`

### Will NOT Touch
- AbsoluteZeroTurnManager.cs, AZGameUI.cs, AZPlayerVisual.cs
- All network scripts (LobbyManager, RelayManager, SessionManager, etc.)
- LobbyTestUI.cs (kept as reference)
- CLAUDE.md, SAFETY_RULES.md

## Existing Code Reused
- `LobbyManager.cs` — all lobby API calls
- `RelayManager.cs` — `StartHostWithRelayAsync()`, `JoinRelayAsync()`
- `SessionManager.cs` — `StartGame()`, `Disconnect()`
- `LobbyTestUI.cs` — flow logic reference
- `AZGameUI.cs` — UI construction patterns (CreateText, CreateButton, CreatePanel helpers)

## Discovered Issues
- Namespace `AbsoluteZero.UI.Lobby` conflicts with `Unity.Services.Lobbies.Models.Lobby` → renamed to `AbsoluteZero.UI.LobbyUI`
- TMP Essential Resources not imported in fresh project → imported via `AssetDatabase.ImportPackage`
