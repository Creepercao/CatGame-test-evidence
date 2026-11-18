# S1 场景成长交付报告

2026-11-18。目标为两套可玩像素房间，沿用F1世界密度、脚点和等距结构。本文为本轮交付范围，历史方案与失败记录不删除。

## 实际行为与配置

- 初住暖木小屋Starter；累计产出totalEarned达到100000进入伙伴客厅Companions。花费余额不退回，无阶段编号，无新增购买锁。
- 建筑512×288，家具64×64片组，世界32PPU/Point。Starter6片、Companions11片；多片家具按语义共用地面脚点和深度，附独立占地。保留猫咪、用品、HUD、经济对象，新增占地覆盖猫脚时仅校正位置。
- 过渡0.6秒，模态门控暂停；引导、菜单、待播CG优先。旧档和离线收益按最终累计选主题，不增加存档字段。不制作或声称五套房间均完成，后续量级暂沿用伙伴客厅。
- Unity菜单`CatGame/Art/Select room growth themes`，资源配置`Assets/Resources/Settings/RoomThemeSettings.asset`；可调门槛、资源目录、过渡时长。修改占地需同步维护layout与家具脚点。
- 正式素材由JSON像素工具原生绘制，15色，不是将1672×941概念图缩小。概念预览、源JSON与生产PNG分开保存。

## 版本与Git

纯游戏运行时d25e3c31415e69e9107544f6214244e6d086cc7a；发行源码5a0f4376bd04723a2b8e38cd42974dec23a2ebc4，差异仅README。版本1.2.0/Android code7；Windows与Android同源。提交使用Creepercao隐私邮箱。测试工具、QA代码和文档进入独立证据仓库，私有工程及用户备份不公开。

本地分发`Build/S1/distribution-v1.2.0`：Windows ZIP包含已发布的独立修改器，153项解压读回完全一致；APK SHA256为9c68b1df6b8db6d56fafee0524947d19d8ac0b45b1e69d3e34b406588d9e9a80。沿用Android既有调试签名，覆盖安装保留存档；未上架。目标发行链接：https://github.com/Creepercao/CatGame/releases/tag/v1.2.0，发布完成以服务器回执为准，不以本报告文字代替实际发布。

## 证据与结果

| 层次 | 实测 | 范围 |
| --- | --- | --- |
| 原生素材 | art-check-02 4/4 | JSON/PNG一致、地板结构、家具片组脚点、受限调色板 |
| EditMode | edit-full-01 89/89 | 临界值、非法配置、原存档读回、离线跨节点 |
| PlayMode | play-full-02 99/99 | 真实CG先播、模态互斥、对象保留、百猫、缺资源恢复 |
| 追加命令集成 | play-theme-06 8/8 | 含新增真实修改器命令与换房；不是全量100/100 |
| Windows QA Player | windows-growth-01有效部分 + windows-restart-01 | 两主题1920/1600/2560截图、实际自然收益跨节点、原生鼠标clicks+1；百猫20.005秒2881观察，脚点/深度错误0；独立新进程关闭离线框后100可见/440拥有并可游玩 |
| 普通Windows启动 | windows-menu-debugger-01 passed/restored | 同源普通包启动、主菜单写保护、独立修改器自动连接本轮PID；frozen=true是独立EXE标志 |
| Android自然基线及升级 | baseline-smoke-02 1/1、upgrade-01 1/1 | 普通1.1.0自然产出购买第二猫，Home/冷热恢复；code6→7覆盖前后原存档字节一致，加载进度保留 |
| Android场景/触摸 | android-growth-01 4/4 | 明确标记夹具从99990以实际收益跨节点、重启保持；1600/2000/1200×900实际Sleep猫身Appium触摸，保存clicks0→1→2→3；三比例背景覆盖 |

各层统计不相加为独立用例总数、通过率或覆盖率。Android成长夹具将20个CG标为已看，仅隔离场景变化；不冒充自然全通关或Android端CG顺序证明。

## 保留的失败与限制

百猫Rebuild的延迟Destroy取样440而非100、重启QA误找Card而未关Summary、测试类命名空间歧义、首帧自然收益误假设均属测试问题，原轮次保留。首轮Windows重启绿灯不能证明无遮挡交互，已独立复验。AVD SDK目录配置错误与System UI ANR不计产品Bug。

两端首次增量构建退出0但出现CS0246，均拒绝采用，第二次无C#错误才进入打包。Windows普通包前台激活多轮失败、定向OS消息也未完成菜单进入；本轮没有重新证明普通包内运行中修改器编辑/自动保存全链路，不能拿F1旧结果替代。真实鼠标和换房有效证据来自本轮QA Player，实际命令集成另有NUnit。此限制留作后续有前台交互条件时复测，不改成成功。

未重新测真机性能，未做小时级稳定性，手机和MuMu未操作。只读AVD/本轮Appium/隔离ADB均退出，端口无残留，当前4个私有文件按本轮备份恢复；私有内容不发布。

每步操作和原始失败见S1场景切换测试记录、Robot output.xml、NUnit XML、steps.jsonl、Player日志及PNG；独立核验器仅检查记录和摘要一致，不等于重新运行Unity/Appium。

最终追加：统一v1.2.0已公开，GitHub四附件摘要匹配，标签5a0f437且latest正确，五个旧发行仍保留。最后文件审计曾发现后续未显式隔离的CLI测试再次写正常两份存档；保留私有副本并恢复4文件后，新增强制隔离执行器。play-theme-07重复8项8/8，正常文件前后摘要不变。该测试隔离缺口及修正留痕，不把各轮finally曾成功恢复扩大成整轮始终无影响，也不公开用户存档。
