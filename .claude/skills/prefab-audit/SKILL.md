# /prefab-audit — 프리팹/씬 참조 무결성 검사

프리팹과 씬 파일에서 Missing Reference, 깨진 GUID, 빈 참조를 탐지한다.
Unity에서 가장 흔한 런타임 에러 원인을 사전에 잡는다.

## 입력 형식

```
/prefab-audit [경로] [--scene] [--fix-hint]
```

- 기본: `Assets/RoguelikeGame/` 하위 모든 .prefab
- `--scene`: .unity 파일도 포함
- `--fix-hint`: 유사 GUID 제안 (느림)
- 경로 지정: 특정 프리팹/폴더만 검사

## 실행 절차

### Step 1: 대상 파일 수집

1. `Glob("Assets/RoguelikeGame/**/*.prefab")` → 프리팹 목록
2. `--scene` 옵션 시 `Glob("Assets/**/*.unity")` 추가
3. 대상 파일 수 출력

### Step 2: Missing Reference 탐지 (fileID: 0)

1. 각 .prefab/.unity 파일에서:
   ```
   Grep("fileID: 0, guid: 0{32}", 대상파일)
   ```
2. `{fileID: 0, guid: 00000000000000000000000000000000, type: 0}` = **완전한 Missing Reference**
3. 발견된 위치의 컨텍스트(전후 5줄)에서 컴포넌트명/필드명 추출

### Step 3: MonoScript GUID 유효성 검증

1. 각 .prefab/.unity에서 `m_Script:` 라인 추출:
   ```
   Grep("m_Script:.*guid:", 대상파일)
   ```
2. 추출된 GUID가 실제 .cs.meta 파일에 존재하는지 확인:
   ```
   Grep("guid: 추출된GUID", "Assets/**/*.cs.meta")
   ```
3. 매칭 실패 → **MonoScript Missing** (스크립트 삭제/이동됨)

### Step 4: 빈 참조 필드 탐지

1. 각 .prefab에서 Object 참조 필드 중 비어있는 것:
   ```
   Grep("fileID: 0[^0]", 대상파일)
   ```
   또는:
   ```
   Grep("{fileID: 0}", 대상파일)
   ```
2. 이는 Inspector에서 None/Missing으로 보이는 참조
3. 컨텍스트에서 어떤 컴포넌트의 어떤 필드인지 추출

### Step 5: 삭제된 프리팹 참조 확인

1. `Glob("Assets/RoguelikeGame/**/*.prefab.meta")` 에서 GUID 추출
2. 다른 .prefab/.unity에서 해당 GUID 참조 여부 확인
3. .prefab은 삭제되었지만 참조가 남아있는 경우 → **Dangling Reference**

### Step 6: 보고서 출력

```markdown
# Prefab Audit Report — [날짜]

## 요약
| 유형 | 건수 | 심각도 |
|------|------|--------|
| Missing Reference (null) | N | 높음 |
| MonoScript Missing | N | 높음 |
| Empty Object Field | N | 중간 |
| Dangling Prefab Ref | N | 높음 |

## Missing References
| # | 파일 | 컴포넌트 | 필드 | GUID |
|---|------|---------|------|------|
| 1 | Enemy.prefab | EnemyCharacter | target | 000...000 |

## MonoScript Missing
| # | 파일 | GUID | 예상 스크립트 |
|---|------|------|-------------|
| 1 | UI.prefab | abc123... | (삭제됨) |
```

## 주의사항

- .prefab/.unity 파일이 **텍스트(YAML) 직렬화** 모드여야 검색 가능
  - `ProjectSettings/EditorSettings.asset`에서 `m_SerializationMode: 2` (Force Text) 확인
- Binary 직렬화 파일은 grep 불가 → 스킵하고 경고 출력
- Library/ 폴더는 탐색 제외
- 이 skill은 **읽기 전용** — 파일 수정 없음
