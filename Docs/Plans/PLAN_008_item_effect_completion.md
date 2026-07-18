# PLAN_008: Item Effect Full Application

**Status:** ✅ Complete  
**Created:** 2026-07-17  
**Updated:** 2026-07-17 (Q&A answers integrated)

---

## Q&A Answers Summary — Changes Required

| Q# | Topic | Answer | Code Impact |
|----|-------|--------|-------------|
| Q2 | Warm Tea uses | 1회 (모든 소모 아이템 1회) | **WarmTea.asset MaxUses 2→1** |
| Q3 | Cat uses | 1회 | Already correct |
| Q9 | Tarot Card | 사용이 아이템 사용 카운트 X + 상대 공개 + 추가 사용 1회 | **Major: free Sub + reveal + extra selection** |
| Q18 | Smartphone | 3회 사용 특수 예외 | **Smartphone.asset MaxUses 1→3** |
| Q19 | Hug T-shirt risk | 의도된 리스크 | No change |
| Q20 | Round reset | 온도37°/기본리필/랜덤4개 재지급/버프초기화 | **StartNextRound: grant 4 random items** |
| Q21 | Stacking | 버프디버프 중첩O, 선풍기강화 중첩X | **FanSpeed upgrade: prevent stacking** |
| Q23 | Delayed vs defense | 방어 무시 | Already correct (BuffSystem applies raw) |

---

## Implementation Tasks

### Phase 1: SO Asset Fixes (Data)
- [ ] 1.1 **WarmTea.asset** — MaxUses: 2→1 (MCP or manual)
- [ ] 1.2 **Smartphone.asset** — MaxUses: 1→3 (3회 사용 특수 예외, HealPerUse=[3,5,7] already correct)

### Phase 2: FanSpeed Upgrade System (Q21 + Issue #4)
**Problem:** Screwdriver sets FanSpeed=2, but (a) no revert to 1, (b) can stack
**Design answer:** 선풍기 강화는 isUpgrade bool로 중첩 방지

- [ ] 2.1 **PlayerState** — add `NetworkVariable<bool> IsFanUpgraded` (server-write)
- [ ] 2.2 **BuffDebuffSystem.ApplyEffect** — FanSpeedChange: check `IsFanUpgraded`, skip if already true. Set `IsFanUpgraded = true` when applied
- [ ] 2.3 **TurnManager.PrepPhaseRoutine** — at phase END (before AttackPhase), if `IsFanUpgraded == true`: reset `FanSpeed = 1`, `IsFanUpgraded = false`. Screwdriver effect lasts exactly 1 PrepPhase
- [ ] 2.4 **TurnManager.StartNextRound** — reset `FanSpeed = 1`, `IsFanUpgraded = false`

### Phase 3: Round Reset — Random Item Grant (Q20)
**Design answer:** 랜덤 아이템은 라운드 시작 시 4개 재지급

- [ ] 3.1 **TurnManager.StartNextRound** — after `ResetForNewRound()`, call `GrantRandomItems(4, GetDropTable())` for both players
- [ ] 3.2 **PlayerInventory.ResetForNewRound** — verify it clears random slots (already does ✅), resets thresholds (already does ✅)

### Phase 4: Tarot Card System (Q9) — Most Complex
**Design answer:**
1. 타로카드 사용은 아이템 사용으로 카운트하지 않는다
2. 사용 후 상대방 선택 아이템 공개
3. 공개 후 아이템 사용 한 번 더 가능 (카운터 가능)

**Current:** TarotCard is Sub slot, sets `OpponentRevealed = true`. `hasUsedSub` prevents second Sub.

