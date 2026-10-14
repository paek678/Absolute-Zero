# Solo BT authoring guide

Updated for the **2026-10-14 Solo integration worktree**, Unity Behavior1.0.16. Original main is not yet merged. [Setup](SOLO_BOT_SETUP.md), [specification](Plans/PLAN_solo_bot_main_integration_20261013.md), [actual validation](Validation/solo_bot_main_integration_20261013.md).

## 1. Where behavior comes from

| Change | Owner |
|---|---|
| Priority of crisis, lethal and weighted choices | Production Behavior graph and its guards |
| Pure item value, cooling/threat projection and deterministic ordering | `BotDecisionModel.cs` |
| Difficulty chances and effective delay | Difficulty, knowledge and item-use profiles; copied by resolver |
| A graph step | Thin nodes in `SoloBehaviorNodes.cs`, conditions in `SoloBehaviorConditions.cs` |
| Valid submission, failed-use debit and Ready | Controller -> trusted adapter -> existing authoritative game |

The legacy `BotTactic`/`BotTacticalWeights`, five tactic nodes and production `Estimate` API were removed. Do not copy their old examples into this integration. The automated human-policy evaluator deliberately preserves the pre-integration formula under development symbols; it is separate from the new bot scorer.

## 2. Current graph

```text
On Start (Repeat=false)
└─ Try In Order
   ├─ Bounded Solo retry
   │  └─ Sequence
   │     ├─ SoloObserve
   │     ├─ SoloCandidates
   │     ├─ SoloScore
   │     ├─ Try In Order
   │     │  ├─ Conditional Guard: SoloInCrisis -> SoloPickCrisis
   │     │  ├─ Conditional Guard: SoloMustFinish -> SoloPickLethal
   │     │  └─ SoloPickWeighted
   │     ├─ SoloThinking
   │     ├─ SoloSubmitUse
   │     ├─ SoloAwaitUse
   │     ├─ SoloChooseReady
   │     ├─ SoloWaitReady
   │     └─ SoloReady
   └─ SoloFallback
```

The controller initializes a private runtime graph, disables the agent's automatic Update and ticks it once through the existing owner. Never add a second tick loop or repeating Start. Sequence requires successful steps; Try In Order stops on first success. Guard priority is meaningful: crisis care precedes a lethal attack. Crisis without eligible care selects no-item Ready and must not fall through to attack. Retry budget2 allows at most three attempts in the current legal window; fallback never authorizes a stale request.

Installed Behavior sorts outgoing children by ascending X. Preserve intentional distinct left-to-right positions and inspect the compiled order after rearranging. Duplicate graphs/assets in Unity rather than editing YAML/GUIDs. Keep a compiled BehaviorGraph binding, unique IDs/versions and normal Ready encounters distinct from explicit validation fixtures.

`Plan046SoloBotBuilder` is the migration/default builder, not a routine graph-save button. It changes default bindings/configuration and validates the intended baseline topology. Preserve custom graphs/profiles and read its scope before running it. The removed `Plan036BotGraphBuilder` is no longer a valid entry point.

## 3. Actual node calls

| Node / condition | Controller boundary |
|---|---|
| `SoloObserve` | `CaptureObservation()` |
| `SoloCandidates` | `BuildCandidates()` |
| `SoloScore` | `ScoreCandidates()` |
| `SoloInCrisis` | Read `InCrisis` |
| `SoloMustFinish` | Read `MustTakeLethal` |
| `SoloPickCrisis`, `SoloPickLethal`, `SoloPickWeighted` | `PickCrisis()`, `PickLethal()`, `PickWeighted()` |
| `SoloThinking` | Poll `Think()` |
| `SoloSubmitUse` | Submit once through `Submit()` |
| `SoloAwaitUse` | Poll `AwaitUse()` without a new roll/submission |
| `SoloChooseReady`, `SoloWaitReady`, `SoloReady` | Choose, wait and validate Ready |
| `SoloFallback`, `SoloRetry` | Bounded legal-window fallback/retry |

