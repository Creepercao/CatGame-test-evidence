# Android 购买后切后台失败排查

## 当前验收结论 · 2026-11-15

已复现并修复一种已购买进度丢失：BuyCat/BuyItem仅标脏，Android可在C#暂停保存前被强停。成功购买现同步尝试SaveNow，错误记录/脏标记重试保留；火箭共用同一路径，不改变价格/收益/存档结构。原历史单次失败的唯一原因无法追溯确认，不把本轮受控复现等同于原现场精确重播。

| 执行 | 实际结果 | 证据目录（Build/Android/purchase-trace/） |
|---|---|---|
| 旧诊断八组，无购买确认即Home | 7购买保留，1仅Down后Pause、无购买回调；诊断Robot通过不等于购买8/8 | matrix-01 |
| 旧诊断八组，确认购买后Home | 7保留，1已回调两猫但落盘一猫；Robot失败 | matrix-confirmed-01 |
| 修复首轮 | 前3组保存正确，第4组在3秒门禁提前失败，未收全该组证据 | matrix-fixed-01 |
| 修复完整第二轮，默认系统动画 | 保存8/8，末组Home3.125秒超预算，Robot失败 | matrix-fixed-02 |
| 相同修复包，显式禁用临时AVD系统窗口动画 | 保存8/8，四组Home均小于原3秒，Robot1/1 | matrix-fixed-noanimation-01 |
| 旧普通包自然收益存档基线 | Robot1/1，购买确认到Home1.984秒，冷启动两猫 | upgrade-baseline-smoke-01 |
| 普通1.0.2覆盖升级 | 直接Appium脚本通过；安装前后存档字节相同，继续后保留两猫 | upgrade-01 |
| 普通1.0.2首次新用户 | Android全屏教学遮挡，Robot失败，未新建档 | release-smoke-01 |
| 普通1.0.2新用户复验 | 按明确系统教学ID关闭提示；Robot1/1，购买到Home0.625秒，冷启动两猫，停止后主档两猫 | release-smoke-02 |
| 购买即时落盘用例 | 原码0/2，修复2/2；完整PlayMode89/89、EditMode69/69，无跳过 | durability-red/green.xml、full-play/edit.xml |

普通包：1.0.2/code5，ARM64，纯游戏源码bd85811；与preview.1签名一致。原始证据不改成成功；分析器把保存结果和时序门禁分别重算，并在任一失败时退出1。Appium请求/响应另存逐步JSONL，Robot时间为本地+08、服务器日志为UTC；不直接用设备UTC减主机UTC。

纠正前文计数笔误：覆盖升级的exact比较字段实际为11项，另有2项集合保留和3项单调增加检查，不是12项。新APK验收在Android14临时AVD进行，本轮未重复手机性能采样，不以历史v4真机P95代表新版本。

普通APK与安装说明/SHA三个附件已上传发行草稿并完成服务端摘要核对，尚未公开；最终公开与证据仓库提交后追加。首次发行脚本的本地仓库所有权、草稿按tag查询404、UTF-8解码问题均在取证/发布工具层，分别调整为单命令safe.directory、现有发行ID读取与显式UTF-8；未重复创建发行、未泄露凭据。


## 2026-11-13 · 复现准备

上次发布候选 APK 的 `avd-smoke-05` 在购买点击到 Home 0.594 秒后，强停重启仍显示一只猫。保存的 JSON 中 Milk 数量为 1，余额仍随自然产出增加。该轮结果为失败，尚不能仅凭点击命令成功确定购买已被游戏受理。

复现对象为 `Build/Android/release/CatGame-Android.apk`，SHA-256 `449fe74ba4d4413cfe81f7c20ac145a36438996682c618902972c684d59d5aa2`。使用现有 Android 14 AVD 的只读临时实例，独立用户 `CatGame-Appium-QA`，Appium UiAutomator2 8.6.0，1600×900。沿用上次真实跑出的单猫 QA 档作为前置，仅清零离线时间基准。

待检验的原因按以下顺序记录。

