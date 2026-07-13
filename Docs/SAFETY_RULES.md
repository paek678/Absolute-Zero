# Safety Rules — Absolute Zero

> Living document. Update immediately when errors are found or lessons learned.
> Last updated: 2026-07-13

---

## Rule Verification System

> Safety rules **constrain** Claude's actions. Incorrect rules can block valid work,
> so **only rules with verified code evidence are binding.**

### Verification States

| State | Meaning | Claude Behavior |
|-------|---------|-----------------|
| `✅ Verified` | Code evidence confirmed | **Must comply** — stop immediately on violation |
| `⏳ Unverified` | Needs further confirmation | **Caution level** — reference but may override if needed, mention to user |
| `❌ Disproven` | Confirmed mismatch with code | **Ignore** — delete or revise on next review |

### Rule Lifecycle

```
New rule (⏳ Unverified)
    ↓ Code evidence confirmed (file:line specified)
Verified (✅ Verified)
    ↓ Code changes invalidate rule
Re-verification needed (⏳ Unverified)
    ↓ Confirmed mismatch with code
Retired (❌ Disproven) → Delete or revise and re-register
```

### Verification Procedure (when adding/reviewing rules)

1. **Confirm code evidence:** Verify the code pattern actually exists via `Grep`/`Read`
2. **Record evidence:** Specify confirmed file:line numbers in the rule
3. **Search for counterexamples:** Check if code violating the rule already exists
4. **Assess impact scope:** Determine if the restricted behavior is actually dangerous
5. **Assign state:** Pass steps 1-4 → `✅ Verified`, insufficient → `⏳ Unverified`

### Periodic Review Triggers

- **Before starting code modification:** Verify related rule evidence is still valid
- **On new session start:** Re-verify rules related to changed files

---

## NEVER DO (Critical)

### RULE-001: ScriptableObject runtime modification forbidden
- **Status:** ✅ Verified (Unity engine behavior)
- **Category:** Architecture
- **Reason:** ScriptableObject field changes persist in Editor. Runtime modification corrupts source asset data permanently
- **Correct pattern:** Read from SO at initialization, copy values to runtime plain class or struct. Never write back to SO fields at runtime
- **Evidence:** Unity engine documented behavior — SO assets are shared instances in Editor, changes write through to disk
- **Ported from:** AbyssNode RULE-001 (universal Unity constraint)
- **Added:** 2026-07-13

### RULE-003: recompile_scripts MCP call forbidden
- **Status:** ✅ Verified
- **Category:** Tooling
- **Reason:** Breaks MCP WebSocket connection, requires manual Unity Editor restart to reconnect
- **Correct pattern:** Rely on Unity Editor auto-recompile on file save. Never call `mcp__unityMCP__recompile_scripts`
- **Evidence:** `.claude/settings.json` deny list. AbyssNode에서 확인된 동작
- **Ported from:** AbyssNode RULE-002 (universal MCP constraint)
- **Added:** 2026-07-13

### RULE-004: MCP HTTP direct connection required on Windows (not stdio)
- **Status:** ✅ Verified
- **Category:** Tooling
- **Reason:** stdio transport (uvx Python server) fails to discover Unity Editor instances on Windows — `No Unity Editor instances found` error. HTTP direct connection works reliably
- **Correct pattern:** Use `"type": "http", "url": "http://127.0.0.1:8080/mcp"` in `.mcp.json`
- **Evidence:** `.mcp.json` HTTP config. AbyssNode에서 3회 이상 stdio 실패 후 HTTP로 전환하여 해결
- **Ported from:** AbyssNode RULE-034 (universal Windows + Unity MCP constraint)
- **Added:** 2026-07-13

### RULE-002: .meta file path renaming requires planned migration
- **Status:** ✅ Verified (Unity engine behavior)
- **Category:** Git/Unity
- **Reason:** Unity `.meta` files bind GUIDs to paths. Renaming folders/files without preserving `.meta` GUIDs breaks all serialized references in scenes, prefabs, and ScriptableObjects
- **Correct pattern:** Use Unity Editor to rename (preserves GUID), or plan batch rename that preserves `.meta` file pairings. Never rename via filesystem directly
- **Evidence:** Unity engine `.meta` binding architecture (official docs)
- **Ported from:** AbyssNode RULE-003 (universal Unity constraint)
- **Added:** 2026-07-13

