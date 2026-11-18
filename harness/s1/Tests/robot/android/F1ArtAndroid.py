"""F1 AVD-only observations and Appium touches; never edits a game save."""
import json
import hashlib
import re
import subprocess
import time
from pathlib import Path
from datetime import datetime, timezone
from xml.etree import ElementTree

import cv2
import numpy as np
from PIL import Image, ImageChops, ImageStat
from AndroidGame import AndroidGame
from robot.api import logger


class F1ArtAndroid(AndroidGame):
    def _guard(self):
        assert self.serial == "emulator-5564" and self.user != "0"
        assert self._shell("getprop", "ro.product.model") == "sdk_gphone64_x86_64"
        assert self._shell("am", "get-current-user") == self.user

    def _record(self, action, data):
        with (self.output_dir / "steps.jsonl").open("a", encoding="utf-8") as output:
            output.write(json.dumps({"utc": datetime.now(timezone.utc).isoformat(), "action": action,
                                     "data": data}, ensure_ascii=False) + "\n")

    def dismiss_one_avd_system_ui_anr(self, reference):
        self._guard()
        source = self.capture_ui_hierarchy("system-ui-before.xml")
        self.capture("system-ui-before.png")
        nodes = list(ElementTree.fromstring(source).iter("node"))
        visible_title = any(node.get("text") == "System UI isn't responding" for node in nodes)
        buttons = [node for node in nodes if node.get("text") == "Wait"]
        dialog = (324, 224, 1276, 675)
        with Image.open(reference) as image:
            known = image.convert("RGB").crop(dialog)
        current = self._screenshot().convert("RGB")
        error = sum(ImageStat.Stat(ImageChops.difference(current.crop(dialog), known)).mean) / 3
        assert error < 1, "Screenshot must match the explicitly reviewed System UI ANR; refuse unknown failures"
        if visible_title:
            assert len(buttons) == 1
            box = list(map(int, re.findall(r"\d+", buttons[0].get("bounds"))))
            assert len(box) == 4
            locator = "native-node"
        else:
            # Secondary-user UiAutomator may omit a system-user overlay, though screenshots include it.
            box = [324, 524, 1276, 675]
            locator = "reviewed-system-ui-reference-image"
        self._record("system-ui-anr-wait-once", {"bounds": box, "locator": locator, "referenceError": error})
        self.tap((box[0] + box[2]) // 2, (box[1] + box[3]) // 2)
        time.sleep(1)
        after = self.capture_ui_hierarchy("system-ui-after.xml")
        self.capture("system-ui-after.png")
        updated = self._screenshot().convert("RGB").crop(dialog)
        assert sum(ImageStat.Stat(ImageChops.difference(updated, known)).mean) / 3 > 2.5, \
            "Observed system dialog did not disappear after the single Wait"

    def _saved_state(self, name):
        self._guard()
        path = f"/data/media/{self.user}/Android/data/{self.PACKAGE}/files/CatGame/save.json"
        raw = self._shell("su", "0", "cat", path)
        state = json.loads(raw)
        (self.output_dir / name).write_text(raw, encoding="utf-8")
        return state

    def _home_and_wait(self):
        self.press_key(3)
        end = time.monotonic() + 5
        while True:
            try:
                self.cat_game_is_backgrounded()
                return
            except AssertionError:
                if time.monotonic() >= end:
                    raise
                time.sleep(.2)

    def _dismiss_known_cg(self, frame, tag):
        offset = (frame.width - 1600) // 2
        if self._error(frame, "story-continue", offset) > 2.5:
            return False
        number = getattr(self, "_f1_cg_dismissed", 0) + 1
        assert number <= 4, "Unexpected number of CGs; stop instead of blindly clicking"
        self._f1_cg_dismissed = number
        name = tag + f"-cg-{number}"
        frame.save(self.output_dir / (name + ".png"))
        before = self._saved_state(name + "-save-before.json")
        # Exercise the promised any-image click, away from the orange continue button.
        point = [int(frame.width * .75), 100]
        self._record("dismiss-recognized-cg-image", {"point": point, "clicksBefore": before["clicks"]})
        self.tap(*point)
        time.sleep(.25)
        self.capture(name + "-after.png")
        self._home_and_wait()
        after = self._saved_state(name + "-save-after.json")
        assert after["clicks"] == before["clicks"], "CG dismissal must not click through into a cat"
        self.start_cat_game()
        return True

    def wait_for_two_cats_and_dismiss_pending_cg(self, width, tag):
        end = time.monotonic() + 30
        while True:
            frame = self._screenshot().convert("RGB")
            if frame.size != (int(width), 900):
                self._record("resume-viewport-transient", {"actual": frame.size, "expected": [int(width), 900]})
                if time.monotonic() >= end:
                    frame.save(self.output_dir / (tag + "-wrong-viewport.png"))
                    raise AssertionError(f"Viewport did not settle to {width}x900; actual={frame.size}")
                time.sleep(.4)
                continue
            if self._dismiss_known_cg(frame, tag):
                continue
            if self._error(frame, "two-cats") <= 2.5:
                frame.save(self.output_dir / (tag + "-home.png"))
                return
            if time.monotonic() >= end:
                frame.save(self.output_dir / (tag + "-home-not-found.png"))
                raise AssertionError("Two-cat HUD was not visible after handling only recognized story CGs")
            time.sleep(.4)

    def upgrade_normal_apk_without_changing_save(self, apk):
        self._guard()
        apk = Path(apk)
        apk_hash = hashlib.sha256(apk.read_bytes()).hexdigest()
        assert apk_hash == "8e32a1f2b1192ac7fba2bb15bf904feaf8015b4cf730f8a6113026d9ffa44d9f"
        assert "versionCode=5 " in self._shell("dumpsys", "package", self.PACKAGE)
        self.press_key(3)
        end = time.monotonic() + 5
        while True:
            try:
                self.cat_game_is_backgrounded()
                break
            except AssertionError:
                if time.monotonic() >= end:
                    raise
                time.sleep(.2)
        self.force_stop_cat_game()
        path = f"/data/media/{self.user}/Android/data/{self.PACKAGE}/files/CatGame/save.json"
        read = [*self.adb[:-1], "exec-out", "su", "0", "cat", path]
        before = subprocess.run(read, capture_output=True, check=True, timeout=30).stdout
        (self.output_dir / "before-install-save.json").write_bytes(before)
        state = json.loads(before)
        assert sum(cat["count"] for cat in state["cats"]) == 2
        install = subprocess.run([*self.adb[:-1], "install", "--user", self.user, "-r", str(apk)],
                                 capture_output=True, timeout=180)
        (self.output_dir / "install.stdout.txt").write_bytes(install.stdout)
        (self.output_dir / "install.stderr.txt").write_bytes(install.stderr)
        self._record("cover-install", {"exit": install.returncode, "apkSha256": apk_hash})
        assert install.returncode == 0 and b"Success" in install.stdout
        after = subprocess.run(read, capture_output=True, check=True, timeout=30).stdout
        (self.output_dir / "after-install-before-launch-save.json").write_bytes(after)
        package = self._shell("dumpsys", "package", self.PACKAGE)
        assert "versionCode=6 " in package and "versionName=1.1.0" in package
        result = {"beforeSha256": hashlib.sha256(before).hexdigest(), "afterSha256": hashlib.sha256(after).hexdigest(),
                  "bytesIdentical": before == after, "fromCode": 5, "toCode": 6,
                  "apkSha256": apk_hash, "naturalOwnedCats": 2}
        (self.output_dir / "upgrade.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
        assert before == after, "Cover installation must preserve the actual old save bytes before first launch"

    def upgraded_state_retains_owned_cats_and_clicks(self):
        before = json.loads((self.output_dir / "before-install-save.json").read_bytes())
        after = self._saved_state("after-launch-save.json")
        for field in ("cats", "items", "clicks", "introCompleted"):
            assert before[field] == after[field], field
        self._record("loaded-old-state", {"ownedCats": sum(cat["count"] for cat in after["cats"]),
                                          "clicks": after["clicks"], "introCompleted": after["introCompleted"]})

    def pet_visible_milk_and_verify_saved_click(self, tag):
        self._guard()
        palette = [(77, 73, 87), (104, 97, 113), (39, 38, 49), (56, 54, 66), (139, 130, 147)]
        source = Path(__file__).resolve().parents[3] / "Assets/Resources/Art/Cats/Pixel64/Milk/Sleep.png"
        sheet = np.asarray(Image.open(source).convert("RGBA"))
        templates = []
        for i in range(sheet.shape[1] // 64):
            for flip in (False, True):
                rgba = sheet[:, i * 64:(i + 1) * 64]
                if flip:
                    rgba = cv2.flip(rgba, 1)
                templates.append((i, flip, np.ascontiguousarray(rgba[:, :, :3]),
                                  np.ascontiguousarray(rgba[:, :, 3])))
        self._record("sleep-source", {"sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                                       "reason": "stable natural Sleep body avoids chasing a roaming target through screenshot latency"})
        end = time.monotonic() + 90
        observation = 0
        while True:
            observation += 1
            snapshot = tag + f"-save-observation-{observation:03d}.json"
            before = self._saved_state(snapshot)
            frame = self._screenshot().convert("RGB")
            if self._dismiss_known_cg(frame, tag):
                continue
            half = max(5.625, 10 * frame.height / frame.width)
            logical_height = round(half * 32) * 2
            logical_width = round(half * 32 * frame.width / frame.height) * 2
            # Sample pixel centres when undoing fractional Point upscaling (e.g. 2.5x).
            # Legacy INTER_NEAREST samples block edges and can shift alternate source texels.
            small = cv2.resize(np.asarray(frame), (logical_width, logical_height), interpolation=cv2.INTER_NEAREST_EXACT)
            left = int(logical_width * .30)
            roi = np.ascontiguousarray(small[:, left:])
            best = None
            for index, flip, rgb, mask in templates:
                scores = cv2.matchTemplate(roi, rgb, cv2.TM_SQDIFF, mask=mask)
                minimum, _, location, _ = cv2.minMaxLoc(scores)
                mse = max(0, minimum) / (np.count_nonzero(mask) * 3)
                px, py = location
                patch = roi[py:py + 64, px:px + 64]
                dark = np.zeros((64, 64), dtype=bool)
                for colour in palette:
                    dark |= np.all(patch == colour, axis=2)
                extra = np.count_nonzero(dark & (mask == 0)) / max(1, np.count_nonzero(mask))
                if extra < .08 and (best is None or mse < best[0]):
                    best = (mse, px + left, py, index, flip, mask)
            self._record("sleep-observation", {"size": frame.size, "saveSnapshot": snapshot,
                                               "bestMse": best[0] if best else None})
            if best is not None and best[0] < 400:
                mse, px, py, index, flip, mask = best
                ys, xs = np.nonzero(mask)
                x = round((px + float(xs.mean())) * frame.width / logical_width)
                y = round((py + float(ys.mean())) * frame.height / logical_height)
                frame.save(self.output_dir / (tag + "-body-before.png"))
                (self.output_dir / (tag + "-save-before.json")).write_bytes((self.output_dir / snapshot).read_bytes())
                break
            if time.monotonic() >= end:
                frame.save(self.output_dir / (tag + "-sleep-not-found.png"))
                raise AssertionError("No visually matching natural Sleep pose within 90 seconds; no touch sent")
            time.sleep(.4)
        self._record("appium-pet", {"size": frame.size, "point": [x, y],
                                   "sleepFrame": index, "flipped": flip, "mse": mse, "beforeClicks": before["clicks"]})
        self.tap(x, y)
        time.sleep(.25)
        self.capture(tag + "-body-after.png")
        self.press_key(3)
        end = time.monotonic() + 5
        while True:
            try:
                self.cat_game_is_backgrounded()
                break
            except AssertionError:
                if time.monotonic() >= end:
                    raise
                time.sleep(.2)
        after = self._saved_state(tag + "-save-after.json")
        self._record("saved-click-result", {"before": before["clicks"], "after": after["clicks"]})
        assert after["clicks"] == before["clicks"] + 1, "One Appium body touch must persist exactly one click"
        logger.info(f"{tag}: real touch saved clicks {before['clicks']} -> {after['clicks']}", also_console=True)
