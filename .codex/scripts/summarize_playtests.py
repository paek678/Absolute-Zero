"""Describe observed campaign evidence without promoting fixtures/preflights to matches."""
import argparse
import collections
import csv
import html
import json
from pathlib import Path
import re


def summarize(root):
    rows = []
    item_rows = []
    for manifest_path in sorted(root.rglob('manifest.json')):
        folder = manifest_path.parent
        manifest = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
        if 'players' not in manifest or 'policies' not in manifest:
            continue
        run_id = folder.relative_to(root).as_posix()
        report_path = folder / 'report.json'
        if not report_path.exists() and (folder / 'review.json').exists():
            report_path = folder / 'review.json'
        report = json.loads(report_path.read_text(encoding='utf-8-sig')) if report_path.exists() else {}
        events = []
        for path in [folder / '0/events.jsonl', folder / '0/events.jsonl.partial']:
            if path.exists():
                for line in path.read_text(encoding='utf-8-sig').splitlines():
                    try: events.append(json.loads(line))
                    except ValueError: pass
                break
        log_path = folder / '0/player.log'
        log = log_path.read_text(encoding='utf-8-sig', errors='replace') if log_path.exists() else ''
        deaths = re.findall(r'\[DeathService\] Seat (\d+) killed by seat (\d+) \((\w+)\)', log)
        origins = collections.Counter(origin for _, _, origin in deaths)
        actions = collections.Counter()
        receipt_items = {}
        item_damage = collections.Counter()
        item_heal = collections.Counter()
        for event in events:
            if event.get('kind') == 'command_received':
                match = re.match(r'(?:multi_action|effect_apply):item=(-?\d+)', event.get('detail', ''))
                if match:
                    actions[match[1]] += 1
                    receipt_items[event.get('receipt')] = match[1]
            if event.get('kind') == 'temperature_effect' and event.get('receipt') in receipt_items:
                fields = dict(p.split('=', 1) for p in event.get('detail', '').split(';') if '=' in p)
                try:
                    delta = float(fields['after']) - float(fields['before'])
                    item = receipt_items[event['receipt']]
                    if delta < 0: item_damage[item] -= delta
                    else: item_heal[item] += delta
                except (ValueError, KeyError): pass
        for item, count in actions.items():
            item_rows.append(dict(run=run_id, item_id=item, observed_action_scopes=count,
                                  attributed_temperature_decrease=item_damage[item],
                                  attributed_temperature_increase=item_heal[item],
                                  certified_complete=False, note='Observed scoped changes only; missing hooks or defensive/utility value are not zero effectiveness.'))
        temps = [s['temperature'] for e in events for s in e.get('seats') or []]
        last = events[-1] if events else {}
        last_state = next((e for e in reversed(events) if e.get('seats')), {})
        closed_without_report = not report and (folder / '0/summary.json').exists()
        rows.append(dict(run=run_id, mode=manifest.get('players'), kind=manifest.get('kind'),
            status='PASS' if report.get('passed') else 'FAIL' if report else 'INCOMPLETE_REPORT' if closed_without_report else 'IN_PROGRESS',
            completed=all(p.get('summary', {}).get('completed') is True for p in report.get('peers', [])) if report.get('peers') else False,
            primary_counted=report.get('primary_counted', False), seconds=round(report.get('seconds', last.get('time', 0)), 1),
            rounds=max((e.get('round', 0) for e in events), default=0),
            prep_windows=len({(e.get('round'), e.get('turn')) for e in events if e.get('phase') == 'PrepPhase'}),
            deaths=len(deaths), natural_deaths=origins['Natural'], death_origins=dict(origins),
            ghost_grudge=len(re.findall(r'\[Ghost\] Grudge ', log)), ghost_possession=len(re.findall(r'\[Ghost\] Possession ', log)),
            observed_first_main_actions=dict(collections.Counter(re.findall(r'\[COMBAT\] Executing FIRST player P(\d) main:', log))),
            min_temperature=min(temps, default=None), max_temperature=max(temps, default=None),
            actions_by_item=dict(actions), scores=[s.get('score') for s in last_state.get('seats') or []],
            round_wins=[s.get('wins') for s in last_state.get('seats') or []],
            winner_mask=last_state.get('winnerMask', 0), policies=manifest['policies'],
            build_hashes=manifest.get('build_hashes'),
            capture_mode=manifest.get('capture_mode', 'diagnostic camera re-render'),
            death_metric_scope='Multi death service only; Duel/Solo not measured by this counter',
            unclassified_returns=sum(p.get('unclassified_returns', 0) for p in report.get('peers', [])),
            problems=[p.get('problems', []) for p in report.get('peers', [])],
            capture=str(next(iter(sorted((folder / '0').glob('visual-*-attack-*.png'))), folder / '0/result.png').resolve())))
    (root / 'screening-summary.json').write_text(json.dumps(rows, indent=2, ensure_ascii=False), encoding='utf-8')
    with (root / 'item-screening.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        if item_rows:
            writer = csv.DictWriter(stream, fieldnames=list(item_rows[0]))
            writer.writeheader(); writer.writerows(item_rows)
    with (root / 'screening-summary.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        if rows:
            writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
            writer.writeheader(); writer.writerows(rows)
    header = '<!doctype html><meta charset="utf-8"><title>플레이 테스트 기록</title><style>body{font:16px Segoe UI,Malgun Gothic,sans-serif;background:#101820;color:#e8eff1;margin:40px}h1{color:#a8e6cf}table{border-collapse:collapse;width:100%}td,th{border:1px solid #40505a;padding:12px;text-align:left}th{background:#233640}.note{padding:18px;background:#273842;line-height:1.7}.cards{display:grid;grid-template-columns:1fr 1fr;gap:16px}img{width:100%}article{background:#233640;padding:16px;border-radius:12px}</style>'
    content = '<h1>실제 플레이 테스트 — 관찰 기록</h1><p class="note">사전 테스트와 정상 경기 본집계는 별개입니다. 미니게임 성공·실패는 자동 참가자가 가정한 결과이며, 사람의 실제 조작 실력을 측정한 자료가 아닙니다. 현재 표본으로 최종 밸런스를 판정할 수 없습니다. FAIL은 게임 오류뿐 아니라 기록 누락·시간 초과도 포함합니다.</p>'
    primary = sum(row['primary_counted'] for row in rows)
    pilot = sum(row['kind'] == 'pilot' and row['status'] == 'PASS' for row in rows)
    pilot_attempts = sum(row['kind'] == 'pilot' for row in rows)
    content += f'<h2>본검사 {primary}/60판 · 파일럿 완료 {pilot}/8판 (실행 {pilot_attempts}건)</h2>'
    content += '<div class="cards"><article><h3>수정하고 재검증한 것</h3><p>아이템 기록 누락, 테스트 참가자의 조기 종료, 이전 배치를 기대하던 테스트 기준을 고쳤습니다. 게임의 승리 조건과 아이템 수치는 바꾸지 않았습니다.</p></article>'
    content += '<article><h3>밸런스 검토 후보</h3><p>손풍기는 1회 소모·피해 2도인데 기본 부채는 무제한·3도입니다. 기본 아이템 봉인 시에는 별도 용도가 있습니다. 기획 문서의 손풍기 4도와도 달라 의도 확인이 필요합니다.</p></article>'
    content += '<article><h3>경기 길이</h3><p>3~4인전은 같은 5킬 승리 조건을 사용합니다. 자연 사망에는 처치 점수가 없어 장기전이 될 수 있습니다. 최대 라운드 설정은 현재 종료 판정에서 사용되지 않습니다. 목표 경기 시간은 아직 확정하지 않았습니다.</p></article>'
    content += '<article><h3>화면 검수</h3><p>유령의 파란 직사각형은 현재 임시 스프라이트입니다. 상대 아이템·작은 글씨의 가독성도 확인 대상입니다. 숨김 실행 캡처의 결과창 겹침은 실제 화면 재검사에서는 나타나지 않았습니다. 진단용 캡처와 실제 화면 캡처를 구분해 표시합니다.</p></article></div>'
    content += '<table><tr><th>실행</th><th>인원</th><th>판정</th><th>시간</th><th>진행 라운드</th><th>자연 사망 / 전체 사망</th><th>유령 공격</th></tr>'
    for row in rows:
        deaths = f"{row['natural_deaths']} / {row['deaths']}" if row['mode'] >= 3 else '별도 집계'
        content += '<tr>' + ''.join('<td>' + html.escape(str(value)) + '</td>' for value in [row['run'], row['mode'], row['status'], str(row['seconds'])+'초', row['rounds'], deaths, row['ghost_grudge']]) + '</tr>'
    content += '</table><h2>실행 중 수집한 화면 자료</h2><div class="cards">'
    for row in rows:
        path = Path(row['capture'])
        if path.exists():
            relative = path.relative_to(root.resolve()).as_posix()
            label = '실제 화면 캡처' if row['capture_mode'] == 'framebuffer' else '진단용 재렌더링 — UI 겹침 등은 실제 화면에서 재확인 필요'
            content += '<article><h3>' + html.escape(row['run']) + '</h3><p>'+label+'</p><img src="' + html.escape(relative, quote=True) + '"></article>'
    content += '</div>'
    (root / 'screening-report.html').write_text(header + content, encoding='utf-8')
    print(json.dumps([{k: row[k] for k in ('run','status','seconds','rounds','natural_deaths','deaths','scores')} for row in rows], ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('root', type=Path)
    summarize(parser.parse_args().root.resolve())
