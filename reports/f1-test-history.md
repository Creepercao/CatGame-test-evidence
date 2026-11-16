# F1 素材坐标与像素显示测试记录

日期：2026-11-15。范围：等距房间结构、猫咪/用品/房间/UI的导入及屏幕显示。测试工具：Unity Test Framework/NUnit、Robot（Windows Player）、Appium（后续独立Android AVD）。不操作用户手机或MuMu，不安排小时级运行。与购买存档修复M14、超宽背景修复M15、Windows存档M16保持回归关系。

## 执行原则与用例

- ART-01：导出实际资源引用及GUID/fileID、rect/PPU/pivot/filter/platform，迁移后逐项比对身份与PNG摘要，禁止删除meta重建。
- ART-02：11猫×Idle/Walk/Jump/Sleep完整取景，验证不是空图，保留44独立PNG和人工联系表；画布与脚线定义不随某帧透明范围变化。
- ART-03：真实CatActor组合验证脚点原点、动作画布高度、翻转及二维Collider；32/64帧与不同导入密度保持同一世界画布。
- ART-04：房间墙线/家具占地及吸附后脚点约束；百猫短时漫游允许实际移动且不能穿家具，不冒充小时级性能/稳定性通过。
- ART-05：1920×1080、1600×900、2560×1440实际Windows窗口；Android AVD1600×900、2000×900、1200×900，另有2712×1220投影。验证真实屏幕尺寸、相机FOV、背景完整、身体显示、UI与实际点击；整数/非整数采样明确标注。
- ART-06：用品不透明外形尺寸、落脚中心、遮挡，UI九宫格/缩略图/ADV立绘/成就CG不偏位或裁切。
- ART-07：完整Edit/Play回归、普通发行包存档兼容/购买/CG/结局，Windows修改器连接；最终Windows+Android同一Release、独立证据仓库。

## 逐步执行流水

所有路径以工程根目录为基准，原始失败不覆盖。每条只有明确验收范围，不能从审计或冻结图推出功能/性能结论。

| 步骤 | 操作与源码 | 实际结果/材料 | 判定与后续 |
| --- | --- | --- | --- |
| 01 | 74d1ea4扫描源图 | baseline-01/assets.json | 空meta值正则可跨行，保留初版 |
| 02 | 修正meta行解析 | assets-02.json、source-summary.json，220PNG与meta摘要 | 目录候选不是实际引用 |
| 03 | 217ed7e Unity审计 | unity-refs，505用途/84PNG/7场景Renderer，退出0 | 保留Editor分配器警告，非游戏泄漏结论 |
| 04 | 00fd26b首QA构建 | qa/build-01.log，退出0但CS0103 | 拒绝作为有效包，保留错误 |
| 05 | 同源码复构 | qa-02，无C#错误、许可证警告；dirty=true | QA脚本未提交导致dirty，明确记录 |
| 06 | Robot原生三尺寸首采 | native-01，2560请求实际1920，失败 | 不把文件名当实际尺寸 |
| 07 | 9850130自有HWND客户区调整后构建 | qa-03，无C#错误，旧refreshRate弃用警告 | 未改桌面显示模式 |
| 08 | Robot原生三尺寸复采 | native-02 1/1；实际1920/1600/2560 | 猫画布200/166.667/200px，比例不连贯 |
| 09 | ed03bae独立44姿首采 | poses.xml失败，Milk三张后Sleep7色 | 最低8色无设计依据，是测试假设错误 |
| 10 | 0015cf0完整取景与非空前景复采 | poses-02.xml 1/1，44PNG/pose-report | 已人工查看全44格联系表；不修改源图 |
| 11 | 3e391ab扩充UI/故事/字体引用 | unity-refs-02退出0，528用途/107PNG/7Renderer | F1.0完成；CG23张也在清单内 |
| 12 | 27e96c6 CatActor脚点与Collider契约 | contracts-red-01 0/2 | 原点偏离.8125world；Collider误比较Z厚度 |
| 13 | e6d8be6修正二维比较 | contracts-red-02 1/2 | 唯一红灯是实际脚点契约，保留两轮XML/日志 |
| 14 | 862794a集中配置与显式迁移 | migration-01开始运行 | 结果待实际Unity退出与逐项身份核验 |

## 尚未完成与解释限制

