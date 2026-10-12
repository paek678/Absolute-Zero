# Environment camera: character introductions only

## Decision and implementation

User decision on 2026-10-12: do not turn left for an environment unless it introduces a character. Applies to Solo, 1v1 and Multi.

- Kids and Ambulance retain the existing left pan: 25 degrees in Solo/1v1, 3 degrees in Multi, then return.
- None, SunnyDay, CoolBreeze, CicadaSong, SummerVacation and HeatWaveWarning have zero pan.
- Announcement text, VFX, gameplay effects and environment eligibility remain unchanged.
- `DuelSpriteFacing.EnvironmentPanDegrees(mode, environment)` owns the filter. `TurnManager.AnnounceEnvironmentClientRpc` restores any previous pan before publishing the next announcement, then starts a pan only when required.
- Existing screen-facing sprite/UI rendering and pan timing are preserved. `GAME_DESIGN.md` staging section now reflects this decision.

## Validation

- Unity Editor auto-compilation passed; Editor remains in LobbyScene, Edit Mode.
- Latest Windows development build: `32470b0017234e5faf5aa37d4df5afe7`, succeeded, 0 errors / 5 warnings. Warnings: missing optional RuntimePipelineConfig, MalgunGothic SDF importer consistency, pending editor-code/postprocessing compilation notice, and two existing unused fields. Player runtime tests below used this build.
- Final runtime suite: Solo (one process), Relay 1v1 (two processes), Relay Multi (four processes), eight environment cases per process: **56/56 passed**. All seven processes exited with code 0.
- Expected angles measured: character cases 25/3 degrees, all other cases zero. Original rotation restored within 0.01 degrees; screen-facing artwork/world UI and horizontal floor slots remained within 0.1 degrees. Values in result files round to 0.000 for restore/plane error.
- Inspected actual captures of stationary SunnyDay and HeatWaveWarning, plus Multi Kids with its character introduction.
- Focused `git diff --check` passed.

## Fixture and limits

The explicit development-only `--az-camera-environment-suite` extends `Plan056CameraProbe`. It injects all eight announcements through the server's existing publication path, including events normally excluded from a map, to validate presentation dispatch rather than random environment selection. It does not change the normal eligible environment pool.

The first Multi attempt overlapped its Kids case with a naturally selected Ambulance at the next turn, so all four restore measurements failed while the newer pan was active. Logs retained under `runtime`. The final opt-in fixture stops the host turn coroutine before injecting the cases, preventing automatic announcements during measurement; production play never uses this option. Final results are under `runtime-final`. This is focused camera/presentation coverage, not a complete gameplay regression or fresh promotional recording.

Evidence: `output/validation/environment-camera-policy/runtime-final/summary.json`, per-peer `*.camera-*-result.txt` and PNG captures. Runner: `output/validation/environment-camera-policy/run_final.py`. Existing promotional videos and desktop copies were not replaced.
