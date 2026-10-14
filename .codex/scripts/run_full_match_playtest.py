"""PLAN066 preflight runner. Fail closed; never call a partial trace balance evidence."""
import argparse
import hashlib
import json
import math
import re
from pathlib import Path
import subprocess
import time
import uuid


def digest(path):
    with path.open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


ANALYZER_FINGERPRINT = digest(Path(__file__))


def audit_events(events):
    problems = set()
    possession = {}
    grudge_actor = {}
    grudge_targets = set()
    effects = 0
    for event in events:
        for seat in event.get('seats') or []:
            temperature = seat.get('temperature')
            if temperature is not None and (not isinstance(temperature, (int, float)) or not math.isfinite(temperature) or not 0 <= temperature <= 37):
                problems.add('temperature outside legal range')
        if not event.get('server'):
            continue
        epoch = event.get('matchEpoch', 0)
        if epoch:
            spent = event.get('possessionSpent', 0)
            if possession.get(epoch, 0) & ~spent:
                problems.add('possession spent state reverted within match')
            possession[epoch] = spent
        if event.get('kind') != 'temperature_effect':
            continue
        effects += 1
        fields = dict(part.split('=', 1) for part in event.get('detail', '').split(';') if '=' in part)
        try:
            after = float(fields['after']); target = int(fields['target'])
            state = next(s for s in event['seats'] if s['seat'] == target)
            if abs(after - state['temperature']) > .001:
                problems.add('effect record does not match authoritative target state')
            cause = fields.get('cause', '')
            if cause.startswith('ghost_grudge:source='):
                source = int(cause.split('=')[1])
                key = (epoch, event.get('roundEpoch'), source)
                turn = event['turn']
                if key in grudge_actor and turn - grudge_actor[key] < 2:
                    problems.add('grudge reused before one-turn cooldown')
                grudge_actor[key] = turn
                hit = (epoch, event.get('roundEpoch'), turn, target)
                if hit in grudge_targets:
                    problems.add('multiple ghost damage hits on target in same turn')
                grudge_targets.add(hit)
        except (KeyError, ValueError, TypeError, StopIteration):
            problems.add('invalid temperature-effect evidence')
    return dict(problems=sorted(problems), temperature_effects=effects,
                ghost_damage_observations=len(grudge_targets))


