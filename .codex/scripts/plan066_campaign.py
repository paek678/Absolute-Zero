"""Frozen, sequential PLAN066 scenario construction and resumable execution."""
import argparse
import json
import time
from pathlib import Path
from types import SimpleNamespace
from run_full_match_playtest import run, digest


def verify_attempt(folder, row, stage, build_hashes):
    """Do not resume/count a differently configured executable or scenario."""
    manifest = json.loads((folder / 'manifest.json').read_text())
    expected = {key: row[key] for key in ('players', 'policies', 'seed', 'gameplay_seed', 'timeout')}
    expected.update(kind=stage, behavior='normal', build_hashes=build_hashes)
    if any(manifest.get(key) != value for key, value in expected.items()):
        raise ValueError(f'Attempt manifest/build mismatch: {folder}')
    report = json.loads((folder / 'report.json').read_text())
    if report.get('cohort') != stage:
        raise ValueError(f'Attempt cohort mismatch: {folder}')
    return report


def qualify_pilots(root, followup_root, build_hashes):
    """Keep censored originals, and accept only an explicit bounded same-build follow-up."""
    qualified = []
    allowed = {'TimeoutException: natural match terminal', 'completed false/missing',
               'lobby return not verified', 'missing/duplicate terminal checkpoint',
               'visibleResult false/missing'}
    for row in scenarios('pilot'):
        original = verify_attempt(root/row['id'], row, 'pilot', build_hashes)
        chosen = root/row['id']
        censored = original.get('passed') is not True
        if censored:
            peers = original.get('peers') or []
            reviewed_timeout = peers and all(
                p.get('summary', {}).get('failure') == 'TimeoutException: natural match terminal'
                and p.get('summary', {}).get('healthyTelemetry') is True
                and not p.get('invariants', {}).get('problems')
                and set(p.get('problems', [])) <= allowed for p in peers)
            if followup_root is None or not reviewed_timeout:
                raise ValueError('Pilot incomplete/failed; no qualified follow-up: '+row['id'])
            chosen = followup_root/row['id']
            retry_row = dict(row, timeout=2700)
            retry = verify_attempt(chosen, retry_row, 'pilot', build_hashes)
            if retry.get('passed') is not True:
                raise ValueError('Pilot follow-up did not pass: '+row['id'])
        qualified.append(dict(scenario=row['id'], original=str(root/row['id']),
                              original_censored=censored, qualified_evidence=str(chosen)))
    return qualified


def scenarios(stage):
    rows = []
    def add(players, policies, seed, rotation=0):
        policies = policies[rotation:] + policies[:rotation]
        rows.append(dict(id=f'{stage}-{len(rows)+1:03d}-p{players}', players=players,
                         policies=policies, seed=seed + 10000, gameplay_seed=seed,
                         rotation=rotation, timeout={1:600, 2:900, 3:1800, 4:1500}[players]))
    if stage == 'pilot':
        for players, policies in [(1,['attacker']), (2,['attacker','survival']),
                                  (3,['attacker','survival','utility']),
                                  (4,['attacker','survival','resource','utility'])]:
            for seed in (665101, 665102): add(players, policies, seed)
    else:
        for policy in ('attacker', 'survival', 'resource', 'cautious'):
            for seed in range(666101,666105): add(1,[policy],seed)
        for policies in [['balanced','balanced'], ['attacker','survival'], ['resource','utility'], ['cautious','balanced']]:
            for seed in (666101,666102):
                for rotation in range(2): add(2,policies,seed,rotation)
        for players, lineups in [(3,[['balanced']*3,['attacker','survival','utility']]),
                                  (4,[['balanced']*4,['attacker','survival','resource','utility']])]:
            for policies in lineups:
                for seed in (666101,666102):
                    for rotation in range(players): add(players,policies,seed,rotation)
    return rows


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('exe',type=Path)
    parser.add_argument('output',type=Path)
    parser.add_argument('--stage',choices=['pilot','primary'],default='pilot')
    parser.add_argument('--execute',action='store_true')
    parser.add_argument('--pilot-root',type=Path)
    parser.add_argument('--pilot-followup-root',type=Path,
                        help='Explicit same-build 2700-second follow-ups for reviewed censored pilots; originals remain failed')
    parser.add_argument('--calibrated-multi-budget',action='store_true',
                        help='Use the post-pilot 2700-second Multi watchdog; use a new campaign output root')
    parser.add_argument('--limit',type=int,default=90)
    parser.add_argument('--prerequisite',type=Path,help='Wait for this active qualification report and require PASS before starting')
    args = parser.parse_args()
    args.output.mkdir(parents=True,exist_ok=True)
    rows = scenarios(args.stage)
    if args.calibrated_multi_budget:
        if args.stage != 'primary': raise SystemExit('Calibrated budget is only for a new primary cohort')
        for row in rows:
            if row['players'] >= 3: row['timeout'] = 2700
    path = args.output/'scenario-manifest.json'
    if path.exists() and json.loads(path.read_text()) != rows:
        raise SystemExit('Existing scenario manifest differs; never replace evidence silently')
    path.write_text(json.dumps(rows,indent=2),encoding='utf-8')
    if not args.execute:
        print(json.dumps(dict(stage=args.stage,count=len(rows),executed=0)));raise SystemExit(0)
    if args.prerequisite:
        deadline = time.monotonic() + 2400
        while not args.prerequisite.exists():
            if time.monotonic() >= deadline: raise SystemExit('Qualification report did not finish; no campaign started')
            time.sleep(2)
        if json.loads(args.prerequisite.read_text()).get('passed') is not True:
            raise SystemExit('Qualification failed; no campaign started')
    exe = args.exe.resolve()
    hashes = {str(p.relative_to(exe.parent)): digest(p) for p in exe.parent.rglob('*')
              if p.is_file() and (p.name == exe.name or p.name.startswith('AbsoluteZero.') and p.suffix == '.dll')}
    if args.stage == 'primary':
        if args.pilot_root is None: raise SystemExit('Eight qualified pilot reports required')
        qualified = qualify_pilots(args.pilot_root, args.pilot_followup_root, hashes)
        (args.output/'pilot-qualification.json').write_text(json.dumps(qualified,indent=2),encoding='utf-8')
    results = []
    for row in rows[:args.limit]:
        folder = args.output / row['id']
        result_path = folder/'report.json'
        if folder.exists() and not result_path.exists():
            raise SystemExit(f'Interrupted evidence requires review: {folder}')
        if not result_path.exists():
            code = run(SimpleNamespace(exe=args.exe,output=folder,players=row['players'],policies=row['policies'],
                                       seed=row['seed'],gameplay_seed=row['gameplay_seed'],timeout=row['timeout'],
                                       behavior='normal',cohort=args.stage))
        report = verify_attempt(folder, row, args.stage, hashes)
        results.append(dict(id=row['id'],passed=report['passed'],seconds=report['seconds']))
        (args.output/'campaign-progress.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
        if not report['passed']:
            raise SystemExit('Stopped at failed/censored attempt for investigation; no replacement counted')
