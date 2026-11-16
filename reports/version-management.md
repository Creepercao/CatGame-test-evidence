# 版本、Git 与发行对应说明

核对日期：2026-11-16。正式游戏源码由Git管理；构建目录、素材文件名与Git标签是不同概念。

| 名称 | 实际机制 | 当前证据 |
| --- | --- | --- |
| v1.0.0 | 游戏仓库远程Git标签＋GitHub Release | baf37d1a554b60c7d2d401d2b686c60e3d5d1643 |
| v1.0.1 | 游戏仓库远程Git标签＋GitHub Release | cc926ecfa0c397017e0394f883e1520d985a66a0 |
| android-v1.0.0-preview.1 | 既有Android远程标签＋Release | 0076ec50b2e6c4a3168493aadb51fadf7577e588 |
| android-v1.0.2-preview.2 | 既有Android远程标签＋Release | bd85811ae749fdc5b9d574c8b3e640acdde66197 |
| 本次1.1.0候选 | 本地纯游戏分支codex/f1-art-metrics-game | f536871；Windows/Android共同源码，尚未创建发行标签或发布 |
| Build/Releases/v1.0.1等 | 本地构建产物目录 | 不是Git历史替代品；安装包另有摘要与内容清单 |
| 素材或生成目录中的_v1/_v2 | 资源迭代文件名 | 不等于Git标签或分支，不借此声明独立源码版本 |

游戏仓库：https://github.com/Creepercao/CatGame
测试证据仓库：https://github.com/Creepercao/CatGame-test-evidence

本次已使用git ls-remote --tags origin核对远程标签，且v1.0.1发行回执target_commitish与标签提交相同。后续Windows ZIP（含修改器）和Android APK在同一个Release中，使用同一源码标签；普通包构建日志与文件摘要、验收及安装说明共同留存。旧发行和TODO历史保留，不移动已发布标签来覆盖旧版本。测试源码/证据继续独立管理，不混入纯游戏仓库。

本地完整开发工程的提交还包含私有QA和工具，不与纯游戏提交号混用。发布时分别标注测试来源、游戏来源、产物摘要，不能因目录名叫v1就视作已通过验收或已发行。
