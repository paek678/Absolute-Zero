"""Append a frozen full-match cohort after another finishes; never overlap players."""
import argparse
import ctypes
import json
import os
from pathlib import Path
import subprocess
import sys
import time

from run_full_match_playtest import digest


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def process_alive(pid):
    kernel=ctypes.WinDLL('kernel32',use_last_error=True)
    kernel.OpenProcess.restype=ctypes.c_void_p
    kernel.WaitForSingleObject.argtypes=[ctypes.c_void_p,ctypes.c_ulong]
    kernel.CloseHandle.argtypes=[ctypes.c_void_p]
    handle=kernel.OpenProcess(0x100000,False,int(pid))
    if not handle:return False
    try:return kernel.WaitForSingleObject(handle,0)==258
    finally:kernel.CloseHandle(handle)


def prerequisite_passed(root):
    completion=read(root/'completion.json')
    data=read(root/'campaign-data.json')
    return (completion.get('complete') is True and data.get('recorded')==30
        and set(data.get('modes',{}))=={'Solo','Duel','Multi'}
        and all(v.get('recorded')==10 and v.get('passed')==10 for v in data['modes'].values()))


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('request',type=Path)
    args=parser.parse_args()
    request=read(args.request)
    root=args.request.resolve().parent
    previous=Path(request['after'])
    repo=Path(request['repository'])
    def status(value,**extra):
        (root/'progress.json').write_text(json.dumps(dict(status=value,planned=30,
            queue_pid=os.getpid(),after=str(previous),**extra),indent=2),encoding='utf-8')
    status('queued',completed=0)
    print('QUEUED after '+str(previous),flush=True)
    while True:
        try:
            progress=read(previous/'progress.json')
        except (OSError,ValueError):
            time.sleep(5);continue
        current=progress.get('status','')
        if current=='completed':
            try:
                if prerequisite_passed(previous):break
            except (OSError,ValueError):
                time.sleep(5)
                try:
                    if prerequisite_passed(previous):break
                except (OSError,ValueError):pass
            status('blocked',reason='Previous campaign did not certify thirty passing matches',completed=0)
            return 2
        if current.startswith('failed') or current in ('interrupted','blocked','checkpoint'):
            status('blocked',reason='Previous campaign stopped; retain evidence and investigate',completed=0)
            return 2
        if progress.get('runner_pid') and not process_alive(progress['runner_pid']):
            time.sleep(10)
            if read(previous/'progress.json').get('status')=='completed':continue
            status('blocked',reason='Previous runner absent before completion',completed=0)
            return 2
        time.sleep(5)
    # Wait for the prerequisite runner itself to leave before any new players start.
    for _ in range(12):
        if not process_alive(progress.get('runner_pid',0)):break
        time.sleep(5)
    else:
        status('blocked',reason='Previous runner still alive after completion',completed=0)
        return 2
    for name,expected in request['fingerprints'].items():
        path=Path(name)
        if not path.is_file() or digest(path)!=expected:
            status('blocked',reason='Frozen build or observation tool changed: '+name,completed=0)
            return 2
    command=[sys.executable,str(repo/'.codex/scripts/run_balance_30_campaign.py'),
        request['exe'],str(root),'--seed-offset',str(request['seed_offset']),'--interleave','--json-only']
    status('starting',completed=0)
    print('START additional thirty matches',flush=True)
    with (root/'runner-console.txt').open('w',encoding='utf-8') as output:
        result=subprocess.run(command,cwd=repo,stdout=output,stderr=subprocess.STDOUT)
    final={'runner_exit':result.returncode,'complete':False}
    try:final['complete']=result.returncode==0 and prerequisite_passed(root)
    except (OSError,ValueError):pass
    (root/'queue-result.json').write_text(json.dumps(final,indent=2),encoding='utf-8')
    if not final['complete']:
        try:
            state=read(root/'progress.json')
            if state.get('status') in ('starting','running'):
                status('failed',reason='Runner stopped without a successful final verdict',completed=state.get('completed',0))
        except (OSError,ValueError):status('failed',reason='Runner produced no final progress',completed=0)
    print('ADDITIONAL COMPLETE '+str(final['complete']),flush=True)
    return 0 if final['complete'] else 2


if __name__=='__main__':raise SystemExit(main())
