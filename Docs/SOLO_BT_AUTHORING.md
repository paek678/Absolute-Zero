# Solo BT authoring guide

Verified against repository source and the saved production graph on **2026-09-29**; decision model, graph and profiles updated for the approved two-difficulty bot on **2026-10-13** ([PLAN_046](Plans/PLAN_046_solo_bot_difficulty.md)). Installed package: **Unity Behavior 1.0.16**. This is a guide to the existing Solo implementation; the code samples below have **not** been installed into the game.

**Start here:** [play and configuration guide](SOLO_BOT_SETUP.md). Scope and existing evidence: [implementation plan](Plans/PLAN_036_solo_detailed_implementation.md), [validation results](Validation/PLAN_036_results.md).

## 1. The four things an author edits

| I want to change... | Edit... | Responsibility |
|---|---|---|
| Which branch (crisis / lethal / odds) is tried first | `Assets/Data/Solo/Graphs/SoloDuelDecision.asset` or a duplicate | Graph: decision order |
| How an item is scored or picked | `Assets/Scripts/Core/Solo/BotDecisionModel.cs` | Pure observation/scoring code |
| A reusable step or guard visible in the graph | `SoloBehaviorNodes.cs` / `SoloBehaviorConditions.cs` or a new adjacent C# file | Thin action node or condition calling the controller |
| Success rates, pick odds, lookahead, multipliers, extra delay, thinking, Ready | Difficulty assets (`Beginner.asset`, `Advanced.asset`) | Configuration resolved when launching a match |
| Item delays | `StandardItemUsePolicy.asset` (2 s / 4 s) | Configuration resolved when launching a match |
| Opponent item worth, mini-game tier, formula coefficients | `Assets/Data/Solo/Knowledge/DefaultItemKnowledge.asset` | The per-item sheet; row order breaks ties |

`BotTurnController` connects these pieces. It observes public state, builds legal candidates, runs the graph and submits requests. Keep actual item effects, consumption and combat in the shared game systems.

```mermaid
flowchart LR
    Profile[Bot and difficulty assets] --> Resolve[ResolvedSoloSettings]
    Resolve --> Controller[BotTurnController]
    Graph[Behavior graph and action nodes] --> Controller
    Controller --> Model[BotDecisionModel: score legal candidates]
    Controller --> Adapter[TrustedBotCommandAdapter]
    Adapter --> Delay[BotItemUseOperation: validated delay]
    Delay --> Shared[PlayerState and shared combat]
```

The bot uses the existing 1v1 rules in `GameScene_Solo`. One process runs an internal NGO host with a human and a logical bot participant. The BT decides the bot's input; `TurnManager` remains the only phase owner. The bot plays no mini-game UI. For a mini-game item, the trusted command boundary rolls success from the difficulty's tier rate when the delayed request starts. A failure is consumed through the same `PlayerState` path as a failed human mini-game. Once the human is Ready, the pending delay ends immediately.

## 2. Open and safely change a graph

1. Stop Play Mode and preserve any unsaved scene work.
2. In Unity's Project window, select `Assets/Data/Solo/Graphs/SoloDuelDecision.asset` and open its Behavior graph editor.
3. For a new personality, duplicate the graph **in Unity**, preserving the baseline. Do not copy/edit its serialized YAML or `.meta` GUID by hand.
4. Duplicate `DefaultBot.asset`, `NeutralBaseline.asset` and `DefaultSoloDuel.asset` as needed. Duplicate the item-use policy only if its delays will differ. Shared unchanged profiles can stay referenced.
5. Give each distinct configuration asset a unique `configId` and positive `version`. Example IDs: `solo.bot.practice`, `solo.difficulty.practice`, `solo.encounter.practice`. These names do not imply approved balance.
6. Assign the duplicated graph to the new bot's **Graph** field. This field requires the compiled **BehaviorGraph** associated with the authoring asset. If dragging the asset leaves the field empty, select its runtime graph sub-asset through the object picker; never assign a fixture graph instead.
7. Assign the new bot and difficulty to the encounter. Keep the shared OneVsOne rule and `Assets/Scenes/GameScene_Solo.unity`. Use readiness **Ready** for ordinary development play, not Draft or ValidationFixture.
8. Register the encounter in the saved lobby's `AZLobbyUI` **Solo Profiles** list with a title and description. Preserve the baseline entry and use unique encounter IDs. The player opens **봇 대전**, selects a profile, then presses **대전 시작**. Validation fixtures are excluded from this ordinary menu. See the [workbench and registration guide](BOT_AUTHORING_TOOL_GUIDE.md); final difficulty tuning remains a separate approval step.
9. Save the graph, let Unity finish normal compilation, inspect console errors, and launch through the lobby's **1대 봇 대전** button. Start a fresh match after tuning changes.

