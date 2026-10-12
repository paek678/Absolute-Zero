"""Sequential post-pilot visual/lifecycle/authority qualification; never primary games."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('exe', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--pilot-root', type=Path, required=True)
    parser.add_argument('--diagnose-censored-pilot', action='store_true',
                        help='Run independent diagnostics after reviewed natural-terminal timeouts; does not qualify the pilot')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    deadline = time.monotonic() + 7200
    while True:
        reports = list(args.pilot_root.glob('pilot-*/report.json'))
        failed = [json.loads(p.read_text()) for p in reports if json.loads(p.read_text()).get('passed') is not True]
        if failed:
            timeout_only = all(r.get('peers') and all(
                p.get('summary', {}).get('failure') == 'TimeoutException: natural match terminal'
                and p.get('summary', {}).get('healthyTelemetry') is True
                and not p.get('invariants', {}).get('problems') for p in r['peers']) for r in failed)
            if not args.diagnose_censored_pilot or not timeout_only:
                raise SystemExit('Pilot failure requires review before qualification')
        if len(reports) == 8: break
        if time.monotonic() > deadline: raise SystemExit('Pilot reports incomplete')
        time.sleep(3)
    exe = str(args.exe.resolve())
    scripts = Path(__file__).resolve().parent
    output = args.output.resolve()
    cases = [
        ('framebuffer-solo', 'run_full_match_playtest.py', ['--exe', exe, '--output', str(output/'framebuffer-solo'),
          '--players','1','--policies','attacker','--seed','66101','--gameplay-seed','66101','--timeout','600','--visible']),
        ('ghost-contention','run_visual_four_player.py',[exe,str(output/'ghost-contention'),'--ghost-ledger-case','contention','--relay']),
        ('ghost-round-revival','run_visual_four_player.py',[exe,str(output/'ghost-round-revival'),'--ghost-lifecycle-case','round-revival','--relay']),
        ('possession-attack','run_visual_four_player.py',[exe,str(output/'possession-attack'),'--possession-case','attack','--relay']),
        ('possession-multi-use','run_visual_four_player.py',[exe,str(output/'possession-multi-use'),'--possession-case','multi-use','--relay']),
        ('mini-ticket-replacement','run_visual_four_player.py',[exe,str(output/'mini-ticket-replacement'),'--mini-ticket-case','replacement','--relay']),
        ('mini-ticket-round','run_visual_four_player.py',[exe,str(output/'mini-ticket-round'),'--mini-ticket-case','round','--relay']),
        ('impaired-input','run_validation_matrix.py',[exe,str(output/'impaired-input'),'--filter','input','--count','4',
           '--workers','1','--proxy','--delay-ms','100','--loss','0.02','--port-base','25210']),
        ('services','run_validation_matrix.py',[exe,str(output/'services'),'--filter','services','--count','4','--workers','1','--port-base','25230']),
    ]
    results = []
    for name, script, arguments in cases:
        folder = output/name
        if folder.exists(): raise SystemExit('Existing qualification evidence requires review: '+str(folder))
        print('Starting qualification '+name, flush=True)
        completed = subprocess.run([sys.executable, str(scripts/script), *arguments], check=False)
        reports = list(folder.rglob('report.json')) if folder.exists() else []
        passed = completed.returncode == 0 and bool(reports) and all(json.loads(p.read_text()).get('passed') is True for p in reports)
        results.append(dict(case=name, passed=passed, exit=completed.returncode, primary_counted=False,
                            pilot_gate_passed=not bool(failed)))
        (output/'progress.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
        if not passed: raise SystemExit('Qualification failed; inspect retained evidence before continuing')
    summary = dict(passed=True,cases=len(results),primary_counted=False,pilot_gate_passed=not bool(failed))
    (output/'report.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
    print(json.dumps(summary),flush=True)


if __name__ == '__main__': main()
