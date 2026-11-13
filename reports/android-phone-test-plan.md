# CatGame Android 真机验收方案

目标是用手机实测横屏安全区、触控与系统中断，并建立客户端性能基线。制定本方案时 Android 14 AVD 和 MuMu 已提供功能回归证据，真机项目尚待执行。2026-11-13 已完成 Xiaomi / Android 15 首轮验收，结果与待修复缺陷见 [真机测试记录](android-phone-test-evidence.md)，下文保留验收步骤供复跑。

## 测试准备

1. 手机通过 USB 连接，启用 USB 调试并授权这台电脑。用 `adb devices -l` 区分手机与模拟器，后续所有命令指定目标设备。记录机型、Android 版本、分辨率、系统刷新率、导航模式和是否有屏幕挖孔。
2. 核对 APK 的版本、包名、ABI 和 SHA-256。查看手机上的安装状态。已有玩家档的设备优先使用独立测试配置或测试构建，覆盖安装案例使用单独建立的测试档。
3. 功能测试使用正式 APK。性能采样先以同一测试构建生成一猫与 100 只可见猫的受控场景，记录场景实际数量；用正式 APK 抽查冷启动与常规房间，单独记录构建差异。游戏的目标帧率为 60。

## 功能与适配

| 编号 | 操作 | 通过标准与证据 |
|---|---|---|
| PHONE-01 | 从系统桌面启动，新档跳过引导，摸猫并浏览商店 | 游戏保持横屏，桌面图标正确，猫毛变化，商店拖动有效。记录桌面、主界面及操作前后截图。 |
| PHONE-02 | 检查设置、商店、成就、普通 CG、结局 CG 和统计页 | 两种握持方向下检查屏幕遮挡区域。面板正文可见，主按钮可触达，手势区域无冲突。对端点分别留图。结局用隔离测试场景触发。 |
| PHONE-03 | 购买后 Home、锁屏解锁、系统返回、强停重启 | 解锁后正常交互，购买保留，冷启动后能继续。记录应用前台与 PID、存档前后关键状态。 |
| PHONE-04 | 飞行模式下冷启动、摸猫、购买、保存和再启动 | 系统确认当前无默认网络，核心离线流程可完成。记录屏幕和进程状态。 |

Appium 负责触摸和按键。先用原生层级确认系统界面元素，再用真机截图定出游戏控件点击范围。现有 MuMu 模板绑定 1600×900，真机保持原分辨率以验证实际安全区。

## 帧时间和内存

| 编号 | 负载 | 采样 |
|---|---|---|
| PHONE-PERF-01 | 单猫自然漫游 | 加载完成后预热 10 秒，采集 60 秒。 |
| PHONE-PERF-02 | 可见猫满 100，自然漫游 | 同样的设备、帧率、分辨率和采样时间，核对实际可见数量。 |
| PHONE-PERF-03 | 上述两场景在同一台手机各重复三轮，之后持续挂机 | 每轮前后采集 PID 的 `dumpsys meminfo`。独立记录充电、温度与采样间隔，稳定性只按实际挂机时长报告。 |

使用 Unity 帧间隔数据算 P50、P95、P99 和超过 33.3 ms 的比例。缺少有效帧样本或实际猫数与目标不符时，该轮判无效。暂用 P95 ≤20 ms、超过 33.3 ms 的帧占比 ≤1% 作为 60 FPS 目标下的参考预算。保留原始数据，按设备和构建分开报告。Unity 游戏表面是 SurfaceView，Perfetto 的 FrameTimeline 不能直接作为该游戏帧数的默认来源。Android PSS 与 Unity 管理内存分开列出。[Perfetto FrameTimeline](https://perfetto.dev/docs/data-sources/frametimeline) · [Android 内存工具](https://developer.android.com/topic/performance/memory/guide/tools-overview)

性能 APK 使用独立包名 `com.catgamestudio.catgame.perf`。Unity CLI 的 Android 构建入口为 `CatGame.EditorTools.AndroidPerformanceBuild.Build`，产物为 `Build/Android/performance/CatGame-PerfQA.apk`。安装后执行以下命令，`$adb` 和 `$deviceSerial` 分别为本机 ADB 路径和目标手机序列。追加 `--probe` 可先做两组各 3 秒的采样链路检查。

```powershell
python Tests/robot/android/collect_performance.py --adb $adb --serial $deviceSerial --apk Build/Android/performance/CatGame-PerfQA.apk --repeats 3 --output Build/Android/performance/phone-run-01
```

每轮重启独立性能包，先核对设备上 APK 的 SHA-256。游戏生成固定存档并载入实际房间，检查可见猫数量、屏幕方向、前台焦点和弹窗状态。采集脚本在测量前取一次系统 PSS，再通过应用内文件通知采样器。测量期间暂停 ADB 查询，结束后验证同一进程的 PSS、Unity 帧号连续性和有效样本。若切换前台、旋转或改变场景负载，保留当轮记录并标记无效。

每轮留下设备信息、APK 摘要、命令、关键步骤、截图、帧数据、内存快照和结果。先让游戏进入稳定负载，再停止自动化截图与触摸进行采样，功能证据和性能样本分开保留。
