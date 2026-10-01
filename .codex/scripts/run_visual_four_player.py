"""Run a visible four-player development match and build an evidence gallery."""
import argparse
import ctypes
from ctypes import wintypes
import html
import json
from pathlib import Path
import re
import subprocess
import time
import uuid


def windows_for_pid(pid):
    found = []
    enum_proc = ctypes.WINFUNCTYPE(ctypes.c_bool, wintypes.HWND, wintypes.LPARAM)

    def callback(hwnd, _):
        process_id = wintypes.DWORD()
        ctypes.windll.user32.GetWindowThreadProcessId(hwnd, ctypes.byref(process_id))
        if process_id.value == pid and ctypes.windll.user32.IsWindowVisible(hwnd):
            found.append(hwnd)
        return True

    ctypes.windll.user32.EnumWindows(enum_proc(callback), 0)
    return found


def place_window(process, x, y, width=960, height=540, timeout=25):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline and process.poll() is None:
        windows = windows_for_pid(process.pid)
        if windows:
            ctypes.windll.user32.SetWindowPos(windows[0], 0, x, y, width, height, 0x0040)
            return True
        time.sleep(0.25)
    return False


def read(path):
    return path.read_text(errors="replace") if path.exists() else ""


def terminal_pixels(output):
    """Narrow visibility gate for the current GameHudBuilder result layout, not OCR."""
    try:
        from PIL import Image
    except ImportError:
        return {"passed": False, "error": "Pillow is required for the terminal pixel check", "captures": []}
    evidence = []
    for path in sorted(output.glob("*.final-confirmed-terminal-result.png")):
        with Image.open(path) as image:
            image = image.convert("RGB")
            width, height = image.size
            scale = width / 1920.0
            def region(cx, cy, w, h):
                return image.crop(tuple(round(v) for v in (
                    width / 2 + (cx-w/2)*scale, height / 2 + (cy-h/2)*scale,
                    width / 2 + (cx+w/2)*scale, height / 2 + (cy+h/2)*scale)))
            button = region(110, 60, 200, 45)
            text = region(0, -20, 800, 100)
            button_pixels = sum(r > 70 and r > g * 1.25 and abs(g-b) < 25
                                for y in range(button.height) for x in range(button.width)
                                for r, g, b in [button.getpixel((x, y))])
            text_pixels = sum(max(text.getpixel((x, y))) > 65
                              for y in range(text.height) for x in range(text.width))
            evidence.append({"file": path.name, "button_pixels": button_pixels, "text_pixels": text_pixels,
                             "passed": button_pixels > button.width * button.height * .25 and text_pixels > 30})
    return {"passed": len(evidence) == 4 and all(row["passed"] for row in evidence), "captures": evidence}


def complete_presentation_captures(output, result):
    records, images = result["capture_records"], result["screenshots"]
    if not records or len(records) != len(set(records)):
        return False
    if not all(name in images and (output / name).is_file() and (output / name).stat().st_size > 0
               for name in records):
        return False
    if not any("-prep.png" in name for name in records) or not any("final-" in name for name in records):
        return False
    for row in result["checkpoints"]:
        sequence = re.search(r"seq=(\d+)", row)
        if sequence is None or not any(f"settled-seq-{sequence.group(1)}.png" in name for name in records):
            return False
    return bool(result["checkpoints"])


