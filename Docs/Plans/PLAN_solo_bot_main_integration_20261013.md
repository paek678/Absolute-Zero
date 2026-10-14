# Solo bot difficulty integration into current main

Status: implementation authorized and in progress in the isolated `codex/solo-bot-integration-20261013` worktree. Sequential checkpoint results are recorded in [the execution ledger](../Validation/solo_bot_main_integration_20261013.md). Historical revision scores below describe planning, not implementation passes.

Revision 2 (2026-10-13): addresses the three findings from the initial 85/100 readiness review. Profile-specific evidence, effect-specific mitigation, and candidate-specific timing now have explicit owners, algorithms and acceptance cases. All implementation/runtime gates remain pending; the re-review in section 9 evaluates this plan, not a completed integration.

Revision 3 (2026-10-13): adds the implementation work packages, method/data contracts, shared-code allowlist, test placement, dependency order and handoff artifacts in section 10. The user confirmed that the incoming bot must adapt to the current game, with minimal shared-code changes. This remains a planning-only revision; section 9's score is the prior static assessment, not a new execution result.

## 1. Decision and pinned evidence

The user requested adopting the Solo bot branch's method while making it fit the current implementation. Select **the existing Unity Behavior BT + pure item utility model + trusted server command boundary**. Keep the current main architecture and port feature changes at their owning seams.

