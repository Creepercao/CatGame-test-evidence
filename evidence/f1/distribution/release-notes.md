Windows 与 Android 放在同一个发行版，运行时代码相同。Windows ZIP 已包含可选的外置实时修改器，进入游戏后自动连接，由游戏正常保存修改后的数值。

- 统一猫咪、房间和用品的世界尺寸、脚点、地面锚点；保留等距房间、玩法数值和旧存档兼容。
- 世界层采用 Point 采样；1920×1080 / 2560×1440 使用整数显示，其他尺寸完整取景回退。UI、中文文字与 CG 保留原生清晰度。
- Unity 可通过 WorldArtSettings 配置；素材迁移保留 GUID、切片身份与动画帧顺序。

下载 Windows ZIP 后完整解压运行 CatGame.exe；Android 下载 APK 直接侧载或 adb install -r 覆盖安装，详见附件安装说明。APK 为 ARM64、versionCode 6，沿用原有侧载签名。SHA256SUMS.txt 用于校验下载文件。

验收：EditMode81/81、PlayMode92/92；Windows真实三尺寸与原生鼠标、普通包与独立修改器；Android14 AVD普通APK code5→6实际存档升级、三比例触摸、结局两分支和真实132px切口。完整过程和失败轮次见[独立测试证据](https://github.com/Creepercao/CatGame-test-evidence/blob/main/reports/f1-release-verification.md)。本轮未重新测试真机性能或小时级稳定性，旧版真机结果不代表本版新测。

标签提交1b82df9；受测普通包构建自f536871，两者仅README不同。旧发行与标签继续保留。
