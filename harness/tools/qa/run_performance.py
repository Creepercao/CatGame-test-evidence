"""Run isolated performance Players serially; preserve raw data even on failures."""
import argparse
import csv
import hashlib
import json
import os
import shutil
import subprocess
import sys
import time
import traceback
from datetime import datetime, timezone
from pathlib import Path
from performance_stats import analyse

ROOT = Path(__file__).resolve().parents[2]


def utc():
    return datetime.now(timezone.utc).isoformat()


def activity():
    run = subprocess.run(['tasklist', '/FO', 'CSV', '/NH'], capture_output=True, text=True,
                         encoding='utf-8', errors='replace', check=True, creationflags=subprocess.CREATE_NO_WINDOW)
    return [dict(name=r[0], pid=int(r[1])) for r in csv.reader(run.stdout.splitlines())
            if len(r) > 1 and r[0].lower() in ['unity.exe', 'catgame.exe', 'catgameqa.exe']]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--probe', action='store_true')
    parser.add_argument('--fault-probe', action='store_true', help='Deliberate sampling error; expected exit 1')
    parser.add_argument('--rounds', type=int, default=2)
    args = parser.parse_args()
    assert 1 <= args.rounds <= 5
    assert not args.fault_probe or args.probe
    output = args.output.resolve()
    assert output.is_relative_to((ROOT / 'Tests/robot/results').resolve())
    output.mkdir(parents=True, exist_ok=False)
    exe = Path(os.environ.get('CATGAME_PERFORMANCE_EXE', str(ROOT / 'Build/Performance/QA/CatGameQA.exe')))
    dll = exe.parent / (exe.stem + '_Data/Managed/CatGame.Runtime.dll')
    asset = exe.parent / (exe.stem + '_Data/resources.assets')
    report = dict(started=utc(), probe=args.probe, rounds=[], passed=False)
    try:
        assert exe.is_file() and dll.is_file() and asset.is_file(), 'ENVIRONMENT: build Performance QA first'
        report['preflight'] = activity()
        assert not report['preflight'], 'ENVIRONMENT: another Unity or game Player is running'
        ci = ROOT / 'Build/ci/local-credentials.json'
        if ci.exists():
            sys.path.insert(0, str(ROOT / 'tools/ci'))
            import controller
            jobs = controller.query('/job/CatGame-Quality/api/json?tree=builds[number,building]')
            queue = controller.query('/queue/api/json?tree=items[id]')
            assert not any(b['building'] for b in jobs['builds']) and not queue['items'], 'ENVIRONMENT: CI busy'
        report['commit'] = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
        report['worktree'] = subprocess.check_output(['git', 'status', '--porcelain'], cwd=ROOT, text=True).strip()
        report['artifacts'] = [dict(path=str(p), sha256=hashlib.sha256(p.read_bytes()).hexdigest(), bytes=p.stat().st_size)
                               for p in (exe, dll, asset)]
        for number in range(1 if args.probe else args.rounds):
            folder = output / ('round-' + str(number + 1))
            folder.mkdir()
            shutil.copyfile(dll, folder / 'CatGame.Runtime.dll')
            env = os.environ.copy()
            for name in list(env):
                if name.startswith('CATGAME_QA_') or name.startswith('CATGAME_DEBUG_'):
                    env.pop(name)
            env.update(CATGAME_PLAYER_QA_ROOT=str(folder), CATGAME_QA_PERFORMANCE_ONLY='1',
                       CATGAME_PERF_PROBE='1' if args.probe else '0', CATGAME_DEBUG_DISABLE='1')
            env['CATGAME_PERF_FAULT']='1' if args.fault_probe else '0'
            command = [str(exe), '-screen-fullscreen', '0', '-logFile', str(folder / 'player.log')]
            record = dict(number=number + 1, command=command, started=utc())
            report['rounds'].append(record)
            (folder / 'command.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
            proc = subprocess.Popen(command, cwd=ROOT, env=env)
            record['pid'] = proc.pid
            (folder / 'command.json').write_text(json.dumps(record, indent=2), encoding='utf-8')
            (output / 'result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
            phase = None
            deadline = time.monotonic() + (90 if args.probe else 600)
            try:
                while proc.poll() is None:
                    if time.monotonic() >= deadline:
                        raise TimeoutError('Player exceeded bounded performance run deadline')
                    progress = folder / 'progress.json'
                    if progress.exists():
                        try:
                            current = json.loads(progress.read_text())['phase']
                            if current != phase:
                                print(json.dumps(dict(round=number + 1, phase=current, utc=utc())), flush=True)
                                phase = current
                        except json.JSONDecodeError:
                            pass  # A read-only observation may race a progress update; never retry a game action.
                    time.sleep(2)
                record['exitCode'] = proc.returncode
                assert proc.returncode == 0, 'PLAYER: failed; inspect raw result and log'
                summary = analyse(folder, args.probe)
                expected = (folder / 'CatGame/save.json').resolve()
                actual = Path(summary['environment']['savePath'])
                assert actual.parent.resolve() == expected.parent and actual.name == 'save.json', 'HARNESS: isolation mismatch'
                record['summary'] = summary
                (folder / 'analysis.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
                assert not activity(), 'ENVIRONMENT: another Player or Unity appeared during the run'
            finally:
                if proc.poll() is None:
                    proc.terminate(); proc.wait(timeout=15)
                    record['forcedCleanup'] = True
                record['ended'] = utc()
                (output / 'result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
        report['passed'] = all(r['summary']['passed'] for r in report['rounds'])
    except Exception:
        report['error'] = traceback.format_exc()
    finally:
        report['ended'] = utc()
        (output / 'result.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps(dict(passed=report['passed'], output=str(output), error=report.get('error'))), flush=True)
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
