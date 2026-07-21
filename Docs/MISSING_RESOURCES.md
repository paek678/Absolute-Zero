# Missing Resources Checklist

> All resources needed to complete current systems. Check off as added.
> Last updated: 2026-07-21

---

## 1. Environment Sprites (`Assets/Resources/Environment/`)

| # | File | Used By | Purpose | Done |
|---|------|---------|---------|------|
| 1 | `kid_idle.png` | EnvironmentVFXManager.BuildKidGroup | Kids env — child character idle | [ ] |
| 2 | `rescue_ready.png` | EnvironmentVFXManager.BuildAmbulanceGroup | Ambulance env — rescue worker | [ ] |
| 3 | `sprite3DMat.mat` | EnvironmentVFXManager (all env objects) | 3D-lit sprite material | [ ] |

---

## 2. Item Sprites (`Assets/ItemSprite/`)

| # | File | Item (Korean) | Done |
|---|------|---------------|------|
| 1 | `Screwdriver.png` | 십자드라이버 | [ ] |
| 2 | `Samgyetang.png` | 삼계탕 | [ ] |
| 3 | `ClawMachine.png` | 집게손 | [ ] |
| 4 | `WaterGun.png` | 물총 | [ ] |
| 5 | `Mask.png` | 마스크 | [ ] |
| 6 | `RedCard.png` | 레드카드 | [ ] |

---

## 3. Animation Clips (Animator triggers)

| # | Trigger | Items Using | Description | Done |
|---|---------|-------------|-------------|------|
| 1 | `use` | Samgyetang, Windbreaker, WarmTea, HotPack, Umbrella, etc. | Generic use motion | [ ] |
| 2 | `gun` | Water Gun | Shooting motion | [ ] |
| 3 | `tape` | Blue Tape | Tape wrapping motion | [ ] |
| 4 | `fan` | Fan / HandFan | Fanning motion | [ ] |
| 5 | `mask` | Mask | Wearing mask motion | [ ] |

---

## 4. SFX (`Assets/Resources/Environment/` or Audio folder)

| # | ID | When | Environment/Item | Done |
|---|----|------|-----------------|------|
| 1 | SFX_cicada | On env announce | SunnyDay, CicadaSong | [ ] |
| 2 | SFX_wind | On env announce | CoolBreeze | [ ] |
| 3 | SFX_kidWhistle | Kids entrance (rise) | Kids | [ ] |
| 4 | SFX_kidSteal | Steal staging | Kids Turn 2+ | [ ] |
| 5 | SFX_siren | Ambulance entrance (slide) | Ambulance | [ ] |
| 6 | SFX_clock | Timer shake start | SummerVacation | [ ] |
| 7 | SFX_wear | Blanket overlay | Ambulance Turn 4 | [ ] |

---

## 5. Particle Systems

| # | Effect | Environment | Description | Done |
|---|--------|-------------|-------------|------|
| 1 | Wind particles | CoolBreeze | Flowing wind around scene | [ ] |

---

## 6. Mini-Game UIs (Code)

| # | MiniGameType | Item | Owner | Done |
|---|-------------|------|-------|------|
| 1 | HugCharacter | Hug T-shirt (안아줘요 티셔츠) | User (직접 제작) | [ ] |
| 2 | TapCard | Red Card (레드카드) | Deferred (추후 추가) | [ ] |

---

## 7. Animator Controllers

| # | File | Location | Purpose | Done |
|---|------|----------|---------|------|
| 1 | `coolerA.controller` | `Assets/Resources/` | Cooler animation (SunnyDay/CoolBreeze env) | [ ] |
| 2 | `teaA.controller` | `Assets/Resources/` | Tea animation (SunnyDay env) | [ ] |
| 3 | `catA.controller` | `Assets/Resources/` | Cat animation (Kids env) | [ ] |

---

## Notes

- Environment sprites are loaded via `Resources.Load<Sprite>("Environment/...")` — path update needed in EnvironmentVFXManager after adding files
- Item sprites are assigned to SO `.Icon` field in Inspector
- Animation triggers must match SO `.AnimTrigger` / `.OpponentAnimTrigger` field values
- SFX playback code not yet implemented — placeholder comments exist in EnvironmentVFXManager