基线原生图冻结模拟，出生0.5秒、停顿0.6秒导致11猫重叠，不能当11猫逐只可见或运动证据；44姿独立取景补充可辨认性。新相机实现尚未完成实际后端画面/输入验收，不能因投影计算通过就宣称Windows/Android均通过。真机已有历史v4性能结果不转嫁给本轮新版本。

后续每轮构建/测试继续追加源码、运行参数、真实退出、断言与产物，不把第一次失败删除或覆盖为成功。发布必须Windows和Android同一Release，测试材料继续独立仓库。

步骤14结果追加：migration-01 Unity退出0、无C#错误；112张（猫81/用品21/房间7/UI3）beforeIdentity=afterIdentity全部成立，GUID/fileID/名称/rect保留，源220PNG SHA均未变化。保留Editor退出分配器告警。迁移资产及Home提交bc0505b，随后contracts-green-01执行中。由于旧测试把身体原点当取景中心，改为Renderer.bounds.center；旧中心pivot断言更新为明确脚线6像素，房间墙线负例更新到实际32PPU格，不删除旧红灯材料。

步骤15：contracts-green-01为7/7，含百猫可达性和用品尺度；步骤16：ee2e50a完整EditMode edit-full-01为73/80，7项失败同源：EditMode合成请求分辨率不等于实际640×480测试窗口，Unity将pixelRect约束到实际屏幕。FOV/cover已通过，新增断言误把计算参数当真屏幕。修订恢复完整归一化rect并断言[0,0,1,1]，实际客户区仍必须由原生Robot严格检查，原XML保留。

步骤17：90707f0 edit-full-02完整80/80、退出0，保留前轮73/80。步骤18：9e9c719原生Robot新增实际纹理尺寸=逻辑纹理、脚线=原点、屏幕画布比例及原生viewport门禁；robot-grid-dryrun1/1仅语法检查，不计实际游戏通过。qa-grid-01开始构建，后续实际运行与画面验收。

步骤18实际：qa-grid-01退出0且最终Business QA Succeeded，但日志初次Bee脚本图未含新增WorldArtSettings/WorldPixelCamera，引发CS0103/CS0246；Tundra明确requires additional run，后续图刷新成功且构建清单已有两类。构建门禁仍拒绝该包，native-grid-01没有启动，不算Robot失败/通过。保留完整日志，待当前PlayMode结束后同源码独立qa-grid-02复构，不删除缓存或覆盖首轮。步骤19：完整play-full-01执行中，隔离保存迁移后44姿。

步骤19实际：play-full-01为91/92（163.65秒），真实失败Cat_frames_stay_inside_floor_at_all_four_edges：Milk上边3.78125超出3.01。脚点pivot后仍按对称半高约束，导致上身越界；不放宽原四角断言。修复以完整帧上下极值计算漫游中心偏移和安全半宽，出生身体仍居于配置视觉区域中心；Home视觉区域中心从脚点+26逻辑像素派生，用品格相对脚点偏移保持原值。新增派生中心/出生位置约束，继续同原四角用例与全量回归。

步骤20：6c2fa5e修复后首次rebuild-home-01未启动Editor，CLI退出6：转发的-quit属于保留参数。命令错误保留，按CLI自行管理退出的规则去掉参数，rebuild-home-02独立重建。

步骤21：contracts-green-02为8/8，原四角断言通过；步骤22：qa-grid-02退出0且无C#错误，provenance指向bcf76a6、clean，exe/Runtime DLL摘要保留。步骤23：native-grid-02 Robot1/1，实际三尺寸640×360世界RT，猫屏幕高度192/160/256、viewport恢复原生尺寸，脚点一致；已人工查看1920截图。步骤24：edit-full-03完整81/81。步骤25：visual-grid-01 Robot失败，原生点击0/3；33张图、4313帧30秒样本、百猫脚点/边界/家具深度均0错误，不将这些局部绿灯冒充点击通过。并行完整PlayMode正在复验。

点击排查假设（先记录后验证）：H1系统点击未送达自有QA窗口，则同环境旧qa-03也为0；H2低分辨率相机视口/射线转换改变，则旧包通过、新包失败且鼠标事件送达；H3 UI误拦截，则点击输入送达但EventSystem阻挡或目标不在Collider。先pointer-control-old-01运行旧包，不改生产代码或降低3命中要求。现有本地TODO与本报告作为缺陷跟踪和术语来源，不另发外部Issue。

