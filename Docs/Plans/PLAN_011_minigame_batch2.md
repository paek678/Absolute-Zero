# PLAN_011 — Mini-Game Batch 2 (물총 / 집게손 / 청테이프 / 스마트폰) + Quality Pass

> Status: ✅ Complete (pending user play-test)
> Branch: `feature/minigame-batch2` (branched from main after PR #2 merge)
> Approved 2026-07-18 ("ㄱㄱ") — spec table from user, existing pipeline reused (server start/end judgment, client greybox, WarioWare banner, SO-driven params).
> Extended 2026-07-20 — user: overall quality too rough + "쏘아라" banner not showing → quality pass across all 7 games. Mid-work direction: success-moment visual feedback (like screwdriver's green border) matters most; decorative polish is secondary since real sprites will be overlaid later.

## Judgment interpretations (stated in approved plan)
- 집게손: one attempt — miss = instant fail. Tolerance = claw center within doll width.
- 물총: tap targets directly, missed taps no penalty, timeout only fail. Goal = target count (3).
- 청테이프: cursor ping-pong, green zone random position — tap outside green = instant fail (per spec).
- 스마트폰: wrong dot = instant fail; pointer release before complete = chain reset (retry within time); patterns ㄱ/ㄴ/Z random.

## Checklist
- [x] 1. WaterGunMiniGameUI (쏴라!) — Goal개 과녁 랜덤 벡터+벽 반사 이동, 포인터 다운 즉시 명중(빠른 과녁 대응)
- [x] 2. ClawGrabMiniGameUI (잡아라!) — 집게 왕복(핑퐁) → 탭 → 하강 → 허용오차 판정 → 잡으면 인형 붙여 상승/미스 즉시 실패
- [x] 3. TapeCutMiniGameUI (끊어라!) — 테이프 늘어남 연출 + 커서 왕복 + 초록 영역 랜덤 위치, 1회 판정
- [x] 4. PatternUnlockMiniGameUI (그려라!) — ㄱ/ㄴ/Z 랜덤, 상단 정답 프리뷰(선 포함), 드래그 스냅 연결, 틀린 점 즉시 실패, 손 떼면 체인 리셋
- [x] 5. MiniGameHub에 4타입 연결 + DebugItemGranter 복원(F1~F8) — 병합 때 삭제되어 스택 모델에 맞춰 재작성 (`PlayerInventory.GrantSpecificItem` 재추가: 보유 시 횟수 누적)
- [x] 6. Compile 0 errors. 에셋 패치 불필요 — 파트너가 4종 타입/시간 이미 세팅(WaterGun Goal=3 포함, 나머지 Goal은 코드 기본값 1)
- [x] 7. Docs + staged (branch `feature/minigame-batch2` — main에서 분기, 커밋/PR은 사용자)

## Achievement-Feedback Pass (2026-07-20, user-directed; decorative pass rolled back on user order)
- [x] 8. Base rewrite (`MiniGameUIBase`) — banner readability band (fixes "쏘아라" invisible over red targets), result outro ("성공!/실패…" pop — server submit stays immediate, visual only), timer line yellow→red + last-1.5s pulse, `CreateCircle`/`Finished` helpers
- [x] 9. `MiniGameArt` — procedural soft-edge circle sprite (confirm-pop ring + circle elements, tint via Image.color)
- [x] 10. **Common achievement feedback**: `PlayConfirmPop` (green expanding ring at the achievement position; red variant for miss) — applied to every game per user's "screwdriver green border" principle
- [x] 11. Per-game feedback only, baselines kept simple: 핫팩(goal green pop), 불닭(gauge→green + pop), 물총(circle targets, hit green flash+pop), 집게손(grab green/miss red + pops), 청테이프(zone·cursor green + pops), 스마트폰(circle dots, per-dot pop, path→green + pops), 드라이버(per-screw green pop, borders kept)
- [x] 12. ROLLBACK per user ("장식 연출 전반 롤백" / "간단하고 직관적인 구현에 성취 피드백만"): removed glow·steam·shakes·tap punches·counter punch·phone frame·doll shadow·tape split·prong grip·progress bar·screw sink·live drag line
- [x] 13. Compile 0 errors (only pre-existing TurnManager CS0414), all staged

## Notes
- 파트너 병합으로 인벤토리가 **스택 모델**(동일 아이템 = 슬롯 1개 + 횟수 누적, RollExcludingOwned)로 변경된 것 확인 — granter 복원도 이 모델 준수.
- DebugItemGranter는 병합에서 제거됐던 파일 — 이번 브랜치에서 복원했으므로 PR 리뷰 시 파트너와 유지 여부 협의.
