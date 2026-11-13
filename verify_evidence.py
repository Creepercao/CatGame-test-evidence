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

    cases = read('evidence/android/ending/ending-result.json')
    assert [(case['width'], case['height'], case['action']) for case in cases] == [
        (2000, 900, 'continue'), (1200, 900, 'menu'),
    ]
    assert all(case['completed'] and case['firstEnding']['totalCats'] == 2 for case in cases)
    print('PASS Android v3 ending in two aspect ratios with both exit branches')

    base = 'evidence/android/performance'
    for case, cats, seconds, count in [('single-probe', 1, 3, 22), ('hundred-probe', 100, 3, 9), ('hundred-60s', 100, 60, 145)]:
        folder = f'{base}/{case}'
        result = read(f'{folder}/result.json')
        assert result['visibleCats'] == cats and result['elapsedSeconds'] >= seconds
        with (ROOT / folder / 'frames.csv').open(newline='', encoding='utf-8') as sample:
            rows = list(csv.DictReader(sample))
        frames = [int(row['frame']) for row in rows]
        assert len(frames) == count == result['frameCount']
        assert frames == list(range(frames[0], frames[0] + count))
        if case == 'hundred-probe':
            assert count < 10 and not (ROOT / folder / 'metrics.json').exists()
            continue
        metrics = read(f'{folder}/metrics.json')
        times = sorted(float(row['frame_ms']) for row in rows)
        assert metrics['p95Ms'] == times[math.ceil(.95 * count) - 1]
        for suffix, key in [('before', 'pssBeforeKiB'), ('after', 'pssAfterKiB')]:
            snapshot = (ROOT / folder / f'meminfo-{suffix}.txt').read_text(encoding='utf-8')
            assert f"MEMINFO in pid {metrics['pid']}" in snapshot
            assert int(re.search(r'TOTAL PSS:\s*(\d+)', snapshot).group(1)) == metrics[key] > 0
        assert metrics['withinReferenceBudget'] is (False if seconds == 60 else None)
    print('PASS Android performance probe, insufficient sample, and 60-second retry evidence')

    base = 'evidence/android/purchase'
    for name, expected in [('original-failure', 'FAIL'), ('original-rerun', 'PASS')]:
        case = ET.parse(ROOT / base / f'{name}.xml').find('.//test')
        assert case.find('status').attrib['status'] == expected
    immediate = read(f'{base}/immediate-results.json')
    assert len(immediate) == 4
    for index, case in enumerate(immediate, 1):
        assert case['immediateStop'] and case['home']
        assert case['pid'] != case['restartedPid']
        assert case['stoppedCount'] == case['restartedCount'] == 2
        for phase, expected in [('before', 1), ('stopped', 2), ('restarted', 2)]:
            assert read(f'{base}/{index}-{phase}.json')['cats'][0]['count'] == expected
    assert all(read(f'{base}/setup-guard.json').values())
    print('PASS Android purchase original failure, immediate-stop comparisons, and Robot rerun')

    phone = 'evidence/android/phone'
    for run, statuses in [
        ('smoke-01', ['FAIL']), ('smoke-02', ['FAIL']), ('smoke-03', ['PASS']),
        ('features-01', ['FAIL', 'FAIL', 'PASS']), ('features-02', ['PASS', 'FAIL', 'FAIL']),
        ('features-03', ['PASS', 'PASS']), ('ending-01', ['PASS', 'PASS']),
    ]:
        tests = ET.parse(ROOT / phone / f'{run}.xml').findall('.//test')
        assert [test.find('status').attrib['status'] for test in tests] == statuses, run
    for index in range(1, 4):
        for mode, cats in [('single', 1), ('hundred', 100)]:
            folder = f'{phone}/perf-full-01/{index:02d}-{mode}'
            result = read(f'{folder}/result.json')
            metrics = read(f'{folder}/metrics.json')
            assert result['visibleCats'] == cats and result['elapsedSeconds'] >= 60
            with (ROOT / folder / 'frames.csv').open(newline='', encoding='utf-8') as sample:
                rows = list(csv.DictReader(sample))
            assert len(rows) == result['frameCount'] == metrics['frames']
            frames = [int(row['frame']) for row in rows]
            assert frames == list(range(frames[0], frames[0] + len(frames)))
            intervals = sorted(float(row['frame_ms']) for row in rows)
            assert all(math.isfinite(value) and value > 0 for value in intervals)
            assert metrics['p95Ms'] == intervals[math.ceil(.95 * len(intervals)) - 1]
            assert metrics['pssBeforeKiB'] > 0 and metrics['pssAfterKiB'] > 0
            assert metrics['withinReferenceBudget'] and metrics['over33_3msRatio'] == 0
    idle = read(f'{phone}/idle-01/01-hundred/idle-result.json')
    assert idle['elapsedSeconds'] >= 300 and idle['playSecondsAdvanced'] >= 280
    assert idle['initial']['cats'] == idle['final']['cats'] and len(idle['memorySnapshots']) == 5
    initial = read(f'{phone}/features-03/pet-before.json')
    final = read(f'{phone}/features-03/pet-after.json')
    assert final['clicks'] == initial['clicks'] + 1 == 2
    restored = read(f'{phone}/features-01/offline-08-restored-save.json')
    assert sum(cat['count'] for cat in restored['cats']) == 3
    for branch in ('continue', 'menu'):
        purchased = read(f'{phone}/ending-01/{branch}-purchased.json')
        completed = read(f'{phone}/ending-01/{branch}-completed.json')
        assert completed['endingCompleted'] and completed['firstEnding'] == purchased['firstEnding']
        assert purchased['rocketPurchased'] and 0 <= purchased['catHair'] < 1000
    artifact = read(f'{phone}/artifact-verification.json')
    assert artifact['matches'] and artifact['installedReleaseSha256'] == artifact['localReleaseSha256']
    assert read(f'{phone}/network-restored.json') == {'airplane': '0', 'wifi': '1'}
    print('PASS native phone functional reruns, six frame samples, and 300-second idle evidence')

