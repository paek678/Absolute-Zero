# PLAN 048 - Fixed mode maps and night environment selection

## Approved direction (2026-10-06)

User requests day backgrounds for Solo and 1v1, night for four-player, and exclusion of daytime-only environment effects at night. Multi includes three-player starts and four-to-three continuation; neither changes its map or pool when seats leave or die.

| Requirement | Owner/change | Validation |
|---|---|---|
| Solo and 1v1 day | Bake existing PavilionDay through MapPresetAuthoring into GameScene and GameScene_Solo | Scene binding, lighting, anchor/reference checks; Solo and Relay duel runtime |
| Multi night | Retain existing PavilionNight in GameScene_Multi | Scene binding; Relay three/four-player runtime |
| Night excludes daytime staging | EnvironmentCalculations mode-specific pool; TurnManager passes initialized game mode at server draw | Exhaustive pool membership, one-draw RNG behavior; replicated environment assertions |

Night excludes SunnyDay (sunlight), CicadaSong (daytime cicada presentation), HeatWaveWarning (strong heat lighting/cicada staging). Heat can occur at night, but this project's current presentation is daytime-oriented; exclude rather than rename or introduce a new heat effect.

Keep CoolBreeze, Kids, Ambulance, SummerVacation at night: wind, passersby, emergency services and shorter preparation time are not inherently daytime-only. Day keeps all seven original effects, order and equal weights. Night samples four allowed effects equally with exactly one random draw (no rejection loop).

No item balance, prep timing, effect strength, seat/camera placement, outcome rules, lobby art or network schema changes. Cool Breeze retains authored moonlight in Multi instead of overriding it with daytime intensity 1.8; its wind effect and gameplay remain unchanged. Existing server environment publication remains the only replicated owner. No runtime map-switch manager is added; the fixed scene assets determine the map on normal entry, rematch and round restart.

## Execution

1. Preserve dirty assets and inspect map overrides; apply existing day map to the two duel scenes only.
2. Add mode-scoped authoritative selection and focused tests.
3. Compile and test; rebuild the existing firewall-approved player; exercise Solo and Relay 2/3/4-player cases with map/environment evidence.
4. Record executed results, limitations and captures in `Docs/Validation/PLAN_048_results.md`.
