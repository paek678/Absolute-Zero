# PLAN046 — Solo bot difficulty and judgement (초급 / 상급)

## Status

✅ Complete on branch `feature/solo-bot-difficulty` (2026-10-13). Values stay `provisionalTuning` until human play-feel review (BT03).

## User request and boundaries

Implement the attached solo bot design (`솔로_봇_설계.md`: bot settings, per-item criteria, score formulas, decision order and five answered questions) by extending the existing Solo bot pipeline, the partner's bot-only logic: `BotTurnController`, `TrustedBotCommandAdapter`, `BotItemUseOperation`, the Behavior graph and the configuration resolver. Two player-facing profiles: 초급 (beginner) and 상급 (advanced).

Start temperature, fan and initial items stay identical to the human player (spec, confirmed). Existing 1v1 rules, combat, item effects, grants and the online/Multi paths are unchanged.

## Approved design changes (supersede older notes)

The attached spec is the newer explicit decision; Q1–Q5 were answered "굿".

| Older statement | New decision |
|---|---|
| `GAME_DESIGN.md` Solo: "Bot does NOT play mini-games … no random success roll" | The bot rolls mini-game success per difficulty tier (easy/normal/hard) when it selects the item. |
| `PLAN_040` BT03.1: no bot mini-game or random success roll | Same as above. |
| `PLAN_036`: the bot has no simulated failure roll | A failed roll consumes the item exactly like a human failure (Q2). An interrupted delay still never debits. |
| `SOLO_BT_AUTHORING`: no unrevealed opponent inventory | The opponent's held item list is public (Q15) and may be read. The opponent's selection this turn stays hidden (Q22). |
| BT01-B / BT02-B blocked on difficulty values | Values come from the spec. Start state stays inherited, so BT01-B custom start stats remain disabled and unnecessary. |

## Interpretations (recorded, adjustable in data)

1. **Beginner pick odds** use the settings table: 60/25/15. Q3's text proposed 70/20/10. Changing it is a one-field edit in `Beginner.asset`.
2. **"처치 가능" (lethal)** means the attack score (damage × success × (1 − block chance)) is at least the opponent's temperature. This reproduces the worked example: ice cream 5.0 against 5° is lethal; water gun 3.9 is not.
3. **"이번 준비 시간에 내가 더 잃을 온도"** = own fan speed × min(remaining prep, shortest legal item delay). The bot readies after its delay (example value 2 = the 2 s advanced instant delay). It is zero once the human has readied.
4. **"상대가 바람막이를 쓸 수 있음"** = the opponent holds a usable tool guard and is not blocked by Blue Tape. The permanent-item cooldown exists only on an unmerged branch.
5. **"아무거나"** means uniform among all positive-score items. Ties use the spec table order. Duplicate copies are ranked once per item.
6. **Heal scores are capped at the 37° maximum**, because the score unit is temperature and code caps heals at `MAX_TEMP`. This prevents drinking Warm Tea at 37°. It changes the example only for Buldak, which the example bot does not hold (7.2 → 6.1).
7. **Hard filters** apply where a formula cannot express a wasted or self-lethal action:
   - Claw when own inventory is full or nothing is stealable;
   - Soda when its −5 would put the bot in danger;
   - Screwdriver when the opponent's fan is already boosted;
   - Hug T-shirt at ≤ 0 (spec).
8. **After a failed mini-game** the bot readies without another item. The decision order runs once per Prep.
9. **When the human readies first**, any pending delay ends immediately: the chosen item resolves with its pre-rolled outcome and the bot readies.
10. **Effect values are read from item data** (single source). HandFan uses its data damage 2; the spec text says 4.
11. **Screwdriver** score = opponent loss per prep × (boosted fan / current fan − 1) × 2 turns × lookahead × success.
    - Opponent loss per prep is observed publicly: the opponent's temperature at Prep start minus their temperature when they ready.
    - The prior is half of the Prep window at the current fan speed.

## Pattern selection

| Candidate | Verdict |
|---|---|
| Code FSM | Rejected. The decision is a one-shot priority evaluation per Prep, not long-lived states. |
| Utility scoring alone | Rejected alone. The spec's crisis and lethal overrides must stay visible and editable as graph priority. |
| **Behavior Tree (existing Unity Behavior graph) + pure utility scorer + Command boundary** | **Selected.** |

How the selected option divides the work:
- The graph keeps priority: Try In Order with built-in Conditional Guards.
- A pure model scores items (one formula per item effect, a Strategy keyed by data), so it is testable in EditMode.
- The trusted command boundary owns the delay, the success roll and consumption. Callers never choose outcomes.

It reuses the partner's pipeline with no new network surface or second action path.

## API / package audit (installed versions, no upgrades)

