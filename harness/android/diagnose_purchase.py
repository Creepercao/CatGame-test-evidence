"""Compare purchase gestures and Home interruption in a named AVD QA user."""

import argparse
import json
import subprocess
import time
from pathlib import Path

from AndroidGame import AndroidGame
from check_ending import settle_android_overlay


def run(game, fixture, immediate_stop):
    assert game.serial == "emulator-5564"
    assert game._shell("getprop", "ro.product.model") == "sdk_gphone64_x86_64"
    assert game.user != "0"
    assert game._shell("settings", "--user", game.user, "get", "secure", "user_setup_complete") == "1"
    primary = f"/data/media/{game.user}/Android/data/{game.PACKAGE}/files/CatGame/save.json"
    fixture = json.loads(fixture.read_text(encoding="utf-8-sig"))
    assert fixture["introCompleted"] and fixture["cats"][0] == {"id": "Milk", "count": 1}
    assert fixture["catHair"] >= 26
    fixture["lastSeenUnix"] = 0
    results = []

    def read_state(name):
        raw = game._shell("su", "0", "cat", primary)
        (game.output_dir / name).write_text(raw, encoding="utf-8")
        return json.loads(raw)

    game.open_appium()
    try:
        game.start_cat_game()
        settle_android_overlay(game)
        game.wait_for_visual("menu", "initial-menu.png", timeout=60)
        if subprocess.run([*game.adb, "su", "0", "test", "-f", primary], timeout=30).returncode:
            game.tap(1320, 457)
            game.wait_for_visual("intro", "initial-save-created.png", timeout=90)
        cases = [(0, True), (0, True), (350, True), (350, True)] if immediate_stop else [
            (0, False), (0, True), (350, False), (350, True)]
        for index, (hold_ms, home) in enumerate(cases, 1):
            game.force_stop_cat_game()
            # The fixture is a previously captured QA save, never a player's save.
            subprocess.run([*game.adb, "su", "0", "tee", primary],
                           input=json.dumps(fixture).encode(), check=True, capture_output=True, timeout=30)
            before = read_state(f"{index}-before.json")
            game.start_cat_game()
            settle_android_overlay(game)
            game.wait_for_visual("menu", f"{index}-menu.png", timeout=60)
            game.tap(1320, 372)
            game.wait_for_visual("one-cat", f"{index}-ready.png", timeout=60)
            pid = game.cat_game_pid()
            started = time.monotonic()
            if hold_ms:
                game._command("/actions", {"actions": [{
                    "type": "pointer", "id": "purchase", "parameters": {"pointerType": "touch"},
                    "actions": [{"type": "pointerMove", "duration": 0, "x": 185, "y": 300},
                                {"type": "pointerDown", "button": 0},
                                {"type": "pause", "duration": hold_ms},
                                {"type": "pointerUp", "button": 0}]}]})
            else:
                game.tap(185, 300)
            if home:
                game.press_key(3)
            elapsed = time.monotonic() - started
            after = None
            if immediate_stop:
                deadline = time.monotonic() + 5
                while True:
                    try:
                        game.cat_game_is_backgrounded()
                        break
                    except AssertionError:
                        if time.monotonic() >= deadline:
                            raise
                        time.sleep(.3)
            else:
                time.sleep(15)
                game.capture(f"{index}-after-input.png")
                after = read_state(f"{index}-after-input.json")
            game.force_stop_cat_game()
            stopped = read_state(f"{index}-stopped.json")
            game.start_cat_game()
            game.wait_for_visual("menu", f"{index}-restarted.png", timeout=60)
            resumed = read_state(f"{index}-restarted.json")
            row = {"holdMs": hold_ms, "home": home, "immediateStop": immediate_stop,
                   "inputSeconds": elapsed, "pid": pid,
                   "restartedPid": game.cat_game_pid(), "beforeCount": before["cats"][0]["count"],
                   "afterCount": after["cats"][0]["count"] if after else None,
                   "stoppedCount": stopped["cats"][0]["count"],
                   "restartedCount": resumed["cats"][0]["count"]}
            results.append(row)
            (game.output_dir / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
            print(json.dumps(row), flush=True)
    finally:
        game.force_stop_cat_game()
        game.close_appium()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", required=True)
    parser.add_argument("--user", required=True)
    parser.add_argument("--fixture", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--immediate-stop", action="store_true")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    run(AndroidGame("http://127.0.0.1:4723", args.adb, 5037, "emulator-5564", args.user, args.output),
        args.fixture, args.immediate_stop)
