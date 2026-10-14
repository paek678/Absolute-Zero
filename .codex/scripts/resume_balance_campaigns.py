"""Resume the two frozen 30-match cohorts without altering original evidence.

Old passing cases are reused; retries get new immutable attempt directories.
A failed attempt is never relabeled as a pass. No production gameplay changes.
"""
import argparse
import collections
import copy
import csv
import json
import os
import re
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace

from analyze_balance_playtest import analyze
from balance_campaign_evidence import collect_case, export, load
from run_full_match_playtest import compare_terminals, digest, inspect_peer, run

REPO = Path(__file__).resolve().parents[2]
DEFAULT_ROOT = REPO / "output/balance60-20261013-recovery"
OLD = REPO / "output/balance30-20261013"
REPEAT = REPO / "output/balance30-20261013-repeat"
ADDITIONAL = REPO / "output/balance30-20261013-additional"
ALLOWED_TRANSIENT = "Error:[LobbyGateway] Get failed"


def now():
    return datetime.now(timezone.utc).isoformat()


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".partial")
    temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2), encoding="utf-8")
    temporary.replace(path)


def read_events(path):
    with path.open(encoding="utf-8-sig") as stream:
        return [json.loads(line) for line in stream if line.strip()]


def verify_fingerprints(fingerprints):
    for name, expected in fingerprints.items():
        path = Path(name)
        if not path.is_file() or digest(path) != expected:
            raise RuntimeError("Frozen executable or observation tool changed: " + name)


def validate_cases(cases):
    if len(cases) != 30 or len({c["id"] for c in cases}) != 30:
        raise ValueError("Expected thirty unique cases")
    if collections.Counter(c["mode"] for c in cases) != {"Solo": 10, "Duel": 10, "Multi": 10}:
        raise ValueError("Expected ten cases per mode")
    for c in cases:
        if c["players"] != {"Solo": 1, "Duel": 2, "Multi": 4}[c["mode"]]:
            raise ValueError("Mode/player mismatch")
        if len(c["policies"]) != c["players"]:
            raise ValueError("Policy count mismatch")


def prepare_manifest(root):
    request = load(ADDITIONAL / "launch-request.json")
    repeat = load(REPEAT / "campaign-manifest.json")
    fingerprints = dict(request["fingerprints"])
    fingerprints[str(Path(__file__).resolve())] = digest(Path(__file__))
    manifest = dict(schema=1, planned=60, exe=request["exe"],
                    fingerprints=fingerprints, series={
                        "repeat": dict(cases=repeat["cases"], original=str(REPEAT)),
                        "additional": dict(cases=request["cases"], original=str(ADDITIONAL))},
                    original_failed_attempt=str(REPEAT / "matches/M02"),
                    legacy_original=str(OLD), legacy_in_target=False,
                    max_new_attempts_per_case=3, time_scale=1,
                    balance_values_changed=False, strict_pass_gates_unchanged=True)
    for series in manifest["series"].values():
        validate_cases(series["cases"])
    path = root / "recovery-manifest.json"
    if path.exists() and load(path) != manifest:
        raise RuntimeError("Recovery manifest drift; preserve evidence and investigate")
    verify_fingerprints(fingerprints)
    if not path.exists():
        save(path, manifest)
    return manifest


