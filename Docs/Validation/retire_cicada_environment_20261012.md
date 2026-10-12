# Retire the standalone cicada environment

## Decision and cause

The user confirmed on 2026-10-12 that cicadas are an audio element, not a standalone environment with a gameplay effect. The old day pool still contained `CicadaSong`, and GAME_DESIGN, HUD text, audio dispatch and tests retained that obsolete design. Multi's night pool already excluded it. The preceding camera review checked movement but missed this eligibility issue.

## Changes

- Removed CicadaSong from the shared Solo/1v1 random pool. Day now has six equally weighted environments; night retains four. Server selection still uses one random draw via `EnvironmentRuleService.SelectRandom`.
- Removed the standalone announcement text, display name, diagnostic message and audio dispatch. The HUD explicitly ignores the retired value.
- Preserved `SFX_cicada` for SunnyDay and HeatWaveWarning. Settings audio validation now exercises SunnyDay instead of the retired event.
- Kept enum value 3 reserved so later serialized/network enum values do not shift. Legacy temperature behavior remains inert. The opt-in camera suite may still inject this reserved value to check zero camera movement; it is not a playable environment.
- Updated pool expectations in existing Plan037/Plan048 tests and made the runtime map probe reject CicadaSong in all modes.
- Updated the canonical GAME_DESIGN environment list and staging description. Historical reports describe their original checks and are not rewritten as new results.

## Validation

- Correct Unity project verified through CLI; automatic compilation completed without failure.
- `Plan048EnvironmentTests`: 6/6 passed. Includes 2,000 seeded draws per mode (6,000 total), exact eligible pool coverage, one-draw RNG behavior and three shipped scene/map checks.
- `Plan037TemperatureEnvironmentTests`: 32/32 passed, including default pool draw order and unchanged recovery/duration/third-turn behavior.
- Static call-site audit: production TurnManager selects through EnvironmentRuleService; no alternate production enum-wide random draw found in scoped Turn/Solo code.
- Focused whitespace diff check passed.
- Windows development build `31896cd119d549b182ccfccac4c07d40` succeeded: 0 errors / 4 warnings, updated the fixed `AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe` output.
- Evidence: `output/validation/retire-cicada/plan048-tests.json` and `plan037-tests.json`.

This is an Editor test and build verification; no new full-match multiplayer session or listening test was run for this change. Existing recordings retain their original content.
