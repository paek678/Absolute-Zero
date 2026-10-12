# PLAN073 — Observed media defects fixed

Date: 2026-10-12. Scope: MEDIA-WIND-01 and SESSION-EXIT-01 only.

## Defects and changes

| ID | Cause and correction | Result |
|---|---|---|
| MEDIA-WIND-01 | CoolBreeze enabled the legacy `Cooler` ITEM_FAN sprite at absolute duel coordinates, scale 4, while the Multi camera sits at a different position. Its animated sprite crossed the camera foreground and produced the huge pale/black obstruction. Gate this duel prop out of Multi in both activation paths; keep wind particles, night lighting and camera presentation unchanged. | Fixed; four fresh peer views show unobscured CoolBreeze. |
| SESSION-EXIT-01 | Solo teardown requested LobbyScene during Editor Play Mode exit. Add application-lifetime guards at concrete Solo and online menu-loading boundaries. NGO shutdown and session ownership cleanup still execute. | Fixed; two Editor Stop cycles produce no new errors. Normal Solo return/relaunch and online host-disconnect return pass. |

The initial wind-shader hypothesis was not confirmed: both Editor and player used supported URP particle shaders. No particle shader/material rewrite was needed. Baseline isolation captures are diagnostic observations at different animation frames, not pixel-identical comparisons. The decisive regression evidence is the same forced CoolBreeze in a fresh build with only the Multi prop activation changed.

## Current validation

Evidence root: `output/validation/media-fixes/`.

| Check | Observed result | Evidence |
|---|---|---|
| Ordinary import / player build | Succeeded; 0 errors, 4 existing warnings | `build-final-status.json`, job `17dce87b48aa4f6ebeda34f49c4b88d2` |
| Multi host + three clients | All four exited 0; four announcement screenshots visually inspected. All reports: cooler disabled, wind particles present, reset and replay true | `after/raw/multi/{0,1,2,3}/player.wind*` |
| Solo and duel host/client | All three exited 0. Existing duel prop remains enabled; wind reset/replay true. Solo and duel host screenshots visually inspected | `after/raw/solo/0/player.wind*`, `after/raw/duel-natural/{0,1}/player.wind*` |
| Normal Solo menu return and second launch | Passed; LobbyScene loaded, NGO stopped, second game spawned | `lifecycle-first.json` |
| Two Editor Stop cycles | No new error entries since cursor 5041; same console session, no log clearing | `lifecycle-second.json`, `console-stop-first.json`, `console-stop-second.json` |
| Online host exit | Actual two-peer Relay host-disconnect fixture passed; surviving client returned to LobbyScene | `online-exit/host-exit-2-w0/report.json` |
| Scoped whitespace | `git diff --check` passed for modified production files | Terminal check |

Built executable: `C:/Users/paek6/AppData/Local/Temp/AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe`, 399492480 reported build bytes. Existing allowed path reused. Editor ends in LobbyScene, Edit Mode, clean scene. No commit/push.

## Reproduction and limits

- `WindPresentationProbe` runs only in Editor/development builds with `--az-wind-check`. It forces CoolBreeze, records actual screenshots, temporarily isolates renderers, and checks local VFX reset/replay. It never runs in normal gameplay. The capture runner and lifecycle script are saved under the evidence root.
- Before: `isolate/raw/multi/0/player.wind-0.png`. After: `after/raw/multi/0/player.wind-0.png`.
- Runtime captures are focused presentation/lifecycle checks, not a complete match or balance campaign. They do not establish every possible simultaneous disconnect condition.
- Existing font/Pipeline/unused-field build warnings remain. Shutdown output also includes a ComputeBuffer disposal warning; its resource owner has not been established by this scoped fix. Do not describe all logs as warning-free.
- The earlier seven delivered media files remain the earlier captures; this task fixes code and creates validation captures, rather than replacing that complete media package.

## Deferred observation ledger

| ID | Status | Next evidence needed |
|---|---|---|
| RESOURCE-EXIT-01 | Observed shutdown warning, untriaged; not a reproduced gameplay failure | Reproduce with recorder disabled and capture allocation/resource ownership before deciding on cleanup changes. |
