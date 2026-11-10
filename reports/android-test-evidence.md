# CatGame Android 自动化与版本验收

测试对象为 Unity 2022.3.21f1 导出的 ARM64 APK。应用包名为 `com.catgamestudio.catgame`，首包 `versionCode=1`，应用图标版 `versionCode=2`，两者使用同一签名。本报告单列 Android 结果，Windows CI 和性能数字仍按各自的环境与版本解读。

## 用例与证据

| 风险 | 操作与断言 | 实际结果 |
|---|---|---|
| 购买后短时间切后台，随后进程被终止 | 新档自然攒猫毛，购买第二只猫后立即按 Home。核对前台 Activity、终止旧进程、冷启动继续、HUD 猫数量 | MuMu 通过，购买点击至 Home 为 0.094 秒，PID 8991 → 9595，恢复两只猫。[Robot XML](../evidence/android/mumu-smoke.xml) |
| 横屏宽度变化导致界面无法使用 | 在同一 MuMu 上分别用 20:9 和 4:3，核对菜单与离线面板，拖商店并比较列表像素区域，暂停后打开成就页，再恢复 | 两种比例通过。成就页左右边缘由人工检查截图。[Robot XML](../evidence/android/mumu-layout.xml) |
| 锁屏后触控或进度丢失 | Appium 锁屏，核对设备进入休眠，解锁后比对猫数与 PID，触摸打开设置并返回 | MuMu 电源键未触发休眠，保留失败。Android Studio API 34 AVD 在 v2 上通过，PID 13840 保持一致。[MuMu 原始失败](../evidence/android/mumu-lock.xml) · [AVD 复测](../evidence/android/avd-lock-v2.xml) |
| 覆盖安装导致已有进度或配置丢失 | Appium 在 v1 购买猫抓板并修改数字格式，ADB 按原包名覆盖到 v2，分别比较首次启动前后的存档字段和偏好 | 启动前解析后的完整存档一致，启动后猫、道具、成就、引导、已看 CG 与数字格式一致。[升级证据](../evidence/android/upgrade) |
| 主档损坏或缺失后无法继续 | 在旧实现复现，修复后覆盖主档损坏、缺失、两份均损坏、文件替换失败，以及启动恢复后主动重开新档。再对 Android Player 的独立 QA 用户注入损坏与缺失 | 旧实现在存档子集失败 5 项。修复后 Unity EditMode 55/55、PlayMode 87/87，Android v3 两种故障均从菜单继续两猫存档。[NUnit、Player 证据](../evidence/android/backup) |

## Appium 与断言边界

Robot Framework 7.5 编排。Python 调用 Appium 3.4.2 与 UiAutomator2 8.6.0 执行触摸、滑动、按键、锁屏和截图。Android 控件树只有 `unitySurfaceView`，游戏按钮的点击和界面断言采用固定分辨率下的坐标与截图裁片。ADB 用于启动、强停、查看进程状态和读取隔离测试用户的存档。测试用户由 QA 单独创建，所有状态证据来自游戏内产生的测试存档。

锁屏失败的判断依据包括 MuMu 游戏与桌面都无法用电源键进入休眠。失败保留在原报告，后续用 Android Studio 现有的 `Pixel_3a_API_34` 只读启动实例复测。初次运行遇到全屏提示、System UI 无响应和辅助 APK 增量安装超时，清除系统提示并完成安装后才重新执行。

v3 首轮冒烟被系统无响应弹窗遮挡，第二轮在 Launcher → 游戏旋转期间取得竖屏截图而失败。截图断言改成在同一截止时间内等待正确尺寸，后续使用相同的像素误差门限，第三轮通过。故障注入由 ADB 操作 AVD 内的独立 QA 存档；Appium 通过正常的继续入口验证画面。恢复采用上一份已提交备份，最后一次主档新增的进度可能回退。[三轮 Robot XML 与备份恢复证据](../evidence/android/backup)

## 复跑

用例快照在 [Android Robot 用例目录](../harness/android)。环境需有可运行 APK 的 Android 模拟器、ADB、Appium UiAutomator2、Robot Framework 和 Pillow。为游戏和 UiAutomator2 安装包启用独立 Android 用户 `CatGame-Appium-QA`，切换该用户后运行对应套件。将实际 ADB 路径、端口、设备序列和 QA 用户号传给 `${ADB}`、`${ADB_PORT}`、`${SERIAL}`、`${QA_USER}`。Appium 默认地址为 `http://127.0.0.1:4723`。16:9 用例以 1600×900 为基准，冒烟用例使用新档，布局和锁屏用例沿用冒烟产生的两猫存档。屏幕锁需要支持系统电源键的测试设备。

```powershell
python -m robot --outputdir local-runs/android-lock --variable "ADB:$adb" --variable "ADB_PORT:$adbPort" --variable "SERIAL:$deviceSerial" --variable "QA_USER:$qaUser" harness/android/android_lifecycle.robot
```

模拟器证据覆盖触摸、存档、升级和系统中断。真机遮挡、设备性能、耗电和温度待真机验证。CatGame 核心玩法离线运行，弱网故障注入当前没有对应业务路径。
