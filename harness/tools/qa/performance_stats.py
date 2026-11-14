"""Analyse raw Player samples; retain failed criteria rather than trimming outliers."""
import csv
import json
import math
from pathlib import Path


def distribution(values):
    values = sorted(values)
    if not values or not all(math.isfinite(v) for v in values):
        raise ValueError('Missing or nonfinite samples')
    return dict(count=len(values), mean=sum(values) / len(values),
                p50=values[max(0, math.ceil(len(values) * .50) - 1)],
                p95=values[max(0, math.ceil(len(values) * .95) - 1)],
                p99=values[max(0, math.ceil(len(values) * .99) - 1)], maximum=values[-1])


def verify_save_recovery(folder):
    folder = Path(folder)
    before = json.loads((folder / 'save-backup-before-corruption.json').read_text(encoding='utf-8-sig'))
    restored = json.loads((folder / 'save-restored-after-corruption.json').read_text(encoding='utf-8-sig'))
    assert restored == before, 'Recovered snapshot differs from the previous committed backup'
    for name in ('save.json', 'save.json.bak'):
        actual = json.loads((folder / 'save-stress/CatGame' / name).read_text(encoding='utf-8-sig'))
        assert actual == before, 'Final save or backup differs from the fixed stress fixture'
    assert before['clicks'] == 777 and before['catHair'] == 12345.5
    assert all(cat['count'] == 40 for cat in before['cats'])
    assert all(item['level'] == 5 for item in before['items'])
    damaged = (folder / 'save-stress/CatGame/save.bad.json').read_text(encoding='utf-8-sig')
    try:
        json.loads(damaged)
    except json.JSONDecodeError:
        pass
    else:
        raise AssertionError('Expected a quarantined malformed save')


def analyse(folder, probe=False, require_save_contract=False):
    folder = Path(folder)
    result = json.loads((folder / 'performance-result.json').read_text(encoding='utf-8-sig'))
    environment = json.loads((folder / 'performance-environment.json').read_text(encoding='utf-8-sig'))
    assert result['complete'] and result['errors'] == 0 and result['probe'] == probe
    assert environment['development'] and environment['mainThreadValid'] and environment['gcValid']
    assert (environment['width'], environment['height'], environment['targetFps'], environment['vsync']) == (1280, 720, 60, 0)
    if not probe:
        contract = result.get('saveRecoveryContract')
        if require_save_contract:
            assert contract == 'previous-committed-backup-v1', 'Current full run requires the backup recovery contract'
        if contract is not None:
            assert contract == 'previous-committed-backup-v1', 'Unknown save recovery contract'
            verify_save_recovery(folder)
    expected = ['probe'] if probe else ['single', 'hundred', 'input', 'cg-cold', 'cg-warm', 'save']
    assert [p['name'] for p in result['phases']] == expected
    phases, failures = {}, []
    for phase in result['phases']:
        name = phase['name']
        with (folder / ('frames-' + name + '.csv')).open(encoding='utf-8-sig', newline='') as stream:
            rows = [{k: float(v) for k, v in row.items()} for row in csv.DictReader(stream)]
        assert len(rows) == phase['frames'] and len(rows) > 1
        assert all(b['frame'] > a['frame'] and b['realtime'] > a['realtime'] for a, b in zip(rows, rows[1:]))
        assert all(r['main_thread_ns'] > 0 and r['gc_bytes'] >= 0 for r in rows), 'Unavailable profiler samples'
        assert phase['boundaryErrors'] == 0
        assert phase.get('workingSetBefore', 0) > 0 and phase.get('workingSetAfter', 0) > 0, 'Unavailable RSS endpoints'
        frames = distribution([r['frame_ms'] for r in rows])
        slow = sum(r['frame_ms'] > 33.3 for r in rows) / len(rows)
        summary = dict(elapsed=phase['elapsed'], frames=frames, slowFrameRatio=slow,
                       processCpuMs=phase.get('cpuMilliseconds'),
                       processWorkingSetBefore=phase.get('workingSetBefore'), processWorkingSetAfter=phase.get('workingSetAfter'),
                       mainThreadMs=distribution([r['main_thread_ns'] / 1e6 for r in rows]),
                       gcBytes=distribution([r['gc_bytes'] for r in rows]),
                       boundaryChecks=phase['boundaryChecks'], visible=phase['visible'], owned=phase['owned'])
        if 'cpuMilliseconds' in phase:
            summary['processCpuPercentOneCore'] = phase['cpuMilliseconds'] / (phase['elapsed'] * 1000) * 100
            summary['processCpuPercentMachine'] = summary['processCpuPercentOneCore'] / environment['logicalProcessors']
        for field in ['mono_bytes', 'unity_allocated_bytes', 'unity_reserved_bytes']:
            values = [r[field] for r in rows]
            summary[field] = dict(first=values[0], last=values[-1], minimum=min(values), maximum=max(values), delta=values[-1] - values[0])
        if name in ['single', 'hundred', 'input']:
            required = 90 if name == 'hundred' else 60
            assert phase['elapsed'] >= required and rows[-1]['realtime'] - rows[0]['realtime'] >= required - .2
            if frames['p95'] > 20 or slow > .01:
                failures.append(dict(phase=name, reason='frame-budget', p95=frames['p95'], slowRatio=slow))
        if name == 'hundred':
            assert phase['visible'] == 100 and phase['owned'] == 440
        phases[name] = summary
    operations = result['operations']
    operation_stats = {}
    for phase, action in sorted(set((o['phase'], o['action']) for o in operations)):
        selected = [o for o in operations if o['phase'] == phase and o['action'] == action]
        operation_stats[phase + '/' + action] = distribution([o['elapsedMs'] for o in selected])
    if not probe:
        assert result['purchases'] == 100 and result['coldCG'] == 20 and result['warmCG'] == 200
        assert result['roundTrips'] == 1000 and result['corruptRecovery'] == 1
        clicks = [o for o in operations if o['phase'] == 'input' and o['action'] == 'click']
        buys = [o for o in operations if o['phase'] == 'input' and o['action'] == 'buy']
        assert len(clicks) == result['clicks'] and len(buys) == result['attempts']
        assert sum(o['success'] for o in buys) == 100
        assert len({o['frame'] for o in clicks}) == len(clicks) and len({o['frame'] for o in buys}) == len(buys)
        if len(clicks) < 1140 or len(buys) < 570:
            failures.append(dict(phase='input', reason='request-cadence', clicks=len(clicks), buys=len(buys)))
        assert operation_stats['cg-warm/open']['count'] == 200 and operation_stats['save/write']['count'] == operation_stats['save/read']['count'] == 1000
    return dict(passed=not failures, probe=probe, phases=phases, operations=operation_stats,
                criteriaFailures=failures, environment=environment, rawFunctionalResult=result)
