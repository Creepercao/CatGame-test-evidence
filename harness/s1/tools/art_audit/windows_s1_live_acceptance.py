"""Ordinary 1.2.0 acceptance: owned-window input, live commands, autosave and cold load.

Uses a declared QA fixture, snapshots the current four normal files privately, and
always restores them. Foreground mouse input is forbidden until the owned HWND is active.
"""
import ctypes as C
from ctypes import wintypes as W
import hashlib,json,os,re,subprocess,sys,time,zipfile
from pathlib import Path
from PIL import Image,ImageGrab,ImageChops,ImageStat

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'tools/external_debugger'))
from client import Client
from discovery import find_live_session

out=Path(sys.argv[1]).resolve();out.relative_to(ROOT);out.mkdir(exist_ok=False)
private=out/'private';private.mkdir()
old=json.loads((ROOT/'Build/S1/windows-user-save-backup/private-manifest.json').read_text(encoding='utf-8-sig'))
save_root=Path(old['source'])
exe=ROOT/'Build/S1/release-windows-02/player/CatGame.exe'
package=ROOT/'Build/S1/distribution-v1.2.0'
manifest=json.loads((package/'package-manifest.json').read_text(encoding='utf-8-sig'))
env=dict(os.environ,CATGAME_DEBUG_REGISTRY=str(out/'registry'))
for key in ['CATGAME_DEBUG_SESSION','CATGAME_DEBUG_DISABLE']:env.pop(key,None)
os.environ['CATGAME_DEBUG_REGISTRY']=env['CATGAME_DEBUG_REGISTRY']
user32=C.windll.user32
user32.GetForegroundWindow.restype=W.HWND
user32.SetForegroundWindow.argtypes=[W.HWND]
user32.IsWindow.argtypes=[W.HWND]
user32.IsWindowVisible.argtypes=[W.HWND]
user32.GetWindowThreadProcessId.argtypes=[W.HWND,C.POINTER(W.DWORD)]
user32.SetWindowTextW.argtypes=[W.HWND,W.LPCWSTR]
user32.GetClientRect.argtypes=[W.HWND,C.POINTER(W.RECT)]
user32.GetWindowRect.argtypes=[W.HWND,C.POINTER(W.RECT)]
user32.ClientToScreen.argtypes=[W.HWND,C.POINTER(W.POINT)]
user32.ShowWindow.argtypes=[W.HWND,C.c_int]
user32.PostMessageW.argtypes=[W.HWND,W.UINT,W.WPARAM,W.LPARAM]
try:user32.SetProcessDPIAware()
except AttributeError:pass
process=None;steps=[];snapshots={};modified=False
result=dict(passed=False,restored=False,runtimeGameSource=manifest['runtimeGameSource'],releaseSource=manifest['releaseSource'])
original_cursor=W.POINT();user32.GetCursorPos(C.byref(original_cursor));mouse_sent=False

def record(action,data):
    steps.append(dict(utc=time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime()),action=action,data=data))
    (out/'steps.json').write_text(json.dumps(steps,ensure_ascii=False,indent=2),encoding='utf-8')

def wait(predicate,seconds=30):
    end=time.monotonic()+seconds
    while time.monotonic()<end:
        value=predicate()
        if value:return value
        if process is not None and process.poll() is not None:raise AssertionError('Owned ordinary Player exited')
        time.sleep(.15)
    raise TimeoutError('Owned Player readiness/foreground deadline exceeded')

def owned(handle):
    pid=W.DWORD();user32.GetWindowThreadProcessId(handle,C.byref(pid))
    return bool(user32.IsWindow(handle) and pid.value==process.pid)

def window():
    handles=[];callback=C.WINFUNCTYPE(W.BOOL,W.HWND,W.LPARAM)
    def visit(handle,_):
        if owned(handle) and user32.IsWindowVisible(handle):
            kind=C.create_unicode_buffer(128);user32.GetClassNameW(handle,kind,128)
            rect=W.RECT();user32.GetClientRect(handle,C.byref(rect))
            if kind.value=='UnityWndClass' and rect.right>=1000 and rect.bottom>=600:handles.append(handle)
        return True
    user32.EnumWindows(callback(visit),0)
    return handles[0] if handles else None

