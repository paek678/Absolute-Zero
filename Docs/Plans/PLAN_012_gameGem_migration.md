# PLAN_012 — gameGem Asset Migration + Integration

> Status: ✅ Complete (Phase 1-3 Done, Phase 4 Optional)
> Source: `C:\Users\paek6\Downloads\gameGem-main\gameGem-main\Assets\`
> Target: `C:\Users\paek6\Absolute Zero\Assets\`

---

## Phase 1: File Copy (30 files)

### 1.1 — FPS 1인칭 시스템 (21 files)

**Copy from** `gameGem/MainFolder/Animations/FPS/` **→** `Assets/Art/Animations/FPS/`

| # | File | Type |
|---|------|------|
| 1 | `FPSA.controller` | Animator Controller |
| 2-13 | `FPS_card/defence/eat/fan/feed/gun/hug/mask/swing/tape/tarrot/useItem.anim` | Animation Clips (12) |

**Copy from** `gameGem/MainFolder/Sprite/FPS/` **→** `Assets/Art/Sprites/FPS/`

| # | File | Type |
|---|------|------|
| 14-21 | `FPS_card/eat/fan/gun/hug/mask/tape/ziper.png` | Sprite Sheets (8) |

**Why:** FPS anims use transform animation of 4 children (`hand1`, `hand2`, `item`, `Particle`) — positions/rotations/scales, NOT sprite-swap. Each action sets the `item` child's sprite statically to the matching FPS_*.png, then the `.anim` clip animates hand movements.

### 1.2 — Kids NPC Animator (4 files)

**Copy from** `gameGem/MainFolder/Animations/` **→** `Assets/Resources/Environment/`

| # | File | States |
|---|------|--------|
| 1 | `kidA.controller` | skew(default) ↔ ready ↔ steal |
| 2-4 | `kidA_ready.anim`, `kidA_skew.anim`, `kidA_steal.anim` | 3 clips |

### 1.3 — Rescue Worker Animator (3 files)

**Copy from** `gameGem/MainFolder/Animations/` **→** `Assets/Resources/Environment/`

| # | File | States |
|---|------|--------|
| 1 | `rescueA.controller` | ready(default) → complete |
| 2-3 | `rescueA_ready.anim`, `rescueA_complete.anim` | 2 clips |

### 1.4 — Particle Sprites (2 files)

**Copy from** `gameGem/MainFolder/Sprite/` **→** `Assets/Art/Sprites/`

| # | File | Purpose |
|---|------|---------|
| 1 | `particle_food.png` | Food item particle |
| 2 | `smoothParticle.png` | Generic smooth particle |

---

## Phase 2: Environment VFX Code Wiring

### 2.1 — EnvironmentVFXManager: Sprite Path Update

**File:** `Assets/Scripts/Core/Combat/EnvironmentVFXManager.cs`

현재 `Resources/` 루트에서 로드 → `Resources/Environment/`로 경로 변경

```
// BuildKidGroup() — line 343
BEFORE: Resources.Load<Sprite>("kid_idle")
AFTER:  Resources.Load<Sprite>("Environment/kid_idle")

// BuildAmbulanceGroup() — line 373
BEFORE: Resources.Load<Sprite>("rescue_ready")
AFTER:  Resources.Load<Sprite>("Environment/rescue_ready")
```

**작업:** 기존 `Resources/kid_idle.png`, `rescue_ready.png` 등을 `Resources/Environment/`로 이동

이동할 파일:
- `Resources/kid_idle.png` → `Resources/Environment/kid_idle.png`
- `Resources/kid_rob.png` → `Resources/Environment/kid_rob.png`
- `Resources/kid_skew.png` → `Resources/Environment/kid_skew.png`
- `Resources/rescue_ready.png` → `Resources/Environment/rescue_ready.png`
- `Resources/rescue_save.png` → `Resources/Environment/rescue_save.png`

### 2.2 — EnvironmentVFXManager: Kids Animator 추가

**BuildKidGroup()** 수정 — kid 오브젝트에 Animator 컴포넌트 추가:

```csharp
// 현재 코드 (sprite만 설정):
var sr = go.AddComponent<SpriteRenderer>();
sr.sprite = idleSprite;