步骤26：pointer-control-old-01同环境旧qa-03也为原生点击0/3，脚点/边界/深度均0；不能据此声称新相机造成点击回归。步骤27：完整play-full-02为92/92、退出0，上一轮四角失败保留；新姿势44图和既有40图独立保留。步骤28：仅QA新增pointer-probe逐帧记录窗口焦点/光标设置/Unity按钮事件/实际射线/UI阻挡；只有确认目标为本QA前台且光标设置成功才注入，复现模式第一尺寸后退出，避免每轮重复30秒采样。qa-pointer-01构建中。

步骤28结果：qa-pointer-01 source3c901f1退出0且无C#错误。68201f9短Robot pointer-probe-01失败，Player退出2；首条foreground-request=False、ownsForeground=false，UI与modal均false，射线可命中Cat_Milk，保护逻辑未注入点击。结合旧新均0/3，当前为原生输入环境未验收，不修改游戏逻辑、也不移除nativePointerHits=3门禁。Windows限制前台激活（https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow），具体拒绝原因不作唯一结论；后续获得QA前台时复验。继续Android独立AVD/Appium工作。

Android环境流水：AVD初次74424/77756启动，但内部调用System32/adb39杀掉SDK adb41服务；未进行任何手机/MuMu操作。首次重启保护因额外conhost/crashpad子进程拒绝操作（未停止进程），进一步精确验证父启动器/唯一QEMU后只重启自有进程。avd-02进程内设置SDK/PATH/ADB15037，回环5565曾连接并证实sys.boot_completed=1/model sdk_gphone64_x86_64，但后续旧ADB内部调用再次打断服务，QA用户创建全部device-not-found而未执行。emulator本地-help-all确认支持-adb-path，avd-03显式固定SDK37；前两轮日志与停止身份回执保留。没有改全局PATH/ADB或用户模拟器设置。

AVD03显式-adb-path后确认boot=1、稳定读取并成功创建/切换QA用户10；Appium独立14723已启动（UiAutomator2 8.6.0）。随后15037后台ADB仍反复消失，首次旧APK安装退出-1且空日志，包未确认安装，不能算升级基线。改用保留执行会话的nodaemon专用服务收集transport日志，5037不停止。Android普通candidate-01最终退出0但有初次Bee新类未发现C#错误，按门禁拒绝，candidate-02同纯游戏f536871独立复构中。只规范了Unity自动写入的m_SubKind尾空格，版本/素材不另改。

环境定论追加：独立ADB前台服务0xc0000409的日志明确为不支持监听带hostname的socket（could not install smartsocket listener: listening on specified hostname currently unsupported）。改为-L tcp:15037、不带主机名并保留会话14502后，emulator-5564和用户10正常可读；不把该配置错误写作游戏或SDK随机崩溃。旧版安装客户端虽退出-1，实际pm已显示包存在，需要以dumpsys code5和自然新档复验，不能把失败退出等同没有副作用。Appium14723/UiA8206专用，默认系统动画保留。

Android构建：candidate-02从纯游戏f536871成功退出0且无C#错误，APK111708615字节、SHA256 8e32a1f2b1192ac7fba2bb15bf904feaf8015b4cf730f8a6113026d9ffa44d9f（version1.1.0/code6待安装验证）。candidate-01的最终包和错误日志保留但不用于验收。旧普通1.0.2 SHA已与公开版本一致，android-old-smoke-01开始真实新档/后台/购买/冷启动验收，生成可用于覆盖升级的自然存档。

Android旧版基线android-old-smoke-01失败：已处理沉浸式引导，但菜单截图实际是System UI isn't responding，不是游戏功能断言失败；未创建自然两猫存档。system-ui-01环境恢复未执行点击：二级用户UiAutomator层级不含系统覆盖层，严格标题守卫失败，截图仍可见该对话框。人工核对参考图后，system-ui-02仅在已审阅区域RGB差<1时通过Appium点击Wait一次，截图差异确认消失（1/1，仅环境恢复，不计游戏测试通过）；保留两轮层级、原图和操作step记录。后续同旧APK独立重跑，不覆盖第一次失败。

