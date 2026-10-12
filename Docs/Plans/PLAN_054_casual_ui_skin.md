# PLAN054 — Shared casual UI artwork

## Approved scope

Use the approved menu/combat concept boards in `output/ui-style-20261011`.
Generate and retain text-free reusable image components first, then apply the
rounded cream/mint/sky/butter/peach/lavender style to the existing UI.
Keep names, text, icons and state bound to existing controls and Gmarket Sans Bold.

## Tasks

- T01: Export transparent panel, pill and card images; import with verified
  nine-slice borders. Preserve prompts and original concepts for human review.
- T02: Central skin helper, explicit lobby panel integration, dynamic closet
  cells/selection and room rows. Settings, mode and bot controls share the skin.
- T03: Departure dialog, result, ghost skills and environment notices. Reuse
  existing thermometer, clock, participant banner and authored lobby art.
- T04: Loading progress and mini-game common entrance/result chrome.
- T05: Unity compile, targeted asset/input/state tests and before/after screen
  capture. Fix observed defects, record limitations and human review items.

## Boundaries

No rule, network authority, cooldown, difficulty, inventory/world placement,
character, timing, or save schema changes. No generated concept avatars or new
lobby readiness mechanics. Do not style full-screen dim/input blockers or clipping
viewports as framed panels. Preserve authored callbacks and selection ownership.

## Implementation

`UI/Common/CasualUiSkin` centralizes sprite loading, palette, typography and
control skinning. Existing builders/views invoke it explicitly. No global scene
scanner or per-frame restyling. Generated assets live in Resources/UI/Casual.
Result plaque follows the existing cinematic fade; controls retain real state.

## Evidence

Initial worktree snapshot: `output/validation/plan054/status-before.txt`.
110 pre-existing changed/untracked entries; preserve unrelated work.
Unity CLI verified Absolute Zero, Unity 6000.3.11f1, PID 13152, port 7800.
T01–T05 implemented and checked on 2026-10-11. Final focused tests 9/9;
full Editor suite 1100/1104 (four pre-existing map-coordinate expectations).
Real Solo menu/return flow and two actual Relay scenarios passed. Artwork and
runtime gallery are ready for human approval. See
[results and limitations](../Validation/PLAN_054_results.md).
