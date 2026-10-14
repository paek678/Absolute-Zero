"""I05 real-player cases; never launch before the frozen collector has exited."""
import argparse
import ctypes
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import time

CASES=('crisis-heal','crisis-defense','advanced-lethal','beginner-choice','no-care-ready',
       'retry-exhaustion','pending-early-ready','mini-failure','copy-compaction','deadline',
       'active-fan-delay','active-fan-no-care','pending-target-death','pending-owner-death','pending-session-exit')
REQUIRED={
 'crisis-heal':('actual crisis guard selects Warm Tea',),
 'crisis-defense':('actual crisis guard selects Windbreaker',),
 'advanced-lethal':('safe advanced graph selects the lethal item',),
 'beginner-choice':('beginner graph uses weighted choice','beginner actually queued a nonlethal alternative'),
 'no-care-ready':('crisis without a response ends with no-item Ready',),
 'retry-exhaustion':('real open authoritative window exhausts exactly the configured retry budget',),
 'pending-early-ready':('opponent Ready uses the ordinary human RPC','existing operation completes early'),
 'mini-failure':('real bot mini-game failure consumes once','failure replay and polling neither reroll'),
 'copy-compaction':("actual command resolves the compacted copy's current slot",),
 'deadline':('late but current request admits before the real deadline','closed real input window cannot queue or debit'),
 'active-fan-delay':('real Prep fan ticks cool during the admitted delay',),
 'active-fan-no-care':('real active-fan survival bound chooses Ready before a fatal item delay',),
 'pending-target-death':('pending operation cancels on actual participant death','dead participant cannot cause a fallback Ready'),
 'pending-owner-death':('pending operation cancels on actual participant death','dead participant cannot cause a fallback Ready'),
 'pending-session-exit':('normal session exit begins with genuinely pending graph work',
     'normal session disposal cancels pending work','normal re-entry creates a fresh binding',
     'old session poll and Ready cannot mutate the new participant')
}
BASELINE=Path('C:/Users/paek6/Absolute Zero/output/balance60-20261013-recovery')
EXE=Path('C:/Users/paek6/AppData/Local/Temp/AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe')
MEDIA_REQUEST=Path(__file__).resolve().parents[2]/'output/solo-bot-integration-20261013/i05-media-request.json'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def alive(pid):
    kernel=ctypes.WinDLL('kernel32',use_last_error=True)
    kernel.OpenProcess.restype=ctypes.c_void_p
    kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong]
    kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x100000,False,int(pid))
    if not handle:return False
    try:return kernel.WaitForSingleObject(handle,0)==258
    finally:kernel.CloseHandle(handle)


def collection_guard():
    completion=read(BASELINE/'completion.json')
    progress=read(BASELINE/'progress.json')
    if completion.get('complete') is not True or completion.get('passed')!=60:
        return 'Frozen collection incomplete: '+str(progress.get('passed'))+'/60'
    if progress.get('status')!='completed' or progress.get('complete') is not True:
        return 'Frozen completion and live status disagree'
    series=completion.get('series',{})
    if set(series)!={'repeat','additional'}:
        return 'Missing or unexpected collection series'
    for group in series.values():
        modes=group.get('modes',{})
        if set(modes)!={'Solo','Duel','Multi'} or any(v.get('passed')!=10 for v in modes.values()):
            return 'Collection mode counts do not certify ten passing cases each'
    manifest=read(BASELINE/'recovery-manifest.json')
    attempts=read(BASELINE/'attempts.json')
    planned={(name,case['id'],case['mode']):case for name,group in manifest['series'].items() for case in group['cases']}
    expected=set(planned)
    passing=[row for row in attempts if row.get('passed') is True]
    actual=[(row.get('series'),row.get('case'),row.get('mode')) for row in passing]
    if len(expected)!=60 or len(actual)!=60 or len(set(actual))!=60 or set(actual)!=expected:
        return 'Collection does not contain exactly sixty distinct planned passing conditions'
    if len({row.get('source') for row in passing})!=60:
        return 'Collection reuses source evidence across passing conditions'
    for row in passing:
        if row.get('errors') or row.get('diagnostic_errors') or not Path(row['source']).is_dir():
            return 'Collection pass has missing source evidence or errors'
        derived=BASELINE/row['derived']
        if not derived.is_file():
            return 'Collection derived evidence is missing'
        evidence=read(derived)
        # The collector intentionally stores descriptive data in record.match;
        # attempts.json is a separate orchestration envelope, not that schema.
        record=evidence.get('record',{}).get('match',{})
        validation=evidence.get('validation',{})
        case=planned[(row['series'],row['case'],row['mode'])]
        if (record.get('id')!=row['case'] or record.get('mode')!=row['mode']
            or record.get('source')!=row['source'] or record.get('passed') is not True
            or record.get('errors') or record.get('gameplay_seed')!=case['gameplay_seed']
            or validation.get('strict_passed') is not True or validation.get('diagnostic_errors')):
            return 'Collection derived evidence does not match its selected attempt'
    if alive(progress.get('runner_pid',0)):
        return 'Frozen collector process remains alive'
    count=subprocess.check_output(['powershell','-NoProfile','-Command',
        "@(Get-Process -Name AbsoluteZeroVisual4P -ErrorAction SilentlyContinue).Count"],text=True).strip()
    if int(count)!=0:return 'An existing player process remains alive'
    return None


