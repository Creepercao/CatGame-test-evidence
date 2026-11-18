# S1场景切换测试记录

2026-11-18新增，原始产物位于Build/S1。所有轮次保留，以下按发生顺序记录。

| 轮次 | 源码/范围 | 实际结果 | 后续 |
| --- | --- | --- | --- |
| import-01 | 6adf50b初版代码，由CLI1.0.1执行RoomThemeSetup.Apply | 退出0，未见C#错误；导入32PPU/Point与两主题配置；保留原有spritesheet弃用和Editor退出分配警告 | 新meta/config随后652da5b入Git |
| art-check-01 | JSON/PNG、透明边与片组脚点、建筑/地板一致、整数预览与调色板 | 4/4通过，Pillow getdata弃用警告保留 | 后续改为getcolors，不改像素 |
| edit-theme-01 | 652da5b，节点临界值/无效配置/资源真实导入 | 7/7，退出0 | 待完整引擎回归 |
| play-theme-01 | 652da5b，跨节点保留Actor/State/Items/HUD、余额消费不回退、模态互斥、切换销毁恢复、猫位校正 | 4/4，退出0 | 继续旧档/离线、百猫和失败恢复 |
| play-theme-02 | 扩展为6项 | 5/6：百猫用例在Rebuild延迟Destroy完成前GetComponents得到440候选，尚未进入漫游验证 | 取样时机错误，不计产品Bug；补yield一帧后严格检查100，不降低断言 |

同源新增Edit用例覆盖已有存档序列化/加载与离线收益跨节点，尚待执行。当前所有通过结果都限于上述范围；未宣称真实Windows输入、普通APK Appium或发行包通过。旧QA中已出现过同类延迟销毁取样问题，本轮用例未沿用该时机约束，记录为应复用的测试维护经验。

play-theme-03：维持百猫断言为100，只修正延迟Destroy后的取样时机，6/6通过，退出0；包括原先未到达的百猫移动与同一Actor身份验证、缺资源失败保留原场景及恢复ModalGate。edit-full-01完整89/89、退出0，新增旧格式存档读回与离线跨节点也通过。当前play-full-01完整PlayMode执行中；新增Windows QA将以真实Player拍摄两主题三分辨率、原生鼠标、20秒百猫脚点与排序，并使用第二个进程验证读档，不将这些未执行步骤提前写成通过。

windows-growth-01：QA构建qa-windows-01来源3067c8b，退出0且未见C#错误。Robot1/1完成实际1920/1600/2560三尺寸两主题、活跃收益跨节点、原生鼠标clicks+1、20.005秒百猫2881次观察（footErrors=depthErrors=0），并以新PID读回440猫及clicks1。首轮曾短暂等待本轮PID焦点，随后已获前台并成功发送原生输入；needs-focus.txt保留为过程痕迹，不表示当前仍待焦点。

截图复核发现S1-QA-002：重启脚本查找OfflineEarningsOverlay/Card，实际节点为Summary，未关闭离线框；首轮报告未断言可游玩状态仍绿灯。保留首轮XML/result/遮挡截图，范围限定为已加载正确主题与存档，不能当作重启后无遮挡可交互通过。修正Summary路径并新增ModalGate未阻塞、TimeScale>0及重启100可见猫断言。不是产品离线收益弹窗故障。将独立构建qa-windows-02后复验。

windows-growth-02：QA脚本修正包构建退出0且无C#错误；Robot因90秒未取得本轮窗口前台焦点失败，没有发送原生鼠标输入，旧窗口已经退出。不能写成产品点击失败，也不能抹去该轮红灯。

windows-restart-01：改用首轮QA存档的原字节副本作为明确标记的夹具，单独启动新进程，Robot1/1。正确关闭真实Summary离线框后ModalGate空闲、TimeScale>0，100可见/440拥有猫、clicks1及伙伴客厅读回正确；截图复核无遮挡。此轮不冒充自然新档或重复鼠标测试，鼠标与20秒百猫证据仍引用首轮有效部分。

android-env-01：AVD启动失败，原因是模拟器误用了不含API34系统映像的Unity SDK根目录。android-env-02改用Android Studio SDK根目录，并保持隔离ADB15037后启动成功。android-user-01创建只读AVD中专用用户10，完成初始化与900×1600配置；不操作手机/MuMu。上述环境步骤不计游戏功能通过。

版本准备：原工程764d5fc、纯游戏候选d25e3c3，版本1.2.0/code7；新增真实成就CG先于房间切换的PlayMode用例。完整play-full-02、普通Windows构建和1.1.0 Android自然购买升级基线正在执行，结果尚未确认。

play-full-02完整99/99、退出0，新增真实first_click CG排队/播放后才允许主题切换通过。release-windows-01虽退出0，但日志包含RoomThemeController首次增量编译CS0246，拒绝采用；保留原日志后release-windows-02重建退出0，无C#错误，作为普通包候选。

