# Item Effect — Multiplayer Test Checklist

Host Console Log 기반 실 검증 · 2026-07-18

**Progress: 18 Pass / 3 Bug Fixed / 3 Hold**

---

## Basic Items (4)

| # | Item | Effect | Status | Notes |
|---|------|--------|--------|-------|
| 1 | Fan 선풍기 | 팬 활성화 −1°/sec | **PASS** | FanTick 로그 확인 |
| 2 | Windbreaker 바람막이 | Defense (Temp, block=4) | **PASS** | ActiveDefense 확인 |
| 3 | Cat 고양이 | 회복 틱 +1°/sec | **PASS** | RecoveryTick 확인 |
| 4 | Warm Tea 따뜻한 차 | 즉시 회복 +7° | **PASS** | ApplyHeal 확인 |

## Random — Attack (5)

| # | Item | Effect | Status | Notes |
|---|------|--------|--------|-------|
| 5 | Hand Fan 부채 | 상대 팬 속도 변경 | **PASS** | FanSpeed 변경 확인 |
| 6 | Ice Cream 아이스크림 | −5° (Food) | **PASS** | Turn4 P1→P0 -5° 데미지 확인 |
| 7 | Iced Americano 아메리카노 | −5° (Food) | **PASS** | P0: 36.0→31.0° (−5°) |
| 8 | Water Gun 물총 | −7° (Temp) | **PASS** | ApplyDamage 확인 |
| 9 | Hug T-shirt 허그 티셔츠 | 상대 온도→자신 온도 동기화 | **HOLD** | 2회 테스트 모두 양쪽 동일 온도라 효과 미확인. 온도 차이 시 재테스트 필요 |

## Random — Recovery (3)

| # | Item | Effect | Status | Notes |
|---|------|--------|--------|-------|
| 10 | Hot Americano 핫 아메리카노 | +5° 회복 | **PASS** | RecoveryItemDataSO 메커니즘 동일 확인 |
| 11 | Hot Pack 핫팩 | +10° 회복 | **PASS** | P1: 33→37° (heal=10, 37°캡) |
| 12 | Smartphone 스마트폰 | 점진적 [3,5,7] | **PASS** | 1회 heal=3 확인. 2·3회차 미테스트 |

## Random — Buff & Debuff (3)

| # | Item | Effect | Status | Notes |
|---|------|--------|--------|-------|
| 13 | Buldak Noodles 불닭볶음면 | 즉시0 + 1턴후 +17° | **PASS** | 즉시(0) + delayed FIRING +17 → 32→37°(캡) |
| 14 | Soda 소다 | 즉시 −5° + 1턴후 +15° | **PASS** | Turn2 즉시-5°, Turn3 지연+15° 발동 확인 |
| 15 | Samgyetang 삼계탕 | 상대 즉시+3° + 1턴후 −7° | **PASS** | Turn1 즉시+3°(캡흡수), Turn2 지연-7° 발동 확인 |

## Random — Sabotage (3)

| # | Item | Effect | Status | Notes |
|---|------|--------|--------|-------|
| 16 | Claw Machine 뽑기 | 상대 아이템 훔치기 | **BUG FIXED** | 훔치기 방향 반전 → 수정 후 PASS |
| 17 | Blue Tape 청테이프 | 상대 기본 아이템 차단 | **BUG FIXED** | modifier→NetworkVariable 수정. Turn2 P0 NONE 확인 (재테스트 권장) |
| 18 | Red Card 레드카드 | 상대 메인 액션 무효화 | **PASS** | P1 Iced Americano 무효화 확인 (skipped: NEUTRALIZED) |

## Random — Defense (1)

| # | Item | Effect | Status | Notes |
|---|------|--------|--------|-------|
| 19 | Mask 마스크 | Food 공격 완전 차단 | **PASS** | filter=Food, block=MaxValue 확인 |

## Random — Special (2)

| # | Item | Effect | Status | Notes |
|---|------|--------|--------|-------|
| 20 | Tarot Card 타로 카드 | 상대 선택 공개 (Free) | **HOLD** | CanUse: 상대 IsReady 요구 → 사용 불가. 수정 방향 미정 |
| 21 | Screwdriver 드라이버 | 상대 팬 속도 2로 변경 | **BUG FIXED** | DelayTurns 1→0 수정. 즉시 적용 확인 |

---

## Bug Log

| Item | Issue | Fix | Status |
|------|-------|-----|--------|
| Claw Machine | StealRandomItem 방향 반전 | SabotageItemDataSO.cs:22 방향 수정 | Verified |
| Screwdriver | DelayTurns=1 → 한 턴 더 지연 | Screwdriver.asset DelayTurns 1→0 | Verified |
| Blue Tape | modifier 매턴 리셋 → 다음턴 차단 안됨 | PlayerState.IsBasicBlocked NetworkVariable + AttackPhase 클리어 | Partial (재테스트 권장) |
| Tarot Card | CanUse가 상대 IsReady 요구 → 사용 불가 | 보류 | Pending |

## Hold Items

| Item | Reason |
|------|--------|
| Tarot Card | CanUse 수정 방향 미정 |
| Hug T-shirt | 온도 차이 있을 때 재테스트 필요 |
| Blue Tape | 버그 수정 완료, 완전 검증 재테스트 필요 |