def fingerprint():
    files=[EXE]+list((EXE.parent/(EXE.stem+'_Data/Managed')).glob('AbsoluteZero.*.dll'))
    return {str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in files}


def port_guard(port):
    if not 1024 <= port <= 65535:
        return 'Development loopback port must be from 1024 to 65535'
    import socket
    try:
        with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as candidate:
            if hasattr(socket,'SO_EXCLUSIVEADDRUSE'):
                candidate.setsockopt(socket.SOL_SOCKET,socket.SO_EXCLUSIVEADDRUSE,1)
            candidate.bind(('127.0.0.1',port))
    except OSError:
        return 'Another process owns the requested loopback port '+str(port)
    return None


def check_report(case,report,exit_code,log):
    errors=[]
    if exit_code!=0:errors.append('player exit '+str(exit_code))
    if report.get('passed') is not True or report.get('failure') or report.get('errors'):
        errors.append('fixture reported failure or diagnostics')
    if report.get('scope')!='I05 unmocked production graph/session; explicit controlled initial state; case='+case:
        errors.append('wrong or missing fixture identity')
    checks=report.get('checks')
    if not isinstance(checks,list) or not checks or any(not isinstance(x,str) for x in checks):
        errors.append('missing or malformed runtime checks')
        checks=[]
    common=('normal development launch resolves requested production encounter',
            'real local host has two distinct live participant bindings',
            'actual loopback transport uses the requested development port',
            'controller and graph are released on normal lobby return')
    for required in common+REQUIRED[case]:
        if not any(required in check for check in checks):errors.append('missing required check: '+required)
    import re
    if re.search(r'^(?:\w+\.)*\w*Exception:|Fatal error\.',log,re.M):
        errors.append('player exception including teardown')
    return errors


def check_traces(case,folder):
    traces=list(folder.glob(case+'-g*-bot-trace.txt'))
    if not traces:return ['missing per-session production trace']
    if case=='deadline':return []  # This case deliberately leaves the graph disabled.
    for path in traces:
        trace=path.read_text(encoding='utf-8-sig')
        if 'start seed=' not in trace or 'difficulty=' not in trace:
            return ['missing graph start identity in '+path.name]
    if case=='pending-session-exit' and len(traces)<2:return ['missing re-entry trace']
    return []


