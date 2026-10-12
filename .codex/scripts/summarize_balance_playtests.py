"""Render a Korean review page from completed PLAN067 evidence only."""
import csv,html,json,sys
from pathlib import Path
from analyze_balance_playtest import analyze_run
root=Path(sys.argv[1]); results=[]
for folder in sorted(root.iterdir()):
 if folder.is_dir() and (folder/'report.json').exists():
  result=analyze_run(folder)
  report=json.loads((folder/'report.json').read_text(encoding='utf-8'))
  result['duration_seconds']=report.get('seconds')
  result['mode']=report.get('peers',[{}])[0].get('terminal',{}).get('mode')
  result['outcome']=[{k:s.get(k) for k in ('seat','wins','score')} for s in report.get('peers',[{}])[0].get('terminal',{}).get('seats',[])]
  results.append(result)
(root/'balance-summary.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
coverage=[dict(run=r['run'],mode=r.get('mode'),**item) for r in results for item in r.get('items',[])]
with (root/'balance-item-coverage.csv').open('w',encoding='utf-8-sig',newline='') as f:
 fields=sorted(set().union(*(row.keys() for row in coverage))) if coverage else ['run']
 writer=csv.DictWriter(f,fieldnames=fields);writer.writeheader();writer.writerows(coverage)
H=html.escape
item_names={'Fan':'부채','Windbreaker':'바람막이','Warm Tea':'따뜻한 차','Cat':'고양이',
 'Hand Fan':'손풍기','Ice Cream':'아이스크림','Iced Americano':'아이스 아메리카노',
 'Water Gun':'물총','Hug T-shirt':'안아줘요 티셔츠','Hot Americano':'뜨거운 아메리카노',
 'Smartphone':'스마트폰','Hot Pack':'핫팩','Mask':'마스크','Samgyetang':'삼계탕',
 'Soda':'탄산음료','Buldak Noodles':'불닭볶음면','Screwdriver':'십자드라이버',
 'Claw Machine':'집게손','Blue Tape':'청테이프','Red Card':'레드카드'}
def num(value):return '—' if value is None else f'{value:.2f}' if isinstance(value,float) else str(value)
parts=['''<!doctype html><meta charset="utf-8"><title>밸런스 관찰 결과</title><style>
body{background:#f0f5f5;color:#223a3c;font:16px/1.65 "Malgun Gothic",sans-serif;max-width:1200px;margin:36px auto;padding:0 24px}h1,h2{line-height:1.3}.card,section{background:white;border:1px solid #d5e2df;border-radius:18px;padding:24px;margin:18px 0}table{border-collapse:collapse;width:100%;font-size:14px}th,td{text-align:left;padding:9px;border-bottom:1px solid #e3ecea}th{background:#e9f4ef}.note{color:#546b6d}.good{color:#187052}.bad{color:#b23b3b}.scroll{overflow:auto}code{background:#edf2f5;padding:2px 5px}</style>
<h1>게임 밸런스 관찰 결과</h1><p>원래 게임 규칙·아이템 수치·봇 BT는 유지했습니다. 아래는 새 계측 빌드의 정상 경기 결과입니다.</p>''']
parts.append(f'<div class="card"><b>완료 기록 {len(results)} / 5개 시나리오</b><br>실행 완료와 데이터 검증 통과를 구분합니다. 기존 빌드의 19판과 합쳐 승률을 계산하지 않습니다.</div>')
parts.append('<p class="note">봇전 두 경기는 자동 플레이어의 최소 준비 대기만 1.25초 / 5초로 바꿉니다. 같은 시드라도 온라인 실행 순서와 이후 선택은 달라질 수 있습니다. 미니게임 성공 여부는 모델링했으며 사람의 승률이 아닙니다.</p>')
for r in results:
 parts.append(f'<section><h2>{H(r["run"])} · {num(r.get("duration_seconds",0)/60)}분</h2>')
 parts.append(f'<p class="{"good" if r["passed"] else "bad"}">기록 검증: {"통과" if r["passed"] else H(str(r["errors"]))} / 행동 {r.get("actions",0)}건 / 온도 효과 {r.get("impacts",0)}건 / 출처 미연결 {r.get("unattributed_impacts",0)}건</p>')
 parts.append('<h3>준비 속도와 선공·온도</h3><table><tr><th>자리</th><th>평균 준비 완료</th><th>첫 행동 순서 / 배정 턴</th><th>선풍기 온도 변화</th><th>준비 후 회복</th></tr>')
 for seat,v in r.get('seats',{}).items():
  parts.append(f'<tr><td>P{int(seat)+1}</td><td>{num(v.get("mean_ready_seconds"))}초</td><td>{v.get("first_order_slots",0)} / {v.get("ordered_turns",0)}</td><td>{num(v.get("fan_net_temperature"))}°</td><td>{num(v.get("recovery_net_temperature"))}°</td></tr>')
 parts.append('</table><p class="note">첫 순서는 방어 또는 이후 취소되는 행동을 포함할 수 있습니다. 평균 준비 시간은 미니게임 대기를 포함한 실제 서버 수락 시각입니다.</p><h3>아이템 효과</h3><div class="scroll"><table><tr><th>아이템</th><th>사용</th><th>상대 피해</th><th>자신 피해</th><th>자신 회복</th><th>상대 회복</th><th>방어로 막힘</th><th>상·하한 낭비</th><th>막은 피해</th><th>막은 회복</th></tr>')
 for item in r.get('items',[]):
  if not item.get('actions') and not item.get('uses') and not item.get('prevented_damage'):continue
  name=item.get('name',str(item['item_id']))
  parts.append('<tr><td>'+H(item_names.get(name,name))+'</td>'+''.join('<td>'+num(item.get(k))+'</td>' for k in ['uses','opponent_damage','self_damage','self_healing','opponent_healing','mitigated','clipped','prevented_damage','prevented_healing'])+'</tr>')
 parts.append('</table></div><p class="note">—는 해당 지표가 기록되지 않았다는 뜻입니다. 보유 횟수는 합법적 사용 기회와 다르며, 실제 피해량은 승리 기여도를 직접 증명하지 않습니다.</p>')
 unused=[item_names.get(i.get('name'),i.get('name',str(i['item_id']))) for i in r.get('items',[]) if i.get('held_copy_prep_windows') and not i.get('uses')]
 parts.append('<p class="note">보유했지만 사용 표본이 없는 아이템: '+H(', '.join(unused) or '없음')+'</p>')
 parts.append('<h3>지연 효과·사망·득점</h3><p>종료 시 미해결 예약: '+H(str(r.get('scheduled_pending_at_end',[])))+' / 다인전 사망 원인: '+H(str(r.get('multi_death_causes',{})))+'</p>')
 if r.get('ghosts'):
  parts.append('<table><tr><th>유령</th><th>귀신의 한 적중</th><th>실제 피해</th><th>빙의 소모 관측</th></tr>')
  for seat,v in r['ghosts'].items():
   parts.append(f'<tr><td>P{int(seat)+1}</td><td>{v.get("grudge_hits",0)}</td><td>{num(v.get("actual_damage",0))}°</td><td>{v.get("possession_spends_observed",0)}</td></tr>')
  parts.append('</table>')
 if r.get('unattributed_impact_details'):
  parts.append('<p class="note">아이템 행동에 연결되지 않은 아래 관측은 환경 회복일 수도 있습니다. 이 숫자만으로 기록 오류라고 판정하지 않으며 원본 로그와 대조합니다.</p><table><tr><th>이벤트</th><th>라운드 / 턴</th><th>대상</th><th>온도</th><th>요청 변화</th><th>상·하한 낭비</th></tr>')
  for event in r['unattributed_impact_details']:
   v=event['observation']
   parts.append(f'<tr><td>{event["event_index"]}</td><td>{event["round"]} / {event["turn"]}</td><td>P{v["target"]+1}</td><td>{num(v["before"])} → {num(v["after"])}</td><td>{num(v["requested"])}</td><td>{num(v["clipped"])}</td></tr>')
  parts.append('</table>')
 parts.append('<p>최종 상태: '+H(str(r.get('outcome',[])))+'</p></section>')
parts.append('<section><h2>판정 범위</h2><p>기능 통과와 밸런스 적합성은 별개입니다. 아이템별 사용 표본·좌석별 반복·사람 플레이 대조가 충분해지기 전에는 상향/하향을 확정하지 않습니다. 기존 60판 계획의 미완료분은 별도 유지합니다.</p></section>')
(root/'balance-review.html').write_text(''.join(parts),encoding='utf-8')
print(json.dumps({'completed':len(results),'passed':sum(r['passed'] for r in results),'html':str(root/'balance-review.html')}))