def inspect_peer(folder):
    problems = []
    summary_path = folder / 'summary.json'
    if not summary_path.exists():
        return {'passed': False, 'problems': ['missing summary'], 'events': 0}
    try:
        summary = json.loads(summary_path.read_text(encoding='utf-8-sig'))
        if not isinstance(summary, dict):
            raise ValueError('summary must be an object')
    except (ValueError, OSError) as error:
        return {'passed': False, 'problems': ['invalid summary: ' + type(error).__name__], 'events': 0}
    for field in ('completed', 'visibleResult', 'healthyTelemetry'):
        if summary.get(field) is not True:
            problems.append(field + ' false/missing')
    schema = summary.get('schema', 1)
    if not isinstance(schema, int):
        problems.append('invalid summary schema')
        schema = 2
    if schema >= 2 and summary.get('returnedToLobby') is not True:
        problems.append('lobby return not verified')
    if summary.get('failure'):
        problems.append(summary['failure'])
    log_path = folder / 'player.log'
    log = log_path.read_text(encoding='utf-8-sig', errors='replace') if log_path.exists() else ''
    if re.search(r'^(?:\w+\.)*\w*Exception:|Fatal error\.|\[(?:PLAYTEST|CAMERA056|DEATH063)\] FAIL', log, re.M):
        problems.append('process log contains exception/failure (including teardown)')
    path = folder / 'events.jsonl'
    events = []
    if not path.exists():
        problems.append('no finalized telemetry')
        path = folder / 'events.jsonl.partial'
    if path.exists():
        for line in path.read_text(encoding='utf-8-sig').splitlines():
            try:
                value = json.loads(line)
                if not isinstance(value, dict) or 'kind' not in value:
                    raise ValueError('invalid event envelope')
                events.append(value)
            except ValueError:
                problems.append('malformed event')
    if [event.get('index') for event in events] != list(range(1, len(events) + 1)):
        problems.append('event index discontinuity')
    if not events or events[-1].get('kind') != 'recorder_stopping':
        problems.append('missing recorder closure')
    receipts = [event.get('receipt') for event in events if event['kind'] == 'command_received']
    returns = [event.get('receipt') for event in events if event['kind'] == 'command_returned']
    verdicts = {event.get('receipt') for event in events if event['kind'] == 'command_verdict'}
    if any(not isinstance(r, int) or r <= 0 for r in receipts + returns):
        problems.append('missing authority receipt identity')
        receipts = [r for r in receipts if isinstance(r, int) and r > 0]
        returns = [r for r in returns if isinstance(r, int) and r > 0]
    if sorted(receipts) != sorted(returns) or len(set(receipts)) != len(receipts):
        problems.append('unpaired/duplicate authority receipt')
    if any(event['kind'] == 'diagnostic_error' for event in events):
        problems.append('runtime error recorded')
    final = [event for event in events if event['kind'] == 'terminal_checkpoint']
    if len(final) != 1:
        problems.append('missing/duplicate terminal checkpoint')
    for event in events:
        for seat in event.get('seats') or []:
            if 'inventory' not in seat:
                problems.append('missing inventory serialization')
                continue
            inventory = seat['inventory']
            if not isinstance(inventory, list) or any(not isinstance(x, dict) or not isinstance(x.get('CopyId'), int)
                or not isinstance(x.get('ItemId'), int) for x in inventory):
                problems.append('invalid inventory serialization')
                continue
            copies = [x['CopyId'] for x in inventory if x['ItemId'] >= 0]
            if 0 in copies or len(copies) != len(set(copies)):
                problems.append('invalid inventory copy identity')
                break
    invariants = audit_events(events)
    problems.extend(invariants['problems'])
    return {'passed': not problems, 'problems': sorted(set(problems)), 'events': len(events),
            'invariants': invariants,
            'summary': summary, 'terminal': final[0] if final else None,
            'native_resource_warning': 'GarbageCollector disposing of ComputeBuffer' in log,
            'unclassified_returns': len(set(returns) - verdicts), 'causal_attribution_certified': False}


def compare_terminals(peers):
    """At terminal only; inventory version does not certify an atomic live frame."""
    final = [peer.get('terminal') for peer in peers]
    if not final or any(value is None for value in final):
        return {'status': 'inconclusive', 'reason': 'missing terminal checkpoint'}
    def projection(value):
        return {'winnerMask': value['winnerMask'], 'terminalReleased': value['terminalReleased'],
                'seats': [{key: seat[key] for key in ('seat', 'score', 'wins', 'temperature', 'life', 'inventory')}
                          for seat in value['seats']]}
    try:
        if any(not isinstance(value, dict) or not isinstance(value.get('seats'), list) for value in final):
            return {'status': 'inconclusive', 'reason': 'malformed terminal projection'}
        if any(not isinstance(seat, dict) or 'inventory' not in seat for value in final for seat in value['seats']):
            return {'status': 'inconclusive', 'reason': 'missing inventory serialization'}
        normalized = [projection(value) for value in final]
    except (KeyError, TypeError):
        return {'status': 'inconclusive', 'reason': 'malformed terminal projection'}
    return {'status': 'passed' if all(x == normalized[0] for x in normalized) else 'failed',
            'reason': 'stable terminal shared state comparison'}


SOLO_EVIDENCE_FIELDS = ('encounterId','difficultyId','botId','graphName','graphAuthoringId',
    'configurationVersions','catalogNames','effectiveDelays','miniGameSuccess','minimumThinkSeconds',
    'maximumThinkSeconds','readyPolicy','readyReserveSeconds','extraItemDelaySeconds','retryBudget',
    'provisionalTuning','validationFixture','inheritOneVsOneGrants','judgement','coefficients')


