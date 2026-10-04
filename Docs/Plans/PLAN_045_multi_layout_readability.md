# PLAN045 — Multi layout readability

## User request and boundaries

Adjust the four-player character arrangement, visible opponent inventory, and the squares below local inventory where they overlap or look misplaced. Keep the existing cyclic seat mapping, participant ownership, targeting and gameplay rules. Duel and Solo retain their existing presentation. Preserve editable map anchors and existing art.

## Confirmed causes

- Multi still renders the twelve authored local and twelve legacy opponent `ItemPlace` floor squares, even when those inventory positions are unused.
- Remote inventory spacing (0.62 units) is smaller than the maximum icon size (0.72). West/east icons also rotate sideways; north icons rotate upside down.
- Remote use-count text inherits the per-sprite scale, making its apparent size inconsistent.
- Local random items use the left half of an eight-slot row although normal Multi permits four random items.

## Implementation sequence

1. Capture a reproducible four-peer full inventory fixture (four basic + four random) before changes. Keep actual pointer input regression separate from diagnostic layout captures.
2. Hide only authored floor marker renderers in Multi; retain referenced anchor transforms and colliders. Center the four local random slots and preserve two clearly separated item rows.
3. Arrange each opponent inventory upright near its own seat: four columns/two rows at west/east, one row at north to clear the central icebox. Normalize tight sprite geometry independently of text and transparent image padding. More than eight total items fit additional columns inside the same area. Keep view reconciliation by physical copy identity.
4. Adjust only Multi map visual anchors where needed after inspecting rendered results. Separate the central icebox, local rows and remote grids. Do not change authoritative spawn seats or Duel anchors.
5. Compile, inspect all four rendered perspectives, run real Relay pointer selection/cancellation/Ready and relevant regressions; fix visible failures before recording completion.

## Acceptance evidence

- Normal eight and overflow twelve local views, with the corresponding views per remote participant; equal replicated inventory signatures across four peers. Overflow is reachable through item theft even though normal random grants cap at four.
- No redundant white floor squares, sideways icons, overlapping icon cells or clipped local inventory.
- All local seats retain west/north/east identity; departure does not compress seats.
- Item selection, target arrow, cancellation and Ready work with actual screen coordinates.
- Explicitly distinguish diagnostic camera renders, automated tests and human visual acceptance.

## Status

Implementation and scoped validation complete. Editor 1042/1042; real Relay layout 2/2, input 3/3 and contact smoke 1/1 (21 peer observations total). See [results and remaining display checks](../Validation/PLAN_045_results.md). Evidence directory: `output/validation/plan045/`.

## Final placement and safeguards

- Only Multi remote visual anchors changed in the four map prefabs: west/east X ±4.8, Z 5.1; north Z 8.8. Server spawn anchors and Duel anchors are unchanged.
- All 24 legacy `ItemPlace` renderers in `GameScene_Multi` are hidden. Anchor objects, references and original sprites remain editable.
- Local random inventory is centered inside its existing eight-slot row endpoints; a full eight random copies still fit those endpoints. The four basic anchors leave a larger center gap for Ready (X −2.7, −1.2, 1.2, 2.7).
- Local stay-fan anchor moves to (6.2, .5, .5), outside the full local item row.
- The icebox stays south-center at (0, .05, 2.4), with a .55 presentation parent outside the animated root. Its existing open/close clips keep working.
- Distribution restores each item's original scale instead of assigning Vector3.one. The shared fix preserves the authored sizes in Duel/Solo as well.
- Remote grid orientation uses the initial seating camera, so a delayed inventory update during a combat close-up cannot leave icons rotated.
- No gameplay inventory limits, contents, seat identity, server authority, selection protocol or combat rules changed. Development-only fixtures seed the 8/12-item cases and use separate Relay profiles.
