# PLAN_004: Core Game Systems Implementation

**Status:** 🔄 In Progress  
**Created:** 2026-07-14  
**Scope:** 코어 게임 시스템 구현 (TurnManager, PlayerState, PlayerInventory, TemperatureSystem, CombatResolver 등)

---

## Context

SYSTEM_ARCHITECTURE.md 설계 완료. 기존 코드는 단순한 Attack/Defend/Charge 3액션 시스템 → Sub/Main 아이템 기반의 서버 권위 턴 루프로 전면 교체 필요.

**역할 분담:**
- **본인(이 플랜):** 코어 게임 시스템 전체
- **팀원:** ItemDataSO 서브클래스 (Attack/Recovery/Sabotage) + SO 에셋 + ExecuteEffect 로직

**기존 코드 상태:**
- `AbsoluteZeroTurnManager.cs` — 완전 교체
- `TurnEnums.cs` — 완전 교체 (TurnPhase 값 변경, ActionType 삭제)
- `AZPlayerVisual.cs` — 수정 (새 PlayerState 참조)
- `AZGameUI.cs` — 리라이트
- Network 인프라 — 변경 없음

---

## Phase 0: Pre-work — 구 코드 정리 + 디렉토리 생성

- [x] 0.1 디렉토리 생성: `Core/Match/`, `Core/Turn/`, `Core/Combat/`, `Core/Item/`, `Core/Item/Data/`, `Core/Buff/`, `Core/Common/`, `ScriptableObjects/Items/`
- [x] 0.2 구 파일 임시 처리: 클래스명 `_OLD` 접미사 (컴파일 유지, Phase 2 후 삭제)
- [x] 0.3 `NetworkConstants.cs` MatchState 값 갱신 (현재 미사용 확인 완료)

---

## Phase 1: 열거형 + 데이터 구조 + 순수 C# 클래스

> 네트워크 의존 없음. 전부 컴파일 가능.

- [x] 1.1 `Core/Common/GameEnums.cs` — TurnPhase : byte (5값)
- [x] 1.2 `Core/Item/ItemEnums.cs` — ItemCategory, ItemSlotType, ItemPersistence, SabotageType, MiniGameType, EffectType, CombatEventType, DamageFilter
- [x] 1.3 `Core/Player/PlayerModifiers.cs` — PlayerModifiers struct + DefenseInfo struct
- [x] 1.4 `Core/Item/ItemSlotNetData.cs` — INetworkSerializable struct (255=unlimited, -1=empty)
- [x] 1.5 `Core/Item/Data/ItemDataSO.cs` — abstract base (팀원 계약)
- [x] 1.6 `Core/Item/Data/DefenseItemDataSO.cs` — CombatResolver 타입 체크용 (우리가 구현)
- [x] 1.7 `Core/Item/Data/ItemContext.cs` — class (FIX-01), ref UserModifiers/TargetModifiers
- [x] 1.8 `Core/Player/ActionQueue.cs` — QueuedAction, Sub/Main 큐잉, Clear
- [x] 1.9 `Core/Combat/TemperatureSystem.cs` — TickFan/TickRecovery/ApplyDamage/ApplyHeal/IsDead/CheckThresholds
- [x] 1.10 `Core/Combat/CombatResult.cs` — CombatResult, CombatEvent, CombatResultData(INetworkSerializable)
- [x] 1.11 `Core/Item/ItemDropTable.cs` — 가중 랜덤 (빈 풀=null)
- [x] 1.12 `Core/Buff/BuffDebuffSystem.cs` — ScheduledEffect, Schedule/ProcessTurnStart/ClearAll

---

## Phase 2: NetworkBehaviour 컴포넌트

