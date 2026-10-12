import argparse, hashlib, json
from pathlib import Path
from types import SimpleNamespace
from run_full_match_playtest import run, digest
from analyze_balance_playtest import analyze_run

parser=argparse.ArgumentParser()
parser.add_argument('exe',type=Path);parser.add_argument('output',type=Path)
parser.add_argument('--only',nargs='*')
args=parser.parse_args()
rows=[
 dict(id='solo-ready125',players=1,policies=['attacker'],ready_delay=1.25,timeout=600),
 dict(id='solo-ready500',players=1,policies=['attacker'],ready_delay=5,timeout=600),
 dict(id='duel',players=2,policies=['attacker','survival'],ready_delay=None,timeout=900),
 dict(id='multi3',players=3,policies=['attacker','survival','utility'],ready_delay=None,timeout=2700),
 dict(id='multi4',players=4,policies=['attacker','survival','resource','utility'],ready_delay=None,timeout=2700),
]
args.output.mkdir(parents=True,exist_ok=True)
plan=args.output/'scenarios.json'
if plan.exists() and json.loads(plan.read_text())!=rows:raise SystemExit('Scenario drift; use a new root')
plan.write_text(json.dumps(rows,indent=2),encoding='utf-8')
expected_hashes={str(p.relative_to(args.exe.parent)):digest(p) for p in args.exe.parent.rglob('*')
 if p.is_file() and (p.name==args.exe.name or p.name.startswith('AbsoluteZero.') and p.suffix=='.dll')}
for existing in args.output.glob('*/manifest.json'):
 if json.loads(existing.read_text(encoding='utf-8')).get('build_hashes')!=expected_hashes:
  raise SystemExit('Existing cohort uses a different executable; use a new output root')
progress=[]
for row in rows:
 if args.only and row['id'] not in args.only:continue
 folder=args.output/row['id']
 if folder.exists():raise SystemExit('Existing evidence retained; use --only for untouched scenarios or a new root')
 code=run(SimpleNamespace(exe=args.exe,output=folder,players=row['players'],policies=row['policies'],
  seed=677101,gameplay_seed=667101,timeout=row['timeout'],behavior='normal',cohort='balance-validation',
  ready_delay=row['ready_delay'],visible=False))
 result=analyze_run(folder)
 progress.append(dict(id=row['id'],gameplay_passed=code==0,balance_passed=result['passed'],
  errors=result['errors'],actions=result.get('actions',0),impacts=result.get('impacts',0),
  unattributed_impacts=result.get('unattributed_impacts'),seats=result.get('seats')))
 (args.output/'progress.json').write_text(json.dumps(progress,indent=2),encoding='utf-8')
 print(json.dumps(progress[-1]),flush=True)
 if code or not result['passed']:raise SystemExit('Stopped at new gameplay or observation failure; retain evidence')
all_results=[]
for row in rows:
 folder=args.output/row['id']
 if not (folder/'report.json').exists():raise SystemExit('Subset completed; full cohort remains incomplete')
 manifest=json.loads((folder/'manifest.json').read_text(encoding='utf-8'))
 if manifest['build_hashes']!=expected_hashes:raise SystemExit('Mixed-build cohort cannot pass')
 result=analyze_run(folder)
 all_results.append(dict(id=row['id'],passed=result['passed'],errors=result['errors']))
 if not result['passed']:raise SystemExit('Existing attempted scenario still needs review')
(args.output/'report.json').write_text(json.dumps({'passed':True,'cases':all_results,'primary_counted':False},indent=2),encoding='utf-8')