Android升级android-upgrade-01 Robot1/1：普通code5→code6安装退出0，启动前实际存档SHA前后相同9af6255e3e0437970c490fb060fcfd91069bc33b2240501afcbcd884ade3e8e2，保留自然两猫、用品、点击和intro字段。Appium从实际菜单继续/关闭离线面板进入新画风Home，已人工看图。

Android-touch-01为0/3：首项色板定位到行走猫，但截图与触摸间猫实际已明显移动，保存clicks0；尚不能单独归因输入代码。后两项错误地假设热切wm尺寸后仍在Home：截图实际为主菜单，4:3更出现Android兼容视图重启提示。保留所有截图/日志/前后原存档。下一轮测试改为尺寸切换前ForceStop并完整菜单继续（沿用已验收适配流程），真实点击选择已渲染Sleep姿势的稳定猫身并记录源模板/匹配与触摸时序，不对失败触摸自动重试，也不更改生产逻辑。Windows另加窗口句柄/可见性/PID探针，区分句柄为空和前台权限，pointer-probe-02执行中。

Windows追加：pointer-probe-02确认目标句柄非0、visible=true、所属PID正确，但foreground=false，未注入。用户同意标题栏交接；pointer-manual-01却在鼠标前被旧Toast固定时刻断言中止，没有pointer文件，原失败保留。隔离PointerOnly的Intro/Toast后，pointer-manual-02实际Robot1/1，foreground-request=True、Input.down捕获、Physics命中Cat_Milk、clicks0→1，完整原生1920viewport；程序随后退出，位置探测未找到进程属于已结束而非窗口仍隐藏。没有声称用户必然点击或窗口长期可见。三尺寸完整视觉验收仍待新构建qa-visual-final。

Android-touch-02在suite setup因15037 SDK37服务再次退出-1而未发触摸；保留。换为SDK32专用服务后touch-03三尺寸菜单/离线/Home均正常，但Sleep识别90秒未通过，未注入触摸。离线同一失败截图含真实Sleep，INTER_NEAREST边缘重采样MSE562.799，INTER_NEAREST_EXACT像素中心重采样MSE0.127；保持MSE400门限，修正检测工具，未改游戏。touch-04随后UiA host观察进程4294967295退出，框架报instrumentation not running，三项失败；device exit-info表明UiA未发生OOM/崩溃，之后在teardown才被显式force-stop。独立server32仍存活，因Appium仍调用SDK37长驻am instrument，下一步仅改Appium的SDK客户端为原SDK32，所有参数/存档/游戏APK保持相同。原始exit-info/logcat和服务日志保留。

Windows完整visual-grid-02 Robot1/1（qa-visual-final/a6feac2）：1920/1280/1024三尺寸真实鼠标nativePointerHits=3，Input逐帧/Collider命中/点击数有记录；33张界面截图，30秒4320帧、100可见/440持有，bounds/roomFoot/furnitureDepth均0。原固定3秒Toast采样改为5秒内实际观测alpha渐隐，断言仍要求0<alpha<1，原时序失败保留。单次指针与完整视觉是不同用例，不混作同一个结果。

Appium服务切换保护两次拒绝：本版本/sessions不存在；后续关闭证明的原始文本匹配因HTTP200带ANSI颜色未命中。均未停止任何进程。去除ANSI后核验最近会话创建3904行、关闭4127行且无后续创建，保留proof，再仅替换自有14723节点。此为工具预检修订，不是游戏失败或用户授权缺失。

Android-touch-05：SDK32客户端/服务端匹配后，16:9实际Sleep猫身Appium触摸成功并保存clicks0→1；模板MSE0.244，原400阈值未改，原存档未注入。后两项失败截图是正常“第一次摸猫”成就CG，非Home不可渲染；测试未处理新获得成就的真实后果。保留1/3和CG截图。下一轮只识别既有story-continue参考后，在CG图片区域任意点击，Home保存后确认clicks不增加（防穿透），再继续三比例；不关闭生产剧情或预先伪造seen字段。

## 2026-11-16恢复记录

