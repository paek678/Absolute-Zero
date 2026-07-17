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
- **1 item per turn** — select one item, confirm with "사용하기", then press "준비 끝"
  - Exception: certain instant-use items (e.g. Tarot Card) activate immediately on confirm, then allow selecting **1 additional item**
- Alternative: press **"준비 끝"** without selecting any item → no action this turn
- Ready / Main select → fan stops → temperature **recovers at 1°/sec**
- Timer expires with no selection → **idle** (no action taken, fully vulnerable)
- Temperature hits 0° during prep → **instant loss**

### Attack Phase
- Execute in **Ready-press order** (who pressed first goes first)
- **Defense exception**: always activates regardless of order
- If first action kills (0°), **second action is cancelled** — round ends immediately (Q10 confirmed)
- Simultaneous Ready → **lower temperature acts first** (comeback opportunity) (Q14 confirmed)

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

| Item | Cat | Persistence | Value | Effect |
|------|-----|-------------|-------|--------|
| Fan (부채) | ATK | Permanent | 3° | Opponent temp −3° |
| Windbreaker (바람막이) | DEF | Permanent | 4° | Partial block: absorbs up to 4° of temp attack damage |
| Warm Tea (따뜻한 차) | REC | Consumable | 7° | Self temp +7° |
| Cat (고양이) | SAB | Consumable | — | Reroll ALL opponent random items |

### Random Items (Threshold Drops)

| Item | Cat | Value | Drop% | Mini-game |
|------|-----|-------|-------|-----------|
| Hand Fan (손풍기) | ATK | 4° | 12% | — |
| Ice Cream (아이스크림) | ATK | 5° | 10% | — |
| Iced Americano (아.아) | ATK | 5° | 10% | — |
| Water Gun (물총) | ATK | 7° | 6% | 5s: hit 3 moving targets |
| Hug T-shirt (안아줘요 티셔츠) | ATK | =my temp | 4% | 10s: hug approaching character |
| Hot Americano (뜨.아) | REC | 5° | 10% | — |
| Smartphone (스마트폰) | REC | 3→5→7° | 6% | 5s: pattern unlock |
| Hot Pack (핫팩) | REC | 10° | 4% | 5s: tap 10× |
| Mask (마스크) | DEF | 100% food block | 8% | — |
| Samgyetang (삼계탕) | DBF | opp +3 now / opp −7 next | 8% | — |
| Soda (탄산음료) | BUF | self −5 now / self +15 next | 6% | — |
| Buldak Noodles (불닭볶음면) | BUF | +17° | 2% | 10s: tap to boil water |
| Screwdriver (십자드라이버) | SPC | 2×fan | 4% | 7s: tighten 3 screws |
| Tarot Card (속마음 타로카드) | SPC | — | 2% | — |
| Claw Machine (집게손) | SAB | — | 4% | 7s: timing claw grab |
| Blue Tape (청테이프) | SAB | — | 4% | 5s: timing tape cut |
| Red Card (레드카드) | SAB | — | 2% | 5s: tap red card among yellows |

> **Drop rates sum to 102%** — treated as **weighted pool** (e.g. 손풍기 12/102 ≈ 11.8%). Duplicate drops allowed, but **max 3 copies** of same item.
> **9 items require mini-games**, 8 do not.

```
Category key: ATK=Attack  DEF=Defense  REC=Recovery
              BUF=Buff    DBF=Debuff   SAB=Sabotage  SPC=Special
```

---

## Mini-Game System

Mini-games trigger during PrepPhase when selecting certain items. The PrepPhase timer keeps running.

### Flow

```
Item click → Mini-game starts (PrepPhase timer continues)
  ├─ SUCCESS → item queued for use
  ├─ FAIL    → selection cancelled, return to item selection
  └─ PREP TIME EXPIRES → mini-game force-cancelled, no effect
```

- Failure = immediate selection cancel (one chance only)
- To retry, must re-select the item and play the mini-game again
- Success = item enters the action queue

### Mini-Game Details

| Item | Time | Input | Description | Success | Fail |
|------|------|-------|-------------|---------|------|
| Screwdriver (십자드라이버) | 7s | Circular drag | 3 screws appear. Drag clockwise (3 rotations each) to tighten | All 3 screws tightened in time | Time out or incomplete |
| Claw Machine (집게손) | 7s | Timing tap | Claw moves left-right above target item. Tap when aligned | Claw grabs item at correct timing | Missed timing or time out |
| Water Gun (물총) | 5s | Tap | 3 targets move irregularly. Tap to shoot | Hit all 3 targets in time | Missed targets or time out |
| Hot Pack (핫팩) | 5s | Rapid tap (10×) | Hot Pack image turns red as tapped, gauge rises | 10 taps within 5s | Count not reached |
| Blue Tape (청테이프) | 5s | Timing tap | Tape stretches with timing bar. Tap in green zone | Tap in green zone | Tap outside green or time out |
| Smartphone (스마트폰) | 5s | Drag | 3×3 dot grid, trace shown pattern (ㄱ, ㄴ, Z, etc.) | Pattern matched | Wrong pattern or time out |
| Buldak Noodles (불닭볶음면) | 10s | Rapid tap | Pot + gauge. Tapping boils water, gauge rises | Gauge reaches 100% | Gauge incomplete |
| Red Card (레드카드) | 5s | Tap | Yellow + red cards appear mixed. Find and tap red card | Tap red card correctly | Tap yellow or time out |
| Hug T-shirt (안아줘요 티셔츠) | 10s | Timing tap (both sides) | Character approaches center. Tap left+right (arms) simultaneously when in hug zone | Timed correctly | Mistimed or time out |

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
[1] Click item on floor
        │
  (mini-game required?)
  YES → mini-game starts (PrepPhase timer keeps running)
        ├─ SUCCESS → item queued
        ├─ FAIL    → selection cancelled, back to [1]
        └─ PREP TIME OUT → mini-game cancelled, no effect
  NO  → item queued immediately
        │
  (Sub item?)
  YES → queued, turn continues (can still pick Main)
        → executes at Attack Phase START
  NO  → Main item queued for Attack Phase
        │
