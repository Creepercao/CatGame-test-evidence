"""Read-only verification of curated historical evidence (not a game test run)."""
from pathlib import Path
import json, hashlib, csv, math, re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
def read(path):
    return json.loads((ROOT/path).read_text(encoding='utf-8'))

def verify_android():
    android = ROOT / 'evidence/android'
    for filename, names, expected in [
        ('mumu-smoke.xml', ['ANDROID-SMOKE-01'], 'PASS'),
        ('mumu-layout.xml', ['ANDROID-LAYOUT-01', 'ANDROID-LAYOUT-02'], 'PASS'),
        ('mumu-lock.xml', ['ANDROID-LIFECYCLE-01'], 'FAIL'),
        ('avd-smoke.xml', ['ANDROID-SMOKE-01'], 'PASS'),
        ('avd-lock-v2.xml', ['ANDROID-LIFECYCLE-01'], 'PASS'),
    ]:
        report = ET.parse(android / filename)
        tests = report.findall('.//test')
        assert [test.attrib['name'].split()[0] for test in tests] == names, filename
        assert all(test.find('status').attrib['status'] == expected for test in tests), filename

    failed_lock = (android / 'mumu-lock.xml').read_text(encoding='utf-8')
    assert 'The device screen is still not locked after 5000ms timeout' in failed_lock
    passed_lock = (android / 'avd-lock-v2.xml').read_text(encoding='utf-8')
    assert 'isLocked=True; Android mWakefulness=Asleep' in passed_lock
    assert 'isLocked=False; Android mWakefulness=Awake' in passed_lock
    pids = re.findall(r'CatGame QA PID (\d+)', passed_lock)
    assert len(pids) == 2 and pids[0] == pids[1]

    base = 'evidence/android/upgrade'
    initial = read(f'{base}/v1-save.json')
    assert initial == read(f'{base}/v2-before-launch-save.json')
    restored = read(f'{base}/v2-after-launch-save.json')
    for field in ('cats', 'items', 'achievements', 'introCompleted', 'seenStoryEvents'):
        assert initial[field] == restored[field], field
    assert initial['cats'][0] == {'id': 'Milk', 'count': 2}
    assert initial['items'][0] == {'id': 'item_1_1', 'level': 1}
    for phase in ('v1', 'v2-before-launch', 'v2-after-launch'):
        assert read(f'{base}/{phase}-settings.json')['numberFormat'] == 1
    print('PASS Android Robot outcomes, lock/wake continuity, and v1 to v2 save retention')

    backup = android / 'backup'
    red = ET.parse(backup / 'editmode-red.xml').getroot()
    assert int(red.attrib['failed']) == 5
    for mode, total, method in [
        ('EditMode', 55, 'Load_restores_previous_save_when_primary_is_missing_or_corrupt'),
        ('PlayMode', 87, 'Recovered_save_enables_continue_and_new_game_discards_previous_backup'),
    ]:
        report = ET.parse(backup / f'{mode}.xml').getroot()
        assert int(report.attrib['passed']) == total and int(report.attrib['failed']) == 0
        cases = report.findall(f'.//test-case[@methodname="{method}"]')
        assert len(cases) == (2 if mode == 'EditMode' else 1)
        assert all(case.attrib['result'] == 'Passed' for case in cases)
    print('PASS save recovery red/green and runtime lifecycle evidence')

    for filename, verdict in [('avd-v3-smoke.xml', 'FAIL'), ('avd-v3-smoke-02.xml', 'FAIL'), ('avd-v3-smoke-03.xml', 'PASS')]:
        test = ET.parse(backup / filename).find('.//test')
        assert test.attrib['name'].startswith('ANDROID-SMOKE-01 ')
        assert test.find('status').attrib['status'] == verdict
    cases = read('evidence/android/backup/recovery-result.json')
    assert [case['scenario'] for case in cases] == ['corrupt-primary', 'missing-primary']
    assert len({case['pid'] for case in cases}) == 2
    for case in cases:
        assert case['result'] == 'pass' and case['milkCount'] == 2
        scenario = case['scenario']
        before = read(f'evidence/android/backup/{scenario}-backup-before.json')
        after = read(f'evidence/android/backup/{scenario}-restored-primary.json')
        for field in ('cats', 'items', 'clicks', 'introCompleted', 'achievements', 'seenStoryEvents'):
            assert before[field] == after[field], (scenario, field)
        assert 'Recovered CatGame progress from backup.' in (backup / f'{scenario}-unity.txt').read_text(encoding='utf-8')
    print('PASS Android v3 Player recovery after corrupt and missing primary')

