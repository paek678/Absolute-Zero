# PLAN_010 — Basic-Item Bug Fix + WarioWare Banner + Mini-Game Minimal Reface

> Status: 🔄 In Progress
> Branch: `feature/minigame-greybox`
> Approved by user 2026-07-18 ("레츠고"). Icon-tap confirmed non-conflicting with design (minigame table already describes icon reacting to taps).

## Checklist

### ① Basic items showing Empty/gray on local row
- [x] TurnManager.WaitForPlayersRoutine: require `IsSpawned` on both players before initializing (root-cause class: pre-spawn NetworkList/NV writes — matches the "NetworkVariable written before spawn" warnings seen in play console)
- [x] ItemWorldView.UpdateDisplay: refresh SPRITE on slot change (confirmed bug — only label updated; grants/rerolls kept stale image)
- [x] Client replication race: ItemWorldDisplay.TryInitialize + AZGameUI.TrySpawnOpponentItems wait until all 12 slots replicated
- [ ] Play-verify (user): both rows show basics with art + randoms as gray squares

### ③ Screwdriver difficulty
- [x] Asset MiniGameGoal 3 → 1 (1 rotation per screw × 3 screws in 7s) — MCP patched

### ② WarioWare banner + minimal reface (+ user refinement: icon-tap, 3 screws side-by-side)
- [x] MiniGameUIBase: big panel/title/timer-bar removed → light dim(0.35) + thin top timer line + banner routine (top line → text → bottom line, staggered smoothstep slide in from LEFT, 0.5s hold, slide out RIGHT same order); `CreateIconTapTarget` helper (padded invisible hitbox + visual icon child)
- [x] HotPack: tappable pack icon (hit 380² / icon 260²), reddens+grows, counter under, banner "탭해라!"
- [x] Buldak: tappable pot icon + gauge bar + %, banner "끓여라!"
- [x] Screwdriver: 3 screws side-by-side, active = yellow border, done = GREEN border (user request), rotation on active screw, center deadzone 10px, banner "조여라!"
- [x] Compile 0 errors
- [ ] Play-verify (user): banner motion, icon tap feel, screw difficulty
