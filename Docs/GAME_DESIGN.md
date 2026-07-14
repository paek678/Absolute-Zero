# Absolute Zero — Game Design Document

> **Genre**: 2.5D 1v1 Turn-Based Deathmatch | **Format**: Bo3 (3 rounds, first to 2 wins)
> **Visual**: 3D Korean pavilion (정자) + 2D hand-drawn characters/items
> **Network**: Host-authoritative, Unity Relay

---

## Overview

Two players sit across a wooden floor (마루). Each has a **fan blowing cold air**, lowering their body temperature. Use items to attack, defend, or sabotage. **First to reach 0° loses the round.**

```
Starting Temp: 37°  |  Fan: -1°/sec  |  Prep Time: 20s  |  Defeat: 0°  |  Recovery: 1°/sec (after Ready)
```

---

## Turn Cycle

```
PREP PHASE (20s)          ATTACK PHASE              RESOLUTION
─────────────────    →    ────────────────    →    ──────────────
Fan ON: -1°/sec          Actions execute           Apply results
Select item              in Ready-press order      Check 0° death
Press "Ready"            Defense always active     Threshold items
Fan OFF: recovery        First kill = win          → Next turn
```

### Prep Phase
- Fan decreases temperature at **1°/sec** while active
- Items are classified as **Sub** (utility) or **Main** (primary action):
  - **Sub items**: can use multiple per turn (effects applied immediately)
  - **Main item**: selecting ends turn immediately (max 1 per turn)
  - Order: **Sub → Main** (OK) / **Main → Sub** (blocked — Main ends turn)
- Alternative: press **"Ready" (준비 끝)** without Main → Sub effects only
- Ready / Main select → fan stops → temperature **recovers at 1°/sec**
- Timer expires with no selection → **idle** (no action taken, fully vulnerable)
- Temperature hits 0° during prep → **instant loss**

### Attack Phase
- Execute in **Ready-press order** (who pressed first goes first)
- **Defense exception**: always activates regardless of order
- If first action kills (0°), first player wins (second action behavior TBD — see Q10)

---

## Screen Layout

```
┌─────────────────────────────────────────────────┐
│  [Opponent Temp ██████████████████]              │
│  [My Temp ██████████████████████]          [20s] │
│                                                  │
│              ┌───────────┐                       │
│              │ OPPONENT  │  [선풍기]              │
│              │ (front)   │                       │
│              └───────────┘                       │
│         ···opponent items on floor···            │
│                                                  │
│  ─ ─ ─ ─ ─ ─ ─ 마루 ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─  │
│                                                  │
│    [기본1][기본2]            [랜덤1][랜덤2]        │
│      [기본3][기본4] [준비끝] [랜덤3][랜덤4]        │
│                 (semicircle arc)                  │
└─────────────────────────────────────────────────┘

Camera: Fixed 3rd-person behind player, slight overhead angle
Rendering: 3D background + 2D sprites on floor
Both clients see mirrored view (my items = bottom, opponent = top)
```

---

## Temperature System

```
37° ████████████████████████████ RED        (start)
30° ██████████████████████       PINK       → +1 random item
20° ██████████████               SKY BLUE   → +2 random items, frost edge
10° ████████                     BLUE       → +3 random items, heavy frost
 0° ░░░░░░░░░░░░░░░░░░░░░░░░░░░ FROZEN     → DEFEAT
```

