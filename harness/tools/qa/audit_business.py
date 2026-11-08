"""Audit retained business, expected-failure and full-regression evidence."""
import argparse
import hashlib
import json
import struct
import xml.etree.ElementTree as ET
from pathlib import Path


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--business', type=Path, required=True)
    parser.add_argument('--failure', type=Path, required=True)
    parser.add_argument('--regression', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    assert read(args.business / 'result.json')['releaseAllowed'] is True
    assert read(args.failure / 'result.json')['releaseAllowed'] is False
    assert read(args.regression / 'result.json')['releaseAllowed'] is True
    folders = list(args.business.glob('business-*'))
    assert len(folders) == 1
    folder = folders[0]
    steps = [json.loads(line) for line in (folder / 'steps.jsonl').read_text(encoding='utf-8').splitlines()]
    assert [s['step'] for s in steps] == list(range(1, len(steps) + 1))
    launches = [s['result']['pid'] for s in steps if s['action'] == 'launch']
    exits = [s['result'] for s in steps if s['action'] == 'normal-exit']
    assert len(set(launches)) == 2 and [e['pid'] for e in exits] == launches
    assert all(e['exitCode'] == 0 for e in exits)
    assert any(s['action'] == 'restart-verified' for s in steps)
    prepared = steps[0]['result']
    assert hashlib.sha256(Path(prepared['runtimeDll']).read_bytes()).hexdigest() == prepared['runtimeSha256']
    screenshots = []
    actions = []
    for step in steps:
        if not isinstance(step['action'], dict):
            continue
        request, reply = step['action'], step['result']
        assert request['id'] == reply['id'] and reply['pid'] in launches and reply['ok']
        actual = Path(reply['savePath'])
        assert actual.parent.resolve() == (folder / 'player/CatGame').resolve() and actual.name == 'save.json'
        screenshot = folder / 'player' / (request['id'] + '.png')
        raw = screenshot.read_bytes()
        assert raw[:8] == b'\x89PNG\r\n\x1a\n' and struct.unpack('>II', raw[16:24]) == (1280, 720)
        screenshots.append(str(screenshot))
        actions.append(request['action'])
    assert actions == ['new', 'click', 'buy', 'quit', 'continue', 'quit']
    saved = read(folder / 'player/CatGame/save.json')
    assert saved['clicks'] == 200 and next(c['count'] for c in saved['cats'] if c['id'] == 'Milk') == 2
    failures = list(args.failure.glob('business-*/failures.json'))
    assert len(failures) == 1
    failure = read(failures[0])[0]
    assert failure['keyword'] == 'Assert Milk Count' and '99' in failure['error']
    assert Path(failure['screenshot']).is_file()
    counts = {}
    for kind, minimum in [('PlayMode', 85), ('EditMode', 51)]:
        root = ET.parse(args.regression / 'unity-main' / ('test-results-' + kind + '.xml')).getroot()
        assert int(root.get('passed')) >= minimum and int(root.get('failed')) == int(root.get('skipped')) == 0
        counts[kind] = int(root.get('passed'))
    result = dict(passed=True, businessSteps=len(steps), screenshots=len(screenshots), ownedPids=launches,
                  normalExits=exits, preservedClicks=200, preservedMilk=2, regression=counts,
                  failureKeyword=failure['keyword'], runtimeSha256=prepared['runtimeSha256'])
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with args.output.open('x', encoding='utf-8') as output:
        json.dump(result, output, indent=2)
    print(json.dumps(result))


if __name__ == '__main__':
    main()
