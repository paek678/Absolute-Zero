# PLAN_008 — Mini-Game System (Greybox: Hot Pack / Buldak / Screwdriver)

> Status: 🔄 Implemented — compile clean + assets patched; MPPM play-test pending
> Branch: `feature/minigame-greybox` (from b5e11d8)
> Created: 2026-07-17
> Spec basis: GAME_DESIGN.md "Mini-Game System" section + Q11/Q12 confirmed rulings + user constraints (2026-07-17)

---

## User Constraints (2026-07-17)

1. **Server judges only START and END** of a mini-game; gameplay runs entirely client-side (= confirmed Q11 model: client judges success/fail, server timer is master).
2. Mini-games are **UI-based greybox** (uGUI, runtime-built — project convention).
3. **Scope: 3 games this pass** — 핫팩(Hot Pack, rapid tap), 불닭볶음면(Buldak, tap-to-fill gauge), 십자드라이버(Screwdriver, circular drag). The other 6 games reuse the same pipeline later.

## Confirmed spec being implemented (from GAME_DESIGN.md)

- Trigger: clicking an item with `RequiresMiniGame` during PrepPhase; **prep timer keeps running**.
- SUCCESS → item queued (same as normal selection). FAIL → selection cancelled, **item NOT consumed** (Q12), re-select to retry. PREP TIMEOUT → force-cancel, no effect.
- Server timer is master: when server ends prep, client force-fails any running mini-game.
- Opponent sees nothing (Q10 pending — default: independent prep, no mirroring).

## Current code reality (verified)

- `ItemDataSO` already has `RequiresMiniGame` / `MiniGameType` / `MiniGameTimeLimit` — referenced nowhere else yet (clean hook).
- Client click path: `AZGameUI.cs:351` → `PlayerState.SelectItemServerRpc(slot)`; ready at `:370`.
- Server queue path lives inside `PlayerState.SelectItemServerRpc` (Sub → `SetSub`, Main → `SetSelected` + `HasSelectedItem`).
- `TurnManager.PrepPhaseRoutine` (line 137) ends via ForceReady at timeout (204) → `AttackPhaseRoutine`; `OnPhaseChangedClientRpc` exists (416).
- PlayerState is owner-owned → `SendTo.Owner` ClientRpc reaches exactly the right client (no FIX-06 workaround needed).

## Design

### Flow
```
[client] item click ──SelectItemServerRpc──▶ [server] validate (phase/ready/slot/usable/CanUse)
                                                │  RequiresMiniGame == false → queue as today
                                                ▼  true
                                     record pending {slot, deadline = ServerTime + timeLimit (+0.5s grace),
                                                     capped at prep end}
[client] ◀──StartMiniGameClientRpc(slot, type, timeLimit)── (SendTo.Owner)
   open greybox UI, play locally, judge locally
   └─▶ SubmitMiniGameResultServerRpc(slot, success)  (sent once; local timeout auto-sends fail)
                                            [server] validate: pending matches / phase Prep / !IsReady / within deadline
                                                success → ServerQueueItem(slot)   (extracted shared path)
                                                fail    → clear pending only (item untouched — Q12)
[any time] prep ends → server clears pendings; phase-change callback force-closes client UI as FAIL
```

### New/changed pieces

| Piece | Where | Notes |
|-------|-------|-------|
| `ServerQueueItem(slot, itemData)` | `PlayerState` (extract from SelectItemServerRpc) | shared by normal select + minigame success; behavior unchanged |
| Pending mini-game state + `StartMiniGameClientRpc` + `SubmitMiniGameResultServerRpc` | `PlayerState` | per-player state lives naturally on the player object; server-side fields only |
| Prep-end pending cleanup | `TurnManager.PrepPhaseRoutine` end / ForceReady | one call per player: `ClearPendingMiniGame()` |
| `int MiniGameGoal` field | `ItemDataSO` | tap count (핫팩), gauge taps (불닭), rotations per screw (드라이버) — additive, non-breaking |
| Registry values | `ItemManager` | 핫팩: TapRepeat / **5s / 10 taps (Q1 answered 2026-07-17)**; 불닭: BoilWater / 10s / gauge 100% (tap +4%); 드라이버: ScrewTighten / 7s / 3 screws × 3 rotations |
| `MiniGameHub` (client MonoBehaviour) | `Assets/Scripts/UI/MiniGame/` | maps MiniGameType → game UI; subscribes to start-Rpc event + phase change (force-cancel); blocks 준비끝 while a game is open |
| `MiniGameUIBase` (abstract) | same folder | runtime-built panel (AZGameUI style): title, countdown bar, cancel handling, Success()/Fail() single-shot submit |
| `HotPackMiniGameUI` | same | tap button N times before T — tapped count + reddening image placeholder |
| `BuldakMiniGameUI` | same | gauge 0→100%, +4%/tap, 10s |
| `ScrewdriverMiniGameUI` | same | 3 screws; pointer drag accumulates angle around screw center (IDragHandler); 3×360° per screw, then next |