**Execution order matters:** in the installed package, outgoing child connections are compiled in ascending horizontal position (`GraphAssetProcessor.GetSortedConnections`). Keep sibling nodes at distinct X positions, left to right in execution order. Moving them for presentation can change behavior. Verify the compiled order and a trace after rearranging; do not rely on wire creation order or equal-X placement.

**`Plan046SoloBotBuilder.Build()` is a one-time creator, not a save/refresh command.**
- It refuses to run when any of its assets exist: the graph, item knowledge, standard delays, beginner/advanced difficulties and both encounters.
- It also binds `DefaultBot` (graph and knowledge), sets the development baseline to repeatable judgement (no random rolls), and writes the lobby profile list.
- Its `Validate()` is read-only. It checks the **default** graph with an exact 22-node expectation, both guards, the step order, launch resolution of beginner/advanced/baseline, knowledge coverage and the lobby list. It is not a general validator for a customized graph.

## 3. Current graph: exact execution structure

The saved default graph has 22 nodes including its start and flow nodes. Numbers in brackets are the spec's decision steps:

```text
On Start [Repeat = false]
└─ Try In Order
   ├─ Bounded Solo retry
   │  └─ Sequence
   │     ├─ Capture own and public observation
   │     ├─ Build legal own item candidates                 [1]
   │     ├─ Score candidates by difficulty                  [2]
   │     ├─ Try In Order
   │     │  ├─ Sequence
   │     │  │  ├─ Conditional Guard: Bot is in mortal danger
   │     │  │  └─ Pick best recovery or defense             [3]
   │     │  ├─ Sequence
   │     │  │  ├─ Conditional Guard: Lethal attack must be taken
   │     │  │  └─ Pick lethal attack                        [4]
   │     │  └─ Pick by difficulty odds                      [5]
   │     ├─ Wait for bounded thinking
   │     ├─ Decide mini-game outcome and submit delayed item [6]
   │     ├─ Await item delay or opponent Ready              [7]
   │     ├─ Choose Ready timing
   │     ├─ Wait for safe Ready time
   │     └─ Submit authoritative Ready
   └─ Fallback Ready without item
```

The guards are the package's built-in **Conditional Guard (Action)** with project conditions (`SoloInCrisis`, `SoloMustFinish`) that read the latest score sheet.

- **Sequence:** continue after each successful step; a failed step fails the sequence.
- **Try In Order:** try the next child when a child fails; stop at the first success. It is not random selection and does not continuously rescore every branch while a later step runs.
- **Bounded Solo retry:** retry the sequence from observation, within the same valid preparation window. Budget `2` allows an initial attempt plus at most two retries.
- **Fallback:** submit Ready without a new item only while the bot can still act and has no pending item operation. It does not guarantee success after the input window closes, and it does not explicitly clear an already queued selection.

The controller starts one run per committed `PrepInputKey` (generation, round, turn, sequence). It creates/initializes a runtime `BehaviorGraphAgent`, keeps the agent component disabled, and ticks its graph manually while the run is valid. This is intentional. Do not enable a second automatic tick loop, add another controller, or enable Start repetition.

### Existing nodes and their code boundary

All custom actions are in `Action/Absolute Zero/Solo`; the retry modifier is in `Flow/Absolute Zero`.

| Node class | Controller call | Result |
|---|---|---|
| `SoloObserve` | `CaptureObservation()` | Copy own state and public opponent state, including the held item list |
| `SoloCandidates` | `BuildCandidates()` | Validate usable own inventory entries, delays and current heals |
| `SoloScore` | `ScoreCandidates()` | Score sheet: danger, recovery/defense multiplier, ranked positive items |
| `SoloInCrisis` (condition) | `InCrisis` | True in mortal danger |
| `SoloPickCrisis` | `PickCrisis()` | Best recovery/defense (odds only when the difficulty may miss in crisis) |
| `SoloMustFinish` (condition) | `MustTakeLethal` | True when a lethal attack exists and the difficulty never misses it |
| `SoloPickLethal` | `PickLethal()` | Best lethal attack |
| `SoloPickWeighted` | `PickWeighted()` | Best / second / any positive item by difficulty odds |
| `SoloThinking` | `Think()` | Running until thinking ends; fails to re-observe when mortal danger newly appears; succeeds at once if the opponent is Ready |
| `SoloSubmitUse` | `Submit()` | Start one delayed item request; the boundary rolls mini-game success here |
| `SoloAwaitUse` | `AwaitUse()` | Running while pending; success when queued, or when a failed roll consumed the item |
| `SoloChooseReady` | `ChooseReadyTime()` | Set the intended Ready time |
| `SoloWaitReady` | `WaitReady()` | Wait until that time or urgent temperature |
| `SoloReady` | `SubmitReady()` | Success only on authoritative Ready acceptance |
| `SoloFallback` | `Fallback()` | Try Ready without a new item request |
| `SoloRetry` | `CanAct()`, `RetryBudget` | Bound retries and respect the input window |