def inspect_record(folder, case):
    """Recompute strict checks without writing into the raw attempt."""
    report = load(folder / "report.json")
    peers = [inspect_peer(folder / str(peer)) for peer in range(case["players"])]
    comparison = compare_terminals(peers)
    strict = (report.get("passed") is True and report.get("failure") is None
              and len(report.get("exits", [])) == case["players"]
              and all(code == 0 for code in report["exits"])
              and all(peer["passed"] for peer in peers)
              and comparison["status"] == "passed")
    observed = load(folder / "manifest.json")
    for key in ("players", "policies", "seed", "gameplay_seed"):
        if observed.get(key) != case[key]:
            raise ValueError("Case identity mismatch: " + str(folder) + " / " + key)
    if observed.get("time_scale") != 1 or observed.get("behavior") != "normal":
        raise ValueError("Non-production-condition attempt: " + str(folder))
    balance = analyze(read_events(folder / "0/events.jsonl"))
    if not strict:
        balance["passed"] = False
        balance["errors"].append("normal match gate did not pass on revalidation")
    log = (folder / "0/player.log").read_text(encoding="utf-8-sig", errors="replace")
    balance["environment_announcements_from_host_log"] = dict(
        collections.Counter(re.findall(r"Environment selected: (\w+)", log)))
    record = collect_case(folder, case, balance)
    errors = []
    for peer in range(case["players"]):
        errors.extend(dict(peer=peer, **{k: e.get(k) for k in ("detail", "time", "phase", "round", "turn")})
                      for e in read_events(folder / str(peer) / "events.jsonl")
                      if e.get("kind") == "diagnostic_error")
    validation = dict(strict_passed=strict, terminal_comparison=comparison,
                      peers=[dict(passed=p["passed"], problems=p["problems"],
                                  summary=p.get("summary")) for p in peers],
                      diagnostic_errors=errors)
    return record, validation


def execution_cases(cases):
    return sorted(cases, key=lambda c: (int(c["id"][1:]), {"Solo": 0, "Duel": 1, "Multi": 2}[c["mode"]]))


def retryable_lobby_read(validation, balance_errors=()):
    """Only an exact pre-game lobby-read error with otherwise complete healthy play."""
    if set(balance_errors) - {"normal match gate did not pass on revalidation"}:
        return False
    errors = validation.get("diagnostic_errors", [])
    if not errors or any(e.get("detail") != ALLOWED_TRANSIENT or e.get("phase") != "none"
                         or e.get("round") != 0 or e.get("turn") != 0 for e in errors):
        return False
    if validation.get("terminal_comparison", {}).get("status") != "passed":
        return False
    allowed = {"healthyTelemetry false/missing", "runtime error recorded"}
    return all(set(p["problems"]) <= allowed
               and all((p.get("summary") or {}).get(k) is True
                       for k in ("completed", "visibleResult", "returnedToLobby"))
               for p in validation.get("peers", [])) and bool(validation.get("peers"))


def selected_entries(entries):
    """One case counts at most once; retain every failure separately in attempts.json."""
    by_case = {}
    for entry in entries:
        key = (entry["series"], entry["case"])
        previous = by_case.get(key)
        if previous is None or (entry.get("passed") and not previous.get("passed")):
            by_case[key] = entry
    return by_case


def collect_attempt(root, series, case, folder, tag):
    entry = dict(series=series, case=case["id"], mode=case["mode"],
                 source=str(folder.resolve()), attempt=tag, passed=False)
    try:
        record, validation = inspect_record(folder, case)
        relative = Path("derived") / series / case["id"] / (tag + ".json")
        save(root / relative, dict(record=record, validation=validation))
        entry.update(passed=record["match"]["passed"], derived=str(relative),
                     retryable=retryable_lobby_read(validation, record["match"]["errors"]),
                     errors=record["match"]["errors"],
                     diagnostic_errors=validation["diagnostic_errors"])
    except (OSError, ValueError, KeyError, TypeError) as error:
        entry.update(incomplete=True, retryable=False, errors=[type(error).__name__ + ": " + str(error)])
    return entry


def reconcile(root, manifest):
    entries = []
    for series, spec in manifest["series"].items():
        for case in spec["cases"]:
            original = Path(spec["original"]) / "matches" / case["id"]
            if original.is_dir():
                entries.append(collect_attempt(root, series, case, original, "original"))
            parent = root / "attempts" / series / case["id"]
            if parent.exists():
                for folder in sorted(parent.glob("attempt-*")):
                    entries.append(collect_attempt(root, series, case, folder, folder.name))
    save(root / "attempts.json", entries)
    return entries


