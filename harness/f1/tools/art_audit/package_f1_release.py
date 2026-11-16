"""Package previously validated ordinary players; read back every Windows ZIP entry."""
import hashlib
import json
import shutil
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
base = root / 'Build/F1/distribution-v1.1.0'
base.mkdir(exist_ok=True)
source = root / 'Build/F1/distribution-candidate/CatGame-Windows-x64-v1.1.0-candidate.zip'
target = base / 'CatGame-Windows-x64-v1.1.0.zip'
readme = '''喵屋 CatGame 1.1.0 · Windows x64
完整解压后运行 CatGame.exe，保留 CatGame_Data、UnityPlayer.dll 和 MonoBleedingEdge。
无需安装 Unity 或 Python。可选修改器：Debugger/CatGameDebugger.exe。
先进入游戏正常玩法，修改器自动连接；修改运行状态，由游戏自动保存。
存档：%USERPROFILE%/AppData/LocalLow/CatGameStudio/喵屋；重新解压不清除进度。
与 Android APK 共用同一个 v1.1.0 Release，附件 SHA256SUMS.txt 可核对完整包。
本版统一世界素材尺寸、脚点、地面锚点与像素显示；玩法数值、存档格式保持兼容。
运行时代码提交 f536871，发行标签增加 README 说明，游戏内容相同。
https://github.com/Creepercao/CatGame/releases/tag/v1.1.0
'''.encode('utf-8-sig')
files = []
with zipfile.ZipFile(source) as src, zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as dst:
    assert src.testzip() is None
    for name in src.namelist():
        data = readme if name == 'Readme-Install.txt' else src.read(name)
        dst.writestr(name, data)
        files.append(dict(path=name, bytes=len(data), sha256=hashlib.sha256(data).hexdigest()))
with zipfile.ZipFile(target) as z:
    assert z.testzip() is None and len(z.namelist()) == len(files) == 152
    for item in files:
        assert hashlib.sha256(z.read(item['path'])).hexdigest() == item['sha256']
    assert hashlib.sha256(z.read('CatGame.exe')).hexdigest() == hashlib.sha256((root/'Build/F1/windows-candidate-02/CatGame.exe').read_bytes()).hexdigest()
    assert hashlib.sha256(z.read('Debugger/CatGameDebugger.exe')).hexdigest() == 'd379f848f4e6637a3d2355e787c752789f7e3f3dfef31a497e20fbea710dd340'
apk = base / 'CatGame-Android.apk'
shutil.copyfile(root / 'Build/F1/android-candidate-02/CatGame-Android.apk', apk)
assert hashlib.sha256(apk.read_bytes()).hexdigest() == '8e32a1f2b1192ac7fba2bb15bf904feaf8015b4cf730f8a6113026d9ffa44d9f'
(base/'Install-Android.txt').write_bytes('''喵屋 CatGame 1.1.0 · Android ARM64 · versionCode 6
包名 com.catgamestudio.catgame。横屏、核心玩法离线运行，无需外置修改器。
下载 APK 后允许浏览器/文件管理器安装应用，按系统提示安装；或 adb install -r CatGame-Android.apk。
沿用既有签名，覆盖安装保留存档；卸载应用可能清除进度，请勿以卸载替代覆盖升级。
本版在 Android 14 AVD 完成实际 code5→6 升级、三比例触摸、结局和刘海安全区验收。
本轮未重测真机性能，不能保证所有机型表现；旧版真机结果不充作本版新测。
Windows 包（含可选修改器）在同一个 v1.1.0 Release，旧版本发行继续保留。
'''.encode('utf-8-sig'))
checks = '\n'.join(hashlib.sha256((base/name).read_bytes()).hexdigest()+'  '+name for name in
                   ['CatGame-Windows-x64-v1.1.0.zip','CatGame-Android.apk','Install-Android.txt'])+'\n'
(base/'SHA256SUMS.txt').write_bytes(checks.encode())
manifest = dict(runtimeGameSource='f5368713d14df8aee0b1854f2520eb66e806663d',
                releaseSource='1b82df94157bbcf370421207ed1d342ac272bb48',
                onlyReleaseSourceDifference='README.md', debuggerSource='released v1.0.1 EXE',
                files=files, zipSha256=hashlib.sha256(target.read_bytes()).hexdigest(),
                apkSha256=hashlib.sha256(apk.read_bytes()).hexdigest(), all152ZipFilesReadBack=True)
(base/'package-manifest.json').write_bytes((json.dumps(manifest,indent=2)+'\n').encode())
print(checks, 'Verified all 152 ZIP entries; ordinary APK unchanged')
