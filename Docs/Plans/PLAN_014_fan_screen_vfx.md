# PLAN_014 — Fan Animation + Hit/Recovery Screen VFX (+ Music/Lobby)

> Status: ✅ Complete (PR open, review/merge pending)
> Branch: `feature/fan-vfx` (branched from `main`)
> PR: https://github.com/paek678/Absolute-Zero/pull/4
> Commits: `7f1aa26`, `28a9834`, `ad2f71a`, `4e1545a`

---

## Overview

Replace the placeholder stay-item fan with animated art (body + spinning blades + grille),
add full-screen hit/recovery VFX, and fix two smaller issues (music re-entry bug, lobby code copy QoL).

Work item classification (per accurate wording):

| Item | Type |
|------|------|
| Fan art animation replacement | Feature |
| Fan rendering technique (squash / depth / scale) | Feature |
| Hit/Recovery screen VFX | Feature |
| Lobby join-code click-to-copy | QoL feature (NOT a bug fix) |
| Music silent on game re-entry | **Bug fix** |

---

## 1. Fan — animated stay-item art

### Source & slicing
- `Assets/Resources/FAN.png` (512×512) is a **parts sheet**, not an animation frame sheet.
- Sliced (Python/PIL, background-only removal via corner flood-fill to preserve internal white) into
  3 sprites in `Assets/Resources/Fan/` (PPU 100, pivot center):
  - `fan_body.png` 175×300 — stand + motor + faint guard outline
  - `fan_blades.png` 152×152 — 4-blade propeller, **re-centered so the hub is at the sprite center**
    (original centroid was (71.7, 73.1) → off-center → caused wobble; re-canvassed to center the hub)
  - `fan_grille.png` 155×183 — front guard (static cover)

### Runtime construction (`AZGameUI.SpawnStayItemFans` / `SpawnFanAt`)
The fan is **created at runtime** as a child of scene marker objects `PlayerStayItem` / `EnemyStayItem`
(previously it drew a placeholder sprite `objtest1_8`). Hierarchy:

```
{marker}_Fan        ← body SR (sortingOrder 3), localScale = s (per-marker)
 └─ Head            ← empty, localPos = head center (unifies blade/grille alignment)
     ├─ Grille      ← SR (sortingOrder 5), static cover
     └─ BladePivot  ← empty, localScale.x = 0.82  (perspective squash)
         └─ Blades  ← SR (sortingOrder 4), FanBladeSpinner rotates this on Z
```

### Key rendering decisions
- **Per-marker scale, not a uniform shrink.** The player marker sits close to the camera
  (world ≈ (3.11, 1.16, −1.53), distance ≈ 4.6) and the enemy marker far (≈ (2.62, 0.36, 7.32),
  distance ≈ 12.5) → ~2.7× perspective size difference. A single FanRoot scale would fix one and
  break the other, so scale is per-marker: `playerFanScale = 0.54`, `enemyFanScale = 0.95`.
- **Lift proportional to scale.** Body pivot is centered, so it must be raised to sit on the table:
  `localPosition.y = fanLiftBase (1.4) × scale`. Not scaling the lift makes the fan float.
