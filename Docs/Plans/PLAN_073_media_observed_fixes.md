# PLAN073 — Fix defects observed during media validation

## Scope

User request: list and fix the recorded errors after the fresh media delivery.

1. MEDIA-WIND-01: large pale/black CoolBreeze geometry obscures four-player gameplay in the built player. Preserve the environment's rules and camera motion; fix its rendering, then verify the exact environment in all four views and duel/Solo.
2. SESSION-EXIT-01: Solo cleanup attempts to load LobbyScene while the Editor is exiting Play Mode. Preserve normal leave/restart, network shutdown and exclusive session ownership; suppress new menu scene loads during application teardown.

Existing font/Pipeline/unused-field build warnings are not established gameplay failures. Successful feeding tests and encoder validation need no speculative changes. No unrelated refactoring, commits or media-wide retake is required.

## Sequence and acceptance

- Capture baseline player shader/material diagnostics and visible wind geometry. Compare with the Editor and wind renderer disabled.
- Fix the concrete rendering cause, clean up owned runtime resources, and test environment reset/reuse.
- Add a shared application-lifetime check at concrete menu scene-loading boundaries. Test Editor Stop twice plus normal Solo menu return/restart and online leave regression.
- Compile, focused tests, fresh fixed-path Windows player build; run real multi-client and Solo/duel visual checks. Preserve before/after screenshots and known limitations in a single validation ledger.

Validation ledger: `Docs/Validation/PLAN_073_results.md`.