- Source: `feature/solo-bot-difficulty`, `e683ac128cb10982abbb5f9c59d37f3cc7ca3347` (2026-10-13 17:42 KST).
- Target inspected: `main`, `b74ec19c4f54cc184ed00f4ecb2266935b2d034d`, including the user's current uncommitted observation changes.
- Common ancestor: `0109dd897fc1b8eaee6bab74dab5dc8ce431a57c`. Each side has one exclusive commit; the source does not contain the target's recent combat/Multi integration.
- Source plan: [PLAN046 at the pinned commit](https://github.com/paek678/Absolute-Zero/blob/e683ac128cb10982abbb5f9c59d37f3cc7ca3347/Docs/Plans/PLAN_046_solo_bot_difficulty.md).
- Local contracts: [game design](../GAME_DESIGN.md), [design questions](../DESIGN_QUESTIONS.md), [architecture target](../AI_TARGET_ARCHITECTURE.md), [migration boundaries](PLAN_018_architecture_migration.md), [existing Solo graph plan](PLAN_036_solo_duel_bt_graph.md).
- Installed API evidence: Unity Behavior 1.0.16 and NGO 2.11.2. No new packages or API migration is required for this integration.

The user's selection adopts the source feature's difficulty-specific mini-game outcome roll, public held-item observation, two profiles, and Ready behavior. This supersedes the local Solo design paragraph that still prohibits random success rolls, for this integration only. Preserve ordinary human mini-games and shared combat rules. Do not claim a separate original specification attachment was independently reviewed; PLAN046 and code are the available source evidence.

### Pattern choice

| Option | Decision |
|---|---|
| Existing BT + pure scorer + current trusted command boundary | Selected: visible priority, testable scoring, one authoritative execution path |
| Replace with a separate FSM or second bot command system | Reject: duplicates existing phase/lifetime/authority ownership |
| Replace main files wholesale with branch versions | Reject: loses current inventory/telemetry/UI changes and breaks a current test-driver dependency |

## 2. Behavior to adopt

Observe public/own state -> build legal candidates -> score -> crisis recovery/defense -> advanced lethal priority -> weighted choice -> submit delayed request -> resolve outcome -> Ready. Keep bounded retries for stale/rejected requests and a no-item Ready fallback. A resolved mini-game failure consumes once and then readies, without another item attempt.

| Setting | Beginner | Advanced |
|---|---:|---:|
| Ordinary item delay | 4 seconds | 2 seconds |
| Mini-game item delay | 6 seconds | 4 seconds |
| Easy / normal / hard mini-game success | 75 / 60 / 45 percent | 90 / 80 / 70 percent |
| Best / second / uniform positive-score pick | 60 / 25 / 15 percent | 90 / 8 / 2 percent |
| Future-effect valuation | 0.5 | 0.8 |
| May miss lethal selection | Yes | No, after crisis priority |

Uniform choice includes best and second place; these are branch probabilities, not final marginal item frequencies. Preserve the source's expected-damage definition of lethal and clearly label it as an estimate, not guaranteed death. Preserve initial temperature, fan, inventory grants, temperature cap, damage/heal values, 1v1 match rules, and Multi rules from current main.

Keep source difficulty tuning provisional during integration. The current release guard must remain effective until the acceptance/release milestone below; do not silently expose provisional profiles in production.

## 3. Confirmed integration seams and corrections

| ID | Evidence / incompatibility | Planned treatment |
|---|---|---|
| SYNC-01 | Main `FullMatchPlaytestDriver.Select()` calls `BotDecisionModel.Estimate()` and reads Damage/Heal/Defense/Utility/Permanent; the source removes that API and candidate fields | First extract the unchanged old estimation formula into a development-only playtest evaluator and DTO. Switch only the driver call. Characterize output and RNG consumption before replacing the production model. Keep attacker/survival/resource/utility policies unchanged. |
| SYNC-02 | Source graph runtime guards at lines 418/444 are `ConditionalGuardModifier` with `m_Child` reference -2 (null). Installed `OnStart()` fails on null child | Build explicit guarded child subtrees: crisis guard -> crisis pick; lethal guard -> lethal pick; then weighted fallback. Verify compiled node types, condition identity, child links, priority and actual execution after save/reimport. Do not rely on constructor type: authoring validation can change Action to Modifier. |
| SYNC-03 | Windbreaker reduces matching direct damage by 2, while Duel blocks an entire matching debuff; source has one generic block factor | Implement the effect-specific valuation table in section 3.2 using current snapshots. Distinguish direct/equalization damage from whole-debuff rejection and current from future value. Preserve Mask, filters and Multi's different resolver policy. Change AI valuation only; update affected score assertions. |
| SYNC-04 | Source uses total slot count to reject Claw; current inventory can reuse empty slots at capacity | Observe `FindAvailableCopySlot() >= 0` as a read-only capacity fact. Preserve separate copies, finite-copy eligibility, new CopyIds, queue invalidation, grants and generation limits. Do not duplicate transfer mutation in the scorer. |
| SYNC-05 | Source deduplicates own candidates by item ID before comparing copy-specific recovery values | Evaluate legal copies first, select the best applicable copy per item with deterministic ties, then apply per-item choice odds. Preserve Smartphone's current-use heal progression and exact CopyId through compaction/revalidation. Do not add duplicate weighting to probability merely because more copies exist. |
| SYNC-06 | Main PlayerState has newer admission/epoch/transaction checks and selection, mini-game and Ready telemetry | Add the trusted bot-failure seam surgically. Extract only validated consume/compact behavior shared with human failure. Keep caller-specific guards, human rejection responses/finally cleanup and telemetry. Record bot outcome once without duplicating consumption observations. |
| SYNC-07 | Shared files changed on both sides: graph, LobbyScene, PlayerInventory, PlayerState, AZLobbyUI and GAME_DESIGN | Integrate hunks into current owners. Patch only Solo profile bindings and copy in the current lobby. Preserve nickname/customization/session UI and other recent changes. |
| SYNC-08 | Source also untracks `.mcp.json` and regenerates six `.meta` files | Exclude tool configuration changes. Audit metadata individually; retain valid target GUIDs. Repair only verified corruption, preferring original GUID recovery; validate references before any replacement. |
| SYNC-09 | Source success/scoring RNG is separate from gameplay RNG | Preserve deterministic per-session/Prep/request streams, one outcome per request and replay idempotence. Do not advance item-drop RNG or make failed polls reroll. |
| SYNC-10 | Current full-match driver starts the default menu selection and records no resolved Solo difficulty identity | Add the development-only profile selection and evidence contract in section 3.1. Test both IDs through the existing selection/start path. Missing, ambiguous or mismatched identities fail the new cohort instead of falling back to Beginner. |
| SYNC-11 | Source estimates own cooling from the shortest candidate delay and includes delayed damage in immediate threat | Use candidate-specific projection and bounded pre-submit revalidation in section 3.3. Preserve difficulty odds and command ownership; do not treat delayed effects as immediate lethal damage or cancel/reroll an accepted operation. |

Keep source tuning assumptions (future-effect horizon and opponent intent probabilities) explicit in data. They are estimates, not exact combat simulation. Read no opponent action queue, hidden choice, or private remaining-use counters. Corrections below change bot evaluation, not shared combat rules or difficulty probabilities.

### 3.1 Explicit profile selection and evidence (SYNC-10)

Current evidence: `FullMatchPlaytestDriver.cs:75-79` opens Solo and invokes Start without selecting a profile; its Summary and resolved_configuration omit Solo identity. `SoloSelectionPresenter` owns the filtered menu and selected encounter; `SoloSelectionView` creates buttons named Profile0, Profile1, etc. Button order and translated titles are not stable configuration identities.

1. Add optional runner argument `--solo-encounter-id`, forwarded as `--az-playtest-solo-encounter-id` only for a one-player development run. The new integration cohort requires an explicit value: `solo.encounter.beginner` or `solo.encounter.advanced`. Legacy runs without the argument retain their old default-selection behavior and cannot count toward V06.
2. Add a narrow development-only lookup through the current lobby/presenter using the same filtered profile list. Resolve one exact ConfigId to its menu option; exercise the existing view selection event and Start button. Do not bypass presenter inspection, the coordinator, provisional guards, or normal production graph launch. Reject missing/duplicate IDs, a disabled option, a non-Solo request, and a non-interactable Start before launch. Do not infer a successful selection from a label or index alone.
3. After launch, compare the requested encounter and its expected difficulty against `LaunchContext.Solo.EncounterId` and `DifficultyId`. Require the production graph/controller and reject validation fixtures. Record requested/resolved encounter ID, resolved difficulty/bot ID, ConfigurationVersions, effective delays/judgement values, graph identity from the build manifest, executable/assembly hashes, cohort/case/attempt IDs and the human-driver seed. Log the actual bot decision seed/Prep key and request/outcome identities; a human-driver seed is not a bot seed. Do not claim independent bot RNG samples from driver-seed changes alone.
4. Extend only the new cohort's analyzer contract: absent identity, requested/resolved mismatch, unexpected tuning/graph/build, diagnostic errors, incomplete result/lobby return, or duplicate selected attempts fail certification. Keep raw failed attempts. Additive versioned metadata must not change existing gameplay observations or relax any baseline gate. Update runner/analyzer tests alongside their schema; choose actual filenames by tracing the runner imports at implementation time.
5. Run the following separately identified acceptance cases. P01/P02 also require a complete natural match each, result, lobby return and profile switching on re-entry. This is minimum functional coverage, not statistical balance certification.

| Case | Required result |
|---|---|
| P01 Beginner | Requested `solo.encounter.beginner` resolves to that encounter and its configured difficulty; complete natural-match evidence has matching identities. |
| P02 Advanced | Requested `solo.encounter.advanced` resolves to that encounter and its configured difficulty; complete natural-match evidence has matching identities. |
| P03 Menu reorder | Reversing menu order still selects each requested ID through the normal selection path. |
| P04 Invalid selection | Unknown/duplicate/disabled IDs, non-Solo use of the argument and disabled Start fail explicitly without default-profile fallback. |
| P05 Invalid evidence | Injected requested/resolved mismatch, absent metadata, unexpected tuning/build/graph or duplicate selected attempts fail new-cohort certification. |

The runner and analyzer are potential frozen campaign dependencies. Change them only after the existing collector and owned players have finished and baseline hashes/artifacts have been preserved. The new cohort must never be added to the recovery runner's sixty cases.

### 3.2 Effect-specific mitigation and valuation (SYNC-03)

Authoritative references: `Core/Combat/TemperatureSystem.ApplyDamage`, `Core/Item/ItemEffectCalculations.Attack` and `DuelDebuff`, and `Core/Combat/CombatResolver`'s separate Multi path. Use immutable numeric snapshots and pure helpers; never call mutation/logging effect execution from the scorer.

Let d be nonnegative immediate damage, b the matching defense amount, p the existing difficulty's estimated defense-use probability (zero when no usable matching guard is publicly held), and s the bot item's success probability. A held defense is an option, not proof the opponent selected it. Match the filter or All; when multiple guards are available, use the strongest applicable prevention estimate without adding their amounts together. Preserve the source single-opponent intent heuristic rather than summing mutually exclusive attacks.

| Effect | Planned calculation / boundary |
|---|---|
| Direct attack | Conditional damage with guard = max(0, d-b); without guard = d. Expected damage = s * ((1-p)*d + p*max(0,d-b)). Lethal eligibility uses this immediate estimate only. |
| Equalization | Derive the sign and amount from projected own/public opponent temperature at use. A negative target delta is damage and uses the same mitigation formula; a positive target delta heals the opponent and is not positive attack utility. Preserve source policy that Equalize is not a forced-lethal candidate in this integration. |
| Duel delayed debuff | Any matching defense rejects the entire immediate-plus-scheduled outcome, even with finite b. Apply the defense probability to the complete net utility, not a subtraction of b from scheduled damage. Account separately for immediate opponent heal/capped value and discounted future loss. Never reuse Multi's finite-defense policy here. |
| Guard choice | Score prevented utility for the applicable effect and filter: capped direct/equalization prevention versus whole-debuff rejection. Keep immediate prevention and discounted future prevention separate; a guard against a future-only threat must not qualify as a current-crisis rescue. Account for the loss of any immediate beneficial part of a blocked debuff. Apply the existing opponent-attack probability and care multiplier once. |
| Recovery / delayed recovery | Cap immediate recovery against the candidate's projected temperature at use, including current-copy heal progression. Subtract immediate self-cost before crediting discounted future recovery; future recovery cannot rescue death before it becomes due. Preserve the actual effect order and temperature bounds. |
| No matching defense / Multi | No mitigation estimate for a wrong filter. Shared mutation and Multi resolver behavior stay unchanged; validate them separately in V07. |

Minimum exact cases E01-E06 (synthetic numeric snapshots, not production asset changes):

- E01: d=5, b=2, matching filter, p=0.3, s=1 -> expected damage 4.4; conditional prevented damage 2. At s=0.8 -> 3.52 before any lethal bonus.
- E02: wrong filter -> expected damage 5; full matching block with p=0.3 -> 3.5; certainty p=1 and full block -> 0. Exercise the real Mask snapshot as well as synthetic filters.
- E03: own/projected target temperatures 10/15 -> equalization damage 5 before defense; reversed temperatures -> opponent healing, no positive attack score; equal temperatures -> zero. Defense affects only the damage branch.
- E04: Duel debuff immediate +3 / scheduled -7 with matching finite defense -> neither immediate heal nor scheduled loss occurs. Without defense, keep +3 and next-turn -7 separate; with lookahead 0.5 and no cap interaction, undiscounted immediate cost plus discounted damage utility is 0.5 before p/s. Multi's corresponding finite-defense behavior remains its own regression assertion.
- E05: future-only damage cannot produce immediate lethal or an immediate-crisis defense rescue; test both matching and nonmatching guards. Compare net prevented utility, not scheduled damage alone.
- E06: projected own temperature 33 and current-copy heal 7 -> immediate heal utility 4 before success/care factors; delayed recovery cannot compensate a lethal immediate self-cost. Verify production Smartphone copies and temperature cap behavior.

### 3.3 Candidate timing, crisis and revalidation (SYNC-11)

Observe current own/public temperatures, fan-active flags, public fan values, Ready, remaining Prep time, and legal CopyIds. Follow the actual Solo/Duel tick path: `PrepTurnOperations.ApplyDuelTick` calls `ApplyFanTick` without the Multi-only modifier multiplier. Use its effective rate, not `ApplyMultiTick`'s rate. Do not read hidden opponent modifiers or action queues. Use one observation per scoring pass. Public known effects already due are distinct from a hypothetical opponent item that has not been selected.

- Project each candidate to its own use time. For the selected profiles (zero thinking and immediate Ready), effective delay is its resolved item delay, or zero when the opponent is already Ready. The existing reserve and strict expiry margin constrain affordability; reserve is not extra intentional cooling time. Nonzero thinking/near-deadline policies require their actual additional wait in projection and characterization before they can be supported by this integration.
- Cooling is tick-based in `TemperatureSystem` (one-second interval), not continuously applied. Keep estimated utility distinct from survival bounds. For survival screening without private tick phase, use a conservative ceiling of the candidate's wait divided by the tick interval, multiplied by the applicable own per-tick fan loss; zero when the fan is inactive. Label this bound in traces, check ticks immediately around request/Ready in fixtures, and never alter the real clock/tick order to fit the estimate. Re-read live temperature before submitting.
- Immediate threat uses current-use direct/equalization effects, not the full scheduled loss of a delayed debuff. Future effects contribute discounted utility separately. An already-active publicly known effect due within the horizon must be included at its real due phase; uncertainty about later effects is explicit, not a peek at hidden intent.
- Initial crisis can retain the shortest legal wait as a lower-bound screening step, but every scored candidate carries its own projected temperature, immediate threat, self-cost, and crisis flag. For both initial crisis and a crisis discovered after choosing a candidate, select an eligible Recovery/Defense response using the adopted difficulty policy. If none qualifies, terminate selection with no-item Ready; never fall through to lethal/weighted selection. Represent this result explicitly so a BT selector cannot mistake it for an ordinary failed guard. Allow only one crisis redirect per scoring pass; test both cases with positive/lethal attacks also available. Do not repeatedly draw weighted choices until a favorable result appears. These survival estimates do not promise a win or successful mini-game.
- Before BeginUse, refresh the legal window, copy/target identity, Ready and temperatures. If material changes invalidate the chosen projection or make it newly dangerous, return through the existing bounded retry route; each replan spends that same retry budget, then Ready fallback. No unbounded same-frame loop. Once BeginUse accepts a pending request, only the existing operation can shorten delay, validate, cancel for stale legality, and resolve. Do not restart it to improve the roll or switch items.
- Trace observed state, effective delay, projected cooling, immediate/future threat, crisis redirect, CopyId, Prep key, RNG seed and request ID. Preserve deterministic replay for identical observations; keep bot-choice draws separate from the single success roll and gameplay/item-drop RNG.

Minimum timing cases T01-T06:

| Case | Required result |
|---|---|
| T01 different candidate delays | Own temperature 8, active fan 1/tick, immediate threat 3, zero thought: 4s candidate projects margin 1; 6s candidate projects -1. Choosing the latter must trigger crisis handling before submit. Use controlled tick boundaries and verify the runtime observation independently. |
| T02 early Ready | Opponent Ready makes effective item wait/cooling projection zero; a later Ready while pending shortens only that existing request, with one outcome roll and at most one debit. Expired/stale/dead requests still reject. |
| T03 future-only harm | No immediate attack and scheduled-only loss must not fabricate immediate lethal/crisis; move an already-known due effect into the actual due phase and assert the changed result. |
| T04 live drift | Temperature decreases or CopyId disappears between scoring and submit: bounded replan or Ready fallback, no stale debit; repeated drift exhausts the budget. Pending requests never reroll. |
| T05 boundary timing | Test just before/at/after a fan tick and Prep deadline, and a sub-second remaining window. The scorer may conservatively reject; the command boundary must never accept an expired request. Fan inactive gives zero fan loss. |
| T06 lifetime | Round change cancels old pending work and clears round-specific cooling samples; new session clears all observations and graph instance state. No second-round reuse of an old key, history sample or outcome. |

## 4. Scope by owner

| Owner/files | Integration work |
|---|---|
| `Core/Solo/BotDecisionModel.cs` | Source scorer, immutable effect/defense/timing observations and candidate projections, with SYNC-03/04/05/11 adaptations and E/T cases |
| `Core/Solo/BotTurnController.cs` | Read-only snapshots, candidate-specific crisis redirect and bounded pre-submit revalidation, recent public cooling samples, early Ready, cleanup and structured trace |
| `Core/Solo/SoloBehaviorNodes.cs`, new `SoloBehaviorConditions.cs` | Named action/condition nodes delegating to controller; no authoritative writes |
| `Core/Solo/Configuration/` | Judgement settings, item knowledge, immutable resolved profiles, delay policy, range/reference validation; remove obsolete tactical tuning consumers together |
| `Core/Solo/TrustedBotCommandAdapter.cs`, `BotItemUseOperation.cs` | Single-roll delayed request, current binding/key/copy checks, success/failure resolution, cancellation and no duplicate debit |
| `Core/Player/PlayerState.cs`, `PlayerActionContracts.cs` | Trusted failure entry and explicit failure reason, preserving current human/network/telemetry behavior |
| `Core/Player/PlayerInventory.cs` | Reuse existing capacity query and mutation operations; avoid source-version replacement |
| `Core/Solo/FullMatchPlaytestDriver.cs`, proposed `PlaytestItemEvaluation.cs` | Development-only legacy evaluator extraction; explicit encounter selection, resolved-identity assertions and versioned evidence. Preserve all existing driver policies and RNG consumption. |
| `Data/Solo/` | Beginner/Advanced encounters and tuning, item knowledge and delay policy; migrate default graph references and baseline explicitly |
| `Editor/Plan046SoloBotBuilder.cs`, authoring/validation tools | Adapt builder into an explicit migration of existing assets, safe repeat validation; retire obsolete graph builder after all callers migrate |
| `Scenes/LobbyScene.unity`, `UI/Lobby/AZLobbyUI.cs`, `SoloSelectionPresenter.cs`, `SoloSelectionView.cs` | Bind two profiles and explain behavior; add a development-only ConfigId-to-option lookup using the same filtered list and normal view event/start path, without a second session launcher |
| `Tests/Editor/`, targeted runtime fixtures | Scoring, actual graph execution, commands, migration, driver compatibility and mode regression coverage |
| `.codex/scripts/run_full_match_playtest.py` and its imported analyzers/tests | After baseline collection finishes: explicit encounter argument, versioned identity validation and separate integration cohort manifest. Do not change the recovery campaign cases or pass gates. |

Paths in the table are under `Assets/` or `Assets/Scripts/` as appropriate, except the explicitly named `.codex/scripts/` row. Keep the current `SoloSessionCoordinator` exit/stop fixes, combat/VFX, ghost/deathmatch behavior, and legacy BotBrain unchanged. Do not import unrelated permanent-lock work or new starting-stat advantages.

Before the configuration slice, enumerate all old model/tactical consumers, including `Editor/SoloBotAuthoringWindow.cs`, `Editor/Plan036ExtensionValidation.cs`, `Editor/Plan036ConfigurationBuilder.cs`, `Tests/Editor/Plan036BotDecisionTests.cs` and `Plan036SoloConfigurationTests.cs`. Migrate fixtures and builders together. A development-only DTO must not leak into non-development references. New fields/APIs in this document are proposed seams, not claims that they already exist.

## 5. Authority and execution contract

`SoloSettingsResolver` -> immutable settings -> server `BotTurnController` -> pure score sheet -> BT selects copy/target -> existing trusted adapter -> existing PlayerState admission -> existing queue/consume path -> TurnManager/shared combat -> replicated presentation.

- TurnManager remains the only phase driver. The bot never executes combat effects directly.
- Delays/rolls belong to one live bot binding and Prep key. Validate at request start and before resolution.
- Early opponent Ready shortens only the delay. It cannot bypass expiry, death, invalid target, missing copy, current session, or phase checks.
- Success queues normal combat; mini-game failure consumes exactly once with no effect or selection. Stale/cancelled requests never consume.
- Preserve existing human ticket identity/deadline checks and online duel queue payload semantics.
- Despawn, scene exit and new session/round must cancel stale pending work, observers and cooling history at the correct lifetime.
- The decision feature is Solo-only. Shared helpers require separate Duel/Multi regression evidence; they do not introduce multiplayer bot participants.

## 6. Implementation order and acceptance gates

All phases below are pending. This document does not execute them.

1. **Capture target and protect existing work.** Recheck source tip and main, save the dirty-file inventory/diffs and hashes, pin the chosen source commit and build/tool identities. Work on a `codex/` integration branch based on the current target with the existing local changes preserved and identifiable. Avoid whole-commit cherry-pick/scene replacement.
2. **Decouple the automated player's old estimator (SYNC-01).** Preserve formula, candidate ordering, ties, policies, media-only bias and random draw count. Establish characterization tests; compile Editor and development symbols. This prevents an otherwise hidden source-branch/main API break.
3. **Port configuration, pure scoring and narrow command seams as one compiling slice.** Update all removed-field consumers, fixture setup and tests together. Apply sections 3.2/3.3 and the copy/capacity adaptations. Establish E01-E06/T01-T06 expected outcomes from current effect/timing owners, then validate forced success/failure, stale/duplicate requests and telemetry accounting before enabling the new graph. Stop on a design/current-code mismatch rather than changing shared rules to satisfy a score assertion.
4. **Migrate graph and wire profiles through a verified Editor.** Keep existing asset GUIDs, migrate runtime subasset references deliberately, preserve the current lobby and custom appearance. The source builder refuses existing GraphPath: implement an explicit migration path instead of deleting the asset to evade that guard. Save/reimport and inspect the compiled graph, not just authoring nodes or node count.
5. **Wire profile-specific evidence, then run development validation.** Only after the frozen collector finishes, extend runner/driver/analyzer per section 3.1. Pass P03-P05 negative/identity tests before certifying P01/P02 natural matches. Exercise actual crisis and lethal branches, failures, early Ready, expiry, second round, lobby return and repeated entry. Run shared Duel/Multi regressions and revalidate old driver policy behavior. Store new results separately with exact source/build/profile IDs; a completed match of the wrong difficulty is a failed validation case.
6. **Release and handoff.** Review play feel and all outstanding gates. Promote difficulty/item-use tuning deliberately only when accepted, and verify both development and non-development launch paths. Update the Solo sections of shared design/authoring docs and validation ledger without overwriting current unrelated work. Deliver a reviewable scoped diff and evidence before any requested merge/release.

### Protect the running sixty-match collection

The recovery campaign in `output/balance60-20261013-recovery/` is an existing frozen baseline. Do not treat a count in a planning document as its live status. Its executable, observation scripts, seeds, cases, pass gates and raw attempts remain untouched. See [recovery contract](../Validation/balance60_recovery_20261013.md).

Do not rebuild over `AZVisual4P_Current/Player`, edit fingerprinted scripts, or run another `AbsoluteZeroVisual4P` process during collection. The default integration schedule performs runtime validation/rebuilding after the collector and owned players finish. Source edits, if later requested sooner, must remain isolated from the active Editor/build and cannot update that campaign's fingerprints. Do not stop or restart the collector merely to apply this feature.

New bot evidence belongs to a separate cohort; do not merge its matches into the old sixty or use the old passes as proof of new behavior. Before reusing the approved executable path after completion, preserve the frozen baseline binary/evidence identity and record the new build hash.

## 7. Verification matrix

| Gate | Required evidence |
|---|---|
| V01 Compilation and data | Current Editor + development + non-development compilation; no removed production Estimate/tactical API consumers or release references to test-only DTOs/seams; valid .meta/asset references, resolver range/knowledge coverage, no unintended package/tool changes |
| V02 Existing driver compatibility | Same old inputs produce same Damage/Heal/Defense/Utility/Permanent, selected copy/target and RNG call count for each existing policy; media-only behavior remains scoped |
| V03 Pure new scoring | E01-E06 and T01-T06 model assertions; difficulty odds including uniform component, deterministic ties, effect-specific defense, no over-cap heal, eligible empty-slot steal and duplicate Smartphone copies. Drive RNG boundary values for branch odds; do not demand empirical percentages from a few matches. |
| V04 Compiled graph priority | Load the production asset after reimport, assert connected guards, tick real graph; crisis takes recovery/defense before lethal; safe advanced lethal takes lethal; beginner can choose otherwise; no candidate reaches Ready fallback; retry is bounded |
| V05 Request lifecycle | Runtime T02/T04/T05/T06 and bounded crisis redirect; roll once; success/failure; exact one-use failure debit; duplicate poll/replay; compaction; input expiry; death/target loss; session exit and second-round reset. A stale request must never start a new roll or debit; distinguish lawful advancement of an already-valid pending operation during a poll from mutation caused by the stale request itself. |
| V06 Natural Solo and attribution | P01-P05; at least one complete natural match with each explicitly requested/resolved difficulty, plus forced rare-branch fixtures, result/lobby return/re-entry and profile switching. Trace score/branch/copy/outcome and enforce no runtime errors. Natural lethal capture stays pending if not observed; forced fixtures are separate evidence. |
| V07 Shared mode regression | Host+remote Duel and Host+three remotes Multi: selection, human mini-game pass/fail, copy grants/reroll/steal, deathmatch/ghost and round reuse, results/return; preserve all current telemetry and strict gates |
| V08 Release guard | Provisional data rejects ordinary release; accepted profiles resolve/start after deliberate promotion; invalid profiles stay rejected; no blanket guard bypass |
| V09 Evidence isolation | Old campaign originals/fingerprints untouched, new cohort uniquely labeled; no combining old/new difficulty results; document exact checks actually run |

Scorer-only tests do not satisfy V04. Branch documentation reports 1061 total/164 focused passes, but these are historical source claims, not verification of the integrated build. Human play feel remains distinct from automated completion.

## 8. Requirement trace and handoff

| Requirement/reference | Work/gate | Evidence state at planning |
|---|---|---|
| User-selected source difficulty behavior / PLAN046 | Phases 3-4, V03-06 | Source code/data inspected; target integration pending |
| GAME_DESIGN Solo and PLAN036 host/BT boundaries | Phase 3, V05-07 | Current admission/queue paths inspected; runtime pending |
| Current main inventory + observations | SYNC-01/04/05/06, V02/05/07 | Concrete target call sites and uncommitted diffs inspected |
| Source graph priority intent | SYNC-02, V04 | Null children verified in serialized runtime and installed package source; correction pending |
| Review finding R1: difficulty attribution | SYNC-10, P01-P05, V06 | Current default-selection/metadata gap confirmed; explicit ID selection and fail-closed evidence planned |
| Review finding R2: effect-specific mitigation | SYNC-03, E01-E06, V03/V07 | Duel damage/debuff and Multi differences confirmed; pure valuation and regression assertions planned |
| Review finding R3: candidate-specific timing | SYNC-11, T01-T06, V03/V05 | Source shortest-delay estimate and current tick behavior confirmed; bounded correction planned |
| Existing sixty-match recovery contract | Isolation rule, V09 | Frozen-baseline contract inspected; no build/tool/campaign mutation in this task. Read live state before any future build/tool change. |

Create one integration result ledger under `Docs/Validation/` when implementation begins, using V01-V09 as stable pending checks with report/capture/build references. Keep this plan linked from ACTIVE_CONTEXT. Record any later source or target revision change before proceeding.

Rollback is the inverse of the integration's scoped changes against the recorded target snapshot. Do not reset the whole dirty worktree or restore branch versions over pre-existing edits. Keep baseline assets/build evidence sufficient to recover previous Solo behavior while preserving current Multi and telemetry work.

## 9. Revision 2 static re-review

Review scope: the plan against current main `b74ec19c4f54cc184ed00f4ecb2266935b2d034d`, preserved local changes, and source `e683ac128cb10982abbb5f9c59d37f3cc7ca3347`. Previous plan SHA-256: `A7E02496A269507C203E8D28D24052C660CFEE52370871D6EACC6003A45175CB`. Source, command, UI and effect reads are static evidence; no new Unity compile/build/gameplay run was performed for this document revision.

| Prior finding | Revision treatment | Closure meaning |
|---|---|---|
| R1 default-profile-only tests | Stable encounter selection through the current UI, resolved settings checks, versioned metadata, analyzer rejection cases, explicit runner ownership and baseline freeze | Closed in plan; P01-P05 implementation/execution pending |
| R2 generic partial-defense correction | Separate direct/equalization/whole-debuff rules with formulas, numeric cases, wrong filters and Multi regression boundary | Closed in plan; E01-E06 implementation/execution pending |
| R3 shortest-delay danger estimate | Per-candidate projection, conservative tick-aware survival estimate, one crisis redirect, bounded live-state revalidation and immutable accepted operation | Closed in plan; T01-T06 implementation/execution pending |

Additional consistency corrections: remove stale campaign count; include presenter/view and runner/analyzer in owner scope; compile release symbols for test-only seam isolation; distinguish polling an existing request from effects caused by a stale request; explicitly preserve source Equalize lethal policy and difficulty probabilities.

Document verification: resolve all six local Markdown links, check all 37 seam/gate/case IDs and sections 1-9, check whitespace and numeric E01/E04/T01 examples, and compare the pre/post tracked gameplay diff plus Python script hash inventory. The first check found P04 described only inside a grouped sentence; it was expanded into the explicit P01-P05 table before the final rerun. These are document/static checks, not game test executions.

Readiness rubric (judgment, not an empirical success probability): architecture/authority 20/20, compatibility/migration 24/25, behavior/timing specification 23/25, verification/attribution 18/20, baseline preservation 10/10 = **95/100**. The three previous planning gaps are addressed. Remaining uncertainty concerns actual Unity graph migration/compilation, tick/order behavior under runtime conditions and natural two-profile/Duel/Multi evidence; it is not converted into a claimed test pass. No additional blocking contradiction was found within this static scope.

Disposition: ready to begin the scoped implementation after the frozen-campaign scheduling conditions are satisfied; not ready for merge/release certification. V01-V09 remain pending until reports from the new integrated build close them. Human play-feel acceptance and deliberate tuning promotion remain separate release decisions.

## 10. Detailed implementation specification (revision 3)

This section decomposes section 6; it does not replace the behavior definitions or acceptance cases in sections 3 and 7. Every work package below is **pending**. Method/type names explicitly marked proposed are implementation targets; existing methods were checked against the pinned target/source. Do not treat proposed symbols as current APIs.

Execution authorized on 2026-10-13: minimum checkpoint score 95/100, every required check passed, and no blocking correctness finding before advancing. Allocate 40 points to requirement completion, 35 to verification evidence, and 25 to preservation/authority. Missing required evidence blocks advancement regardless of total. The execution ledger owns actual status. I02-I04 are one atomic checkpoint. Isolated source compilation/EditMode tests may run in the integration worktree; gameplay players, existing test-tool changes and rebuilding the approved executable wait for the frozen campaign.

### 10.1 Change boundary and shared-code allowlist

The current game is authoritative. If bot estimates disagree with it, correct the bot snapshot/scorer/controller. Do not change the game to make an imported bot test pass.

| Area | Allowed implementation delta | Preservation requirement |
|---|---|---|
| `Assets/Scripts/Core/Solo/` | Configuration, scorer, BT orchestration, trusted delayed bot request and development evidence | Keep authority/Prep-key boundaries; no direct combat execution or alternate turn driver |
| `Assets/Scripts/Core/Player/PlayerState.cs` | Add the trusted bot failure entry; factor the existing consume/compact statements into one small helper if needed | Existing human RPC checks, tickets, target resolution, telemetry verdicts, rejection handling and finally cleanup retain their behavior and ordering |
| `Assets/Scripts/Core/Player/PlayerActionContracts.cs` | Add the source `MiniGameFailed` reason without changing existing enum numeric identities | Append or assign explicitly if necessary; never renumber existing serialized/telemetry values |
| `Assets/Scripts/Core/Player/PlayerInventory.cs` | Reuse `FindAvailableCopySlot`, CopyId lookup and existing mutation methods | Default: no edit. Do not port the source's whole inventory file or overwrite current observation changes |
| `Assets/Scripts/UI/Lobby/` and `Assets/Scenes/LobbyScene.unity` | Solo profile bindings/copy and development-only selection bridge | Preserve nickname, customization, online lobby, cancellation and scene ownership |
| Tests, Solo authoring tools, Solo assets, scoped documentation and post-campaign test tools | Migration, fixtures, identity evidence and validation | Preserve GUIDs and existing unrelated fixtures; new tools cannot weaken old pass criteria |
| `Core/Combat/`, `Core/Turn/`, item effect/data classes, item assets, `SeatInventoryMutator`, `ItemDropTable`, shared rules, session/transport and Multi gameplay | Read and characterize only for this integration | No new changes to damage/heal, fan ticks, drops, grants, transfer rules, win conditions, RPC authority, queue semantics or session exit fixes |

Preserve all pre-existing dirty changes even inside read-only areas; the rule concerns this integration's delta. If a genuine shared-game defect is discovered, record it separately with reproduction and a proposed scope decision. Do not silently fold a gameplay fix into this port. Existing pure helpers may be called without rewriting their behavior; keep any additional projection helper inside the Solo namespace.

Runtime validation amendment, 2026-10-14: the original Editor legitimately occupied18777. The bounded exception is an explicit **development-only** initial local Solo port in `AppBootstrapper`, preserving its default and excluding the override from ordinary compilation. No phase, authority, shared-rule or online transport behavior changes. The stale development ghost fixture's old4-kill seed is separately adapted to actual `KillsToWin-1`; production victory rules remain unchanged. `PLAN036_RELEASE_GUARD` additionally tests ordinary rejection and unsaved approved-copy launch/replay via the existing resolver/coordinator and its existing constructor port, without enabling production tuning or adding a global bypass. See the execution ledger for failures and actual validation.

### 10.2 Bot data and method contracts

Prefer extending the source's existing small structs/classes rather than adding a new service layer. Keep scoring data immutable and free of PlayerState, network collections, scene lookups and runtime-mutable SO state.

| Contract | Fields / operation to implement | Owner and invariant |
|---|---|---|
| Item profile snapshot | Extend source `BotItemProfile` with actual defense amount/filter, immediate target/self delta, scheduled amount/delay and effect kind sufficient for section 3.2 | `BotDecisionModel.Profile` reads the current `ItemEffectRuleSnapshot`; copy needed numeric arrays. Catalog/knowledge mapping stays in resolver. |
| Turn observation | Extend `BotObservation` with own/public fan-active state, `CanReceiveStolenCopy`, observed public due effects where exposed by the current game, and timing inputs | `BotTurnController.CaptureObservation`; `CanReceiveStolenCopy` comes from `FindAvailableCopySlot() >= 0`. Do not invent visibility of pending opponent actions. |
| Candidate | Retain exact CopyId, target seat, catalog ID, delay and current-copy heal; add projection to scored result rather than mutating the candidate | `BuildCandidates` uses `ServerValidateBotItem` and `TryGetItemDelay`; identity remains CopyId rather than slot index. |
| Candidate projection (proposed value struct) | Effective wait, own projected temperature, cooling survival bound, immediate threat, future utility, affordability and crisis flag | Proposed pure `ProjectCandidate` in `BotDecisionModel.cs`; use actual Solo/Duel tick behavior and sections 3.2/3.3. No extra gameplay RNG draws. |
| Scored sheet | Keep score/bonus/category and add separate immediate/future values plus the projection; compare legal copies before choosing one representative per item | `Score`; deterministic ties use existing knowledge order, then item ID, then CopyId for copies of the same item. Do not apply odds once per duplicate. |
| Selection | Existing source `TryPickCrisis`, `TryPickLethal`, `TryPickWeighted`; one selected-candidate crisis redirect per scoring pass | `BotTurnController` owns choice RNG and the selected copy. No repeated random draws until an acceptable item appears. |
| Submission | Refresh legality/current public state and compare the chosen projection before `BeginUse`; invalidate through the existing retry path | `Think`/`Submit`; an accepted operation is never replaced for scoring reasons. Adapter remains the only success-roll owner. |
| Development identity evidence | Requested/resolved encounter, difficulty/bot IDs, configuration versions, effective settings, graph/build identity and actual decision seed/request identity | Driver and trace; proposed schema extension must be additive for legacy runs, mandatory only for the new integration cohort. |
| Development selection bridge (proposed) | `ISoloPlaytestSelection.TrySelectEncounter(string encounterId, out string expectedDifficultyId, out string error)` | Define in proposed `Core/Solo/PlaytestSoloSelection.cs` under development/Editor symbols; implement in AZLobbyUI under the same symbols. Selects via existing view events, never starts a session or writes game state. Core driver discovers the interface on scene components; it must not reference the UI assembly/class. |

If the current public observation surface cannot expose a due effect, document that limit in the evidence and model it as unknown. Do not create a hidden-information read or change replicated gameplay state to satisfy a projection test. Test due-effect projection with explicit public snapshots and separately verify whether the live observation can supply them.

### 10.3 Ordered work packages and completion criteria

Execution order: **I00 -> I01 -> (I02 + I03 + I04, one compatible source slice) -> I05 -> I06 -> I07 -> I08**. The grouped source slice prevents temporarily removed APIs/node types from becoming an accepted intermediate build. Individual edits can be staged for review, but do not run a match until the whole slice compiles and asset migration is validated. These are logical review checkpoints, not authorization to commit, merge or release automatically. Verified assembly direction is UI -> Core; preserve it and add no Core -> UI reference.

#### I00 — Pin baseline and establish the allowed delta

- Inputs: the source/target hashes in section 1, current dirty worktree, baseline recovery manifest and live process state.
- Save a manifest of branch/HEAD, dirty paths and hashes, source patch, installed Unity/package versions, existing Solo asset GUID/runtime references, and baseline executable/assembly/tool hashes. Preserve untracked work separately from the integration changes.
- Begin a `codex/` integration branch from the actual current target only when implementation starts. Recheck any target/source drift before using this plan. Do not reset the worktree or cherry-pick the entire source commit.
- Create proposed ledger `Docs/Validation/solo_bot_main_integration_20261013.md` with I00-I08 and V01-V09 initially pending and links to evidence. Store new run artifacts under `output/solo-bot-integration-20261013/<build-id>/`; never under the recovery campaign.
- Exit: the preserved baseline and diff boundary can be reconstructed; no collector/player conflict and no forbidden build/tool mutation. While the frozen campaign is active, source work requires the already-described isolation; normal implementation/runtime scheduling waits for its completion.

#### I01 — Decouple the existing automated human evaluator

- Add proposed `Assets/Scripts/Core/Solo/PlaytestItemEvaluation.cs`, entirely under `UNITY_EDITOR || DEVELOPMENT_BUILD`, containing a separately named DTO and the unchanged old `Estimate` formula.
- Update only `FullMatchPlaytestDriver.Select()` to use it. Preserve candidate iteration, policy branches, tie behavior, media bias and `_random.NextDouble()` count/order. Do not change production bot behavior in this package.
- Add proposed `Assets/Tests/Editor/PlaytestItemEvaluationTests.cs`: baseline comparison across enabled item snapshots, boundary temperatures, every recovery use index, each driver policy, ties and media-only cases. Use captured baseline expectations/selection traces, not the new bot scorer as the oracle. Include CopyId/target and random-stream position.
- Exit: V02 characterization passes; Editor/development compile and non-development exclusion are verified. Existing driver outputs remain behaviorally identical before replacing the production model.

#### I02 — Port configuration and adapt the pure scorer

- Port source `BotJudgementSettings`, `BotItemKnowledgeSO`, `BotDifficultyProfileSO`, `ResolvedSoloSettings` and related configuration changes into current owners. `SoloSettingsResolver.Resolve` keeps identity/range/catalog/provisional checks and snapshots effective settings.
- Implement the contracts in section 10.2 inside `BotDecisionModel.Profile`, `Score` and its pure selection/projection helpers. Read defense/filter/timing from current assets. Do not modify item assets or import source effect implementations.
- Remove obsolete tactical field/API consumers together in `SoloBotAuthoringWindow`, `Plan036ExtensionValidation`, `Plan036ConfigurationBuilder` and affected Plan036 tests. Keep legacy evaluator symbols limited to I01. Add source `Plan046BotJudgementTests.cs`, updating its old full-block expectations to section 3.2.
- Add proposed `Assets/Tests/Editor/SoloBotIntegrationScoringTests.cs` for E01-E06, T01/T03/T05 model cases, reused empty slots, duplicate Smartphone copies and deterministic copy ties. Characterize empty/unknown/disabled candidates and malformed settings without silently choosing an unknown item.
- Exit with I03/I04: compiling source slice; V01 configuration checks and V03 pure checks pass; no game effect/rule delta.

#### I03 — Add the trusted bot failure operation with minimal shared changes

- Port `IBotItemUseBoundary.RollSuccess`, `OthersReady` and `FailMiniGame`; wire them in `TrustedBotCommandAdapter`. Preserve current `IsCurrent`, `TryWindow`, authorized queue/Ready and disposal paths.
- In `BotItemUseOperation.Begin`, store one success outcome after valid request admission. Preserve request history/high-water and duplicate replay semantics. In `Advance`, validate clock/key/window/copy/target again before success queue or failure consumption. `OthersReady` may remove only the wait. Keep all cancellation reasons and no-debit behavior for genuinely invalid requests.
- Add source-shaped `PlayerState.ServerFailBotMiniGameItem(key, candidate, authorization)`: require the current trusted adapter, bot admission, matching binding/Prep key, valid CopyId/target and a mini-game item. Resolve the current copy/slot before mutation; reject stale identity.
- Proposed private helper `ConsumeFailedMiniGameItem(byte slotIndex)` contains only the existing failure log/consume/compact behavior. The human `SubmitMiniGameResultServerRpc` keeps every preceding guard, its `MiniGameFailureConsumed` verdict and finally cleanup. Bot outcome telemetry is emitted once; the existing inventory observer continues to own the consumption record. Do not duplicate it with a second observer.
- Extend `Plan036BotDelayTests` and `Plan036ActionAuthorityTests`: deterministic forced pass/fail via test boundary, one debit, replay/compaction, wrong adapter, stale key, changed target, deadline/death, early Ready and repeated failure polling. Force outcomes in fixtures without altering production success settings.
- Exit with I02/I04: all focused command checks pass; review diff confirms no human admission/RPC/queue semantics were changed. Shared helper changes retain mandatory V07 runtime debt.

#### I04 — Wire observation, selection and bounded lifecycle behavior

- Adapt `BotTurnController.CaptureObservation`, `BuildCandidates`, source `ScoreCandidates` and `PickCrisis`/`PickLethal`/`PickWeighted`. In `Picked` (or one proposed helper called by it), apply the single candidate-crisis redirect; in `Think`/`Submit`, revalidate current state as described in section 3.3.
- Spend the same configured retry budget for stale projection and ordinary rejected requests. Verify where the source retry decorator owns counting; do not add a second independent retry budget. After exhaustion use `Fallback`, which must not submit Ready over an active pending request.
- `AwaitUse` treats confirmed mini-game failure as a terminal consumed outcome and proceeds to Ready. It must not use generic cancellation as proof of consumed failure.
- In `OnPrep`, distinguish a new turn from a changed round/session using existing key fields. Clear round-specific cooling history on round change, preserve only intended within-round samples, and cancel prior work. `StopRun`, `Detach`, `OnDisable` and `OnDestroy` remain idempotent and remove observers/graph instances.
- Port source `SoloBehaviorNodes` and `SoloBehaviorConditions`, updating obsolete node consumers as part of the compiling slice. Node actions delegate to the controller; they do not mutate combat state.
- Exit: I02-I04 compile together, pure and command checks pass, deterministic controller fixtures cover T02/T04/T06 and bounded fallback. Production asset execution remains blocked until I05 validates the migrated graph.

The Plan046SoloBotBuilder source and switched callers must already compile in I02-I04. I05 executes migration; it does not defer defining the builder until after callers need it.

#### I05 — Migrate the existing graph and Solo assets in the Editor

- Adapt source `Assets/Editor/Plan046SoloBotBuilder.cs`: keep read-only `Validate`; add explicit proposed `MigrateExisting` for this project's existing GraphPath, distinct from clean creation. Never delete the asset to bypass `Build()`'s existing-path rejection.
- Use a verified Editor connection. Before writing, resolve all graph references (DefaultBot, baseline/encounters and fixtures), preserve GUIDs and record runtime subasset fileIDs. Modify authoring objects through installed Behavior APIs; if compilation replaces a runtime subasset, update affected references deliberately and verify them after reload.
- Build this logical topology: bounded retry around the decision sequence; Observe -> Candidates -> Score -> priority selector (crisis guard with crisis-pick child, lethal guard with lethal-pick child, weighted fallback) -> Think -> Submit -> AwaitUse -> ChooseReady -> WaitReady -> Ready; outer no-item fallback after exhausted failure. Confirm the exact source retry/fallback wiring before preserving it. Every modifier guard must own a real child; a sibling action is insufficient.
- Update the builder's structure assertions for the migrated topology, not the historical constant 22 alone. Validate runtime guard types, conditions, child links, selector order, single-shot start and bounded retries after save/reimport. Tick the compiled asset for V04 scenarios.
- Create/update only Solo knowledge, policies, Beginner/Advanced encounters/difficulties and required baseline links; preserve valid existing GUIDs. Keep provisional tuning and current shared rule/start-stat/grant references. Audit the six source metadata repairs individually. Exclude `.mcp.json` changes.
- Repeat migration/validation once: no duplicate asset, no duplicated lobby profile and no reference drift. Exit: V01 asset checks and V04 actual compiled graph checks pass. No production launch uses a partially migrated graph.

#### I06 — Add stable menu selection and certified profile evidence

- `AZLobbyUI` owns the bridge to its existing presenter/view. Add proposed development-only ID lookup in `SoloSelectionPresenter` using its filtered `_profiles`; add proposed view helper that invokes the same selected-option button event. Keep `Select`/`Inspect`/`StartAsync` and normal Start button behavior authoritative. Do not invoke a second coordinator/session path.
- Implement the section 10.2 development interface so `FullMatchPlaytestDriver` in Core does not import `AbsoluteZero.UI`. Require exactly one active selection bridge; missing/ambiguous bridges fail the explicit new-profile request. Return the expected difficulty ID from the uniquely resolved profile, then compare it with the post-launch resolved configuration. Compile the interface, implementation, lookup and call sites under matching symbols; ordinary release builds retain the existing UI dependency graph.
- Update `FullMatchPlaytestDriver`'s Solo startup, Summary and resolved configuration recording per section 3.1. Compare launch identity before enabling `_driving`; mismatch produces a failed summary and normal owned-session cleanup.
- Current Python runner owns both execution and analysis: extend `.codex/scripts/run_full_match_playtest.py` argument parsing/`run(args)` and add an explicit identity validator beside `inspect_peer`. Preserve the existing `inspect_peer(folder)` contract for old callers; pass expected new-cohort identity separately so an old summary cannot certify the new cohort. Keep `audit_events` and `compare_terminals` gates intact.
- Extend `.codex/scripts/test_full_match_playtest.py` for forwarding, missing/duplicate/mismatched identity, menu-independent expectations, malformed metadata and unchanged legacy cases. Record effective settings and build/graph manifest identity; never infer them from folder names.
- Exit: P03-P05 pass before natural matches, development-only seams are absent from release compilation, and the existing recovery scripts/cases remain unchanged. All runner edits occur after the baseline collection restriction is cleared.

#### I07 — Run the integrated build's ordered verification

- First execute focused Editor model/configuration/command tests, then compiled-graph/lifecycle fixtures, then relevant full Editor regression tests once. Resolve new failures before moving to natural-match evidence. Re-run only affected checks after a change, broadening when shared code or assets changed.
- Keep `Assets/Tests/Plan036Behavior/Runtime/Plan036BehaviorProbe.cs` for package graph lifecycle coverage. For integrated controller/command fixtures, extend the existing development-only partial `Plan036ActionProbe` with proposed `Assets/Tests/Plan036Actions/Runtime/Plan036ActionProbe.SoloIntegration.cs`; its ActionFixture asmdef already references Core/Behavior/NGO. Use its existing legal access pattern, not new public production mutation hooks. BehaviorFixture has no Core reference; add no Core imports there or assembly cycles. The EditorTests assembly already references Core/UI/Behavior for scoring and UI tests. Keep production assets separate from fixture assets.
- Build to the approved executable path only after preserving the frozen baseline build and verifying no owned players remain. Record the new executable/assembly hashes and manifest. No new firewall exceptions or package upgrades are part of this work.
- Run P01 and P02 sequentially with explicit encounter IDs and distinct immutable attempt folders; test switching profiles on re-entry. Then execute host+remote Duel and host+three-remotes Multi regressions in V07. Preserve failed attempts and state the observed reason; do not borrow the recovery campaign's retry policy or rerun a gameplay failure until it disappears.
- Required evidence: new build identity, exact command/case manifest, raw peer events/logs, summaries, requested/resolved settings, bot branch/request traces, graph fixture report, terminal/return evidence and process exit status. Screenshots/visual checks support presentation claims; telemetry completion alone does not certify appearance or play feel.
- Exit: each V01-V07/V09 requirement has passed evidence or remains explicitly pending/failed. A natural lethal branch not observed stays separately unobserved even when its forced fixture passes. Do not report all gates passed if any required subcase lacks evidence.

#### I08 — Scope review, release guard and handoff

- Compare the final integration delta with I00, ignoring no pre-existing dirty work. Verify the section 10.1 allowlist and rule/item asset hashes. Check non-development compilation, rejection of provisional/invalid settings and the absence of test-only APIs in ordinary player launch.
- Keep tuning provisional until the user accepts the integrated behavior. Test the promotion path with controlled test assets/configurations; do not permanently promote production data merely to make V08 green. Actual release certification requires the explicit promotion and its launch evidence.
- Update only the affected Solo design/authoring sections and the single integration result ledger; link it from ACTIVE_CONTEXT. Include completed gates, unresolved runtime/visual decisions, exact source/target/build identities, changed paths and scoped rollback instructions.
- Exit: reviewable integration diff and truthful evidence handoff. Merge, deployment and shared-rule changes are not implied by completing this plan.

### 10.4 Checkpoint acceptance and stop conditions

| Checkpoint | Must be true before proceeding |
|---|---|
| Baseline -> source work | Source/target pinned, dirty work preserved, campaign isolation satisfied |
| Legacy extraction -> bot replacement | Old evaluator and driver selection/RNG behavior characterized; no release dependency on test DTO |
| Source slice -> asset migration | I02-I04 compile together; no removed API/node consumers; pure and command tests pass |
| Asset migration -> natural matches | Stable references after reload/repeat migration; actual BT priority works; P03-P05 reject invalid profile evidence |
| Natural matches -> handoff | Correct profile identities, strict summaries/terminal/return checks, owned process exits and shared-mode evidence recorded |
| Handoff -> release | All required gates closed on the integrated build, play-feel decision and deliberate tuning promotion complete |

Stop dependent work on compilation errors, placeholder/missing graph children, profile mismatch, wrong controller admission, repeated consumption, shared-rule drift or lost baseline identity. Preserve evidence and fix the bot/adapter or migration within the allowlist; never weaken a gate, alter combat values, suppress diagnostics or overwrite a failed attempt to obtain a pass.

Revision 3 verification scope: current method/assembly ownership and source graph topology were inspected; document identifiers/links, dependency sequence and unchanged gameplay/tool diff were checked. No implementation package has been executed, and no additional Unity test pass is claimed by this detail revision.

## Current continuation after chat transfer

Current-build activation, 2026-10-14: the user subsequently approved the current Beginner/Advanced settings. Only three approval flags changed, with unchanged numeric values and GUIDs. Actual ordinary menu/default-port source launches, graph/Ready, replay/normal stop and invalid rejection passed; final standard build16e10647033a44998870fe9001b9f7c6 succeeded errors0/warnings5, with test APIs excluded and normal startup/close0. This closes current-build source activation, superseding the provisional-status sentence below for these two choices. Build the isolated integration worktree; main merge/push/deployment and human final balance acceptance remain separate. Exact source/runtime scope and preserved failures are in the single ledger.

Latest execution checkpoint, 2026-10-14: I00-I08 completed their authorized technical review gates (I05 96, I06 97, I07 97, I08 96). Four natural integrated matches and seventeen controlled shared regressions passed; an actual standard Windows build succeeded with errors0/warnings5, diagnostic APIs excluded and normal startup/close verified. The ledger below owns build identities, failure provenance and scoped rollback. Source tuning remains provisional: ordinary shipping Solo launch still requires explicit acceptance/promotion plus actual accepted-source launch/build evidence. No main merge/push/deployment or final tuning acceptance is implied. User cancelled further video organization; retain all existing recordings and raw failures.

Ownership moved to chat `01a0a2ba-0beb-7191-b41f-ca3ec883a24f`. I00-I04 and the existing migration/Editor evidence remain preserved. The I05 live fixture is now implemented under the existing ActionFixture assembly and is scripts-compiled; its ten runtime cases are listed in the execution ledger. Compilation does not close I05. Keep the old sixty-match runtime/tool fingerprints unchanged; after verified collection/process completion, execute the I05 cases before proceeding to I06. Normal deployment still excludes the fixture, and tuning remains provisional.

The current continuation expands I05 to fifteen controlled runtime cases, adding active-fan delay/survival and pending actor death, target death, session exit/re-entry. The execution ledger owns their actual status. A one-shot guarded continuation waits for the protected campaign, preserves its Player, then builds/runs the I05 matrix; the existing heartbeat must inspect that owner before launching anything. Runtime results and checkpoint review, not preparation or compilation, permit advancement to I06-I08.

### I05 transport isolation amendment

The first integrated runtime attempt could not reach Prep because the original Editor already owned the fixed loopback port. Preserve that Editor/session. Extend the allowlist narrowly to `AppBootstrapper.InitializeLocalServices` and a development-only port resolver: an explicit `--az-solo-local-port` configures the existing `SoloSessionCoordinator` constructor before startup, without replacing its lifecycle, binding, transport approval or game logic. Ordinary builds retain port 18777 and contain no resolver method. The runtime fixture checks actual loopback address/port, and its runner checks port ownership before each fresh case. Invalid/missing/duplicate options must fail; never terminate an unrelated owner to obtain a pass. Existing baseline tools and collection evidence remain unchanged. Add focused negative tests and non-development metadata checks; the execution ledger records the new attempt and all actual results.