- **Blade perspective = parent X-squash, not a Y tilt.** The blade sprite is drawn flat/symmetric
  (a rotated/perspective drawing looks lumpy). To make it read as 3/4, apply an X-scale of
  `cos35° ≈ 0.82`. The squash MUST live on the **parent** (`BladePivot`), not on `Blades`:
  - Unity applies child transforms before parent transforms → "rotate then squash":
    the round propeller spins on Z (smooth, it's a disc), then the parent squashes it horizontally.
    The ellipse outline stays fixed; only the blades rotate inside (correct, coin-spin trick).
  - Squashing the blades themselves would be "squash then rotate" → the ellipse rotates with the
    blades → wobble. (This subtlety caused a back-and-forth; parent squash is the correct one.)
  - This reproduces the exact on-screen look of a `Euler(0, 35, 0)` tilt while keeping the blade
    **plane parallel to the grille/body**, so there is no depth-buffer poke-through — no ZWrite/ZTest
    hacks needed (`sprite3DMat` lighting preserved).
- **Head pivot** unifies blade + grille center so alignment is a single tunable.

### FanBladeSpinner (`Core/Common/FanBladeSpinner.cs`)
- Rotates the bound blade transform only while that player's `PlayerState.IsFanActive` is true
  (same signal as the hair-wind), decelerates smoothly to a stop on Ready.
- Player binding: `-1` = local, `-2` = opponent, `>=0` = specific clientId. Guards against
  `SpawnManager.SpawnedObjects` being null before/after the network session (`IsListening`).
- **Rotation overflow prevention:** tracks its own angle wrapped to `[0, 360)` and sets
  `localRotation = baseRotation × Euler(0,0,angle)` each frame, instead of accumulating on the
  Transform (which grows the editor euler hint unbounded).

### Live tuning
- All fan values are `[SerializeField]` on `AZGameUI` (playerFanScale, enemyFanScale, fanLiftBase,
  fanHeadCenter, fanBladeScale, fanBladeSquashX, fanGrilleScale, blade/grille Z).
- `OnValidate` (play mode) → `ReapplyFanTuning` re-applies to the two spawned fans instantly,
  so values can be dialed in during play (then Copy/Paste Component Values to persist before Stop).

### Test scene
- `Assets/Scenes/FanTestScene.unity` — camera + light + one assembled fan; `spinWhenNoState = true`
  makes the blades spin without a network session. NOTE: it uses a flat ortho camera, which does NOT
  match the in-game angled perspective (persp FOV 67, pos (0,3.5,−4), 16° down) + 30°-tilted markers,
  so final sizing/positioning was tuned live in GameScene, not here.

---

## 2. Hit / Recovery screen VFX

### `Core/Combat/ScreenVFXManager.cs`
- Lazy singleton; builds its own ScreenSpaceOverlay canvas (sortingOrder 200) with two full-screen
  `RawImage`s from `Resources/background_VFX1` (icy frost frame) and `background_VFX2` (warm frame).
- `PlayHitVFX()` / `PlayRecoveryVFX()` fade the frame in → hold → out (~0.5s). Restarting the
  coroutine on repeat calls yields a sustained flash for multi-hit items rather than stacking.

### Hooks (`Core/Combat/CombatVFXManager.cs`)
Category-intent based, local player only (fires on the client where it matters):
- **Frost (被弾)** when the local player is the target of **Attack or Debuff**
  (Ice Cream, Iced Americano, Hug T-shirt, Samgyetang).
- **Warm (recovery)** when the local player uses **Recovery or Buff**
  (Hot Americano, Hot Pack, Smartphone, Warm Tea, **Buldak** & Soda — buffs are self-heals).
- Attack/Recovery are hooked at the existing damage/recovery reaction points; Buff/Debuff are
  handled at the top of `PlayItemSequence` (they don't pass through those branches).
- Driven by combat events (item use), NOT raw temperature, so the fan's −1°/sec cooling never triggers it.

---

## 3. Music re-entry — bug fix

`AZGameUI.EnsureAudioManager` returned early when `GameAudioManager.Instance` already existed, so on
game → lobby → game the BGM (stopped by `StopBGM` on leaving) was never restarted. Fix: always call
`PlayBGM()` on game entry (it self-no-ops via `if (_bgmSource.isPlaying) return`).

---

## 4. Lobby join-code click-to-copy — QoL feature

`AZLobbyUI`: added a `Button` on the lobby code text → `GUIUtility.systemCopyBuffer = code`, label
changed to "Lobby Code (click to copy):", status feedback on copy.

---

## Files

| File | Type |
|------|------|
| `Assets/Resources/FAN.png`, `Fan/fan_body|blades|grille.png` | Added (sprites) |
| `Assets/Resources/background_VFX1|2.png` | Added (VFX frames) |
| `Assets/Scripts/Core/Common/FanBladeSpinner.cs` | Added |
| `Assets/Scripts/Core/Combat/ScreenVFXManager.cs` | Added |
| `Assets/Scenes/FanTestScene.unity` | Added |
| `Assets/Scripts/UI/Game/AZGameUI.cs` | Modified (fan spawn + music + live tuning) |
| `Assets/Scripts/UI/Lobby/AZLobbyUI.cs` | Modified (code copy) |
| `Assets/Scripts/Core/Combat/CombatVFXManager.cs` | Modified (screen VFX hooks) |

## Commits
1. `7f1aa26` fan animation replacement + rotation overflow prevention
2. `28a9834` fan rendering (parent squash / per-marker scale) + music re-entry fix + lobby code copy
3. `ad2f71a` hit/recovery screen VFX
4. `4e1545a` screen VFX buff/debuff coverage (Buldak self-heal, etc.)

## Notes / lessons
- Blade rotation center must be the sprite center — re-canvas the sprite so the hub is centered.
- For a spinning flat sprite that must look tilted, squash the **parent** (rotate-then-squash), not
  the child; this also avoids depth poke since the plane stays parallel.
- The fan test scene's flat ortho camera does not represent the in-game angled/perspective view —
  tune fan sizing live in GameScene.
- Amending an already-pushed commit diverged the remote; resolved with `reset --soft` to the remote
  tip + a new commit (no force-push).