1. 短点击未被 Unity 受理。预测同一档案下短点击即使留在前台也可能不买，延长触摸按下时长后购买成功。
2. Home 或强停先于后台保存。预测前台购买能成功，而立即 Home 后存档丢失；增加后台等待可改变结果。
3. 写盘错误。预测画面猫数已增加，但 JSON 未增加，日志出现保存错误，或重启状态回退。

对照脚本为 `Tests/robot/android/diagnose_purchase.py`。分别使用 Appium `clickGesture` 和按下保持 350 ms 的 W3C 触摸，并对照留在前台与立即 Home 两种情形。每轮使用相同 QA 档，记录购买前、输入后 15 秒、强停后与重新启动的主档，以及屏幕截图、PID 和输入耗时。输入后 15 秒覆盖 10 秒自动保存周期，以区分输入和后台保存。

本轮环境准备曾遇到 ADB 重启后目标暂不可见，以及 Appium 辅助包路径未命中。设备检查阻止后续执行，重新核对模拟器与 APK 实际路径后再继续。原始输出留在会话中，运行日志在 `Build/Android/purchase-diagnosis/`。

## 四组对照结果

首轮四组均购买成功，重启后的 JSON 都是两只 Milk。此前的写盘错误日志没有出现。本轮前置直接取自上次失败后的 QA 档，余额为 79.307983，购买扣除 26 后继续自然产出。

| 触摸 | 后续操作 | 输入耗时 | 输入后、强停后、重启猫数 |
|---|---|---|---|
| clickGesture | 前台等待 15 秒 | 0.109 秒 | 2、2、2 |
| clickGesture | 发送 Home 后等待 15 秒，后查仍在前台 | 0.219 秒 | 2、2、2 |
| 按下 350 ms | 前台等待 15 秒 | 0.437 秒 | 2、2、2 |
| 按下 350 ms | 发送 Home 后等待 15 秒，后查仍在前台 | 0.515 秒 | 2、2、2 |

原始主档和画面分别在 `matrix-01/{序号}-before.json`、`after-input.json`、`stopped.json`、`restarted.json` 及同前缀 PNG，汇总为 `matrix-01/results.json`。全量 Appium 请求与时间戳保留在 `appium.log`。由于该轮有意等待了自动保存周期，随后使用 `--immediate-stop` 单独检验原用例更紧的终止时序。

立即强停首轮和第二轮均在后台检查失败，尚未执行预定的立即强停。第一轮只检查一次，第二轮采用原 Robot 的 5 秒内每 300 ms 轮询仍失败。核对 `matrix-01/2-after-input.png` 可见游戏房间，确认上述 Home 两行只可作为前台保存证据。进一步查询发现临时新建 QA 用户的 `user_setup_complete=0`。这是新建用户环境，原失败轮次已成功切到 Launcher。仅对临时 QA 用户置 1，保留两轮失败，随后运行 `immediate-03`。

## 原时序对照

`immediate-03` 在确认 Launcher 前台后马上强停，没有等待自动保存。两次 clickGesture 的点击到 Home 耗时为 2.172 和 0.156 秒，两次 350 ms 触摸为 0.515 和 0.516 秒。四轮强停后的 JSON 均为两只 Milk，重新启动后的 JSON 也为两只，且每轮冷启动 PID 均变化。

该结果支持当前 APK 可以通过后台落盘，但没有复现上次偶发失败。暂不能把根因归到触摸丢失或保存错误。继续以原 `android_smoke.robot` 跑新档、引导、自然收益、成就 CG、购买、Home 与冷启动。新增 QA 系统初始化前置检查，未初始化时在 Appium 会话建立前失败，避免将无效 Home 当作后台测试。

## 原始 Robot 流程复测与当前结论

`original-smoke/output.xml` 实测 1/1 通过。新档通过引导，自然产出达到购买条件，出现并关闭成就 CG，购买到 Home 为 2.031 秒，低于原 3 秒预算。热恢复 PID 保持 13236，购买后切后台并强停，冷启动 PID 变为 13660。继续游戏与离线结算后，HUD 两猫画面比对通过，误差 0.92，像素门限仍为 2.5。全过程截图、Robot 日志和 HTML 报告分别保留。

