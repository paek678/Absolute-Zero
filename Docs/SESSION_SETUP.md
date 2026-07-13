# Session Setup — Absolute Zero

> New session bootstrap. Read this file and execute every step in order.
> After completing all steps, report the results table to the user.

---

## Step 1: MCP Unity Connection

> **Instance info (Name@hash) changes every Unity Editor restart.**
> Never hardcode — always detect dynamically via `mcpforunity://instances`.
> Port (default 8080) may also differ per machine or if multiple editors are open.

```
1-1. Load MCP tools:
     ToolSearch("select:mcp__unityMCP__set_active_instance,mcp__unityMCP__read_console")

1-2. Detect running Unity instances (name, hash, version are all dynamic):
     ReadMcpResourceTool(server="unityMCP", uri="mcpforunity://instances")

1-3. Set active instance using the EXACT Name@hash returned from 1-2:
     mcp__unityMCP__set_active_instance(instance="{Name}@{hash}")
     ⚠ Do NOT reuse hash from previous sessions — it changes every restart

1-4. Verify connection:
     mcp__unityMCP__read_console(action="get", count="3")
```

### Failure Cases

| Symptom | Cause | Fix |
|---------|-------|-----|
| `instance_count: 0` | Unity Editor not running or MCP server not started | Open Unity Editor → MCP for Unity panel → Connect |
| `no_unity_session` after set_active | Hash mismatch (stale) | Re-run step 1-2 to get fresh hash |
| `Connection refused` | MCP HTTP server not started | Check Unity MCP panel → Stop Server → Start Server |
| Multiple instances returned | Multiple Unity Editors open | Ask user which project, then set that instance |

---

## Step 2: Read Harness State

```
2-1. Read current work state:
     Read("Docs/ACTIVE_CONTEXT.md")

2-2. If status is "In Progress" — read the active plan:
     Read("Docs/Plans/PLAN_NNN_*.md")  ← file referenced in ACTIVE_CONTEXT

2-3. If resuming work — read recent changes:
     Read("Docs/RECENT_CHANGES.md")
```

---

## Step 3: Verify Safety Rules

```
3-1. Read safety rules:
     Read("Docs/SAFETY_RULES.md")

3-2. Note current rule count and last updated date
```

---

## Step 4: Report to User

Print this table with actual values filled in:

```
=== Session Setup Complete ===

| Item                  | Status | Details                    |
|-----------------------|--------|----------------------------|
| MCP Connection        | ✅/❌  | {instance name} @ {hash}   |
| Unity Version         | —      | {version from instances}   |
| Console Errors        | ✅/⚠️  | {error count}              |
| Work Status           | —      | {Idle / In Progress}       |
| Active Plan           | —      | {plan name or "none"}      |
| Safety Rules          | —      | {N} rules loaded           |
| Last Modified         | —      | {date from ACTIVE_CONTEXT} |

Ready for work. What's the task?
```

---

## Quick Reference

| Need | Command |
|------|---------|
| Check console | `mcp__unityMCP__read_console(action="get", count="10")` |
| Scene hierarchy | `mcp__unityMCP__manage_scene(action="get_hierarchy")` |
| Create object | `mcp__unityMCP__manage_gameobject(action="create", ...)` |
| Run C# code | `mcp__unityMCP__execute_code(action="execute", code="...")` |
| Project info | `ReadMcpResourceTool(server="unityMCP", uri="mcpforunity://project-info")` |
| Editor state | `ReadMcpResourceTool(server="unityMCP", uri="mcpforunity://editor-state")` |
