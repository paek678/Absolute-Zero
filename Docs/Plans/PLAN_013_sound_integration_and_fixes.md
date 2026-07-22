# PLAN_013: Sound System Integration + Remaining Fixes

**Status:** ✅ Complete
**Date:** 2026-07-22

---

## Phase 1: Quick Fixes (코드/에셋)

- [x] 1-1. Fan.asset OpponentAnimTrigger: "swing" → "attack"
  - **Why:** 부채로 공격받으면 상대가 "swing"(부채 휘두르기)을 재생 — 맞아야 하므로 "attack"이 맞음
  - 유저가 직접 지적함: "부채로 다맞으면 왜 한번 떄리냐 그냥 맞기만 해야지"

- [x] 1-2. ItemDataSO.cs:48 CanUse() 서버사이드 검증 수정
  - **Before:** `Persistence != ItemPersistence.RandomConsumable` (Permanent + BasicConsumable 모두 차단)
  - **After:** `SlotType == ItemSlotType.Main` (Main 아이템만 차단)
  - **Why:** 클라이언트 사이드(ItemWorldDisplay.CanSelectItem)는 이미 SlotType으로 체크. 서버사이드도 일치시켜야 함

## Phase 2: MCP Scene Verification

- [x] 2-1. GameScene 계층구조 확인
  - GameManager (TurnManager, MatchManager, ItemManager) ✅
  - CombatVFXManager (HitEffect, IceBreakEffect, FinalBreakEffect 프리팹 연결) ✅
  - EnvironmentVFXManager ✅
  - IceboxController (Icebox 프리팹 연결) ✅
  - EnemyPlayer (Animator, SortingGroup, body/lowerbody/item) ✅
  - SpawnPoints (12 PlayerItem + 12 EnemyItem + StayItems + BoxSpawnPoint) ✅
  - Main Camera (CameraShake) ✅ — FPS는 런타임 생성
  - **결론: 추가 씬 작업 불필요**

- [x] 2-2. LobbyScene Managers 확인
  - NetworkManager, RelayManager, LobbyManager, SessionManager, PlayerSpawnManager ✅
  - GameAudioManager는 AZGameUI.Start()에서 런타임 생성 (DontDestroyOnLoad)

## Phase 3: Sound System Integration (이전 세션에서 완료)

- [x] 3-1. GameAudioManager.cs 생성 (28 clips, 5 AudioSources)
- [x] 3-2. CombatVFXManager — 아이템 SFX + 피격음
- [x] 3-3. AZPlayerVisual — freeze/icebreak SFX
- [x] 3-4. HoverEffect — 호버 사운드
- [x] 3-5. IceboxController — 박스 오픈 SFX
- [x] 3-6. EnvironmentVFXManager — 잼민이 훔치기 SFX
- [x] 3-7. AZGameUI — BGM, 부채 루프, 시계 틱, 환경 SFX, 버튼 클릭
- [x] 3-8. Attack Phase Timing Summary Log

---

## 예상 오류 분석

### 🔴 Critical (게임 플레이 영향)

| # | 오류 | 원인 | 해결 방안 | 우선순위 |
|---|------|------|----------|---------|
| E-01 | **시계 틱 ↔ 환경 SFX 충돌** | `_envSource`를 시계 틱과 환경 루프(매미 등)가 공유. 시계 틱 시작 시 매미 루프가 끊기고, 시계 틱 종료 후 매미가 복구 안 됨 | 시계 틱 전용 AudioSource 분리, 또는 시계 틱 종료 시 환경 루프 복구 | 높음 |
| E-02 | **부채 루프 + 라운드 종료** | PrepPhase에서 부채 루프 시작 → 온도 0도 도달로 직접 HandleRoundEnd 진입 시 AttackPhase를 안 거쳐서 StopFanLoop 안 불림 | `OnPhaseChanged`에서 `RoundOver` 케이스에도 StopFanLoop 추가 | 높음 |

### 🟡 Medium (UX 품질)

| # | 오류 | 원인 | 해결 방안 | 우선순위 |
|---|------|------|----------|---------|
| E-03 | **피격음 연속 재생 겹침** | 다타격 아이템(부채 HitCount=3, Interval=0.2s)에서 PlayDamaged()가 0.2초 간격으로 3번 불림 → SFX_damaged 오디오 겹침 | PlayOneShot이므로 겹침은 의도된 동작이지만, 볼륨 과다 가능. 필요시 hitCount > 1일 때 볼륨 감소 | 낮음 |
| E-04 | **BGM 재시작 안 됨** | 디스커넥트 후 재접속 시 GameAudioManager가 DontDestroyOnLoad로 살아있어 BGM 이미 재생 중이므로 PlayBGM()이 early return | 의도된 동작. 단, BGM이 멈춘 상태였다면 문제 → StopBGM 호출 지점 확인 필요 | 낮음 |
| E-05 | **호버 사운드 밴 아이템** | 청테이프로 금지된 Main 아이템에도 마우스 올리면 호버 사운드 재생 | 금지 아이템도 호버 가능하되 클릭만 불가능이 맞는 UX. 또는 SetHovered에서 밴 상태 체크 | 무시 가능 |

### 🟢 Low (마이너/비기능)

| # | 오류 | 원인 | 해결 방안 | 우선순위 |
|---|------|------|----------|---------|
| E-06 | **`new WaitForSeconds()` GC** | CombatVFXManager에서 ItemDataSO의 가변 duration으로 `new WaitForSeconds()` 사용 (CLAUDE.md 규칙 6 위반) | SO 값이 가변이라 캐시 불가. GC 영향 미미 (전투당 ~5회) | 무시 가능 |
| E-07 | **오디오 클립 미발견** | Resources.Load 실패 시 `[Audio] Clip not found:` 경고 | 28개 파일 모두 확인 완료. 경고 로그로 디버깅 가능 | 해결 완료 |
| E-08 | **환경 SFX 라운드 간 지속** | 매미 루프가 라운드 전환 시 자동으로 안 꺼질 수 있음 | StopEnvironment()가 새 환경 발표 시 호출됨. 라운드 리셋 시에도 호출 필요할 수 있음 | 낮음 |

---

## Phase 4: Critical 오류 수정

- [x] 4-1. E-02 수정: OnPhaseChanged에서 RoundOver/WaitingForPlayers 시 StopFanLoop
- [x] 4-2. E-01 수정: 시계 틱 전용 AudioSource 추가 (_clockSource)

## Discovered Issues
(작업 중 발견된 추가 이슈 기록용)

