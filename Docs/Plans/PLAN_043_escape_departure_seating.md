# PLAN 043 — Match menu, departure rules and circular seating

## Approved scope (2026-10-04)

- Escape opens a Korean return-to-lobby dialog in Solo, Duel and Multi. Opening it does not pause clocks, mini-games or combat. Continue/Escape closes it; an explicit return button leaves.
- Solo leaves locally. Online Duel ends for both when a participant leaves. Multi continues after a remote departure only while at least three connected participants remain (alive and ghosts both count).
- The user explicitly approved the host exception: host departure returns everyone to the lobby. Host migration is excluded.
- All Multi viewers share the same fixed circular seat order. Local seat is south; subsequent seats clockwise occupy west, north, east. Departure leaves a hole; identities and surviving positions are never compacted. Duel/Solo retain the sole opposite visual slot.
- Existing map anchors, cosmetics, item rules and unrelated dirty changes are preserved.

## Findings and implementation tasks

| Task | Owner / finding | Change | Acceptance |
|---|---|---|---|
| T1 projection | AZPlayerVisual.GetRemoteVisualSlot removes local from sorted seats, changing neighbours across viewers | Relative circular projection shared by visual, target, inventory and temperature bindings; explicit Duel mapping | All ordered seat pairs; opposite symmetry; missing seat stability; Duel/Solo regression |
| T2 departure | MatchRoster captures/despawns but no minimum connected-player exit policy; scene visual remains visible after despawn | Scene-scoped departure observer delegates cleanup to existing session router; hide released visual and decorations | 4→3 continues; 3→2 and Duel terminate; host exception; alive/ghost departure; repeated callbacks |
| T3 menu | No in-match Escape menu; Escape currently cancels aiming | Non-pausing uGUI modal with local input suppression and existing session exit path | Open/close/confirm, no click/keyboard leakage, timers continue, mini-game/attack/result boundaries, all modes |
| T4 runtime | Existing matrix expects older departure behaviour in some cases | Focused Editor tests, current build, independent-player UI/departure/circular-seat checks, console and captures | Record actual evidence and limits separately in validation ledger |

## Architecture and safety

The existing session router remains the single cleanup owner. The host evaluates connected participants, never living-player count. No new phase writer, client-authoritative gameplay state, RPC identity convention, host migration or Unity package is introduced. Presentation input blocking is local and never affects timer/result submission.

## Status

T1–T4 implemented and the scoped automated gates passed on 2026-10-04. Editor tests: 1017/1017. Actual Relay: 11 menu/departure runs plus 3 input-regression runs (48 peer-process observations); Solo and local-UTP checks also passed. Departure during preparation, attack, mini-game, ghost state and result display, plus the next round with three players, were exercised.

Baseline hashes: `output/validation/plan043/baseline-sha256.json`. Exact evidence, retained diagnostics and external-PC/long-soak limits are in [the validation ledger](../Validation/PLAN_043_results.md).
