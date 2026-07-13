# Game Design — Absolute Zero

> 2.5D 1v1 Multiplayer Turn-Based Deathmatch
> Core concept: Manage temperature instead of HP. Reduce opponent's temperature to 0°.

---

## Core Rules

- Starting temperature: 37°
- Defeat condition: temperature reaches 0° or below
- One turn = Prep Turn + Attack Turn
- During Prep Turn, both players simultaneously choose actions
- Prep Turn time limit: 20 seconds
- Players who haven't chosen an action lose 1° per second
- Once a player selects an action, their temperature loss stops
- When both players have selected or 20 seconds pass → Attack Turn begins
- Attack Turn: both actions are resolved simultaneously
- Net temperature change is calculated and applied

---

## Turn Flow

1. Prep Turn starts
2. Both players choose actions simultaneously
3. Players without selection lose 1°/sec
4. Selection stops temperature loss for that player
5. Both selected OR 20 seconds elapsed → Prep Turn ends
6. Attack Turn starts
7. Both actions calculated simultaneously
8. Net temperature change applied
9. Check if any temperature ≤ 0°
10. No winner → next Prep Turn

---

## Base Values

| Parameter | Value |
|-----------|------:|
| Starting temperature | 37° |
| Defeat threshold | ≤ 0° |
| Prep Turn duration | 20 sec |
| Base temp loss rate | -1°/sec |
| Strong wind loss rate | -2°/sec |

---

## Actions

| Action | Effect | Description |
|--------|--------|-------------|
| Fan (부채) | Opponent -5° | Basic attack |
| Samgyetang (삼계탕) | Self +10° | Instant heal |
| Ghost Mask (귀신 가면) | Debuff opponent | Increases opponent's temp loss next turn |
| Fan Sabotage (선풍기 조작) | Double opponent's wind | Opponent's Prep Turn loss becomes -2°/sec next turn |

---

## Win/Loss Conditions

- Reduce opponent to 0° or below → Win
- Own temperature reaches 0° or below → Lose
- Both reach 0° simultaneously → higher final temperature wins
- Equal final temperature → Draw

---

## Network Model

- NGO (Netcode for GameObjects)
- Unity Relay for connection
- Host-authoritative: server computes all game state
- Client sends action selection only
- NetworkVariable for temperature, turn state, timer sync
- Rpc for result broadcast and VFX triggers

---

## Demo Scope (Minimum Viable)

1. 2 players connect via Relay
2. Prep Turn runs for 20 seconds
3. Temperature decreases in real-time during prep
4. Each player can select an action
5. Both actions resolved simultaneously
6. Temperature changes reflected in UI
7. Win/loss determined at 0° threshold
