"""Decode recorded I05 evidence and export clips/frames; never certify human play feel."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from PIL import Image,ImageStat

LABELS={'crisis-heal':'위기_회복_우선','advanced-lethal':'상급_처치_판단',
        'mini-failure':'실패_소모_검증','active-fan-delay':'선풍기_냉각_사용_지연',
        'pending-session-exit':'퇴장_재입장_검증'}


def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))


def command(arguments,report):
    result=subprocess.run(arguments,capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=120)
    report.write_text(json.dumps({'command':arguments,'exit':result.returncode,'stderr':result.stderr},indent=2),encoding='utf-8')
    if result.returncode:raise RuntimeError('Media command failed; see '+str(report))
    return result.stdout


def clip_window(folder,duration):
    capture=dict(line.split('=',1) for line in (folder/'test-video.mp4.capture.txt').read_text().splitlines() if '=' in line)
    offset=float(capture['startGameTime'])
    events=[]
    for line in (folder/'video-timeline.tsv').read_text(encoding='utf-8-sig').splitlines():
        parts=line.split('\t')
        if len(parts)==4:events.append((float(parts[0])-offset,parts[-1]))
    starts=[t for t,label in events if label=='controlled Prep configured']
    ends=[t for t,label in events if label=='normal presentation observed']
    impacts=[t for t,label in events if label=='normal combat received']
    if not starts or not ends or not impacts:raise ValueError('Recorded gameplay timeline incomplete')
    start=max(0,starts[0]-.5);end=min(duration,ends[-1]+.2)
    if end-start<2 or start>=duration:raise ValueError('Gameplay clip has no usable duration')
    return start,end,impacts[-1]


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--input',type=Path,required=True)
    parser.add_argument('--output',type=Path,required=True)
    args=parser.parse_args()
    root=args.input.resolve();output=args.output.resolve()
    rows=read(root/'results.json')
    if not rows or any(row.get('passed') is not True for row in rows):
        raise SystemExit('Only successful fixture evidence is exported by this command; failed runs remain diagnostic originals')
    media=read(root/'media-request.json')
    ffmpeg=Path(media['ffmpeg']);ffprobe=ffmpeg.with_name('ffprobe.exe')
    if not ffmpeg.is_file() or not ffprobe.is_file():raise SystemExit('Exact capture toolchain missing')
    output.mkdir(parents=True,exist_ok=False)
    videos=output/'01_영상';screenshots=output/'02_스크린샷';checks=output/'03_검증기록'
    for folder in (videos,screenshots,checks):folder.mkdir()
    summaries=[]
    try:
        for index,row in enumerate((r for r in rows if r.get('recorded') is True),1):
            case=row['case'];folder=Path(row['source']);source=folder/'test-video.mp4'
            metadata=json.loads(command([str(ffprobe),'-v','error','-select_streams','v:0','-show_entries',
                'stream=width,height,nb_frames,r_frame_rate:format=duration','-of','json',str(source)],checks/(case+'-probe.json')))
            duration=float(metadata['format']['duration']);stream=metadata['streams'][0]
            if duration<2 or stream['width']<640 or stream['height']<360:raise ValueError('Recording size/duration unusable: '+case)
            command([str(ffmpeg),'-v','error','-i',str(source),'-f','null','-'],checks/(case+'-decode.json'))
            start,end,impact=clip_window(folder,duration)
            label=f'{index:02d}_'+LABELS.get(case,case)
            movie=videos/(label+'.mp4')
            command([str(ffmpeg),'-hide_banner','-loglevel','error','-ss',str(start),'-i',str(source),
                '-t',str(end-start),'-an','-c:v','libx264','-preset','veryfast','-threads','2','-crf','20',
                '-pix_fmt','yuv420p','-movflags','+faststart',str(movie)],checks/(case+'-clip.json'))
            command([str(ffmpeg),'-v','error','-i',str(movie),'-f','null','-'],checks/(case+'-clip-decode.json'))
            images=[]
            for number,t in enumerate((start+1,min(end-.3,impact+.5),end-.5),1):
                path=screenshots/(label+f'_{number:02d}.png')
                command([str(ffmpeg),'-v','error','-ss',str(t),'-i',str(source),'-frames:v','1',str(path)],checks/(case+f'-frame{number}.json'))
                with Image.open(path) as picture:
                    statistics=ImageStat.Stat(picture.convert('RGB'))
                    blank=max(statistics.stddev)<2 or max(statistics.mean)<3
                images.append({'path':str(path),'time_in_original':t,'blank':blank})
            if all(frame['blank'] for frame in images):raise ValueError('All sampled gameplay frames are blank: '+case)
            summaries.append({'case':case,'label':LABELS.get(case,case),'fixture_passed':True,
                'source':str(source),'source_sha256':hashlib.sha256(source.read_bytes()).hexdigest(),
                'video':str(movie),'original_seconds':duration,'clip_seconds':end-start,
                'width':stream['width'],'height':stream['height'],'screenshots':images,
                'full_decode_passed':True,'natural_match':False,'human_visual_review':'pending'})
            (output/'media-review.json').write_text(json.dumps({'complete':False,'videos':summaries},ensure_ascii=True,indent=2),encoding='utf-8')
        if not summaries:raise ValueError('No requested recordings found')
        result={'complete':True,'scope':'controlled I05 scenarios, not natural-match certification',
                'human_visual_review':'pending','videos':summaries}
        (output/'media-review.json').write_text(json.dumps(result,ensure_ascii=True,indent=2),encoding='utf-8')
        (output/'먼저_읽어주세요.txt').write_text('솔로 봇 통제 검증 영상입니다.\n초기 온도와 아이템을 지정한 테스트이며 자연 경기 완주 영상은 별도입니다.\n01_영상: 게임 구간 영상 / 02_스크린샷: 주요 장면 / 03_검증기록: 디코딩 검사\n영상은 무음이며 개발 빌드 표시가 포함됩니다.\n프레임과 실제 모션의 육안 검토는 아직 남아 있습니다.\n',encoding='utf-8')
        print(json.dumps({'complete':True,'videos':len(summaries),'output':str(output)},ensure_ascii=True))
        return 0
    except Exception as error:
        (output/'media-review.json').write_text(json.dumps({'complete':False,'error':str(error),'videos':summaries},ensure_ascii=True,indent=2),encoding='utf-8')
        raise


if __name__=='__main__':raise SystemExit(main())