def main():
    manifest=read('provenance.json')
    for row in manifest['files']:
        p=ROOT/row['path']
        assert hashlib.sha256(p.read_bytes()).hexdigest()==row['publishedSha256'], row['path']
        if p.suffix=='.xml': ET.parse(p)
        elif p.name=='save.bad.json':
            try: json.loads(p.read_text(encoding='utf-8'))
            except json.JSONDecodeError: pass
            else: raise AssertionError('Expected deliberately corrupted fixture')
        elif p.suffix=='.json': json.loads(p.read_text(encoding='utf-8'))
        elif p.suffix=='.jsonl':
            for line in p.read_text(encoding='utf-8').splitlines():
                if line.strip(): json.loads(line)
    print('PASS public hashes and structured formats:',len(manifest['files']))
    for kind,count in [('PlayMode',86),('EditMode',51)]:
        x=ET.parse(ROOT/f'evidence/ci-5/full/unity-main/test-results-{kind}.xml').getroot()
        assert int(x.get('passed'))==count and int(x.get('failed'))==int(x.get('skipped'))==0
    print('PASS NUnit: PlayMode 86, EditMode 51')
    for color in ['red','green']:
        path=next((ROOT/f'evidence/roaming-target-{color}-20261106-01').rglob('test-results-PlayMode.xml'))
        failures=int(ET.parse(path).getroot().get('failed'))
        assert (failures>0) if color=='red' else (failures==0)
    print('PASS roaming defect red/green results')
    for n,allowed in [(2,False),(3,False),(4,True),(5,True)]:
        p=read(f'evidence/ci-{n}/pipeline.json')
        assert p['releaseAllowed'] is allowed
        if not allowed: assert p['stages'][-1]['exitCode']!=0
    print('PASS failed CI gates and successful delivery records')
    stepsfile=next((ROOT/'evidence/ci-5/business').rglob('steps.jsonl'))
    steps=[json.loads(s) for s in stepsfile.read_text(encoding='utf-8').splitlines() if s.strip()]
    launches=[s['result']['pid'] for s in steps if s['action']=='launch']
    exits=[s['result'] for s in steps if s['action']=='normal-exit']
    assert len(set(launches))==2 and [e['pid'] for e in exits]==launches and all(e['exitCode']==0 for e in exits)
    assert any(s['action']=='restart-verified' for s in steps)
    print('PASS real process restart evidence')
    for n in [1,2]:
        folder=f'evidence/performance-baseline-20261106-02/performance/round-{n}'
        with (ROOT/folder/'frames-hundred.csv').open(encoding='utf-8') as f:
            vals=sorted(float(row['frame_ms']) for row in csv.DictReader(f))
        assert vals and all(math.isfinite(x) and x>0 for x in vals)
        p95=vals[math.ceil(.95*len(vals))-1]
        expected=read(folder+'/analysis.json')['phases']['hundred']['frames']['p95']
        assert abs(p95-expected)<1e-6
        assert p95<=20 and sum(x>33.3 for x in vals)/len(vals)<=.01
        print(f'PASS round {n} hundred-cat P95={p95:.6f}ms, frames={len(vals)}')
    verify_android()
    print('All evidence checks passed; no new Unity/Robot execution performed.')

if __name__=='__main__': main()
