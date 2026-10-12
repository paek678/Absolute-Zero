import argparse,json,time
from pathlib import Path
from run_validation_matrix import run_case
from analyze_balance_playtest import analyze
from run_full_match_playtest import digest
p=argparse.ArgumentParser();p.add_argument('exe',type=Path);p.add_argument('root',type=Path);a=p.parse_args()
ready=a.root/'runtime/report.json';deadline=time.monotonic()+9000
while not ready.exists():
 progress=a.root/'runtime/progress.json'
 if progress.exists():
  try:rows=json.loads(progress.read_text())
  except ValueError:rows=[]
  if any(not r['gameplay_passed'] or not r['balance_passed'] for r in rows):raise SystemExit('Normal cohort requires review before supplemental tests')
 if time.monotonic()>deadline:raise SystemExit('Normal cohort incomplete')
 time.sleep(3)
results=[]
for count in (2,4):
 root=a.root/'delayed-fixtures';root.mkdir(exist_ok=True)
 report=run_case(a.exe.resolve(),root.resolve(),('item-delay',count,0),26710+count,relay=True,record_telemetry=True)
 folder=root/f'item-delay-{count}-w0';path=folder/'host-trace/events.jsonl'
 if not report['passed'] or not path.exists():raise SystemExit('Delayed fixture failed or host trace not finalized')
 events=[json.loads(x) for x in path.read_text(encoding='utf-8').splitlines()]
 balance=analyze(events)
 delayed=[e for e in events if e.get('kind')=='balance_impact' and (e.get('balance') or {}).get('reason')=='scheduled']
 passed=balance['passed'] and len(delayed)==3 and all(e['balance']['action']['id']>0 for e in delayed)
 evidence=dict(passed=passed,players=count,forced_fixture=True,primary_counted=False,gameplay_passed=report['passed'],
               delayed_impacts=len(delayed),observation=balance,
               assembly_sha256=digest(a.exe.parent/'AbsoluteZeroVisual4P_Data/Managed/AbsoluteZero.Core.dll'))
 (folder/'balance.json').write_text(json.dumps(evidence,indent=2),encoding='utf-8')
 results.append(dict(players=count,passed=passed,delayed_impacts=len(delayed),errors=balance['errors']))
 (root/'progress.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
 print(json.dumps(results[-1]),flush=True)
 if not passed:raise SystemExit('Delayed attribution verification failed')
(root/'report.json').write_text(json.dumps(dict(passed=True,cases=results,primary_counted=False),indent=2),encoding='utf-8')
