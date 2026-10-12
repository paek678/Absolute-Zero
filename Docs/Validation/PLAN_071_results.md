# PLAN071 — Separate item copies

Date: 2026-10-12. Unity 6000.3.11f1. [Plan](../Plans/PLAN_071_separate_item_copies.md).

## Result

Acquired duplicates now occupy independent slots instead of adding remaining uses to an existing copy. Ordinary random/specific grants and both duel/Multi steal paths use the same capacity/hole policy. Full inventories reject a transfer without removing the victim's item. Prepared Multi transfers revalidate their reserved destination. Original per-item uses and existing capacity/top-up limits remain unchanged.

World item cards no longer display counts or infinity. Copy-to-anchor mapping now also covers duel/solo local and opponent items. New copies use acquisition IDs when multiple updates arrive together; existing copies retain their anchors through compaction and refill.

## Executed checks

| Check | Evidence / outcome |
|---|---|
| Editor inventory calculations | `output/plan071-inventory-tests.json`: 17/17 passed |
| Fixed anchors and acquisition order | `output/plan071-anchor-tests.json`: 6/6 passed |
| Visual polish / label regression | `output/plan071-label-tests.json`: 11/11 passed |
| Fresh Windows development player | `output/validation/plan071/build-report.json`: succeeded, 0 errors / 4 warnings |
| Four-player Relay inventory | `inventory/inventory-4-w0/report.json`: passed across four peers |
| Duel Relay inventory | `duel-retry/inventory-2-w0/report.json`: passed across two peers |
| Duel and Multi Relay input | `duel-retry/input-2-w0/report.json`, `multi-input/input-4-w0/report.json`: passed |
| Four-player 12-copy layout | `layout/layout-4-w1/report.json`: passed; 12 local / 36 remote item views per peer, matching inventory signatures across all four seats |
| Solo live rendering and consumption/refill | `solo-before.json`, `solo-after.json`, `solo-anchor-check.json`: three ice creams and a mask have four distinct positions and empty labels. Consume copy 62, compact and grant copy 64: other three positions/uses unchanged; replacement occupies the vacated cell |

Relative runtime evidence paths above are under `output/validation/plan071/`. Screenshots are actual Unity camera/UI diagnostic renders, not generated mockups: `solo-duplicates.png`, `solo-refill.png`, `layout/layout-4-w1/seat0.layout-full.png` through `seat3.layout-full.png`. Visually inspected Solo and Multi seats 0 and 2; remaining perspectives have automated layout evidence.

## Failures, qualifications and remaining checks

**Follow-up:** the Editor Stop lobby-load exception recorded below was subsequently fixed in [PLAN073](PLAN_073_results.md), with two Stop cycles, normal Solo return/relaunch and Relay host-exit return verified.

- Initial duel inventory runner reported failure because its client connection-complete marker was absent. Both peers exited 0 with matching final inventory and passing inventory assertions. The unchanged fresh rerun includes Relay verification and passed. Retained first failure at `inventory/inventory-2-w0/report.json`; likely short-fixture/connection logging timing, not established as fixed. `checkpoints_equal:false` is expected for this fixture because it emits no presentation CHECK records; matching final states are separate evidence.
- Editor Stop after the Solo fixture emitted two `Lobby scene could not load` exceptions from session shutdown attempting a lobby return during Play Mode teardown. This was outside inventory execution; recorded for a separate lifecycle fix. Do not claim an entirely error-free Editor session.
- Existing build warnings remain (font importer, Pipeline runtime configuration, two unused fields). Existing unrelated whitespace at `Assets/Scenes/LobbyScene.unity:9166` remains; no new scoped whitespace defect found.
- These are focused fixtures and input regressions, not another 50-match balance campaign. Additional duplicates consume inventory capacity, so later long-form balance observations must use this new policy.
- Final Editor is stopped in clean `LobbyScene`, compilation is not failed. No persistent scene fixture edits, commits or pushes were made.

## Media

User cancelled ongoing recapture. Recording runner/recorder were stopped; `output/media-delivery-20261012-30s/progress.json` now says `stopped-by-user`. Partial files are preserved and are not a completed delivery. Do not restart recording without a new user request.
