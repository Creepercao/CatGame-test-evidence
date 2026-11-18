# 版本、Git 与发行对应说明

核对日期：2026-11-16。正式游戏源码由Git管理；构建目录、素材文件名与Git标签是不同概念。

| 名称 | 实际机制 | 当前证据 |
| --- | --- | --- |
| v1.0.0 | 游戏仓库远程Git标签＋GitHub Release | baf37d1a554b60c7d2d401d2b686c60e3d5d1643 |
| v1.0.1 | 游戏仓库远程Git标签＋GitHub Release | cc926ecfa0c397017e0394f883e1520d985a66a0 |
| android-v1.0.0-preview.1 | 既有Android远程标签＋Release | 0076ec50b2e6c4a3168493aadb51fadf7577e588 |
| android-v1.0.2-preview.2 | 既有Android远程标签＋Release | bd85811ae749fdc5b9d574c8b3e640acdde66197 |
| 本次构建时1.1.0候选（历史） | 本地纯游戏分支codex/f1-art-metrics-game | f536871；Windows/Android共同源码，尚未创建发行标签或发布 |
| Build/Releases/v1.0.1等 | 本地构建产物目录 | 不是Git历史替代品；安装包另有摘要与内容清单 |
| 素材或生成目录中的_v1/_v2 | 资源迭代文件名 | 不等于Git标签或分支，不借此声明独立源码版本 |

游戏仓库：https://github.com/Creepercao/CatGame
测试证据仓库：https://github.com/Creepercao/CatGame-test-evidence

本次已使用git ls-remote --tags origin核对远程标签，且v1.0.1发行回执target_commitish与标签提交相同。后续Windows ZIP（含修改器）和Android APK在同一个Release中，使用同一源码标签；普通包构建日志与文件摘要、验收及安装说明共同留存。旧发行和TODO历史保留，不移动已发布标签来覆盖旧版本。测试源码/证据继续独立管理，不混入纯游戏仓库。

本地完整开发工程的提交还包含私有QA和工具，不与纯游戏提交号混用。发布时分别标注测试来源、游戏来源、产物摘要，不能因目录名叫v1就视作已通过验收或已发行。


## 发行完成追加

v1.1.0已发布：https://github.com/Creepercao/CatGame/releases/tag/v1.1.0 。正式标签与main对应1b82df94157bbcf370421207ed1d342ac272bb48；双平台受测普通包构建于f5368713d14df8aee0b1854f2520eb66e806663d，两提交仅README不同。Windows/Android与校验说明在同一个Release，GitHub服务器摘要已核对，旧四个发行继续保留。以上候选行是发行前的历史记录。

## 2026-11-18 S1追加

新增版本1.2.0/Android code7，纯游戏运行时d25e3c31415e69e9107544f6214244e6d086cc7a，main与新v1.2.0标签为5a0f4376bd04723a2b8e38cd42974dec23a2ebc4，差异仅README。Windows与Android同源；本地包位于Build/S1/distribution-v1.2.0，Windows ZIP含既有独立修改器。测试证据单独4acb215已推送，发行上传与服务器摘要核对中，完成后追加回执；不覆盖v1.1.0或更早标签。

完成追加：v1.2.0已公开，服务器四附件摘要匹配且latest正确，五个旧发行仍保留；发布回执在Build/S1/distribution-v1.2.0/release-verified.json。原“上传中”是历史状态，保留以追溯过程。