// 추가할 코드:
var kidCtrl = Resources.Load<RuntimeAnimatorController>("Environment/kidA");
if (kidCtrl != null)
{
    var anim = go.AddComponent<Animator>();
    anim.runtimeAnimatorController = kidCtrl;
}
```

**KidsStealRoutine()** 수정 — Animator trigger 추가:

```csharp
// Rise behind opponent 전에:
var kidAnim = leftKid.GetComponent<Animator>();
if (kidAnim != null) kidAnim.SetTrigger("ready");

// Steal pause 시점에:
if (kidAnim != null) kidAnim.SetTrigger("steal");

// Sink back 후 원위치 복귀 시:
if (kidAnim != null) kidAnim.SetTrigger("skew");
```

### 2.3 — EnvironmentVFXManager: Rescue Animator 추가

**BuildAmbulanceGroup()** 수정:

```csharp
// 현재 코드 (sprite만 설정):
var sr = go.AddComponent<SpriteRenderer>();
sr.sprite = readySprite;

// 추가할 코드:
var rescueCtrl = Resources.Load<RuntimeAnimatorController>("Environment/rescueA");
if (rescueCtrl != null)
{
    var anim = go.AddComponent<Animator>();
    anim.runtimeAnimatorController = rescueCtrl;
}
```

**AmbulanceBlanketRoutine()** 수정 — `!healSelf` 분기에서 `complete` trigger:

```csharp
// Rescue worker rise 완료 후, blanket hold 전에:
var rescueAnim = rescue.GetComponent<Animator>();
if (rescueAnim != null) rescueAnim.SetTrigger("complete");
```

---

## Phase 3: FPS 1인칭 시스템 통합

### 3.1 — FPS GameObject 구조 (코드로 빌드)

gameGem의 FPS 구조:
```
FPS (root, Animator: FPSA.controller)
├── hand1 (SpriteRenderer, pos: -1.57, -1.5, 2)
├── hand2 (SpriteRenderer, pos: 대칭)
├── item  (SpriteRenderer — 액션별 스프라이트 교체)
└── Particle (ParticleSystem)
```

**핵심 설계 결정:**
- FPS는 **로컬 플레이어 전용** (IsOwner == true일 때만 생성)
- Camera의 child로 배치 → 항상 화면 하단에 고정
- 상대방은 FPS를 볼 수 없음 (3인칭 playerA 애니메이션만 보임)

**구현 위치:** `AZPlayerVisual.OnNetworkSpawn()` 또는 새 `FPSVisualController.cs`

```csharp
// AZPlayerVisual.OnNetworkSpawn() 현재:
if (IsOwner) return;  // ← 자기 자신은 스킵하고 EnemyPlayer만 바인딩

