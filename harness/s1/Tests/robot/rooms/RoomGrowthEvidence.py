"""Check actual QA Player records independently, including both OS processes and native image dimensions."""
import hashlib
import json
import shutil
from pathlib import Path

from PIL import Image

def prepare_restart_checkpoint(original, destination):
    old=Path(original);new=Path(destination)
    assert not new.exists(),'A restart probe must use a new directory'
    first=json.loads((old/'result.json').read_text(encoding='utf-8'))
    assert first['complete'] and first['nativeHits']==1 and first['owned']==440
    (new/'CatGame').mkdir(parents=True)
    raw=(old/'CatGame/save.json').read_bytes()
    shutil.copyfile(old/'CatGame/save.json',new/'CatGame/save.json')
    (new/'fixture-save-before.json').write_bytes(raw)
    (new/'fixture.json').write_text(json.dumps(dict(originalRun=str(old),originalSource=first['source'],
        saveSha256=hashlib.sha256(raw).hexdigest(),kind='restored QA checkpoint, not user progress'),indent=2),encoding='utf-8')

def verify_restart_probe(original, folder, source):
    old=Path(original);root=Path(folder)
    first=json.loads((old/'result.json').read_text(encoding='utf-8'))
    second=json.loads((root/'restart-result.json').read_text(encoding='utf-8'))
    assert first['complete'] and first['nativeHits']==1 and first['footErrors']==first['depthErrors']==0
    assert second['complete'] and second['restart'] and second['source']==source
    assert first['pid']!=second['pid'] and second['theme']=='companions'
    assert second['owned']==440 and second['visible']==100 and second['clicks']==first['clicks']==1
    steps=[json.loads(line) for line in (root/'steps.jsonl').read_text(encoding='utf-8').splitlines()]
    assert all(s['passed'] for s in steps)
    assert any(s['message']=='fresh Player resumed playable state after offline summary' for s in steps)
    with Image.open(root/'restart-companions-1600x900.png') as picture:assert picture.size==(1600,900)
    (root/'verification.json').write_text(json.dumps(dict(passed=True,originalSource=first['source'],
        restartSource=source,sourceBoundary='production code unchanged; only QA offline-modal checks differ',
        originalNativeInputRetained=True,freshPlayableLoad=True),indent=2),encoding='utf-8')

def verify_growth_evidence(folder, source):
    root=Path(folder)
    first=json.loads((root/'result.json').read_text())
    second=json.loads((root/'restart-result.json').read_text())
    assert first['complete'] and second['complete']
    assert first['source']==second['source']==source
    assert first['pid']!=second['pid'] and not first['restart'] and second['restart']
    assert first['theme']==second['theme']=='companions'
    assert first['identitiesPreserved'] and first['nativeHits']==1
    assert first['owned']==second['owned']==440 and first['visible']==second['visible']==100
    assert first['clicks']==second['clicks']==1
    assert first['footErrors']==first['depthErrors']==0 and first['samples']>30
    expected=[(theme,w,h) for theme in ['starter','companions'] for w,h in [(1920,1080),(1600,900),(2560,1440)]]
    assert [(s['theme'],s['width'],s['height']) for s in first['screens']]==expected
    frames=[s['frame'] for s in first['screens']]
    assert all(b>a for a,b in zip(frames,frames[1:]))
    artifacts=[]
    for theme,w,h in expected:
        path=root/f'{theme}-{w}x{h}.png'
        with Image.open(path) as image:
            image.verify()
        with Image.open(path) as image:
            assert image.size==(w,h)
            assert len(image.convert('RGB').resize((64,36)).getcolors(64*36))>20
        artifacts.append(dict(path=path.name,sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    assert artifacts[0]['sha256']!=artifacts[3]['sha256']
    steps=[json.loads(line) for line in (root/'steps.jsonl').read_text().splitlines()]
    assert all(step['passed'] for step in steps)
    assert any(s['message']=='fresh Player resumed playable state after offline summary' for s in steps)
    start=next(s['realtime'] for s in steps if s['message']=='settled hundred-cat roster')
    end=next(s['realtime'] for s in steps if s['message']=='real-time hundred-cat feet and furniture depth remain valid')
    assert end-start>=20
    (root/'verification.json').write_text(json.dumps(dict(passed=True,source=source,artifacts=artifacts,
        freshProcess=True,observationSeconds=end-start),indent=2),encoding='utf-8')