**Implementation approach:**
- [ ] 4.1 **ItemDataSO** — add `bool IsFreeAction` field (default false). TarotCard.asset sets true
- [ ] 4.2 **PlayerState.SelectItemServerRpc** — if item has `IsFreeAction`, do NOT set `hasUsedSub = true`. Execute reveal immediately (not queued to AttackPhase)
- [ ] 4.3 **ActionQueue** — add `bool tarotUsed` flag. When Tarot used: `tarotUsed = true` but `hasUsedSub` stays false (allows another Sub)
- [ ] 4.4 **Reveal RPC** — new `RevealOpponentItemClientRpc(byte targetClientIdx, short itemId)` on TurnManager. Sent only to the Tarot user. Called immediately when Tarot is used during PrepPhase (not at AttackPhase)
- [ ] 4.5 **Reveal logic** — when Tarot activates:
  - Server checks if opponent `HasSelectedItem.Value == true`
  - If yes: send opponent's selected itemId to Tarot user via RPC
  - If no: send -1 (opponent hasn't selected yet — "상대 미선택")
  - Tarot gets consumed either way
- [ ] 4.6 **AZGameUI** — receive reveal RPC, show opponent's item name as floating text or panel overlay near opponent area. Auto-hide after 5s or on phase change
- [ ] 4.7 **TarotCard.asset** — add `IsFreeAction: 1`

**Flow:**
```
PrepPhase → Player selects Tarot (Sub slot)
  → Server: IsFreeAction=true, so hasUsedSub stays false
  → Server: immediately checks opponent's selection
  → Server: sends reveal RPC to Tarot user only
  → Client: shows opponent's item (or "미선택")
  → Player can still select Main item AND another Sub item
  → Ready button → AttackPhase proceeds normally
```

### Phase 5: Latent Bug Fixes
- [ ] 5.1 **CombatResolver.ExecuteMain** — add `DropTable = GetDropTable()` to ItemContext (line ~130). Currently missing, would NullRef if a Main-slot item accesses DropTable
- [ ] 5.2 **CombatResolver.ExecuteMain** — add `ActiveEnvironment` to ItemContext (prep for environment system)

### Phase 6: Verification
- [ ] 6.1 WarmTea — confirm 1회 사용 후 소멸
- [ ] 6.2 Smartphone — confirm 3회 사용, 3→5→7 회복량
- [ ] 6.3 Screwdriver — FanSpeed 2로 변경, 1 PrepPhase 후 복귀
- [ ] 6.4 Screwdriver 중첩 — 2회 연속 사용 시 2번째 무시 확인
- [ ] 6.5 Tarot Card — 상대 아이템 공개 + 추가 사용 가능 확인
- [ ] 6.6 Round reset — 온도 37, 랜덤 4개 재지급 확인
- [ ] 6.7 Samgyetang 지연효과 — 다음 턴 방어 아이템 무시하고 -7 적용 확인

---

## Files to Modify

| File | Change | Phase |
|------|--------|-------|
| `Data/Items/Basic/WarmTea.asset` | MaxUses 2→1 | 1 |
| `Data/Items/Random/Recovery/Smartphone.asset` | MaxUses 1→3 | 1 |
| `Data/Items/Random/Special/TarotCard.asset` | Add IsFreeAction=1 | 4 |
| `Core/Item/Data/ItemDataSO.cs` | Add `bool IsFreeAction` field | 4 |
| `Core/Player/PlayerState.cs` | Add `IsFanUpgraded` NV + Tarot free-action logic in SelectItemServerRpc | 2,4 |
| `Core/Player/ActionQueue.cs` | Add `tarotUsed` flag | 4 |
| `Core/Buff/BuffDebuffSystem.cs` | FanSpeed stacking prevention (check IsFanUpgraded) | 2 |
| `Core/Turn/TurnManager.cs` | FanSpeed revert in PrepPhase + random grant in StartNextRound + Reveal RPC | 2,3,4 |
| `Core/Combat/CombatResolver.cs` | Add DropTable + ActiveEnvironment to ItemContext | 5 |
| `UI/Game/AZGameUI.cs` | Reveal display UI | 4 |

## Will NOT Touch
- AttackItemDataSO, DefenseItemDataSO, RecoveryItemDataSO, BuffItemDataSO, DebuffItemDataSO, SabotageItemDataSO — all effects correctly implemented
- SpecialItemDataSO — FanSpeedChange and RevealOpponent logic correct, only PlayerState/TurnManager need system support
- PlayerInventory — consume/grant/steal/reroll all working
- ItemDropTable — weighted roll working
- Mini-game system — separate plan

## Deferred
- **Mini-game system** — all 9 mini-game items marked RequiresMiniGame but system not built. Separate plan
- **ExtraAction** — merged into Tarot Card design (Q9). No separate ExtraAction system needed
- **Item slot UI 4→12** — Q16, separate UI task
- **Drop rate rebalancing** — Q7, designer handling in spreadsheet