[2] Press "준비 끝" → fan OFF, recovery starts → TURN END
    (action cannot be changed after "준비 끝")

Alternative: Press "준비 끝" without selecting → no action this turn
```

> **Main/Sub system:** 1 Main + 1 Sub per turn. Sub items execute at Attack Phase start before Main resolution.
> **TBD:** Which items are Sub? (See Open Questions)

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
| Both reach 0° simultaneously | Draw — round voided, replay (no score change) |
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

---

## Open Questions (기획 확인 필요)

### Resolved

| # | Question | Answer |
|---|----------|--------|
| Q4 | **삼계탕 "+3, -7" 효과 방향** | ✅ 즉시 상대 온도 +3° (올림) → 다음 턴 상대 온도 -7° (내림). 둘 다 상대에게 적용 |
| Q5 | **탄산음료 "-5, +15" 효과 방향** | ✅ 즉시 내 온도 -5° (자해 비용) → 다음 턴 내 온도 +15° (이득). 둘 다 자신에게 적용 |
| Q11 | **미니게임 판정 권한** | ✅ 클라이언트 판정 + 서버 타임아웃 강제 실패. 상세 아래 참조 |
| Q12 | **미니게임 실패 시 아이템 소모** | ✅ 소모 안 됨. 실패해도 아이템 유지, 다시 선택하여 재도전 가능 |
| Q13 | **공격턴 아이템 사용 연출** | ✅ 전용 애니메이션/이펙트 예정, 아트 확정 후 결정 |
| Q14 | **선풍기 월드 표시** | ✅ StayItem 위치에 해당 오브젝트 스폰만 하면 됨. 연출은 추후 추가 |
| Q15 | **상대방 아이템 보유 목록** | ✅ EnemyItem 위치에 서버가 내려주는 목록 표시. 소모 시 시각적 제거, 재획득 시 다시 표시. 서버 권위 |
| Q17 | **환경 시스템** | ✅ 데모에는 미포함, 이후 추가 |
| Q1 | **핫팩 미니게임 수치** | ✅ 5초 / 10회 연타 (2026-07-17 확정) |
| Q2 | **따뜻한 차 사용 횟수** | ✅ 1회 — 스마트폰 제외 모든 소모품은 1회 후 소멸 |
| Q3 | **고양이 사용 횟수** | ✅ 1회 |
| Q7 | **드롭 확률 합계 102%** | ✅ 스프레드시트에서 조정 예정 — 확정 전까지 가중치 풀(/102) 유지 |
| Q9 | **타로카드 타이밍** | ✅ 준비 시간 내 즉발 + 사용은 "아이템 사용"으로 카운트 안 함 (사용 후 1개 더 선택 가능) |
| Q10 | **미니게임 중 상대 화면** | ✅ 안 보임 — 상대 모션은 "대기"/"준비 끝" 뿐. 타로카드만 사용 즉시 상대에게 시각 전달 |
| Q16 | **슬롯 UI 레이아웃** | ✅ 랜덤 4×2 배치, 빈 슬롯 처음부터 표시 |
| Q18 | **스마트폰 3→5→7°** | ✅ 특수 기능으로 3회 사용 가능 |
| Q19 | **티셔츠 역효과** | ✅ 의도된 리스크 (심리전 요소) |
| Q20 | **라운드 리셋 범위** | ✅ 온도/소모품/랜덤(4개 재지급)/버프/구간 이력 초기화, 영구템 유지(강화 수치만 리셋) |
| Q21 | **버프/디버프 중첩** | ✅ 중첩 가능 — 단 선풍기 강화만 isUpgrade로 중첩 방지 |
| Q22 | **이번 턴 선택 공개** | ✅ 기본 비공개, 타로카드 사용 시에만 공개 |
| Q23 | **지연 효과 방어** | ✅ 방어 무시하고 온도 적용 |

### Resolved: Mini-Game Judgment Model (Q11)

```
미니게임 중 성공/실패 → 클라이언트에서 판정 → 결과만 ServerRpc로 전송
PrepPhase 타이머 만료 → 서버가 턴 종료 판정 → 클라이언트도 미니게임 강제 실패 처리

핵심: 서버 타이머가 마스터. 클라/서버 핑 차이로 꼬여도
      서버의 "턴 종료" 판정이 최종 → 클라는 무조건 실패로 전환.
```

### Remaining (질문지 제외 — 아이템 스프레드시트에 직접 명시 예정)

| # | Item | Status |
|---|------|--------|
| Q6 | **마스크 "음식 아이템" 범위** — 아이템 기획서에 음식 태그 직접 명시 예정 | ⏳ 스프레드시트 대기 |
| Q8 | **각 아이템 Main/Sub 구분** — 아이템 기획서에 슬롯 구분 직접 명시 예정 | ⏳ 스프레드시트 대기 |
| Q7 후속 | **드롭 확률 조정치** — 스프레드시트 확정 시 수치 반영 | ⏳ 스프레드시트 대기 |

> 그 외 질문은 전부 답변 완료 (2026-07-17). 답변 상세와 파생 코드 수정 백로그는 `DESIGN_QUESTIONS.md` 참조.
