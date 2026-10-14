"""Lossless references and descriptive tables for the 30-match campaign.

No balance recommendations: retain evidence for the later Astra review.
"""
import collections
import json
import math
import re
from pathlib import Path


def load(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def save(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')


def drop_rows(events):
    rows, errors = [], []
    for e in events:
        if not e.get('server') or e.get('kind') != 'balance_drop_draw':
            continue
        b = e.get('balance') or {}
        candidates = b.get('dropCandidates') or []
        total = sum(c['weight'] for c in candidates if c['eligible'])
        selected = b.get('selectedDrop')
        if not candidates or any(not math.isfinite(c['weight']) or c['weight'] <= 0 for c in candidates):
            errors.append('invalid candidate weights')
        if not math.isclose(total, b.get('eligibleWeight', -1), rel_tol=1e-5, abs_tol=1e-5):
            errors.append('eligible total mismatch')
        if total > 0 and selected not in [c['item'] for c in candidates if c['eligible']]:
            errors.append('selected item outside eligible pool')
        if total == 0 and selected:
            errors.append('selected item from empty eligible pool')
        for c in candidates:
            rows.append(dict(event=e['index'], time=e['time'], round=e['round'], turn=e['turn'],
                seat=e['actor'], context=b.get('reason'), item=c['item'], weight=c['weight'],
                eligible=c['eligible'], expected_probability=c['weight']/total if c['eligible'] and total > 0 else 0,
                selected=bool(selected and c['item'] == selected), successful_draw=total > 0,
                grant_certified=False))
    return rows, sorted(set(errors))


def collect_case(folder, case, balance):
    report = load(folder/'report.json')
    events = [json.loads(x) for x in (folder/'0/events.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    drops, errors = drop_rows(events)
    host = [e for e in events if e.get('server')]
    prep = [e for e in host if e['kind'] == 'balance_prep_open']
    terminals = [e for e in host if e['kind'] == 'terminal_checkpoint']
    terminal = terminals[0] if len(terminals) == 1 else {}
    results = [e for e in host if e['kind'] == 'result_visible' and e['time'] >= terminal.get('time', float('inf'))]
    # Summary contains the visible-result gate; do not substitute connection time for gameplay time.
    last = terminal or (host[-1] if host else {})
    winners = [i for i in range(4) if terminal.get('winnerMask', 0) & (1 << i)]
    if case['players'] <= 2:
        winners = [s['seat'] for s in terminal.get('seats', [])
                   if s.get('wins', 0) == max((x.get('wins', 0) for x in terminal.get('seats', [])), default=0)]
    match = dict(id=case['id'], mode=case['mode'], policies=' / '.join(case['policies']),
        gameplay_seed=case['gameplay_seed'], passed=bool(report['passed'] and balance['passed'] and not errors),
        wall_seconds=report['seconds'], gameplay_seconds=last['time']-prep[0]['time'] if prep else None,
        rounds=last.get('round'), turns=last.get('turn'), winner_seats=winners,
        winner_mask=terminal.get('winnerMask'), awarded_kills=sum(s.get('score',0) for s in terminal.get('seats',[])),
        actions=balance.get('actions'), impacts=balance.get('impacts'),
        random_draws=len({r['event'] for r in drops if r['successful_draw']}),
        source=str(folder), errors=sorted(set(errors + balance.get('errors', []))),
        visual_mode=load(folder/'manifest.json').get('capture_mode'))
    common={'match':case['id'],'mode':case['mode']}
    def tag(rows): return [dict(common, **r) for r in rows]
    mini, choices = [], []
    for peer in range(case['players']):
        p=folder/str(peer)/'events.jsonl'
        for line in p.read_text(encoding='utf-8-sig').splitlines():
            e=json.loads(line)
            if e['kind'].startswith('minigame_'):
                mini.append(dict(peer=peer,event=e['index'],time=e['time'],round=e['round'],turn=e['turn'],
                    seat=e['actor'],copy=e['copy'],kind=e['kind'],detail=e['detail'],human_input=False))
            if e['kind']=='policy_candidates':
                for entry in filter(None,e['detail'].split(';')):
                    item,copy,score=entry.split(':')
                    choices.append(dict(peer=peer,event=e['index'],time=e['time'],round=e['round'],turn=e['turn'],
                        seat=e['actor'],item_id=int(item),copy=int(copy),estimated_score=float(score),
                        server_legal_opportunity_certified=False))
    return dict(match=match, items=tag(balance.get('items',[])), seats=tag([dict(v,seat=k) for k,v in balance.get('seats',{}).items()]),
        rounds=tag(balance.get('rounds',[])), drops=tag(drops), targets=tag(balance.get('target_pressure',[])),
        seat_items=tag(balance.get('per_seat_items',[])), minigames=tag(mini), driver_candidates=tag(choices),
        environments=tag([dict(environment=k,announcements=v) for k,v in balance.get('environment_announcements_from_host_log',{}).items()]),
        ghosts=tag([dict(v,seat=k) for k,v in balance.get('ghosts',{}).items()]),
        event_kinds=dict(collections.Counter(e['kind'] for e in host)))


def export(root, records, cases, status, write_workbook=True):
    data=dict(schema=1,status=status,planned=len(cases),recorded=len(records),
        passed=sum(r['match']['passed'] for r in records),modes={},records=records,
        limitations=['Modeled human inputs and fixed real Solo BT; no human skill or fun qualification.',
            'All driver targets prefer the coldest alive opponent.',
            'Per-draw probabilities measure actual RNG draws, not committed grants; planned top-ups/rerolls are separate contexts.',
            'Driver candidate estimates are not server-certified legal opportunities.',
            'Human readability, observer performance overhead and absent rare cases remain unmeasured.',
            'Later Astra interpretation is intentionally deferred; statistics are descriptive.'])
    for mode in ('Solo','Duel','Multi'):
        subset=[r for r in records if r['match']['mode']==mode]
        data['modes'][mode]=dict(recorded=len(subset),passed=sum(r['match']['passed'] for r in subset),records=subset)
        save(root/(mode.lower()+'-data.json'),data['modes'][mode])
    save(root/'campaign-data.json',data)
    tables={key:[row for r in records for row in r.get(key,[])] for key in
        ('rounds','items','seats','seat_items','drops','targets','minigames','environments','ghosts','driver_candidates')}
    # Conditional counts retain context and include eligibility denominators; never compare to one global weight percentage.
    probs=collections.defaultdict(lambda:dict(draws_observed=0,eligible_draws=0,expected_count=0.,observed_count=0))
    for row in tables['drops']:
        k=(row['mode'],row['context'],row['item'])
        if not row['successful_draw']: continue
        p=probs[k];p['draws_observed']+=1;p['eligible_draws']+=int(row['eligible'])
        p['expected_count']+=row['expected_probability'];p['observed_count']+=int(row['selected'])
    tables['drop_totals']=[dict(mode=k[0],context=k[1],item=k[2],**v) for k,v in sorted(probs.items())]
    save(root/'tables.json',tables)
    plan=(Path(__file__).resolve().parents[2]/'Docs/Plans/PLAN_balance_30_match_flows_20261013.md').read_text(encoding='utf-8-sig')
    coverage=[]
    for line in plan.splitlines():
        m=re.match(r'\| (B\d\d) \| (.*?) \| (.*?) \|',line)
        if not m: continue
        coverage.append(dict(id=m[1],measurement=m[2],requires=m[3],
            status='human review deferred' if 'H' in m[3] else 'visual review pending' if 'V' in m[3] else
                'partial evidence; missing fields remain unmeasured' if 'N' in m[3] else 'raw/derived evidence available' if records else 'not observed',
            completed_qualification=False))
    save(root/'coverage.json',coverage)
    assert len(coverage)==60
    if not write_workbook:
        return data
    from openpyxl import Workbook, load_workbook
    from openpyxl.styles import Font, PatternFill, Alignment
    wb=Workbook();wb.remove(wb.active)
    overview=[dict(mode=mode,planned=10,recorded=data['modes'][mode]['recorded'],passed=data['modes'][mode]['passed'],
        status=status) for mode in data['modes']]
    sheets={'진행도':overview,'판별 기록':[r['match'] for r in records], '라운드':tables['rounds'],
        '아이템 효과':tables['items'],'좌석':tables['seats'],'대상 압박':tables['targets'],
        '스폰 확률':tables['drop_totals'],'추첨 원본':tables['drops'],'미니게임':tables['minigames'],
        '유령':tables['ghosts'],'환경':tables['environments'],'선택 후보':tables['driver_candidates'],'관측 범위':coverage,
        '읽기 안내':[dict(note=x) for x in data['limitations']]}
    for name,rows in sheets.items():
        ws=wb.create_sheet(name)
        fields=list(dict.fromkeys(k for row in rows for k in row)) or ['미관측']
        ws.append(fields)
        for row in rows:
            ws.append([json.dumps(row.get(k),ensure_ascii=False) if isinstance(row.get(k),(dict,list)) else row.get(k) for k in fields])
        for c in ws[1]:c.font=Font(name='맑은 고딕',bold=True,color='FFFFFF');c.fill=PatternFill('solid',fgColor='356E61')
        ws.freeze_panes='A2';ws.auto_filter.ref=ws.dimensions;ws.sheet_view.zoomScale=90
        for column in ws.columns:
            letter=column[0].column_letter
            ws.column_dimensions[letter].width=min(65,max(14,max(len(str(c.value or '')) for c in list(column)[:60])+2))
        for row in ws.iter_rows(min_row=2):
            for c in row:
                c.font=Font(name='맑은 고딕',size=10);c.alignment=Alignment(vertical='top')
                if isinstance(c.value,float):c.number_format='0.00'
    target=root/'플레이테스트_30판_기록.xlsx'
    tmp=root/'workbook.partial.xlsx';wb.save(tmp);tmp.replace(target)
    check=load_workbook(target,read_only=True)
    assert check['판별 기록'].max_row==len(records)+1
    assert len(coverage)==60
    check.close()
    return data
