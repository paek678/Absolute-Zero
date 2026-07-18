# PLAN_009: Environment Variable System

**Status:** ✅ Complete
**Created:** 2026-07-18

## Goal
Implement environment variable system that activates from Turn 2, with camera pan announcement and per-environment effects.

## Design Spec (from GAME_DESIGN.md)
- Appears at 2nd prep phase of each round. Random. Resets per round.
- 7 environments: SunnyDay, CoolBreeze, CicadaSong, Kids, Ambulance, SummerVacation, HeatWaveWarning

## Tasks

### TurnManager.cs
- [x] Add `NetworkVariable<EnvironmentType> ActiveEnvironment`
- [x] Add `OnEnvironmentAnnounced` static event
- [x] Add `EnvironmentAnnouncementRoutine()` — random pick + ClientRpc + wait
- [x] Add `AnnounceEnvironmentClientRpc()` — camera pan + text trigger
- [x] Add `EnvironmentCameraRoutine()` — rotate left, hold, rotate back
- [x] Add `GetEnvironmentName()` helper (Korean names)
- [x] Modify `ResolutionPhaseRoutine` — insert announcement after Turn 1
- [x] Modify `PrepPhaseRoutine` — SummerVacation prep time, SunnyDay/CoolBreeze recovery rate
- [x] Add Kids effect — remove 1 random unused item per player per turn
- [x] Add Ambulance effect — Turn 4 lower-temp player +10°
- [x] Reset `ActiveEnvironment` in `StartNextRound`

### CombatResolver.cs
- [x] Add `EnvironmentType` parameter to `Resolve()` and `DetermineOrder()`
- [x] HeatWaveWarning — lower-temp player acts first

### AZGameUI.cs
- [x] Add environment announcement text (top-center, large)
- [x] Subscribe to `OnEnvironmentAnnounced` event
- [x] Show/hide with coroutine

## Will NOT Touch
- ItemEnums.cs (EnvironmentType already defined)
- ItemContext.cs (ActiveEnvironment field already exists)
- Scene files
- Item SO assets

## Discovered Issues
(none yet)
