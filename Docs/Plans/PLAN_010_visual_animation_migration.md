# PLAN_010 — Visual & Animation Migration (SampleScene → GameScene)

> **Status:** ✅ Complete
> **Created:** 2026-07-20
> **Source:** `Assets/Scenes/SampleScene.unity` (gameGem project import)
> **Target:** `Assets/Scenes/GameScene.unity`

---

## Overview

SampleScene(gameGem)의 2D 스프라이트 캐릭터, 배경 환경, 애니메이션, 파티클 이펙트, UI 비주얼을 GameScene에 적용한다.
현재 GameScene은 캡슐 캐릭터 + 16개 바닥 타일 + 런타임 빌드 UI만 존재.

## Source Assets (gameGem)

| Category | Assets | Path |
|----------|--------|------|
| Player Sprites | 29 sheets (player_*.png) | `Art/gameGem/MainFolder/Sprite/` |
| Player SpriteLib | PlayerSpriteLib.spriteLib | `Art/gameGem/MainFolder/Sprite/` |
| Player Prefab | player.prefab | `Art/gameGem/MainFolder/Sprite/` |
| Player Animator | playerA.controller (20 clips) | `Art/gameGem/MainFolder/Animations/` |
| Object Anims | coolerA, catA, teaA, TimeAlarmA controllers | `Art/gameGem/New Folder/animations/` |
| Particle Prefabs | HitEffect, IceBreakEffect, FinalBreakEffect | `Art/gameGem/MainFolder/Prefabs/` |
| Background Sprites | background.png, pillar2.png, objtest1.png | `Art/gameGem/MainFolder/Sprite/`, `New Folder/` |
| Floor Material | tileMat, tileMat 1, tileMat 2, floorTile.png | `Art/gameGem/MainFolder/Materials/`, `New Folder/` |
| UI Sprites | UIsprite1.png | `Art/gameGem/MainFolder/Sprite/` |
| Environment Sprites | kid_idle/rob/skew, rescue_ready/save, freeze1~3 | `Art/gameGem/MainFolder/Sprite/` |
| 3D Sprite Material | sprite3DMat.mat | `Art/gameGem/MainFolder/Materials/` |

