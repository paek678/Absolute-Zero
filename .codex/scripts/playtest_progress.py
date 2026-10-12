"""Read-only live progress; no game state changes and no success inference."""
import json
from pathlib import Path
import sys

root = Path(sys.argv[1])
reports = [json.loads(p.read_text()) for p in root.glob('*/report.json')]
active = []
for path in root.glob('*/0/events.jsonl.partial'):
    with path.open('rb') as stream:
        stream.seek(0, 2)
        size = stream.tell()
        stream.seek(max(0, size - 65536))
        lines = stream.read().decode('utf-8', errors='replace').splitlines()
    last = {}
    for line in reversed(lines):
        try:
            candidate = json.loads(line)
            if candidate.get('seats'):
                last = candidate
                break
        except (ValueError, AttributeError): pass
    active.append(dict(run=path.parent.parent.name, seconds=round(last.get('time',0)),
                       round=last.get('round'), turn=last.get('turn'), phase=last.get('phase'),
                       scores=[dict(seat=s.get('seat'),score=s.get('score'),wins=s.get('wins')) for s in last.get('seats',[])]))
print(json.dumps(dict(passed=sum(r.get('passed') is True for r in reports),
                     failed=sum(r.get('passed') is not True for r in reports),active=active)))
