"""Bounded local multi-process cases. Records failures without stopping later cases."""
import argparse
import concurrent.futures
import json
from pathlib import Path
import re
import subprocess
import time
import uuid
from matrix_udp_proxy import MatrixUdpProxy
from run_visual_four_player import place_window

CASES = [("temperature-env", n, 0) for n in (2, 3, 4)] + [("item-delay", n, 0) for n in (2, 3, 4)] + [("input", 2, 0), ("input", 3, 0), ("input", 4, 0)] + [("win", 3, w) for w in range(3)] + [("win", 4, w) for w in range(4)] + [
    ("ghost", 4, 0), ("ghost", 4, 2), ("ghost", 4, 1), ("ghost", 4, 3), ("special", 3, 0), ("duel", 2, 0),
    ("disconnect-prep", 4, 0), ("disconnect-attack", 4, 0),
    ("disconnect-terminal", 4, 0), ("host-exit", 4, 0), ("host-exit", 4, 1), ("host-exit", 4, 2), ("host-exit", 4, 3), ("host-exit", 2, 0), ("init-failure", 4, 0),
    ("inventory", 4, 0), ("inventory", 2, 0), ("rpc-guards", 4, 0), ("services", 4, 0), ("multi-round", 4, 0), ("idle", 4, 0),
    ("minigame-target", 4, 0), ("minigame-actor", 4, 0), ("minigame-failure", 4, 0), ("disconnect-idle", 4, 0),
    ("joint", 4, 0), ("delayed", 4, 0), ("duel-repeat", 2, 0), ("duel-mini", 2, 0),
    ("topup-transition", 4, 0), ("topup-transition", 4, 1),
    ("topup-transition", 4, 2), ("topup-transition", 4, 3),
    ("topup-transition", 4, 4), ("topup-transition", 4, 5),
    ("topup-transition", 4, 6), ("topup-transition", 4, 7),
    ("topup-transition", 4, 8), ("topup-transition", 4, 9)]


def clients_observed_game(folder, names):
    """Host-exit recovery starts only after every client's scene probe is armed."""
    clients = names[1:]
    return bool(clients) and all(
        (folder / f"{name}.log").exists()
        and re.search(r"\[MATRIX\] STATE local=\d+ ",
                      (folder / f"{name}.log").read_text(errors="replace"))
        for name in clients)


