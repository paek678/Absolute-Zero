"""Bounded runtime fixture coverage. Never count these as natural/balance matches."""
import argparse
import json
from pathlib import Path
from run_validation_matrix import run_case

CASES = [('input', n, 0) for n in (2, 3, 4)] + [
    ('minigame-target', 4, 0), ('minigame-actor', 4, 0), ('minigame-failure', 4, 0),
    *[('ghost', 4, seat) for seat in range(4)],
    ('topup-transition', 4, 0), ('topup-transition', 4, 1), ('topup-transition', 4, 5),
    ('disconnect-prep', 4, 0), ('disconnect-attack', 4, 0), ('host-exit', 4, 0), ('host-exit', 2, 0),
    ('special', 3, 0), ('joint', 4, 0), ('delayed', 4, 0),
    ('duel-repeat', 2, 0), ('duel-mini', 2, 0), ('inventory', 4, 0), ('rpc-guards', 4, 0)]

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('exe', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--start', type=int, default=0)
    parser.add_argument('--limit', type=int, default=len(CASES))
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    results = []
    for i, case in list(enumerate(CASES))[args.start:args.start + args.limit]:
        try:
            result = run_case(args.exe.resolve(), args.output.resolve(), case, 19800 + i * 20,
                              capture=case[0] in ('special', 'joint', 'duel-mini'))
        except Exception as error:
            result = dict(case=case, passed=False, runner_error=type(error).__name__ + ': ' + str(error))
        results.append(result)
        (args.output / f'batch-{args.start}.json').write_text(json.dumps(results, indent=2), encoding='utf-8')
    print(json.dumps(dict(total=len(results), passed=sum(r['passed'] for r in results),
                         natural_matches_counted=0, transport='local UTP; not Relay')))
