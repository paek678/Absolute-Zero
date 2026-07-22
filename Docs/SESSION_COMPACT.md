# Session Compaction — 2026-07-22

## 이번 세션에서 완료된 작업

### 1. 상대 체력바 구분선 (AZGameUI.cs)
- `BuildOppDividerLines(hpBar.transform)` 추가 — 3개 검은 구분선이 상대 HP바 앞으로 보이도록 수정

### 2. 죽음/얼음 애니메이션 수정 (AZPlayerVisual.cs)
- `PlayDeathSequence()`: flash/anim 코루틴 정리 후 "end" 트리거 → DeathRoutine 시작
- `DeathRoutine()`: 1프레임 대기 → freeze → 얼음 깨지면 visualRoot를 (0,-100,0)으로 숨김 → 0.4초 후 복귀 → "end"
- `_waitBreakHide = new(0.4f)` static readonly 추가

### 3. 선풍기 자동 준비 버그 수정 (AZGameUI.cs)
- `OnReadyClicked()`에 `HoverRaycaster.Instance.CurrentHovered != null` 가드 추가
- Physics raycast + GraphicRaycaster 동시 발동 방지

### 4. 방어 애니메이션 분기 (CombatVFXManager.cs)
- `PlayItemSequence`에 `targetDefending` 파라미터 추가
- 방어 아이템 사용 시 PlayDamageFlash 대신 "defence" 애니메이션 재생
- 방어 후 `ReturnToIdle()` 호출

### 5. 히트 파티클 수정 (CombatVFXManager.cs)
- 공격: `if (!isLocalUser)` — 내가 맞을 때만 파티클
- 회복: `if (isLocalUser)` — 내가 회복할 때만 파티클

### 6. 고양이 스프라이트 시퀀스 (CombatVFXManager.cs)
- `PlayCatSpriteSequence(userIdx, targetIdx, isLocalUser)` 구현
- 5단계: sleep → wakeup → jump/jump2 → 포물선 이동 → rummage(좌우흔들기) → 화면밖 퇴장
- 스프라이트 경로: `Resources/Cat/cat_sleep|wakeup|jump|jump2|rummage.png`

### 7. AnimDuration 일괄 변경
- 21개 아이템 SO 전체 AnimDuration → **1.5초** 통일

### 8. HP바 온도 기반 색상 Lerp (AZGameUI.cs)
- `_myHpFillImage` / `_oppHpFillImage` 필드 추가
- `GetTempColor(float temp)` static 메서드:
  - 37~30°: 초록(0.39,0.78,0.31) → 핑크(0.90,0.47,0.59)
  - 30~20°: 핑크 → 하늘색(0.39,0.71,0.92)
  - 20~10°: 하늘색 → 파란색(0.20,0.39,0.86)
  - 10° 이하: 파란색 고정
- `UpdateTempDisplay()`에서 매 프레임 색상 적용

---

## 미완료/보류 작업

### 보류 (유저 지시 대기)
- **Fan 3P 1hit 분리**: CombatVFXManager에서 Fan+3P 특수 케이스 hitCount=1, delay=0.3s (유저가 "아직 하지마" 지시)
- **방어 애니메이션 2회 재생 이슈**: 상대가 defence 2번 보임 (반응 + 본인 아이템) — 유저 미지시

### 아트 리소스 미존재
- `SFX_wind.wav` (CoolBreeze 환경 효과용)
- 4개 아이템 스프라이트: Samgyetang, Mask, Screwdriver, ClawMachine
- CoolBreeze 바람 파티클

### FPS 시스템 누락
- FPS SpriteNames 캐시에 "swing", "feed", "defence" 미등록 → 해당 트리거의 아이템 스프라이트 미표시
- FPS "use" 트리거 → 스프라이트 캐시에 "use" 엔트리 없음 (애니메이션 자체는 작동)

### 기타
- TarotCard "턴 종료 1초 차단" 구현 여부 미확인
- Icebox glow 머터리얼 GUID 누락 가능 (9dfc825aed78fcd4ba02077103263b40)
- 런타임 종합 테스트 미진행

---

## 핵심 코드 구조 참고

### 1P/3P 판별 기준
```
isLocalUser = (int)nm.LocalClientId == userIdx
isTargetOpponent = userIdx != (int)nm.LocalClientId
```

### CombatVFXManager 아이템 시퀀스 플로우
```
PlayCombatVFXSequence → 선공 PlayItemSequence → 후공 PlayItemSequence → 사망 체크
  ↳ PlayItemSequence(userIdx, itemId, nm, targetDefending)
    ↳ FPS 애니메이션 (isLocalUser)
    ↳ 3P 애니메이션 (!isLocalUser)
    ↳ EffectDelay 대기
    ↳ HitCount 만큼 반복: 방어면 defence 애니, 아니면 DamageFlash + HitParticle
```

### 아이템 SO 데이터 위치
- `Assets/Data/Items/Basic/` — Fan, Windbreaker, WarmTea, Cat
- `Assets/Data/Items/Random/{Attack,Buff,Debuff,Defense,Recovery,Sabotage,Special}/`

### 현재 아이템 AnimTrigger 매핑
| AnimTrigger | 사용 아이템 |
|-------------|------------|
| swing | Fan |
| defence | Windbreaker |
| use | WarmTea, Soda, HotAmericano, HotPack, Smartphone, Screwdriver, ClawMachine |
| fan | HandFan |
| gun | WaterGun |
| feed | IceCream, IcedAmericano, Samgyetang |
| hug | HugTshirt |
| eat | BuldakNoodles |
| tape | BlueTape |
| card | RedCard, TarotCard |
| (없음) | Cat (스프라이트 시퀀스) |

---

## 다음 세션 시작 프롬프트

```
이전 세션(2026-07-22) 이어서 작업합니다.
Docs/SESSION_COMPACT.md 읽고 컨텍스트 복원해줘.
```
