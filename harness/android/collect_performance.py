"""Collect Unity frame intervals and Android PSS from the dedicated Perf QA APK."""

import argparse
import csv
import hashlib
import io
import json
import math
import re
import subprocess
import time
from pathlib import Path
from uuid import uuid4


PACKAGE = "com.catgamestudio.catgame.perf"
ACTIVITY = f"{PACKAGE}/com.unity3d.player.UnityPlayerActivity"


def dump_json(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


class Device:
    def __init__(self, adb, port, serial):
        self.adb = [adb, "-P", str(port), "-s", serial]
        self.user = self.shell("am", "get-current-user").strip()
        if not self.user.isdecimal():
            raise RuntimeError(f"Unexpected Android user: {self.user!r}")

    def shell(self, *args):
        return subprocess.check_output([*self.adb, "shell", *args], encoding="utf-8", timeout=90)

    def run_as(self, *args):
        return self.shell("run-as", PACKAGE, "--user", self.user, *args)

    def stop(self):
        self.shell("am", "force-stop", "--user", self.user, PACKAGE)

    def pid(self):
        processes = self.shell("ps", "-A", "-o", "USER,PID,NAME")
        for line in processes.splitlines()[1:]:
            owner, pid, package = line.split(maxsplit=2)
            if owner.startswith(f"u{self.user}_") and package == PACKAGE:
                return pid
        raise RuntimeError(f"Perf QA process is absent for Android user {self.user}")

    def meminfo(self, pid, path):
        result = self.shell("dumpsys", "meminfo", pid)
        path.write_text(result, encoding="utf-8")
        value = re.search(r"TOTAL PSS:\s*(\d+)|^\s*TOTAL\s+(\d+)\s+", result, re.MULTILINE)
        if not value or int(value.group(1) or value.group(2)) <= 0:
            raise RuntimeError(f"Valid PSS unavailable. See {path}")
        return int(value.group(1) or value.group(2))

    def wait_report(self, remote, filename, timeout):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            try:
                raw = subprocess.check_output([*self.adb, "shell", "run-as", PACKAGE, "--user", self.user,
                                               "cat", f"{remote}/{filename}"], encoding="utf-8",
                                              stderr=subprocess.DEVNULL, timeout=10)
                return json.loads(raw)
            except subprocess.CalledProcessError:
                time.sleep(.6)
        raise TimeoutError(f"Missing Android QA artifact: {filename}")


def analyze(csv_text, result, mode, probe):
    expected = 1 if mode == "single" else 100
    seconds = 3 if probe else 60
    if (result["state"] != "complete" or result["mode"] != mode
            or result["visibleCats"] != expected or result["elapsedSeconds"] < seconds):
        raise RuntimeError(f"Incomplete or incorrect Android sample: {result}")
    rows = list(csv.DictReader(io.StringIO(csv_text)))
    frames = [int(row["frame"]) for row in rows]
    timestamps = [float(row["realtime"]) for row in rows]
    intervals = [float(row["frame_ms"]) for row in rows]
    if len(rows) != result["frameCount"] or len(rows) < 10:
        raise RuntimeError(f"Expected at least 10 samples matching the reported frame count; got {len(rows)} rows"
                           f" and {result['frameCount']} reported frames")
    if (any(not math.isfinite(value) or value <= 0 for value in timestamps + intervals)
            or any(b != a + 1 for a, b in zip(frames, frames[1:]))
            or any(b <= a for a, b in zip(timestamps, timestamps[1:]))):
        raise RuntimeError("Frame CSV failed continuity or finite-value checks")
    ordered = sorted(intervals)

    def percentile(p):
        return ordered[math.ceil(p * len(ordered)) - 1]

    over_budget = sum(value > 33.3 for value in ordered) / len(ordered)
    return {"p50Ms": percentile(.50), "p95Ms": percentile(.95), "p99Ms": percentile(.99),
            "over33_3msRatio": over_budget,
            "withinReferenceBudget": None if probe else percentile(.95) <= 20 and over_budget <= .01}


def collect(device, output, mode, probe):
    output.mkdir()
    run_id = uuid4().hex
    remote = f"files/AndroidPerf/{run_id}"
    device.stop()
    try:
        started = device.shell("am", "start", "-W", "--user", device.user, "-n", ACTIVITY,
                               "--es", "catgame_perf", mode, "--es", "catgame_perf_run", run_id,
                               "--ez", "catgame_perf_probe", str(probe).lower())
        (output / "launch.txt").write_text(started, encoding="utf-8")
        if "Status: ok" not in started:
            raise RuntimeError(started)
        warmup = device.wait_report(remote, "warmup.json", 35)
        dump_json(output / "warmup.json", warmup)
        pid = device.pid()
        before = device.meminfo(pid, output / "meminfo-before.txt")
        device.run_as("touch", f"{remote}/start")
        # Leave the device undisturbed through the measured frame interval.
        time.sleep(warmup["warmupSeconds"] + warmup["requestedSeconds"])
        result = device.wait_report(remote, "result.json", 20)
        dump_json(output / "result.json", result)
        if result["frameCount"]:
            csv_text = device.run_as("cat", f"{remote}/frames.csv")
            (output / "frames.csv").write_text(csv_text, encoding="utf-8")
        else:
            csv_text = ""
        if device.pid() != pid:
            raise RuntimeError("Android Perf QA process changed during sampling")
        after = device.meminfo(pid, output / "meminfo-after.txt")
        metrics = {"mode": mode, "probe": probe, "pid": int(pid), "frames": result["frameCount"],
                   "pssBeforeKiB": before, "pssAfterKiB": after, **analyze(csv_text, result, mode, probe)}
        dump_json(output / "metrics.json", metrics)
        print(json.dumps(metrics), flush=True)
    finally:
        device.stop()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", default="adb")
    parser.add_argument("--port", type=int, default=5037)
    parser.add_argument("--serial", required=True)
    parser.add_argument("--apk", type=Path, required=True, help="Local copy of the installed Perf QA APK")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--mode", choices=("single", "hundred", "both"), default="both")
    parser.add_argument("--probe", action="store_true", help="Collect three seconds per scene for pipeline validation")
    parser.add_argument("--repeats", type=int, choices=range(1, 6), default=1)
    args = parser.parse_args()

    device = Device(args.adb, args.port, args.serial)
    apk_path = next(line.removeprefix("package:") for line in device.shell("pm", "path", "--user", device.user, PACKAGE).splitlines()
                    if line.startswith("package:") and line.endswith("/base.apk"))
    with args.apk.open("rb") as package:
        expected_hash = hashlib.file_digest(package, "sha256").hexdigest()
    actual_hash = device.run_as("sha256sum", apk_path).split()[0]
    if actual_hash != expected_hash:
        raise RuntimeError("Installed Perf QA APK differs from the specified local artifact")
    args.output.mkdir(parents=True, exist_ok=False)
    dump_json(args.output / "device.json", {"serial": args.serial, "user": device.user,
              "model": device.shell("getprop", "ro.product.model").strip(),
              "android": device.shell("getprop", "ro.build.version.release").strip(),
              "apkSha256": actual_hash, "probe": args.probe})
    modes = ("single", "hundred") if args.mode == "both" else (args.mode,)
    for index in range(1, args.repeats + 1):
        for mode in modes:
            collect(device, args.output / f"{index:02d}-{mode}", mode, args.probe)


if __name__ == "__main__":
    main()
