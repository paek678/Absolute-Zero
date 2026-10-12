# Thermometer-attached combat icons — 2026-10-12

## Change

- Attacker, recipient and possession indicators are now `Image` children of the corresponding thermometer's `RectTransform`. The former separate overlay canvases, animated character-bounds projection and frame-by-frame collision relocation are removed from `CombatRoleIconPresenter`.
- A role uses slot 0 and the persistent debuff uses slot 1 beneath the remote thermometer. The local debuff uses slot 0 beneath the local thermometer. Slots stay fixed when an attack starts or ends. Local attacker/recipient visibility remains suppressed.
- Remote icons use 120 thermometer-local units; the local HUD icon uses 64 reference units. World-space indicators inherit the thermometer's camera facing and scale.
- Remove `MyTemp` and `OppTemp` creation and their unused references/writes for the common HUD used by solo, duel and Multi. Temperature simulation, slider animation and color behavior remain intact.
- Explicitly destroy reparented icons when their presenter is destroyed. No authority, RPC, cooldown or debuff-duration changes.

## Validation

- Normal Unity import/compilation passed. Build `0ca6f4512a5247d1bb66745392a9aba3` succeeded with 0 errors and 4 warnings.
- Development-only possession probe now checks actual UI parent, stable anchored position, visibility against the replicated mask, absence of numeric labels and absence of the old persistent ring.
- Role capture probe checks actual thermometer parenting and absent labels alongside existing role visibility and cat-rack assertions.
- Real Relay four-player round-revival: `output/validation/ghost-skill-icons/thermometer-icons-round/report.json` passed; all four peers logged active, persisted and cleared status with stable local anchors and no numeric temperature labels.
- Real Relay four-player role/cat matrix: `output/validation/plan069/thermometer-fixed/result-4.json` passed; 432 role/layout/cat assertions, zero failed assertions across four peers.
- Actual 1600×900 framebuffer captures inspected: four-player observer sees both remote attacker and recipient in fixed thermometer slots; front/side/local possession captures show no numeric temperatures. No sprite-bound-driven relocation remains.
- Real Relay duel role/cat matrix: `output/validation/plan069/thermometer-fixed-duel/result-2.json` passed; 70 observed role/layout/cat assertions, zero failed assertions. Actual `client1.role-01-Cat-item-start.png` confirms thermometer-attached attack icon and no numerical temperature labels.
- Editor left on LobbyScene, EditMode, clean, not compiling. No commit/push or unrelated asset edits.

## Deferred checks

- TI-01: Camera rotation and unusual aspect ratios; inspect fixed children throughout a full environment pan and at non-16:9 aspect ratios.
- TI-02: Dedicated simultaneous role plus possession stimulus; confirm both fixed slots are readable on the same thermometer.
- TI-03: A dedicated solo-bot capture; common builder/presenter implementation is shared, but duel screenshots alone are not separate solo runtime evidence.
