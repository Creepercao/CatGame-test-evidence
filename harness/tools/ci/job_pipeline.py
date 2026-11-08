"""Jenkins job stages. No delivery archive exists unless every gate passes."""
import ast
import hashlib
import json
import os
import platform
import subprocess
import sys
import zipfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def utc():
    return datetime.now(timezone.utc).isoformat()


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, text=True).strip()


def main():
    number = os.environ.get('BUILD_NUMBER', '')
    if not number.isdecimal() or not os.environ.get('JENKINS_URL'):
        raise SystemExit('This entry requires a real Jenkins build environment')
    output = ROOT / 'Tests/robot/results' / ('ci-' + number)
    output.mkdir(parents=True, exist_ok=False)
    packages = ROOT / 'Build/ci/packages' / number
    delivery = ROOT / 'Build/ci/deliveries' / number
    state = dict(started=utc(), number=int(number), url=os.environ.get('BUILD_URL'),
                 trigger='manual', commit=git('rev-parse', 'HEAD'), python=sys.version,
                 platform=platform.platform(), stages=[], releaseAllowed=False)
    env = os.environ.copy()
    env.update(PYTHONIOENCODING='utf-8', CATGAME_CI_DELIVERY=str(packages),
               CATGAME_EDITOR_QA_ROOT=str(output / 'isolated-editor'))
    exit_code = 1

    def save():
        (output / 'pipeline.json').write_text(json.dumps(state, indent=2), encoding='utf-8')

    def stage(name, command):
        row = dict(name=name, started=utc(), command=command)
        state['stages'].append(row)
        save()
        print('CI STAGE ' + name, flush=True)
        with (output / (name + '.log')).open('w', encoding='utf-8') as log:
            row['exitCode'] = subprocess.run(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT).returncode
        row['ended'] = utc()
        save()
        if row['exitCode'] != 0:
            raise RuntimeError('Stage failed: ' + name)

    def unity(name, method):
        stage(name, [sys.executable, 'tools/qa/run_unity.py', '--output',
                     str(output / name), '--method', 'CatGame.EditorTools.CatGameSetup.' + method])

    def robot(name, profile=None, suite=None):
        command = [sys.executable, 'tools/qa/run_suite.py', '--output', str(output / name)]
        command += ['--profile', profile] if profile else ['--suite', str(suite)]
        stage(name, command)
        verdict = json.loads((output / name / 'result.json').read_text(encoding='utf-8'))
        if not verdict['releaseAllowed']:
            raise RuntimeError('Robot quality gate refused: ' + name)

    try:
        if git('status', '--porcelain'):
            raise RuntimeError('CI requires a clean committed source tree')
        sources = git('ls-files').splitlines()
        fingerprints = []
        for source in sources:
            path = ROOT / source
            if path.is_file():
                fingerprints.append(dict(path=source, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
            if path.suffix == '.py':
                ast.parse(path.read_text(encoding='utf-8-sig'), filename=source)
        (output / 'source-hashes.json').write_text(json.dumps(fingerprints, indent=2), encoding='utf-8')
        unity('compile', 'CICompileCheck')
        robot('smoke', profile='smoke')
        # Controlled erroneous configuration and its correction exercise a real CI gate.
        expected = 99 if os.environ.get('VERIFY_TEST_FAILURE', 'false').lower() == 'true' else 2
        fixture = output / 'ci-gate.robot'
        fixture.write_text('*** Test Cases ***\nConfigured Ownership Contract\n'
                           f'    Should Be Equal As Integers    {expected}    2\n', encoding='utf-8')
        robot('configuration-gate', suite=fixture)
        robot('full', profile='full')
        method = 'CIControlledBuildFailure' if os.environ.get('VERIFY_BUILD_FAILURE', 'false').lower() == 'true' else 'BuildCIPackage'
        unity('build', method)
        env['CATGAME_BUSINESS_EXE'] = str(packages / 'QA/CatGameQA.exe')
        robot('business', profile='business')
        if git('rev-parse', 'HEAD') != state['commit'] or git('status', '--porcelain'):
            raise RuntimeError('Source changed during build; refuse delivery')
        binaries = [p for p in packages.rglob('*') if p.is_file()
                    and not any('DoNotShip' in part for part in p.parts)]
        if not (packages / 'Release/CatGame.exe').is_file() or not (packages / 'QA/CatGameQA.exe').is_file():
            raise RuntimeError('Missing built players')
        hashes = [dict(path=str(p.relative_to(packages)), bytes=p.stat().st_size,
                       sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in binaries]
        delivery.mkdir(parents=True, exist_ok=False)
        (delivery / 'binary-hashes.json').write_text(json.dumps(hashes, indent=2), encoding='utf-8')
        archive = delivery / 'CatGame-Windows.zip'
        with zipfile.ZipFile(archive, 'x', compression=zipfile.ZIP_DEFLATED, compresslevel=1) as stream:
            for path in binaries:
                stream.write(path, str(path.relative_to(packages)))
        state.update(releaseAllowed=True, archive=str(archive),
                     archiveSha256=hashlib.sha256(archive.read_bytes()).hexdigest(), binaryFiles=len(binaries))
        exit_code = 0
    except Exception as error:
        state['error'] = str(error)
        print('CI BLOCKED: ' + str(error), flush=True)
    finally:
        state['ended'] = utc()
        state['exitCode'] = exit_code
        save()
        print(json.dumps(state), flush=True)
    return exit_code


if __name__ == '__main__':
    raise SystemExit(main())
