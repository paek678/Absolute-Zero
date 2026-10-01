# PLAN 041 — Post-pull recovery and verification

## Contract

User approved fixing the findings of the 2026-10-01 post-pull audit and rerunning debugging. Preserve gameplay balance, authority, turn order, 1v1/Solo behavior, existing local work and incoming art. No package upgrades or new gameplay rules.

## Tasks

Completed 2026-10-01: T1–T5. Final evidence and remaining art/external-device limits are in [PLAN 041 results](../Validation/PLAN_041_results.md).

- T1: Recover the shelved Multi view/map/anchor wiring; reconcile incoming scene-only outfit overrides so a visual slot cannot assign another player's clothing. Preserve incoming textures/libraries and the inactive legacy decoration.
- T2: Make owned HUD shake offsets safe when their RectTransform is destroyed before the presenter; verify repeated cleanup and replacement layout ownership.
- T3: Register all seven incoming tops through the existing cosmetic atlas/registry path, mapping body and both arm poses without changing head/bottom appearance or animation transforms. Verify wardrobe, persistence and network delivery.
- T4: Investigate terminal screen capture/render divergence; check actual rendered winner/loser controls. **Closed as an incorrect earlier visual diagnosis**, not a product defect. Added a targeted screenshot pixel gate.
- T5: Strengthen targeted diagnostic prerequisites and run Editor tests, build, manual input probes, ghost/possession, disconnect/minigame boundaries, 1v1 and Solo regression.

## Evidence and gates

Pre-fix evidence: `C:/Users/paek6/.codex/backups/absolute-zero/post-pull-audit-20261001-123227/followup/REPORT.md`.
Current snapshots and run evidence: `C:/Users/paek6/.codex/backups/absolute-zero/post-pull-fix-20261001`.
Canonical outcome/deferred checks: `Docs/Validation/PLAN_041_results.md`.

Each task requires current-source verification. Automated gameplay PASS is separate from visual review. Keep any remaining actual-PC, latency, long-duration and manual checks explicit.
