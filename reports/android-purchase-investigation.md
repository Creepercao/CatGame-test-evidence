# Android 购买中断排查

2026-11-13，使用相同的公开源码候选 APK，SHA-256 为 `449fe74ba4d4413cfe81f7c20ac145a36438996682c618902972c684d59d5aa2`，Android 14 AVD、1600×900、Appium UiAutomator2 8.6.0、隔离 QA 用户。

上次购买到 Home 0.594 秒后强停重启仍为一猫，保留原失败。本轮使用此前真实跑出的单猫 QA 档，分别对照短点击与按下 350 ms、前台等待与立即 Home。前台等待 15 秒再强停的四组恢复两猫。其中两组发送 Home 后发现 QA 用户尚未初始化，游戏仍在前台，这两组仅能作为前台保存证据。

后续两轮在后台前置断言失败。查询该临时新建用户的 `user_setup_complete=0`，仅对 QA 用户置 1。后台确认后立即强停的四组均恢复两猫，其中短点击到 Home 的最短耗时为 0.156 秒。每组均有强停后与冷启动后的存档，PID 也发生变化。[原始对照数据](../evidence/android/purchase)

原始新档自然收益 Robot 用例复跑 1/1 通过。包含引导、成就 CG、购买、Home、强停、冷启动和继续游戏。购买到 Home 为 2.031 秒，小于原来的 3 秒预算。最终两猫画面 RGB 误差 0.92，门限仍为 2.5。[原失败 XML](../evidence/android/purchase/original-failure.xml) · [本轮通过 XML](../evidence/android/purchase/original-rerun.xml)

当前候选包能通过本轮流程，上次偶发失败的根因尚未复现确认。新增测试用户初始化检查防止无效后台测试，它无法解释原失败，因为原记录中 Home 已进入 Launcher。游戏源码未作修改。对照脚本及前置检查来自本地提交 `3281900`。[对照脚本](../harness/android/diagnose_purchase.py)
