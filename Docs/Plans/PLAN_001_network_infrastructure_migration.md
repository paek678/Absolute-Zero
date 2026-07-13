# PLAN_001: Network Infrastructure Migration from ArenaCombat_server

> **Status:** 🔄 In Progress (Code complete — awaiting manual Unity Editor setup)
> **Created:** 2026-07-13
> **Source:** `C:\Users\paek6\Unity Project\ArenaCombat_server\NETWORK_MIGRATION_GUIDE.md`

---

## Objective

Migrate NGO + Relay + Lobby multiplayer infrastructure from ArenaCombat_server project into Absolute Zero. All code uses namespace `AbsoluteZero`. This provides the network foundation for the 2.5D 1v1 turn-based deathmatch.

## Scope Declaration

### Will Modify
- `Packages/manifest.json` — add 7 missing network packages
- `Assets/Scripts/Core/Network/` — 8 new files (A-1 to A-8)
- `Assets/Scripts/UI/Utility/` — 1 new file (A-9)
- `Assets/Scripts/UI/TestUI/` — 3 new files (A-10 to A-12)
- `Docs/ACTIVE_CONTEXT.md` — status update
- `Docs/RECENT_CHANGES.md` — change log
- `Docs/CHANGES.md` — high-level entry

### Will NOT Touch
- Existing URP settings (`Assets/Settings/`)
- Existing scenes (`Assets/Scenes/SampleScene.unity`)
- `.claude/` harness files
- `CLAUDE.md`, `Docs/SAFETY_RULES.md` (no changes needed)
- Turn system / game logic (separate plan)

---

## Task Checklist

### Phase 1: Package Installation
- [x] Add 7 packages to `Packages/manifest.json` (netcode.gameobjects, transport, services.relay, services.lobby, services.authentication, multiplayer.playmode, multiplayer.tools)
- [x] Verify `inputsystem` 1.19.0 and `ugui` 2.0.0 already present

### Phase 2: Folder Structure
- [x] Create `Assets/Scripts/Core/Network/`
- [x] Create `Assets/Scripts/UI/TestUI/`
- [x] Create `Assets/Scripts/UI/Utility/`
- [x] Create `Assets/Scenes/` (already exists)
- [x] Create `Assets/Prefabs/`

### Phase 3: Network Core Files (namespace: AbsoluteZero)
- [x] A-1: `NetworkConstants.cs`
- [x] A-2: `PlayerSpawnPoint3D.cs`
- [x] A-3: `LobbyServiceHelper.cs`
- [x] A-4: `RelayManager.cs`
- [x] A-5: `LobbyManager.cs`
- [x] A-6: `SessionManager.cs`
- [x] A-7: `PlayerSpawnManager.cs`
- [x] A-8: `SceneLoadSyncManager.cs`

### Phase 4: UI Files (namespace: AbsoluteZero)
- [x] A-9: `ScrollableLogDisplay.cs`
- [x] A-10: `OutgoingDataLog.cs`
- [x] A-11: `LobbyPlayerSlotsUI.cs`
- [x] A-12: `LobbyTestUI.cs`

### Phase 5: Harness Updates
- [x] Update `Docs/ACTIVE_CONTEXT.md`
- [x] Update `Docs/RECENT_CHANGES.md`
- [x] Update `Docs/CHANGES.md`

### Phase 6: User Handoff (Manual Unity Editor Work)
- [x] Report LobbyScene setup instructions
- [x] Report GameScene setup instructions
- [x] Report Player prefab + Network Prefabs List instructions
- [x] Report Unity Dashboard setup instructions

## Discovered Issues
(none)