def focus(handle,phase):
    assert owned(handle)
    title=f'CatGame 1.2.0 S1R - {phase} - PID {process.pid}'
    user32.SetWindowTextW(handle,title);user32.ShowWindow(handle,9);user32.SetForegroundWindow(handle)
    if user32.GetForegroundWindow()!=handle:
        request=dict(pid=process.pid,title=title,phase=phase,active=True)
        (out/'focus-needed.json').write_text(json.dumps(request),encoding='utf-8')
        print('WAITING_FOR_OWNED_WINDOW_FOCUS '+title,flush=True)
        record('await-owned-foreground',request)
        wait(lambda:owned(handle) and user32.GetForegroundWindow()==handle,600)
        request['active']=False
        (out/'focus-needed.json').write_text(json.dumps(request),encoding='utf-8')
    record('foreground-confirmed',dict(pid=process.pid,phase=phase,owned=True))

def click(handle,x,y,phase):
    global mouse_sent
    focus(handle,phase)
    # Allow focus and the user's title-bar release to reach Unity before clicking.
    stable_since=time.monotonic()
    def settled():
        nonlocal stable_since
        if user32.GetForegroundWindow()!=handle or user32.GetAsyncKeyState(1)&0x8000:
            stable_since=time.monotonic();return False
        return time.monotonic()-stable_since>=.8
    wait(settled,15)
    rect=W.RECT();user32.GetClientRect(handle,C.byref(rect))
    point=W.POINT(round(rect.right*x/1600),round(rect.bottom*y/900))
    client_point=[point.x,point.y];user32.ClientToScreen(handle,C.byref(point))
    assert owned(handle) and user32.GetForegroundWindow()==handle
    user32.SetCursorPos(point.x,point.y);time.sleep(.15)
    assert owned(handle) and user32.GetForegroundWindow()==handle
    mouse_sent=True;user32.mouse_event(2,0,0,0,0)
    try:time.sleep(.12)
    finally:user32.mouse_event(4,0,0,0,0)
    record('native-owned-window-click',dict(phase=phase,pid=process.pid,client=client_point,screen=[point.x,point.y],foregroundAtRelease=user32.GetForegroundWindow()==handle))

def screenshot(handle,name):
    focus(handle,'capture-'+name)
    time.sleep(.25)
    C.windll.dwmapi.DwmFlush()
    origin=W.POINT();user32.ClientToScreen(handle,C.byref(origin))
    client=W.RECT();user32.GetClientRect(handle,C.byref(client))
    assert owned(handle) and user32.GetForegroundWindow()==handle
    # Unity's flip-model surface can return a stale PrintWindow image. Capture
    # the real foreground client rectangle from the desktop instead.
    frame=ImageGrab.grab(bbox=(origin.x,origin.y,origin.x+client.right,origin.y+client.bottom)).convert('RGB')
    assert frame.size==(1600,900),frame.size
    frame.save(out/name);record('owned-window-screenshot',dict(file=name,size=frame.size,pid=process.pid))
    return frame

def room(handle,theme,name):
    frame=screenshot(handle,name)
    source=ROOT/f'Assets/Resources/Art/Room/Pixel/Growth/{theme}/architecture.png'
    expected=Image.open(source).convert('RGB').getpixel((322,32));point=(1006,100)
    actual=frame.getpixel(point)
    assert max(abs(a-b) for a,b in zip(actual,expected))<=2,(theme,actual,expected)
    record('room-source-pixel-match',dict(theme=theme,point=point,actual=actual,expected=expected))

