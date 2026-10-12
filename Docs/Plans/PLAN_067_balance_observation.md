# PLAN 067 — Balance observation and renewed tests

## Contract

User requested additional collection/observation logic and renewed playtests. Preserve production rules, item values, BT assets, turn timing and network payloads. All new hooks are development-only, opt-in observers. Preserve PLAN066 attempts; a new executable uses a separate cohort.

## Implementation

1. Link committed item actions, consumption and scheduled effects with a server-local action identity (seat + physical CopyId + item + action sequence).
2. Record requested versus actual temperature effects, defense mitigation and clipping separately. Record committed Multi calculation evidence only after the applicator succeeds; never count speculative calculations as effects.
3. Record authoritative preparation start, Ready acceptance (including bots), actual action order, inventory changes, suppressed uses and scheduled cancellation/application.
4. Analyze item use/effect totals, ready latency, passive temperature changes, per-seat initiative, round score progress and deaths. Keep unavailable or ambiguous attribution explicit. Do not infer causal win contribution from correlations.
5. Validate observer math, correlation and malformed/incomplete evidence; run existing combat regressions, compile and rebuild through the connected Editor.
6. Run renewed normal Solo, Duel, 3p and 4p games sequentially with fresh evidence. Preserve stopped/failed attempts and report coverage limits. Further 60-game completion stays in PLAN066; old and new builds are not pooled as one balance cohort.

## Authority and evidence boundaries

- Authoritative hooks run on existing host paths; observers never submit new gameplay commands or update replicated data.
- Delayed effects retain original action identity independently of current turn/action; round/seat clearing records cancellation.
- Inventory observations distinguish physical-copy creation/removal/use changes from genuine random draws. Held-item exposure is not a legal-use opportunity denominator.
- Human mini-game results remain modeled; no actual-human win-rate certification.
- No bot speed or balance tuning is authorized by this instrumentation request.

## Implementation map

| Owner | Change |
|---|---|
| `PlaytestTelemetry`, new `PlaytestBalanceObservation` | Optional host observations, action/effect identities, structured impact math, readable inventory identities |
| `CombatResolver`, `MultiCombatResolution`, `MultiCombatApplicator`, `MultiAttackOperation` | Action scopes and committed pure-calculation observations; internal development-only metadata preserves public gameplay results |
| `ItemEffectApplicator`, `TemperatureSystem`, `BuffDebuffSystem` | Immediate and delayed realized effects, suppression/blocking, cancellation and original-source correlation |
| `PlayerState`, `PlayerInventory`, `TurnManager` | Server-accepted choices/Ready, actual order, physical inventory mutations, preparation timing |
| `FullMatchPlaytestDriver`, `run_full_match_playtest.py` | Opt-in test-human Ready delay with unchanged production timing and BT |
| `analyze_balance_playtest.py`, `test_balance_playtest.py` | Correlation/reconciliation checks and per-item/per-seat CSVs |
| `run_plan067_balance.py`, `run_plan067_delayed.py`, `summarize_balance_playtests.py` | Frozen normal cohort, separate forced delayed fixtures and Korean review artifact |
| `Plan067BalanceTests` | Focused Editor observation tests; existing Plan029/034/036/037 and recorder tests remain regression gates |

Environmental effects with no item scope remain explicitly unlinked-to-item; retain exact event details and audit the authoritative host log. Do not attribute default action fields to an item.

## Gates

| Task | Gate |
|---|---|
| B01 observation | Damage + block + clipping reconcile; delayed identity survives turn changes; non-opt-in behavior unchanged |
| B02 analysis | Synthetic missing/double/dangling effects fail; zero is distinguished from unmeasured |
| B03 runtime | Fresh build; complete normal games; closed healthy logs; consistent terminal states and lobby return |
| B04 report | Korean summary, per-run/per-item CSV, limitations, retained failures and next balance questions |
