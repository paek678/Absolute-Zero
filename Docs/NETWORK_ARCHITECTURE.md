# Network Architecture — Absolute Zero

> NGO + Relay host-authoritative multiplayer architecture.
> Based on ArenaCombat_server patterns, adapted for turn-based mechanics.

---

## Architecture Overview

```
┌─────────────┐     Unity Relay     ┌─────────────┐
│   Client A  │ ←─────────────────→ │   Client B  │
│  (Host)     │                     │  (Client)   │
│             │                     │             │
│ TurnManager │                     │ UI only     │
│ Resolution  │                     │ Action Rpc  │
│ NetworkVars │                     │ Callbacks   │
└─────────────┘                     └─────────────┘
```

- **Host** runs all game logic (TurnManager, resolution, temperature calculation)
- **Client** only sends action selection via ServerRpc and reads NetworkVariable updates
- Unity Relay handles NAT traversal and connection relay

---

## Authority Model

| Data | Owner | Sync Method |
|------|-------|-------------|
| Player temperature | Host | `NetworkVariable<float>` |
| Turn phase | Host | `NetworkVariable<TurnPhase>` |
| Turn timer | Host | `NetworkVariable<float>` |
| Action selection | Client → Host | `[ServerRpc]` |
| Action result | Host → All | `[ClientRpc]` |
| Player ready state | Host | `NetworkVariable<bool>` |

---

## Connection Flow

1. Player A creates lobby (RelayManager allocates relay)
2. Player A gets join code
3. Player B enters join code (RelayManager joins relay)
4. SessionManager assigns player slots (0 = Host, 1 = Client)
5. Both players ready → TurnManager starts first PrepTurn

---

## Rpc Patterns

### Client → Server
```csharp
[ServerRpc]
void SubmitActionServerRpc(ActionType action, ServerRpcParams rpcParams = default)
```

### Server → All Clients
```csharp
[ClientRpc]
void BroadcastTurnResultClientRpc(float player0TempChange, float player1TempChange, ActionType p0Action, ActionType p1Action)
```

---

## Key Constraints

1. **Never trust client state** — all validation happens on host
2. **NetworkVariable writes are host-only** — client writes cause exception
3. **Action buffer must be cleared each turn** — stale actions from previous turns cause incorrect resolution
4. **Relay connection timeout: handle gracefully** — show reconnect UI, don't crash
5. **Host migration is NOT supported in demo** — if host disconnects, game ends
