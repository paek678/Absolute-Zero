"""Extra current-build lifecycle and grant-order fixtures; never primary matches."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time
import uuid
from run_full_match_playtest import digest


def run_transitions(exe, folder):
    """Equivalent to run_solo_acceptance.ps1 transitions, without changing shell policy."""
    folder.mkdir()
    processes = []
    token = uuid.uuid4().hex[:10]
    timed_out = False
    try:
        for peer in range(4):
            startup = subprocess.STARTUPINFO()
            startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            startup.wShowWindow = 0
            command = [exe, '-screen-width', '1280', '-screen-height', '720', '-screen-fullscreen', '0',
                       '-force-d3d11', '--plan036-output', str(folder), '-logFile', str(folder/f'peer{peer}.log'),
                       '--plan036-mode-transitions', '--plan036-peer', str(peer),
                       '--az-services-profile', f'azp36-{peer}-{token}']
            processes.append(subprocess.Popen(command, startupinfo=startup))
        (folder/'processes.json').write_text(json.dumps([p.pid for p in processes]))
        deadline = time.monotonic()+720
        while any(p.poll() is None for p in processes):
            if time.monotonic() > deadline:
                timed_out = True
                break
            if any(p.poll() not in (None, 0) for p in processes): break
            time.sleep(1)
    finally:
        for process in processes:
            if process.poll() is None: process.terminate()
            try: process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)
    reports = [json.loads(p.read_text(encoding='utf-8-sig')) for p in folder.glob('*report.json')]
    passed = (not timed_out and len(reports) == 4 and all(r.get('passed') is True for r in reports)
              and all(p.returncode == 0 for p in processes))
    (folder/'summary.json').write_text(json.dumps(dict(passed=passed, reports=len(reports),
        timed_out=timed_out, exits=[p.returncode for p in processes], primary_counted=False),indent=2))
    return 0 if passed else 1


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('exe', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    output = args.output.resolve()
    scripts = Path(__file__).resolve().parent
    exe = str(args.exe.resolve())
    exe_path = args.exe.resolve()
    hashes = {str(p.relative_to(exe_path.parent)):digest(p) for p in exe_path.parent.rglob('*')
              if p.is_file() and (p == exe_path or p.name.startswith('AbsoluteZero.') and p.suffix == '.dll')}
    (output/'manifest.json').write_text(json.dumps(dict(kind='boundary-fixtures',build_hashes=hashes),indent=2))
    cases = [('solo-transitions', None)]
    for variant in (6, 7, 8, 9):
        name = f'topup-order-{variant}'
        delay = '--delay-view-ms' if variant < 8 else '--delay-descriptor-ms'
        cases.append((name, [sys.executable, str(scripts/'run_validation_matrix.py'), exe,
                     str(output/name), '--filter', 'topup-transition', '--variant', str(variant),
                     '--count', '4', '--workers', '1', '--relay', delay, '2000']))
    results = []
    for name, command in cases:
        print('Starting lifecycle qualification '+name, flush=True)
        code = run_transitions(exe, output/name) if command is None else subprocess.run(command, check=False).returncode
        folder = output/name
        reports = ([folder/'summary.json'] if name == 'solo-transitions'
                   else list(folder.rglob('report.json')))
        passed = (code == 0 and bool(reports)
                  and all(p.exists() and json.loads(p.read_text(encoding='utf-8-sig')).get('passed') is True
                          for p in reports))
        results.append(dict(case=name, passed=passed, primary_counted=False))
        (output/'progress.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
        if not passed:
            raise SystemExit('Lifecycle qualification failed; inspect retained evidence')
    (output/'report.json').write_text(json.dumps(dict(passed=True, cases=results,
                                                   primary_counted=False), indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