## 4. Understand the current decision rules before tuning

The approved spec is recorded in [PLAN_046](Plans/PLAN_046_solo_bot_difficulty.md). The formulas live in `BotDecisionModel.Score`.

- **Score unit:** temperature. Item effect values come from the item assets.
- **Success:** 1 for items without a human mini-game; otherwise the difficulty's tier rate (easy / normal / hard).
- **Lookahead:** applies to effects on a later turn.
- **Recovery/defense multiplier:** crisis multiplier in mortal danger; comfort multiplier when own temperature − opponent temperature ≥ comfort margin; otherwise 1.

| Item effect (examples) | Score |
|---|---|
| Direct attack (Fan, Ice Cream, Water Gun, Iced Americano, Hand Fan) | damage × success × (1 − block chance) |
| Hug T-shirt | (opponent temperature − own temperature) × success; ≤ 0 is excluded |
| Windbreaker / Mask | opponent's highest usable tool / food damage × opponent attack chance × recovery/defense multiplier |
| Recovery (Warm Tea, Hot Pack, Hot Americano, Smartphone current use) | min(heal, 37 − own temperature) × success × recovery/defense multiplier |
| Buldak / Soda | (min(next-turn heal, 37 − (own temperature − own cost)) × lookahead − own cost) × success × recovery/defense multiplier; Soda is excluded when its −5 would cause mortal danger |
| Samgyetang | (next-turn damage × lookahead − immediate target heal) × success × (1 − food block chance) |
| Cat | sum of opponent random item worth × 0.4 × lookahead |
| Claw | highest stealable opponent item worth × 2 × lookahead × success; excluded when own inventory is full |
| Blue Tape | 3 × lookahead × success |
| Red Card | highest opponent item worth × 0.7 × success |
| Screwdriver | opponent cooling per Prep × (boosted fan / current fan − 1) × 2 turns × lookahead × success; excluded when already boosted |

Supporting definitions:
- **Block chance** is the difficulty's opponent block chance. It applies only while the opponent can use the matching Windbreaker (tool) or Mask (food).
- **Mortal danger:** own temperature − own cooling until Ready − opponent's highest usable attack ≤ 0.
  - Own cooling until Ready = own fan speed × shortest affordable item delay, or 0 once the opponent is Ready.
- **Lethal:** a direct attack whose score is at least the opponent's temperature. It gains `kill bonus × success`.
- **Opponent cooling per Prep:** the publicly observed drop from the opponent's Prep-start temperature to their Ready (average of the last 3). Before the first observation it is half the Prep window at the opponent's fan speed.

Each item is ranked once even when several copies are held. Items whose final score is ≤ 0 are never picked. Ties keep the item knowledge row order. Every candidate must still fit `delay + Ready reserve + 0.05 < remaining seconds` (the delay counts as zero once the opponent is Ready).

Branch rules:
- **Crisis guard:** the best positive Recovery or Defense item, or the odds pick among them when the difficulty may miss in crisis.
- **Lethal guard:** only when the difficulty never misses a lethal attack (advanced). Beginner keeps the bonus but uses the odds.
- **Odds:** with probability best → first, best + second → second, otherwise uniform among all positive items. The bot's private per-Prep RNG decides.
- **Change priority** by editing the graph order, and the values by editing the difficulty/knowledge assets. Preserve Observe, legal validation, delayed submission, Ready and the bounded fallback around the selector.

### Estimates are deliberately limited

- **Observation** contains:
  - own candidates;
  - public temperatures and fan speeds;
  - opponent Ready;
  - Blue Tape state;
  - the opponent's held item identities, which are shown on screen (Q15).
- **Never observed:** the opponent's selected item, queue and use counts (Q22).
- **Scores are expectations, not guarantees.** Shared combat still applies defense, order and timing. In code, the Windbreaker is a −2 reduction, while the spec formula treats it as a block chance.
- **Reveal and extra-action items** (disabled Tarot) have no strategy and no knowledge row.

Additional controller safety thresholds:
- Thinking re-observes when mortal danger newly appears, and ends at once at 5° or below or once the opponent is Ready.
- NearDeadline Ready waits only while the temperature is above 10 and the opponent is not Ready, and uses at least 0.15 seconds of reserve.