- **Unity Behavior 1.0.16** (manifest pin).
  - Runtime: public `Unity.Behavior.Action` / `Condition` with `[Condition]`, and the built-in `ConditionalGuardAction` ("Conditional Guard (Action)").
  - Editor builder: `Assets/Editor` compiles into `Assembly-CSharp-Editor`, which the authoring assembly grants InternalsVisibleTo (`ConditionalGuardNodeModel`, `ConditionModel`, `ConditionUtility`, `NodeRegistry`). This is the same access the existing builder already used.
  - Sibling execution order is ascending X (`GraphAssetProcessor.GetSortedConnections`).
- **NGO 2.11.2**: no new RPC or NetworkVariable. The bot reads server-side replicated public state: `PlayerInventory.SlotStates` (everyone-readable `NetworkList`), `IsReady`, `IsBasicBlocked`, `FanSpeed`, `Temperature`.

## Pattern-to-code mapping

| Spec step | Graph node | Code |
|---|---|---|
| 1 usable items | Build legal own item candidates | `BotTurnController.BuildCandidates` → `PlayerState.ServerValidateBotItem` |
| 2 score | Score candidates by difficulty (new) | `BotDecisionModel.Score` |
| 3 crisis | Conditional Guard [Bot is in mortal danger] → Pick best recovery or defense | `SoloInCrisis`, `SoloPickCrisis` |
| 4 lethal (advanced) | Conditional Guard [Lethal attack must be taken] → Pick lethal attack | `SoloMustFinish`, `SoloPickLethal` |
| 5 odds | Pick by difficulty odds | `SoloPickWeighted` |
| 6 success | Decide mini-game outcome and submit delayed item | `TrustedBotCommandAdapter` roll inside `BotItemUseOperation.Begin` |
| 7 wait / Ready | Await item delay or opponent Ready → Ready | `BotItemUseOperation.Advance`, `BotTurnController.WaitReady` |

**Data flow:**
1. Inputs:
   - `BotDifficultyProfileSO`: judgement settings and extra item delay;
   - `BotDefinitionSO`: graph, base delay policy and `BotItemKnowledgeSO`;
   - item assets.
2. `SoloSettingsResolver` → immutable `ResolvedSoloSettings`, including per-catalog `BotItemProfile`.
3. `BotTurnController` observes own state plus the opponent's public state.
4. `BotDecisionModel` scores and picks.
5. `TrustedBotCommandAdapter` / `BotItemUseOperation` apply the delay and the success roll.
6. `PlayerState` either queues the item, or consumes it through the shared human-failure path.

**Authority:** host only. Nothing new reaches clients.

## Work scope

**Modify**
- Solo runtime:
  - `Assets/Scripts/Core/Solo/BotDecisionModel.cs`
  - `BotTurnController.cs`
  - `SoloBehaviorNodes.cs`
  - `BotItemUseOperation.cs`
  - `TrustedBotCommandAdapter.cs`
- Solo configuration:
  - `Configuration/SoloConfigurationSO.cs`
  - `BotDifficultyProfileSO.cs`
  - `BotDefinitionSO.cs`
  - `ResolvedSoloSettings.cs`
  - `SoloSettingsResolver.cs`
- Player:
  - `Assets/Scripts/Core/Player/PlayerState.cs`: extract the human failure consumption into a helper; add a trusted bot failure entry.
  - `PlayerActionContracts.cs`: append `MiniGameFailed`.
  - `PlayerInventory.cs`: expose slot capacity.
- Editor:
  - `Assets/Editor/SoloBotAuthoringWindow.cs`
  - `Plan036ExtensionValidation.cs`
  - `Plan036ConfigurationBuilder.cs`
- Tests:
  - `Assets/Tests/Editor/Plan036BotDecisionTests.cs`
  - `Plan036BotDelayTests.cs`
  - `Plan036SoloConfigurationTests.cs`
  - `Plan036ActionAuthorityTests.cs`
- Assets:
  - `Assets/Data/Solo/Bots/DefaultBot.asset`
  - `Difficulties/NeutralBaseline.asset`
  - `Graphs/SoloDuelDecision.asset` (recreated)
  - `Assets/Scenes/LobbyScene.unity` (Solo profile list only)
- Docs:
  - `GAME_DESIGN.md` (Solo bot section)
  - `SOLO_BT_AUTHORING.md`
  - `BOT_AUTHORING_TOOL_GUIDE.md`
  - `PLAN_040` (BT status note)
  - `CHANGES.md`
  - `RECENT_CHANGES.md`
  - `ACTIVE_CONTEXT.md`

**Create**
- Code:
  - `Assets/Scripts/Core/Solo/Configuration/BotItemKnowledgeSO.cs`
  - `Assets/Scripts/Core/Solo/SoloBehaviorConditions.cs`
  - `Assets/Editor/Plan046SoloBotBuilder.cs`
  - `Assets/Tests/Editor/Plan046BotJudgementTests.cs`
- Data:
  - `Assets/Data/Solo/Knowledge/DefaultItemKnowledge.asset`
  - `ItemUse/StandardItemUsePolicy.asset`
  - `Difficulties/Beginner.asset`, `Difficulties/Advanced.asset`
  - `Encounters/BeginnerSoloDuel.asset`, `Encounters/AdvancedSoloDuel.asset`

