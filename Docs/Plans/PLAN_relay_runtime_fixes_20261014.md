# Relay and runtime corrections — 2026-10-14

## Authority and scope

The user authorized correcting the findings in `Docs/Validation/relay_runtime_audit_20261014.md` and preparing a verified replacement build. Deadline: 2026-10-15 14:00 KST. Current integration checkout and its pre-existing dirty work are the source. Preserve the previous audit, prior build and Desktop/Demo package; create a separate corrected build. No automatic email, merge, commit, push, deployment, dependency changes or balance retuning.

## Trace and implementation

| Finding / requirement | Owner / implementation | Required evidence |
|---|---|---|
| B01: participants may wait for the host normally | NetworkSessionCoordinator separates pre-start waiting from transport startup deadlines, retains cancellation and lost-lobby cleanup | Actual Relay wait >35s then start; cancel and host departure during waiting |
| B02: room dictates mode/count and synchronized scene | Validate authoritative lobby metadata before transport/scene admission; update current online session lease safely without releasing ownership | Both mismatched menu directions, correct lease/scene/count, malformed metadata rejection |
| B03: no generic already-member dead end | LobbyGateway authenticated membership lookup; recover matching waiting lobby, explain and clean only own stale in-progress membership without reviving a TimedOut seat | Same-profile waiting re-entry, active-match rejection/cleanup, subsequent fresh room |
| B04: design Temperature System caps random inventory at8 (Multi remains4) | Pass effective GameRule.MaxRandomItems through shared Solo/Duel PrepTurnOperations.ApplyDuelTick | Production-caller regression with consumed basic slots, human/bot, repeated thresholds; real runtime grants |
| B05: design environment table specifies CoolBreeze SFX_wind | Supply a local original seamless wind resource; existing GameAudioManager routes and gain controls stay in use | Imported/packaged clip, real loop start/stop and mute/volume routing |
| B06: user requested Korean game UI | Translate remaining ordinary status messages and clear phase-obsolete status in MatchHudPresenter/GameUIManager | Phase progression and ordinary UI text checks; no debug-only camera artifact promoted to bug |
| Child DontDestroyOnLoad noise | Keep root component persistence, let the existing persistent Managers parent own child lifetime | No new dispatcher warnings; repeated normal scene return |
| Audit fixture re-entry race | Development-only MatrixScenarioProbe waits for coordinator Ready and released session before re-entry | Exact controlled host4→3 case rerun; preserve old failed attempt |

Source rules take priority over old design passages mentioning five kills. Keep current user-approved Multi three rounds / first three kills / tied winners and all difficulty values unchanged. A single12m26s sample does not authorize silent retuning. Same-match reconnection would require a separate grace/identity policy; this fix addresses recovery and accurate feedback within current immediate-forfeit rules.

## Validation and preservation

1. Save initial dirty diff/status, inspect current Editor project, preserve old Player.
2. Focused regressions and full Editor suite; rely on automatic compilation only.
3. Fresh development Player at the existing allowed path. Freeze scripts/build during executions.
4. Fresh actual Lobby/Relay boundary cases; Solo threshold/audio/UI checks; shared controlled cases and natural Beginner/Advanced/Duel/Multi completion as required for shared paths.
5. Independent diff review, ordinary Windows build with development/debugging/profiler off, test APIs excluded, normal launch/cleanup.
6. Maintain canonical results in `Docs/Validation/relay_runtime_fixes_20261014.md`; distinguish controlled inputs, natural automated matches and diagnostic renders. Keep every failed run. Restore Editor/settings and stop only owned test processes.

Evidence root: `output/relay-fixes-20261014/`. The audit hashes in the preceding evidence folder are the pre-change reference. Do not rewrite that audit into a pass.