本轮未更改游戏源码或重打 APK。确认的结果是当前候选包在上述前台与立即强停对照、原自然收益用例中都能购买并保留进度。上次偶发失败没有复现，根因仍未确认，保持待跟踪。新发现的临时 QA 用户初始化问题已通过测试前置检查防止误测；它不能解释原失败，因为原记录中 Home 已进入 Launcher。

后续真机验收应保留同样的购买、后台、强停与重启数据。如果再次出现一猫，先对照购买前后余额、画面和落盘时间，再判断点击未受理或保存回退。

本地测试提交为 `3281900`，作者为用户的 Git 身份。新增前置检查实测未初始化用户被拒绝，初始化完成后 Appium 可正常连接，结果在 `setup-guard.json`。测试结束后关闭本轮临时 AVD 和 Appium 服务。

独立证据仓库已推送 `f980711`，包含原失败 XML、复测通过 XML、立即强停前后存档与脚本。317 份材料的哈希和结构校验通过，核验脚本也检查了这次对照的猫数与 PID。哈希核验本身不计为一次新的游戏测试。[公开排查报告](https://github.com/Creepercao/CatGame-test-evidence/blob/f980711/reports/android-purchase-investigation.md)

## 2026-11-15 · 输入受理诊断准备

用户恢复工作，小时级测试不安排，手机不参与。继续独立 AVD 排查 M14。保留原失败和复测结果，不以未复现结案。

新增独立 Development 诊断包 `com.catgamestudio.catgame.inputqa`，仅该包且启动 intent 提供合法 GUID 的 `catgame_input_run` 才启用记录；`catgame_input_fps` 仅接受 6/60。正常游戏和 .perf 包不启用，不改变游戏购买/保存实现。观测组件只实现 PointerDown/Up，购买按钮原回调后的第二监听记录内存猫数，不添加拖拽处理、不改为按下购买。每半秒记录一帧时长，事件立即追加；仪表有 IO 扰动，本轮不作性能验收。

计划同一单猫 QA 档、同一位置，对照 60/6 目标 FPS、短点击/350ms、前台等待/立即 Home；每次分别记录输入耗时、游戏事件、主档、强停和重启结果。6FPS 是主动放大条件，不等同于原故障现场；只有出现受理证据后才能判断，未受理不得写为购买保存丢失。构建、安装及实测尚未执行。

### 首轮八组实测

诊断构建源 46446c0，Unity CLI 1.0.0/Unity 2022.3.21f1，退出0，APK SHA256 `647a3699415eb03833a80fc77e6bfcc3c0ef2659a512e4a22282001d4c61f47d`，独立包 versionName1.0.1/versionCode4。CLI 保留 LicenseClient 通道/签名警告，最终构建成功，不能写为无警告。临时 AVD 无旧 QA 用户，重新建立用户10并完成系统初始化；安装首次 ADB 短暂未识别目标失败，重查仅 emulator-5564 后重装成功，未操作手机/MuMu。

Robot诊断1/1完成（不是八次购买全部通过）：八组中七次购买回调后内存2猫、强停后2猫、重启2猫。第6组6FPS/短点击/立即Home出现1猫。其事件为frame69收到pointer-down，随后仍在frame69发生paused/blur，无pointer-up及after-button-callback，存档也为1猫。这支持本次受控故障为输入未完成，未发现已购买后丢档。6FPS实测帧间隔约166.67ms，不能将目标60FPS写为实际60FPS，60目标组实测多为33–66ms。原始记录位于Build/Android/purchase-trace/matrix-01。

注意主机inputUtc与Android事件UTC存在时钟偏移，不能直接相减；判断顺序使用同一Unity frame/realtime及同一主机monotonic，各自内部排序。未将该受控复现当作原2026-11-13历史事件的唯一根因。

准备修订生命周期用例：购买短点击一次后先观察HUD两猫，再立即Home，仍小于原3秒预算且不等待10秒自动保存、不自动补点；原始无确认路径保留在诊断矩阵中。该改动限定测试前置语义——验证“已发生的购买能保存”；不能拿新用例通过宣称低帧率输入问题已修好。确认对照尚在执行，不提前写通过。

### 确认后Home仍出现已购买丢失（新证据）

第二轮 matrix-confirmed-01 Robot1/1失败，八组实际7/8保留；第6组6FPS短点击已截图确认两猫、Unity frame67已记录pointer-up和after-button-callback（milk2、余额54.2093），Android已切后台后强停，磁盘仍一猫。该组 trace 没有paused，说明“系统已后台”先于Unity暂停回调；不能把没有暂停记录当作整组环境无效而丢弃这次真实购买丢失。分析器已改为显式记录pauseObserved，核对原始主档与事件，结果 accepted-but-lost=1。

因此前一节“修订测试前置即可”的假设不充分，继续生产修复，不以确认用例替代产品保存问题。源码显示BuyCat/BuyItem目前仅MarkDirty；暂停/10秒自动保存才落盘。计划成功购买立即提交，同已有火箭购买一致；摸猫/自然产出仍按原自动保存，不新增每帧写盘。先补Unity测试：成功购买后不yield、不发pause、直接从磁盘恢复须有扣款和新数量，再改源码与实机路径复验。

Unity测试命令首次误用test不支持的--log-file，CLI退出2且未执行Editor；该环境/命令失败单独保留为durability-red-cli.log，后续去掉参数重试，不计为红灯用例。

### 生产修复红绿

新增PurchaseDurabilityTests两项真实红灯0/2：猫购买磁盘余额1000应为974、用品1000应为900。随后将成功的猫咪/用品/火箭购买统一到PersistPurchase，标脏后仅当SaveSystem拥有同一State才同步SaveNow；摸猫/自然生产不改自动保存。原SaveNow错误记录与标脏重试逻辑仍保留，不宣称硬件/权限失败也可保证成功。相同两项绿灯2/2。完整PlayMode/EditMode正在执行，尚未构建修复APK，M14保持未完成。

失败第6组的PID13518专属Unity日志已提取：Java主线程存在`I Unity : onPause`，但C#诊断没有paused事件；应表述为“Android/Java暂停通知已到、C#暂停处理尚未观测到”，不能笼统称所有Unity层都未收到。主档仍一猫与购买回调后两猫共同构成保存空档证据。日志仅对应临时AVD诊断应用PID，不读取手机日志。

### 全量回归与修复构建检查点

PlayMode89/89、EditMode69/69均通过，无跳过；原两项即时落盘定向红0/2、绿2/2记录保留。生产修复与回归提交85c45c5，作者为用户Git身份。修复诊断APK输出到Build/Android/purchase-trace/fixed，正在构建；尚未安装复测、未更新普通发行APK，也未推送本轮公开证据。不将引擎绿灯代替Android设备绿灯。

修复诊断APK构建成功，源85c45c5/dirty=false，SHA256 `73bfc73c378f0d49cd68590c86a08275e5fcd961e47358d0e34ee495d99d913b`（121964632字节）。构建完成后首次安装再次ADB重连失败，确认同一AVD启动/QA用户10后重试安装成功；Robot matrix-fixed-01八组确认购买对照正在执行。普通候选版本准备升为1.0.2/versionCode5，确保后续覆盖安装版本递增；该元数据变更未视作设备测试通过。

修复诊断首轮matrix-fixed-01前三组均购买/重启2猫，第四组60FPS保持350ms确认后Home超过原3秒预算，脚本按门禁失败，不能作为全量通过。首版脚本在预算断言处提前退出，未提取第四组事件/存档，暴露失败证据采集不足；保留原XML/截图/错误，不将前三组绿灯外推全部。改为每组完成事件及存档采集后再汇总同一3秒门禁，matrix-fixed-02正在执行，阈值及购买/强停动作未放宽。

独立纯游戏本地候选提交bd85811只包含IdleGameController生产修复和1.0.2/code5元数据，未含诊断、测试或文档；已检查并保留该仓库原有LF格式。该提交尚未推送。Windows已公开1.0.1/Android preview.1仍是旧发行物，不能把本地候选当作已发布。

第二轮修复包matrix-fixed-02完整八组均购买并强停/重启2猫，保存一致性8/8；最后6FPS保持350ms组Home总3.125秒，超过原3秒门禁，Robot仍失败。未放宽阈值。Appium HTTP时间戳显示该组Home请求从06:46:05.795到06:46:07.634，服务1838ms，其余该轮Home为63–89ms。主机离线同截图PNG编码3次为27.7/19.5/18.3ms（非游戏性能采样），不支持将超时主要归因PNG编码。下一步显式对照Appium官方已安装8.6.0支持的disableWindowAnimation，仅临时API34 AVD，默认其他用例不变，驱动正常关闭时恢复系统比例。不得把关闭系统窗口动画说成游戏帧率优化；原时序失败保留。

普通APK从纯游戏bd85811候选开始构建，1.0.2/code5，尚未验收/发布。

普通候选构建退出0，aapt核对包名com.catgamestudio.catgame、1.0.2/code5，SHA256 `c0fbcbb264d986bb63ffa4691d3473572198dc59cfb2d63060950a74169e0a66`，111702349字节。CLI本轮未提供source字段，不能伪称该字段证明源提交；使用构建命令指定纯游戏目录、构建前本地bd85811干净提交、构建日志及输出摘要建立来源记录。构建后移除仅Unity自动写入的m_SubKind尾空格，纯游戏目录恢复干净。普通包尚未安装/验收/推送/发布。

动画对照首次前置因构建后ADB尚未重连失败，未建立Robot会话；确认原模拟器进程84884仍存活、未重启，后续同一emulator-5564恢复sys.boot_completed=1和用户10。再次执行matrix-fixed-noanimation-01，显式动画开关只作用临时AVD，结束后读回系统比例。

### 普通候选验收进展

动画对照matrix-fixed-noanimation-01完成Robot1/1通过，八组保存8/8且四组Home均小于3秒；它与旧默认动画失败分开报告。正常结束后window/transition均恢复1.0，animator从未显式设置null恢复为默认值1.0，不能称设置字节完全相同。临时只读AVD结束后丢弃，不改变用户正常设备。

上一版普通APK升级前新档Robot1/1通过，购买→HUD确认→Home1.984秒，自然收益购买后冷启动两猫。随后check_purchase_upgrade.py直接Appium专项通过（不是额外Robot计数）：1.0/code4→1.0.2/code5，安装前后主档字节一致SHA256 `3a8d8cf62474fd19f84025c348cf35477da420c442aa8154fa552078010c7dfa`；继续与离线结算后两猫，核对12项进度字段，收益/游玩时间允许正常增加，未注入存档。APK签名v1/v2均通过，与preview.1证书一致。

普通APK范围审计：纯游戏467项跟踪文件无Tests/tools/docs/AssetsTests，候选与父提交只变动IdleGameController和ProjectSettings；ARM64原生库未找到五个已知QA标记。该检查只支持已审计标记及路径，不宣称任意二进制字符串均已证明无测试代码。另用一猫/两猫实际截图分别比对两个HUD裁片，一猫对一猫误差0.943/对两猫13.861，两猫对两猫0.922/对一猫14.053，原门限2.5能区分，未放宽。普通新版新档验收已另建隔离QA用户进行中。

普通新档release-smoke-01在新QA用户11首次启动失败，菜单视觉误差107.75。检查截图可见Android全屏教学“Viewing full screen / Got it”覆盖菜单；只读原生树确认包android、标题id android:id/immersive_cling_title、按钮android:id/ok。此时尚未新建存档，不清数据、不改游戏。为首次菜单观察增加显式handle_system_hint=true，只在上述标题与专属按钮同时匹配才点击原生按钮；不对ANR/游戏弹窗自动点击。原失败XML/图片/树保留，release-smoke-02重跑中。
