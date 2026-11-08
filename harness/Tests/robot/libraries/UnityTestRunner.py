"""
Robot Framework 库：在 Unity 内部驱动 PlayMode / EditMode 测试。

为什么不是 Appium / WinAppDriver：
Unity 玩家窗口是 Direct3D 自绘的，不暴露 Windows UIAutomation 控件树，
外部框架只能看到一个窗口，点不到里面的按钮。真正的交互验证必须在
Unity 进程内部完成（EventSystem / 逻辑入口），所以这里包的是
Unity Test Framework 的批处理运行器。

这个库做的事：
  1. 用 -batchmode -runTests 跑测试套件（不弹窗口、不抢焦点、不占鼠标）
  2. 解析 NUnit XML 结果
  3. 把统计和失败明细交给 Robot 断言
"""

import os
import subprocess
import xml.etree.ElementTree as ET


class UnityTestRunner:
    """ROBOT LIBRARY"""

    ROBOT_LIBRARY_SCOPE = "SUITE"

    def __init__(self, unity_exe, project_path, logs_dir=None, timeout_seconds=1800):
        self.unity_exe = unity_exe
        self.project_path = os.path.abspath(project_path)
        self.logs_dir = logs_dir or os.path.join(self.project_path, "Logs")
        self.timeout_seconds = int(timeout_seconds)
        self._results = {}      # platform -> 解析后的结果字典
        self._last_platform = None

    # ------------------------------------------------------------------
    # 测试执行
    # ------------------------------------------------------------------
    def run_tests(self, test_platform="PlayMode", test_filter=None):
        """跑指定平台的测试套件，返回统计字典。"""
        lock_file = os.path.join(self.project_path, "Temp", "UnityLockfile")
        if os.path.exists(lock_file):
            raise AssertionError(
                "Unity project lock exists at %s; close the Editor manually before batch tests."
                % lock_file
            )
        os.makedirs(self.logs_dir, exist_ok=True)
        results_file = os.path.join(self.logs_dir, "test-results-%s.xml" % test_platform)
        log_file = os.path.join(self.logs_dir, "test-%s.log" % test_platform)

        if os.path.exists(results_file):
            os.remove(results_file)

        cmd = [
            self.unity_exe,
            "-batchmode",
            "-projectPath", self.project_path,
            "-runTests",
            "-testPlatform", test_platform,
            "-testResults", results_file,
            "-logFile", log_file,
        ]
        if test_filter:
            cmd += ["-testFilter", test_filter]

        exit_code = 0
        try:
            proc = subprocess.run(
                cmd, cwd=self.project_path, capture_output=True, text=True,
                timeout=self.timeout_seconds,
            )
            exit_code = proc.returncode
        except subprocess.TimeoutExpired:
            raise AssertionError(
                "Unity 测试运行超过 %d 秒未结束，日志见 %s" % (self.timeout_seconds, log_file)
            )

        if not os.path.exists(results_file):
            raise AssertionError(
                "Unity没有生成测试结果；须根据日志区分许可、编译或执行故障。exit=%s\n日志尾部:\n%s"
                % (exit_code, self._log_tail(log_file))
            )

        parsed = self._parse_results(results_file)
        if exit_code != 0:
            raise AssertionError(
                "Unity exited with code %s despite producing XML; result=%s. Log: %s"
                % (exit_code, parsed['result'], log_file)
            )
        parsed["exit_code"] = exit_code
        parsed["platform"] = test_platform
        self._results[test_platform] = parsed
        self._last_platform = test_platform
        return parsed

    def run_playmode_tests(self):
        return self.run_tests("PlayMode")

    def run_editmode_tests(self):
        return self.run_tests("EditMode")

    # ------------------------------------------------------------------
    # 结果解析
    # ------------------------------------------------------------------
    def _parse_results(self, results_file):
        root = ET.parse(results_file).getroot()

        def attr(node, key, default=""):
            return node.attrib.get(key, default)

        cases, failed = [], []
        for case in root.iter("test-case"):
            result = attr(case, "result")
            cases.append({
                "name": attr(case, "name"),
                "result": result,
                "duration": attr(case, "duration"),
            })
            if result == "Failed":
                msg = ""
                failure = case.find("failure")
                if failure is not None:
                    m = failure.find("message")
                    if m is not None and m.text:
                        msg = " ".join(m.text.split())
                failed.append({"name": attr(case, "name"), "message": msg})

        return {
            "total": int(attr(root, "total", "0")),
            "passed": int(attr(root, "passed", "0")),
            "failed": int(attr(root, "failed", "0")),
            "skipped": int(attr(root, "skipped", "0")),
            "duration": float(attr(root, "duration", "0") or 0),
            "result": attr(root, "result"),
            "cases": cases,
            "failed_cases": failed,
        }

    # ------------------------------------------------------------------
    # 供 Robot 断言的便捷关键字
    # ------------------------------------------------------------------
    def get_test_case_names(self, test_platform="PlayMode"):
        self._ensure_ran(test_platform)
        return [c["name"] for c in self._results[test_platform]["cases"]]

    def get_failed_test_names(self, test_platform="PlayMode"):
        self._ensure_ran(test_platform)
        return [c["name"] for c in self._results[test_platform]["failed_cases"]]

    def get_failure_details(self, test_platform="PlayMode"):
        self._ensure_ran(test_platform)
        failed = self._results[test_platform]["failed_cases"]
        if not failed:
            return "无失败用例"
        return "\n".join("· %s：%s" % (c["name"], c["message"]) for c in failed)

    def _ensure_ran(self, test_platform):
        if test_platform not in self._results:
            raise AssertionError("还没有运行 %s 套件，请先调用 Run Tests" % test_platform)

    @staticmethod
    def _log_tail(log_file, lines=40):
        if not os.path.exists(log_file):
            return "(没有日志)"
        with open(log_file, "r", encoding="utf-8", errors="replace") as f:
            content = f.read().splitlines()
        return "\n".join(content[-lines:])
