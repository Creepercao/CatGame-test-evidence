"""Business keywords against one owned Development Player, with isolated game saves."""
import hashlib
import json
import os
import subprocess
import time
from datetime import datetime, timezone
from pathlib import Path
from uuid import uuid4
from robot.api.deco import keyword, library
from robot.libraries.BuiltIn import BuiltIn


@library(scope='SUITE', auto_keywords=False)
class GameBusiness:
    ROBOT_LISTENER_API_VERSION = 3

    def __init__(self):
        self.ROBOT_LIBRARY_LISTENER = self
        self.process = None
        self.sequence = 0
        self.state = None
        self.failures = []

    def _record(self, action, result):
        self.sequence += 1
        with (self.output / 'steps.jsonl').open('a', encoding='utf-8') as stream:
            stream.write(json.dumps(dict(step=self.sequence, utc=datetime.now(timezone.utc).isoformat(),
                                         action=action, result=result), ensure_ascii=False) + '\n')

    @keyword
    def prepare_isolated_game(self):
        self.output = Path(BuiltIn().get_variable_value('${OUTPUT DIR}')) / ('business-' + uuid4().hex[:8])
        self.output.mkdir(parents=True, exist_ok=False)
        self.root = Path(__file__).resolve().parents[3]
        self.save_root = self.output / 'player'
        self.save_root.mkdir()
        self.exe = Path(os.environ.get('CATGAME_BUSINESS_EXE', str(self.root / 'Build/Quality/QA/CatGameQA.exe')))
        if not self.exe.is_file():
            raise AssertionError('ENVIRONMENT: build Business QA player first: ' + str(self.exe))
        dll = self.exe.parent / (self.exe.stem + '_Data/Managed/CatGame.Runtime.dll')
        self._record('prepare', dict(exe=str(self.exe), sha256=hashlib.sha256(self.exe.read_bytes()).hexdigest(),
                                    runtimeDll=str(dll), runtimeSha256=hashlib.sha256(dll.read_bytes()).hexdigest()))

    @keyword
    def launch_game(self):
        if self.process is not None and self.process.poll() is None:
            raise AssertionError('HARNESS: owned game is already running')
        ready = self.save_root / 'business-ready.flag'
        ready.unlink(missing_ok=True)
        env = os.environ.copy()
        for name in list(env):
            if name.startswith('CATGAME_QA_') or name.startswith('CATGAME_DEBUG_'):
                env.pop(name)
        env.update(CATGAME_PLAYER_QA_ROOT=str(self.save_root), CATGAME_QA_BUSINESS_ONLY='1', CATGAME_DEBUG_DISABLE='1')
        command = [str(self.exe), '-screen-fullscreen', '0', '-logFile', str(self.output / ('player-' + uuid4().hex[:8] + '.log'))]
        self.process = subprocess.Popen(command, cwd=self.root, env=env)
        self._record('launch', dict(command=command, pid=self.process.pid))
        self._wait(ready.is_file, 40)

    def _wait(self, predicate, seconds=15):
        end = time.monotonic() + seconds
        while time.monotonic() < end:
            if predicate():
                return
            if self.process.poll() is not None:
                raise AssertionError('ENVIRONMENT/PLAYER: early exit ' + str(self.process.returncode))
            time.sleep(.05)
        raise AssertionError('ENVIRONMENT/PLAYER: timed out; inspect Player log before classification')

    def _request(self, action, count=0):
        request_id = uuid4().hex
        wire = dict(id=request_id, action=action, count=int(count))
        pending = self.save_root / (request_id + '.tmp')
        pending.write_text(json.dumps(wire), encoding='utf-8')
        pending.replace(self.save_root / 'business-request.json')
        response = self.save_root / (request_id + '.json')
        self._wait(response.is_file)
        result = json.loads(response.read_text(encoding='utf-8-sig'))
        self._record(wire, result)
        self.last_screenshot = self.save_root / (request_id + '.png')
        assert result['id'] == request_id and result['pid'] == self.process.pid, 'HARNESS: response from unexpected process'
        # SaveSystem atomically replaces the file during quit. Canonicalize the stable
        # directory, not a file handle whose final name may change during replacement.
        actual = Path(result['savePath'])
        expected = self.save_root / 'CatGame/save.json'
        assert actual.parent.resolve() == expected.parent.resolve() and actual.name.lower() == 'save.json', (
            f'HARNESS: save isolation mismatch: actual={actual}, expected={expected}')
        assert result['ok'], 'PLAYER: ' + str(result.get('error'))
        self.state = result['state']
        return result

    @keyword
    def start_new_game(self):
        self._request('new')
        self.assert_milk_count(1)

    @keyword
    def pet_cat(self, count):
        before = self.state['clicks']
        self._request('click', count)
        assert self.state['clicks'] == before + int(count), 'PRODUCT: pet count mismatch'

    @keyword
    def buy_milk_cat(self):
        before = next(c['count'] for c in self.state['cats'] if c['id'] == 'Milk')
        self._request('buy')
        self.assert_milk_count(before + 1)

    @keyword
    def assert_milk_count(self, expected):
        actual = next(c['count'] for c in self.state['cats'] if c['id'] == 'Milk')
        assert actual == int(expected), f'PRODUCT: expected Milk={expected}, actual={actual}'

    @keyword
    def save_and_quit_game(self):
        self._request('quit')
        self.saved_state = self.state
        code = self.process.wait(timeout=20)
        self._record('normal-exit', dict(pid=self.process.pid, exitCode=code))
        assert code == 0, 'PLAYER: abnormal exit'

    @keyword
    def continue_saved_game(self):
        previous = self.saved_state
        self._request('continue')
        # Production/offline settlement may increase balances; identity and counts must persist.
        assert self.state['cats'] == previous['cats'], 'PRODUCT: cats lost after restart'
        assert self.state['clicks'] == previous['clicks'], 'PRODUCT: clicks lost after restart'
        assert self.state['catHair'] >= previous['catHair'], 'PRODUCT: balance decreased after restart'
        self._record('restart-verified', dict(catsPreserved=True, clicksPreserved=True, balanceNondecreasing=True))

    def end_keyword(self, data, result):
        if result.status != 'FAIL' or not hasattr(self, 'output'):
            return
        evidence = dict(keyword=data.name, error=result.message, classification='needs-triage')
        if getattr(self, 'last_screenshot', None) is not None:
            evidence['lastStepScreenshot'] = str(self.last_screenshot)
        if self.process is not None and self.process.poll() is None:
            try:
                reply = self._request('snapshot')
                evidence['screenshot'] = str(self.save_root / (reply['id'] + '.png'))
            except Exception as error:
                evidence['captureError'] = str(error)
        self.failures.append(evidence)
        (self.output / 'failures.json').write_text(json.dumps(self.failures, indent=2), encoding='utf-8')

    @keyword
    def close_owned_game(self):
        if self.process is None or self.process.poll() is not None:
            return
        try:
            self.save_and_quit_game()
        finally:
            if self.process.poll() is None:
                self.process.terminate()
                self.process.wait(timeout=10)
                self._record('forced-cleanup', dict(pid=self.process.pid, classification='harness-cleanup'))