def verify_android_viewport():
    base = 'evidence/android/viewport'
    for name, passed, failed in [('editmode-red-02.xml', 5, 2), ('EditMode.xml', 61, 1),
                                 ('EditMode-02.xml', 61, 1), ('EditMode-03.xml', 62, 0),
                                 ('PlayMode-01.xml', 87, 0), ('save-isolated-01.xml', 1, 0)]:
        report = ET.parse(ROOT / base / name).getroot()
        assert int(report.get('passed')) == passed and int(report.get('failed')) == failed, name
    red = ET.parse(ROOT / base / 'editmode-red-02.xml')
    failed_cases = red.findall('.//test-case[@result="Failed"]')
    assert len(failed_cases) == 2
    assert all('RoomViewportTests' in case.get('classname') for case in failed_cases)
    assert any('(2712,1220)' in case.get('name') for case in failed_cases)
    assert any('(2560,1440)' in case.get('name') for case in failed_cases)
    green = ET.parse(ROOT / base / 'EditMode-03.xml')
    cameras = green.findall('.//test-case[@classname="CatGame.EditTests.RoomViewportTests"]')
    assert len(cameras) == 7 and all(case.get('result') == 'Passed' for case in cameras)
    for name in ('EditMode.xml', 'EditMode-02.xml'):
        cases = ET.parse(ROOT / base / name).findall('.//test-case[@result="Failed"]')
        assert len(cases) == 1 and cases[0].get('methodname').startswith('Stress_save_load')
    diagnostic = (ROOT / base / 'EditMode-02.xml').read_text(encoding='utf-8')
    assert 'HResult=0x80070497' in diagnostic and 'failed cycle=874' in diagnostic
    assert read(f'{base}/test-exits-03.json') == {'editModeExit': 0, 'playModeExit': 0}
    for run, statuses in [('phone-v3-red-01', ['FAIL']), ('phone-v4-01', ['PASS']),
                          ('avd-v4-01', ['PASS', 'PASS', 'PASS'])]:
        tests = ET.parse(ROOT / base / run / 'output.xml').findall('.//test')
        assert [test.find('status').get('status') for test in tests] == statuses
    for run, name, size, widths in [
        ('phone-v3-red-01', '03-room', (2712, 1220), [156] * 3),
        ('phone-v4-01', '03-room', (2712, 1220), [0] * 3),
        *[('avd-v4-01', f'{width}-02-room', (width, 900), [0] * 3) for width in (1600, 2000, 1200)],
    ]:
        coverage = read(f'{base}/{run}/{name}.coverage.json')
        png = (ROOT / base / run / f'{name}.png').read_bytes()
        assert hashlib.sha256(png).hexdigest() == coverage['imageSha256']
        assert (int.from_bytes(png[16:20], 'big'), int.from_bytes(png[20:24], 'big')) == size
        assert (coverage['width'], coverage['height']) == size
        assert coverage['rightClearWidths'] == widths and coverage['covered'] == (max(widths) == 0)
    upgrade = read(f'{base}/phone-upgrade-v4/result.json')
    before = (ROOT / base / 'phone-upgrade-v4/before-save.json').read_bytes()
    after = (ROOT / base / 'phone-upgrade-v4/before-launch-save.json').read_bytes()
    assert before == after and hashlib.sha256(before).hexdigest() == upgrade['saveSha256']
    assert upgrade['fromVersionCode'] == 3 and upgrade['toVersionCode'] == 4
    assert upgrade['totalCats'] == 3 and upgrade['clicks'] == 2
    artifact = read(f'{base}/artifact.json')
    assert upgrade['installedReleaseSha256'] == upgrade['localReleaseSha256'] == artifact['apkSha256']
    assert artifact['versionCode'] == 4 and not artifact['developmentBuild']
    assert artifact['buildCliExit'] == artifact['buildEditorExit'] == 0
    print('PASS viewport red/green, retained save-stress failures, phone upgrade and three AVD ratios')


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
    verify_android_viewport()
    print('All evidence checks passed; no new Unity/Robot execution performed.')

if __name__=='__main__': main()
