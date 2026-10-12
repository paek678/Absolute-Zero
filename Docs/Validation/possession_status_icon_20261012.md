# Possession status icon — 2026-10-12

> Placement superseded by [thermometer attachment follow-up](thermometer_icons_20261012.md). The original overlay placement below is historical evidence, not the current implementation.

## Requested behavior and implementation

- Replace the persistent purple world-space possession ring with the existing `Resources/skill_capture` sprite. Use the same 64 reference-pixel size and camera-independent overlay placement as attack/recipient icons.
- `CombatRoleIconPresenter` observes the replicated `GhostPossessedMask` with current session/scene and logical-seat binding checks. Display persists while the bit is active and hides when it clears or the owner/binding disappears. Remote icons use the existing overhead/HP-bar placement; the local FPS target uses its own health HUD because its head is not rendered.
- Adjacent placement avoids covering active attacker/recipient icons. Remove only the continuous `PossessedSeat` ring from `GhostSkillVFXPresenter`; retain the brief casting, impact and item-suppression feedback.
- No server mutation, RPC format, duration, cooldown, consumption or skill-effect changes. The proposed timed ring fade was superseded by the user's persistent-icon decision and is absent from final source.

## Validation

- Normal Unity compilation passed. The initial diagnostic probe referenced UI from the Core assembly and failed compilation; corrected the probe to inspect the rendered hierarchy without a reverse assembly dependency. No assembly definitions were changed.
- Final build job `2478ceda32da4a5096d5d7d0486737c3` succeeded, 0 errors and 4 warnings, using the fixed executable path.
- Real Relay four-player runs, all completed with synchronized checkpoints and all peers exiting 0:
  - `output/validation/ghost-skill-icons/possession-status/report.json`: active and persisted icons on all four perspectives; no old world ring. Actual frames inspected for remote front/side and local HUD placement.
  - `output/validation/ghost-skill-icons/possession-status-clear/report.json`: possession/attack suppression regression passed. This fixture exits before the debuff reset and is not used as expiry proof.
  - `output/validation/ghost-skill-icons/possession-status-round/report.json`: round-revival lifecycle passed; all four logs report `POSSESSION_ICON_CLEARED` after the authoritative mask clears. Frame captures preserve the removal.
- The development-only `--az-possession-icon-check` observes active UI visibility versus the replicated state, confirms persistence beyond two seconds, and rejects a surviving old ring. It does not modify gameplay state; the pre-existing scenarios stage ghost/skill conditions.
- Test players exited; Editor remains LobbyScene, clean Edit Mode. No commit/push.

## Limits

- These are focused staged scenarios, not full natural matches. Normal next-turn expiry, mid-skill disconnect and all aspect ratios were not separately exercised in this change. Round-reset removal and current binding cleanup are distinguished from those untested cases.
- Simultaneous status/attack collision avoidance is implemented but was not separately driven as a dedicated visual case.
- Existing provisional rectangle ghost art and unrelated environment VFX observations remain outside this change.

## Screenshot

`output/validation/ghost-skill-icons/possession-status/seat1.0009.possession-status-persisted.png`: actual 1600x900 framebuffer; the left opponent carries the possession icon above its HP bar.
