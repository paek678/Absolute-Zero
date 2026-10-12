# Fixed inventory anchors validation — 2026-10-11

Evidence root: `output/validation/plan061/`.

- New fixed-slot regression tests: 5/5 passed (incremental 1–12 fill, consume/
  compaction/refill, round reset, excess stolen copies, stacked uses/duplicate types).
- Existing presentation identity tests: 9/9 passed.
- Existing map/grid/distribution tests: 7/7 passed.
- Windows development build succeeded, 0 errors / 4 warnings (`build.json`).
- Actual Relay matrix: input flow, 8-item layout and 12-item layout passed 3/3
  scenarios, 12 peer executions, all exit codes 0 (`relay/matrix.json`). All four
  full-inventory screenshots reviewed; item rendering, seat binding and input
  remain functional.
- Compared the first eight items' positions between separate 8-item and 12-item
  runs at every local/remote view: 128 entries, maximum delta 0, no mismatches
  (`geometry-comparison.json`). This is settled geometry evidence; the sequential
  append/consume/compact/refill lifecycle is covered by the focused unit tests.
- Runtime captures are 960x540. Full example:
  `relay/layout-4-w1/seat0.layout-full.png`; partial example:
  `relay/layout-4-w0/seat0.layout-full.png`.
- Editor remains clean LobbyScene/Edit Mode, test players exited. Existing dirty
  font and bot graph hashes preserved. No commit/push.

## Follow-up scope

Other resolutions and a full gameplay/rematch run are separate from these
focused inventory geometry and input checks.
