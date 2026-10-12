# PLAN 069 — Combat role icons and cat inventory choreography

## User request
Show existing UI_attacker/UI_target sprites during attacks and opponent feeding; omit self use. Verify the cat traverses the target inventory in four-player side seats and provide actual gameplay screenshots.

## Implementation
- CombatVFXManager retains the current item presentation context for a scoped playback lifetime. UI reads actor/recipient bindings; no RPC or shared gameplay state changes.
- CombatRoleIconPresenter renders existing sprites above visible opponents. Per the follow-up decision, hide either role for the local FPS participant: local-to-remote shows only target, remote-to-local only attacker, remote-to-remote both. Avoid scorecards, health bars and clock. Clear after action, cancellation, disconnect or scene disposal.
- Cat follows the same stable item anchors used by each viewer's relative seating layout. Use a bounded sweep along the actual row and a modest jump; keep art camera aligned.
- Development-only role-cat fixture exercises three cat targets, feeding, fan attack, self recovery and cleanup on every peer.

## Scope / validation
No inventory, damage, targeting, map placement, sprite art or network protocol changes. Verify automatic compilation, a standalone Host + three clients, a duel pair, frame captures, role visibility/cleanup and cat distance to inventory row. Record exact results and remaining gaps in Docs/Validation/PLAN_069_combat_role_icons_cat_rack.md.

## Completion
Implemented and focused validation passed (2026-10-12). See [results and deferred ledger](../Validation/PLAN_069_combat_role_icons_cat_rack.md). A separate CoolBreeze VFX occlusion is recorded there as D01 and remains open.