def recover_legacy(root):
    """Reaggregate all sixteen earlier passes; incomplete D07 remains explicitly unqualified."""
    manifest = load(OLD / "campaign-manifest.json")
    records, audit = [], []
    for case in manifest["cases"]:
        folder = OLD / "matches" / case["id"]
        if not folder.exists():
            continue
        entry = collect_attempt(root, "legacy", case, folder, "original")
        audit.append(entry)
        if entry.get("derived"):
            records.append(load(root / entry["derived"])["record"])
    target = root / "legacy"
    target.mkdir(parents=True, exist_ok=True)
    export(target, records, manifest["cases"], "historical evidence only; excluded from current sixty", write_workbook=False)
    save(target / "attempts.json", audit)
    return dict(recorded=len(records), passed=sum(r["match"]["passed"] for r in records),
                incomplete_attempts=sum(e.get("incomplete", False) for e in audit))


def publish(root, manifest, entries, status, current=None, reason=None):
    selected = selected_entries(entries)
    summaries, rows = {}, []
    for series, spec in manifest["series"].items():
        target = root / "series" / series
        target.mkdir(parents=True, exist_ok=True)
        records = [load(root / selected[(series, c["id"])]["derived"])["record"]
                   for c in spec["cases"] if (series, c["id"]) in selected
                   and selected[(series, c["id"])].get("derived")]
        data = export(target, records, spec["cases"], status, write_workbook=False)
        modes = {mode: dict(planned=10, recorded=v["recorded"], passed=v["passed"])
                 for mode, v in data["modes"].items()}
        summaries[series] = dict(planned=30, recorded=data["recorded"], passed=data["passed"], modes=modes)
        for record in records:
            rows.append(dict(series=series, **record["match"]))
    passed = sum(e.get("passed", False) for e in selected.values())
    complete = passed == 60 and all(s["passed"] == 30 for s in summaries.values())
    state = dict(schema=1, updated_at_utc=now(), runner_pid=os.getpid(), status=status,
                 planned=60, recorded=len(selected), passed=passed, remaining=60-passed,
                 complete=complete, current=current, reason=reason, series=summaries,
                 failed_attempts=sum(not e["passed"] for e in entries),
                 attempts=len(entries), legacy=load(root / "legacy-summary.json"),
                 interpretation="descriptive data only; final balance/human/visual qualification remains separate")
    save(root / "attempts.json", entries)
    save(root / "match-index.json", rows)
    fields = list(dict.fromkeys(k for row in rows for k in row))
    csv_path = root / "match-index.csv.partial"
    with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields or ["series", "id"])
        writer.writeheader()
        for row in rows:
            writer.writerow({k: json.dumps(v, ensure_ascii=False) if isinstance(v, (list, dict)) else v
                             for k, v in row.items()})
    csv_path.replace(root / "match-index.csv")
    save(root / "progress.json", state)
    save(root / "completion.json", dict(complete=complete, planned=60, passed=passed,
                                      series=summaries, checked_at_utc=now()))
    lines = ["# Live playtest recovery", "", "Updated UTC: " + state["updated_at_utc"], "",
             f"Status: {status}. Passing: {passed}/60. Remaining: {60-passed}.",
             "", "| Series | Mode | Planned | Recorded | Passing |",
             "|---|---|---:|---:|---:|"]
    for name, summary in summaries.items():
        for mode, count in summary["modes"].items():
            lines.append(f"| {name} | {mode} | 10 | {count['recorded']} | {count['passed']} |")
    lines += ["", f"Historical earlier matches: {state['legacy']}; excluded from the current sixty.",
              "", "Every failed attempt remains in attempts.json and its immutable source directory.",
              "Passing checks do not certify human experience, causal balance effects, or all visual ordering.",
              "", "Current: " + str(current), "Reason: " + str(reason)]
    (root / "STATUS.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    return state


def ensure_no_players():
    result = subprocess.run(["powershell.exe", "-NoProfile", "-Command",
                             "@(Get-Process -Name AbsoluteZeroVisual4P -ErrorAction SilentlyContinue).Count"],
                            capture_output=True, text=True, check=True,
                            creationflags=subprocess.CREATE_NO_WINDOW)
    if int(result.stdout.strip()) != 0:
        raise RuntimeError("Another AbsoluteZeroVisual4P process exists; do not overlap or terminate it")


def acquire_lock():
    import msvcrt
    path = REPO / "output/.balance-playtest-recovery.lock"
    stream = path.open("a+b")
    if path.stat().st_size == 0:
        stream.write(b"0")
        stream.flush()
    stream.seek(0)
    try:
        msvcrt.locking(stream.fileno(), msvcrt.LK_NBLCK, 1)
    except OSError:
        stream.close()
        raise RuntimeError("Another recovery collector is active")
    return stream


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, default=DEFAULT_ROOT)
    parser.add_argument("--run", action="store_true", help="Run missing cases after reconciliation")
    args = parser.parse_args()
    root = args.output.resolve()
    root.mkdir(parents=True, exist_ok=True)
    lock = acquire_lock()
    entries, manifest = [], None
    current = None
    try:
        manifest = prepare_manifest(root)
        save(root / "legacy-summary.json", recover_legacy(root))
        entries = reconcile(root, manifest)
        state = publish(root, manifest, entries, "reconciled")
        print(json.dumps({k: state[k] for k in ("status", "recorded", "passed", "remaining", "legacy")}), flush=True)
        if not args.run:
            return 0
        for series, spec in manifest["series"].items():
            for case in execution_cases(spec["cases"]):
                key = (series, case["id"])
                selected = selected_entries(entries).get(key)
                if selected and selected["passed"]:
                    continue
                own = [e for e in entries if (e["series"], e["case"]) == key and e["attempt"] != "original"]
                if own and not own[-1].get("retryable"):
                    raise RuntimeError("Existing failed/incomplete retry needs investigation: " + str(key))
                if selected and not selected.get("retryable"):
                    raise RuntimeError("Original failed/incomplete case needs investigation: " + str(key))
                passed = False
                for number in range(len(own) + 1, manifest["max_new_attempts_per_case"] + 1):
                    verify_fingerprints(manifest["fingerprints"])
                    ensure_no_players()
                    folder = root / "attempts" / series / case["id"] / f"attempt-{number:02}"
                    if folder.exists():
                        raise RuntimeError("Refusing to overwrite attempt: " + str(folder))
                    current = dict(series=series, case=case["id"], mode=case["mode"],
                                   attempt=number, source=str(folder))
                    publish(root, manifest, entries, "running", current)
                    print("START " + json.dumps(current), flush=True)
                    run(SimpleNamespace(exe=Path(manifest["exe"]), output=folder, players=case["players"],
                                        policies=case["policies"], seed=case["seed"],
                                        gameplay_seed=case["gameplay_seed"], timeout=case["timeout"],
                                        behavior="normal", cohort="balance-30-primary",
                                        ready_delay=None, visible=case["visible"]))
                    verify_fingerprints(manifest["fingerprints"])
                    entry = collect_attempt(root, series, case, folder, folder.name)
                    entries.append(entry)
                    publish(root, manifest, entries, "running", current)
                    print("END " + json.dumps(entry, ensure_ascii=True), flush=True)
                    if entry["passed"]:
                        passed = True
                        break
                    if not entry.get("retryable"):
                        raise RuntimeError("Non-transient validation failure: " + str(key))
                    publish(root, manifest, entries, "retry_cooldown", current,
                            "Exact pre-game Lobby Get failure; original failed attempt retained")
                    time.sleep(30)
                if not passed:
                    raise RuntimeError("Lobby retry budget exhausted: " + str(key))
        state = publish(root, manifest, entries, "completed")
        if not state["complete"]:
            raise RuntimeError("Cannot certify sixty passing distinct cases")
        print("RECOVERY COMPLETE 60/60", flush=True)
        return 0
    except Exception as error:
        if manifest is not None and (root / "legacy-summary.json").exists():
            publish(root, manifest, entries, "stopped", current, type(error).__name__ + ": " + str(error))
        print("STOP " + type(error).__name__ + ": " + str(error), flush=True)
        return 2
    finally:
        lock.close()


if __name__ == "__main__":
    raise SystemExit(main())
