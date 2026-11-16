"""Trace native Appium input, Unity acknowledgement and disk state in an isolated AVD APK."""
import argparse
import json
import subprocess
import time
import uuid
from datetime import datetime, timezone
from pathlib import Path

from AndroidGame import AndroidGame
from check_ending import settle_android_overlay


def run(game, fixture_path, fps_values, repeat, home_policy):
    assert game.serial == "emulator-5564" and game.user != "0"
    assert game.PACKAGE == "com.catgamestudio.catgame.inputqa"
    assert game._shell("getprop", "ro.product.model") == "sdk_gphone64_x86_64"
    primary = f"/data/media/{game.user}/Android/data/{game.PACKAGE}/files/CatGame/save.json"
    internal = f"/data/user/{game.user}/{game.PACKAGE}/files/PurchaseTrace"
    fixture = json.loads(fixture_path.read_text(encoding="utf-8-sig"))
    assert fixture["introCompleted"] and fixture["cats"][0] == {"id": "Milk", "count": 1}
    assert fixture["catHair"] >= 26
    fixture["lastSeenUnix"] = 0
    results = []

    def read(name):
        raw = game._shell("su", "0", "cat", primary)
        (game.output_dir / name).write_text(raw, encoding="utf-8")
        return json.loads(raw)

    def launch(run_id, fps):
        result = game._shell("am", "start", "-W", "--user", game.user,
                             "-n", f"{game.PACKAGE}/{game.ACTIVITY}",
                             "--es", "catgame_input_run", run_id, "--ei", "catgame_input_fps", str(fps))
        assert "Status: ok" in result, result

    def events(run_id):
        raw = game._shell("su", "0", "cat", f"{internal}/{run_id}/events.jsonl")
        return raw, [json.loads(line) for line in raw.splitlines() if line]

    game.open_appium()
    try:
        game.start_cat_game()
        settle_android_overlay(game)
        game.wait_for_visual("menu", "initial-menu.png", timeout=90)
        if subprocess.run([*game.adb, "su", "0", "test", "-f", primary], timeout=30).returncode:
            game.tap(1320, 457)
            game.wait_for_visual("intro", "initial-save-created.png", timeout=90)
        for fps in fps_values:
            for hold in (0, 350):
                for home in (False, True):
                    for iteration in range(repeat):
                        number = len(results) + 1
                        run_id = uuid.uuid4().hex
                        prefix = f"{number:02d}"
                        game.force_stop_cat_game()
                        subprocess.run([*game.adb, "su", "0", "tee", primary],
                                       input=json.dumps(fixture).encode(), check=True, capture_output=True, timeout=30)
                        before = read(f"{prefix}-before.json")
                        launch(run_id, fps)
                        game.wait_for_visual("menu", f"{prefix}-menu.png", timeout=90)
                        game.tap(1320, 372)
                        game.wait_for_visual("one-cat", f"{prefix}-ready.png", timeout=90)
                        deadline = time.monotonic() + 20
                        while True:
                            raw, rows = events(run_id)
                            frames = [row for row in rows if row["kind"] == "frame"]
                            if len(frames) >= 4:
                                break
                            assert time.monotonic() < deadline, "Trace did not settle"
                            time.sleep(.5)
                        assert rows[-1]["milk"] == 1 and rows[-1]["interactable"], rows[-1]
                        game.capture(f"{prefix}-affordable.png")
                        input_utc = datetime.now(timezone.utc).isoformat()
                        pid = game.cat_game_pid()
                        started = time.monotonic()
                        if hold:
                            game._command("/actions", {"actions": [{"type": "pointer", "id": "purchase",
                                "parameters": {"pointerType": "touch"}, "actions": [
                                {"type": "pointerMove", "duration": 0, "x": 185, "y": 300},
                                {"type": "pointerDown", "button": 0}, {"type": "pause", "duration": hold},
                                {"type": "pointerUp", "button": 0}]}]})
                        else:
                            game.tap(185, 300)
                        input_done = time.monotonic()
                        if home:
                            if home_policy == "confirmed":
                                game.wait_for_visual("two-cats", f"{prefix}-purchase-acknowledged.png", timeout=2)
                            game.press_key(3)
                            home_elapsed = time.monotonic() - started
                            deadline = time.monotonic() + 5
                            while True:
                                try:
                                    game.cat_game_is_backgrounded()
                                    break
                                except AssertionError:
                                    assert time.monotonic() < deadline, "Home did not leave the game"
                                    time.sleep(.2)
                        else:
                            time.sleep(12)
                            game.capture(f"{prefix}-after-input.png")
                        game.force_stop_cat_game()
                        stopped_after_seconds = time.monotonic() - started
                        stopped = read(f"{prefix}-stopped.json")
                        raw, rows = events(run_id)
                        (game.output_dir / f"{prefix}-events.jsonl").write_text(raw, encoding="utf-8")
                        game.start_cat_game()
                        game.wait_for_visual("menu", f"{prefix}-restarted.png", timeout=90)
                        restored = read(f"{prefix}-restored.json")
                        acknowledgements = [row for row in rows if row["kind"] == "after-button-callback"]
                        accepted = any(row["milk"] == 2 for row in acknowledgements)
                        row = {"run": run_id, "fps": fps, "holdMs": hold, "home": home,
                               "homePolicy": home_policy, "homeAfterSeconds": home_elapsed if home else None,
                               "inputUtc": input_utc, "pid": pid, "restartedPid": game.cat_game_pid(),
                               "stoppedAfterSeconds": stopped_after_seconds,
                               "iteration": iteration, "inputSeconds": input_done - started,
                               "beforeCount": before["cats"][0]["count"],
                               "stoppedCount": stopped["cats"][0]["count"],
                               "restoredCount": restored["cats"][0]["count"],
                               "down": sum(e["kind"] == "pointer-down" for e in rows),
                               "up": sum(e["kind"] == "pointer-up" for e in rows),
                               "callbacks": len(acknowledgements), "accepted": accepted,
                               "lostAcceptedPurchase": accepted and restored["cats"][0]["count"] != 2,
                               "preInputFrameMs": [e["frameMs"] for e in frames[-3:]]}
                        row["timingBudgetPassed"] = not (home and home_policy == "confirmed") or home_elapsed < 3
                        results.append(row)
                        (game.output_dir / "results.json").write_text(json.dumps(results, indent=2), encoding="utf-8")
                        print(json.dumps(row), flush=True)
        assert not any(row["lostAcceptedPurchase"] for row in results), "Accepted purchase was not durable"
        assert all(row["timingBudgetPassed"] for row in results), "Acknowledged purchase to Home exceeded original 3s budget; see results.json"
    finally:
        game.force_stop_cat_game()
        game.close_appium()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", required=True)
    parser.add_argument("--user", required=True)
    parser.add_argument("--fixture", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--fps", type=int, nargs="+", choices=(6, 60), default=[60, 6])
    parser.add_argument("--repeat", type=int, choices=range(1, 21), default=1)
    parser.add_argument("--home-policy", choices=("raw", "confirmed"), default="raw")
    parser.add_argument("--disable-window-animation", choices=("true", "false"), default="false")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    game = AndroidGame("http://127.0.0.1:4723", args.adb, 5037, "emulator-5564", args.user, args.output)
    game.PACKAGE = "com.catgamestudio.catgame.inputqa"
    game.disable_window_animation = args.disable_window_animation == "true"
    run(game, args.fixture, args.fps, args.repeat, args.home_policy)
