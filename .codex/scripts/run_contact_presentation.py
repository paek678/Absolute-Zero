"""Opt-in contact presentation cases in real player processes; no asset data edits."""
import argparse
import json
from pathlib import Path
import re
import subprocess
import time
import uuid

ITEMS = {"hug": "Hug T-shirt", "ice": "Ice Cream", "coffee": "Iced Americano", "soup": "Samgyetang"}


def run(exe, root, mode, count, actor, target, item, relay, port, defense="", capture_motion=False):
    suffix = "-" + defense.lower() if defense else ""
    folder = root / f"{mode}-{count}-{item}-{actor}to{target}{suffix}"
    folder.mkdir(parents=True, exist_ok=False)
    token = uuid.uuid4().hex
    coordination = folder / "lobby-code.tmpdata"
    processes, reports = {}, {}
    failure = None
    peers = 1 if mode == "solo" else count
    try:
        for peer in range(peers):
            name = "host" if peer == 0 else f"client{peer}"
            args = [str(exe), "-batchmode", "-force-d3d11", "-screen-width", "1280", "-screen-height", "720",
                    "-screen-fullscreen", "0", "-logFile", str(folder / f"{name}.log"),
                    "--az-contact-item", ITEMS[item], "--az-contact-actor", str(actor), "--az-contact-target", str(target)]
            if defense:
                args += ["--az-contact-defense", defense]
            if capture_motion:
                args += ["--az-contact-capture-motion", "1"]
            if mode == "solo":
                args += ["--az-contact-solo", "1"]
            else:
                args += ["--az-matrix", "contact", "--az-count", str(count), "--az-run", token,
                         "--az-role", "host" if peer == 0 else "client", "--az-port", str(port)]
                if relay:
                    args += ["--az-relay-flow", "1", "--az-services-profile", f"az44-{peer}-{token[:8]}",
                             "--az-coordination-file", str(coordination)]
            info = subprocess.STARTUPINFO()
            info.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            info.wShowWindow = 0
            processes[name] = subprocess.Popen(args, startupinfo=info)
            if peer == 0 and mode != "solo":
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
        end = time.monotonic() + 170
        while any(p.poll() is None for p in processes.values()) and time.monotonic() < end:
            if any(p.poll() not in (None, 0) for p in processes.values()):
                raise RuntimeError("player validation failed")
            time.sleep(.4)
        if any(p.poll() is None for p in processes.values()):
            raise TimeoutError("scenario timeout")
    except Exception as error:
        failure = str(error)
    finally:
        for process in processes.values():
            if process.poll() is None:
                process.terminate()
            process.wait(timeout=20)
        if coordination.exists():
            coordination.unlink()
    for name, process in processes.items():
        path = folder / f"{name}.contact.json"
        evidence = json.loads(path.read_text(encoding="utf-8-sig")) if path.exists() else {}
        log_path = folder / f"{name}.log"
        log = log_path.read_text(errors="replace") if log_path.exists() else ""
        reports[name] = dict(exit=process.returncode, evidence=evidence,
                            errors=re.findall(r"^.*(?:Exception:|\[CONTACT\] FAIL |\[MATRIX\] FAIL |Visual binding failed).*$", log, re.M))
    passed = failure is None and len(reports) == peers and all(
        p["exit"] == 0 and p["evidence"].get("passed") and not p["errors"] for p in reports.values())
    relay_verified = None
    if relay and mode != "solo":
        logs = [(folder / f"{name}.log").read_text(errors="replace") for name in processes]
        ids = [re.search(r"\[ServicesGateway\] Initialized, PlayerId: (\S+)", log) for log in logs]
        relay_verified = all(ids) and len({match.group(1) for match in ids}) == count and "[RelayGateway] Allocated" in logs[0]
        relay_verified = relay_verified and all("[RelayGateway] Joined relay" in log for log in logs[1:])
        passed = passed and relay_verified
    result = dict(case=folder.name, passed=passed, runner_error=failure, relay_verified=relay_verified, peers=reports)
    (folder / "report.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(f"{folder.name}: {'PASS' if passed else 'FAIL'}", flush=True)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("executable", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--cases", default="solo:2:hug:0:1,solo:2:ice:0:1,online:2:hug:1:0,online:3:coffee:2:0,online:4:hug:0:1,online:4:soup:2:3")
    parser.add_argument("--relay", action="store_true")
    parser.add_argument("--capture-motion", action="store_true", help="Capture diagnostic motion frames for visual review.")
    parser.add_argument("--defense", choices=["Mask", "Windbreaker"], default="",
                        help="Target selects original defense item; current Windbreaker filter does not match food.")
    args = parser.parse_args()
    results = []
    for index, entry in enumerate(args.cases.split(",")):
        mode, count, item, actor, target = entry.split(":")
        if mode not in ("solo", "online") or item not in ITEMS or int(count) not in (2, 3, 4):
            parser.error("cases use solo|online:2|3|4:hug|ice|coffee|soup:actor:target")
        if args.defense and item not in ("ice", "coffee"):
            parser.error("defense cases use ice or coffee so the original direct damage outcome can be checked")
        results.append(run(args.executable.resolve(), args.output.resolve(), mode, int(count), int(actor), int(target), item, args.relay, 19440 + index, args.defense, args.capture_motion))
        args.output.mkdir(parents=True, exist_ok=True)
        (args.output / "summary.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
    raise SystemExit(0 if all(result["passed"] for result in results) else 1)