## 5. Configure a bot without adding C#

| Asset / field | Meaning and constraints |
|---|---|
| `BotDefinitionSO.graph` | Compiled graph for this bot |
| `BotDefinitionSO.itemKnowledge` | Required for normal launches (optional for fixtures): per-item opponent worth, mini-game tier (required for mini-game items), score coefficients; unique current-catalog items; row order breaks ties |
| `BotDifficultyProfileSO.judgement` | Success rates per tier, best/second pick chances (sum ≤ 1), lookahead, opponent block/attack chances (all 0..1), kill bonus, crisis/comfort multipliers and comfort margin (≥ 0), may-miss-lethal / may-miss-crisis |
| `extraItemDelaySeconds` | Finite, nonnegative; added to every effective item delay (beginner 2) |
| `minimumThinkSeconds`, `maximumThinkSeconds` | Finite, nonnegative; maximum >= minimum. Thinking is shortened when the available preparation time is insufficient. Approved profiles use 0 |
| `readyPolicy` | `AfterSelection` or `NearDeadline`; the latter can affect ordinary 1v1 initiative/cooling |
| `readyReserveSeconds` | Nonnegative and shorter than the shared Prep duration |
| `retryBudget` | Integer 0..32; zero means no retry after the initial attempt |
| `BotItemUsePolicySO.defaultDelaySeconds` | Finite and strictly positive; used when an item has no override |
| `BotItemUsePolicySO.overrides` | Unique current-catalog item references and positive delays |
| `BotDifficultyProfileSO.itemUseOverride` | Replaces the entire base item-use policy, not just matching entries |
| Bot cosmetic IDs | Existing registry IDs for the correct part, or empty for the base character |

The resolver validates both weight sets and both referenced item policies even when an override determines the effective values. Keep unused-but-referenced profiles valid too.

Player profiles (`BeginnerSoloDuel`, `AdvancedSoloDuel`) share `DefaultBot`, the production graph and `StandardItemUsePolicy` (2 s instant, 4 s mini-game). They differ only in their difficulty asset: beginner adds 2 s. Both use 0 thinking, 0.25 s Ready reserve, two retries and AfterSelection Ready, and remain `provisionalTuning` until play-tested.

The development baseline (`DefaultSoloDuel` / `NeutralBaseline`) keeps 0.2–0.45 s thinking and the 0.75 s / 1.5 s delays. It uses the advanced judgement with success rates 1 and best pick chance 1, so fixtures stay repeatable.

Per-item override entries take precedence over the default: changing only the default does not change an item that already has an override.

### Settings that are not ready for Inspector-only customization

`BotStatProfileSO` contains starting temperature, fan baseline and initial-item fields, but `SoloSettingsResolver.CheckStats` currently **rejects custom overrides or a nonempty initial-item list**. `ResolvedSoloSettings` still inherits the shared 1v1 baseline and grants. Checking an override does not activate the feature; it fails launch.

To implement this later, specify whether each value applies per match or per round, how custom initial items replace/add to shared grants, and how fan reset works. Then implement resolver snapshots, authoritative application/reset and tests as one task. The user intends these controls and difficulty selection to be part of future BT work; this guide does not claim they are already functional.

Ordinary Editor/Development entry accepts the current provisional tuning. Non-development entry rejects it. Final reviewed tuning, asset flags and release validation must agree before shipping. Do not bypass the release guard to test a new personality.

## 6. Write a custom action node

First reuse an existing node when it already expresses the required step. For a genuinely new step, keep the node thin: obtain the controller, call its narrow operation, return the correct status. Unity's wizard supports **right-click empty graph space → Create new → Action** and generates the node class and lifecycle methods; see [Unity's custom-node instructions](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/create-custom-node.html).

The example below wraps the **existing lethal pick** to show the project integration pattern. It adds no new intelligence and is unnecessary if `SoloPickLethal` already fits.

Suggested location if intentionally installed: `Assets/Scripts/Core/Solo/Nodes/SoloChooseFinishExample.cs`. Keep it in the existing `AbsoluteZero.Core` assembly; controller decision methods are `internal`. A file in an unrelated assembly cannot call them simply by importing the namespace.

```csharp
using System;
using Unity.Behavior;
using Unity.Properties;

namespace AbsoluteZero.Core.Solo
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(
        name: "Choose finish example",
        story: "Choose estimated lethal damage",
        category: "Action/Absolute Zero/Solo",
        id: "af827a82a15b421a94913470c3f911a8")]
    public partial class SoloChooseFinishExample : Unity.Behavior.Action
    {
        protected override Status OnStart()
        {
            var controller = GameObject != null
                ? GameObject.GetComponent<BotTurnController>()
                : null;

            if (controller == null || !controller.CanAct())
                return Status.Failure;

            return controller.PickLethal()
                ? Status.Success
                : Status.Failure;
        }
    }
}
```

