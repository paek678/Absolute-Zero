# Game Systems — Absolute Zero

> Technical design for core game systems.
> Update this document when system implementations change.

---

## AbsoluteZeroTurnManager

Central state machine for turn flow. Runs on Host/Server only.

### Responsibilities
- Turn phase state machine: PrepTurn → AttackTurn → Resolution → (loop or GameOver)
- 20-second Prep Turn timer (NetworkVariable)
- Per-player temperature decay during Prep Turn (1°/sec or 2°/sec with strong wind)
- Store player action selections (received via ServerRpc)
- Detect when both players have selected (or timer expires)
- Simultaneous action resolution
- Win/loss determination
- Temperature NetworkVariable management

### Turn Phase Enum (planned)
```
PrepTurn → AttackTurn → Resolution → PrepTurn (loop)
                                   → GameOver (if winner)
```

### Action Resolution Order
1. Both player actions are revealed
2. Temperature modifications calculated (attack effects + healing)
3. Net temperature change applied to each player
4. Check defeat condition (≤ 0°)
5. Broadcast results via ClientRpc

---

## AZDemoUI

Client-side UI controller. Reads NetworkVariable callbacks.

### Responsibilities
- Display own temperature bar (bottom)
- Display opponent temperature bar (top)
- Display remaining time (center top)
- Display action buttons (bottom left)
- Display ready state indicator
- Display turn result log (right side)

### Data Binding
- Temperature bars ← `NetworkVariable<float>` OnValueChanged
- Timer display ← `NetworkVariable<float>` OnValueChanged
- Action buttons → `[ServerRpc]` on click
- Result log ← `[ClientRpc]` broadcast from server

---

## Network Infrastructure (from ArenaCombat base)

### Planned Components
| Component | Role |
|-----------|------|
| RelayManager | Unity Relay allocation + join |
| SessionManager | Player slot assignment, ready state |
| LobbyManager | Room creation, matchmaking |
| NetworkManager | NGO bootstrap, connection lifecycle |
| InputValidator | Server-side action validation |

### Data Flow
```
Client A selects action
    → [ServerRpc] SubmitActionServerRpc(actionType)
    → Host stores in actionBuffer[playerIndex]
    → Both selected or timer expires
    → Host resolves simultaneously
    → Host updates NetworkVariables (temperature, phase)
    → [ClientRpc] BroadcastResultClientRpc(results)
    → All clients update UI
```

---

## Screen Layout

```
┌──────────────────────────────────┐
│     [Opponent Temp Bar]          │
│     [Opponent Status]            │
│                                  │
│   [Timer: 20s]                   │
│                                  │
│  ┌────────┐    ┌────────┐       │
│  │Opponent│    │  Fan   │       │
│  │ Char   │    │(wind)  │ [Log] │
│  └────────┘    └────────┘       │
│                                  │
│  ┌────────┐                     │
│  │  My    │                     │
│  │ Char   │                     │
│  └────────┘                     │
│                                  │
│ [Actions]   [My Temp Bar]       │
│ [부채][삼계탕][가면][선풍기]      │
└──────────────────────────────────┘
```