android-baseline-smoke-01：系统沉浸提示关闭后出现AVD的System UI ANR，未到游戏主菜单，Robot0/1。人工复核本轮截图后，android-system-recover-01以已识别图案和节点约束仅选一次Wait，Robot1/1，不能计为游戏功能通过。android-baseline-smoke-02仅将启动就绪观察改为45秒，Robot1/1：自然收益攒够后买第二猫，实际购买确认后Home耗时0.969秒，热恢复同PID，冷启动新PID，离线框领取后两猫恢复。尚未往该自然档注入夹具。

windows-release-probe-01：检测到当前私有文件与F1历史备份不同，在修改前中止，result失败且restored=true。不是游戏缺陷。先只读备份当前4文件至私有目录，再以新备份运行probe-02；备份内容不公开，最终必须复核恢复摘要。

release-android-01同样退出0但含首次CS0246，拒绝采用；release-android-02退出0、无C#错误，签名校验成功。两端采用同一纯游戏d25e3c3，候选Windows ZIP153文件全部解压读回，内含既有独立修改器；APK SHA256为9c68b1df6b8db6d56fafee0524947d19d8ac0b45b1e69d3e34b406588d9e9a80，暂未发布。

windows-release-probe-02/03：前台窗口激活超时，03短暂AttachThreadInput也未获焦点，没有发送物理鼠标；04明确标记的窗口定向OS消息也未使菜单进入游戏，失败保留，不能当作原生鼠标。三轮finally均恢复本轮4文件。停止重复该环境路径，另用windows-menu-debugger-01独立验证普通包启动、主菜单blocked和打包修改器自动连接，passed/restored均true；frozen=true指独立EXE打包。普通包运行中实时改值未在本轮重新验收；不能借F1或QA结果冒充它已通过。

android-upgrade-01 Robot1/1：未修改自然两猫旧档即覆盖code6→7，安装前与首次启动前存档SHA相同；继续进入、离线领取后cats/items/clicks/intro保留，Starter实际像素样本与原始资源一致。

android-growth-01第一用例已通过：随后才明确注入累计99990、余额200、20个CG已看过的隔离夹具，用实际每秒0.8收益跨100000；截图分别匹配Starter和Companions原生背景色，Home保存后冷启动仍为伙伴客厅且两猫。该夹具用于隔离换房，不冒充自然全通关或Android CG顺序证明；真实CG顺序由新增PlayMode用例覆盖。后三比例猫身触摸仍在执行，尚未写通过。

android-growth-01最终4/4：1600/2000/1200×900三比例分别定位实际自然Sleep猫身，Appium单次触摸后Home保存clicks0→1、1→2、2→3；背景原图像素匹配、右缘无底色条均通过。未操作手机或MuMu，未做本版真机性能或小时级稳定性。

为补充修改器跨节点集成，play-theme-04新增用例发生CatGame同名类/命名空间歧义CS0117，CLI退出6且无XML，已保留Editor-prev.log和CLI日志；命名空间改用global限定。play-theme-05为7/8，新增用例错误假定首帧totalEarned为0，实际已自然产出0.0097124；原有7项仍过。只修夹具显式将累计初始化0，不改生产逻辑，play-theme-06复跑中。不得把编译失败或夹具失败计入产品Bug。

play-theme-06最终8/8、退出0：实际EditorDebugCommands只改余额100万不增加累计/不换房，AddHair计入累计100000后换房，余额归零仍在伙伴客厅，三次操作均生成恢复记录。此为引擎真实命令集成，不代替本轮普通Windows包前台菜单/运行中外置修改器未成功复测的边界。完整99项结果来自play-full-02，之后仅新增这一项并定向跑8项，不写成全量100/100。

发布核验：v1.2.0已公开，两端四附件服务器摘要与本机相同，实际标签5a0f437，latest为本版，五个旧发行仍公开。首次统一上传耗时约数分钟，未因此提前宣称完成。

最终文件审计首次失败：普通包probe的finally在各轮当时恢复成功，但后续CLI引擎测试结束后，正常目录save.json和save.json.bak再次变化，两个Player日志仍相同。检查无活跃游戏/Editor。源代码显示Editor未设置CATGAME_EDITOR_QA_ROOT时，SaveSystem.Bootstrap会读取正常目录并将已加载档标脏；本次直接CLI调用未显式设置该环境，因此先前“各probe恢复”不足以证明全轮结束无副作用。未将此问题误计为游戏存档算法缺陷。

已把变化后的两文件另存私有目录，再恢复本轮备份4文件；私有内容均不发布。新增run_isolated_unity_tests.py强制为本轮输出设置CATGAME_EDITOR_QA_ROOT。play-theme-07用同一8项执行8/8、退出0，复跑前后正常4文件摘要完全一致。private-restoration-summary.json保留首审失败与最后恢复结论，不删除碰壁。最终无游戏/Editor/AVD/Appium残留；原始私有文件、调试凭据目录不进入公开证据。