- Generate a different unique node ID for each new node type; keep an existing type's ID stable. The shown ID belongs to this example only. PowerShell can generate one with `[guid]::NewGuid().ToString('N')`.
- Keep `[Serializable, GeneratePropertyBag]`, `partial` and the node metadata. Resolve compilation errors before adding the node to a graph.
- The graph's owner GameObject is the controller object. It is not necessarily the bot's character GameObject; do not obtain the actor with `GameObject.GetComponent<PlayerState>()`.
- Place this kind of selection node inside the pick selector **after Observe, Candidates and Score**, before Thinking/Submit.
- `Success` here means a legal candidate was chosen, not that its damage has happened.

### Waiting and asynchronous operations

`OnStart` begins a node; `OnUpdate` polls a running node. A pending operation returns `Running`, not `Success`. `Failure` must let the surrounding retry/fallback act. Use `OnEnd` to clean up resources owned by that node when appropriate. Package lifecycle background: [Unity Behavior node types](https://docs.unity3d.com/Packages/com.unity.behavior@1.0/manual/node-types.html).

The existing pattern in `SoloAwaitUse` is:

```csharp
protected override Status OnStart() =>
    GameObject.GetComponent<BotTurnController>().AwaitUse();

protected override Status OnUpdate() =>
    GameObject.GetComponent<BotTurnController>().AwaitUse();
```

This is an excerpt from an existing class, not another standalone script to paste into the project. Submission happens once in the preceding `SoloSubmitUse`; the waiting node only polls that operation. Do not submit again every frame, add another timer, or play a fake mini-game. `BotItemUseOperation` already owns the authoritative delay. `BotTurnController.StopRun` aborts pending work on cancellation/invalid context. Do not indiscriminately abort that shared operation from every action's `OnEnd`, which also runs at normal action completion.

## 7. Add a new branch or smarter scoring

Use this order for a focused implementation task:

1. **Define the decision in plain language.** State the public inputs, legal candidate set, priority, fallback and expected examples. Do not begin by creating new RPCs.
2. **Prefer data when sufficient.** Changing success rates, odds, multipliers, item worth or coefficients needs only the difficulty/knowledge assets. A new branch is useful only when it has distinct eligibility/priority.
3. **Extend the pure model.** Use `BotObservation`, `BotCandidate` and `BotItemProfile`, not scene searches or `PlayerState` inside scoring. Add missing public observations through the controller, copying them into an immutable snapshot. Do not read the opponent's selection, queue or use counts.
4. **For a new item effect**, map it in `BotDecisionModel.Profile` (formula, category, kind) and give it a score case. Unknown effects stay unchosen; do not assign invented damage.
5. **For new profile knobs**, add serialization/defaults, resolver range checks and a copied field in `ResolvedSoloSettings`. Update configuration versions and existing-profile defaults together.
6. **Expose it through the controller.** A new pick or guard needs its own narrow controller member with `CanAct` and the existing session/key checks.
7. **Add the thin node or condition and graph branch.** Assign a unique type ID and intentional left-to-right position. Preserve the single submission/await/Ready sequence.
8. **Add decision tests first**, then verify the saved graph, authoritative delay/cancellation and ordinary matches. Preserve baseline online 1v1/Multi behavior whenever shared code is touched.

For a newly introduced item subtype, also implement its `ItemEffectRuleSnapshot` mapping and shared effect handling. An unsupported effect fails Solo configuration resolution today.

### Example decision test

This illustrative test uses current internal APIs. If adopted, place it in the existing `Assets/Tests/Editor` assembly (`AbsoluteZero.EditorTests` already has friend access). The fixture tests the scorer, not graph wiring or actual combat. Similar coverage already exists in `Plan036BotDecisionTests` and `Plan046BotJudgementTests` (the spec's worked example with real items); avoid adding a duplicate test without a new behavior to protect.

```csharp
using System;
using AbsoluteZero.Core.Solo;
using AbsoluteZero.Core.Solo.Configuration;
using AbsoluteZero.Core.Turn;
using NUnit.Framework;

namespace AbsoluteZero.Tests
{
    public sealed class SoloAuthoringExampleTests
    {
        [Test]
        public void AdvancedTakesTheOnlyLethalAttack()
        {
            var profiles = new[]
            {
                new BotItemProfile(0, true, BotItemFormula.DirectAttack, BotItemCategory.Attack, BotAttackKind.Tool, 2, 0, 1, 3, false, false, 0),
                new BotItemProfile(1, true, BotItemFormula.DirectAttack, BotItemCategory.Attack, BotAttackKind.Food, 4, 0, 1, 5, true, true, 1)
            };
            var observation = new BotObservation(new PrepInputKey(1, 1, 1, 1), 1, 0,
                temperature: 30, opponentTemperature: 3, fan: 1, opponentFan: 1, opponentReady: false, remaining: 10,
                opponentPrepLoss: 3, ownSlots: 6, slotCapacity: 12, opponentItems: Array.Empty<BotOpponentItem>(),
                candidates: new[] { new BotCandidate(11, 0, 0, 2), new BotCandidate(12, 0, 1, 2) });
            var sheet = BotDecisionModel.Score(observation, profiles, BotJudgementSettings.Advanced, BotScoringCoefficients.Approved, 0.25);

            Assert.That(BotDecisionModel.TryPickLethal(sheet, BotJudgementSettings.Advanced, out var chosen), Is.True);
            Assert.That(chosen.Copy, Is.EqualTo(12u));
        }
    }
}
```

Add cases for no eligible item, exact deadline equality, crisis/lethal thresholds, odds frequencies, fixed RNG replay and unchanged source data. To verify first-success priority, also test the graph; scorer tests do not prove selector order.

## 8. Inspect decisions while playing

Launch ordinary Solo from the lobby and filter the Unity console or Development Player log for `[SoloBT]`.

Expected event progression is:

```text
start seed=... difficulty=... -> observe remaining=... opponentItems=... -> legal candidates=...
-> score crisis=... care=... loss=... threat=... ranked=Item:final(! = lethal),...
-> crisis/lethal/odds copy=... item=... target=... think=...
-> submit Pending:... due=... -> operation Queued:... (or Cancelled:MiniGameFailed) -> ready ReadyAccepted:...
```

This is a schematic, not a captured test result. Actual lines include generation `g`, round `r`, turn `t`, sequence `seq` and server-clock `time`. Record those fields plus encounter/profile versions when reporting a bug. The controller retains its latest 256 trace entries and exposes `RunCount`, `SelectionCount`, `ReadyCount`, `IsRunning` and `RuntimeGraph` for inspection.

The graph has these debug Blackboard variables:

| Name (exact) | Type | Meaning |
|---|---|---|
| `Round` | int | Current input key's round |
| `Turn` | int | Current input key's turn |
| `Own temperature` | float | Captured own temperature |
| `Opponent temperature` | float | Captured opponent temperature |
| `Remaining seconds` | float | Captured preparation time remaining |
| `In crisis` | bool | Latest score sheet's mortal-danger result |
| `Lethal available` | bool | A lethal attack is in the latest ranking |
| `Selected item` | string | Chosen item asset name, or empty before selection |
| `Selected copy` | string | Chosen inventory CopyId, or `0` before selection |

These are observation/debug outputs, refreshed on observation/choice, not continuously live values or Inspector tuning inputs. Editing them does not change the model's private observation. Preserve the graph owner variable and per-run runtime graph isolation; inspect `RuntimeGraph` for live values rather than treating the source asset as runtime state.

| Symptom | Inspect first |
|---|---|
| Bot never starts | Ordinary encounter vs fixture, current host/Prep window, controller present, graph compiled, startup error |
| Changing odds or rates has no visible effect | Earlier successful crisis/lethal guard, item not in the knowledge table, edited the wrong difficulty asset, match not restarted |
| Bot repeatedly uses the same item | Resource branch eligibility, candidate scores/ties, available inventory and legal target |
| Bot uses no item | No legal candidate, insufficient delay budget, no positive score, retry exhausted |
| Bot waits longer than expected | Thinking range, per-item override, NearDeadline Ready, remaining server-clock time |
| New node calls fail to compile | Wrong assembly, missing `partial`/attributes, nonexistent controller API, namespace collision |
| Graph changes have no effect | Encounter references another bot/graph, duplicate graph runtime reference, stale player build |
| Start-stat checkbox prevents launch | Deliberate custom stats/loadout guard; feature is still pending |
| Customized graph fails 22-node validation | Baseline-specific builder check; validate the intended topology with explicit expectations |

## Editor authoring workbench (PLAN040 BT01-A)

For step-by-step workbench instructions, see [Bot Authoring Workbench — User Guide](BOT_AUTHORING_TOOL_GUIDE.md).

Open **Absolute Zero > Solo > Bot Authoring**, or select a Solo configuration asset and click **봇 설정 검증 창 열기** in its Inspector.

1. Select an encounter from the asset field or the registered encounter list. `ValidationFixture` entries are labeled as test-only. Selecting one does not start a game or approve release tuning.
2. Click **저장된 로비 연결로 다시 검사** after edits. The workbench reads the saved `LobbyScene` item/cosmetic bindings in a temporary preview scene, closes it in `finally`, and checks the enabled Build Settings scene list. It does not include unsaved lobby edits, modify the current scene, or save assets.
3. Read three separate launch results: ordinary development Solo, release Solo, and explicit fixture entry. They call the existing resolver; a default profile can pass development and still correctly fail release because tuning remains provisional.
4. Use **수정 위치** to select the encounter, bot, difficulty, graph, stats or item-delay asset. The Inspector explains each configuration type. Runtime editing of these Inspector fields is disabled. Fields still use Unity serialized binding / Undo.
5. Review the effective values and item timing table. Difficulty item-use overrides replace the whole base policy, and the difficulty's extra delay is added to every item. Disabled Tarot remains in the stable catalog but is marked as excluded.
6. The table shows *nominal* think + item delay + Ready reserve. Overflow is a warning, not a new launch rejection: the existing controller shortens thinking, filters unaffordable actions and can fall back to Ready. Frame overhead, observation time, graph correctness and actual inventory still require play tests.
7. Start settings remain **inherit only**. Turning on an override or adding initial items is rejected until the start/reset/grant policy is approved. The displayed inherited temperature/fan values are taken from the resolved settings, not editable duplicate defaults.

The tool also reports duplicate IDs across saved configuration assets, including otherwise independent encounters. Shared references to the same asset are allowed. Copying a profile requires a new stable ID; update the version when changing approved data. This diagnostic does not silently rename IDs or change the launch policy.

| Failure | Where to correct it |
|---|---|
| Missing/placeholder graph | Bot asset → Graph; compile the intended Behavior graph and check its own topology |
| Null/duplicate/unsupported catalog entry | Saved LobbyScene → AZLobbyUI → Solo Catalog; preserve network catalog ordering |
| Unknown cosmetic ID | Bot asset → Cosmetics; validate against the saved lobby registry |
| Invalid think/range/reserve/judgement values | Difficulty asset; chances in [0, 1], best + second ≤ 1, finite nonnegative multipliers and delays |
| Missing item knowledge / mini-game tier | Bot asset → Item Knowledge; one row per enabled catalog item, tier required for mini-game items |
| Missing/duplicate item-delay entry | Effective item-use policy; item references must belong to the current catalog |
| Custom stats/loadouts disabled | Stats asset; keep overrides off and initial items empty until BT01-B decisions |
| Provisional shipping rejection | Expected while balance is pending; do not turn off the flag merely to clear the warning |
| Scene not loadable | Encounter full scene path + enabled Build Settings; do not auto-add fixture scenes to product builds |

The workbench and its report code are Editor-only. It does not build or run the baseline graph generator. BT02-A adds an explicit player-facing selection screen. The saved lobby profile list controls visibility; it currently contains the baseline only. A selection is revalidated on Start, and replay retains the captured settings. Implementation and validation status remain in the PLAN040 ledger.

## 9. Validation checklist for a BT change

These are **checks to run after an implementation/tuning change**, not new passes claimed by this documentation task.

### A. Configuration and graph

- [ ] Unique configuration IDs; valid versions, references, finite values and supported item mappings.
- [ ] Correct compiled BehaviorGraph reference, no placeholders/missing node types; Start Repeat off.
- [ ] Explicit child order and one legal submit/await/Ready path; bounded retry plus reachable fallback.
- [ ] Private runtime graph/Blackboard; shared assets unchanged by play.
- [ ] Intended encounter resolves with `SoloSettingsResolver.TryResolveForDevelopment`; its scene is enabled/loadable. A general resolver pass proves structural/configuration validity, not that a custom graph makes progress.
- [ ] For a changed graph, assert the intended nodes, order and controller operations in its own validation. Keep baseline-specific checks meaningful; do not remove assertions just to accept any graph.

### B. Focused Editor tests

Run relevant tests from the existing Editor Test Runner/Unity CLI path:

- `Plan036BotDecisionTests`: affordability, duplicates, unknown items, heal cap, isolated RNG.
- `Plan046BotJudgementTests`: the approved score sheet's worked example with real items, crisis, lethal, odds frequencies and exclusions.
- `Plan036BotDelayTests`: delayed operation lifecycle and idempotency.
- `Plan036ActionAuthorityTests` and `Plan036PrepInputTests`: validated requests, identity and preparation-window boundaries.
- `Plan036SoloConfigurationTests` and `Plan036SoloConfigurationAssetTests`: configuration/resolution, asset references and release gates.
- New focused cases for any new decision rule; exact shared regression scope depends on changed code.

### C. Play Mode and real development player

- [ ] Ordinary lobby → Solo → natural match result → replay → lobby; no fixture replacing decisions.
- [ ] Each intended branch (crisis / lethal / odds) actually executes with a recorded legal candidate and target; mini-game failures consume once and Ready follows; the human's Ready ends the bot's delay.
- [ ] Cooling during thinking triggers the intended re-observation; no stale item submission.
- [ ] Item disappears/becomes invalid before submission; bounded replanning/fallback works.
- [ ] Deadline too close for an item; no late consumption or stuck preparation phase.
- [ ] Exit while thinking and while item delay is pending; old work does not execute after re-entry.
- [ ] Exactly one active graph/controller; no duplicate item/Ready requests or gameplay RNG pollution.
- [ ] Shared 1v1/Multi regressions if authority, item effects, grants or turn code changed.

Existing boundary fixtures: `Plan036ExtensionValidation.Start()`, `Plan036BoundaryValidation.Start()` and `Plan036ExitValidation.Start()`. They inject specific situations and are separate from natural-match evidence; read their entry requirements in [setup](SOLO_BOT_SETUP.md) before running them.

For an existing, freshly rebuilt fixed test player, a natural Solo sample can use:

```powershell
$repo = 'C:\Users\paek6\Absolute Zero'
$evidence = Join-Path $repo ('output\validation\solo-bt\' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
& (Join-Path $repo '.codex\scripts\run_solo_acceptance.ps1') `
    -Mode corpus -OutputDirectory $evidence `
    -Seed 3609 -HumanPolicy mixed -MatchTimeoutSeconds 600
```

The helper uses `%TEMP%\AZVisual4P_Current\Player\AbsoluteZeroVisual4P.exe`; it does not rebuild your changes. `corpus` runs one local player process with the normal bot and automated human inputs. `mixed` samples human items that do not require mini-games, so it is not all-item/manual-input coverage. Inspect `summary.json`, the report files and logs, not only process exit. Use `replay` for repeated matches; `transitions` additionally exercises real online/Relay transitions and is not needed for every scoring edit.

A seed supports diagnosis but does not guarantee bit-identical full matches under real frame timing. Human play is still needed to judge fairness, difficulty and pacing. Compilation and passing unit tests alone do not establish those qualities.

## 10. Copyable implementation handoff

```text
Implement one Solo BT change using Docs/SOLO_BT_AUTHORING.md.

Goal / player-visible behavior:
Target encounter, bot, difficulty and graph assets:
Allowed own/public observations:
Eligibility and score examples, including one negative example:
Priority relative to crisis guard / lethal guard / odds pick:
Think delay / item-delay policy / Ready policy (approved values only):
Fallback when there is no legal or affordable item:
New configuration fields or stat/reset decisions required:

Keep BotTurnController as the graph-run owner, TurnManager as phase owner,
and TrustedBotCommandAdapter/BotItemUseOperation as the action boundary.
Do not add bot mini-game UI, reads of the opponent's selection/queue, direct effect writes,
duplicate host/player objects, or unbounded retries.

Implement one small unit, run focused tests and a relevant runtime case,
fix failures, and report files changed, evidence and remaining limitations.
Do not label provisional tuning or unsupported stat overrides complete.
```

## Source map and documentation verification

- [Controller and run lifecycle](../Assets/Scripts/Core/Solo/BotTurnController.cs)
- [Action nodes and bounded retry](../Assets/Scripts/Core/Solo/SoloBehaviorNodes.cs)
- [Observation, estimates and scoring](../Assets/Scripts/Core/Solo/BotDecisionModel.cs)
- [Configuration resolver](../Assets/Scripts/Core/Solo/Configuration/SoloSettingsResolver.cs) and [resolved settings](../Assets/Scripts/Core/Solo/Configuration/ResolvedSoloSettings.cs)
- [Trusted commands](../Assets/Scripts/Core/Solo/TrustedBotCommandAdapter.cs) and [delayed operation](../Assets/Scripts/Core/Solo/BotItemUseOperation.cs)
- [Baseline graph authoring helper](../Assets/Editor/Plan036BotGraphBuilder.cs)
- [Decision tests](../Assets/Tests/Editor/Plan036BotDecisionTests.cs)
- [Lobby encounter binding](../Assets/Scripts/UI/Lobby/AZLobbyUI.cs)

For this guide, the source signatures, current saved graph order, profile values, package node lifecycle/order implementation and official Unity documentation were inspected. Examples remain documentation snippets: no new node was installed, compiled or exercised in a live graph by this documentation task. Existing test results remain in the validation ledger. `Docs` is the Obsidian vault source, so maintain this guide here without a second copied note.
