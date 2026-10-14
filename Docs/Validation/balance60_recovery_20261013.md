# Sixty-match collection recovery — 2026-10-13

## Authorization and scope

The user explicitly requested continuing all uncollected matches while aggregating the data in chat `01a0eb5c-5112-7d73-8397-c9eb1c9564bd`.
This resumes the currently designated **repeat 30 + additional 30 = 60** cases, ten Solo/Duel/Multi per cohort, twenty per mode overall.
The earlier `balance30-20261013` cohort remains separate: sixteen passing records and an incomplete D07. It is reaggregated, not mixed into the current sixty and not silently restarted as a third cohort.

Canonical live output: `output/balance60-20261013-recovery/`.
Prior cohort progress files are historical terminal records; their stopped/blocked values are not the new runner's status.

Non-goals: production gameplay/balance changes, new seeds, weakened pass criteria, rebuilding the player, overwriting raw attempts, retrospective causal balance interpretation, or human/visual certification.

## Confirmed interruption

The previous repeat stopped at M02 on 2026-10-13 16:52 KST with six records and five passes.
M02's host recorded one diagnostic error:
`Error:[LobbyGateway] Get failed`, time 4.05 seconds, phase none, round/turn zero.
The player log adds `Something went wrong`. This identifies the failing service operation, not a proven underlying service outage.
All four peers finished, displayed a result, returned to lobby, and had matching terminal shared state.
The host correctly failed healthyTelemetry and exited 2. The failed report is retained as failed.

The additional queue correctly blocked. Its finalizer then also threw FileNotFoundError because no campaign-data.json existed.
The replacement collector manages progress/final certification directly, including empty and incomplete series, and does not launch the old queue/finalizers.

## Recovery implementation

- New orchestration only: `.codex/scripts/resume_balance_campaigns.py`.
- Tests: `.codex/scripts/test_resume_balance_campaigns.py`.
- Existing executor, analyzer, export implementation, gameplay code, and build remain unchanged.
- Existing executable path with enabled inbound Allow rules is reused:
  `C:/Users/paek6/AppData/Local/Temp/AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe`.
- Executable and six AbsoluteZero managed assembly hashes matched the frozen cohorts; observation tool hashes matched the additional launch request.
- Source and executable identities remain distinct. A launch-time source snapshot does not certify that every working-tree edit is in the frozen executable.

The collector:

1. Revalidates each existing passing attempt against peer summaries, raw events, terminal shared state, exits, authority invariants, balance measurements, and drop records.
2. Reuses the five passing repeat cases without rerunning them.
3. Records the failed original M02 separately and runs a new immutable attempt with its original condition and seed.
4. Runs the remaining repeat cases and then the frozen additional cases sequentially.
5. Retries only the exact pre-game Lobby Get diagnostic when the rest of the match completed and passed structural/state checks. Up to three new attempts per case, with a 30-second cooldown.
6. Stops on a different validation failure, malformed/incomplete retry, exhausted transient retry budget, build/tool drift, or another player process.
7. Holds an OS-released process lock to prevent duplicate recovery collectors and checks for other players before each match.
8. Never counts more than one result per cohort/case. A later passing retry can replace a failed case in the selected aggregate, but the failed attempt remains in attempts.json and its source folder.
9. Writes all eleven descriptive table groups, per-mode records, coverage definitions, and a cross-cohort match index after each attempt.
10. Certifies complete only when all sixty distinct conditions pass, thirty per cohort.

The code does not fix/suppress the Lobby error or mark a failed game healthy. Retries remain explicit evidence.

## Initial validation and execution checkpoint

- Python tests: **53/53 passed** (`test_resume_balance_campaigns`, `test_full_match_playtest`, `test_balance_playtest`), including no duplicate case counting, mixed balance-error rejection, and original interleaved launch order for the additional cohort.
- Initial reconciliation: current target 6 recorded / 5 passing / 55 missing passing results.
- Earlier cohort: 16 recorded / 16 passing / 1 incomplete attempt, separately recorded.
- Original evidence preservation manifest: `original-evidence-hashes.json` (274 files).
- Recovery process launched 2026-10-13 17:38 KST. Four owned player processes launched M02 retry at 17:39.
- This checkpoint is not evidence of sixty-match completion. Read live progress and process state.

## Outputs and status checks

### M02 recovery and controller-only correction

The first fresh M02 attempt passed after 430.60 seconds wall time, with exits [0,0,0,0], no peer validation problems, synchronized terminal state and all peers returning to lobby. The original failed M02 remains untouched.

During that attempt, two orchestration details were tightened: an accompanying balance/drop error must never qualify for the infrastructure retry, and additional cases must follow the original launcher’s Solo/Duel/Multi rotation even though launch-request.json stores mode-grouped cases. Tests increased to 53/53. No loaded match behavior or original observation tool changed.

The first controller detected its changed source fingerprint only after the owned players had finished and stopped safely. Its source, manifest and logs were archived under controller-revisions/. After confirming it and every owned player had exited, only the controller fingerprint was updated; executable, observation tools, cases and seeds remained identical. The replacement collector revalidates the completed M02 and proceeds to S03. Current checkpoint is 6/60 passing, 54 remaining. Use progress.json for later counts.

