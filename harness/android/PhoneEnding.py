"""Ending UI acceptance uses test fixtures only in the dedicated Perf QA package."""

import json
import subprocess
import time
from PhoneGame import PhoneGame


class PhoneEnding(PhoneGame):
    PACKAGE = "com.catgamestudio.catgame.perf"

    def wait_colors(self, name, samples):
        deadline = time.monotonic() + 20
        while True:
            frame = self._screenshot()
            if frame.size == (2712, 1220) and all(
                    max(abs(a - b) for a, b in zip(frame.getpixel(self.point("center", x, y)), color)) <= 8
                    for x, y, color in samples):
                frame.save(self.output_dir / name)
                return
            if time.monotonic() >= deadline:
                frame.save(self.output_dir / name)
                raise AssertionError(f"Expected ending UI colors absent: {name}")
            time.sleep(.3)

    def check_ending_branch(self, branch):
        assert branch in ("continue", "menu")
        assert self.PACKAGE == "com.catgamestudio.catgame.perf"
        self.force_stop_cat_game()
        state = self.saved_state(f"{branch}-original-qa.json")
        for index, cat in enumerate(state["cats"]):
            cat["count"] = 1 if index == 0 else 0
        state.update(catHair=1_000_000_000_500, lastSeenUnix=0, introCompleted=True,
                     pendingStoryEvents=[], rocketPurchased=False, endingCompleted=False, hasEndingSnapshot=False)
        path = f"/sdcard/Android/data/{self.PACKAGE}/files/CatGame/save.json"
        subprocess.run([*self.adb, "tee", path], input=json.dumps(state).encode(),
                       check=True, capture_output=True, timeout=30)
        self.start_cat_game()
        self.wait_for_visual("menu", f"{branch}-menu.png")
        self.tap_ui("menu", 1320, 372)
        self.wait_for_visual("one-cat", f"{branch}-home.png")
        self.tap_ui("left", 300, 183)
        time.sleep(.6)
        self.capture(f"{branch}-rocket.png")
        self.tap_ui("left", 185, 680)
        coral = (232, 138, 106)
        panel = (239, 220, 198)
        self.wait_colors(f"{branch}-confirmation.png", [(996, 560, coral)])
        if branch == "continue":
            self.tap_ui("center", 667, 560)
            self.wait_for_visual("one-cat", f"{branch}-cancelled.png")
            assert not self.saved_state(f"{branch}-cancelled.json")["rocketPurchased"]
            self.tap_ui("left", 185, 680)
            self.wait_colors(f"{branch}-reopened.png", [(996, 560, coral)])
        self.tap_ui("center", 933, 560)
        self.wait_colors(f"{branch}-cg.png", [(688, 860, coral)])
        purchased = self.saved_state(f"{branch}-purchased.json")
        assert purchased["rocketPurchased"] and not purchased["endingCompleted"] and purchased["hasEndingSnapshot"]
        assert 0 <= purchased["catHair"] < 1000
        assert purchased["firstEnding"]["totalCats"] == 1
        self.tap_ui("left", 50, 50)
        self.wait_colors(f"{branch}-statistics.png", [(565, 723, coral), (1035, 723, panel)])
        self.tap_ui("center", 648 if branch == "continue" else 952, 723)
        self.wait_for_visual("one-cat" if branch == "continue" else "menu", f"{branch}-returned.png")
        after = self.saved_state(f"{branch}-completed.json")
        assert after["endingCompleted"] and after["firstEnding"] == purchased["firstEnding"]