def equal_effective_value(actual, expected):
    if isinstance(expected, bool):return type(actual) is bool and actual == expected
    if isinstance(expected, (int,float)):
        return type(actual) in (int,float) and math.isfinite(actual) and math.isfinite(expected) and math.isclose(actual,expected,rel_tol=1e-6,abs_tol=1e-5)
    if isinstance(expected,list):
        return isinstance(actual,list) and len(actual)==len(expected) and all(equal_effective_value(a,b) for a,b in zip(actual,expected))
    if isinstance(expected,dict):
        return isinstance(actual,dict) and set(actual)==set(expected) and all(equal_effective_value(actual[k],v) for k,v in expected.items())
    return type(actual) is type(expected) and actual==expected


def solo_decision_seed(bot_id,generation,round_number,turn):
    value=2166136261
    encoded=(bot_id+'/'+str(generation)+'/'+str(round_number)+'/'+str(turn)).encode('utf-16-le')
    for index in range(0,len(encoded),2):
        value=((value ^ int.from_bytes(encoded[index:index+2],'little'))*16777619)&0xffffffff
    return value if value<0x80000000 else value-0x100000000


def validate_solo_identity(folder, expected, encounter, difficulty, seed):
    """Additive new-cohort gate; inspect_peer remains unchanged for legacy callers."""
    problems=[]
    try:
        summary=json.loads((folder/'summary.json').read_text(encoding='utf-8-sig'))
        actual=json.loads((folder/'solo-identity.json').read_text(encoding='utf-8-sig'))
        if not isinstance(actual,dict) or not isinstance(summary,dict):raise ValueError('identity object expected')
    except (OSError,ValueError):return ['missing or malformed Solo identity evidence']
    if summary.get('schema')!=3:problems.append('Solo identity summary schema missing')
    for key,value in [('requestedEncounterId',encounter),('resolvedEncounterId',encounter),
                      ('expectedDifficultyId',difficulty),('resolvedDifficultyId',difficulty)]:
        if summary.get(key)!=value:problems.append('summary '+key+' mismatch')
    for key,value in [('requestedEncounterId',encounter),('expectedDifficultyId',difficulty),
                      ('encounterId',encounter),('difficultyId',difficulty),('humanDriverSeed',seed)]:
        if actual.get(key)!=value:problems.append('identity '+key+' mismatch')
    if type(actual.get('generation')) is not int or actual['generation']<=0:problems.append('invalid Solo session generation')
    for key in SOLO_EVIDENCE_FIELDS:
        if key not in expected or key not in actual or not equal_effective_value(actual[key],expected[key]):
            problems.append('effective Solo setting missing/mismatch: '+key)
    if actual.get('validationFixture') is not False:problems.append('validation fixture cannot certify natural Solo')
    log=(folder/'player.log').read_text(encoding='utf-8-sig',errors='replace') if (folder/'player.log').exists() else ''
    starts=re.findall(r'^\[SoloBT\] g=(\d+) r=(\d+) t=(\d+) seq=(\d+) time=[\d.]+ start seed=(-?\d+) difficulty=(\S+)',log,re.M)
    if not starts or any(int(x[0])!=actual.get('generation') or x[5]!=difficulty for x in starts):
        problems.append('actual bot decision seed/session/difficulty trace missing or mismatched')
    if any(int(x[4])!=solo_decision_seed(actual.get('botId',''),int(x[0]),int(x[1]),int(x[2])) for x in starts):
        problems.append('actual bot decision seed does not match stable key derivation')
    pending=re.findall(r'^\[SoloBT\] .* submit Pending:.* request=(\d+) operation=(\d+) copy=(\d+) target=(\d+)',log,re.M)
    if not pending or any(any(int(v)<=0 for v in x[:3]) for x in pending):
        problems.append('actual bot request/operation/copy identity evidence missing')
    return problems