**Delete**
- `Assets/Editor/Plan036BotGraphBuilder.cs`: superseded by the PLAN046 builder, and it would rebuild the obsolete tactic graph.

**Will NOT touch**
- `TurnManager` phase logic, `CombatResolver`, item effect code and item SO values.
- Human mini-game behavior. Only the identical three-line consumption moves into a helper.
- Online / Multi / Relay paths.
- Package versions.
- `BotStatProfileSO` inherit-only policy.
- Legacy `BotBrain.cs`.
- Validation fixture encounters and graphs.

## Checklist

- [x] C1 Configuration schema: judgement settings, mini-game tier, item knowledge SO, extra item delay; remove tactical weights and decision noise.
- [x] C2 Resolver: validation, delay + extra delay, per-catalog item profiles and success chances; knowledge required for normal launches only.
- [x] C3 Pure model: observation with public opponent items, scoring per spec formula, crisis / lethal / odds picks, deterministic RNG.
- [x] C4 Trusted boundary: success roll at request start, early completion once the opponent is Ready, failure path through the shared human consumption.
- [x] C5 Controller and nodes: Score / PickCrisis / PickLethal / PickWeighted, two conditions, opponent cooling observation, Ready on opponent Ready.
- [x] C6 Editor tooling: PLAN046 builder (graph, assets, lobby profiles, validation); authoring window; extension validation; configuration builder hook.
- [x] C7 Tests: spec worked-example scores, picks, filters, delay / roll / early completion, configuration validation; existing Solo suites.
- [x] C8 Build assets in Unity, run the EditMode suites, inspect a natural Solo trace for each profile.
- [x] C9 Docs and harness records.

## Discovered issues

- [Fixed, blocking] Six `.meta` files were committed as NUL bytes in `9a34c21`. This broke compilation (`LocalMatchPerspective`). They were regenerated with new GUIDs; nothing references them by GUID.
- [Tracked] Docs still describe `IsPermanentLocked`, which exists only on `origin/feature/permanent-lock-emote`.
- [Tracked] HandFan data damage is 2; `GAME_DESIGN.md` says 4°.
- [Tracked] Windbreaker in code is a −2 reduction for tool hits; the design says it "blocks tool attacks". The spec formula assumes a full block.
- [Tracked] Screwdriver keeps fan speed 2 for the rest of the round (`DelayTurns` 0); the original intent was one Prep.
- [Tracked] `GAME_DESIGN.md:69` says "Main select → fan stops"; the code stops the fan on Ready only.
- [Tracked] `Assets/Tests/Plan036Presentation/Runtime` contains only an asmdef, with no scripts.
- [Tracked, pre-existing] Exiting Play Mode during an active Solo match logs `InvalidOperationException: Lobby scene could not load` from `SoloSessionCoordinator.ReturnToLobbyAsync`. NGO shutdown during play-mode exit triggers it; `SoloSessionCoordinator` was not changed.

## Validation plan

- **EditMode:**
  - `Plan046BotJudgementTests`: the spec worked example row by row; lethal, crisis, odds frequency, filters, ties and determinism.
  - Updated `Plan036*` Solo suites.
  - Configuration tests for the new fields.
- **Builder:** validation of the graph topology, conditions, profiles and lobby binding.
- **Play Mode:** natural Solo match per profile, inspecting `[SoloBT]` traces for score, pick, roll and Ready.

## Results (2026-10-13)

- **Build:** `Plan046SoloBotBuilder.Build()` → `PASS: 22-node graph with crisis/lethal guards, beginner/advanced/baseline resolve, 20 enabled items known, lobby lists 초급/상급.`
- **EditMode:** 1061/1061 passed, 0 failed. The focused Solo/bot subset is 164/164, including `Plan046BotJudgementTests`. The spec worked example reproduces the sheet ranking: Ice Cream 105, Claw 8.96, Cat 6.4, Hot Pack 5.4, Water Gun 3.92, Warm Tea 3.5, Windbreaker 2.8, Fan 2.1.
- **Play Mode, Advanced** (lobby scene, development Solo, idle human):
  - Turn 1 picked Windbreaker 5.6 (threat 8, multiplier 1) and queued it 2.0 s after submit.
  - Turn 2 picked Cat 10.88 with the comfort multiplier 0.5.
  - Round 2 picked Screwdriver 11.2, a mini-game item, queued after 4.0 s.
- **Play Mode, Beginner** (the human readies while the bot's item is pending):
  - The delay was 4 s or 6 s.
  - Items were queued 0.03–0.08 s after the human's Ready instead of at the due time.
  - Picks varied (best / second / other).
  - A Water Gun failure (hard tier, 45%) consumed one use via `Mini-game FAILED: slot 3 — consuming 1 use`; Ready followed without another item.
- **Not covered here:** human play-feel and difficulty judgement (BT03), release-build validation and a lethal-branch capture in natural play. The lethal branch is covered by unit tests.
