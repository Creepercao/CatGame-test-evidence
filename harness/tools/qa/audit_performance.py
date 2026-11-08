"""Reconcile immutable Robot evidence, raw frames, operation traces and repeat cohorts."""
import argparse
import hashlib
import json
import math
import re
import subprocess
import xml.etree.ElementTree as ET
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path

from performance_stats import analyse

ROOT = Path(__file__).resolve().parents[2]


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def robot_passed(folder):
    result = read(folder / 'result.json')
    assert result['releaseAllowed'] and result['exitCode'] == 0
    stat = ET.parse(folder / 'output.xml').find('statistics/total/stat')
    assert int(stat.get('pass')) > 0 and int(stat.get('fail')) == int(stat.get('skip')) == 0


def audit(run, guard):
    robot_passed(run)
    manifest = read(run / 'artifacts.json')
    for entry in manifest:
        path = (run / entry['path']).resolve()
        assert path.is_relative_to(run) and path.is_file()
        assert path.stat().st_size == entry['bytes']
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry['sha256'], str(path)
    host = read(run / 'performance/result.json')
    assert host['passed'] and not host['probe'] and not host['preflight'] and not host['worktree']
    assert len(host['rounds']) >= 2
    original_source = subprocess.check_output(
        ['git', 'show', host['commit'] + ':Assets/Scripts/Idle/StoryCatalog.cs'], cwd=ROOT).decode('utf-8-sig')
    ids = re.findall(r'new StoryDefinition\("([^"]+)"', original_source)
    assert len(ids) == len(set(ids)) == 20
    runtime = next(a for a in host['artifacts'] if a['path'].endswith('CatGame.Runtime.dll'))
    cohorts = []
    same_environment = None
    for record in host['rounds']:
        folder = run / 'performance' / ('round-' + str(record['number']))
        assert record['exitCode'] == 0 and not record.get('forcedCleanup')
        assert hashlib.sha256((folder / 'CatGame.Runtime.dll').read_bytes()).hexdigest() == runtime['sha256']
        summary = analyse(folder)
        assert summary == read(folder / 'analysis.json') == record['summary'] and summary['passed']
        environment = summary['environment']
        assert Path(environment['savePath']).resolve() == folder / 'CatGame/save.json'
        stable_environment = {k: v for k, v in environment.items() if k != 'savePath'}
        if same_environment is None:
            same_environment = stable_environment
        assert stable_environment == same_environment, 'Repeat cohort environment differs'
        raw = summary['rawFunctionalResult']
        assert (raw['clicks'], raw['attempts'], raw['purchases'], raw['coldCG'], raw['warmCG'],
                raw['roundTrips'], raw['corruptRecovery']) == (1200, 600, 100, 20, 200, 1000, 1)
        operations = raw['operations']
        assert all(math.isfinite(o['elapsedMs']) and o['elapsedMs'] >= 0 for o in operations)
        assert all(b['frame'] >= a['frame'] and b['realtime'] >= a['realtime'] and b['utcTicks'] >= a['utcTicks']
                   for a, b in zip(operations, operations[1:]))
        groups = defaultdict(list)
        for operation in operations:
            groups[operation['phase'] + '/' + operation['action']].append(operation)
        expected_counts = {'startup/new-game-home-ready': 1, 'input/click': 1200, 'input/buy': 600,
                           'cg-cold/open': 20, 'cg-cold/complete': 20, 'cg-warm/open': 200,
                           'cg-warm/complete': 200, 'save/write': 1000, 'save/read': 1000}
        assert {key: len(value) for key, value in groups.items()} == expected_counts
        for key, group in groups.items():
            assert [o['index'] for o in group] == list(range(len(group))), key
            if key != 'input/buy':
                assert all(o['success'] for o in group), key
        for phase, repeat in [('cg-cold', 1), ('cg-warm', 10)]:
            for action in ['open', 'complete']:
                assert [o['id'] for o in groups[phase + '/' + action]] == ids * repeat
        for phase in raw['phases']:
            assert phase == read(folder / ('phase-' + phase['name'] + '.json'))
        cohorts.append(dict(number=record['number'], pid=record['pid'], started=record['started'],
                            ended=record['ended'], exitCode=record['exitCode'],
                            frames=sum(s['frames']['count'] for s in summary['phases'].values()),
                            operations=len(operations), phases=summary['phases'], timings=summary['operations']))
    robot_passed(guard)
    unity = ET.parse(guard / 'unity-guard/test-results-PlayMode.xml').getroot()
    cases = list(unity.iter('test-case'))
    assert len(cases) == 1 and cases[0].get('result') == 'Passed'
    output = cases[0].findtext('output', '')
    steps = re.findall(r'PERF GUARD STEP (\d+):', output)
    assert list(map(int, steps)) == list(range(1, 201)), 'Missing input-guard execution steps'
    assert 'PERF GUARD COMPLETE:' in output
    return dict(passed=True, commit=host['commit'], runtimeSha256=runtime['sha256'],
                started=host['started'], ended=host['ended'],
                elapsedSeconds=(datetime.fromisoformat(host['ended']) - datetime.fromisoformat(host['started'])).total_seconds(),
                environment=same_environment, archivedFilesVerified=len(manifest), rounds=cohorts,
                inputGuard=dict(run=str(guard), passed=1, steps=len(steps),
                                scope='Editor EventSystem dispatch; functional stress, separate from Player timing'),
                limits=['Repeat run is not optimization A/B', 'No native input claim',
                        'No thermal/power time series or GPU duration capture',
                        'RSS endpoints and short sequences do not prove absence of leaks'])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--run', required=True, type=Path)
    parser.add_argument('--guard', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    run, guard, out = (p.resolve() for p in (args.run, args.guard, args.output))
    evidence = (ROOT / 'Tests/robot/results').resolve()
    assert all(p.is_relative_to(evidence) for p in (run, guard, out))
    out.mkdir(parents=True, exist_ok=False)
    result = dict(passed=False, utc=datetime.now(timezone.utc).isoformat(), run=str(run), guard=str(guard))
    try:
        result.update(audit(run, guard))
    except Exception as error:
        result['error'] = type(error).__name__ + ': ' + str(error)
    (out / 'audit.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(dict(passed=result['passed'], output=str(out), error=result.get('error')), ensure_ascii=False))
    return 0 if result['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
