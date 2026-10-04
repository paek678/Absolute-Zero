"""PLAN043: real player processes; the opt-in probe drives InputSystem and menu buttons."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import time
import uuid


def run(exe, root, scenario, count, seat, relay, port):
    folder = root / f"{scenario}-{count}-exit{seat}"
    folder.mkdir(parents=True, exist_ok=False)
    token = uuid.uuid4().hex
    coordination = folder / "lobby-code.tmpdata"
    processes = {}
    reports = {}
    failure = None
    peers = 1 if scenario == "solo" else count
    try:
        for peer in range(peers):
            name = "host" if peer == 0 else f"client{peer}"
            args = [str(exe), "-batchmode", "-force-d3d11", "-screen-width", "1280", "-screen-height", "720",
                    "-screen-fullscreen", "0", "-logFile", str(folder / f"{name}.log"),
                    "--plan043-menu-case", scenario, "--plan043-exit-seat", str(seat)]
            if scenario != "solo":
                args += ["--az-matrix", "menu", "--az-count", str(count), "--az-run", token,
                         "--az-role", "host" if peer == 0 else "client", "--az-port", str(port)]
                if relay:
                    args += ["--az-relay-flow", "1", "--az-services-profile", f"az43-{peer}-{token[:8]}",
                             "--az-coordination-file", str(coordination)]
            info = subprocess.STARTUPINFO()
            info.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            info.wShowWindow = 0
            processes[name] = subprocess.Popen(args, startupinfo=info)
            if peer == 0 and scenario != "solo":
                end = time.monotonic() + 75
                marker = "[MATRIX] RELAY_LOBBY_READY" if relay else "[MATRIX] LISTENING"
                while time.monotonic() < end:
                    log = folder / "host.log"
                    if log.exists() and marker in log.read_text(errors="replace"):
                        break
                    if processes[name].poll() is not None:
                        raise RuntimeError("host failed during startup")
                    time.sleep(.3)
                else:
                    raise TimeoutError("host startup timeout")
        end = time.monotonic() + 180
        while any(p.poll() is None for p in processes.values()) and time.monotonic() < end:
            if any(p.poll() not in (None, 0) for p in processes.values()):
                raise RuntimeError("player validation failed")
            time.sleep(.5)
        if any(p.poll() is None for p in processes.values()):
            raise TimeoutError("scenario timeout")
    except Exception as e:
        failure = str(e)
    finally:
        for p in processes.values():
            if p.poll() is None:
                p.terminate()
            p.wait(timeout=20)
        if coordination.exists():
            coordination.unlink()
    for name, p in processes.items():
        path = folder / f"{name}.menu.json"
        evidence = json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else {}
        log = (folder / f"{name}.log").read_text(errors="replace")
        reports[name] = dict(exit=p.returncode, evidence=evidence,
                            errors=re.findall(r"^.*(?:Exception:|\[MENU\] FAIL |Visual binding failed).*$", log, re.M))
    passed = failure is None and len(reports) == peers and all(
        p["exit"] == 0 and p["evidence"].get("passed") and not p["errors"] for p in reports.values())
    relay_verified = None
    if relay and scenario != "solo":
        logs = [(folder / f"{name}.log").read_text(errors="replace") for name in processes]
        ids = [re.search(r"\[ServicesGateway\] Initialized, PlayerId: (\S+)", log) for log in logs]
        relay_verified = all(ids) and len({m.group(1) for m in ids}) == count and "[RelayGateway] Allocated" in logs[0]
        relay_verified = relay_verified and all("[RelayGateway] Joined relay" in log for log in logs[1:])
        passed = passed and relay_verified
    result = dict(passed=passed, runner_error=failure, relay_verified=relay_verified, peers=reports)
    (folder / "report.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"{folder.name}: {'PASS' if passed else 'FAIL'}", flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("executable", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--cases", default="solo:2:0,prep:2:1,prep:3:1,prep:4:2,attack:4:3,ghost:4:1,prep:4:0")
    parser.add_argument("--relay", action="store_true")
    a = parser.parse_args()
    results = []
    for i, entry in enumerate(a.cases.split(",")):
        scenario, count, seat = entry.split(":")
        results.append(run(a.executable.resolve(), a.output.resolve(), scenario, int(count), int(seat), a.relay, 19430+i))
    a.output.mkdir(parents=True, exist_ok=True)
    (a.output / "summary.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    raise SystemExit(0 if all(r["passed"] for r in results) else 1)
