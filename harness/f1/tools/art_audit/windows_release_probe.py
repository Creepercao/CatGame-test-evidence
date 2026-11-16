"""Ordinary Windows binary + frozen debugger probe; restore the approved private save backup."""
import ctypes as C
from ctypes import wintypes as W
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time
import zipfile

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'tools/external_debugger'))
from client import Client
from discovery import find_live_session

out = ROOT / (sys.argv[1] if len(sys.argv) > 1 else 'Build/F1/windows-release-probe-01')
out.mkdir(exist_ok=False)
backup = ROOT / 'Build/F1/windows-user-save-backup'
manifest = json.loads((backup / 'private-manifest.json').read_text(encoding='utf-8-sig'))
save_root = Path(manifest['source'])
steps = []
process = None
restore_needed = False
result = {'passed': False, 'restored': False, 'gameSource': 'f536871'}

def record(action, data):
    steps.append({'utc': time.strftime('%Y-%m-%dT%H:%M:%SZ', time.gmtime()), 'action': action, 'data': data})
    (out / 'steps.json').write_bytes(json.dumps(steps, ensure_ascii=False, indent=2).encode())

def wait(predicate, seconds=30):
    end = time.monotonic() + seconds
    while time.monotonic() < end:
        value = predicate()
        if value:
            return value
        if process is not None and process.poll() is not None:
            raise AssertionError('Owned ordinary Player exited early')
        time.sleep(.1)
    raise TimeoutError('Ordinary Player readiness or foreground timeout')

try:
    for row in manifest['files']:
        raw = (backup / row['relative']).read_bytes()
        assert hashlib.sha256(raw).hexdigest().upper() == row['sha256'].upper()
        assert (save_root / row['relative']).read_bytes() == raw, 'Original changed since backup; abort'
    fixture = json.loads((ROOT / 'Build/F1/android-touch-09/compact-save-after.json').read_text(encoding='utf-8-sig'))
    fixture.update(lastSeenUnix=0, pendingStoryEvents=[])
    restore_needed = True
    (save_root / 'CatGame/save.json').write_bytes(json.dumps(fixture).encode())
    env = os.environ.copy()
    env['CATGAME_DEBUG_REGISTRY'] = str(out / 'registry')
    os.environ['CATGAME_DEBUG_REGISTRY'] = env['CATGAME_DEBUG_REGISTRY']
    env.pop('CATGAME_DEBUG_DISABLE', None)
    env.pop('CATGAME_DEBUG_SESSION', None)
    exe = ROOT / 'Build/F1/windows-candidate-02/CatGame.exe'
    process = subprocess.Popen([str(exe), '-screen-fullscreen', '0', '-logFile', str(out / 'player.log')], env=env)
    record('launch-ordinary-binary', {'pid': process.pid, 'sha256': hashlib.sha256(exe.read_bytes()).hexdigest()})
    found = wait(find_live_session)
    client = Client(found[0])
    assert found[1]['pid'] == process.pid
    assert client.request()['blocked'], 'Main menu writes must be blocked'
    record('discovered-ordinary-menu', {'pid': process.pid, 'blocked': True})
    user32 = C.windll.user32
    user32.GetForegroundWindow.restype = W.HWND
    user32.SetForegroundWindow.argtypes = [W.HWND]
    callback_type = C.WINFUNCTYPE(W.BOOL, W.HWND, W.LPARAM)
    handles = []
    def visit(handle, _):
        pid = W.DWORD()
        user32.GetWindowThreadProcessId(handle, C.byref(pid))
        if pid.value == process.pid and user32.IsWindowVisible(handle):
            kind = C.create_unicode_buffer(128)
            user32.GetClassNameW(handle, kind, len(kind))
            if kind.value == 'UnityWndClass':
                handles.append(handle)
        return True
    wait(lambda: (handles.clear(), user32.EnumWindows(callback_type(visit), 0), handles)[-1])
    # The connection endpoint can be ready while the splash window is still being replaced.
    time.sleep(3)
    handles.clear()
    wait(lambda: (handles.clear(), user32.EnumWindows(callback_type(visit), 0), handles)[-1])
    handle = handles[0]
    record('owned-unity-window', {'handle': handle})
    user32.ShowWindow(handle, 9)
    user32.SetForegroundWindow(handle)
    wait(lambda: user32.GetForegroundWindow() == handle, 45)
    rect = W.RECT()
    user32.GetClientRect(handle, C.byref(rect))
    point = W.POINT(round(rect.right * .825), round(rect.bottom * 372 / 900))
    user32.ClientToScreen(handle, C.byref(point))
    user32.SetCursorPos(point.x, point.y)
    time.sleep(.2)
    user32.mouse_event(2, 0, 0, 0, 0)
    time.sleep(.12)
    user32.mouse_event(4, 0, 0, 0, 0)
    status = wait(lambda: (s if not s['blocked'] and s['state'] else None) if (s := client.request()) else None)
    assert sum(c['count'] for c in status['state']['cats']) == 2
    record('actual-menu-continue-loaded-old-schema', {'visibleCats': status['visibleCats'], 'clicks': status['state']['clicks']})
    status = client.request('SetHair', value=12345)
    assert abs(status['state']['catHair'] - 12345) < .01
    status = client.request('SetCat', 'Milk', 3)
    assert status['visibleCats'] == 3
    packaged = ROOT / 'Build/Releases/v1.0.1/CatGame-Windows-x64-v1.0.1.zip'
    debugger = out / 'CatGameDebugger.exe'
    with zipfile.ZipFile(packaged) as z:
        names = [n for n in z.namelist() if n.endswith('/Debugger/CatGameDebugger.exe') or n == 'Debugger/CatGameDebugger.exe']
        assert len(names) == 1
        debugger.write_bytes(z.read(names[0]))
    smoke = subprocess.run([str(debugger), '--smoke-output', str(out / 'debugger-smoke.json'),
                            '--expected-pid', str(process.pid)], env=env, timeout=30)
    assert smoke.returncode == 0
    proof = json.loads((out / 'debugger-smoke.json').read_text())
    assert proof['connected'] and proof['frozen'] and proof['pid'] == process.pid
    record('packaged-exe-auto-connect', proof)
    time.sleep(11)
    saved = json.loads((save_root / 'CatGame/save.json').read_text(encoding='utf-8-sig'))
    assert next(c['count'] for c in saved['cats'] if c['id'] == 'Milk') == 3
    assert saved['catHair'] >= 12345
    record('game-autosaved-live-edits', {'cats': 3, 'catHair': saved['catHair']})
    result['passed'] = True
except Exception as error:
    result['error'] = repr(error)
    record('failure', repr(error))
finally:
    if process is not None and process.poll() is None:
        process.terminate()
        process.wait(timeout=15)
    if restore_needed:
        for row in manifest['files']:
            target = save_root / row['relative']
            target.write_bytes((backup / row['relative']).read_bytes())
            assert hashlib.sha256(target.read_bytes()).hexdigest().upper() == row['sha256'].upper()
    result['restored'] = True
    (out / 'result.json').write_bytes(json.dumps(result, indent=2).encode())
print(json.dumps(result))
raise SystemExit(0 if result['passed'] and result['restored'] else 1)
