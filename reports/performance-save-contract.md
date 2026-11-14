# Windows 性能存档恢复断言维护

2026-11-14，TODO M17。旧完整性能 QA 在主档损坏后期待新档 clicks=0/count=1，与 M11 的备份恢复契约冲突。先在原 QA-02 Player 保留实际负例，再修改断言和重新构建测试包；不改游戏规则或已发布的 Windows/Android 包。

## 运行前置

- red-01：旧运行器发现旧 CI 凭据文件便访问已关闭 Jenkins，连接拒绝，退出1，尚未启动 Player，不能计为存档断言失败。
- 新增显式 --standalone：仍要求无其他 Unity/Player，且本机18880端口明确拒绝连接；默认 CI 查询保持不变，结束后再次核对端口。
- red-02：2秒探测返回10035（未取得确定连接结果），守卫按设计拒绝运行，退出1。随后只读5秒和10秒探测均在约2秒返回10061。预算调整为5秒，不将未知状态算作未启动CI。
- red-03：使用相同旧QA-02，完整性能模式、单轮、新隔离结果目录，正在执行。输出 Tests/robot/results/save-contract-red-20261114-03；同名旧目录不覆盖。

手机测试已结束。本轮仅使用 Windows 独立 QA Player 和隔离数据。

独立前置守卫另做实测：短暂在127.0.0.1:18880创建自有监听器时，require_stopped_local_ci 明确拒绝；关闭后返回connectionRefused=true。未启动Jenkins或提交任何CI任务。当前旧完整用例已完成single与hundred，继续input/CG/save。

## 实际红灯与修订

red-03 完整旧用例到 save 阶段后失败，Player/运行器均退出1，result.complete=false、roundTrips=500、corruptRecovery=0。现场主档/备份均为 clicks777、Milk40、catHair12345.5，说明游戏按备份恢复，旧期待新档的断言错误。single/hundred/input/CG 已实际执行，不能把当轮记为完整通过。

现仅修改 Development QA：注入损坏前读取上一份备份作预期，恢复后逐字段比较整个GameState，检查隔离损坏文件与备份未变；证据快照在测量结束后才落盘，额外读取/比较发生于含故障注入的save阶段，不进入write/read方法计时。新结果写 saveRecoveryContract=previous-committed-backup-v1。分析器对新契约要求前后快照、最终主/备份、损坏隔离证据一致；旧历史数据无标记时仍按原报告规则读取，不伪造旧快照。

Python六项统计/证据单测通过（含原两项和新增四项），覆盖拒绝新档代替备份、其他字段丢失、快照缺失、伪造有效JSON损坏档。下一步重建QA包跑同一完整性能流程。

修订提交 df44498，仅QA/运行器/分析器/单测四文件；独立QA构建退出0且无脚本编译错误，自带来源provenance记录提交和dirty=false。完整绿灯用例输出 save-contract-green-20261114-01，现已启动，规模及帧预算保持原值，不降低断言。生产游戏和两个发行版保持不变。

复测当前已完成single和hundred，进入input；尚不预报性能或最终通过。此次修改只修QA对恢复规则的理解，不能称为游戏存档修复或性能提升。完整Player负例与复测均为Python性能执行器直接启动，不计作额外Robot用例数；实际引擎与独立Player层次分别报告。

## 复测终态（已执行）

完整绿色运行与Player均退出0，passed=true、criteriaFailures=[]；完成1200次点击、600次购买尝试/100次成功，20次冷CG/200次热CG，1000次存读与1次备份恢复。所有前后快照/最终主备份逐字段一致，损坏文件实际隔离。

共13838帧；single/hundred/input的P95分别为16.717101/16.698901/16.713900ms，>33.3ms占比均0，保持原20ms/1%预算。save/write共1000次，P95 9.5976ms、最大15.8867ms，仅报告本轮方法墙钟，未宣称优化收益或长期稳定性。

后续增加新完整运行强制契约门禁（旧历史分析默认仍兼容）；缺标记的旧QA包会被拒绝。Python共7项单测通过，并使用本轮已采集完整样本执行require_save_contract=True复核通过。这是静态证据复核，不虚称又运行一次Player。QA构建/原完整运行来源df44498，后续门禁工具提交95cdc53；游戏生产代码、已发布Windows1.0.1和Android预览包均未改。

本轮运行器墙钟 299.089 秒；采样阶段合计 230.864 秒，预热及启动另计。完整复验只执行一轮，不混用历史D4两轮的数据。

## 证据归档与复跑

公开副本初次筛选未包含.bak后缀，独立重算据此拒绝通过；补齐已存在的原始备份文件后，662份材料的哈希/结构及完整数据重算通过。该问题属于副本归档，原始测试数据未缺失，未重跑或改变游戏结果。

复跑需匹配QA源码/构建及环境，显式指定CATGAME_PERFORMANCE_EXE后执行 python tools/qa/run_performance.py Tests/robot/results/my-new-run --rounds 1 --standalone。standalone要求本机CI端口关闭；使用运行中的本地CI应省略该参数并按默认检查排队/构建状态。结果目录必须新建且位于Tests/robot/results。独立证据仓库不是完整Unity工程，重建步骤参见该仓库复现说明。只核验现有材料可运行 python verify_evidence.py，不会启动新Player。