def load_solo_expectation(path, fingerprints, encounter, difficulty):
    manifest=json.loads(path.read_text(encoding='utf-8-sig'))
    if manifest.get('kind')!='solo-bot-integrated-build' or manifest.get('development') is not True:
        raise ValueError('explicit integrated development build manifest required')
    expected_hashes=manifest.get('build_hashes',{})
    if not expected_hashes or set(expected_hashes)!=set(fingerprints) or any(expected_hashes.get(k)!=v for k,v in fingerprints.items()):
        raise ValueError('runtime build does not match the certified build manifest')
    profiles=manifest.get('profiles')
    if not isinstance(profiles,list):raise ValueError('profile expectations missing')
    matches=[p for p in profiles if isinstance(p,dict) and p.get('encounterId')==encounter]
    if len(matches)!=1 or matches[0].get('difficultyId')!=difficulty:
        raise ValueError('requested profile absent/ambiguous/mismatched in build manifest')
    if any(key not in matches[0] for key in SOLO_EVIDENCE_FIELDS):raise ValueError('incomplete effective setting expectations')
    return matches[0]


def qualify_solo_cohort(records):
    seen={key:set() for key in ('case_id','attempt_id','source')}
    problems=[] if records else ['no selected Solo attempts']
    for row in records:
        if row.get('passed') is not True or row.get('identity_problems'):
            problems.append('selected Solo attempt did not pass all identity/runtime gates')
        for key in seen:
            value=row.get(key)
            if not isinstance(value,str) or not value or value in seen[key]:
                problems.append('missing or duplicate selected Solo '+key)
            seen[key].add(value if isinstance(value,str) else '')
    return {'passed':not problems,'problems':sorted(set(problems)),'selected':len(records)}


