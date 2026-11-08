"""Run Robot profiles with immutable per-run evidence and a fail-closed gate."""
import argparse
import hashlib
import json
import os
import platform
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROFILES = {
    'smoke': ['Tests/robot/tests/smoke.robot'],
    'full': ['Tests/robot/tests/unity_tests.robot'],
    'external': ['Tests/robot/tests/external_debugger.robot'],
    'business': ['Tests/robot/tests/business.robot'],
    'performance-probe': ['Tests/robot/tests/performance_probe.robot'],
    'performance': ['Tests/robot/tests/performance.robot'],
}


def utc():
    return datetime.now(timezone.utc).isoformat()


def git_value(*args):
    run = subprocess.run(['git', *args], cwd=ROOT, capture_output=True, text=True)
    return run.stdout.strip() if run.returncode == 0 else None


def verdict(exit_code, report):
    """Neither exit code nor XML alone proves a successful run."""
    try:
        root = ET.parse(report).getroot()
        counts = root.find('statistics/total/stat')
        passed, failed, skipped = (int(counts.get(k, '0')) for k in ('pass', 'fail', 'skip'))
        errors = [n.text for n in root.findall('errors/msg')]
        allowed = exit_code == 0 and passed > 0 and failed == 0 and skipped == 0 and not errors
        return dict(releaseAllowed=allowed, passed=passed, failed=failed, skipped=skipped,
                    robotErrors=errors, reason='passed' if allowed else 'test-or-execution-failure')
    except (OSError, ET.ParseError, AttributeError, ValueError) as error:
        return dict(releaseAllowed=False, reason='missing-or-invalid-report', error=str(error))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--profile', choices=PROFILES, default='smoke')
    parser.add_argument('--output', help='Must be a new directory under Tests/robot/results')
    parser.add_argument('--suite', action='append', help='Explicit suite for harness checks or targeted runs')
    parser.add_argument('--include', action='append', default=[])
    parser.add_argument('--unity', default=os.environ.get('UNITY_EXE', r'D:\Unity\2022.3.21f1\Editor\Unity.exe'))
    args = parser.parse_args()
    run_id = datetime.now().strftime('%Y%m%d-%H%M%S') + '-' + uuid.uuid4().hex[:8]
    evidence_root = ROOT / 'Tests/robot/results'
    out = (ROOT / args.output).resolve() if args.output else evidence_root / ('quality-' + run_id)
    if not out.is_relative_to(evidence_root.resolve()):
        parser.error('Output must stay under Tests/robot/results')
    out.mkdir(parents=True, exist_ok=False)
    suites = [Path(s).resolve() for s in (args.suite or [str(ROOT / s) for s in PROFILES[args.profile]])]
    command = [sys.executable, '-m', 'robot', '--outputdir', str(out)]
    for tag in args.include:
        command.extend(['--include', tag])
    command.extend(str(s) for s in suites)
    env = os.environ.copy()
    env.update(UNITY_EXE=args.unity, CATGAME_PROJECT=str(ROOT), PYTHONIOENCODING='utf-8')
    vendor = ROOT / 'Tests/robot/vendor'
    if vendor.is_dir():
        env['PYTHONPATH'] = str(vendor) + os.pathsep + env.get('PYTHONPATH', '')
    metadata = dict(runId=run_id, started=utc(), profile=args.profile, explicitSuites=bool(args.suite),
                    commit=git_value('rev-parse', 'HEAD'), worktree=git_value('status', '--porcelain'),
                    python=sys.version, platform=platform.platform(), unity=args.unity,
                    command=command, suites=[dict(path=str(s), sha256=hashlib.sha256(s.read_bytes()).hexdigest()
                                                if s.is_file() else None) for s in suites])
    (out / 'run.json').write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding='utf-8')
    code = None
    failure = None
    try:
        with (out / 'console.log').open('w', encoding='utf-8') as log:
            code = subprocess.run(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT).returncode
    except OSError as error:
        failure = str(error)
    result = verdict(code, out / 'output.xml')
    result.update(runId=run_id, ended=utc(), exitCode=code, launchError=failure,
                  classification='passed' if result['releaseAllowed'] else 'needs-triage')
    # Failed tests require evidence-based triage; do not guess product vs harness vs environment.
    (out / 'result.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
    artifacts = [dict(path=str(p.relative_to(out)), bytes=p.stat().st_size,
                      sha256=hashlib.sha256(p.read_bytes()).hexdigest())
                 for p in out.rglob('*') if p.is_file()]
    (out / 'artifacts.json').write_text(json.dumps(artifacts, indent=2), encoding='utf-8')
    print(json.dumps(dict(output=str(out), **result), ensure_ascii=False))
    return 0 if result['releaseAllowed'] else 1


if __name__ == '__main__':
    raise SystemExit(main())
