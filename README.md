# CatGame · 游戏测试工程与证据

面向游戏自动化测试岗位的项目作品：展示需求与风险如何转化为用例、失败如何定位、版本如何放行，以及性能结论如何由数据支撑。游戏源码与发行版见 [Creepercao/CatGame](https://github.com/Creepercao/CatGame)。

本仓库由本人利用AI辅助整理设计、实现与执行材料。项目数据来自真实运行记录，不将AI协作成果表述为全部独立手写；个人掌握程度需通过解释和复现说明。此仓库没有商业项目或线上生产经验声明。

## 招聘方快速阅读：JD → 证据

| JD关注点 | 推荐先看 | 可以核验什么 |
|---|---|---|
| 设计文档评审、方案与用例设计 | [测试设计与覆盖](reports/test-design.md) | 前置、预期、边界、状态与风险；需求到用例的映射 |
| 自动化用例编写维护 | [业务Robot用例](harness/Tests/robot/tests/business.robot)、[Python业务库](harness/Tests/robot/libraries/GameBusiness.py)、[引擎内测试](harness/Assets/Tests) | 关键字分层、进程管理、断言与测试隔离 |
| 移动端 Robot + Appium | [Android 用例与实测](reports/android-test-evidence.md)、[复跑套件](harness/android) | Appium 实际触摸、滑动和锁屏，覆盖安装前后比较存档，记录 MuMu 失败和 Android 14 AVD 复测 |
| Android 真机验收 | [真机过程和结果](reports/android-phone-test-evidence.md)、[超宽屏修复回归](reports/android-viewport-regression.md) | 原生尺寸操作、离线购买和结局；v3→v4 存档字节不变、房间露边红绿对照与三个 AVD 比例复验 |
| 版本质量与CI/CD | [最终交付审计](evidence/ci-delivery-audit-20261106-01/result.json)、[同源失败门禁](evidence/ci-audit-20261106-01/result.json) | 测试失败/构建失败阻断，以及纠正后的成功交付 |
| Bug复现、评级、追踪与闭环 | [缺陷与追溯](reports/缺陷与追溯.md)、[失败](evidence/roaming-target-red-20261106-01)、[复测](evidence/roaming-target-green-20261106-01) | 固定种子复现、保留阈值、修复与回归证据 |
| 保存压力与故障定位 | [Windows 文件替换排查](reports/windows-save-replacement-diagnosis.md) | 原失败现场、Win32 最小复现、七项确定性红绿、三轮千次压力及真实进程重启；Windows 修复包待交付 |
| 客户端性能、压力测试 | [执行前方案](reports/性能专项方案.md)、[专项报告](reports/性能专项报告-2026-11-06.md)、[两轮原始数据](evidence/performance-baseline-20261106-02/performance) | 环境、预热、帧时间分布、操作耗时和结论边界 |
| 测试平台从0到1 | [执行入口](harness/tools/qa/run_suite.py)、[失败取证](evidence/business-failure-20261106-01)、[异常采样留证](evidence/performance-failure-20261106-01) | 不只跑成功路径，也验证结果缺失、异常退出与partial数据 |

## 核心结果（分层统计）

- 最终CI构建5：PlayMode **86/86**、EditMode **51/51**，失败/跳过均0。Robot编排分别冒烟2/2、配置1/1、完整5/5、业务1/1；不相加冒充独立用例数或覆盖率。
- 实际Player业务链：新游戏→200次摸猫→购买→保存→退出→新进程启动→继续读档；**12步、两个进程正常退出**。操作经Actor/按钮回调，不能证明原生鼠标全链路。
- 同源构建2/3分别验证测试与构建失败不交付，构建4成功；构建5是后续版本。CI仅验证本地Jenkins手动触发，不声称提交自动触发或自动上线。
- 两轮性能记录 **27607帧、8482条操作**。100可见猫P95为16.731/16.763ms，在指定设备与负载下达标；保存压力存在长尾，不声称所有场景不卡顿或性能优化百分比。
- 缺陷材料含4个游戏/工具案例、2个测试平台案例，部分为历史补整理。故意失败与环境/脚本故障不计为产品Bug。
- Android 另行验收。MuMu 上验证购买后切后台与重启、20:9 和 4:3 布局；Android 14 AVD 上验证锁屏后原进程恢复，以及 v1 → v2 升级保留道具、成就和数字格式。

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

**真机与显示修复进度**　普通 v4 已通过房间露边的真机红绿、覆盖升级及三种 AVD 比例复验；QA v4 六轮合计 21810 帧，均通过参考预算，百猫 301.97 秒挂机观察通过。Windows 存档压力间歇 I/O 失败仍保留并跟踪。其他机型、GPU 调用栈、长期泄漏、耗电和数小时通关尚未实测。[真机报告](reports/android-phone-test-evidence.md)、[本轮回归](reports/android-viewport-regression.md)、[v4 性能复验](reports/android-v4-performance.md)
