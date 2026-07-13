# /console-check — Unity 콘솔 로그 분석

MCP Unity의 get_console_logs를 호출하여 에러/경고를 분류하고 원인을 역추적한다.

## 입력 형식

```
/console-check [--errors-only] [--match-issues]
```

- 기본: Error + Warning 모두 분석
- `--errors-only`: Error만 표시
- `--match-issues`: KNOWN_ISSUES.md 자동 매칭

## 전제 조건

- Unity Editor 실행 중
- MCP Unity WebSocket 연결 상태 (포트 8090)

## 실행 절차

### Step 1: 콘솔 로그 수집

1. `mcp__mcp-unity__get_console_logs` 호출
2. 반환된 로그를 Error / Warning / Info 로 분류
3. 총 건수 출력

### Step 2: Error 분석

각 Error에 대해:

1. **NullReferenceException** 패턴:
   - 스택트레이스에서 `at 클래스.메서드 (파일:라인)` 추출
   - 해당 파일:라인 `Read`로 코드 확인
   - null 가능성 있는 변수 식별
   - RULE-012(target null 체크), RULE-020(enemies null 슬롯) 관련 여부 판단

2. **MissingReferenceException** 패턴:
   - "The object of type 'X' has been destroyed" 메시지에서 타입 추출
   - 프리팹/씬 참조 문제로 분류
   - `/prefab-audit` 실행 권장 여부 판단

3. **MissingComponentException** 패턴:
   - 어떤 GameObject에서 어떤 컴포넌트가 없는지 추출
   - RULE-021(MapManagers 컴포넌트 버그) 관련 여부 확인

4. **기타 Exception**:
   - 메시지와 스택트레이스 요약

### Step 3: Warning 분석

1. 반복 Warning 그룹핑 (동일 메시지 N회 → 1건)
2. "SendMessage has no receiver" → 빈 메서드/이벤트 핸들러 문제
3. 성능 Warning 식별

### Step 4: KNOWN_ISSUES 매칭 (--match-issues)

1. `Read("Docs/KNOWN_ISSUES.md")` → 등록된 이슈 목록 로드
2. 각 에러를 KI-NNN과 매칭:
   - KI-001: MapManagers 컴포넌트 버그 → MissingComponentException 매칭
   - KI-002: SkillAction_68 스킵 → 관련 에러 매칭
   - KI-003: null 슬롯 → NullReferenceException + CombatManager 매칭
3. 미등록 에러 → "신규 이슈 후보" 로 분류

### Step 5: 보고서 출력

```markdown
# Console Check Report — [날짜]

## 요약
| 유형 | 건수 |
|------|------|
| Error | N |
| Warning | N |
| KNOWN_ISSUES 매칭 | N |
| 신규 이슈 후보 | N |

## Errors
| # | 유형 | 메시지 요약 | 파일:라인 | 관련 규칙 |
|---|------|-----------|----------|----------|
| 1 | NullRef | target is null | SkillEffect...:242 | RULE-012 |

## Warnings (그룹핑)
| # | 메시지 | 반복 횟수 |
|---|--------|----------|
| 1 | SendMessage has no receiver | 12 |

## 신규 이슈 후보 (KNOWN_ISSUES 미등록)
| # | 유형 | 메시지 | 권장 KI 번호 |
|---|------|--------|-------------|
| 1 | IndexOutOfRange | enemies[3] | KI-025 |
```

## 주의사항

- MCP Unity 미연결 시 즉시 중단 + 연결 방법 안내
- `mcp__mcp-unity__recompile_scripts` 절대 호출 금지 (RULE-002)
- 이 skill은 **읽기 전용** — 코드 수정 없음
