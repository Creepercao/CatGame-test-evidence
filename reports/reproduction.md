# 阅读与复现

## 可立即运行

仓库根目录执行`python verify_evidence.py`。只读现有公开记录，输出核验结果，不生成新的游戏通过结论。

## 真实游戏测试的准备

源码快照的原环境为Windows11、Unity2022.3.21f1、Python3.12。`harness/`各路径原本位于完整Unity工程根目录；单独在本仓库执行run_suite.py不会自动获得游戏。

1. 单独复制游戏公开仓库作为实验工程，保留原存档及发行包。
2. 审阅后将harness中的Assets/Tests、Player*QA、Tests/robot和tools/qa按路径合入实验工程。CatGameSetup.cs含QA构建入口，会替换公开游戏中的精简版本，必须检查差异。
3. 在实验工程Packages声明`com.unity.test-framework` 1.1.33，安装Robot依赖并配置Unity路径。测试源码引用的Editor修改器等历史依赖可能还需从原完整工程补齐；当前快照未保证与公开游戏直接叠加即全量编译。
4. 先编译、再小范围验证，再运行完整测试；每次输出到新的本地目录，不覆盖本仓库历史证据。QA测试需重建Development Player，不把公开正式包当成带测试入口的包。
5. Jenkins需自行安装、配置环境与任务。job_pipeline.py供审阅编排；仓库不提供原本地Jenkins实例、账号或密钥，也不声称已在另一台机器复现成功。

建议先阅读business.robot→GameBusiness.py→PlayerBusinessQA.cs，再看保存重启JSONL；性能阅读performance.robot→run_performance.py→PlayerPerformanceQA.cs→performance_stats.py。历史记录与未来复跑分开保存和比较。
