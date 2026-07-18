# Absolute Zero — Direction & Detail Design Spec

> Received: 2026-07-18

---

## 1. Environment Variable Detail Design

### Activation Sequence
- Triggers **before the 2nd Prep Phase** starts
- **5-second announcement cutscene**, then Prep Phase begins
- During the cutscene, a **UI Text line appears center-screen** (unique per environment)
- **Temperature does NOT decrease** during the cutscene

### Environment Types

#### 1. SunnyDay (햇살쨍쨍)
- **Ability:** This round, turning off the fan recovers temperature at **2°/sec** (instead of 1°/sec)
- **Line:** "햇살이 더 쨍쨍해집니다."
- **Background VFX:** Global Light color shifts yellow, intensity increases

#### 2. CoolBreeze (바람선선)
- **Ability:** This round, turning off the fan does **NOT recover temperature** (0°/sec)
- **Line:** "시원한 바람이 불어옵니다."
- **Background VFX:** Wind particle effect around the scene (ref: https://www.youtube.com/watch?v=Jj8UHGe5Aps)

#### 3. Kids (잼민이들)
- **Ability:** On the **next turn** (before 3rd Prep Phase), one unused random item is stolen from each player
- **Line:** "근처에 어린 친구들이 서성거립니다."
- **Background VFX:** Elementary school kid sprites wander around the pavilion
- **Action VFX:**
  - My view: a kid's hand appears from behind and pulls an item backward
  - Opponent view: a kid reaches from behind the opponent and takes an item

#### 4. Ambulance (앰뷸런스)
- **Ability:** Before the **4th Prep Phase**, the player with the **lower temperature** gets **+10° heal**
- **Line:** "근처에 응급구조원이 대기중입니다."
- **Background VFX:** An ambulance arrives behind the opponent's side
- **Action VFX:**
  - My view: two hands appear from behind and wrap a blanket around the player
  - Opponent view: a paramedic places a blanket over the opponent
  - When the paramedic leaves, the blanket fades out (opacity decreases gradually)

#### 5. SummerVacation (여름방학)
- **Ability:** This round, **Prep Phase timer reduced** from 20s to **10s**
- **Line:** "여름방학이 얼마 남지 않았습니다."
- **Background VFX:** The timer UI shakes and turns red

#### 6. HeatWaveWarning (폭염경보)
- **Ability:** Attack order ignores ready-press order; **lower-temperature player acts first**
- **Line:** "폭염경보가 발생했습니다."
- **Background VFX:** Global Light color shifts red, intensity increases

---

## 2. Item Animation Detail Design

### General Rules
1. **When I use an item:** 1st-person view — only the item moves (use motion), no character animation
2. **When opponent uses an item:** Opponent character animation changes based on category (Attack / Defense / Food / Special)
3. **When opponent takes damage:** Opponent character flashes white + damage-taken animation
4. **When I take damage:** Screen edges get a freezing ice effect

### Per-Item Animations

| # | Item | Animation Description |
|---|------|----------------------|
| 1 | **Fan** | Swing the fan up and down **5 times over 2 seconds** toward the opponent |
| 2 | **Windbreaker** | A hand enters from the right and makes a zipper-closing motion |
| 3 | **Food items** | Food rises from below, Minecraft-style eating motion, camera tilts slightly down |
| 4 | **Feed items** | Right hand holds food, after 1s extends hand toward opponent |
| 5 | **Cat** | Cat opens eyes, jumps onto a random opponent item slot, scratches around on it |
| 6 | **Screwdriver** | Camera moves close to opponent's fan; screwdriver appears above and scrambles it |
| 7 | **Water Gun** | Fires the water gun toward the opponent |
| 8 | **Claw Machine** | Claw appears above a random opponent item, grabs it, tosses it to my inventory slot |
| 9 | **Tarot Card** | Hand-raise motion lifting a card upward; arrow reveals which item opponent will use |
| 10 | **Recovery items** | Item rises from below; orange glow effect on screen edges |
| 11 | **Blue Tape** | Camera moves close to opponent's basic items; blue tape covers/blocks them |
| 12 | **Mask** | Mask rises from below; stays on screen until Attack Phase ends |
| 13 | **Red Card** | Hand-raise motion lifting a card; opponent does a disappointed animation |
| 14 | **Hug T-shirt** | Camera moves close to opponent; two hands reach toward the opponent |

### Notes
- Detailed animation references will be provided as video later
- Videos currently available for: Food items (Warm Tea), Fan, Cat (sprite unchanged)
