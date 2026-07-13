# PLAN_002: Turn System + Greybox Demo

> **Status:** ✅ Complete
> **Created:** 2026-07-13

---

## Objective

Implement the temperature-based turn system and wire it into a greybox demo. Players see capsule objects, temperature bars, timer, and action buttons. Everything server-authoritative via NGO.

## Turn Flow
1. **WaitingForPlayers** → 2 players connected → start
2. **PrepTurn** (20s): temperature -1°/sec, players pick action (Attack/Defend/Charge)
3. **AttackTurn**: resolve simultaneously, apply results, display 3s
4. → Back to PrepTurn (or GameOver if temp ≤ 0)

## Scope Declaration

### Will Create
- `Assets/Scripts/Core/Game/TurnEnums.cs` — TurnPhase, ActionType enums
- `Assets/Scripts/Core/Game/AbsoluteZeroTurnManager.cs` — NetworkBehaviour turn state machine
- `Assets/Scripts/UI/Game/AZGameUI.cs` — Runtime-built greybox UI
- `Assets/Scripts/Core/Player/AZPlayerVisual.cs` — Capsule visual with temp-based color

### Will NOT Touch
- Network infrastructure (PLAN_001 files)
- Lobby system
- CLAUDE.md, SAFETY_RULES.md

---

## Task Checklist

- [x] Create folder structure (Core/Game, UI/Game, Core/Player)
- [x] TurnEnums.cs
- [x] AbsoluteZeroTurnManager.cs
- [x] AZGameUI.cs
- [x] AZPlayerVisual.cs
- [x] MCP: verify compilation (0 errors, 0 warnings)
- [x] MCP: set up GameScene objects (TurnManager + SceneLoadSyncManager + SpawnPoints + GameUI + Ground + Camera + Light)
- [x] MCP: create Player prefab with AZPlayerVisual + NetworkObject
- [x] MCP: create NetworkPrefabsList + wire to NetworkManager + PlayerSpawnManager
- [x] MCP: set up LobbyScene (NetworkManager + Managers + Camera + Light + EventSystem)
- [x] MCP: Build Settings (LobbyScene=0, GameScene=1)
- [x] Update harness docs
- [x] Report flow to user

## Discovered Issues
(none)
