# PLAN_017 — Permanent-Item Consecutive-Use Lock + Taunt Emote System

> Status: ✅ Implemented — PR #6 open (`feature/permanent-lock-emote` → `main`)
> Date: 2026-07-24
> This doc is a work summary pushed to `main`; the code lands via PR #6.

## Goal

Two gameplay/UX features:
1. **기본 영구 아이템 연속 사용 방지** — the two basic *permanent* items (부채/Fan, 바람막이/Windbreaker) cannot be used two turns in a row.
2. **도발 이모티콘 (taunt emote)** — after readying, a player can flick out an emoji that appears as a speech bubble over their character on the opponent's screen, to taunt during the wait for the opponent to ready.

## Feature 1 — Permanent-item consecutive-use lock

- **Design:** using either permanent basic item locks *both* permanent items for the **next turn only** (same "banned" treatment as Blue Tape / 청테이프). Consumables (차/Warm Tea, 고양이/Cat) are unaffected.
- **Data:** the four basic items are Fan(Permanent/Main), Windbreaker(Permanent/Main), Warm Tea(Consumable/Main), Cat(Consumable/Main). Only the two `Permanent` ones are locked.
- **Implementation:**
  - `PlayerState.IsPermanentLocked` — new server-authoritative NetworkVariable (separate from Blue Tape's `IsBasicBlocked` so it scopes to `Persistence == Permanent`, not all Main-slot items).
  - `ItemDataSO.CanUse` — rejects a Permanent item while `IsPermanentLocked`.
  - `CombatResolver` — sets the user's `IsPermanentLocked` when a Permanent item is consumed, on **both** the main path (`ExecuteMain`) and the defense path (`ApplyDefense`, since Windbreaker is a defense item).
  - `TurnManager.AttackPhaseRoutine` — resets both `IsBasicBlocked` and `IsPermanentLocked` at attack-phase start → one-turn lifetime (set during turn N resolve → blocks turn N+1 prep → cleared at turn N+1 attack).
  - `InventoryPresenter` — banned overlay (`banned_tape`) + non-interactable + click-block for Permanent items under `IsPermanentLocked`; subscribes to its `OnValueChanged`.

## Feature 2 — Taunt emote system

Assets: `Resources/Emoticon/` — 5 emojis (slow / tongue / sneer / excited / lose), each a `_char` + `_text` sprite (English filenames for build safety), Sprite import.

- **`EmoteCatalog`** (`Core/Emote`) — id → (character, text) sprite loader. Array index = the network emote id.
- **`EmoteWheel`** (`UI/Emote`, attached to the Ready button):
  - Only after the local player is ready and in PrepPhase.
  - Press-and-hold the Ready button → 5 emojis fan out in an upper arc (staggered easeOutBack fade-in).
  - Drag toward one (angular pick with deadzone) → it highlights; release fires it; release in the deadzone cancels.
  - Auto-closes when prep ends (checked in `Update`, and `OnDisable` for the case the Ready UI is deactivated).
  - Sender gets a local confirmation pop; fires `PlayerState.SendEmoteServerRpc`.
- **`EmoteBubble`** (`Core/Emote`):
  - Screen-space overlay (+`CanvasScaler` 1920×1080 → resolution-independent) that tracks the sender's head via `Camera.main.WorldToScreenPoint` → `ScreenPointToLocalPointInRectangle`.
  - Emoji only (character + text, no bubble background), nudged slightly left; ~1s fade in/out.
  - Hidden when the head projects behind the camera (`sp.z <= 0`); self-destroys domain-reload orphans.
- **Networking (`PlayerState`):**
  - `SendEmoteServerRpc` (owner→server) — accepted only while `TurnManager.AcceptEmotes` (prep active, pre-attack window open) and `emoteId` in range; records `LastEmoteServerTime`.
  - `ShowEmoteClientRpc` (everyone) — **skips the world bubble on the sender's own client** (`IsOwner`), otherwise anchors to the opponent's enemy-visual (`AZPlayerVisual.GetVisualRoot()`); **no netTransform fallback**.
- **Attack-start delay (`TurnManager`):** when prep ends, closes the emote window (`AcceptEmotes`) and waits the remaining display time (≤`EMOTE_DISPLAY_SEC` = 1s) of the last taunt before starting the attack phase, so the in-flight taunt is the last and finishes first. No temperature-state change (the transition already doesn't decay).

## Root-cause note (emote anchoring bug — diagnosed via logs, fixed)

Network positions were P0=(0,0,-1) / P1=(0,0,8), but the game seats characters **client-locally** (self near / enemy at a fixed far seat (0,1.6,8)). The local player's `GetVisualRoot()` is null (first-person). The original `netTransform` fallback for a player's *own* emote therefore lands on the wrong seat — **masked on the host** (its network position coincides with its near seat) but **broken on the client** (its network position equals the far/enemy seat). Fix: never fall back to netTransform; skip the world bubble on the sender's own client (local pop only) and anchor only to the opponent's enemy-visual.

## Testing

- MPPM 2-player tested; the anchoring bug was reproduced, logged, root-caused, and fixed. Compile 0 errors.

## Follow-up (not in this PR)

- **Emote sound effect** (요청: "사운드는 나중에") — the emote trigger SFX is a separate task.

## Files

- New: `Core/Emote/EmoteCatalog.cs`, `Core/Emote/EmoteBubble.cs`, `UI/Emote/EmoteWheel.cs`, `Resources/Emoticon/*`
- Modified: `Core/Player/PlayerState.cs`, `Core/Turn/TurnManager.cs`, `Core/Combat/CombatResolver.cs`, `Core/Item/Data/ItemDataSO.cs`, `Core/Inventory/InventoryPresenter.cs`, `UI/Game/AZGameUI.cs`

## Commits (on `feature/permanent-lock-emote`)

- `deb746d` 기본 영구 아이템 연속 사용 방지 (부채/바람막이 한 턴 잠금)
- `49ecc90` 도발 이모티콘 시스템 (준비 후 이모지 휠 → 상대 머리 위 말풍선)
- `3a0107d` 도발 후 공격 시작 지연 (재생 중 도발 끝까지 대기)
