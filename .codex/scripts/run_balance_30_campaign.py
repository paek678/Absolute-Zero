"""Frozen, sequential full-match campaign with incremental Excel/JSON export."""
import argparse
import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path
from types import SimpleNamespace
from run_full_match_playtest import run, digest
from analyze_balance_playtest import analyze_run
from balance_campaign_evidence import collect_case, export, load, save


def scenarios():
    rows=[]
    solo=['balanced','balanced','attacker','attacker','survival','survival','utility','utility','cautious','cautious']
    duel=[['balanced','balanced'],['balanced','balanced'],['attacker','survival'],['survival','attacker'],
        ['resource','utility'],['utility','resource'],['attacker','cautious'],['cautious','attacker'],
        ['survival','utility'],['utility','survival']]
    multi=[['attacker','survival','resource','utility'],['utility','attacker','survival','resource'],
        ['resource','utility','attacker','survival'],['survival','resource','utility','attacker'],
        ['attacker','attacker','utility','resource'],['utility','resource','attacker','attacker'],
        ['survival','utility','survival','resource'],['survival','resource','survival','utility'],
        ['cautious','attacker','utility','resource'],['utility','resource','cautious','attacker']]
    for mode,prefix,players,lineups,timeout in [('Solo','S',1,[[p] for p in solo],900),
        ('Duel','D',2,duel,1200),('Multi','M',4,multi,2400)]:
        for i,policies in enumerate(lineups):
            group=i if mode=='Solo' or i<2 and mode=='Duel' else i//2 if mode=='Duel' else 0 if i<4 else 1+(i-4)//2
            rows.append(dict(id=f'{prefix}{i+1:02}',mode=mode,players=players,policies=policies,
                seed=713000+players*100+group*10,gameplay_seed=813000+players*100+group,
                timeout=timeout,visible=i==0))
    return rows


def main():
    parser=argparse.ArgumentParser();parser.add_argument('exe',type=Path);parser.add_argument('output',type=Path)
    parser.add_argument('--limit',type=int)
    parser.add_argument('--seed-offset',type=int,default=0)
    parser.add_argument('--interleave',action='store_true',help='Solo/Duel/Multi rotation; ten cases each')
    parser.add_argument('--json-only',action='store_true',help='Retain all observations in JSON without creating an Excel workbook')
    args=parser.parse_args()
    if args.limit is not None and not 1<=args.limit<=30:parser.error('--limit must be 1..30')
    root=args.output.resolve();root.mkdir(parents=True,exist_ok=True);cases=scenarios()
    for case in cases:
        case['seed']+=args.seed_offset;case['gameplay_seed']+=args.seed_offset
    if args.interleave:
        cases=sorted(cases,key=lambda c:(int(c['id'][1:]),{'Solo':0,'Duel':1,'Multi':2}[c['mode']]))
    def write_progress(**state):
        save(root/'progress.json',dict(planned=30,runner_pid=os.getpid(),
            updated_at_utc=datetime.now(timezone.utc).isoformat(),**state))
    def write_evidence(records,status):
        return export(root,records,cases,status,write_workbook=not args.json_only)
    hashes={str(p.relative_to(args.exe.parent)):digest(p) for p in args.exe.parent.rglob('*')
        if p.is_file() and (p==args.exe or p.name.startswith('AbsoluteZero.') and p.suffix=='.dll')}
    manifest=dict(schema=1,cases=cases,build_hashes=hashes,runner_sha256=digest(Path(__file__)),
        balance_values_changed=False,interpretation='deferred to Astra',time_scale=1,
        seed_offset=args.seed_offset,interleaved=args.interleave,json_only=args.json_only,
        policy_pairing='seat-seeded sensitivity comparison; not policy-identity matched causal experiment')
    path=root/'campaign-manifest.json'
    if path.exists() and load(path)!=manifest:raise SystemExit('Frozen cohort drift; preserve evidence and use a new root')
    save(path,manifest);records=[]
    write_evidence(records,'starting')
    for case in cases:
        folder=root/'matches'/case['id']
        if folder.exists():
            if not (folder/'report.json').exists():raise SystemExit('Incomplete existing attempt; preserve and investigate')
            balance=analyze_run(folder);record=collect_case(folder,case,balance)
        else:
            if args.limit and len(records)>=args.limit:break
            write_progress(status='running',current=case['id'],completed=len(records))
            print('START '+case['id']+' '+case['mode']+' '+str(case['policies']),flush=True)
            code=run(SimpleNamespace(exe=args.exe,output=folder,players=case['players'],policies=case['policies'],
                seed=case['seed'],gameplay_seed=case['gameplay_seed'],timeout=case['timeout'],behavior='normal',
                cohort='balance-30-primary',ready_delay=None,visible=case['visible']))
            if not (folder/'0/events.jsonl').exists():
                write_progress(status='failed',current=case['id'],completed=len(records),runner_exit=code)
                write_evidence(records,'failed; retain incomplete attempt');raise SystemExit(2)
            balance=analyze_run(folder);record=collect_case(folder,case,balance)
        records.append(record)
        status='running' if record['match']['passed'] else 'failed; stopped for investigation'
        write_evidence(records,status)
        write_progress(status=status,current=case['id'],completed=len(records),
            passed=sum(r['match']['passed'] for r in records))
        print('END '+case['id']+' '+json.dumps(record['match'],ensure_ascii=True),flush=True)
        if not record['match']['passed']:raise SystemExit(2)
    complete=len(records)==30
    data=write_evidence(records,'completed' if complete else 'checkpoint; campaign incomplete')
    write_progress(status='completed' if complete else 'checkpoint',completed=len(records))
    save(root/'completion.json',dict(complete=complete and all(r['match']['passed'] for r in records),
        modes={m:dict(recorded=v['recorded'],passed=v['passed']) for m,v in data['modes'].items()},
        planned=30,recorded=len(records),interpretation='pending analysis'))
    print('CAMPAIGN '+('COMPLETE' if complete else 'CHECKPOINT'),flush=True)


if __name__=='__main__':main()