- Max capacity: **8 random items** (basic items don't count)
- Threshold grants are **one-time only** — re-crossing doesn't re-grant
- Visual: segment markers disappear after grant

---

## Item System

### Types

| Type | Persistence | Example |
|------|-------------|---------|
| Basic/Permanent | Never consumed | Fan, Windbreaker |
| Basic/Consumable | Refreshes each game | Warm Tea, Cat |
| Random/Consumable | One-time use | All random items |

### Categories

| Category | Target | Timing | Examples |
|----------|--------|--------|----------|
| **Attack** | Opponent temp ↓ | Immediate | Fan, Ice Cream, Water Gun |
| **Defense** | Block incoming | Immediate | Windbreaker, Mask |
| **Recovery** | Self temp ↑ | Immediate | Warm Tea, Hot Pack |
| **Buff** | Self benefit | Next turn | Buldak Noodles, Soda |
| **Debuff** | Opponent penalty | Next turn | Samgyetang |
| **Sabotage** | Disrupt opponent | Varies | Cat, Red Card, Blue Tape |
| **Special** | Unique mechanic | Varies | Screwdriver, Tarot Card |

### Basic Items (Always Available)

| Item | Cat | Value | Effect |
|------|-----|-------|--------|
| Fan (부채) | ATK | 3° | Opponent temp −3° |
| Windbreaker (바람막이) | DEF | 4° | Block temp attacks (block mechanic TBD — see Q34) |
| Warm Tea (따뜻한 차) | REC | 7° | Self temp +7° (consumable) |
| Cat (고양이) | SAB | — | Reroll ALL opponent random items (consumable) |

### Random Items (Threshold Drops — One-Time Use)

| Item | Cat | Value | Drop | Mini-game |
|------|-----|-------|------|-----------|
| Hand Fan (손풍기) | ATK | 4° | 12% | — |
| Ice Cream (아이스크림) | ATK | 5° | 10% | — |
| Iced Americano (아.아) | ATK | 5° | 10% | — |
| Water Gun (물총) | ATK | 7° | 6% | 5s: hit 3 moving targets |
| Hug T-shirt (안아줘요) | ATK | =my temp | 4% | 10s: hug approaching character |
| Hot Americano (뜨.아) | REC | 5° | 10% | — |
| Smartphone (스마트폰) | REC | 3→5→7° | 6% | 5s: pattern unlock |
| Hot Pack (핫팩) | REC | 10° | 4% | 10s: tap 30× to heat up |
| Mask (마스크) | DEF | 100% | 8% | — |
| Samgyetang (삼계탕) | DBF | +3/−7° | 8% | — |
| Soda (탄산음료) | BUF | −5/+15° | 6% | 5s: gauge-match soda shake |
| Buldak Noodles (불닭볶음면) | BUF | +17° | 2% | 10s: tap to boil water |
| Screwdriver (십자드라이버) | SPC | 2°/s fan | 4% | 5s: tighten 3 screws |
| Tarot Card (속마음 타로카드) | SPC | — | 2% | 5s: pick 1 of 3 cards |
| Claw Machine (집게손) | SAB | — | 4% | 7s: timing claw grab |
| Blue Tape (청테이프) | SAB | — | 4% | 5s: timing tape cut |
| Red Card (레드카드) | SAB | — | 2% | 5s: tap red card |

> **Drop rates sum to 102%** — not a probability distribution. Drop mechanic TBD (see Q35).
> **11 items require mini-games**, 6 do not.

```
Category key: ATK=Attack  DEF=Defense  REC=Recovery
              BUF=Buff    DBF=Debuff   SAB=Sabotage  SPC=Special
```

---

## Environment Variables

Appears at **2nd prep phase** of each round. Random. Resets per round.

| Environment | Effect | Impact |
|-------------|--------|--------|
| **Sunny Day** (햇살쨍쨍) | Fan-off recovery: 2°/sec | Recovery items less valuable |
| **Cool Breeze** (바람선선) | Fan-off recovery: 0°/sec | Recovery items critical |
| **Cicada Song** (매미울음) | Audio/visual distraction | Pure chaos |
| **Kids** (잼민이들) | Steal 1 unused random item | Can't hoard items |
| **Ambulance** (앰뷸런스) | Turn 4: lower-temp player +10° | Comeback mechanic |
| **Summer Vacation** (여름방학) | Prep time: 20s → 10s | Less decision time |
| **Heat Wave** (폭염경보) | Lower-temp player acts first | Overrides Ready-order |

---

## Item Selection Flow

```
[1] Click item on floor → [2] Zoom-in + description → [3] "Use" confirm
                                                            │
                                              (mini-game required?)
                                              YES → play mini-game
                                                    pass → item queued
                                                    fail → item destroyed
                                              NO  → item queued
                                                            │
                                              (Sub or Main?)
                                              SUB → effect applied, stay in prep
                                                    can select more Sub or Main
                                              MAIN → effect queued → TURN END
                                                     fan OFF, recovery starts
                                                            │
Alternative: [4] Press "준비 끝" without Main → Sub effects only → TURN END
```

> **Sub → Main (OK)**: use utility items first, then finish with primary action.
> **Main → Sub (BLOCKED)**: selecting Main immediately ends the turn.

---

## Attack Sequence (Visuals)

```
"납량 시작" (Cooling Begins) → center screen text
     ↓
1st player item animation → temp bar reacts (shake + flash)
     ↓
Check: opponent 0°? → YES: freeze + shatter → round end
                       NO: continue
     ↓
2nd player item animation → temp bar reacts
     ↓
Check: opponent 0°? → round end or next turn
```

### Defeat Animations
| Scenario | Visual |
|----------|--------|
| Opponent reaches 0° | Opponent freezes solid → ice shatters → next round |
| Opponent 0° (final round) | Freeze → character shatters completely |
| Self reaches 0° | Screen edges freeze → fade out → next round |
| Self 0° (final round) | Camera rotates to opponent POV → own character shatters |

---

## Win Conditions

| Condition | Result |
|-----------|--------|
| Opponent temp = 0° | Win the round |
| Self temp = 0° | Lose the round |
| Both reach 0° | TBD |
| First to **2 round wins** | Match victory |

---

## Network Model

```
CLIENT                          SERVER (Host)
──────                          ─────────────
SelectItemRpc(slot) ──────→     Validate + store
ReadyRpc() ───────────────→     Record timestamp, stop fan
MiniGameResultRpc() ──────→     Validate result
                                    ↓
                         CombatResolver.Resolve()
                         TemperatureSystem.Apply()
                         BuffDebuffSystem.Tick()
                                    ↓
         ←──────────────── ResultRpc(outcomes)
         ←──────────────── NetworkVariable updates
UI updates from callbacks          (temp, phase, timer)
```

All mutations server-side: damage, healing, time, win/loss, item use, buff/debuff, mini-game validation.