- [x] 2.1 `Core/Player/PlayerState.cs` — NV(Temperature/FanSpeed/IsReady/IsFanActive) + ServerRpc 3종 (UseSubItem/SelectMainItem/PressReady) + 6단계 검증
- [x] 2.2 `Core/Player/PlayerInventory.cs` — NetworkList<ItemSlotNetData> Awake() 초기화 (FIX-02), ConsumeItem(255 skip, FIX-03), MakeSlot, ResetForNewRound
- [x] 2.3 `Core/Combat/CombatResolver.cs` — Resolve(DetermineOrder→ApplyDefense→ExecuteMain), Defense 이중적용 방지 (FIX-08)
- [x] 2.4 `Core/Item/ItemManager.cs` — NB, 레지스트리, InitializePlayerInventory, ItemDropTable 생성
- [x] 2.5 `Core/Turn/TurnManager.cs` — 싱글턴, 상태 머신 코루틴(Waiting→Prep→Attack→Resolution→RoundOver), PrepStartServerTime 1회 전송 (FIX-04), 캐시 WaitForSeconds
- [x] 2.6 `Core/Match/MatchManager.cs` — NV(RoundNumber/Wins/MatchState), 데모=단일 라운드
- [x] 2.7 구 코드 삭제 (`_OLD` 파일) + AZPlayerVisual 수정

---

## Phase 3: UI 리라이트 + Dummy Items

- [x] 3.1 `UI/Game/AZGameUI.cs` 전면 리라이트 — 런타임 빌드, 온도 바, 타이머(로컬 계산), 아이템 슬롯(Sub/Main), Ready 버튼, 결과 패널, NV 콜백 구독
- [x] 3.2 `AttackItemDataSO.cs` — 더미 공격 아이템 서브클래스
- [x] 3.3 `RecoveryItemDataSO.cs` — 더미 회복 아이템 서브클래스
- [x] 3.4 `SabotageItemDataSO.cs` — 더미 방해 아이템 서브클래스
- [x] 3.5 `ItemManager.cs` 테스트 모드 추가 — SO 에셋 없이 런타임에 더미 아이템 4종 생성 (Fan/Windbreaker/WarmTea/Cat)
- [x] 3.6 클라이언트 레지스트리 초기화 — AZGameUI에서 ItemManager.InitializeClientRegistry 호출

---

## Phase 4: Scene/Prefab 설정 + 통합 테스트 (수동 — Unity Editor)

- [ ] 4.1 **Player.prefab**: PlayerState + PlayerInventory 컴포넌트 추가
- [ ] 4.2 **GameScene TurnManager GO 교체**: 구 AbsoluteZeroTurnManager 삭제 → 새 TurnManager + MatchManager + ItemManager 추가
- [ ] 4.3 MPPM 또는 ParrelSync로 2인 접속 테스트
- [ ] 4.4 테스트 시나리오: Prep→Attack→Resolution 1턴 순환 확인

---

## 의존 관계

```
Phase 1 (순수 C#):
  GameEnums, ItemEnums → PlayerModifiers → ActionQueue
  ItemSlotNetData
  ItemDataSO(base) → DefenseItemDataSO → ItemContext
  TemperatureSystem, CombatResult, ItemDropTable, BuffDebuffSystem

Phase 2 (네트워크):
  PlayerState ← ActionQueue, ItemContext, TurnManager
  PlayerInventory ← ItemSlotNetData, ItemDataSO
  CombatResolver ← PlayerModifiers, ActionQueue, DefenseItemDataSO
  ItemManager ← ItemDataSO, PlayerInventory, ItemDropTable
  TurnManager ← 위 전부
  MatchManager ← NetworkConstants.MatchState

Phase 3 (UI):
  AZGameUI ← PlayerState, TurnManager, PlayerInventory
  AZPlayerVisual 수정 ← PlayerState

Phase 4 (통합):
  Scene/Prefab 설정 + 테스트
```

---

## 팀원 인터페이스 계약

**우리가 제공:** ItemDataSO 베이스, DefenseItemDataSO, ItemContext, 모든 열거형, TemperatureSystem, PlayerInventory  
**팀원이 구현:** AttackItemDataSO, RecoveryItemDataSO, SabotageItemDataSO + SO 에셋  
**규칙:** ItemDataSO/ItemContext 수정 금지, 온도 변경은 TempSystem 경유, ExecuteEffect 내 ConsumeItem 호출 금지

---

## Will NOT Touch
- Network 인프라 (`Core/Network/` 전체)
- `AZLobbyUI.cs`
- TestUI, Utility 스크립트
- LobbyScene
