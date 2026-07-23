# PLAN_014 — 3P Item Sprite Binding

> **Status:** ✅ Complete
> **Created:** 2026-07-23
> **Goal:** Connect item sprites to 3P character's `item` child object during combat animations

---

## Problem

EnemyPlayer prefab has an `item` child with SpriteRenderer. 8 animation clips control its Position + IsActive.
But AZPlayerVisual has **zero code** to find/set the item child's sprite.
Result: item object activates during animation but shows default/empty sprite.

## Scope

### Will Modify
- `Assets/Scripts/Core/Player/AZPlayerVisual.cs` — item child cache + SetItemSprite/ClearItemSprite
- `Assets/Scripts/Core/Combat/CombatVFXManager.cs` — call SetItemSprite before 3P PlayCombatAnimation
- `Assets/Scripts/Core/Common/GameSprites.cs` — add 4 missing items to ItemSpriteMap

### Will NOT Touch
- FPSVisualController (1P system — already working)
- Animation clips (item curves already exist)
- ItemWorldView / ItemWorldDisplay (world item display — separate system)
- Cat sprite sequence (independent path)

---

## Tasks

- [x] Create plan file
- [x] AZPlayerVisual: cache `item` child Transform + SpriteRenderer in OnNetworkSpawn
- [x] AZPlayerVisual: add `SetItemSprite(Sprite)` and `ClearItemSprite()` methods
- [x] AZPlayerVisual: call `ClearItemSprite()` in `ReturnToIdle()`
- [x] CombatVFXManager: call `SetItemSprite` before `PlayCombatAnimation` when `!isLocalUser`
- [x] CombatVFXManager: ensure ClearItemSprite on all exit paths (normal + death + defence)
- [x] GameSprites: add Samgyetang, Mask, Screwdriver, Claw Machine to ItemSpriteMap
- [x] Update ACTIVE_CONTEXT + RECENT_CHANGES

## Edge Cases Handled
- Items with no item curves (defence, hug): sprite set but item stays IsActive=0 → invisible → safe
- Cat: separate path, never reaches SetItemSprite
- swing1 SpriteHash curve: likely inert without SpriteResolver component → monitor at runtime
- Feed reaction: creates separate GO for target, independent of user's item child
- Missing art (4 items): GetItemSprite returns fallback → placeholder shown