def run_case(executable, root, case, port, proxy=False, delay_ms=0, loss=0, hold_uplink=0, drop_seat=3, relay=False, delay_view_ms=0, delay_descriptor_ms=0, visible=False, leak_diagnostics=False, resource_probe=False, manual_input=False, cosmetic_check=False, settings_check=False):
    kind, count, winner = case
    folder = root / f"{kind}-{count}-w{winner}"
    folder.mkdir(parents=True, exist_ok=False)
    token = uuid.uuid4().hex
    joined = count - 1 if kind == "init-failure" else count
    names = ["host"] + [f"client{i}" for i in range(1, joined)]
    processes = {}
    killed = None
    failure = None
    proxies = {}
    held = False
    coordination = folder / "lobby-code.tmpdata"
    try:
        for name in names:
            endpoint = port
            if proxy and name != "host":
                proxies[name] = MatrixUdpProxy(port, delay_ms, loss, seed=names.index(name))
                endpoint = proxies[name].port
            info = subprocess.STARTUPINFO()
            info.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            info.wShowWindow = 0
            command = [str(executable)] + ([] if visible else ["-batchmode"])
            command += ["-force-d3d11",
                "--az-matrix", kind, "--az-role", "host" if name == "host" else "client",
                "--az-count", str(count), "--az-winner", str(winner), "--az-run", token,
                "--az-drop-seat", str(drop_seat),
                "--az-network-stress", "1" if proxy and (loss > 0 or delay_ms > 0) else "0",
                "--az-port", str(endpoint), "-logFile", str(folder / f"{name}.log")]
            if manual_input:
                command.extend(["--az-manual-input", "1"])
            if cosmetic_check:
                command.extend(["--az-cosmetic-id", f"hat_0{names.index(name) + 1}",
                                "--az-cosmetic-count", str(count), "--az-cosmetic-full-set"])
            if resource_probe:
                command.extend(["--az-resource-probe", "observe"])
            if settings_check:
                command.extend(["--az-settings-probe", str(names.index(name))])
            if leak_diagnostics:
                command.extend(["--az-leak-diagnostics", "1"])
            if visible:
                command.extend(["-screen-fullscreen", "0", "-screen-width", "960", "-screen-height", "540",
                                "--az-matrix-visible", "1"])
            if relay:
                profile = f"azm-{'h' if name == 'host' else name[-1]}-{token[:8]}"
                command.extend(["--az-relay-flow", "1", "--az-services-profile", profile,
                                "--az-coordination-file", str(coordination)])
            if delay_view_ms > 0 and name != "host":
                command.extend(["--az-delay-inventory-view-ms", str(delay_view_ms)])
            if delay_descriptor_ms > 0 and name != "host":
                command.extend(["--az-delay-grant-descriptor-ms", str(delay_descriptor_ms)])
            processes[name] = subprocess.Popen(command, startupinfo=None if visible else info)
            if visible:
                index = names.index(name)
                place_window(processes[name], (index % 2) * 960, (index // 2) * 540)
            if name == "host":
                deadline = time.monotonic() + (75 if relay else 35)
                while time.monotonic() < deadline:
                    log = folder / "host.log"
                    ready_marker = "[MATRIX] RELAY_LOBBY_READY" if relay else "[MATRIX] LISTENING"
                    if log.exists() and ready_marker in log.read_text(errors="replace"):
                        break
                    if processes[name].poll() is not None:
                        raise RuntimeError("Host exited during startup")
                    time.sleep(.5)
                else:
                    raise TimeoutError("Host startup timeout")
        deadline = time.monotonic() + 320
        while any(p.poll() is None for p in processes.values()) and time.monotonic() < deadline:
            host = (folder / "host.log").read_text(errors="replace")
            if proxies and hold_uplink > 0 and not held and "phase=AttackPhase" in host:
                held = True
                for item in proxies.values():
                    item.uplink_hold_until = time.monotonic() + hold_uplink
                (folder / "uplink-hold.json").write_text(json.dumps(dict(seconds=hold_uplink, trigger="first AttackPhase")))
            for name, process in processes.items():
                if name == killed:
                    continue
                log_path = folder / f"{name}.log"
                text = log_path.read_text(errors="replace") if log_path.exists() else ""
                if "[MATRIX] FAIL" in text or (process.poll() not in (None, 0)):
                    failure = failure or f"{name} failed before scenario completion"
                if kind != "host-exit" and "[MATRIX] NET_STOP" in text and "[MATRIX] PASS" not in text:
                    failure = failure or f"{name} network stopped before scenario completion"
            if failure:
                break
            if killed is None:
                trigger = (((kind in ("disconnect-prep", "disconnect-idle") or (kind == "host-exit" and winner < 2)) and "phase=PrepPhase" in host)
                           or ((kind == "disconnect-attack" or (kind == "host-exit" and winner == 2)) and "phase=AttackPhase" in host)
                           or (kind == "host-exit" and winner == 3 and re.search(r"terminal=[1-9]\d*/8/False", host))
                           or (kind == "disconnect-terminal" and re.search(r"terminal=[1-9]\d*/1/False", host)))
                if kind == "host-exit":
                    trigger = trigger and clients_observed_game(folder, names)
                if trigger:
                    killed = "host" if kind == "host-exit" else None
                    if killed is None:
                        for candidate in names[1:]:
                            log = folder / f"{candidate}.log"
                            if log.exists() and re.search(r"\[MATRIX\] STATE local=" + str(drop_seat) + " ", log.read_text(errors="replace")):
                                killed = candidate
                                break
                    if killed is None:
                        time.sleep(.5)
                        continue
                    processes[killed].terminate()
                    (folder / "fault.json").write_text(json.dumps(dict(process=killed, trigger=kind)))
            time.sleep(.5)
    except Exception as error:
        failure = str(error)
    finally:
        for process in processes.values():
            if process.poll() is None:
                process.terminate()
            process.wait(timeout=20)
        for item in proxies.values():
            item.close()
        if coordination.exists():
            coordination.unlink()
    results = {}
    for name, process in processes.items():
        path = folder / f"{name}.log"
        text = path.read_text(errors="replace") if path.exists() else ""
        errors = re.findall(r"^.*(?:Exception:|\[MATRIX\] FAIL|Visual binding failed|Identity binding timed out).*$", text, re.M)
        checks = re.findall(r"\[MATRIX\] CHECK seq=(\d+) (.+)", text)
        results[name] = dict(exit=process.returncode, passes=re.findall(r"\[MATRIX\] PASS (.+)", text),
                             checks=checks, errors=errors, intentionally_terminated=name == killed,
                             visible=re.findall(r"\[MatchResult\] Visible seq=(\d+) local=(\d+) winners=(\d+)", text),
                             screenshots=[Path(path).name for path in re.findall(r"\[MATRIX\] CAPTURE (.+\.png)", text)
                                          if Path(path).exists()])
        if cosmetic_check:
            results[name]['cosmetic'] = re.findall(r"\[COSMETIC\] VERIFIED (.+)", text)
            results[name]['errors'].extend(re.findall(r"^.*\[COSMETIC\] FAIL.*$", text, re.M))
    active = [v for v in results.values() if not v["intentionally_terminated"]]
    passed = failure is None and len(results) == joined and all(v["exit"] == 0 and len(v["passes"]) == 1 and not v["errors"] for v in active)
    if cosmetic_check:
        signatures = [v.get('cosmetic', []) for v in active]
        passed = passed and len(signatures) == count and len(signatures[0]) == 1 and all(s == signatures[0] for s in signatures)
    if settings_check:
        for index, name in enumerate(names):
            settings_path = folder / f"{name}.settings.json"
            settings = json.loads(settings_path.read_text(encoding="utf-8-sig")) if settings_path.exists() else {}
            results[name]['settings'] = settings
            passed = passed and settings.get('passed', False) and settings.get('peer') == index and settings.get('gameplayViews', 0) > 0
    # State comparisons are made only at completed presentations. Disconnect can change
    # the visible roster between clients' settlement callbacks, so report that separately.
    synchronized = bool(active) and bool(active[0]["checks"]) and all(v["checks"] == active[0]["checks"] for v in active)
    if kind in ("win", "ghost", "special", "joint", "delayed"):
        passed = passed and synchronized
    if kind == "special":
        host_text = (folder / "host.log").read_text(errors="replace")
        passed = passed and all(re.search(r"\[COMBAT\] Actions:.*main=" + re.escape(item), host_text)
                                for item in ("Cat", "Hug T-shirt", "Ice Cream"))
    if kind == "duel-mini":
        host_text = (folder / "host.log").read_text(errors="replace")
        passed = passed and "Mini-game SUCCESS: Hug T-shirt" in host_text
        passed = passed and all("DUEL_MINIGAME_ROUND2" in v["passes"][0] for v in active if v["passes"])
    if visible:
        passed = passed and all(len(v["screenshots"]) >= 2 for v in active)
        if kind == "duel-mini":
            passed = passed and any("minigame-active" in name for name in results["host"]["screenshots"])
    if kind in ("win", "ghost", "disconnect-prep", "disconnect-attack", "disconnect-terminal"):
        passed = passed and all(len(v["visible"]) == 1 and int(v["visible"][0][2]) == 1 << winner for v in active)
    if kind in ("joint", "delayed"):
        expected_mask = 3 if kind == "joint" else 2
        passed = passed and all(len(v["visible"]) == 1 and int(v["visible"][0][2]) == expected_mask for v in active)
    if hold_uplink > 0:
        host_text = (folder / "host.log").read_text(errors="replace")
        passed = passed and held and "[PresentationBarrier] Timeout" in host_text and "ACK ignored" in host_text
    relay_verified = None
    if relay:
        logs = {name: (folder / f"{name}.log").read_text(errors="replace") for name in names}
        player_ids = [re.search(r"\[ServicesGateway\] Initialized, PlayerId: (\S+)", logs[name])
                      for name in names]
        relay_verified = (all(player_ids) and len({match.group(1) for match in player_ids}) == joined
                          and "[RelayGateway] Allocated" in logs["host"]
                          and "[MATRIX] RELAY_HOST_LISTENING" in logs["host"]
                          and all("[RelayGateway] Joined relay" in logs[name]
                                  and "[MATRIX] RELAY_CLIENT_CONNECTED" in logs[name]
                                  for name in names[1:]))
        passed = passed and relay_verified
    ordering_verified = None
    if kind == "topup-transition" and winner in (6, 7):
        logs = {name: (folder / f"{name}.log").read_text(errors="replace") for name in names}
        rejection_marker = ("[MATRIX] STALE_GRANT_COMMAND_REJECTED" if winner == 6
                            else "[MATRIX] STALE_GRANT_CANCEL_REJECTED")
        ordering_verified = (delay_view_ms > 0
                             and any(rejection_marker in log
                                     for name, log in logs.items() if name != "host")
                             and all("[MATRIX] GRANT_VIEW_DELAY_STARTED" in log
                                     and "[MATRIX] GRANT_VIEW_DELAY_RELEASED" in log
                                     for name, log in logs.items() if name != "host"))
        passed = passed and ordering_verified
    if kind == "topup-transition" and winner in (8, 9):
        logs = {name: (folder / f"{name}.log").read_text(errors="replace") for name in names}
        seat_one_logs = [log for name, log in logs.items() if name != "host"
                         and re.search(r"\[MATRIX\] STATE local=1 ", log)]
        client = seat_one_logs[0] if len(seat_one_logs) == 1 else ""
        markers = ("[MATRIX] GRANT_DESCRIPTOR_DELAY_STARTED",
                   "[MATRIX] GRANT_VIEW_BEFORE_DESCRIPTOR",
                   "[MATRIX] REVERSE_STALE_SENT",
                   "[MATRIX] REVERSE_STALE_REJECTED",
                   "[MATRIX] GRANT_DESCRIPTOR_DELAY_RELEASED")
        positions = [client.find(marker) for marker in markers]
        ordering_verified = (delay_descriptor_ms > 0 and all(i >= 0 for i in positions)
                             and positions == sorted(positions))
        passed = passed and ordering_verified
    proxy_healthy = all(p.stats["errors"] == 0 for p in proxies.values()) if proxy else None
    if proxy_healthy is False:
        passed = False
    report = dict(case=case, passed=passed, checkpoints_equal=synchronized, runner_error=failure, results=results,
                  proxy=dict(enabled=proxy, one_way_delay_ms=delay_ms, loss=loss, hold_uplink_seconds=hold_uplink,
                             stats={k: v.stats for k, v in proxies.items()}), drop_seat=drop_seat,
                  transport="unity-relay" if relay else "local-utp", relay_verified=relay_verified,
                  delay_view_ms=delay_view_ms, delay_descriptor_ms=delay_descriptor_ms,
                  ordering_verified=ordering_verified,
                  visible_capture=visible, proxy_healthy=proxy_healthy, leak_diagnostics=leak_diagnostics, resource_probe=resource_probe, manual_input=manual_input)
    (folder / "report.json").write_text(json.dumps(report, indent=2))
    print(f"{folder.name}: {'PASS' if passed else 'FAIL'}", flush=True)
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("executable", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--filter", default="")
    parser.add_argument("--workers", type=int, default=2)
    parser.add_argument("--count", type=int, choices=[2, 3, 4], default=None)
    parser.add_argument("--port-base", type=int, default=17900)
    parser.add_argument("--proxy", action="store_true")
    parser.add_argument("--relay", action="store_true")
    parser.add_argument("--visible", action="store_true")
    parser.add_argument("--leak-diagnostics", action="store_true")
    parser.add_argument("--resource-probe", action="store_true")
    parser.add_argument("--manual-input", action="store_true")
    parser.add_argument("--delay-view-ms", type=int, default=0)
    parser.add_argument("--delay-descriptor-ms", type=int, default=0)
    parser.add_argument("--delay-ms", type=float, default=0)
    parser.add_argument("--loss", type=float, default=0)
    parser.add_argument("--hold-uplink", type=float, default=0)
    parser.add_argument("--drop-seat", type=int, choices=[1, 2, 3], default=3)
    parser.add_argument("--limit", type=int, default=0)
    parser.add_argument("--variant", type=int, default=None,
                        help="Run only cases with this scenario variant index")
    args = parser.parse_args()
    if args.manual_input and (args.filter != "idle" or not args.visible):
        parser.error("Manual input requires --filter idle --visible")
    if not 0 <= args.loss <= 1 or args.delay_ms < 0 or args.hold_uplink < 0:
        parser.error("Delay/hold must be nonnegative and loss must be in [0,1]")
    if not args.proxy and (args.delay_ms or args.loss or args.hold_uplink):
        parser.error("Network impairment requires --proxy")
    if args.relay and args.proxy:
        parser.error("Relay mode cannot use the local UTP proxy")
    if args.delay_view_ms < 0 or args.delay_descriptor_ms < 0:
        parser.error("View and descriptor delays must be nonnegative")
    args.output.mkdir(parents=True, exist_ok=False)
    cases = [case for case in CASES if not args.filter or case[0] in args.filter.split(",")]
    if args.count is not None:
        cases = [case for case in cases if case[1] == args.count]
    if args.variant is not None:
        cases = [case for case in cases if case[2] == args.variant]
    if args.limit > 0:
        cases = cases[:args.limit]
    if not cases:
        parser.error("No scenarios matched; an empty selection cannot pass validation")
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = [pool.submit(run_case, args.executable.resolve(), args.output.resolve(), case, args.port_base + i, args.proxy, args.delay_ms, args.loss, args.hold_uplink, args.drop_seat, args.relay, args.delay_view_ms, args.delay_descriptor_ms, args.visible, args.leak_diagnostics, args.resource_probe, args.manual_input) for i, case in enumerate(cases)]
        reports = [future.result() for future in futures]
    (args.output / "matrix.json").write_text(json.dumps(reports, indent=2))
    raise SystemExit(0 if all(r["passed"] for r in reports) else 1)