def build_report(output, metadata, processes, placement, runner_error):
    roles = ["host", "client1", "client2", "client3"]
    results = {}
    error_pattern = re.compile(
        r"^.*(?:\[VISUAL\] FAIL|\[COSMETIC\] FAIL|Exception:|NullReferenceException|IndexOutOfRangeException|"
        r"Identity binding timed out|Visual binding failed).*$", re.MULTILINE)
    seat_by_role = {"host": "host"}
    for role in roles[1:]:
        match = re.search(r"\[VISUAL\] ASSIGNED local=(\d+)", read(output / f"{role}.log"))
        seat_by_role[role] = f"seat{match.group(1)}" if match else role
    warning_patterns = {
        "missing_animator": r"_animator is NULL",
        "missing_crown_glyph": r"Unicode value \\u265B was not found",
        "loading_overlay_unassigned": r"SceneLoadSyncManager\] overlayRoot is not assigned",
        "duplicate_minigame_result": r"Mini-game result rejected: no pending game",
        "presentation_timeout": r"PresentationBarrier\] Timeout|Result presentation timeout",
    }
    for role in roles:
        log_path = output / f"{role}.log"
        text = read(log_path)
        process = processes.get(role)
        checkpoints = re.findall(r"\[VISUAL\] CHECKPOINT (.+)", text)
        results[role] = {
            "exit_code": process.returncode if process else None,
            "pass": re.findall(r"\[VISUAL\] PASS (.+)", text),
            "assignments": re.findall(r"\[VISUAL\] ASSIGNED (.+)", text),
            "plans": re.findall(r"\[VISUAL\] PLAN (.+)", text),
            "checkpoints": checkpoints,
            "capture_records": [Path(name.strip()).name for name in re.findall(r"\[VISUAL\] CAPTURE (.+)", text)],
            "errors": error_pattern.findall(text),
            "findings": {name: len(re.findall(pattern, text)) for name, pattern in warning_patterns.items()},
            "screenshots": [path.name for path in sorted(output.glob(f"{seat_by_role[role]}.*.png"))],
            "window_placed": placement.get(role, False),
            "authenticated_player_id": (re.search(r"\[ServicesGateway\] Initialized, PlayerId: (\S+)", text).group(1)
                                            if re.search(r"\[ServicesGateway\] Initialized, PlayerId: (\S+)", text) else None),
            "relay_allocation": "[RelayGateway] Allocated" in text,
            "relay_join": "[RelayGateway] Joined relay" in text,
            "relay_connected": ("[VISUAL] RELAY_HOST_LISTENING" in text
                                or "[VISUAL] RELAY_CLIENT_CONNECTED" in text),
        }
    lethal_order_verified = None
    possession_verified = None
    mini_ticket_verified = None
    ledger_verified = None
    lifecycle_verified = None
    full_match_verified = None
    if metadata.get("full_natural"):
        signatures = [[re.sub(r" local=\d+", "", row) for row in result["checkpoints"]]
                      for result in results.values()]
        checks = []
        for role in roles:
            log = read(output / f"{role}.log")
            scores = re.findall(r"\[VISUAL\] NATURAL_KILL_SCORE local=\d+ score=(\d+)", log)
            casts = re.findall(r"\[VISUAL\] GHOST_VFX_START actor=1 skill=(\d+) target=(\d+)", log)
            result = re.findall(r"\[MatchResult\] Visible seq=(\d+) local=\d+ winners=(\d+)", log)
            checks.append(scores == ["0", "1", "2", "3", "4", "5"]
                          and casts == [("1", "2"), ("0", "0"), ("0", "2"),
                                        ("0", "0"), ("0", "2"), ("0", "0")]
                          and len(result) == 1 and result[0][1] == "2"
                          and "[VISUAL] FULL_SUPPRESSED" in log
                          and "[VISUAL] FULL_LETHAL_ARMED" not in log
                          and "[PresentationBarrier] Timeout" not in log)
        full_match_verified = all(checks)
        synchronized = (full_match_verified and len(signatures[0]) >= 10
                        and all(rows == signatures[0] for rows in signatures[1:]))
        capture_complete = all(len(result["screenshots"]) >= 20 for result in results.values())
    elif metadata.get("full_match"):
        checks = []
        for role in roles:
            log = read(output / f"{role}.log")
            ordinary = re.findall(r"\[VISUAL\] CHECKPOINT seq=(\d+) local=\d+", log)
            suppressed = re.findall(r"\[VISUAL\] FULL_SUPPRESSED local=\d+ seq=(\d+) count=(\d+)", log)
            cue = re.findall(r"\[VISUAL\] FULL_SUPPRESSION_CUE local=\d+ actor=(\d+)", log)
            result = re.findall(r"\[MatchResult\] Visible seq=(\d+) local=\d+ winners=(\d+)", log)
            casts = re.findall(r"\[VISUAL\] GHOST_VFX_START actor=1 skill=(\d+) target=(\d+)", log)
            checks.append(len(ordinary) >= 6 and ordinary[:4] == ["1", "2", "3", "4"]
                          and len(suppressed) == 1 and suppressed[0][1] == "1"
                          and cue == ["2"] and casts == [("1", "2"), ("0", "0")]
                          and len(result) == 1 and result[0][1] == "2"
                          and "[VISUAL] GHOST_ACTUAL_IMPACT" in log
                          and "[VISUAL] GHOST_DEATH_PRESENTATION" in log
                          and "[PresentationBarrier] Timeout" not in log)
        full_match_verified = all(checks)
        signatures = [[re.sub(r" local=\d+", "", row) for row in result["checkpoints"]]
                      for result in results.values()]
        synchronized = (full_match_verified and bool(signatures[0])
                        and all(rows == signatures[0] for rows in signatures[1:]))
        capture_complete = all(len(result["screenshots"]) >= 14 for result in results.values())
    elif metadata.get("mini_ticket_case"):
        checks = []
        copies = []
        for role in roles:
            log = read(output / f"{role}.log")
            assignment = re.search(r"\[VISUAL\] ASSIGNED local=(\d+)", log)
            seat = int(assignment.group(1)) if assignment else -1
            if seat == 2:
                first = re.findall(r"\[VISUAL\] MINI_TICKET_A attempt=(\d+) copy=(\d+)", log)
                second = re.findall(r"\[VISUAL\] MINI_TICKET_B attempt=(\d+) copy=(\d+)", log)
                stale = re.findall(r"\[VISUAL\] MINI_STALE_INERT copy=(\d+) uses=(\d+)", log)
                queued = re.findall(r"\[VISUAL\] MINI_B_QUEUED copy=(\d+)", log)
                checks.append(len(first) == len(second) == len(stale) == len(queued) == 1
                              and int(second[0][0]) > int(first[0][0])
                              and first[0][1] != second[0][1]
                              and stale[0][0] == second[0][1] == queued[0])
                copies.extend(queued)
            else:
                observed = re.findall(r"\[VISUAL\] MINI_OBSERVER_QUEUED local=\d+ copy=(\d+)", log)
                checks.append(len(observed) == 1)
                copies.extend(observed)
        host_log = read(output / "host.log")
        replacement = re.findall(r"\[VISUAL\] MINI_REPLACED slot=0 item=\d+ a=(\d+) b=(\d+)", host_log)
        stale_rejections = re.findall(r"Mini-game result rejected: stale ticket (\d+)", host_log)
        topup_ok = ("[VISUAL] MINI_TOPUP_TRIGGERED copy=" in host_log
                    if metadata["mini_ticket_case"] == "replacement-topup" else True)
        round_staged = re.findall(r"\[VISUAL\] MINI_ROUND_STAGED a=(\d+) b=(\d+) round=(\d+)", host_log)
        round_ok = (len(round_staged) == 1 and round_staged[0][1] == copies[0]
                    if metadata["mini_ticket_case"] == "round" and copies else
                    metadata["mini_ticket_case"] != "round")
        replacement_ok = (len(replacement) == 1 and replacement[0][1] == copies[0]
                          if metadata["mini_ticket_case"] != "round" and copies else
                          metadata["mini_ticket_case"] == "round")
        mini_ticket_verified = (all(checks) and len(set(copies)) == 1
                                and replacement_ok and round_ok
                                and len(stale_rejections) >= 3 and topup_ok)
        synchronized = mini_ticket_verified
        capture_complete = all(len(result["screenshots"]) >=
                               (6 if "[VISUAL] ASSIGNED local=2" in read(output / f"{role}.log") else 2)
                               for role, result in results.items())
    elif metadata.get("ghost_lifecycle_case"):
        signatures = []
        checks = []
        for role in roles:
            log = read(output / f"{role}.log")
            assignment = re.search(r"\[VISUAL\] ASSIGNED local=(\d+)", log)
            seat = int(assignment.group(1)) if assignment else -1
            presentations = re.findall(r"\[VISUAL\] LIFECYCLE_PRESENTATION local=\d+ ordinal=(\d+) seq=(\d+)", log)
            state = re.findall(r"\[VISUAL\] LIFECYCLE_CHECK local=\d+ match=(\d+) round=(\d+) spent=(\d+) possessed=(\d+)", log)
            replies = [(int(request_id), result) for request_id, result in
                       re.findall(r"\[VISUAL\] LEDGER_REPLY local=\d+ id=(\d+) result=(\w+)", log)]
            signatures.append(state)
            checks.append([row[0] for row in presentations] == ["1", "2", "3"]
                          and len(state) == 1 and state[0][2:] == ("2", "0")
                          and int(state[0][1]) == 2
                          and replies == ([(0x8000D101, "Accepted"),
                                           (0x8000D102, "Unavailable")] if seat == 1 else []))
        synchronized = bool(signatures[0]) and all(row == signatures[0]
                                                    for row in signatures[1:])
        lifecycle_verified = all(checks)
        capture_complete = all(len(result["screenshots"]) >= 5 for result in results.values())
    elif metadata.get("ghost_ledger_case"):
        reverse = metadata["ghost_ledger_case"] == "contention-reverse"
        expected_replies = {
            1: [(0x8000C101, "Accepted"), (0x8000C102, "TargetAlreadyAffected"),
                (0x8000C103, "Accepted"), (0x8000C104, "Unavailable"),
                (0x8000C105, "Unavailable"), (0x8000C106, "Accepted"),
                (0x8000C107, "Unavailable")],
            3: [(0x8000C301, "TargetAlreadyAffected"), (0x8000C302, "Accepted"),
                (0x8000C303, "Accepted"), (0x8000C304, "Unavailable"),
                (0x8000C305, "Unavailable")],
        }
        if reverse:
            expected_replies = {
                1: [(0x8000E101, "TargetAlreadyAffected"),
                    (0x8000E102, "Accepted")],
                3: [(0x8000E301, "Accepted"),
                    (0x8000E302, "TargetAlreadyAffected"),
                    (0x8000E303, "Accepted")],
            }
        end_signatures = []
        checks = []
        for role in roles:
            log = read(output / f"{role}.log")
            assignment = re.search(r"\[VISUAL\] ASSIGNED local=(\d+)", log)
            seat = int(assignment.group(1)) if assignment else -1
            starts = re.findall(r"\[VISUAL\] LEDGER_START turn=(\d+) local=\d+ spent=(\d+) cd1=(\d+) cd3=(\d+)", log)
            ends = re.findall(r"\[VISUAL\] LEDGER_END turn=(\d+) local=\d+ damage=(\d+) spent=(\d+) possessed=(\d+)", log)
            replies = [(int(request_id), result) for request_id, result in
                       re.findall(r"\[VISUAL\] LEDGER_REPLY local=\d+ id=(\d+) result=(\w+)", log)]
            settled = re.findall(r"\[VISUAL\] LEDGER_CHECK local=\d+ seq=(\d+) turn=3 spent=(\d+) damage=(\d+)", log)
            if reverse:
                reverse_end = re.findall(r"\[VISUAL\] LEDGER_REVERSE_END local=\d+ damage=(\d+) spent=(\d+) possessed=(\d+)", log)
                reverse_check = re.findall(r"\[VISUAL\] LEDGER_REVERSE_CHECK local=\d+ seq=(\d+) damage=(\d+) spent=(\d+)", log)
                end_signatures.append(reverse_end)
                checks.append(reverse_end == [("1", "10", "5")]
                              and len(reverse_check) == 1 and reverse_check[0][1:] == ("1", "10")
                              and replies == expected_replies.get(seat, []))
            else:
                end_signatures.append(ends)
                checks.append(starts == [("1", "0", "0", "0"), ("2", "10", "1", "0"),
                                         ("3", "10", "0", "1")]
                              and ends == [("1", "1", "10", "5"), ("2", "1", "10", "0"),
                                           ("3", "4", "10", "0")]
                              and replies == expected_replies.get(seat, [])
                              and len(settled) == 1 and settled[0][1:] == ("10", "4"))
        synchronized = bool(end_signatures[0]) and all(row == end_signatures[0]
                                                         for row in end_signatures[1:])
        ledger_verified = all(checks)
        capture_complete = all(len(result["screenshots"]) >= (4 if reverse else 8)
                               for result in results.values())
    elif metadata.get("possession_case"):
        case = metadata["possession_case"]
        expected_suppressed = 0 if case == "minigame-failure" else 1
        signatures = []
        cost_signatures = []
        possession_checks = []
        for role in roles:
            log = read(output / f"{role}.log")
            batch = re.findall(r"\[VISUAL\] POSSESSION_BATCH case=(\S+) local=\d+ seq=(\d+) suppressed=(\d+) tempsStable=(\w+)", log)
            check = re.findall(r"\[VISUAL\] POSSESSION_CHECK case=(\S+) local=\d+ seq=(\d+) copy=(\d+) before=(\d+) after=(\d+) cue=(\w+)", log)
            cue = re.findall(r"\[VISUAL\] POSSESSION_CUE case=(\S+) local=\d+", log)
            marker = re.findall(r"\[VISUAL\] POSSESSION_MARKER case=(\S+) local=\d+", log)
            timing = re.findall(r"\[VISUAL\] POSSESSION_TIMING case=(\S+) local=\d+ seq=(\d+) elapsed=([\d.]+) cueOffset=([-\d.]+) attackOffset=([-\d.]+)", log)
            actions = re.findall(r"\[VISUAL\] POSSESSION_BATCH case=\S+ local=\d+ seq=\d+ suppressed=\d+ tempsStable=\w+ events=\d+ actions=(\d+)", log)
            ready = re.findall(r"\[VISUAL\] POSSESSION_READY case=(\S+) local=(\d+) selected=(\w+)", log)
            forced = re.findall(r"\[VISUAL\] POSSESSION_FORCE case=(\S+) local=(\d+) seq=(\d+)", log)
            removed = re.findall(r"\[VISUAL\] POSSESSION_RENDERER_REMOVED case=(\S+) local=(\d+) actor=(\d+)", log)
            ack_fault = re.findall(r"\[VISUAL\] POSSESSION_ACK_FAULT case=(\S+) local=(\d+) drop=(\w+)", log)
            ack_recovered = re.findall(r"\[VISUAL\] POSSESSION_ACK_RECOVERED case=(\S+) local=\d+ turn=(\d+)", log)
            disconnected = re.findall(r"\[VISUAL\] POSSESSION_DISCONNECT case=(\S+) local=(\d+) seq=(\d+)", log)
            assignment = re.search(r"\[VISUAL\] ASSIGNED local=(\d+)", log)
            local_seat = int(assignment.group(1)) if assignment else -1
            signatures.append(batch)
            if case != "attack-disconnect" or local_seat != 3:
                cost_signatures.append(check)
            if case == "attack-disconnect" and local_seat == 3:
                possession_checks.append(len(batch) == 1 and batch[0][0] == case
                                         and batch[0][2:] == ("1", "True")
                                         and disconnected == [(case, "3", batch[0][1])]
                                         and marker == [case])
                continue
            expected_actions = 0 if case in ("defense", "minigame-failure") else 1
            timing_ok = (len(timing) == 1 and timing[0][0] == case
                         and (float(timing[0][2]) <= 2.5 if expected_actions == 0
                              or (case == "attack-force" and local_seat == 2)
                              else float(timing[0][2]) >= 3.0))
            if case in ("defense-counter", "defense-counter-missing-renderer") and timing_ok:
                timing_ok = (float(timing[0][3]) >= 0
                             and float(timing[0][4]) >= float(timing[0][3]))
            possession_checks.append(len(batch) == 1 and len(check) == 1
                                     and batch[0][0] == case and check[0][0] == case
                                     and int(batch[0][2]) == expected_suppressed
                                     and batch[0][3] == ("False" if case in
                                         ("defense-counter", "defense-counter-missing-renderer") else "True")
                                     and check[0][1] == batch[0][1]
                                     and actions == [str(expected_actions)] and timing_ok
                                     and (forced == [(case, "2", batch[0][1])]
                                          if case == "attack-force" and local_seat == 2
                                          else not forced)
                                     and (removed == [(case, "2", "0")]
                                          if case == "defense-counter-missing-renderer" and local_seat == 2
                                          else not removed)
                                     and (ack_fault == [(case, "2", "True" if case == "attack-drop-ack" else "False")]
                                          if case in ("attack-drop-ack", "attack-late-ack") and local_seat == 2
                                          else not ack_fault)
                                     and (len(ack_recovered) == 1 and ack_recovered[0][0] == case
                                          and int(ack_recovered[0][1]) >= 2
                                          if case in ("attack-drop-ack", "attack-late-ack", "attack-disconnect")
                                          else not ack_recovered)
                                     and not disconnected
                                     and marker == [case]
                                     and (not ready if local_seat == 1 else len(ready) == 1)
                                     and (local_seat != 2 or
                                          ((case, "2", "False") in ready if case == "minigame-failure"
                                           else (case, "2", "True") in ready))
                                     and ((cue == [case]) if expected_suppressed else not cue))
        synchronized = (bool(signatures[0]) and all(rows == signatures[0] for rows in signatures[1:])
                        and bool(cost_signatures[0])
                        and all(rows == cost_signatures[0] for rows in cost_signatures[1:]))
        possession_verified = all(possession_checks)
        if case in ("attack-drop-ack", "attack-late-ack"):
            host_log = read(output / "host.log")
            possession_verified = (possession_verified
                                   and "[PresentationBarrier] Timeout after" in host_log)
            fault_log = next((read(output / f"{role}.log") for role in roles
                              if "[VISUAL] ASSIGNED local=2" in read(output / f"{role}.log")), "")
            possession_verified = (possession_verified
                                   and ("Development ACK dropped: seq=" in fault_log
                                        if case == "attack-drop-ack"
                                        else "Development delayed ACK sent: seq=" in fault_log))
        if case == "attack-disconnect":
            possession_verified = (possession_verified
                                   and "[PresentationBarrier] Disconnect: removed" in read(output / "host.log")
                                   and "[PresentationBarrier] Timeout after" not in read(output / "host.log"))
        capture_complete = all(len(result["screenshots"]) >=
                               (3 if case == "attack-disconnect" and
                                "[VISUAL] ASSIGNED local=3" in read(output / f"{role}.log") else 4)
                               for role, result in results.items())
    elif metadata.get("ghost_lethal_showcase"):
        skill_signatures = [re.findall(r"\[VISUAL\] GHOST_VFX_START (.+)", read(output / f"{role}.log"))
                            for role in roles]
        synchronized = (len(skill_signatures[0]) == 1
                        and all(rows == skill_signatures[0] for rows in skill_signatures[1:]))
        capture_complete = all(len(result["screenshots"]) >= 6 for result in results.values())
        order = []
        for role in roles:
            log = read(output / f"{role}.log")
            cast = re.search(r"\[VISUAL\] GHOST_VFX_START actor=1 skill=0 target=0", log)
            impact = re.search(r"\[VISUAL\] GHOST_ACTUAL_IMPACT cast=\d+", log) if cast else None
            death = re.search(r"\[PlayerVisual\] PlayDeathSequence START", log[impact.end():]) if impact else None
            result = re.search(r"\[MatchResult\] Visible seq=\d+ local=\d+ winners=2",
                               log[impact.end() + death.end():]) if death else None
            order.append(bool(cast and impact and cast.start() < impact.start() and death and result))
        lethal_order_verified = all(order)
    elif metadata.get("ghost_showcase"):
        skill_signatures = [re.findall(r"\[VISUAL\] GHOST_VFX_START (.+)", read(output / f"{role}.log"))
                            for role in roles]
        synchronized = (len(skill_signatures[0]) == 2
                        and all(rows == skill_signatures[0] for rows in skill_signatures[1:]))
        capture_complete = all(len(result["screenshots"]) >= 7 for result in results.values())
    else:
        checkpoint_signatures = []
        for result in results.values():
            checkpoint_signatures.append([re.sub(r" local=\d+", "", row) for row in result["checkpoints"]])
        synchronized = (bool(checkpoint_signatures[0])
                        and all(rows == checkpoint_signatures[0] for rows in checkpoint_signatures[1:]))
        # The limit counts presentations, including death-only presentations, not combat turns.
        # Validate actual frame records and every settled sequence instead of an invented 2x ratio.
        capture_complete = all(complete_presentation_captures(output, result) for result in results.values())
    relay_verified = None
    if metadata.get("transport") == "unity-relay":
        player_ids = [result["authenticated_player_id"] for result in results.values()]
        relay_verified = (all(player_ids) and len(set(player_ids)) == 4
                          and results["host"]["relay_allocation"]
                          and all(results[role]["relay_join"] for role in roles[1:])
                          and all(result["relay_connected"] for result in results.values()))
    cosmetic_verified = None
    if metadata.get("cosmetic_check"):
        signatures = [re.findall(r"\[COSMETIC\] VERIFIED (.+)", read(output / f"{role}.log")) for role in roles]
        cosmetic_verified = len(signatures[0]) == 1 and all(value == signatures[0] for value in signatures[1:])
    terminal_visual = terminal_pixels(output) if metadata.get("ghost_lethal_showcase") else None
    ordinary_flow = not any(metadata.get(key) for key in (
        "ghost_showcase", "ghost_lethal_showcase", "possession_case", "ghost_ledger_case",
        "ghost_lifecycle_case", "mini_ticket_case", "full_match", "full_natural"))
    target_capture_verified = None
    if ordinary_flow:
        target_capture_verified = all(
            len(re.findall(r"\[VISUAL\] TARGET_HOVER_CAPTURE .+ direction=" + direction + r" visible=True",
                           read(output / f"{role}.log"))) == 1
            and sum(f"turn-01-target-{direction}-" in name for name in result["screenshots"]) == 1
            for role, result in results.items() for direction in ("west", "north", "east"))
    passed = (runner_error is None and len(results) == 4 and synchronized and capture_complete and relay_verified is not False
              and (terminal_visual is None or terminal_visual["passed"])
              and target_capture_verified is not False
              and cosmetic_verified is not False
              and possession_verified is not False
              and mini_ticket_verified is not False
              and full_match_verified is not False
              and ledger_verified is not False
              and lifecycle_verified is not False
              and lethal_order_verified is not False
              and all(result["exit_code"] == 0 and len(result["pass"]) == 1 and not result["errors"]
                      for result in results.values()))
    report = {"passed": passed, "runner_error": runner_error, "checkpoints_synchronized": synchronized,
              "capture_complete": capture_complete, "relay_verified": relay_verified,
              "terminal_visual": terminal_visual,
              "target_capture_verified": target_capture_verified,
              "visual_review": "required: inspect rendered captures separately",
              "cosmetic_verified": cosmetic_verified,
              "lethal_order_verified": lethal_order_verified,
              "possession_verified": possession_verified, "mini_ticket_verified": mini_ticket_verified,
              "full_match_verified": full_match_verified,
              "ledger_verified": ledger_verified,
              "lifecycle_verified": lifecycle_verified,
              "metadata": metadata, "players": results}
    (output / "report.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    return report


def write_gallery(output, report):
    rows = []
    for role, result in report["players"].items():
        images = []
        for name in result["screenshots"]:
            images.append(f'<figure><img src="{html.escape(name)}"><figcaption>{html.escape(name)}</figcaption></figure>')
        plans = "\n".join(result["plans"]) or "No recorded plan"
        errors = "\n".join(result["errors"]) or "None"
        findings = "\n".join(f"{name}: {count}" for name, count in result["findings"].items() if count) or "None"
        rows.append(f"<section><h2>{html.escape(role)}</h2><pre>{html.escape(plans)}</pre>"
                    f"<h3>Detected errors</h3><pre>{html.escape(errors)}</pre><h3>Findings</h3><pre>{html.escape(findings)}</pre>"
                    f"<div class='shots'>{''.join(images)}</div></section>")
    page = f"""<!doctype html><html><head><meta charset='utf-8'><title>Absolute Zero 4P Visual Debug</title>
<style>body{{font-family:Segoe UI,sans-serif;background:#10151d;color:#e7edf5;margin:24px}} .status{{padding:14px;background:#1c2633;border-radius:8px}}
section{{margin-top:28px;border-top:1px solid #415064}} pre{{white-space:pre-wrap;background:#18212c;padding:12px}} .shots{{display:grid;grid-template-columns:1fr 1fr;gap:12px}}
figure{{margin:0;background:#18212c;padding:8px}} img{{width:100%;height:auto;display:block}} figcaption{{font-size:12px;margin-top:6px;word-break:break-all}}</style></head>
<body><h1>Absolute Zero — Four-player visual debug</h1><div class='status'>Automated state checks: <b>{'PASS' if report['passed'] else 'FAIL'}</b><br>Visual review: required separately; image presence does not prove UI correctness.<br>
Seed: {report['metadata']['seed']} | Requested presentation limit: {report['metadata']['turns']} | Run: {html.escape(report['metadata']['run'])}</div>{''.join(rows)}</body></html>"""
    (output / "gallery.html").write_text(page, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("executable", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--seed", type=int, default=29031)
    parser.add_argument("--turns", type=int, choices=range(1, 21), default=4,
                        help="Ordinary-flow presentation limit, including death-only sequences (legacy option name)")
    parser.add_argument("--port", type=int, default=17859)
    parser.add_argument("--timeout", type=int, default=320)
    parser.add_argument("--ghost-showcase", action="store_true")
    parser.add_argument("--ghost-lethal-showcase", action="store_true")
    parser.add_argument("--possession-case", choices=["attack", "attack-force", "attack-drop-ack",
                        "attack-late-ack", "attack-disconnect", "heal", "defense", "defense-counter",
                        "defense-counter-missing-renderer",
                        "multi-use", "unlimited", "minigame-success", "minigame-failure"])
    parser.add_argument("--ghost-ledger-case", choices=["contention", "contention-reverse"])
    parser.add_argument("--ghost-lifecycle-case", choices=["round-revival"])
    parser.add_argument("--mini-ticket-case", choices=["replacement", "replacement-topup", "round"])
    parser.add_argument("--full-match", action="store_true")
    parser.add_argument("--full-natural", action="store_true")
    parser.add_argument("--relay", action="store_true")
    parser.add_argument("--resource-probe", action="store_true")
    parser.add_argument("--cosmetic-check", action="store_true")
    parser.add_argument("--top-check", type=int, choices=[0, 3],
                        help="Also equip four incoming tops, starting at this zero-based catalog offset")
    args = parser.parse_args()
    if args.top_check is not None:
        args.cosmetic_check = True
    args.output.mkdir(parents=True, exist_ok=False)
    executable = args.executable.resolve()
    if not executable.exists():
        parser.error(f"Executable does not exist: {executable}")

    token = uuid.uuid4().hex
    metadata = {"run": token, "seed": args.seed, "turns": args.turns, "port": args.port,
                "transport": "unity-relay" if args.relay else "local-utp", "top_check": args.top_check,
                "ghost_showcase": args.ghost_showcase or args.ghost_lethal_showcase,
                "ghost_lethal_showcase": args.ghost_lethal_showcase,
                "possession_case": args.possession_case,
                "ghost_ledger_case": args.ghost_ledger_case,
                "ghost_lifecycle_case": args.ghost_lifecycle_case,
                "mini_ticket_case": args.mini_ticket_case,
                "full_match": args.full_match or args.full_natural,
                "full_natural": args.full_natural,
                "resource_probe": args.resource_probe,
                "cosmetic_check": args.cosmetic_check,
                "ordinary_presentation_limit": 4 if args.full_match or args.full_natural else args.turns,
                "executable": str(executable)}
    (args.output / "scenario.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    processes = {}
    placement = {}
    runner_error = None
    positions = {"host": (0, 0), "client1": (960, 0), "client2": (0, 540), "client3": (960, 540)}
    try:
        for index, role in enumerate(positions):
            profile_suffix = token[:8]
            services_profile = f"azr-{'h' if role == 'host' else role[-1]}-{profile_suffix}"
            command = [str(executable), "-screen-fullscreen", "0", "-force-d3d11",
                       "-screen-width", "960", "-screen-height", "540",
                       "--az-visual-flow", "1", "--az-role", "host" if role == "host" else "client",
                       "--az-run", token, "--az-seed", str(args.seed), "--az-turns", str(args.turns),
                       "--az-timeout", str(args.timeout),
                       "--az-port", str(args.port), "-logFile", str((args.output / f"{role}.log").resolve())]
            if args.relay:
                command.extend(["--az-relay-flow", "1", "--az-services-profile", services_profile,
                                "--az-coordination-file", str((args.output / "lobby-code.tmpdata").resolve())])
            if args.ghost_showcase:
                command.extend(["--az-ghost-showcase", "1"])
            if args.ghost_lethal_showcase:
                command.extend(["--az-ghost-lethal-showcase", "1"])
            if args.possession_case:
                command.extend(["--az-possession-case", args.possession_case])
            if args.ghost_ledger_case:
                command.extend(["--az-ghost-ledger-case", args.ghost_ledger_case])
            if args.ghost_lifecycle_case:
                command.extend(["--az-ghost-lifecycle-case", args.ghost_lifecycle_case])
            if args.mini_ticket_case:
                command.extend(["--az-mini-ticket-case", args.mini_ticket_case])
            if args.full_match:
                command.extend(["--az-full-match", "1"])
            if args.resource_probe:
                command.extend(["--az-resource-probe", "observe"])
            if args.cosmetic_check:
                command.extend(["--az-cosmetic-id", f"hat_0{index + 1}"])
            if args.top_check is not None:
                command.extend(["--az-cosmetic-top", f"top_{args.top_check + index + 1:02d}"])
            if args.full_natural:
                command.extend(["--az-full-natural", "1"])
            processes[role] = subprocess.Popen(command)
            placement[role] = place_window(processes[role], *positions[role])
            if role == "host":
                deadline = time.monotonic() + 35
                while time.monotonic() < deadline:
                    ready_marker = "[VISUAL] RELAY_LOBBY_READY" if args.relay else "[VISUAL] HOST_LISTENING"
                    if ready_marker in read(args.output / "host.log"):
                        break
                    if processes[role].poll() is not None:
                        raise RuntimeError("Host exited before listening")
                    time.sleep(0.5)
                else:
                    raise TimeoutError("Host did not listen within 35 seconds")
            time.sleep(0.4 if index else 0.8)

        deadline = time.monotonic() + args.timeout
        while time.monotonic() < deadline and any(process.poll() is None for process in processes.values()):
            for role, process in processes.items():
                log = read(args.output / f"{role}.log")
                if "[VISUAL] FAIL" in log or (process.poll() not in (None, 0)):
                    runner_error = f"{role} failed during the scenario"
                    break
            if runner_error:
                break
            time.sleep(1)
        if any(process.poll() is None for process in processes.values()) and not runner_error:
            runner_error = f"Scenario exceeded {args.timeout} seconds"
    except Exception as error:
        runner_error = str(error)
    finally:
        for process in processes.values():
            if process.poll() is None:
                process.terminate()
        for process in processes.values():
            try:
                process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)
        coordination_file = args.output / "lobby-code.tmpdata"
        if coordination_file.exists():
            coordination_file.unlink()

    report = build_report(args.output, metadata, processes, placement, runner_error)
    write_gallery(args.output, report)
    print(json.dumps(report, indent=2, ensure_ascii=False))
    raise SystemExit(0 if report["passed"] else 1)


if __name__ == "__main__":
    main()
