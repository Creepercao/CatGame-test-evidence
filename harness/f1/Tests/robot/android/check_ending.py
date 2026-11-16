"""Exercise the release APK ending through Appium on the isolated API 34 AVD."""

import argparse
import json
import re
import subprocess
import time
from pathlib import Path
from xml.etree import ElementTree

from AndroidGame import AndroidGame


ROCKET_PRICE = 1_000_000_000_000
CORAL = (232, 138, 106)
PANEL = (239, 220, 198)


def settle_android_overlay(game):
    for index in range(8):
        frame = game._screenshot()
        if frame.size == (1600, 900) and game._error(frame, "menu") <= 2.5:
            return
        root = ElementTree.fromstring(game.capture_ui_hierarchy(f"system-overlay-{index}.xml"))
        for node in root.iter():
            if node.get("package") in ("android", "com.android.systemui") and node.get("text") in ("Wait", "Got it"):
                left, top, right, bottom = map(int, re.findall(r"\d+", node.attrib["bounds"]))
                game.tap((left + right) // 2, (top + bottom) // 2)
                break
        time.sleep(1)


def wait_for_colors(game, width, name, samples):
    end = time.monotonic() + 20
    while True:
        frame = game._screenshot()
        if frame.size == (width, 900) and all(
            max(abs(a - b) for a, b in zip(frame.getpixel(point), color)) <= 8 for point, color in samples
        ):
            frame.save(game.output_dir / name)
            return
        if time.monotonic() >= end:
            frame.save(game.output_dir / name)
            raise AssertionError(f"Expected screen colors did not appear in {name}")
        time.sleep(.4)


def run(game, fixture):
    assert game._shell("getprop", "ro.product.model") == "sdk_gphone64_x86_64"
    primary = f"/data/media/{game.user}/Android/data/{game.PACKAGE}/files/CatGame/save.json"

    def state():
        return json.loads(game._shell("su", "0", "cat", primary))

    def write_fixture():
        game.force_stop_cat_game()
        subprocess.run([*game.adb, "su", "0", "tee", primary], input=json.dumps(fixture).encode(),
                       check=True, capture_output=True, timeout=30)
        assert state()["catHair"] == ROCKET_PRICE + 500

    game.open_appium()
    try:
        game.start_cat_game()
        settle_android_overlay(game)
        game.wait_for_visual("menu", "initial-menu.png")
        # A newly installed APK creates its owned save directory through the regular new-game path.
        if subprocess.run([*game.adb, "su", "0", "test", "-f", primary], timeout=30).returncode:
            game.tap(1320, 457)
            game.wait_for_visual("intro", "initial-save-created.png")
        results = []
        for width, action in ((2000, "continue"), (1200, "menu")):
            write_fixture()
            game.set_display_width(width)
            game.start_cat_game()
            center = width // 2
            menu_x = round(width * .825)
            game.wait_for_visual("menu", f"{width}-menu.png", width=width, offset_x=menu_x - 1320)
            game.tap(menu_x, 372)
            game.wait_for_visual("two-cats", f"{width}-home.png", width=width)
            game.tap(300, 183)
            time.sleep(.6)
            game.capture(f"{width}-rocket-card.png")
            game.tap(185, 680)
            wait_for_colors(game, width, f"{width}-confirmation.png", [((center + 196, 560), CORAL)])
            if action == "continue":
                game.tap(center - 133, 560)
                game.wait_for_visual("two-cats", f"{width}-cancelled.png", width=width)
                assert not state()["rocketPurchased"]
                game.tap(185, 680)
                wait_for_colors(game, width, f"{width}-reopened.png", [((center + 196, 560), CORAL)])
            game.tap(center + 133, 560)
            wait_for_colors(game, width, f"{width}-ending-cg.png", [((center - 112, 860), CORAL)])
            purchased = state()
            assert purchased["rocketPurchased"] and purchased["hasEndingSnapshot"] and not purchased["endingCompleted"]
            assert 0 <= purchased["catHair"] < 1000 and purchased["firstEnding"]["totalCats"] == 2
            game.tap(50, 50)  # Outside the visible CTA.
            wait_for_colors(game, width, f"{width}-statistics.png", [((center - 235, 723), CORAL), ((center + 235, 723), PANEL)])
            if action == "continue":
                game.tap(center - 152, 723)
                game.wait_for_visual("two-cats", f"{width}-resumed.png", width=width)
            else:
                game.tap(center + 152, 723)
                game.wait_for_visual("menu", f"{width}-returned.png", width=width, offset_x=menu_x - 1320)
            after = state()
            assert after["endingCompleted"] and after["firstEnding"] == purchased["firstEnding"]
            results.append({"width": width, "height": 900, "action": action, "completed": True,
                            "firstEnding": after["firstEnding"]})
            print(f"PASS {width}x900: rocket confirmation, ending CG, statistics, {action}", flush=True)
        (game.output_dir / "ending-result.json").write_text(json.dumps(results, indent=2) + "\n", encoding="utf-8")
    finally:
        game.restore_display_size()
        game.close_appium()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--adb", required=True)
    parser.add_argument("--user", required=True)
    parser.add_argument("--fixture", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    fixture = json.loads(args.fixture.read_text(encoding="utf-8"))
    assert fixture["introCompleted"] and not fixture["rocketPurchased"] and sum(cat["count"] for cat in fixture["cats"]) == 2
    fixture.update(catHair=ROCKET_PRICE + 500, lastSeenUnix=0)
    args.output.mkdir(parents=True, exist_ok=False)
    run(AndroidGame("http://127.0.0.1:4723", args.adb, 5037, "emulator-5564", args.user, args.output), fixture)