def run(args):
    root = args.output.resolve()
    root.mkdir(parents=True, exist_ok=False)
    exe = args.exe.resolve()
    build_root = exe.parent
    fingerprints = {str(p.relative_to(build_root)): digest(p) for p in build_root.rglob('*')
                    if p.is_file() and (p.name == exe.name or p.name.startswith('AbsoluteZero.') and p.suffix == '.dll')}
    encounter=getattr(args,'solo_encounter_id',None)
    difficulty=getattr(args,'solo_difficulty_id',None)
    build_manifest=getattr(args,'solo_build_manifest',None)
    explicit_solo=any(x is not None for x in (encounter,difficulty,build_manifest))
    expectation=None
    if explicit_solo:
        if args.players!=1 or not all((encounter,difficulty,build_manifest)):
            raise ValueError('explicit Solo requires one local player and encounter/difficulty/build manifest')
        if getattr(args,'trial_kills',None) is not None or getattr(args,'trial_rounds',None) is not None:
            raise ValueError('new Solo profile cohort cannot override shared rules')
        for path in build_root.rglob('*'):
            if path.is_file() and path.suffix in ('.assets','.resS','.resource'):
                fingerprints[str(path.relative_to(build_root))]=digest(path)
        expectation=load_solo_expectation(build_manifest,fingerprints,encounter,difficulty)
    repository = Path(__file__).resolve().parents[2]
    sources = [p for directory in ('Assets/Scripts', 'Assets/Data', 'Assets/Scenes', 'Packages', 'ProjectSettings')
               for p in (repository / directory).rglob('*')
               if p.is_file() and p.suffix in ('.cs', '.asset', '.unity', '.json', '.meta', '.txt')]
    source_hashes = {str(p.relative_to(repository)): digest(p) for p in sorted(sources)}
    (root / 'source-hashes.json').write_text(json.dumps(source_hashes, indent=2), encoding='utf-8')
    cohort = getattr(args, 'cohort', 'instrumentation-preflight')
    manifest = dict(schema=1, kind=cohort, players=args.players,
                    policies=args.policies, seed=args.seed, seed_scope='policy only' if args.gameplay_seed is None else 'policy and isolated gameplay streams',
                    gameplay_seed=args.gameplay_seed,
                    behavior=args.behavior,
                    transport='local NGO' if args.players == 1 else 'Lobby/Relay',
                    build_hashes=fingerprints, primary_counted=False, time_scale=1,
                    timeout=args.timeout, resolution=[1280, 720], frame_cap=30,
                    causal_attribution_certified=False)
    if explicit_solo:
        manifest['case_id']=getattr(args,'case_id',None) or root.name
        manifest['attempt_id']=getattr(args,'attempt_id',None) or uuid.uuid4().hex
    manifest['source_snapshot_hash'] = digest(root / 'source-hashes.json')
    manifest['eligible_primary'] = cohort == 'primary' and args.behavior == 'normal'
    visible = getattr(args, 'visible', False)
    manifest['capture_mode'] = 'framebuffer' if visible else 'diagnostic camera re-render; overlay ordering requires framebuffer verification'
    manifest['ready_delay_override'] = getattr(args, 'ready_delay', None)
    manifest['trial_kills'] = getattr(args, 'trial_kills', None)
    manifest['trial_rounds'] = getattr(args, 'trial_rounds', None)
    manifest['policy_version'] = '2; seat-seeded ties; Ready seconds attacker=1.25 resource=1.75 survival=2 balanced/utility=2.5 cautious=5'
    manifest['source_snapshot_note'] = 'launch-time working tree, not proof that every source edit is in this executable; build hashes identify runtime'
    if explicit_solo:
        manifest['solo_request']={'encounter':encounter,'difficulty':difficulty,'build_manifest':str(build_manifest.resolve()),
                                  'build_manifest_sha256':digest(build_manifest),'expected':expectation}
    (root / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    processes = []
    token = uuid.uuid4().hex[:10]
    failure = None
    started = time.monotonic()
    try:
        for peer in range(args.players):
            folder = root / str(peer)
            folder.mkdir()
            info = subprocess.STARTUPINFO()
            info.dwFlags |= subprocess.STARTF_USESHOWWINDOW
            info.wShowWindow = 0
            command = [str(exe)] + ([] if visible else ['-batchmode']) + ['-force-d3d11', '-screen-fullscreen', '0',
                       '-screen-width', '1280', '-screen-height', '720',
                       '-logFile', str(folder / 'player.log'), '--az-playtest', str(folder),
                       '--az-playtest-drive', '1', '--az-playtest-peer', str(peer),
                       '--az-playtest-players', str(args.players), '--az-playtest-seed', str(args.seed + peer),
                       '--az-playtest-policy', args.policies[peer % len(args.policies)],
                       '--az-playtest-lineup', ','.join(args.policies),
                       '--az-playtest-behavior', args.behavior,
                       '--az-playtest-cohort', cohort,
                       '--az-playtest-timeout', str(args.timeout), '--az-services-profile', f'azp-{token}-{peer}']
            if getattr(args, 'ready_delay', None) is not None:
                command += ['--az-playtest-ready-delay', str(args.ready_delay)]
            if explicit_solo:
                command += ['--az-solo-encounter-id',encounter,'--az-solo-difficulty-id',difficulty]
            if args.players==1 and getattr(args,'solo_local_port',None) is not None:
                command += ['--az-solo-local-port',str(args.solo_local_port)]
            if getattr(args,'record_video',False):
                if not visible or not getattr(args,'ffmpeg',None) or not args.ffmpeg.is_file():
                    raise ValueError('recording requires visible rendering and an existing ffmpeg')
                command += ['--az-record-video',str(folder/'raw-video.mp4'),'--az-ffmpeg',str(args.ffmpeg.resolve())]
            if args.gameplay_seed is not None:
                command += ['--az-gameplay-seed', str(args.gameplay_seed)]
            if getattr(args, 'trial_kills', None) is not None:
                command += ['--az-trial-kills', str(args.trial_kills), '--az-trial-rounds', str(args.trial_rounds)]
            if visible:
                command += ['--plan036-framebuffer-captures']
            (folder/'command.json').write_text(json.dumps(command,indent=2),encoding='utf-8')
            processes.append(subprocess.Popen(command, startupinfo=None if visible else info))
        deadline = started + args.timeout + 210
        while any(p.poll() is None for p in processes):
            if time.monotonic() > deadline:
                failure = 'outer timeout'
                break
            if any(p.poll() not in (None, 0) for p in processes):
                failure = 'peer failed; stop remaining peers, retain evidence'
                break
            time.sleep(1)
    except Exception as error:
        failure = 'runner: ' + type(error).__name__ + ': ' + str(error)
    finally:
        for process in processes:
            if process.poll() is None:
                process.terminate()
            try:
                process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=10)
                failure = failure or 'owned test process required forced termination'
        coordination = root / 'lobby.tmpdata'
        if coordination.exists():
            coordination.unlink()
    peers = []
    for i in range(args.players):
        try:
            peers.append(inspect_peer(root / str(i)))
            if explicit_solo:
                identity_errors=validate_solo_identity(root/str(i),expectation,encounter,difficulty,args.seed)
                peers[-1]['solo_identity_problems']=identity_errors
                peers[-1]['problems']+=identity_errors
                peers[-1]['passed']=peers[-1]['passed'] and not identity_errors
        except Exception as error:
            peers.append(dict(passed=False, problems=['analyzer: ' + type(error).__name__], events=0))
    comparison = compare_terminals(peers)
    result = dict(passed=failure is None and all(p.poll() == 0 for p in processes)
                  and all(p['passed'] for p in peers) and comparison['status'] == 'passed',
                  failure=failure, seconds=time.monotonic()-started, exits=[p.returncode for p in processes],
                  primary_counted=False, peers=peers, terminal_comparison=comparison)
    result['cohort'] = cohort
    result['analyzer_source_hash_at_import'] = ANALYZER_FINGERPRINT
    result['primary_counted'] = cohort == 'primary' and result['passed'] and args.behavior == 'normal'
    (root / 'report.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(passed=result['passed'], output=str(root), seconds=result['seconds'],
                         problems=[p['problems'] for p in peers]), ensure_ascii=False), flush=True)
    return 0 if result['passed'] else 2


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--exe', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--players', type=int, choices=[1, 2, 3, 4], default=1)
    parser.add_argument('--seed', type=int, default=6601)
    parser.add_argument('--gameplay-seed', type=int)
    parser.add_argument('--behavior', choices=['normal', 'idle'], default='normal')
    parser.add_argument('--policies', nargs='+', default=['balanced'])
    parser.add_argument('--timeout', type=int, default=900)
    parser.add_argument('--ready-delay',type=float,help='Test human Ready delay only; real BT and game timers unchanged')
    parser.add_argument('--visible', action='store_true', help='Use a visible player and actual framebuffer captures for visual qualification')
    parser.add_argument('--trial-kills', type=int, choices=range(1, 6))
    parser.add_argument('--trial-rounds', type=int, choices=range(1, 6))
    parser.add_argument('--cohort',default='instrumentation-preflight')
    parser.add_argument('--solo-encounter-id')
    parser.add_argument('--solo-difficulty-id')
    parser.add_argument('--solo-build-manifest',type=Path)
    parser.add_argument('--solo-local-port',type=int)
    parser.add_argument('--record-video',action='store_true')
    parser.add_argument('--ffmpeg',type=Path)
    parser.add_argument('--case-id')
    parser.add_argument('--attempt-id')
    args = parser.parse_args()
    if (args.trial_kills is None) != (args.trial_rounds is None) or (args.trial_kills is not None and args.players != 4):
        parser.error('Balance trials require four players and both --trial-kills and --trial-rounds')
    raise SystemExit(run(args))
