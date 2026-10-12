"""Continue sequentially after diagnostics; preserve the censored pilot and stop on new failures."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time
from types import SimpleNamespace
from run_full_match_playtest import run


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('exe', type=Path)
    parser.add_argument('root', type=Path)
    args = parser.parse_args()
    root = args.root.resolve()
    report = root/'lifecycle-extra-02'/'report.json'
    deadline = time.monotonic()+1800
    while not report.exists():
        progress = root/'lifecycle-extra-02'/'progress.json'
        if progress.exists() and any(x.get('passed') is not True for x in json.loads(progress.read_text())):
            raise SystemExit('Lifecycle failure; remaining campaign not started')
        if time.monotonic() > deadline: raise SystemExit('Lifecycle evidence did not finish')
        time.sleep(3)
    for required in (report, root/'qualification'/'report.json'):
        if json.loads(required.read_text()).get('passed') is not True:
            raise SystemExit('Independent qualification did not pass')
    followup = root/'pilot-followup'/'pilot-008-p4'
    if followup.exists(): raise SystemExit('Follow-up evidence already exists; review before resuming')
    print('Starting retained-pilot follow-up, 2700-second watchdog; no gameplay rule changes', flush=True)
    code = run(SimpleNamespace(exe=args.exe, output=followup, players=4,
               policies=['attacker','survival','resource','utility'], seed=675102,
               gameplay_seed=665102, timeout=2700, behavior='normal', cohort='pilot'))
    if code: raise SystemExit('Follow-up failed; primary campaign not started')
    scripts = Path(__file__).resolve().parent
    command = [sys.executable, str(scripts/'plan066_campaign.py'), str(args.exe), str(root/'primary-v2'),
               '--stage','primary','--execute','--pilot-root',str(root/'pilot'),
               '--pilot-followup-root',str(root/'pilot-followup'),'--calibrated-multi-budget']
    raise SystemExit(subprocess.run(command,check=False).returncode)


if __name__ == '__main__': main()
