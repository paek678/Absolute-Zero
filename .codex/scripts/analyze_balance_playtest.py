"""PLAN067: authoritative balance observations; never infer causal win contribution."""
import argparse
import collections
import csv
import json
import math
import statistics
from pathlib import Path


def analyze(events):
    errors=[]; actions={}; ended=set(); schedules={}; resolved=set()
    receipt_verdicts=collections.defaultdict(list); receipt_actions=collections.defaultdict(list)
    consumption_links=collections.Counter()
    for event in events:
        if not event.get('server'):continue
        if event.get('kind')=='command_verdict':receipt_verdicts[event.get('receipt',0)].append(event.get('detail',''))
        if event.get('kind')=='balance_action_begin' and event.get('receipt',0):
            a=(event.get('balance') or {}).get('action') or {}
            receipt_actions[(event['receipt'],a.get('source'),a.get('item'),a.get('copy'))].append(a.get('id'))
    items=collections.defaultdict(collections.Counter); names={}
    seats=collections.defaultdict(collections.Counter); ready=collections.defaultdict(list)
    rounds={}; unlinked=0; unlinked_details=[]; impacts=0; measured=0; target_pressure=collections.Counter()
    actor_items=collections.defaultdict(collections.Counter); ghost=collections.defaultdict(collections.Counter); verdicts=collections.Counter()
    held_seen=set(); inventory_events=0; event_ids=set(); delayed_impacts=set(); deaths=collections.Counter()
    possession_uses_seen=set(); possessed_turns_seen=set()
    for event in events:
        if not event.get('server'): continue
        for seat in event.get('seats') or []:
            for slot in seat.get('inventory') or []:
                if slot.get('itemName'): names[slot['ItemId']]=slot['itemName']
        if 'index' in event:
            if event['index'] in event_ids:errors.append('duplicate event index')
            event_ids.add(event['index'])
        kind=event.get('kind',''); data=event.get('balance') or {}; identity=data.get('action') or {}
        aid=identity.get('id',0); item=identity.get('item',-1); actor=identity.get('source',-1)
        key=(event.get('round',0),event.get('turn',0))
        for seat in range(max(event.get('possessionSpent',0),event.get('possessed',0)).bit_length()):
            token=(event.get('matchEpoch',0),seat)
            if event.get('possessionSpent',0)&(1<<seat) and token not in possession_uses_seen:
                possession_uses_seen.add(token);ghost[seat]['possession_spends_observed']+=1
            token=(event.get('matchEpoch',0),event.get('roundEpoch',0),*key,seat)
            if event.get('possessed',0)&(1<<seat) and token not in possessed_turns_seen:
                possessed_turns_seen.add(token);seats[seat]['possessed_turns_observed']+=1
        if kind=='command_verdict':verdicts[event.get('detail','unknown')]+=1
        if item>=0 and actor>=0:
            per=actor_items[(actor,item)]
            if kind=='balance_action_begin':per['actions']+=1
            if kind=='balance_consume':per['uses']+=1
            if kind=='balance_queued':per['accepted_queue_selections']+=1
            if kind=='balance_impact' and aid:
                delta=data.get('after',0)-data.get('before',0)
                per['damage']+=max(0,-delta);per['healing']+=max(0,delta)
                relation='self' if data.get('target')==actor else 'opponent'
                per[relation+'_damage']+=max(0,-delta);per[relation+'_healing']+=max(0,delta)
                per['mitigated']+=data.get('mitigated',0);per['clipped']+=data.get('clipped',0)

        if event.get('seats') and event.get('round',0)>0:
            r=rounds.setdefault(event['round'],{'start':event['time'],'end':event['time'],'turns':set(),'first_scores':None,'last_scores':None})
            r['end']=event['time']; r['turns'].add(event.get('turn',0))
            scores={str(s['seat']):s.get('score',0) for s in event['seats']}
            if r['first_scores'] is None:r['first_scores']=scores
            r['last_scores']=scores
        if kind=='balance_action_begin':
            if aid<=0 or aid in actions:errors.append('duplicate/invalid action id')
            actions[aid]=identity;items[item]['actions']+=1
            if identity.get('target',actor)!=actor:target_pressure[(actor,identity.get('target'))]+=1
        elif kind=='balance_action_end':
            if aid not in actions or aid in ended:errors.append('dangling/duplicate action end')
            elif actions[aid]!=identity:errors.append('action end identity changed')
            ended.add(aid)
        elif kind=='balance_impact':
            impacts+=1
            if aid not in actions:
                unlinked+=1
                unlinked_details.append(dict(event_index=event.get('index'),round=event.get('round'),turn=event.get('turn'),
                                            phase=event.get('phase'),observation=data))
                if aid: errors.append('impact references missing action')
            elif actions[aid] != identity: errors.append('impact identity changed')
            vals=[data.get(k,0) for k in ('before','after','requested','mitigated','clipped')]
            if not all(isinstance(x,(int,float)) and math.isfinite(x) for x in vals):
                errors.append('nonfinite impact');continue
            before,after,raw,block,clip=vals
            if raw*(after-before)<-1e-4 or block < -1e-4 or clip < -1e-4 or abs(abs(raw)-block-clip-abs(after-before)) > .002:
                errors.append('impact reconciliation failed')
            if aid:
                bucket=items[item]; bucket['measured_impacts']+=1
                bucket['damage']+=max(0,before-after);bucket['healing']+=max(0,after-before)
                relation='self' if data.get('target')==actor else 'opponent'
                bucket[relation+'_damage']+=max(0,before-after);bucket[relation+'_healing']+=max(0,after-before)
                bucket['mitigated']+=block;bucket['clipped']+=clip
                if data.get('reason')=='scheduled':bucket['delayed_impacts']+=1
                if before>0 and after<=0:bucket['lethal_temperature_crossings']+=1
                if block>0 and data.get('defenseItem',-1)>=0:
                    blocked_kind='prevented_damage' if raw<0 else 'prevented_healing'
                    items[data['defenseItem']][blocked_kind]+=block
                    actor_items[(data['target'],data['defenseItem'])][blocked_kind]+=block
            eid=data.get('effect',0)
            if eid:
                if eid in delayed_impacts:errors.append('duplicate scheduled impact')
                delayed_impacts.add(eid)
            if eid and (eid not in schedules or schedules[eid].get('action')!=identity):
                errors.append('scheduled impact lost origin')
        elif kind=='balance_queued':items[item]['accepted_queue_selections']+=1
        elif kind=='balance_consume':
            receipt=event.get('receipt',0)
            if aid:
                consumption_links['explicit_action']+=1
                if aid not in actions or actions[aid]!=identity:errors.append('consumption lost action identity')
            elif 'MiniGameFailureConsumed' in receipt_verdicts.get(receipt,[]):
                consumption_links['minigame_failure_no_effect']+=1;items[item]['failed_minigame_spends']+=1
            elif len(receipt_actions.get((receipt,actor,item,identity.get('copy')),[]))==1:
                consumption_links['unique_command_and_physical_copy']+=1
            else:consumption_links['unresolved']+=1
            items[item]['uses']+=1
            items[item]['limited_spends' if data.get('reason')=='limited' else 'unlimited_uses']+=1
        elif kind=='balance_item_result':
            items[item]['result_'+data.get('reason','unknown')]+=1
        elif kind=='balance_defense_active':items[item]['defense_activations']+=1
        elif kind=='balance_scheduled':
            eid=data.get('effect',0)
            if eid<=0 or eid in schedules:errors.append('duplicate schedule id')
            schedules[eid]=data
            if aid not in actions: errors.append('schedule missing source action')
            items[item]['scheduled']+=1
        elif kind=='balance_schedule_end':
            eid=data.get('effect',0)
            if eid not in schedules or eid in resolved:errors.append('dangling/duplicate scheduled resolution')
            elif schedules[eid].get('action')!=identity:errors.append('schedule identity changed')
            resolved.add(eid);items[item]['schedule_'+data.get('reason','unknown')]+=1
        elif kind=='balance_ready':
            latency=data.get('seconds',-1)
            if latency<0:errors.append('ready without preparation start')
            else:ready[actor].append(latency)
            seats[actor]['ready_count']+=1
        elif kind=='balance_order':
            order=data.get('order') or []
            if len(set(order))!=len(order):errors.append('duplicate seat in order')
            if order:seats[order[0]]['first_order_slots']+=1
            for seat in order:seats[seat]['ordered_turns']+=1
        elif kind=='balance_prep_open':
            measured+=1
            for seat in event.get('seats') or []:
                for slot in seat.get('inventory') or []:
                    iid=slot.get('ItemId',-1)
                    token=key+(seat['seat'],slot.get('CopyId',0))
                    if iid>=0 and token not in held_seen:
                        held_seen.add(token);items[iid]['held_copy_prep_windows']+=1
        elif kind=='balance_inventory_change':
            inventory_events+=1
            prev=data.get('previousSlot') or {}; nxt=data.get('nextSlot') or {}
            if data.get('reason') in ('Add','Insert') and nxt.get('ItemId',-1)>=0:
                items[nxt['ItemId']]['copy_insertions']+=1
            elif data.get('reason')=='Value' and nxt.get('ItemId',-1)>=0:
                if nxt.get('CopyId')!=prev.get('CopyId') or nxt.get('ItemId')!=prev.get('ItemId'):
                    items[nxt['ItemId']]['copy_replacements_in']+=1
        elif kind=='death':
            fields=dict(p.split('=',1) for p in event.get('detail','').split(';') if '=' in p)
            deaths[fields.get('origin','unknown')]+=1
        elif kind=='temperature_effect':
            fields=dict(p.split('=',1) for p in event.get('detail','').split(';') if '=' in p)
            if fields.get('cause','').startswith('ghost_grudge:source='):
                source=int(fields['cause'].split('=')[-1]);ghost[source]['grudge_hits']+=1
                ghost[source]['actual_damage']+=max(0,float(fields['before'])-float(fields['after']))
            if fields.get('cause') in ('fan','recovery'):
                seat=int(fields['target']); delta=float(fields['after'])-float(fields['before'])
                seats[seat][fields['cause']+'_net_temperature']+=delta
    if not measured:errors.append('no balance preparation observations')
    if not actions:errors.append('no balance actions')
    if actions.keys()-ended:errors.append('unclosed balance actions')
    for item in items:
        items[item]['item_id']=item;items[item]['name']=names.get(item,'unknown')
    for seat,values in ready.items():
        seats[seat]['mean_ready_seconds']=statistics.mean(values)
        seats[seat]['min_ready_seconds']=min(values);seats[seat]['max_ready_seconds']=max(values)
    round_rows=[]
    for number,r in rounds.items():
        round_rows.append({'round':number,'seconds':r['end']-r['start'],'observed_turns':len(r['turns']),
                           'kill_score_gain':sum(r['last_scores'].values())-sum(r['first_scores'].values())})
    mode=next((e.get('mode') for e in reversed(events) if e.get('mode') not in (None,'none')),None)
    last_round=max(rounds,default=0)
    no_score_closed_rounds=[r['round'] for r in round_rows if mode=='Multi' and r['round']<last_round and r['kill_score_gain']==0]
    return {'mode':mode,'closed_rounds_without_kill_score_gain':no_score_closed_rounds,'passed':not errors,'errors':sorted(set(errors)),'actions':len(actions),'impacts':impacts,
            'unattributed_impacts':unlinked,'unattributed_impact_details':unlinked_details,'prep_windows':measured,'inventory_events':inventory_events,
            'scheduled_pending_at_end':sorted(schedules.keys()-resolved),
            'items':list(items.values()),'seats':dict(seats),'rounds':round_rows,
            'consumption_links':dict(consumption_links),
            'multi_death_causes':dict(deaths),'ghosts':dict(ghost),'command_verdicts':dict(verdicts),
            'per_seat_items':[dict(v,seat=a,item_id=i,name=names.get(i,'unknown')) for (a,i),v in actor_items.items() if v],
            'target_pressure':[{'source':a,'target':b,'actions':n} for (a,b),n in target_pressure.items()],
            'limitations':['held exposure is not legal opportunity','copy insertions include initialization and transfers, not just random drops',
                           'lethal crossings are not awarded kills','first order slot may be defense or an ultimately skipped action',
                           'human mini-game outcomes modeled; no causal win-rate claim']}


