# PLAN_006 — Item World Display (Hover Interaction)

> Create world-space item visuals with mouse raycast hover detection

## Status: ✅ Complete

## Scope

Dummy 2D sprite prefabs for items placed in world space. Camera-based mouse raycasting detects hover. Hovered item shows scale-up + outline effect.

### Will Create
- `Assets/Scripts/Core/Item/ItemWorldView.cs` — per-item component: sprite visual, outline, collider, hover animation
- `Assets/Scripts/UI/Game/ItemWorldDisplay.cs` — spawns item views from inventory, handles raycasting + hover state

### Will Modify
- `Assets/Scripts/UI/Game/AZGameUI.cs` — spawn ItemWorldDisplay on Start

### Will NOT Touch
- PlayerState, ActionQueue, CombatResolver (gameplay logic unchanged)
- TurnManager, MatchManager (server systems unchanged)
- ItemDataSO hierarchy (data unchanged)
- Existing UI buttons (kept as-is for now)

## Tasks

### Phase 1: Core Components
- [x] ItemWorldView.cs — sprite creation, outline toggle, scale lerp, collider
- [x] ItemWorldDisplay.cs — inventory-based spawning, raycast hover, state management

### Phase 2: Integration
- [x] AZGameUI.cs — spawn ItemWorldDisplay instance on Start
- [ ] Verify: hover detection works, outline toggles, scale animates

## Design

### Item Visual (per slot)
- Colored card sprite (48x64px at 64ppu = 0.75x1 world units)
- Colors by category: Attack=red, Defense=blue, Recovery=green, Sabotage=amber
- TextMesh label showing item name
- Outline: slightly larger sprite behind (12% scale), yellow, toggled on hover
- Scale: lerp to 1.15x on hover, 1.0x on unhover

### Hover System
- Camera.main.ScreenPointToRay(Input.mousePosition)
- Physics.Raycast → BoxCollider on each ItemWorldView
- Track current/previous hovered → SetHovered(true/false)

### Layout
- Items in horizontal row, configurable spacing/position
- Default: spawns at MyItemSpawnRoot marker position (0, 0.05, 1.5), spacing=1.5 units