Selection success means a candidate was chosen; it does not mean damage occurred. Pending returns Running. A real modeled mini-game failure is a terminal one-use failure outcome, not a fresh retry until success. Stop/exit/death/expiry cancel pending work, and old callbacks may not debit or act in a new generation.

## 4. Scoring and data

`BotObservation` copies own and public data. Public held opponent items support Cat/Claw/Red Card value; opponent selection/future rolls remain unknown. Each candidate identifies an individual `CopyId` and legal target. Delay/cooling, public due effects where available, over-cap recovery, item-specific defense and steal capacity are projected conservatively. Shared item assets supply actual effect values; correct the bot when estimates disagree with gameplay.

`BotScoreSheet` records crisis/care/loss/threat and ranked entries. Knowledge entry order provides stable tie ordering. Current categories are Attack, Defense, Recovery, Disrupt and NextTurn. Difficulty chooses best, second or a uniform positive-score entry with the remaining probability. These are candidate probabilities, not branch weights. Advanced's safe lethal guard is distinct from Beginner's permission to miss lethal. The selected item's mini-game tier selects one success probability; one operation rolls it once. Poll/replay must not reroll.

| Configuration | Current meaning |
|---|---|
| Bot graph / knowledge / appearance | Compiled graph, item worth/tier/coefficient sheet, existing appearance IDs |
| Judgment | Mini-game success by tier, best/second chances, lookahead, defense expectation and care/lethal settings |
| `extraItemDelaySeconds` | Added to effective per-item delay |
| Ready reserve / retry budget | Existing input-window safety and bounded retries |
| Item policy override | Replaces the entire base policy; per-item entries precede its default |
| Starting stats/grants | Inherit current 1v1 rules; custom overrides still rejected |

All probabilities/coefficients must be finite and within resolver ranges; best+second cannot exceed1. Normal integrated entry currently requires zero thinking and AfterSelection Ready. Nonzero thinking/NearDeadline belong to separately labeled boundary fixtures. Both current difficulties and delay policies remain provisional; current values and timing tables are in [setup](SOLO_BOT_SETUP.md) and the workbench.

Register the desired encounter in saved `AZLobbyUI` Solo Profiles with a title/description. Start revalidates the profile; replay retains the captured settings. A file's presence alone does not register it. [Workbench](BOT_AUTHORING_TOOL_GUIDE.md).

## 5. Add a node or scoring change

1. State the public inputs, legal candidates, priority, fallback and examples, including a negative case.
2. Prefer a pure scoring/configuration change when it requires no new control flow.
3. Keep scoring free of scene searches, mutable SO state and direct effects. Copy any additional permitted public input through the controller.
4. Add a narrow controller operation with current key/session checks, then a thin graph node with a unique stable type ID.
5. Preserve the single submit/await/Ready path and crisis priority; no new RPC/turn driver or hidden-opponent shortcut.
6. Add meaningful focused decision/boundary tests, compiled-graph checks and actual relevant runtime evidence. A scorer unit test does not prove graph priority or gameplay.

Thin-node pattern, illustrating the existing lethal call (not an installed new node):

```csharp
using System;
using Unity.Behavior;
using Unity.Properties;

namespace AbsoluteZero.Core.Solo
{
    [Serializable, GeneratePropertyBag]
    [NodeDescription(name: "Lethal example", story: "Choose the legal lethal candidate",
        category: "Action/Absolute Zero/Solo", id: "af827a82a15b421a94913470c3f911a8")]
    public partial class SoloLethalExample : Unity.Behavior.Action
    {
        protected override Status OnStart()
        {
            var controller = GameObject != null ? GameObject.GetComponent<BotTurnController>() : null;
            return controller != null && controller.CanAct() && controller.PickLethal()
                ? Status.Success : Status.Failure;
        }
    }
}
```

