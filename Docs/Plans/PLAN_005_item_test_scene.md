# PLAN_005 — Item System Test Scene (Playable Sandbox)

> Status: 🔄 In Progress
> Branch: `feature/item-system`
> Goal: a scene the user can open and Play to hand-test the 4 basic items (PLAN_004) without the core turn loop (partner's scope, not yet merged).

---

## Design

- **Why not the lobby flow:** TurnManager/ServerRpc wiring does not exist yet (partner's scope), so lobby → GameScene cannot exercise items. Instead a dedicated sandbox scene starts NGO **Host automatically** (UnityTransport localhost, no Relay/Lobby) so NetworkVariable/NetworkList behave for real.
- **Players:** two in-scene-placed NetworkObjects (`P1`, `P2`) each with `PlayerState` + `PlayerInventory` — in-scene NetworkObjects auto-spawn on host start (no prefab registration needed).
- **Driver:** `ItemSystemTestDriver` (MonoBehaviour, runtime-built TMP UI per project convention) — item buttons per player, fan toggles, next-turn / reset, effect log. Item SO references are serialized fields wired at scene setup via MCP (data refs only; UI stays runtime-built).
- Effects run "server-side" trivially since Host = server. Main/Sub distinction is simplified (no CombatResolver): every item applies on click; 바람막이 sets ActiveDefense for the current turn; [다음 턴] applies scheduled effects + resets modifiers.

## Work Scope Declaration

### Will create
| File | Purpose |
|------|---------|
| `Assets/Scripts/UI/TestUI/ItemSystemTestDriver.cs` | Sandbox driver: host bootstrap, item context build, runtime UI, log |
| `Assets/Scenes/ItemTestScene.unity` | Camera + Light + NetworkManager(UnityTransport) + P1/P2 + TestDriver |

### Will NOT touch
- Everything from PLAN_004 "Will NOT touch" list
- PLAN_004 item/contract code (only consumed, not modified)
- Build settings (test scene opened directly in editor; not added to build)

## Checklist

- [x] 1. ItemSystemTestDriver.cs written (RULE-010 event cleanup, no per-frame allocs)
- [x] 2. Compile check (0 errors — verified via Editor.log; last 4 compile passes clean, incl. driver + Debuff mask fix)
- [x] 3. ~~Scene via MCP~~ → `ItemTestSceneBuilder.cs` editor menu script added instead (MCP session down — see Discovered Issues). Menu: **Tools > AbsoluteZero > Build ItemTestScene** (also MCP-executable via execute_menu_item later)
- [ ] 4. Scene actually built + Play-tested (blocked: needs MCP reconnect OR user clicks the menu once)
- [x] 5. Docs updated, files staged (user commits)

## Discovered Issues

- **In-scene placed NetworkObjects in a programmatically built scene fail to spawn** — `GlobalObjectIdHash` stays 0 for all of them (NGO only computes it through normal editor save flow), so `StartHost()` throws `same GlobalObjectIdHash value 0` and host init aborts (follow-up NREs in NetworkManager teardown are collateral). **Fix:** builder now creates `Assets/Prefabs/ItemTestPlayer.prefab` (hash computed at asset import) and the driver registers it via `AddNetworkPrefab` + spawns 2 instances at runtime. → candidate SAFETY_RULES entry.
- **`Font.CreateDynamicFontFromOSFont` → `TMP_FontAsset.CreateFontAsset` fails in this environment** (warning stack in Editor.log). Fix: load the font file directly via `Font.GetPathsToOSFonts()` (malgun.ttf first), which is the reliable path for TMP dynamic font assets.
- **Unity MCP session unreachable** throughout this plan (HTTP server up, editor-side bridge disconnected; editor process itself healthy/responding). Worked around with the scene-builder menu script. If this recurs → candidate SAFETY_RULES entry.
- **Editor version mismatch:** editor window shows Unity 6.3 LTS (6000.3.11f1) but CLAUDE.md says 6000.0.73f1 — user opened/upgraded with a newer editor? Flagged to user.
- Transient CS0246 errors found in Editor.log (PlayerInventory not found) — from the mid-write window during PLAN_004 batch file creation, resolved by the final full compile. No action needed.