def launch(phase):
    global process
    startup=subprocess.STARTUPINFO();startup.dwFlags|=subprocess.STARTF_USESHOWWINDOW;startup.wShowWindow=1
    process=subprocess.Popen([str(exe),'-screen-fullscreen','0','-screen-width','1600','-screen-height','900','-logFile',str(out/(phase+'-player.log'))],env=env,startupinfo=startup)
    record('launch-ordinary-release',dict(phase=phase,pid=process.pid,exeSha256=hashlib.sha256(exe.read_bytes()).hexdigest()))
    found=wait(find_live_session);assert found[1]['pid']==process.pid
    client=Client(found[0]);assert client.request()['blocked']
    time.sleep(4);handle=wait(window)
    for attempt in range(2):
        click(handle,1320,372,phase+'-continue')
        end=time.monotonic()+5
        while time.monotonic()<end:
            status=client.request()
            if status['visibleCats']>0:break
            time.sleep(.15)
        record('menu-entry-observation',dict(attempt=attempt+1,visibleCats=status['visibleCats'],blocked=status['blocked']))
        if status['visibleCats']>0:break
        # JsonUtility's inline class serialization is not a reliable null-state
        # signal. Retry only if actual pixels still show this same menu.
        frame=screenshot(handle,phase+f'-still-menu-{attempt+1}.png')
        references=ROOT/'Tests/robot/rooms/windows_references'
        metadata=json.loads((references/'provenance.json').read_text(encoding='utf-8'))
        errors={name:sum(ImageStat.Stat(ImageChops.difference(frame.crop(tuple(box)),Image.open(references/(name+'.png')).convert('RGB'))).mean)/3 for name,box in metadata['menuRegions'].items()}
        record('recognized-same-menu-before-retry',dict(meanRgbErrors=errors,limit=2.5))
        assert max(errors.values())<=2.5,'Unknown screen; no repeated continue click sent'
    assert status['visibleCats']>0,'Native continue did not enter the game after two recognized-menu attempts'
    time.sleep(.4);status=client.request()
    if status['blocked']:
        frame=screenshot(handle,phase+'-modal.png')
        references=ROOT/'Tests/robot/rooms/windows_references'
        metadata=json.loads((references/'provenance.json').read_text(encoding='utf-8'))
        errors={}
        for name,box in metadata['regions'].items():
            reference=Image.open(references/(name+'.png')).convert('RGB')
            errors[name]=sum(ImageStat.Stat(ImageChops.difference(frame.crop(tuple(box)),reference)).mean)/3
        record('offline-reference-error',dict(platform='Windows',meanRgbErrors=errors,limit=2.5))
        assert max(errors.values())<=2.5,'Unknown modal; no dismissal click sent'
        click(handle,800,519,phase+'-offline-collect')
    wait(lambda:not client.request()['blocked'])
    return client,handle

def stop():
    global process
    if process is not None and process.poll() is None:
        h=window()
        if h:user32.PostMessageW(h,0x10,0,0)
        try:process.wait(timeout=15)
        except subprocess.TimeoutExpired:process.terminate();process.wait(timeout=15)
        record('owned-player-stopped',dict(pid=process.pid,exitCode=process.returncode))
    process=None

