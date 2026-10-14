"""One-shot I05 continuation. Wait for the protected campaign; stop at the first failure."""
import argparse
from datetime import datetime,timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

ROOT=Path(__file__).resolve().parents[2]
CLI=Path('C:/Users/paek6/AppData/Local/Unity/bin/unity.exe')
spec=importlib.util.spec_from_file_location('i05_fixture',Path(__file__).with_name('run_solo_integration_fixture.py'))
fixture=importlib.util.module_from_spec(spec);spec.loader.exec_module(fixture)


def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def save(path,value):
    temporary=path.with_suffix(path.suffix+'.tmp')
    temporary.write_text(json.dumps(value,ensure_ascii=True,indent=2),encoding='utf-8')
    os.replace(temporary,path)


def verify_frozen(manifest):
    for name,expected in manifest['fingerprints'].items():
        if digest(Path(name))!=expected:raise RuntimeError('Protected fingerprint drift: '+name)


def cli(folder,name,*arguments):
    command=[str(CLI),*arguments,'--project-path',str(ROOT),'--format','json']
    result=subprocess.run(command,cwd=ROOT,capture_output=True,text=True,encoding='utf-8',timeout=45)
    save(folder/(name+'-command.json'),command)
    (folder/(name+'-stdout.json')).write_text(result.stdout,encoding='utf-8')
    (folder/(name+'-stderr.txt')).write_text(result.stderr,encoding='utf-8')
    response=json.loads(result.stdout)
    if result.returncode or response.get('success') is not True:
        raise RuntimeError('Unity CLI failed: '+name+'; '+str(response.get('errors')))
    return response['data']


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--output',type=Path,required=True)
    parser.add_argument('--execute-after-collection',action='store_true')
    args=parser.parse_args()
    if not args.execute_after_collection:parser.error('explicit --execute-after-collection is required')
    root=args.output.resolve()
    allowed=(ROOT/'output/solo-bot-integration-20261013').resolve()
    if allowed not in root.parents:parser.error('output must be a new folder under the integration evidence root')
    if os.path.normcase(str(ROOT))!=os.path.normcase('C:\\Users\\paek6\\AbsoluteZero-SoloIntegration'):
        raise RuntimeError('Unexpected integration root')
    import msvcrt
    lease_path=allowed/'continuation.lock';lease_path.parent.mkdir(parents=True,exist_ok=True)
    lease=lease_path.open('a+b');lease.seek(0)
    try:msvcrt.locking(lease.fileno(),msvcrt.LK_NBLCK,1)
    except OSError:raise SystemExit('Another continuation owns the I05 process lock')
    root.mkdir(parents=True,exist_ok=False)
    state={'pid':os.getpid(),'root':str(root),'stage':'I05','score':90,'runtime_passed':False,
           'started_utc':datetime.now(timezone.utc).isoformat()}

    def update(phase,**details):
        state.update(phase=phase,updated_utc=datetime.now(timezone.utc).isoformat(),**details)
        save(root/'state.json',state)
        print(json.dumps(state),flush=True)

    try:
        update('waiting-for-protected-collection')
        while True:
            reason=fixture.collection_guard()
            progress=fixture.read(fixture.BASELINE/'progress.json')
            if reason is None:break
            if progress.get('status') not in ('running','completed'):
                raise RuntimeError('Protected collection stopped: '+str(progress.get('reason')))
            if progress.get('status')=='running' and not fixture.alive(progress['runner_pid']):
                raise RuntimeError('Protected collector unexpectedly exited before certification')
            if state.get('collection_passed')!=progress.get('passed') or state.get('waiting_reason')!=reason:
                update('waiting-for-protected-collection',collection_passed=progress.get('passed'),waiting_reason=reason)
            if progress.get('status')=='completed' and reason not in ('Frozen collector process remains alive','An existing player process remains alive'):
                raise RuntimeError(reason)
            time.sleep(15)

        manifest=fixture.read(fixture.BASELINE/'recovery-manifest.json')
        verify_frozen(manifest)
        editor=cli(root,'editor-status','command','editor_status')['result']
        if Path(editor['projectPath']).resolve()!=ROOT or editor.get('playMode')!='stopped' or editor.get('compiling'):
            raise RuntimeError('Integration Editor is not verified and idle')
        update('backing-up-frozen-player',collection_passed=60,waiting_reason=None)
        backup=root/'frozen-player'
        shutil.copytree(fixture.EXE.parent,backup)
        copies={}
        for name,expected in manifest['fingerprints'].items():
            source=Path(name)
            if fixture.EXE.parent.resolve() in source.resolve().parents:
                target=backup/source.relative_to(fixture.EXE.parent)
                if digest(target)!=expected:raise RuntimeError('Frozen backup fingerprint mismatch: '+name)
                copies[str(target)]=expected
        save(root/'frozen-backup.json',{'original_fingerprints':manifest['fingerprints'],'verified_backup_fingerprints':copies})
        if fixture.collection_guard() is not None:raise RuntimeError('Collector/player reappeared before build')
        verify_frozen(manifest)

        build_source=root/'build-integrated-player.cs'
        build_source.write_text('''
if (System.IO.Path.GetFullPath(Application.dataPath) != System.IO.Path.GetFullPath("C:/Users/paek6/AbsoluteZero-SoloIntegration/Assets"))
    throw new InvalidOperationException("Wrong Editor project");
var options = new UnityEditor.BuildPlayerOptions {
    scenes = UnityEditor.EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
    locationPathName = "C:/Users/paek6/AppData/Local/Temp/AZVisual4P_Current/Player/AbsoluteZeroVisual4P.exe",
    target = UnityEditor.BuildTarget.StandaloneWindows64,
    options = UnityEditor.BuildOptions.Development | UnityEditor.BuildOptions.DetailedBuildReport
};
var report = UnityEditor.BuildPipeline.BuildPlayer(options);
if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded || report.summary.totalErrors != 0)
    throw new InvalidOperationException("Integrated build failed: " + report.summary.result + ";errors=" + report.summary.totalErrors);
return "Succeeded|errors=" + report.summary.totalErrors + "|warnings=" + report.summary.totalWarnings + "|bytes=" + report.summary.totalSize;
''',encoding='utf-8')
        update('building-integrated-player')
        submitted=cli(root,'build-dispatch','command','eval_file',str(build_source),'--detach')
        job=submitted['jobId'];update('building-integrated-player',build_job=job)
        deadline=time.monotonic()+1800
        while True:
            status=cli(root,'build-status','job','status',job)
            if status['state']=='completed':
                save(root/'build-final.json',status)
                if not status.get('result',{}).get('success'):raise RuntimeError('Integrated build evaluation failed')
                break
            if status['state'] in ('failed','cancelled') or time.monotonic()>deadline:
                raise RuntimeError('Integrated build did not complete: '+status['state'])
            time.sleep(10)

        # Source collection tools are still immutable; runtime replacement is now permitted.
        for name,expected in manifest['fingerprints'].items():
            if fixture.EXE.parent.resolve() not in Path(name).resolve().parents and digest(Path(name))!=expected:
                raise RuntimeError('Frozen observation tool changed during integrated build: '+name)
        update('running-I05-fixtures',runtime_hashes=fixture.fingerprint())
        command=[sys.executable,str(Path(__file__).with_name('run_solo_integration_fixture.py')),
                 '--output',str(root/'fixtures')]
        save(root/'fixtures-command.json',command)
        with (root/'fixtures-console.txt').open('w',encoding='utf-8') as output:
            info=subprocess.STARTUPINFO();info.dwFlags|=subprocess.STARTF_USESHOWWINDOW;info.wShowWindow=0
            process=subprocess.Popen(command,cwd=ROOT,stdout=output,stderr=subprocess.STDOUT,startupinfo=info)
            update('running-I05-fixtures',fixture_runner_pid=process.pid)
            code=process.wait()
        results=fixture.read(root/'fixtures/results.json')
        if code!=0 or len(results)!=len(fixture.CASES) or any(row.get('passed') is not True for row in results):
            raise RuntimeError('I05 fixture failure; preserve the first failed attempt and debug before rerun')
        update('I05-fixtures-completed-awaiting-agent-review',runtime_passed=True,passed=len(results))
        # The agent scores the full checkpoint and implements I06 only after approval evidence.
        return 0
    except Exception as error:
        update('failed-requires-agent-debugging',error=str(error))
        return 2
    finally:
        lease.close()


if __name__=='__main__':raise SystemExit(main())
