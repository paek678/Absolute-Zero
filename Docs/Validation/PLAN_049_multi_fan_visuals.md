# Multi standing fan and hair-wind removal

User direction: remove the standing fans and wind-blown hair presentation in four-player combat. Apply to the shared Multi mode (3–4 players); retain Solo and 1v1 visuals. This is a presentation change: preparation temperature loss/recovery, fan state replication, and Fan/HandFan attack items remain unchanged.

## Implementation

- `FanSpawner.SpawnStayItemFans`: skip spawning in Multi. `GameUIRoot` already waits for the synchronized match configuration before this call, so remote clients do not temporarily spawn duel fans before mode arrival.
- `AZPlayerVisual.SetWind`: gate the animator boolean by match mode. The per-frame refresh routes through this method, preventing replicated fan-active updates from re-enabling hair wind in Multi.
- No scene, prefab, animation clip, server state, RPC, camera, seat or balance changes. Mode is fixed independently of survivor/player count.
- Extend the existing opt-in development map probe with `--az-fan-check` observations: standing fan component count and each bound remote animator's wind boolean versus authoritative state. Normal/release play does not activate this probe.

## Evidence

Root: `output/validation/plan049/`.

- Unity project/connection verified; automatic compilation passed. Windows development build succeeded with 0 errors and 4 existing warning categories (`build.json`).
- Real Relay four-player ordinary sequence: two completed combats, synchronized checkpoints and target input pass. Every peer observed zero standing fans and all three remote wind flags off in preparation/combat/resolution, including the CoolBreeze environment (`natural-input/`). Host diagnostic capture `host.map-2.png` visually inspected.
- Real Relay 1v1 input flow passed. Both peers retained two standing fans; remote wind followed fan-active state (`input-2-w0/`).
- Solo BT regression passed; two fans retained and wind follows active state. Final aggregate: 3/3 scenarios, seven processes, all fan/wind observations pass (`summary.json`).
- Focused diff whitespace check passed. No new unit tests for this small visual gate; current build and runtime observations are the relevant checks. Prior 1,071-test run belongs to PLAN048 and is not claimed as rerun for this change.

## Deferred checks

| ID | Status | Follow-up |
|---|---|---|
| F49-01 | Deferred broader QA | Human visual review on another PC/resolution and with all cosmetic hairstyles. The current check uses existing animated characters and checks their real Animator parameters; it does not certify every future sprite. |
| F49-02 | Static coverage only this change | Three-player start and 4→3 departure retain `GameMode.Multi`, so the same visual gate remains active. Those transitions were not separately rerun for this small change. |