| File/directory | Meaning |
|---|---|
| `progress.json` | Atomic current status, runner PID, current case, passing/remaining counts |
| `completion.json` | All-sixty completion certificate; only `complete: true` plus matching evidence qualifies |
| `STATUS.md` | Human-readable live counts |
| `recovery-manifest.json` | Frozen case sets, executable/tool fingerprints, explicit retry policy |
| `attempts.json` | Every selected-series attempt, including failures, source paths, diagnostics |
| `attempts/<series>/<case>/attempt-NN/` | New raw peer logs/events/configuration/screenshots/report |
| `derived/<series>/<case>/` | Recomputed full data and validation with source references |
| `series/repeat/`, `series/additional/` | campaign-data.json, per-mode data, tables.json and coverage.json |
| `match-index.json`, `match-index.csv` | One selected record per series/case, explicit pass status and source |
| `legacy/`, `legacy-summary.json` | Separate earlier sixteen-game aggregate and incomplete D07 record |
| `legacy/partial-D07-analysis.json` | Diagnostic-only extraction of historical partial host telemetry; never counted as a completed/passing match |
| `runner-console.txt`, `runner-errors.txt` | Detached recovery process logs |

From the repository root:

```powershell
Get-Content output/balance60-20261013-recovery/progress.json -Raw
Get-Content output/balance60-20261013-recovery/runner-console.txt -Tail 8
```

Do not infer process liveness solely from a running status. Verify runner_pid and its children. A partial events.jsonl file in the current attempt is normal while it is writing.

For an authorized safe resume, only after confirming that there is no active collector or owned player:

```powershell
python .codex/scripts/resume_balance_campaigns.py --run
```

This command reconciles existing attempts first. If an incomplete retry exists it stops for investigation instead of overwriting it.
Use a hidden detached process with redirected stdout/stderr for long execution. Do not use an app tool session as the sole lifetime owner of the hours-long collector.

## Follow-up

Heartbeat automation `absolute-zero-60`, attached to the current chat, checks every 15 minutes.
It remains quiet during unchanged healthy execution and reports failures requiring action or final completion.
Ownership subsequently moved to the current chat, and the user authorized continuing the Solo bot integration after this collection. The same heartbeat now remains active through the separately gated I05-I08 integration checks; that newer instruction supersedes pausing at collection completion. See [the integration ledger](solo_bot_main_integration_20261013.md). No external messages or another AI thread were dispatched.

## Deferred validation ledger

| ID | Check | Current status | Evidence needed to close |
|---|---|---|---|
| REC-01 | Recover failed M02 under unchanged condition | Passed: first new retry, 430.60 seconds, all four peers clean | attempts/repeat/M02/attempt-01/report.json and peer evidence |
| REC-02 | Repeat cohort ten passing cases per mode | 5/30 passing at initial checkpoint | series/repeat counts 10/10/10 plus source reports |
| REC-03 | Additional cohort ten passing cases per mode | Not started at initial checkpoint | series/additional counts 10/10/10 plus source reports |
| REC-04 | Preserve historical evidence and separate cohort identity | All 274 original hashes rechecked unchanged after reconciliation | Recheck once more after the full recovery, no old files overwritten |
| REC-05 | Final aggregate, no duplicates, clean process exit | Pending | 60 unique keys, 60 strict passes, complete certificate, no owned player/runner |
| REC-06 | Full B01-B60 qualification | Not claimed by collection | Explicit later visual/human/attribution review; coverage flags stay qualified only with evidence |

The previous observation limits still apply: modeled human behavior, target preference bias, planned RNG draws versus committed grants, missing legal opportunity denominators, partial rare-event coverage, and diagnostic re-render versus framebuffer ordering. Preserve these qualifications in every later summary.

## Final collection checkpoint — 2026-10-13 23:04 KST

Completed **60/60 distinct passing conditions**, repeat 30/30 plus additional 30/30. Each cohort has ten Solo, ten Duel and ten Multi passes, twenty per mode overall. Final M10 completed after 724.41 seconds with four clean peers. The original failed Lobby Get attempt remains failed and preserved: 61 total attempts, sixty selected passing conditions. The older sixteen-game cohort and incomplete D07 are still separate.

Completion/progress certificates agree; the collector and its owned players exited. All 274 original evidence hashes and thirteen frozen runtime/tool fingerprints matched before the integration build started. REC-02, REC-03, REC-04 and REC-05 are therefore passed for their specified collection scope. Evidence: progress.json, completion.json, attempts.json, match-index.json and source/derived peer reports; final preservation is also recorded in the integration worktree's takeover/collection-final-preservation.json. REC-06 remains unqualified: completion does not certify balance, human play feel or every visual observation.

The frozen Player was subsequently copied and hash-verified before its approved path was reused for the separately authorized Solo integration build. That new build and its tests are not part of this unchanged collection cohort.
