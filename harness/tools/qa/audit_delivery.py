"""Read-only evidence audit; output is evidence, not a substitute for the requirements review."""
import hashlib
import argparse
import json
import re
import struct
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('output')
parser.add_argument('--test-run', default='catalog-regression-20261101-01')
parser.add_argument('--build-run', default='catalog-build-20261101-01')
parser.add_argument('--playmode-count', type=int, default=60)
parser.add_argument('--editmode-count', type=int, default=38)
parser.add_argument('--item-icon-count', type=int, default=20)
parser.add_argument('--story-run')
parser.add_argument('--story-count',type=int,default=4)
parser.add_argument('--player-run', default='catalog-player-20261101-01')
parser.add_argument('--pointer-run', default='final-pointer-20261101-01')
args = parser.parse_args()
out = root / args.output
out.mkdir(parents=True, exist_ok=False)
evidence = {'inputs': vars(args)}
for platform, count in [('PlayMode', args.playmode_count), ('EditMode', args.editmode_count)]:
    path = root / f'Tests/robot/results/{args.test_run}/unity-main/test-results-{platform}.xml'
    result = ET.parse(path).getroot()
    assert int(result.get('passed')) == count and int(result.get('failed')) == 0 and int(result.get('skipped')) == 0
    evidence[platform] = [dict(case.attrib) for case in result.iter('test-case')]
metadata = json.loads((root / f'Tests/robot/results/{args.build_run}/delivery-metadata.json').read_text())
build_result=json.loads((root/f'Tests/robot/results/{args.build_run}/result.json').read_text())
assert build_result['exit_code']==0
evidence['build_result']=build_result
for record in metadata:
    assert hashlib.sha256((root / record['path']).read_bytes()).hexdigest() == record['sha256'], record['path']
evidence['delivery_files'] = metadata
for run in dict.fromkeys([args.player_run, args.pointer_run]):
    folder = root / 'Tests/robot/results' / run
    xml = ET.parse(folder / 'output.xml').getroot()
    assert xml.find('suite/status').get('status') == 'PASS'
    metrics = dict(line.split('=', 1) for line in (folder/'player/performance.txt').read_text().splitlines())
    for key in ['boundsErrors', 'roomFootErrors', 'furnitureDepthErrors']: assert metrics[key] == '0'
    assert metrics['roomActors'] == '100'
    if run == args.pointer_run: assert metrics['nativePointerHits'] == '3'
    evidence[run] = metrics
    steps_path=folder/'player/steps.jsonl'
    if steps_path.exists():
        steps=[json.loads(line) for line in steps_path.read_text().splitlines()]
        assert all(step['passed'] and step['step']==i for i,step in enumerate(steps,1))
        evidence[run+'_step_count']=len(steps)
cats = list((root / 'Assets/Resources/Art/Cats/Pixel64').glob('*/*.png'))
assert len(cats) == 44
evidence['cat_sheets'] = []
for path in sorted(cats):
    width, height = struct.unpack('>II', path.read_bytes()[16:24])
    expected = {'Idle':8, 'Walk':8, 'Jump':12, 'Sleep':8}[path.stem]
    assert height == 64 and width == 64 * expected
    evidence['cat_sheets'].append(dict(path=str(path.relative_to(root)), width=width, height=height))
assert not (root/'Assets/Resources/Art/Cats/Calico/Walk.png').exists()
assert len(list((root/'Assets/Resources/Art/Items/Pixel').glob('*.png'))) == args.item_icon_count
assert len(list((root/'Assets/Resources/Art/UI/Pixel').glob('*.png'))) == 3
evidence['sources'] = {str(p.relative_to(root)): hashlib.sha256(p.read_bytes()).hexdigest()
    for parent in ['Assets/Scripts', 'Assets/Scenes', 'ProjectSettings'] for p in (root/parent).rglob('*') if p.is_file()}
if args.story_run:
    folder=root/'Tests/robot/results'/args.story_run
    assert ET.parse(folder/'output.xml').getroot().find('suite/status').get('status')=='PASS'
    story_ids=['three_breeds','six_breeds','all_breeds','planet_cats'] if args.story_count==4 else re.findall(r'new StoryDefinition\("([^"]+)"', (root/'Assets/Scripts/Idle/StoryCatalog.cs').read_text(encoding='utf-8'))
    assert len(story_ids)==args.story_count
    for size in ['1920x1080','1280x720','1024x768']:
        for name in ['cg-'+id for id in story_ids]+['shop-rocket','rocket-confirmation','ending-CG','ending-statistics']:
            assert (folder/'player'/f'{name}-{size}.png').is_file()
    evidence['story_metrics']=json.loads((folder/'player/story-performance.json').read_text())
    assert evidence['story_metrics']['switches']==args.story_count*10
    if args.story_count==20:
        steps=[json.loads(line) for line in (folder/'player/steps.jsonl').read_text().splitlines()]
        assert all(step['passed'] and step['step']==i for i,step in enumerate(steps,1))
        assert sum(step['type']=='stress-complete' for step in steps)==200
        assert sum(step['type']=='screenshot' for step in steps)==72
        evidence['story_step_summary']={'steps':len(steps),'stress_completions':200,'screenshots':72,'all_passed':True,'continuous_step_numbers':True}
(out/'evidence.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'PASS: {args.playmode_count}/{args.editmode_count} all green, {len(metadata)} delivery hashes match, {len(set([args.player_run,args.pointer_run]))} Player run(s) verified including 3 native clicks, 44 cat sheets/{args.item_icon_count} item icons/3 UI masks; source snapshot saved.')