用户恢复Goal，上一轮为有效进展而非无进展：已迁移源码、保留原图与身份，完成NUnit81/92、Windows完整原生三尺寸和普通Android实际升级；暂停时服务已退出0。最新android-touch-06实际为2/3：20:9和4:3真实触摸及保存通过，16:9在CG图片关闭、Home保存并暖恢复后裸尺寸断言失败；CG关闭前后clicks未增加，失败不能混称为触摸未命中。新增恢复方向/尺寸逐帧记录及有截止等待，保留原尺寸断言含义，不对失败触摸自动重试。使用专用AVD恢复暂停前自然存档的测试检查点（明确属于QA恢复，不当作又一次自然新档或覆盖升级证明）。继续检查UI恢复、三比例、普通Windows与修改器、最终证据分仓及同Release交付。

2026-11-16实况追加：迁移后528用途逐条GUID/fileID/rect一致，220PNG摘要未变（Build/F1/migration-comparison.json），F1.2导入验证完成。Android复跑07因Windows端口8206拒绝绑定10013在suite setup失败；改为验证可绑定的18326，不改防火墙。08则显示继续游戏禁用，核对为恢复夹具的CatGame目录/root文件所有者不是包UID1010193，未到CG复现步骤；仅修正隔离目录/文件所有者后09运行中。07/08失败保留，不计入游戏回归。普通Windows/Android同源码f536871候选已构建，正在准备本地双平台发行材料，尚未推送或发布。

2026-11-16最终本轮进展：android-touch-09 Appium3/3（同普通f536871/code6），实际1600/2000/1200×900；自然Sleep模板定位后，各单次原生触摸在Home保存为clicks1→2、2→3、3→4。首次摸猫CG用图片区域点击关闭，前后存档clicks保持1，未点击穿透。复验使用明确标记的暂停前QA检查点，不能替代原先已通过的自然旧档升级证明。三个Home原图右边缘检查通过（room-coverage.json）；原07端口和08夹具所有者失败完整保留。Windows本地分发候选包含发布过的独立修改器，152文件ZIP解压摘要逐条校验，尚未发布。下一步保留完整目标：普通Windows包/修改器实际交付检查、购买/CG/结局回归、独立证据仓库整理、双平台同Release发布、最终工作区和交付核验，F1.5/F1.6不提前勾选。

剩余16:9失败的实际证据已确认：android-touch-09/steps.jsonl捕获CG关闭后暖恢复的一帧actual=[900,1600]、expected=[1600,900]，随后方向恢复，3/3用例通过。修改限于测试等待与诊断，未修改游戏代码或放宽最终尺寸要求。CG图片点击前后原始存档clicks不变，真实摸猫才各增加1。该证据解释原裸断言的失败，不能将其写作游戏无法恢复横屏。

2026-11-16交付验收追加：已核验远程Git标签v1.0.1→cc926ec、android-v1.0.2-preview.2→bd85811；本轮双平台候选共同基于f536871，尚未发布。用户明确允许存档测试，仍只读备份正式目录4文件，原始备份为私有材料，不公开。windows-release-probe-01在窗口前台等待超时，finally已恢复；02排除启动窗口并细化阶段记录后通过：普通Release包真实继续载入两猫测试旧档、SetHair/SetCat在内存应用、游戏自动存为三猫、v1.0.1已发行的独立修改器EXE自动连接本轮PID且frozen=true（指PyInstaller独立打包，并非游戏时间冻结）。退出后4文件原始SHA全部恢复。原存档内容不进入公开证据。

Android普通包结局两出口回归android-ending-01失败在2000菜单就绪，照片是Unity启动底色，没有到购买环节；有限exit-info未见崩溃，记录为启动就绪未验收，不写成结局交易失败。02仅对菜单增加45秒有限观察并记录耗时，其余购买/取消/CG/统计严格断言不变。安全区实际验证、最终公开证据整理及合并Release仍待完成，Goal保持active。

结局定位追加：android-ending-02的45秒等待仍失败，实际initial-menu.png显示Android兼容视图的“Tap to restart this app for a better view”及黑边；不是游戏无菜单，也不是结局交易失败。旧通用结局助手在初始化与finally恢复尺寸时未先停应用，产生暖恢复的旧宽高视图。F1包装器已改为每次开始前ForceStop，恢复显示前也ForceStop；03复验中，菜单等待保留耗时记录，所有交易/CG/统计条件未变。用户关于v1版本管理的疑问已用远程refs/tags实际映射核对，Git源码历史/标签、GitHub发行附件与本地Build目录作明确区分。