// 변경: IsOwner일 때 FPS 시스템 빌드
if (IsOwner)
{
    BuildFPSHand();  // 카메라 하단에 FPS 손 오브젝트 생성
    return;
}
```

### 3.2 — FPSVisualController 신규 스크립트

**File:** `Assets/Scripts/Core/Player/FPSVisualController.cs`

```
역할:
- Camera child에 FPS hand1/hand2/item/Particle 오브젝트 생성
- FPSA.controller 할당
- PlayFPSAnimation(string trigger) — CombatVFXManager에서 호출
- SetItemSprite(Sprite) — 현재 사용 아이템의 FPS 스프라이트 설정
- ReturnToIdle() — "end" trigger
```

**핵심 메서드:**

```csharp
public void PlayFPSAnimation(string trigger)
{
    if (_animator == null) return;
    // 아이템별 FPS 스프라이트 교체
    SetItemSprite(trigger);
    _animator.SetTrigger(trigger);
}
```

### 3.3 — CombatVFXManager: FPS 트리거 연동

**PlayItemSequence()** 수정 — 로컬 플레이어가 아이템 사용 시 FPS 애니메이션도 동시 재생:

```csharp
// line 125-127 현재:
if (hasUserAnim)
{
    userVisual.PlayCombatAnimation(itemData.AnimTrigger);

// 추가:
    bool isLocalUser = (int)nm.LocalClientId == userIdx;
    if (isLocalUser)
    {
        var fps = FPSVisualController.Instance;
        if (fps != null) fps.PlayFPSAnimation(itemData.AnimTrigger);
    }
```

**OpponentAnimTrigger 처리** (line 179-184) — 상대가 나를 공격할 때 내 FPS에도 반응:

```csharp
// 상대 공격이 나에게 올 때 FPS damage 반응은 불필요 (FPS에 damage 상태 없음)
// → FPS는 자기 아이템 사용 시에만 재생
```

### 3.4 — FPS 스프라이트 매핑

아이템 사용 시 `item` child SpriteRenderer에 세팅할 스프라이트:

| AnimTrigger | FPS Sprite | Load Path |
|-------------|-----------|-----------|
| `gun` | FPS_gun.png | `Art/Sprites/FPS/FPS_gun` |
| `tape` | FPS_tape.png | `Art/Sprites/FPS/FPS_tape` |
| `fan` | FPS_fan.png | `Art/Sprites/FPS/FPS_fan` |
| `mask` | FPS_mask.png | `Art/Sprites/FPS/FPS_mask` |
| `card` | FPS_card.png | `Art/Sprites/FPS/FPS_card` |
| `eat` | FPS_eat.png | `Art/Sprites/FPS/FPS_eat` |
| `hug` | FPS_hug.png | `Art/Sprites/FPS/FPS_hug` |
| `use` | (없음 — hand만 움직임) | — |
| `swing` | (없음) | — |
| `defence` | (없음) | — |
| `feed` | (없음) | — |

> FPS sprites는 Resources가 아닌 Art/ 폴더에 있으므로, **Addressables 또는 직접 참조**로 로드 필요.
> 대안: `Assets/Resources/FPS/`로 복사 → `Resources.Load<Sprite>("FPS/FPS_gun")` 사용

---

## Phase 4: Environment Sprites 이동

### 4.1 — Resources/ → Resources/Environment/ 이동

| # | From | To |
|---|------|-----|
| 1 | `Resources/kid_idle.png` + `.meta` | `Resources/Environment/kid_idle.png` |
| 2 | `Resources/kid_rob.png` + `.meta` | `Resources/Environment/kid_rob.png` |
| 3 | `Resources/kid_skew.png` + `.meta` | `Resources/Environment/kid_skew.png` |
| 4 | `Resources/rescue_ready.png` + `.meta` | `Resources/Environment/rescue_ready.png` |
| 5 | `Resources/rescue_save.png` + `.meta` | `Resources/Environment/rescue_save.png` |

### 4.2 — Code Path Updates

| File | Line | Before | After |
|------|------|--------|-------|
| EnvironmentVFXManager.cs | 343 | `Resources.Load<Sprite>("kid_idle")` | `Resources.Load<Sprite>("Environment/kid_idle")` |
| EnvironmentVFXManager.cs | 373 | `Resources.Load<Sprite>("rescue_ready")` | `Resources.Load<Sprite>("Environment/rescue_ready")` |

---

## Predicted Errors / Risks

### 🔴 High Risk

| # | Issue | Cause | Solution |
|---|-------|-------|----------|
| 1 | **FPS anim clip sprite references break** | `.anim` files contain GUID references to sprites from the gameGem project. Moving sprites to a different folder generates new GUIDs → anim clips lose sprite refs | Copy `.meta` files alongside sprites to preserve GUIDs. If GUIDs still break, re-assign sprites in Unity Editor (Animation window) |
| 2 | **kidA/rescueA anim clip sprite references break** | Same GUID issue — anim clips reference kid_idle/skew/rob sprites by GUID | Ensure sprites and their `.meta` files are present at load path. Kid/rescue sprites are already in `Resources/` with GUIDs — moving to `Resources/Environment/` preserves `.meta` GUIDs if moved via Unity Editor (not file system) |
| 3 | **FPS controller trigger name mismatch** | FPSA.controller uses trigger `use` but some SO AnimTrigger might use a different name | Verify: FPSA triggers are `end, swing, tape, mask, hug, gun, card, defence, eat, fan, use, feed`. Cross-ref with all 21 SO AnimTrigger values |

### 🟡 Medium Risk

| # | Issue | Cause | Solution |
|---|-------|-------|----------|
| 4 | **FPS hand position wrong in multiplayer camera** | gameGem FPS is positioned for a specific camera setup. Absolute Zero's camera may have different FOV/position | Tune hand1/hand2/item localPosition values after import. May need adjustment per camera angle |
| 5 | **Animator on kid breaks SinkTransform/RiseTransform** | Adding Animator to kid objects may conflict with coroutine-based position animation (Animator may override position) | Set Animator's `Apply Root Motion = false`. Or use Animator only for sprite changes, not position |
| 6 | **FPS sprites need Resources folder for runtime load** | FPS sprites in `Art/Sprites/FPS/` can't be loaded with `Resources.Load`. Need to be in `Resources/` | Move FPS sprites to `Resources/FPS/` instead, or use SO references with Inspector assignment |
| 7 | **sprite3DMat not in Environment folder** | EnvironmentVFXManager loads `Resources.Load<Material>("sprite3DMat")` — still in Resources root | Keep as-is (it's shared across systems), or copy to Environment/ and update path |

### 🟢 Low Risk

| # | Issue | Cause | Solution |
|---|-------|-------|----------|
| 8 | **FPS_tarrot.anim has no matching SO** | No tarot item exists in Absolute Zero | Ignore — unused anim clip, no harm |
| 9 | **FPS_ziper.png naming inconsistency** | Named `ziper` (typo?) not `zipper` | Just note it — sprite name doesn't affect functionality |
| 10 | **Duplicate assets bloat project size** | 3-4 copies of player/kid/rescue assets across Art/gameGem, testProject, MainFolder | Phase 4 cleanup (with user approval) |
| 11 | **Missing `item` and `Particle` children in FPS** | FPS anim clips reference `item` and `Particle` paths — if these children don't exist, those curves silently no-op | Ensure FPS builder creates all 4 children: hand1, hand2, item, Particle |

---

## Execution Order

```
Phase 1: File Copy (30 files, no code changes)
  ├─ 1.1 FPS anims + controller (13 files)
  ├─ 1.2 FPS sprites (8 files)
  ├─ 1.3 kidA controller + anims (4 files)
  ├─ 1.4 rescueA controller + anims (3 files)
  └─ 1.5 Particle sprites (2 files)
  → Unity reimport → console check (0 errors expected)

Phase 2: Environment sprite move + code path update
  ├─ 2.1 Move 5 sprites from Resources/ to Resources/Environment/
  ├─ 2.2 Update EnvironmentVFXManager load paths
  └─ 2.3 Add Animator components to kid/rescue builders
  → Compile check

Phase 3: FPS system
  ├─ 3.1 Create FPSVisualController.cs
  ├─ 3.2 Modify AZPlayerVisual — IsOwner branch builds FPS
  └─ 3.3 Modify CombatVFXManager — FPS trigger in PlayItemSequence
  → Compile check

Phase 4: Cleanup (optional, user approval)
  └─ Remove duplicate Art/ folders
```

---

## SO AnimTrigger ↔ FPS/Player Trigger Full Mapping

| SO AnimTrigger | FPS State | Player State | FPS Sprite | Items |
|---------------|-----------|-------------|------------|-------|
| `use` | FPS_useItem | — | (hands only) | Samgyetang, Windbreaker, WarmTea, HotPack, Umbrella |
| `gun` | FPS_gun | — | FPS_gun.png | Water Gun |
| `tape` | FPS_tape | — | FPS_tape.png | Blue Tape |
| `fan` | FPS_fan | — | FPS_fan.png | Fan, HandFan |
| `mask` | FPS_mask | — | FPS_mask.png | Mask |
| `card` | FPS_card | playerA_card | FPS_card.png | Red Card |
| `eat` | FPS_eat | playerA_eat1 | FPS_eat.png | Buldak Noodles |
| `hug` | FPS_hug | playerA_hug | FPS_hug.png | Hug T-shirt |
| `swing` | FPS_swing | playerA_swing1 | (hands only) | Screwdriver |
| `defence` | FPS_defence | playerA_defence | (hands only) | Windbreaker (opp) |
| `attack` | — | playerA_attack | — | (generic) |
| `drink` | — | playerA_drink | — | WarmTea |
| `feed` | FPS_feed | playerA_feed | (hands only) | Cat |
| `button` | — | playerA_button | — | Smartphone |
| `heal` | — | playerA_heal | — | Samgyetang |
| `damage` | — | playerA_damage | — | (hit reaction) |
| `freeze` | — | playerA_freeze | — | (freeze state) |
| `disappoint` | — | playerA_disappoint | — | (fail reaction) |
