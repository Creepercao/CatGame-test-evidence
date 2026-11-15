"""Upgrade an actual previous-release QA save without injecting or editing progress."""
import argparse
import hashlib
import json
import re
import subprocess
import time
from pathlib import Path

from AndroidGame import AndroidGame
from check_ending import settle_android_overlay


def run(adb, output, apk):
    output.mkdir(parents=True, exist_ok=False)
    game = AndroidGame("http://127.0.0.1:4723", adb, 5037, "emulator-5564", "10", output, True)
    assert game._shell("getprop", "ro.product.model") == "sdk_gphone64_x86_64"
    assert game._shell("am", "get-current-user") == "10"
    path = f"/data/media/10/Android/data/{game.PACKAGE}/files/CatGame/save.json"

    def save(name):
        raw = subprocess.check_output([*game.adb, "su", "0", "cat", path], timeout=30)
        (output / name).write_bytes(raw)
        return raw, json.loads(raw)

    game.force_stop_cat_game()
    old_version = re.search(r"versionCode=(\d+)", game._shell("dumpsys", "package", game.PACKAGE)).group(1)
    assert old_version == "4", old_version
    old_raw, before = save("old-save.json")
    assert before["cats"][0] == {"id": "Milk", "count": 2}
    executable = [adb, "-P", "5037", "-s", "emulator-5564"]
    result = subprocess.run([*executable, "install", "--user", "10", "-r", str(apk.resolve())],
                            capture_output=True, timeout=180)
    (output / "install.txt").write_bytes(result.stdout + result.stderr)
    assert result.returncode == 0 and b"Success" in result.stdout
    installed_raw, _ = save("upgraded-before-launch.json")
    assert installed_raw == old_raw, "Upgrade changed save bytes before launch"
    new_version = re.search(r"versionCode=(\d+)", game._shell("dumpsys", "package", game.PACKAGE)).group(1)
    assert new_version == "5", new_version
    game.open_appium()
    try:
        game.start_cat_game()
        settle_android_overlay(game)
        game.wait_for_visual("menu", "01-upgraded-menu.png", timeout=90)
        game.tap(1320, 372)
        game.wait_for_visual("offline", "02-offline.png", timeout=60)
        game.tap(800, 519)
        game.wait_for_visual("two-cats", "03-retained-cats.png", timeout=60)
        time.sleep(11)
        game.force_stop_cat_game()
        _, after = save("upgraded-after-play.json")
        exact = ["version", "cats", "items", "clicks", "introCompleted", "rocketPurchased",
                 "endingCompleted", "hasEndingSnapshot", "firstEnding", "prestigeLevel", "prestigeMultiplier"]
        for key in exact:
            assert before[key] == after[key], key
        for key in ["achievements", "seenStoryEvents"]:
            assert set(before[key]) <= set(after[key]), key
        for key in ["catHair", "totalEarned", "playSeconds"]:
            assert after[key] >= before[key], key
        record = {"passed": True, "oldVersionCode": old_version, "newVersionCode": new_version,
                  "apkSha256": hashlib.sha256(apk.read_bytes()).hexdigest(),
                  "saveBytesUnchangedDuringInstall": True, "saveBeforeSha256": hashlib.sha256(old_raw).hexdigest(),
                  "comparedFields": exact, "restoredMilk": after["cats"][0]["count"],
                  "saveWasInjected": False, "ordinaryProductionApk": True}
        (output / "result.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
        print(json.dumps(record), flush=True)
    finally:
        game.force_stop_cat_game()
        game.close_appium()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", required=True)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--apk", required=True, type=Path)
    args = parser.parse_args()
    run(args.adb, args.output, args.apk)
