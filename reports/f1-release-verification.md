# F1 世界素材统一：测试设计、结果与交付

本轮目标是统一在用素材尺寸、脚点、地面锚点与像素显示，保持玩法、经济数值和旧存档兼容。本人利用AI辅助设计、实现与测试。此报告区分引擎测试、QA Player和普通发行包，不相加冒充覆盖率。

## 岗位关注点与可核验证据

| 关注点 | 本轮实际工作 | 原始入口 |
| --- | --- | --- |
| 需求评审与风险 | 528次用途的GUID/fileID/帧顺序、动作脚点、点击、边界、UI、相机与安全区形成验收条件 | [执行前方案](f1-plan.md)、[配置](f1-art-configuration.md) |
| 用例设计与维护 | 引擎真实对象契约先红后绿，保留误断言与产品越界失败 | [契约红灯](../evidence/f1/contracts-red-02/results.xml)、[契约绿灯](../evidence/f1/contracts-green-02/results.xml) |
| Unity/NUnit | 最终EditMode81/81、PlayMode92/92；前轮91/92真实发现身体越界，修复不对称可走区 | [Edit](../evidence/f1/edit-full-03/results.xml)、[Play](../evidence/f1/play-full-02/results.xml)、[旧失败](../evidence/f1/play-full-01/results.xml) |
| Robot与Windows输入 | 三实际窗口1920/1600/2560；普通尺寸UI33图，原生鼠标3次命中；冻结基线与实时玩法分开 | [原生尺寸](../evidence/f1/native-grid-02/runtime/art-metrics.json)、[完整Player](../evidence/f1/visual-grid-02/output.xml) |
| Robot+Appium | 普通APK三比例睡眠猫身体单次触摸，保存clicks严格+1；CG点击无穿透 | [三比例报告](../evidence/f1/android-touch-09/output.xml)、[操作逐步日志](../evidence/f1/android-touch-09/steps.jsonl) |
| 升级与数据可靠性 | 普通code5自然游玩档→code6，安装后首启前存档字节相同，再从菜单加载实际两猫 | [升级结果](../evidence/f1/android-upgrade-01/upgrade.json) |
| 兼容适配 | 原生2220×1080真实132px切口，按钮实测与公式差0.1px，原生点击、Home功能栏、保存通过 | [安全区](../evidence/f1/android-safearea-07/runtime/result.json) |
| 普通交付包 | Windows普通包＋已发布的独立EXE修改器自动连接，数值修改后游戏自动保存；Android取消购买/结局CG/统计/继续及菜单 | [Windows](../evidence/f1/windows-release-probe-02/result.json)、[结局](../evidence/f1/android-ending-03/runtime/ending-result.json) |
| 失败排查与证据可信度 | 编译退出0但有CS错误拒收；窗口尺寸钳制、前台焦点、ADB/UiA端口、Android临时竖屏与兼容提示、识别误断言均留失败轮次 | [完整过程](f1-test-history.md)、[构建记录](../evidence/f1/builds) |
| 版本与交付门禁 | 纯游戏与证据分仓、用户身份Git提交、152个ZIP成员逐个回读摘要、APK与受测包相同、两平台同Release | [包清单](../evidence/f1/distribution/package-manifest.json)、[校验表](../evidence/f1/distribution/SHA256SUMS.txt) |

## 结果边界与来源

- 220张源PNG字节未变；528次引用身份前后无差异，迁移112张导入设置。[迁移对照](../evidence/f1/migration-comparison.json)。
- 引擎与QA结果对应各记录中的私有工程源码；普通包运行时共同对应公开游戏提交f536871，发行标签1b82df9仅增加README。不能将两个仓库的提交号混用。
- 三比例摸猫复测在暂停恢复后使用已标记QA检查点；只有升级测试使用未注入的自然游玩档。结局注入火箭价格，不代表自然通关耗时。
- 30秒100可见猫验证边界、落脚和遮挡，没有本轮真机性能、小时级稳定性、耗电或跨设备普遍结论。本轮没有操作手机和MuMu。
- WindowManager真实切口是必要前置。safearea-01/02逻辑切口为0不能计通过；03–06识别和离线弹窗前置错误属于测试问题，原样保留。
- Windows原用户存档备份和修改器连接令牌不公开。公开副本只保留结论及QA数据，脱敏前后摘要分别在provenance.json。

## 独立复核与复跑

运行 `python verify_evidence.py` 验证既有全仓历史证据；运行 `python verify_f1_evidence.py` 验证本轮XML、JSON、旧档、引用、交互计数、打包和所有公开文件摘要。它们只复核记录，不能代替重新运行游戏。实际复跑见harness/f1，需对应Unity2022.3.21f1、Robot、Python、Appium及自有隔离AVD，修改本机路径、端口和测试用户；不应在他人的日常存档或模拟器上照抄隔离路径。

历史916份清单保留，本轮追加到独立目录，不覆盖旧红灯和旧发行。所有后续发行回执应以distribution中的服务器摘要核对。