def analyze_run(root):
    report_path=root/'report.json'
    report=json.loads(report_path.read_text(encoding='utf-8')) if report_path.exists() else {}
    path=root/'0/events.jsonl'
    if not path.exists():return {'passed':False,'errors':['missing finalized host telemetry'],'run':root.name}
    try:events=[json.loads(x) for x in path.read_text(encoding='utf-8').splitlines()]
    except (ValueError,UnicodeError):return {'passed':False,'errors':['malformed telemetry'],'run':root.name}
    result=analyze(events)
    if report.get('passed') is not True:
        result['passed']=False;result['errors'].append('normal match gate did not pass')
    import re
    log=(root/'0/player.log').read_text(encoding='utf-8',errors='replace') if (root/'0/player.log').exists() else ''
    result['environment_announcements_from_host_log']=dict(collections.Counter(re.findall(r'Environment selected: (\w+)',log)))
    result['run']=root.name
    import hashlib
    result['analyzer_sha256']=hashlib.sha256(Path(__file__).read_bytes()).hexdigest()
    (root/'balance.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    fields=sorted(set().union(*(row.keys() for row in result['items']))) if result['items'] else ['item_id']
    with (root/'balance-items.csv').open('w',encoding='utf-8-sig',newline='') as f:
        writer=csv.DictWriter(f,fieldnames=fields);writer.writeheader();writer.writerows(result['items'])
    for name, rows in [('seat-items',result['per_seat_items']),('rounds',result['rounds']),
                       ('seats',[dict(v,seat=k) for k,v in result['seats'].items()])]:
        fields=sorted(set().union(*(row.keys() for row in rows))) if rows else ['unmeasured']
        with (root/('balance-'+name+'.csv')).open('w',encoding='utf-8-sig',newline='') as f:
            writer=csv.DictWriter(f,fieldnames=fields);writer.writeheader();writer.writerows(rows)
    return result

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('run',type=Path);args=parser.parse_args()
    result=analyze_run(args.run)
    print(json.dumps({k:v for k,v in result.items() if k not in ('items','target_pressure')},ensure_ascii=True))
    raise SystemExit(0 if result['passed'] else 2)