## Will NOT Touch
- Network logic (PlayerState, TurnManager, CombatResolver — server-authoritative)
- Item SO data (Data/Items/*.asset)
- Lobby system (LobbyScene, LobbyManager, AZLobbyUI)
- MiniGame system (MiniGameHub, BuldakMiniGameUI, ScrewdriverMiniGameUI)

---

## Phase 1: Background Environment (Easy)

> 현재 Ground×16 타일 → 정자(pavilion) 구조로 교체

- [x] **1-1.** BackGround 프리팹 생성 ✅
  - SampleScene의 BackGround 오브젝트를 프리팹으로 저장
  - 경로: `Assets/Prefabs/Environment/BackGround.prefab` (floor, pillar, roof×2, background×2)
- [x] **1-2.** GameScene에 BackGround 프리팹 배치 ✅
  - Environment/BackGround로 배치, pos (0.46, -0.5, 27.44) → 바닥 표면 Y=0 정렬
  - GroundPool 비활성화 (보존)
- [x] **1-3.** 조명 조정 ✅
  - Directional Light 이미 SampleScene과 동일 (rot 50,330, intensity 2, 5000K)
  - Spot Light 기존 무대 조명 유지 (pos 0.23,8.58,3.5, 하향 90°, intensity 200)
- [x] **1-4.** 카메라 앵글 미세 조정 ✅
  - 카메라 (0,3.5,-4), FOV 67, rot 16° — 정자 구조 자연스럽게 프레이밍됨, 조정 불필요
- [x] **1-5.** 아이템 마커 위치 확인 ✅
  - 마커 Y=0.30, 바닥 표면 Y=0 → 마커가 바닥 위 0.30 높이에 위치, 정상 동작

---

## Phase 2: Particle Effects (Easy)

> 프리팹 3종 배치 + 활성화 로직

- [x] **2-1.** 이펙트 프리팹 GameScene에 배치 ✅
  - CombatVFXManager GO 생성, Start()에서 프리팹 Instantiate (비활성 상태 유지)
  - 런타임에 플레이어 위치로 이동 후 Play()
- [x] **2-2.** VFX 매니저 스크립트 작성 ✅
  - `Assets/Scripts/Core/Combat/CombatVFXManager.cs`
  - 싱글톤, SerializeField 프리팹 3개, Start()에서 인스턴스 생성
  - `PlayHitAt()`, `PlayIceBreakAt()`, `PlayFinalBreakAt()`
- [x] **2-3.** TurnManager 연결 ✅
  - `TurnManager.OnCombatResult` 이벤트 구독
  - CombatResultData에서 이벤트 타입/소스/타겟 파싱 → 해당 플레이어 위치에 이펙트 재생
  - MainEffect → HitEffect, Death → FinalBreakEffect
- [x] **2-4.** FreezeObject 스프라이트 단계 표시 ✅ (→ Phase 6-5에서 구현)
  - freeze1/2/3.png 활용 — 온도 구간별 동결 단계 시각화
  - AZPlayerVisual에서 FreezeObject 자동 생성 + 온도 기반 스프라이트 전환

---

## Phase 3: Player Character Upgrade (Major)

> AZPlayerVisual (캡슐) → 2D 스프라이트 조립 캐릭터 + Animator

- [x] **3-1.** Player 프리팹 구조 설계 ✅
  - 기존 Player.prefab 하위에 gameGem player.prefab을 nested prefab으로 추가
  - 이름: `CharacterVisual` (Animator + 스프라이트 조립체 포함)
- [x] **3-2.** Player.prefab에 스프라이트 조립체 통합 ✅
  - `CharacterVisual` nested prefab: body(arm1, arm2, head(frontHair×3, backHair)), lowerbody, item
  - SpriteRenderer 10개, SpriteResolver + SpriteLibrary 보존
  - SortingGroup 추가
- [x] **3-3.** Animator Controller 연결 ✅
  - playerA.controller 자동 연결됨 (nested prefab에서 상속)
  - 20개 애니메이션 클립 사용 가능
- [x] **3-4.** AZPlayerVisual 리팩터링 ✅
  - `transform.Find("CharacterVisual")` → Animator + SpriteRenderer[] 수집
  - `PlayAnimation(string triggerName)` — 범용 애니메이션 트리거
  - `PlayDamageFlash()` — PlayerTest.cs 패턴 통합 (_FlashAmount 셰이더)
  - 온도 시각화: 전체 SpriteRenderer에 color tint lerp 유지
- [x] **3-5.** SpriteRenderer 배열 + Material 캐시 ✅
  - `GetComponentsInChildren<SpriteRenderer>(true)` 10개 수집
  - `material` 인스턴스 캐싱, `SetFlashAmount()` 일괄 적용
- [x] **3-6.** 네트워크 동기화 ✅
  - CombatVFXManager → AZPlayerVisual.PlayDamageFlash() + PlayAnimation() 클라이언트 로컬 호출
  - Phase 4에서 CombatResultData 기반 AnimTrigger 시스템으로 구현 완료
- [x] **3-7.** Player 프리팹 등록 확인 ✅
  - DefaultNetworkPrefabs.asset → Player (Assets/Prefabs/Player.prefab) 정상 유지
  - NetworkObject 컴포넌트 무결성 확인됨

---

## Phase 4: Item Action Animation (Medium)

> AttackTurn 결과에 따른 아이템별 애니메이션 재생

- [x] **4-1.** 애니메이션-아이템 매핑 테이블 정의 ✅
  - 21개 아이템 전부 AnimTrigger 매핑 완료
  - 미보유 에셋(WaterGun, ClawMachine, BlueTape) → "button" 대체
- [x] **4-2.** ItemDataSO에 AnimationTrigger 필드 추가 ✅
  - `AnimTrigger` (내가 사용 시) + `OpponentAnimTrigger` (상대 사용 시 상대 반응)
  - 21개 SO 에셋에 MCP execute_code로 일괄 설정
  - RedCard: AnimTrigger="card", OpponentAnimTrigger="disappoint"
- [x] **4-3.** CombatVFXManager에 아이템 애니메이션 통합 ✅
  - OnCombatResult → P1/P2MainItemId로 ItemDataSO 조회 → AnimTrigger 발동
  - 로컬/상대 분기: 로컬=AnimTrigger, 상대=OpponentAnimTrigger (없으면 AnimTrigger 사용)
  - cooler/tea/cat 오브젝트 연출은 추후 Phase로 분리 (아트 연출 단계)
- [x] **4-4.** 1인칭/3인칭 분기 ✅
  - CombatVFXManager.TriggerItemAnimation에서 isLocal 분기 구현
  - 피격 플래시: TriggerDamageFlash로 타겟 플레이어에 적용
- [x] **4-5.** 연출 타이밍 ✅
  - 아이템 애니메이션 트리거 → 0.3초 대기 → 파티클/피격 이펙트 순서
- [x] **4-6.** 미보유 애니메이션 아이템 처리 ✅
  - WaterGun, ClawMachine, BlueTape → "button" 대체 설정
  - SO의 AnimTrigger 필드만 변경하면 추후 교체 가능

---

## Phase 5: UI Visual Enhancement (Medium)

> 런타임 빌드 UI의 비주얼 업그레이드

- [x] **5-1.** HPBar 비주얼 교체 ✅
  - Slider 컴포넌트 기반으로 전환 (UIsprite1 스프라이트 전체 적용)
  - Background(UIsprite1_5), Fill(UIsprite1_4), Outline(UIsprite1_3), Icon(UIsprite1_2 온도계)
  - GiftLine 마커 3개 (UIsprite1_21 라인 + UIsprite1_27/23/22 아이콘)
  - GameSprites 확장: UIsprite1 + attacktime 스프라이트 로딩 추가
- [x] **5-2.** Timer 비주얼 강화 ✅
  - 원형 타이머: Radial360 fill (UIsprite1_7) + 시곗바늘(UIsprite1_11) 회전
  - Timer outline(UIsprite1_6), line(UIsprite1_14) 장식
  - TimeAlarm: 잔여시간 ≤5초 시 attacktime_0 스프라이트 표시 + 셰이크 코루틴
  - 타이머 텍스트 빨간색 전환 (5초 이하)
- [x] **5-3.** EnemyCanvas World Space 업그레이드 ✅
  - Slider 기반 HPBar (동일한 UIsprite1 스프라이트)
  - scale 0.007, 온도계 아이콘 포함
- [x] **5-4.** 버튼 비주얼 유지 ✅
  - Ready 버튼은 기존 objtest1_20 스프라이트 유지 (SampleScene hitBtn/freezeBtn은 게임 메커니즘과 다름)
  - WaitForSeconds 캐시 정리 (HideResultAfterDelay GC fix)

---

## Phase 6: Environment VFX (Easy)

> 환경변수 연출용 스프라이트 + 오브젝트

- [x] **6-1.** 잼민이(Kids) VFX ✅
  - kid_idle 스프라이트 2개 배치 (좌 -3,0.5,6 / 우 3.5,0.5,7, 좌우반전)
  - Kids 환경 시 활성화, 종료 시 비활성화
  - rob/skew 스프라이트는 추후 애니메이션 연출 시 활용 (Resources에 복사 완료)
- [x] **6-2.** 앰뷸런스(Ambulance) VFX ✅
  - rescue_ready 스프라이트 배치 (4,0.5,8)
  - Ambulance 환경 시 활성화
  - rescue_save는 추후 회복 연출 시 활용 (Resources에 복사 완료)
- [x] **6-3.** 날씨 환경 VFX ✅
  - SunnyDay: Directional Light → 따뜻한 노란색 (1,0.95,0.7), 강도 3
  - CoolBreeze: Directional Light → 시원한 파란색 (0.8,0.9,1), 강도 1.8
  - HeatWaveWarning: Directional Light → 붉은색 (1,0.6,0.5), 강도 2.5
  - 모든 전환 1.5초 Lerp 애니메이션
  - SummerVacation: TimeAlarm과 연동 (PrepDuration이 10초로 줄어들어 자연스럽게 알람 발동)
- [x] **6-4.** 환경 VFX 매니저 ✅
  - `EnvironmentVFXManager.cs` 생성, GameScene에 GO+Component 배치
  - OnEnvironmentAnnounced 이벤트 구독
  - ResetVisuals()로 매 라운드 초기화
- [x] **6-5.** FreezeObject 스프라이트 단계 표시 ✅ (Phase 2-4 에서 이관)
  - AZPlayerVisual에 freeze1/2/3 스프라이트 로드 + FreezeObject 자식 생성
  - 온도 ≤25° → freeze1, ≤15° → freeze2, ≤5° → freeze3
  - Resources에 freeze1/2/3.png 복사 완료

---

## Discovered Issues
(작업 중 발견된 이슈 기록용)

---

## Dependencies & Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Player 프리팹 변경 시 NetworkObject 직렬화 깨짐 | 게임 접속 불가 | 기존 프리팹 백업 후 작업, NetworkObject 컴포넌트 유지 |
| SpriteLib GUID 불일치 | 스프라이트 표시 안됨 | 프리팹 내 참조 수동 재연결 |
| 애니메이션 시퀀스로 턴 지연 | 게임 페이스 느려짐 | 타임아웃 설정, 스킵 가능 옵션 |
| 아이템 마커 위치 변경 시 기존 클릭 영역 깨짐 | 아이템 선택 불가 | Phase 1에서 마커 위치 조정 후 즉시 테스트 |
| sprite3DMat 중복 (Resources/ vs Art/gameGem/) | 렌더링 불일치 | 하나로 통일, 참조 정리 |