try:
    assert subprocess.check_output(['powershell','-NoProfile','-Command',"@(Get-Process CatGame,CatGameQA,Unity -ErrorAction SilentlyContinue | Where-Object {$_.Path -notlike '*AppData*Unity*bin*'}).Count"],text=True).strip()=='0','Existing game/Editor present'
    assert hashlib.sha256((package/'CatGame-Windows-x64-v1.2.0.zip').read_bytes()).hexdigest()==manifest['zipSha256']
    for row in manifest['files']:
        if row['path'].startswith('Debugger/') or row['path']=='Readme-Install.txt':continue
        assert hashlib.sha256((exe.parent/row['path']).read_bytes()).hexdigest()==row['sha256'],row['path']
    for row in old['files']:
        relative=row['relative'];raw=(save_root/relative).read_bytes();snapshots[relative]=raw
        dest=private/relative;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(raw)
    record('private-current-snapshot',dict(fileCount=len(snapshots),originalContentsPublished=False))
    fixture=json.loads((ROOT/'Build/F1/android-touch-09/compact-save-after.json').read_text(encoding='utf-8-sig'))
    fixture.update(lastSeenUnix=0,totalEarned=90000,catHair=200,pendingStoryEvents=[])
    fixture['seenStoryEvents']=re.findall(r'new StoryDefinition\("([^"]+)"',(ROOT/'Assets/Scripts/Idle/StoryCatalog.cs').read_text(encoding='utf-8-sig'))
    assert len(fixture['seenStoryEvents'])==20
    (out/'explicit-fixture.json').write_text(json.dumps(fixture),encoding='utf-8')
    modified=True;(save_root/'CatGame/save.json').write_text(json.dumps(fixture),encoding='utf-8')
    record('explicit-qa-fixture',dict(totalEarned=90000,catHair=200,owned=2,allStoryCgsSeen=True,naturalPlaythrough=False))
    client,handle=launch('first');first_pid=process.pid
    assert client.request()['visibleCats']==2
    room(handle,'Starter','starter.png')
    status=client.request('SetHair',value=1000000,earned=False)
    record('set-balance-command-reply',dict(catHair=status['state']['catHair'],totalEarned=status['state']['totalEarned']))
    time.sleep(.4)
    assert client.request()['state']['totalEarned']<100000
    room(handle,'Starter','balance-only-starter.png')
    record('balance-only-does-not-grow-room',dict(balance=1000000))
    status=client.request('AddHair',value=20000,earned=True)
    record('add-earned-command-reply',dict(catHair=status['state']['catHair'],totalEarned=status['state']['totalEarned']))
    time.sleep(1.2);wait(lambda:not client.request()['blocked'])
    room(handle,'Companions','earned-companions.png')
    status=client.request('SetCat','Milk',3);assert status['visibleCats']==3
    client.request('SetHair',value=0,earned=False);time.sleep(.5)
    room(handle,'Companions','spent-companions.png')
    record('live-debug-commands',dict(sharedClientWithExternalDebugger=True,actualDebuggerGuiFieldsClicked=False,ownedCats=3,totalEarned=client.request()['state']['totalEarned']))
    with zipfile.ZipFile(package/'CatGame-Windows-x64-v1.2.0.zip') as z:debugger=z.read('Debugger/CatGameDebugger.exe')
    expected=next(r['sha256'] for r in manifest['files'] if r['path']=='Debugger/CatGameDebugger.exe')
    assert hashlib.sha256(debugger).hexdigest()==expected
    path=out/'CatGameDebugger.exe';path.write_bytes(debugger)
    subprocess.run([str(path),'--smoke-output',str(out/'debugger-smoke.json'),'--expected-pid',str(process.pid)],env=env,check=True,timeout=30)
    proof=json.loads((out/'debugger-smoke.json').read_text(encoding='utf-8'));assert proof['connected'] and proof['frozen'] and proof['pid']==process.pid
    record('distributed-debugger-auto-connect',proof)
    def saved():
        state=json.loads((save_root/'CatGame/save.json').read_text(encoding='utf-8-sig'))
        return state if next(c['count'] for c in state['cats'] if c['id']=='Milk')==3 and state['totalEarned']>=110000 and state['catHair']<1000 else None
    actual=wait(saved,20)
    (out/'autosaved-state.json').write_text(json.dumps(actual),encoding='utf-8')
    record('game-autosaved-live-edits',dict(ownedCats=3,totalEarned=actual['totalEarned'],catHair=actual['catHair']))
    stop();time.sleep(2)
    client,handle=launch('restart');assert process.pid!=first_pid
    state=client.request()['state'];assert client.request()['visibleCats']==3 and state['totalEarned']>=110000 and state['catHair']<1000
    room(handle,'Companions','restart-companions.png')
    record('fresh-process-restored-live-edits',dict(firstPid=first_pid,restartPid=process.pid,ownedCats=3,totalEarned=state['totalEarned'],unblocked=True))
    result.update(passed=True,nativeMenuInput=True,sharedClientLiveEdits=True,distributedDebuggerConnected=True,gameAutosaved=True,coldRestartVerified=True)
except Exception as error:
    result['error']=repr(error);record('failure',repr(error))
finally:
    stop()
    if modified:
        for relative,raw in snapshots.items():(save_root/relative).write_bytes(raw)
        result['restored']=all((save_root/r).read_bytes()==raw for r,raw in snapshots.items())
    if mouse_sent:user32.SetCursorPos(original_cursor.x,original_cursor.y)
    if (out/'focus-needed.json').exists():
        marker=json.loads((out/'focus-needed.json').read_text(encoding='utf-8'));marker['active']=False
        (out/'focus-needed.json').write_text(json.dumps(marker),encoding='utf-8')
    record('final-private-restoration',dict(restored=result['restored'],fileCount=len(snapshots),ownedPlayerExited=process is None))
    (out/'result.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
print(json.dumps(result),flush=True)
raise SystemExit(0 if result['passed'] and result['restored'] else 1)
