"""Reject incomplete I05 evidence rather than accepting a bare passed flag."""
import copy
import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

spec=importlib.util.spec_from_file_location('fixture_runner',Path(__file__).with_name('run_solo_integration_fixture.py'))
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)


def report(case):
    return {'passed':True,'failure':None,'errors':[],
        'scope':'I05 unmocked production graph/session; explicit controlled initial state; case='+case,
        'checks':['normal development launch resolves requested production encounter',
            'real local host has two distinct live participant bindings',
            'actual loopback transport uses the requested development port',
            'controller and graph are released on normal lobby return',*runner.REQUIRED[case]]}


class EvidenceTests(unittest.TestCase):
    def test_case_contracts(self):
        self.assertEqual(set(runner.CASES),set(runner.REQUIRED))
        source=Path(__file__).resolve().parents[2]/'Assets/Tests/Plan036Actions/Runtime/Plan036ActionProbe.SoloIntegration.cs'
        text=source.read_text(encoding='utf-8-sig')
        for case in runner.CASES:
            with self.subTest(case=case):
                self.assertIn('"'+case+'"',text)
                self.assertFalse(runner.check_report(case,report(case),0,''))
                for check in runner.REQUIRED[case]:
                    bad=report(case);bad['checks'].remove(check)
                    self.assertTrue(runner.check_report(case,bad,0,''))

    def test_bad_report(self):
        for changes in ({'passed':False},{'passed':1},{'failure':'timeout'},
                        {'errors':['runtime error']},{'scope':'old fixture'},
                        {'checks':[]},{'checks':'passed'},{'checks':[42]}):
            with self.subTest(changes=changes):
                bad=report('mini-failure');bad.update(changes)
                self.assertTrue(runner.check_report('mini-failure',bad,0,''))

    def test_exit_and_late_exception(self):
        self.assertTrue(runner.check_report('mini-failure',report('mini-failure'),1,''))
        for log in ('NullReferenceException: cleanup','Unity.Netcode.NetworkException: late','Fatal error. crash'):
            with self.subTest(log=log):
                self.assertTrue(runner.check_report('mini-failure',report('mini-failure'),0,log))

    def test_trace_identity_and_reentry(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder=Path(temporary)
            self.assertTrue(runner.check_traces('pending-session-exit',folder))
            first=folder/'pending-session-exit-g1-bot-trace.txt'
            first.write_text('start seed=4 difficulty=advanced',encoding='utf-8')
            self.assertTrue(runner.check_traces('pending-session-exit',folder))
            second=folder/'pending-session-exit-g2-bot-trace.txt'
            second.write_text('start seed=5 difficulty=advanced',encoding='utf-8')
            self.assertFalse(runner.check_traces('pending-session-exit',folder))
            second.write_text('',encoding='utf-8')
            self.assertTrue(runner.check_traces('pending-session-exit',folder))

    def test_video_metadata(self):
        with tempfile.TemporaryDirectory() as temporary:
            folder=Path(temporary)
            self.assertTrue(runner.check_video(folder))
            (folder/'test-video.mp4').write_bytes(b'fixture metadata only')
            capture=folder/'test-video.mp4.capture.txt'
            good='source=Unity framebuffer\ncaptured=80\nencoded=80\nqueueDropped=0\nerror=\n'
            capture.write_text(good,encoding='utf-8')
            self.assertFalse(runner.check_video(folder))
            for bad in (good.replace('80','0'),good.replace('80','invalid'),
                        good.replace('queueDropped=0','queueDropped=1'),
                        good.replace('error=','error=encoder failure'),good.replace('Unity framebuffer','synthetic')):
                with self.subTest(bad=bad):
                    capture.write_text(bad,encoding='utf-8')
                    self.assertTrue(runner.check_video(folder))


class PortGuardTests(unittest.TestCase):
    def test_invalid_ports(self):
        for port in (0,1023,65536,-1):
            self.assertIsNotNone(runner.port_guard(port))

    def test_real_occupied_loopback_port(self):
        import socket
        with socket.socket(socket.AF_INET,socket.SOCK_DGRAM) as owner:
            if hasattr(socket,'SO_EXCLUSIVEADDRUSE'):
                owner.setsockopt(socket.SOL_SOCKET,socket.SO_EXCLUSIVEADDRUSE,1)
            owner.bind(('127.0.0.1',0))
            port=owner.getsockname()[1]
            self.assertIsNotNone(runner.port_guard(port))
        self.assertIsNone(runner.port_guard(port))


class CollectionGuardTests(unittest.TestCase):
    def setUp(self):
        self.temporary=tempfile.TemporaryDirectory()
        self.root=Path(self.temporary.name)
        self.patches=[patch.object(runner,'BASELINE',self.root),
                      patch.object(runner,'alive',return_value=False),
                      patch.object(runner.subprocess,'check_output',return_value='0')]
        for item in self.patches:item.start()
        self.groups={name:{'modes':{mode:{'passed':10} for mode in ('Solo','Duel','Multi')}} for name in ('repeat','additional')}
        self.save('completion.json',{'complete':True,'passed':60,'series':self.groups})
        self.save('progress.json',{'complete':True,'status':'completed','runner_pid':123,'passed':60})
        manifest={'series':{name:{'cases':[]} for name in self.groups}}
        self.attempts=[]
        for name in self.groups:
            for mode in ('Solo','Duel','Multi'):
                for index in range(1,11):
                    case=mode[0]+str(index)
                    manifest['series'][name]['cases'].append({'id':case,'mode':mode,'gameplay_seed':index})
                    source=self.root/name/case;source.mkdir(parents=True)
                    row={'series':name,'case':case,'mode':mode,'source':str(source),
                         'derived':name+'/'+case+'.json','passed':True,'errors':[],'diagnostic_errors':[]}
                    self.attempts.append(row)
                    match={'id':case,'mode':mode,'source':str(source),'passed':True,'errors':[],'gameplay_seed':index}
                    self.save(row['derived'],{'record':{'match':match,'additional_observations':[]},
                                             'validation':{'strict_passed':True,'diagnostic_errors':[]}})
        self.save('recovery-manifest.json',manifest)
        self.save('attempts.json',self.attempts)

    def tearDown(self):
        for item in reversed(self.patches):item.stop()
        self.temporary.cleanup()

    def save(self,name,value):
        import json
        (self.root/name).write_text(json.dumps(value),encoding='utf-8')

    def test_exact_completion(self):
        self.assertIsNone(runner.collection_guard())

    def test_repeated_case_or_source(self):
        original=copy.deepcopy(self.attempts)
        self.attempts[-1]=copy.deepcopy(self.attempts[0]);self.save('attempts.json',self.attempts)
        self.assertIsNotNone(runner.collection_guard())
        original[-1]['source']=original[0]['source'];self.save('attempts.json',original)
        self.assertIsNotNone(runner.collection_guard())

    def test_missing_case(self):
        self.save('attempts.json',self.attempts[:-1])
        self.assertIsNotNone(runner.collection_guard())

    def test_derived_failure_or_mismatch(self):
        row=self.attempts[0]
        match={'id':row['case'],'mode':row['mode'],'source':row['source'],'passed':True,'errors':[],'gameplay_seed':1}
        self.save(row['derived'],{'record':{'match':match},'validation':{'strict_passed':False}})
        self.assertIsNotNone(runner.collection_guard())
        altered=dict(match,mode='Multi')
        self.save(row['derived'],{'record':{'match':altered},'validation':{'strict_passed':True}})
        self.assertIsNotNone(runner.collection_guard())

    def test_missing_evidence(self):
        (self.root/self.attempts[0]['derived']).unlink()
        self.assertIsNotNone(runner.collection_guard())

    def test_active_collector_or_player(self):
        with patch.object(runner,'alive',return_value=True):self.assertIsNotNone(runner.collection_guard())
        with patch.object(runner.subprocess,'check_output',return_value='1'):self.assertIsNotNone(runner.collection_guard())

    def test_incomplete_or_disagreeing_certificate(self):
        self.save('completion.json',{'complete':False,'passed':59,'series':self.groups})
        self.assertIsNotNone(runner.collection_guard())
        self.save('completion.json',{'complete':True,'passed':60,'series':self.groups})
        self.save('progress.json',{'complete':False,'status':'running','runner_pid':123,'passed':60})
        self.assertIsNotNone(runner.collection_guard())


class VideoTimelineTests(unittest.TestCase):
    def setUp(self):
        spec=importlib.util.spec_from_file_location('video_export',Path(__file__).with_name('export_solo_test_videos.py'))
        self.export=importlib.util.module_from_spec(spec);spec.loader.exec_module(self.export)
        self.temporary=tempfile.TemporaryDirectory();self.folder=Path(self.temporary.name)
        (self.folder/'test-video.mp4.capture.txt').write_text('startGameTime=2\n',encoding='utf-8')

    def tearDown(self):self.temporary.cleanup()

    def timeline(self,events):
        (self.folder/'video-timeline.tsv').write_text(''.join(f'{time}\tcase\tkey\t{label}\n' for time,label in events),encoding='utf-8')

    def test_game_clock_alignment(self):
        self.timeline([(10,'controlled Prep configured'),(14,'normal combat received'),(20,'normal presentation observed')])
        start,end,impact=self.export.clip_window(self.folder,25)
        self.assertAlmostEqual(start,7.5);self.assertAlmostEqual(end,18.2);self.assertAlmostEqual(impact,12)

    def test_missing_or_outside_gameplay(self):
        self.timeline([(10,'controlled Prep configured'),(20,'normal presentation observed')])
        with self.assertRaises(ValueError):self.export.clip_window(self.folder,25)
        self.timeline([(10,'controlled Prep configured'),(14,'normal combat received'),(20,'normal presentation observed')])
        with self.assertRaises(ValueError):self.export.clip_window(self.folder,3)

    def test_reentry_is_retained(self):
        self.timeline([(10,'controlled Prep configured'),(11,'normal session stopping'),
                       (15,'controlled Prep configured'),(20,'normal combat received'),(25,'normal presentation observed')])
        self.assertEqual(self.export.clip_window(self.folder,30)[0],7.5)


if __name__=='__main__':unittest.main()
