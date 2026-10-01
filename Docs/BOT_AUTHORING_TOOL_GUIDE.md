# Bot Authoring Workbench — User Guide

## Purpose and opening the tool

Use this tool to check a Solo bot configuration before starting a match. It does not generate a Behavior graph, change balance, save assets, or start a game.

1. Open the Absolute Zero project in Unity, outside Play Mode.
2. Select **Absolute Zero → Solo → Bot Authoring**.
3. Alternatively, select a bot configuration asset and click **봇 설정 검증 창 열기** in its Inspector.
4. Select **DefaultSoloDuel** for the current baseline. Its asset is `Assets/Data/Solo/Encounters/DefaultSoloDuel.asset`.
5. After editing and saving configuration, press **저장된 로비 연결로 다시 검사**.

The tool reads the **saved LobbyScene** item/cosmetic bindings and the enabled scenes in Build Settings. Unsaved lobby changes are not included. It preserves the active scene and closes its temporary preview scene.

## Read the results

| Screen section | Meaning / action |
|---|---|
| 검사할 경기 설정 | The encounter to inspect. Choosing an entry does not launch it. |
| 등록된 경기 설정 | Available encounter assets. **검사 전용** means a fixture, not a player difficulty. |
| 개발판 일반 Solo | Whether the existing development launch resolver accepts it. |
| 배포판 Solo | Whether a non-development player accepts it. Provisional tuning must remain blocked until approved. |
| 명시적 검사 전용 진입 | Whether the explicit fixture entry accepts it; this is separate from ordinary Solo. |
| 수정 위치 | Buttons select the exact asset to edit in the Inspector. |
| 설정 ID / 버전 | IDs and versions copied into a match's resolved settings. |
| 아이템 시간표 | Effective use delays and nominal thinking + use + Ready reserve. Disabled entries are marked **사용 제외**. |

**The current baseline passing development but failing release is expected.** Do not turn off `Provisional Tuning` only to remove a warning. Configuration readiness is not approval of balance, proof of graph progress, or completion of gameplay testing.

## Which asset to edit

| Asset | Fields / responsibility |
|---|---|
| SoloDuelDefinitionSO | Stable encounter ID/version, readiness, bot, difficulty, shared 1v1 rules and dedicated scene path. |
| BotDefinitionSO | Display name, bot-only cosmetic IDs, compiled Behavior graph, base stats, base item policy, default tactical weights. |
| BotDifficultyProfileSO | Think-time range, Ready policy/reserve, tactical-weight override, decision noise, bounded retry count and optional policy override. |
| BotItemUsePolicySO | Positive default item delay and per-item overrides using asset references. Duplicate overrides are invalid. |
| BotStatProfileSO | Reserved start-state inputs. **Currently inherit-only**: all override flags off and initial items empty. |

The difficulty item policy **replaces the entire base policy** when assigned. It does not merge missing entries from the base policy. Tactical weights use the difficulty values only when `Override Tactical Weights` is enabled; they score candidates without reordering graph branches.

Start temperature, fan baseline and initial grants continue to follow current 1v1 rules. Custom start values need the start/reset/grant policy decision in BT01-B before activation.

## Safe editing example

1. Select the intended encounter and open its bot/difficulty/item-policy assets through **수정 위치**.
2. For an independent variant, duplicate the relevant assets, give each copy a new stable lowercase `Config Id`, and link the copies from a copied encounter. Retain provisional tuning while values are unapproved.
3. Change the approved think/delay/weight fields. Do not edit the shared 1v1 rule merely to tune one bot.
4. Save, then refresh the workbench. Resolve errors in the ordinary development result.
5. Check whether timing warnings match the intended behavior. Near a deadline the runtime may shorten thinking, select another action, or use Ready fallback. A nominal sum exceeding the preparation time is a warning, not a new gameplay rule.
6. Validate the actual graph and play a match. The workbench checks compiled roots/placeholders; a structurally valid graph can still contain incorrect behavior or priorities.

Adding an asset does not automatically expose it in the player menu. The lobby's explicit profile list controls player-visible entries; fixtures must remain outside that list.

## Registering a player-visible profile (BT02-A)

The player flow is **main lobby → 봇 대전 → select a profile → 대전 시작**. Merely opening the screen performs local validation; it does not authenticate with UGS or start a host.

- In saved `LobbyScene`, select the object with `AZLobbyUI` and edit **Solo Profiles**. Each entry contains `Title`, `Description` and `Encounter`. The current list contains one baseline entry. Do not invent difficulty labels or balance values before approval.
- Use unique encounter IDs in this explicit list. Duplicate IDs and invalid configurations disable Start with a reason. `ValidationFixture` entries are excluded from ordinary choices even in a development player.
- **뒤로** before Start returns to the main menu. During startup it requests cleanup through the existing Solo coordinator; additional clicks do not start or cancel multiple sessions. If cleanup fails, retry Back; a new game remains blocked while the old session owns the network.
- A successful Start captures configuration IDs, versions and resolved values. **Replay keeps that snapshot**. Changing lobby selection affects the next new match. The human cosmetic profile and bot cosmetic IDs remain separate.
- Adding a profile asset alone does not register it. Saving lobby bindings is required; test the menu and actual graph after changes.

## Troubleshooting

| Message / symptom | Fix |
|---|---|
| `custom stats/loadouts are disabled` | Turn start override flags off and leave initial items empty until BT01-B is approved. |
| `compiled root` | Connect and compile the intended Behavior graph; remove placeholder/missing nodes. |
| `Provisional tuning` in release result | Expected before final balance approval. Development testing remains separate. |
| `same item reference twice` / null catalog | Fix saved LobbyScene → AZLobbyUI → Solo Catalog. Preserve stable catalog order. |
| `duplicate overrides` | Keep one delay override per item asset in the effective policy. |
| `Ready reserve` | Use a finite nonnegative value below the shared preparation duration. |
| Unknown cosmetic ID | Use IDs in the saved lobby cosmetic registry with the correct body part. |
| Scene not loadable | Check the full `Assets/.../*.unity` path and enabled Build Settings. Do not add test scenes to product builds just to clear an error. |
| Duplicate configuration ID | Give independent copies distinct IDs. Reusing the same asset reference is allowed. |
| Changed data not reflected | Save changes, refresh the tool, check the selected encounter, then start a new match/build. A running match retains its captured configuration. |

## Limits and related documents

- Inspector editing is disabled during Play Mode. Runtime source assets must remain read-only.
- The tool reports errors; it does not auto-repair, rename IDs, alter scene bindings, or approve shipping profiles.
- After a BT change, test legal targets, delayed use, fallback, exit/re-entry and replay as described in [SOLO_BT_AUTHORING](SOLO_BT_AUTHORING.md).
- Current passes and deferred visual/manual checks: [PLAN040 validation ledger](Validation/PLAN_040_results.md).
- Implementation order and pending design decisions: [PLAN040](Plans/PLAN_040_followup_features.md).

This file lives in the shared `Docs` vault for Obsidian; maintain this copy rather than a second note.