### Edge cases covered in implementation
- Result arriving after server deadline / after prep end → rejected (late = fail).
- 준비끝 during a game: client disables the button; server also rejects results once `IsReady`.
- Duplicate results (double-tap submit) → single-shot guard client-side + pending cleared server-side on first.
- Fail → retry allowed by clicking the item again (new START), time permitting.
- Client timer displays `min(timeLimit, remaining prep)` so the player sees the real budget.
- Disconnect mid-game → pending cleared with prep end; no lingering state.

## Will NOT touch
- CombatResolver / TemperatureSystem / BuffDebuffSystem / MatchManager
- Network/Lobby/Relay layer, scenes, prefabs
- Item effect logic (ExecuteEffect) — mini-game only gates *whether* the item gets queued
- The other 6 mini-games (pipeline supports them later)

## Open questions — ALL ANSWERED (2026-07-17 designer batch reply)
- ~~Q1 핫팩 수치~~ → **5초 / 10회 연타** (plan uses this).
- ~~Q10 상대 화면~~ → **안 보임 확정** — 상대 모션은 "대기"/"준비 끝" 두 가지뿐; 미니게임 진행 표시 없음. 타로카드만 상대에게 즉시 시각 전달(이번 범위 아님). Plan's default is now confirmed spec.
- Full answers + derived code backlog (차 MaxUses 1, 스마트폰 3회, 선풍기 강화 중첩 방지, 라운드 리셋 시 강화 초기화, 타로 즉발/카운트 제외, 슬롯 12칸 UI): see `DESIGN_QUESTIONS.md`. These are separate follow-ups — NOT in this plan's scope.
- 십자드라이버는 성공 시 효과(상대 선풍기 2×)가 다음 준비시간 적용 — 이미 SpecialItemDataSO + BuffSystem 경로 존재, 이번 작업은 게이트만.

## Checklist (implementation order)

- [x] 1. Data: `ItemDataSO.MiniGameGoal` added; assets patched via MCP — HotPack 5s/10, Buldak 10s/25, Screwdriver 7s/3 (enum members verified: TapRepeat=4 / BoilWater=6 / TightenScrews=7; assets already had RequiresMiniGame/Type set by partner, only TimeLimits corrected to confirmed spec)
- [x] 2. Server: `ServerQueueItem` extracted (shared by normal select + mini-game success); slot-type eligibility checks moved BEFORE the mini-game gate so a doomed game never starts
- [x] 3. Server: pending slot + deadline (`min(limit, prep-remaining) + 0.5s grace`), `StartMiniGameClientRpc` (SendTo.Owner), `SubmitMiniGameResultServerRpc` (single-shot pending consume → phase/ready/deadline validation → success re-validates slot then queues); pending cleared in ResetForNewTurn + PressReady; new SelectItem rejected while pending (modal)
- [x] 4. Client: MiniGameHub (lazy-binds local player + TurnManager like AZGameUI; phase-change force-cancel; unimplemented type → auto-fail) + MiniGameUIBase (fullscreen dim blocks UI, centered panel, countdown bar, single-shot Finish, timeout=fail; client budget = min(limit, prep-remaining))
- [x] 5. HotPackMiniGameUI — tap ×Goal, pack reddens/grows
- [x] 6. BuldakMiniGameUI — tap-to-fill gauge (Goal taps = 100%), pot heats up
- [x] 7. ScrewdriverMiniGameUI — 3 screws × Goal clockwise rotations, pointer-angle accumulation (DeltaAngle, CCW ignored), cross-slot visual rotates, per-screw progress dots
- [x] 8. AZGameUI: hub creation + OnFinishedLocal wiring (success → confirm visuals/stack; fail → "try again"), item-click + 준비끝 locked while IsRunning, mini-game items skip optimistic confirm
- [x] 9a. Compile: 0 errors (MCP read_console) — asset patch resolving `MiniGameGoal` confirms new serialization live
- [ ] 9b. MPPM play-test (user): success-queue / fail-retry(no consume) / prep-timeout mid-game / late-result rejection / ready-block
- [x] 10. Docs updated (RECENT_CHANGES recreated post-sync, ACTIVE_CONTEXT, CHANGES)