### RULE-005: Namespace final segment must not collide with imported type names
- **Status:** ✅ Verified
- **Category:** Code
- **Reason:** `AbsoluteZero.UI.Lobby` resolved to the namespace instead of `Unity.Services.Lobbies.Models.Lobby`, causing CS0118 compilation error
- **Correct pattern:** Suffix namespace with category (e.g., `LobbyUI`, `PlayerVisuals`). Check if final segment matches any imported type name before creating
- **Evidence:** `Assets/Scripts/UI/Lobby/AZLobbyUI.cs` — originally used `AbsoluteZero.UI.Lobby`, had to rename to `AbsoluteZero.UI.LobbyUI`
- **Added:** 2026-07-13

---

## ALWAYS DO

### RULE-010: Event handler cleanup required on coroutine/callback exit
- **Status:** ✅ Verified (universal Unity pattern)
- **Category:** Code
- **Reason:** Event handler (`+=`) without corresponding `-=` on exit causes duplicate calls and NullReferenceException on destroyed objects
- **Correct pattern:** Every `+= handler` must have a corresponding `-= handler` on coroutine exit, OnDisable, or OnDestroy
- **Evidence:** Unity engine lifecycle — destroyed MonoBehaviours with lingering event subscriptions cause MissingReferenceException
- **Ported from:** AbyssNode RULE-014 (universal Unity constraint)
- **Added:** 2026-07-13

### RULE-011: Null check required in entity iteration loops
- **Status:** ✅ Verified (universal Unity pattern)
- **Category:** Code
- **Reason:** Entities (enemies, players, network objects) can be destroyed mid-loop. `foreach` over entity lists without null check causes NullReferenceException
- **Correct pattern:** `foreach (var entity in entities) { if (entity == null) continue; ... }`
- **Evidence:** Unity engine — `Destroy()` sets reference to null but doesn't remove from List. Network objects can despawn mid-frame
- **Ported from:** AbyssNode RULE-012 (universal Unity constraint)
- **Added:** 2026-07-13

---

## WARNINGS

### RULE-020: WaitForSeconds must be cached as static readonly
- **Status:** ✅ Verified (universal Unity performance)
- **Category:** Performance
- **Reason:** `new WaitForSeconds()` per coroutine iteration creates GC pressure. Cached instances eliminate allocation
- **Correct pattern:**
```csharp
private static readonly WaitForSeconds Wait1Sec = new WaitForSeconds(1f);
private static readonly WaitForSeconds Wait01Sec = new WaitForSeconds(0.1f);
```
- **Evidence:** Unity performance best practices (official docs). Coroutines in networked game run frequently
- **Ported from:** AbyssNode RULE-005 (universal Unity constraint)
- **Added:** 2026-07-13

### RULE-021: Never mix PowerShell and Bash syntax in a single pipeline
- **Status:** ✅ Verified (environment constraint)
- **Category:** Tooling
- **Reason:** PowerShell uses `$env:VAR`, backtick escaping, object pipeline; Bash uses `$VAR`, backslash escaping, text pipeline. Mixing causes silent failures or parse errors on Windows
- **Correct pattern:** Use pure PowerShell OR pure Bash per command — never chain `cmd /c` inside PowerShell or vice versa
- **Impact:** Mixed syntax causes unpredictable failures — wrong variable expansion, broken pipelines, or commands that silently do nothing
- **Ported from:** AbyssNode RULE-035 (universal tooling constraint)
- **Added:** 2026-07-13

---

## Adding New Rules

1. Add to appropriate section (NEVER DO / ALWAYS DO / WARNINGS)
2. Number convention: 001-009 = NEVER DO, 010-019 = ALWAYS DO, 020+ = WARNINGS
3. Required fields: Category, Reason, Added date
4. Recommended fields: Correct pattern, File path, Impact
5. Update `Last updated` date
