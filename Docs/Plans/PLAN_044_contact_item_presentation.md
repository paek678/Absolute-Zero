# PLAN044 - Contact item presentation

## User decision and scope

Use suitable existing item motions without broken animation. Keep all existing art, rigs, clips, item rules, authoritative resolution, item order, mini-games and completion barriers. The presentation layer must observe recorded results, never recalculate damage or issue game commands.

## Implementation

- Share contact choreography across Duel, Solo and Multi, before the generic item path.
- Hug T-shirt: approach the target, play the existing hug, show impact at hand contact, let hands retract, restore the original view. Move authored FPS root with the camera and aim toward the actual target; restore both position and rotation on normal or interrupted completion.
- Visual review refinement: the remote character moves as one assembled idle silhouette along a shallow arc. The old jump clip exposed a torso/leg gap and is excluded from this contact sequence. Finish the hug and return to idle before moving back. Receivers see a centered character in front of the icebox; observers see separate actor/target silhouettes. Restore temporary SortingGroup overrides; keep the local defense/FPS in front and the fan at its seat.
- Ice Cream, Iced Americano and Samgyetang: giver uses existing feed motion, recipient uses item-only FPS `use` / world `feed`. Transfer and result display occur when the giver releases the item. Wait for full giver and recipient clips. A fully blocked attempt invokes the defense response without a receiving motion.
- Preserve all other item motions. Add no sprites, arms, bones or clips.
- Own temporary poses and objects per presentation execution. Disposal must be idempotent and late cleanup must not change a newer execution.

## Tasks and gates

1. T01: Audit actual authored clip timing and scene bindings; snapshot worktree. Complete.
2. T02: Implement shared contact choreography, clip duration lookup and pose restoration. Complete.
3. T03: Editor compile and focused/all regression tests; fix observed failures. Complete: 1035/1035 including real-controller end-trigger regression and sorting/pose restoration.
4. T04: Opt-in development probes through real local Solo and network item commands. Capture giver, receiver and observer views in Relay 2/3/4-player sessions. Verify one impact, role-specific FPS, pose restoration, settlement and next turn. Complete: 11 contact cases / 32 peer observations and 2 Relay regressions / 5 peer observations passed. Motion-frame review prompted and verified assembled-body travel and foreground ordering.
5. T05: Record results and limitations, preserve original scene/assets and unrelated changes. Complete; remaining human/art/HUD/network checks are tracked in the linked results.

## Validation evidence

Baseline: `output/validation/plan044/baseline.json` and `baseline-status.txt`.
Final execution evidence and unresolved checks belong in `Docs/Validation/PLAN_044_results.md`.
