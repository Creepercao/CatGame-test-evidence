"""Package only S1 release-gated ordinary Players and the previously shipped debugger."""
import hashlib
import json
import shutil
import zipfile
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'Build/S1/distribution-v1.2.0'
BASE.mkdir(exist_ok=True)
def sha(data):return hashlib.sha256(data).hexdigest()
builds=['release-windows-02','release-android-02']
for run in builds:
    path=ROOT/'Build/S1'/run
    assert (path/'exit-code.txt').read_text(encoding='utf-8-sig').strip()=='0'
    assert 'error CS' not in (path/'Editor.log').read_text(encoding='utf-8-sig')
sources=[(ROOT/'Build/S1'/run/'source.txt').read_text(encoding='utf-8-sig').strip() for run in builds]
assert sources[0]==sources[1]
player=ROOT/'Build/S1/release-windows-02/player'
files={p.relative_to(player).as_posix():p.read_bytes() for p in player.rglob('*') if p.is_file()}
assert 'CatGame.exe' in files and 'UnityPlayer.dll' in files
with zipfile.ZipFile(ROOT/'Build/F1/distribution-v1.1.0/CatGame-Windows-x64-v1.1.0.zip') as old:
    debugger=old.read('Debugger/CatGameDebugger.exe')
assert sha(debugger)=='d379f848f4e6637a3d2355e787c752789f7e3f3dfef31a497e20fbea710dd340'
files['Debugger/CatGameDebugger.exe']=debugger
files['Readme-Install.txt']='''喵屋 CatGame 1.2.0 · Windows x64
完整解压后运行CatGame.exe，保留CatGame_Data、UnityPlayer.dll和MonoBleedingEdge。
可选外置修改器：Debugger/CatGameDebugger.exe；进入游戏后自动连接，改动由游戏保存。
无需安装Unity或Python。本版新增两套像素房间；累计产出达到100000后迁入伙伴客厅。
消费不会使房间退回，猫咪、用品和存档进度保留；后续量级暂沿用伙伴客厅。
Android APK与Windows在同一个v1.2.0 Release；旧版本继续保留。
https://github.com/Creepercao/CatGame/releases/tag/v1.2.0
'''.encode('utf-8-sig')
zip_path=BASE/'CatGame-Windows-x64-v1.2.0.zip'
with zipfile.ZipFile(zip_path,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for name,data in sorted(files.items()):z.writestr(name,data)
with zipfile.ZipFile(zip_path) as z:
    assert z.testzip() is None
    assert set(z.namelist())==set(files)
    for name,data in files.items():assert z.read(name)==data
apk=BASE/'CatGame-Android.apk'
shutil.copyfile(ROOT/'Build/S1/release-android-02/CatGame-Android.apk',apk)
(BASE/'Install-Android.txt').write_text('''喵屋 CatGame 1.2.0 · Android ARM64 · code7
横屏单机游戏，包名com.catgamestudio.catgame，沿用既有签名。
允许浏览器/文件管理器安装APK，覆盖安装保留进度；不要先卸载旧版。
新增累计产出驱动的两套像素房间，与Windows同一Release发布。
本轮使用隔离Android14 AVD进行升级、场景和触摸验证，未重新测试真机性能。
''',encoding='utf-8-sig')
names=[zip_path.name,apk.name,'Install-Android.txt']
(BASE/'SHA256SUMS.txt').write_text(''.join(f'{sha((BASE/n).read_bytes())}  {n}\n' for n in names),encoding='utf-8')
manifest=dict(runtimeGameSource=sources[0],allZipEntriesReadBack=True,entryCount=len(files),
              zipSha256=sha(zip_path.read_bytes()),apkSha256=sha(apk.read_bytes()),
              files=[dict(path=n,bytes=len(data),sha256=sha(data)) for n,data in sorted(files.items())])
(BASE/'package-manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('Packaged source',sources[0],'verified ZIP entries',len(files),'APK',manifest['apkSha256'])
