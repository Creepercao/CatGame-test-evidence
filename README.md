# CatGame · 游戏测试工程与证据

面向游戏自动化测试岗位的项目作品：展示需求与风险如何转化为用例、失败如何定位、版本如何放行，以及性能结论如何由数据支撑。游戏源码与发行版见 [Creepercao/CatGame](https://github.com/Creepercao/CatGame)。

**v1.2.0 双端发布验收**：Windows 正式包通过 Robot 补测，覆盖前台鼠标进入、通过共享客户端实时改值、自动存档，以及冷启动后恢复 3 只猫和伙伴客厅。Android 验证了覆盖安装后保留旧档，以及三种屏幕比例下的 Appium 触摸操作。[Windows 补测记录](evidence/s1/windows-live-08/runtime/result.json) · [双端交付报告](reports/s1-delivery.md) · [发布说明](https://github.com/Creepercao/CatGame/releases/tag/v1.2.0)

本仓库由本人利用AI辅助整理设计、实现与执行材料。项目数据来自真实运行记录，不将AI协作成果表述为全部独立手写；个人掌握程度需通过解释和复现说明。此仓库没有商业项目或线上生产经验声明。

2026-11-18追加：[S1房间成长与双端交付](reports/s1-delivery.md)，[逐轮测试与失败记录](reports/s1-test-history.md)。本轮Edit89/89、Play99/99，随后新增修改器命令集成并定向8/8；Windows QA实际鼠标/百猫/新进程恢复、普通APK自然旧档code6→7升级及Appium三比例触摸。普通Windows仅启动/主菜单写保护/独立修改器自动连接通过，前台进入后的编辑全链路受焦点环境限制未复测成功。新增用例的编译和夹具错误、AVD系统ANR、构建日志错误均保留，不计产品Bug。

独立核验本轮：`python verify_s1_evidence.py`；该命令检查已记录结果与摘要，不会重新运行Unity或Appium。F1及更早历史核验入口保持不变。

S1收尾追加：v1.2.0统一发行已公开并核对服务器附件摘要。最终审计还发现未显式隔离的CLI测试在普通包恢复后再次写正常存档，已保留私有副本并恢复；新隔离执行器复跑8/8，正常4文件前后摘要不变。该测试隔离问题及原失败保留在报告中，私有存档内容不公开。

S1.R最新补验已完成：[普通Windows完整实测](evidence/s1/windows-live-08/runtime/result.json)、[逐步记录](evidence/s1/windows-live-08/runtime/steps.json)、[冷启动画面](evidence/s1/windows-live-08/runtime/restart-companions.png)。Robot1/1，发布包真实前台进入、共享客户端实时修改、累计换房/余额清空不回退、游戏自动保存、新进程恢复三猫及伙伴客厅通过；ZIP内独立修改器自动连接另行验证，没有自动操作GUI字段。上文“普通包受焦点限制”等是原发行时点状态，本次已补齐上述范围；05–07失败保留，正常4文件恢复，本轮进程退出，发行附件保持不变。


2026-11-16追加：[F1素材统一测试与跨平台交付](reports/f1-release-verification.md)。新增Edit81/81、Play92/92、真实Windows三尺寸/鼠标、普通APK升级、Appium三比例触摸与结局、真实132px刘海安全区，以及普通Windows外置修改器验收。旧轮次和失败证据保留；分层统计不相加。本轮未重新测试真机性能。

复核本轮：`python verify_f1_evidence.py`；既有全部历史：`python verify_evidence.py`。

## 招聘方快速阅读：JD → 证据

| JD关注点 | 推荐先看 | 可以核验什么 |
|---|---|---|
| 设计文档评审、方案与用例设计 | [测试设计与覆盖](reports/test-design.md) | 前置、预期、边界、状态与风险；需求到用例的映射 |
| 自动化用例编写维护 | [业务Robot用例](harness/Tests/robot/tests/business.robot)、[Python业务库](harness/Tests/robot/libraries/GameBusiness.py)、[引擎内测试](harness/Assets/Tests) | 关键字分层、进程管理、断言与测试隔离 |
| 移动端 Robot + Appium | [Android 用例与实测](reports/android-test-evidence.md)、[复跑套件](harness/android) | Appium 实际触摸、滑动和锁屏，覆盖安装前后比较存档，记录 MuMu 失败和 Android 14 AVD 复测 |
| Android 真机验收 | [真机过程和结果](reports/android-phone-test-evidence.md)、[超宽屏修复回归](reports/android-viewport-regression.md) | 原生尺寸操作、离线购买和结局；v3→v4 存档字节不变、房间露边红绿对照与三个 AVD 比例复验 |
| 版本质量与CI/CD | [最终交付审计](evidence/ci-delivery-audit-20261106-01/result.json)、[同源失败门禁](evidence/ci-audit-20261106-01/result.json) | 测试失败/构建失败阻断，以及纠正后的成功交付 |
| Bug复现、评级、追踪与闭环 | [缺陷与追溯](reports/缺陷与追溯.md)、[失败](evidence/roaming-target-red-20261106-01)、[复测](evidence/roaming-target-green-20261106-01) | 固定种子复现、保留阈值、修复与回归证据 |
| 保存压力与故障定位 | [Windows 文件替换排查](reports/windows-save-replacement-diagnosis.md) | 原失败现场、Win32 最小复现、七项确定性红绿、三轮千次压力、实际 Player/修改器及 Windows v1.0.1 交付 |
| 测试用例维护与结果可信性 | [性能用例契约维护](reports/performance-save-contract.md) | 旧用例误报的完整 Player 红绿、新包契约门禁、备份前后快照与完整 CSV 重算；区分环境失败、测试缺陷和产品缺陷 |
| 客户端性能、压力测试 | [执行前方案](reports/性能专项方案.md)、[专项报告](reports/性能专项报告-2026-11-06.md)、[两轮原始数据](evidence/performance-baseline-20261106-02/performance) | 环境、预热、帧时间分布、操作耗时和结论边界 |
| 测试平台从0到1 | [执行入口](harness/tools/qa/run_suite.py)、[失败取证](evidence/business-failure-20261106-01)、[异常采样留证](evidence/performance-failure-20261106-01) | 不只跑成功路径，也验证结果缺失、异常退出与partial数据 |

## 核心结果（分层统计）

- 最终CI构建5：PlayMode **86/86**、EditMode **51/51**，失败/跳过均0。Robot编排分别冒烟2/2、配置1/1、完整5/5、业务1/1；不相加冒充独立用例数或覆盖率。
- 实际Player业务链：新游戏→200次摸猫→购买→保存→退出→新进程启动→继续读档；**12步、两个进程正常退出**。操作经Actor/按钮回调，不能证明原生鼠标全链路。
- 同源构建2/3分别验证测试与构建失败不交付，构建4成功；构建5是后续版本。CI仅验证本地Jenkins手动触发，不声称提交自动触发或自动上线。
- 两轮性能记录 **27607帧、8482条操作**。100可见猫P95为16.731/16.763ms，在指定设备与负载下达标；保存压力存在长尾，不声称所有场景不卡顿或性能优化百分比。
- 缺陷材料含4个游戏/工具案例、2个测试平台案例，部分为历史补整理。故意失败与环境/脚本故障不计为产品Bug。
- Android 另行验收。MuMu 上验证购买后切后台与重启、20:9 和 4:3 布局；Android 14 AVD 上验证锁屏后原进程恢复，以及 v1 → v2 升级保留道具、成就和数字格式。

2026-11-15追加：[购买即时保存缺陷闭环](reports/android-purchase-investigation.md)，包含受理与未受理区分、两个确定性红绿、完整89/69、默认动画时序失败、受控Appium对照、普通APK新档及真实存档升级。原失败完整保留，历史v4真机性能不充作本版新测。

## 五分钟独立核验

安装Python 3后，在仓库根目录运行：

```sh
python verify_evidence.py
```

脚本用 Python 校验公开文件哈希、解析 NUnit 和 Robot XML、核对 CI 阻断与放行、检查进程重启和安卓升级存档，并从 CSV 重新计算 100 猫 P95。此操作核验已有证据的一致性。重新执行用例需要对应的 Unity、Robot、Appium 或 Jenkins 环境。

## 仓库结构与复现范围

- `evidence/`：筛选后的XML、JSON、JSONL和CSV，包含成功与失败。
- `harness/`：测试源码快照。Android 目录含视觉断言所需的小幅截图裁片。
- `reports/`：方案、追溯、分析与复现说明。
- `provenance.json`：每份来源文件的位置、原始SHA256与公开副本SHA256。

历史运行版本`e387f3c`、`cf8f230`、`42de242`来自本地完整工程；游戏公开仓库`baf37d1`是移除测试后的独立快照，不能把历史哈希当作公开仓库中可checkout的提交。**本仓库不是开箱即用的Unity工程**，见[复现说明](reports/reproduction.md)。

## 公开副本与边界

公开材料替换本机用户名、工程绝对路径、主机名、局域网地址和 Appium 会话 ID。保留筛选后的结构化报告、隔离测试存档和视觉断言裁片。原报告中的完整日志、截图和归档规模只用于定位原始证据。

`provenance.json`分别记录转换前后摘要；原始哈希不能用于校验已脱敏副本。保留历史时间、数值、失败结果和版本，不将脱敏处理伪装为原始字节。

**当前进度**　Android v4 已完成布局、升级、六轮性能和短时挂机验收。Windows 1175 有限恢复已在 v1.0.1 交付，具体系统占用者尚未知；其后完整性能 QA 的旧存档断言已修正并实际红绿复验。Android 原偶发购买恢复仍跟踪。其他机型、GPU 调用栈、长期泄漏、耗电和数小时通关尚未实测。[真机报告](reports/android-phone-test-evidence.md)、[v4 性能复验](reports/android-v4-performance.md)、[本轮测试维护](reports/performance-save-contract.md)