def check_video(folder):
    capture=folder/'test-video.mp4.capture.txt'
    if not (folder/'test-video.mp4').is_file() or not capture.is_file():
        return ['requested visual evidence missing']
    fields=dict(line.split('=',1) for line in capture.read_text(encoding='utf-8-sig').splitlines() if '=' in line)
    try:
        if fields.get('source')!='Unity framebuffer' or fields.get('error') or int(fields.get('captured','0'))<20 or int(fields.get('encoded','0'))<20:
            return ['recording failed or has no usable frames']
        if int(fields.get('queueDropped','-1'))!=0:return ['recording dropped queued frames; visual review required']
    except ValueError:return ['malformed capture evidence']
    return []


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--preflight',action='store_true')
    parser.add_argument('--case',choices=CASES)
    parser.add_argument('--output',type=Path)
    parser.add_argument('--port',type=int,default=18877)
    parser.add_argument('--selection-bridge',action='store_true')
    parser.add_argument('--switch-profile',action='store_true')
    args=parser.parse_args()
    blocked=collection_guard() or port_guard(args.port)
    if args.preflight:
        print(json.dumps({'can_launch':blocked is None,'reason':blocked,'cases':list(CASES)}))
        return 0
    if blocked:raise SystemExit(blocked)
    if args.output is None:parser.error('--output is required for immutable run evidence')
    import msvcrt
    lock_path=Path(__file__).resolve().parents[2]/'output/solo-bot-integration-20261013/fixture-runner.lock'
    lock_path.parent.mkdir(parents=True,exist_ok=True)
    lease=lock_path.open('a+b');lease.seek(0)
    try:msvcrt.locking(lease.fileno(),msvcrt.LK_NBLCK,1)
    except OSError:raise SystemExit('Another integration fixture runner holds the process lock')
    root=args.output.resolve();root.mkdir(parents=True,exist_ok=False)
    # An integrated player must replace the old frozen runtime only after backup.
    old=read(BASELINE/'recovery-manifest.json')['fingerprints']
    hashes=fingerprint()
    changed=any(v!=old.get(k) for k,v in hashes.items() if k.endswith('AbsoluteZero.Core.dll'))
    if not changed:raise SystemExit('The executable still contains the pre-integration Core assembly')
    cases=[args.case] if args.case else list(CASES)
    media=read(MEDIA_REQUEST) if MEDIA_REQUEST.exists() else {'enabled':False}
    record_cases=set(media.get('cases',[])) if media.get('enabled') is True else set()
    if not record_cases.issubset(CASES):raise SystemExit('Unknown requested recording case')
    if record_cases and not Path(media.get('ffmpeg','')).is_file():raise SystemExit('Requested recording encoder missing')
    (root/'manifest.json').write_text(json.dumps({'kind':'I05-controlled-runtime-fixtures',
        'cases':cases,'runtime_hashes':hashes,'natural_matches':False,'collector_complete':True,
        'loopback_port':args.port},indent=2),encoding='utf-8')
    (root/'media-request.json').write_text(json.dumps(media,indent=2),encoding='utf-8')
    results=[]
    for case in cases:
        blocked=collection_guard() or port_guard(args.port)
        if blocked:raise SystemExit(blocked)
        if fingerprint()!=hashes:raise SystemExit('Integrated runtime drift; preserve this cohort')
        folder=root/case;folder.mkdir()
        report_path=folder/'report.json';log_path=folder/'player.log'
        command=[str(EXE),'-batchmode','-force-d3d11','-screen-fullscreen','0',
            '-screen-width','1280','-screen-height','720','-logFile',str(log_path),
            '--solo-integration-fixture','--solo-integration-case',case,'--plan036-report',str(report_path),
            '--az-solo-local-port',str(args.port)]
        if args.selection_bridge:command += ['--solo-integration-selection-bridge']
        if args.switch_profile:command += ['--solo-integration-switch-profile']
        recording=case in record_cases
        if recording:
            command.remove('-batchmode')  # WaitForEndOfFrame framebuffer capture needs normal rendering.
            command+=['--az-record-video',str(folder/'test-video.mp4'),'--az-ffmpeg',media['ffmpeg'],
                      '--solo-integration-presentation']
        (folder/'command.json').write_text(json.dumps(command,indent=2),encoding='utf-8')
        info=subprocess.STARTUPINFO();info.dwFlags|=subprocess.STARTF_USESHOWWINDOW;info.wShowWindow=1 if recording else 0
        started=time.monotonic();process=subprocess.Popen(command,startupinfo=info)
        timeout=False
        try:code=process.wait(timeout=330)
        except subprocess.TimeoutExpired:
            timeout=True;process.terminate()
            try:code=process.wait(timeout=20)
            except subprocess.TimeoutExpired:process.kill();code=process.wait(timeout=10)
        finally:
            if process.poll() is None:
                process.terminate()
                try:process.wait(timeout=20)
                except subprocess.TimeoutExpired:process.kill();process.wait(timeout=10)
        try:report=read(report_path)
        except (OSError,ValueError):report={}
        log=log_path.read_text(encoding='utf-8-sig',errors='replace') if log_path.exists() else ''
        errors=check_report(case,report,code,log)
        errors+=check_traces(case,folder)
        if recording:
            errors+=check_video(folder)
        if timeout:errors.append('owned player timeout')
        row={'case':case,'passed':not errors,'errors':errors,'exit':code,'seconds':time.monotonic()-started,
             'source':str(folder),'recorded':recording,'natural_match':False}
        results.append(row)
        (root/'results.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
        print(json.dumps(row),flush=True)
        if errors:return 2
    if any(row['recorded'] for row in results):
        export=[sys.executable,str(Path(__file__).with_name('export_solo_test_videos.py')),
                '--input',str(root),'--output',str(root/'visual-evidence')]
        with (root/'video-export-console.txt').open('w',encoding='utf-8') as console:
            code=subprocess.call(export,stdout=console,stderr=subprocess.STDOUT)
        if code:return code
    return 0


if __name__=='__main__':raise SystemExit(main())
