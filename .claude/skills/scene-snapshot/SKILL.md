# /scene-snapshot — 씬 상태 스냅샷

현재 열린 Unity 씬의 구조를 캡처하고 싱글톤/컴포넌트 정상 상태를 검증한다.

## 입력 형식

```
/scene-snapshot [--save] [--check-only]
```

- 기본: 씬 구조 출력 + 검증
- `--save`: `Docs/scene_snapshot_YYYYMMDD.md`로 저장
- `--check-only`: 싱글톤 검증만 (구조 출력 생략)

## 전제 조건

- Unity Editor 실행 중 + 씬 로드 상태
- MCP Unity WebSocket 연결 (포트 8090)

## 실행 절차

### Step 1: 씬 정보 수집

1. `mcp__mcp-unity__get_scene_info` 호출 → 현재 씬 구조
2. 씬 이름, 루트 GameObject 목록 추출

### Step 2: 싱글톤 매니저 검증

프로젝트의 3대 싱글톤을 확인:

| 오브젝트명 | 필수 컴포넌트 | 검증 |
|-----------|-------------|------|
| `@GameManagers` | `Managers` | 존재 + 컴포넌트 확인 |
| `@CombatManager` | `CombatManager` | 존재 + 컴포넌트 확인 |
| `@MapManagers` | `MapManagers` | 존재 + 컴포넌트 확인 (**RULE-021 재발 방지**) |

각 싱글톤에 대해:
1. `mcp__mcp-unity__get_gameobject` (objectPath: "@이름") 호출
2. 컴포넌트 목록에서 필수 컴포넌트 존재 확인
3. `@MapManagers`에 `Managers` 컴포넌트가 붙어있으면 → **RULE-021 버그 재발 경고**

### Step 3: 주요 컴포넌트 상태 확인

1. PlayerCharacter 존재 여부
2. EnemySpawner 참조 상태
3. CombatUI 관련 오브젝트 존재 여부

### Step 4: 보고서 출력

```markdown
# Scene Snapshot — [씬이름] — [날짜]

## 씬 구조
- 루트 오브젝트: N개
- 활성 오브젝트: N개

## 싱글톤 검증
| 오브젝트 | 존재 | 필수 컴포넌트 | 상태 |
|---------|------|-------------|------|
| @GameManagers | ✅ | Managers | ✅ 정상 |
| @CombatManager | ✅ | CombatManager | ✅ 정상 |
| @MapManagers | ✅ | MapManagers | ⚠️ Managers 컴포넌트 감지 |

## 주요 오브젝트
| 오브젝트 | 컴포넌트 | 상태 |
|---------|---------|------|
| Player | PlayerCharacter, StatManager | ✅ |
```

### Step 5: 저장 (--save)

`--save` 옵션 시 `Docs/scene_snapshot_YYYYMMDD.md`로 보고서 저장.
이전 스냅샷과 비교 가능하도록 동일 형식 유지.

## 주의사항

- MCP Unity 미연결 시 즉시 중단
- `mcp__mcp-unity__recompile_scripts` 절대 호출 금지 (RULE-002)
- 씬 구조 변경 없음 — 읽기 전용