## 普通交付包验收追加（2026-11-16）

Windows普通包来源纯游戏f536871（1.1.0），windows-release-probe-02通过：发现实际UnityWndClass窗口后原生继续进入Home；公开v1.0.1中的独立EXE修改器自动连接本轮PID，运行时修改猫毛/猫数量，等待游戏10秒自动保存后核对。passed=true/restored=true，4份原用户文件按备份摘要完整恢复。probe-01的启动窗口发现失败保留；不能把PyInstaller frozen标志误解为冻结游戏时间。用户后续明确允许验收使用当前存档，不要求保留进度；本次仍完成恢复。原用户备份、注册表令牌不公开。

Android普通包code6的android-ending-03由Robot编排Appium，1/1通过，实际两条分支：2000×900取消火箭再购买→结局CG任意图片点击→统计→继续；1200×900购买→CG→统计→主菜单。核对扣款、rocketPurchased、endingCompleted、firstEnding快照不变。使用明确QA夹具并注入火箭价格+500，不声称自然通关耗时。保留ending-01/02失败：首轮菜单等待超时；第二轮截图为Android尺寸兼容重启提示。第三轮在改wm尺寸前和启动前冷停本应用，记录每次视觉等待，未修改游戏生产代码或放宽图像相似阈值。报告及原图位于Build/F1/android-ending-03。

Android安全区专项启动：仅自有只读AVD emulator-5564、QA用户10，原cutout模拟全关闭，临时开启tall刘海并请求900×1600物理覆盖，横屏实际画面和系统Insets正采集；尚未判定通过。该临时环境探针属于本次测试记录，完成后关闭overlay并恢复尺寸。不操作手机/MuMu。

## 安全区探针的环境与用例修正

safearea-01/02：QA用户及系统用户开启tall，但wm size覆盖使逻辑切口仍为0；DisplayDevice物理报告132px不能充作窗口逻辑安全区证据。恢复原生1080×2220后，窗口横屏实际2220×1080、左切口132px。safearea-03/04的识别条件错将带白色文字的一行要求为至少300个连续背景色像素；04保留失败原图，按钮实际完整。识别改为文字上方背景行，05菜单center1854.5 vs安全区公式1854.6通过，但未处理离线弹窗而Home等待失败；06保留原图，确认实际为欢迎回来弹窗。1080p字体原生重栅格化与缩小后的900p文字模板存在10.7RGB差异，未放宽旧2.5阈值；07改用已人工查看的离线弹窗/按钮几何与无文字色块确认后仅点击一次。这些是环境或测试实现问题，不计作游戏缺陷。所有轮次保留。

## F1安全区专项验收（2026-11-16）

android-safearea-07 Robot1/1通过普通f536871/code6。AVD原生2220×1080，WindowManager真实切口insets=[132,0,0,0]；继续按钮实际中心1854.5px，安全区公式132+(2220-132)*0.825=1854.6，误差0.1px。Appium原生点击按钮进入Home，明确识别并关闭离线收益弹窗一次，HUD左信息面板实际位于切口内侧，截图人工查看可见完整功能栏、房间及两猫，无右边背景露边；Home切后台保存后仍为2猫且endingCompleted。finally关闭QA和系统用户tall overlay、恢复900×1600覆盖，Appium会话关闭。原03–06测试实现失败及01/02无切口探针保留，不重复计为产品Bug。

配合Windows三尺寸原生显示/点击、Unity2712×1220投影用例、Android1600/2000/1200×900触摸、上述真实切口，F1.4完成。F1.5引擎规则与真实对象（Edit81/81、Play92/92）、独立原生Player、普通Android Appium、普通Windows修改器和结局均有分层记录，进入证据整理及最终发行核验；短时百猫仅为边界/显示回归，不宣称小时级稳定性、真机性能或所有Android设备适配。

逐步输入取证追加：本轮Appium服务端HTTP请求/响应813行按时间筛选到evidence/f1/appium-http-steps.txt，保留实际clickGesture坐标、键值、截图请求和返回状态；仅移除启动capabilities与替换会话标识，不改操作结果。完整服务端日志留本机。safearea-07对应最后一次会话：1854,446继续；1176,623关闭离线框；HOME后存档核对。它是从实际日志提取的证据，不伪称脚本当时新增了逐步打点。