Generate a fresh ID before installing a new type; retain existing node IDs. Keep code in the Core assembly because decision methods are internal. The graph owner is the controller object, not necessarily the character. Reuse the current guard for this lethal step. The example adds no new strategy and needs no duplicate test merely for wrapping an existing method.

For asynchronous nodes, OnStart begins and OnUpdate polls; clean up only resources owned by that node. `SoloAwaitUse` calls `AwaitUse()` in both methods. The preceding node already submitted: do not resubmit every frame, add another timer/roll or abort the shared operation on every successful node OnEnd.

## 6. Debugging and evidence

Filter a Development Player log for `[SoloBT]`. A typical path is `seed -> observe -> legal -> score -> crisis/lethal/odds -> submit Pending -> operation Queued/Failed -> Ready`. Trace includes generation, round, turn, sequence/server time and positive admitted request/operation/copy identities. Keep exact requested/resolved encounter/difficulty, graph/build identity and effective settings with a report; folder labels alone are insufficient.

Inspect the controller's `RuntimeGraph`, `RunCount`, `SelectionCount`, `ReadyCount` and bounded trace. Blackboard observation/choice values are debug output, not live tuning controls. Do not mutate the shared authoring graph's owner variable to debug a runtime instance.

| Symptom | First check |
|---|---|
| Bot does not start | Exact selected ID, Ready/resolver result, compiled root, host/key ownership |
| No item | Legal copies, time/cooling budget, crisis without care, empty positive-score list |
| Repeated Fan | Available inventory and actual ranked/weighted choices; do not infer a broken RNG from a few turns |
| Wrong delay | Effective policy override plus difficulty extra; opponent Ready may lawfully finish pending use early |
| Old action after exit | Captured generation/key, lease cleanup and operation cancellation |
| New node compile failure | Assembly, internal API, partial/metadata and unique type ID |
| Ordinary build disables Solo | Expected provisional gate until explicit final tuning approval |

Relevant tests include `SoloBotIntegrationScoringTests`, `SoloBotIntegrationControllerTests`, `SoloCompiledGraphTests`, configuration/authority/delay/selection tests and shared inventory/round tests. Consult their actual signatures instead of past tactical examples. Runtime controlled fixtures separately cover crisis, lethal/weighted alternatives, failure/debit, retry, compaction, expiry, fan cooling, death and normal re-entry. Natural samples separately cover explicitly selected difficulties and real results/return; shared regressions separately cover human tickets, grants, ghost seats, rounds and rematch.

Use the integration `run_full_match_playtest.py` with exact encounter/difficulty IDs and a matching build manifest for new natural cohorts. It records immutable attempts, original peer events/logs and real framebuffer media when requested. Reject incomplete/mismatched identity and duplicate selected attempts; preserve failures. Existing old-default helpers and the old sixty-game dataset do not become named-profile evidence. Human fairness, manual-input feel and prolonged soak remain distinct from automated correctness.

## Source map

- Controller: `Assets/Scripts/Core/Solo/BotTurnController.cs`
- Nodes/conditions: `SoloBehaviorNodes.cs`, `SoloBehaviorConditions.cs`
- Pure model: `BotDecisionModel.cs`
- Data/resolution: `Configuration/BotItemKnowledgeSO.cs`, `BotDifficultyProfileSO.cs`, `SoloSettingsResolver.cs`, `ResolvedSoloSettings.cs`
- Authority/delay: `TrustedBotCommandAdapter.cs`, `BotItemUseOperation.cs`
- Migration: `Assets/Editor/Plan046SoloBotBuilder.cs`; generic graph validation also uses `SoloCompiledGraphFixture.cs`
- Menu: `Assets/Scripts/UI/Lobby/SoloSelectionPresenter.cs`, `SoloSelectionView.cs`, `AZLobbyUI.cs`

Only the execution ledger states actual passes. Documentation examples are not new compiled/runtime test evidence. Preserve source assets, shared rules and failure records; never promote provisional values merely to clear a release warning.
