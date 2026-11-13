"""Appium keywords for the native-size, cutout-equipped Android acceptance phone."""

import json
import time
from PIL import Image, ImageChops, ImageStat
from robot.api import logger
from AndroidGame import AndroidGame


class PhoneGame(AndroidGame):
    def __init__(self, *args):
        super().__init__(*args)
        self.references["three-cats"] = self.references["two-cats"]

    def open_appium(self):
        assert not self.serial.startswith("emulator-")
        assert self._shell("am", "get-current-user") == self.user == "0"
        capabilities = {"platformName": "Android", "appium:automationName": "UiAutomator2",
                        "appium:udid": self.serial, "appium:adbPort": self.adb_port,
                        "appium:noReset": True, "appium:skipDeviceInitialization": True}
        self.session = self._request("/session", {"capabilities": {"alwaysMatch": capabilities}})["sessionId"]
        self.start_cat_game()
        deadline = time.monotonic() + 15
        frame = self._screenshot()
        while frame.size != (2712, 1220) and time.monotonic() < deadline:
            time.sleep(.5)
            frame = self._screenshot()
        assert frame.size == (2712, 1220), f"This phone layout expects native 2712x1220, got {frame.size}"
        logger.info("Phone uses native 2712x1220, left cutout inset 88 pixels", also_console=True)

    def point(self, zone, x, y):
        scale = 1220 / 900
        if zone == "left":
            x = 88 + float(x) * scale
        elif zone == "right":
            x = 2712 + (float(x) - 1600) * scale
        elif zone == "menu":
            x = 88 + (2712 - 88) * .825 + (float(x) - 1320) * scale
        elif zone == "center":
            x = 1400 + (float(x) - 800) * scale
        else:
            raise AssertionError(f"Unknown UI zone {zone}")
        return round(x), round(float(y) * scale)

    def tap_ui(self, zone, x, y):
        self.tap(*self.point(zone, x, y))

    def _error(self, frame, target, offset_x=0):
        zone = "left" if target in ("one-cat", "two-cats", "three-cats", "milk-affordable") else (
            "menu" if target == "menu" else "right" if target == "intro" else "center")
        left, top, right, bottom = self.references[target]
        region = frame.crop((*self.point(zone, left, top), *self.point(zone, right, bottom)))
        native_reference = self.reference_dir.parent / "phone_references" / f"{target}.png"
        if native_reference.exists():
            with Image.open(native_reference) as reference:
                assert region.size == reference.size
                return sum(ImageStat.Stat(ImageChops.difference(region, reference)).mean) / 3
        with Image.open(self.reference_dir / f"{target}.png") as reference:
            region = region.resize(reference.size, Image.Resampling.LANCZOS)
            return sum(ImageStat.Stat(ImageChops.difference(region, reference)).mean) / 3

    def wait_for_visual(self, target, name, timeout=20, **kwargs):
        deadline = time.monotonic() + float(timeout)
        while True:
            frame = self._screenshot()
            error = float("inf")
            other = None
            if frame.size == (2712, 1220):
                error = self._error(frame, target)
                if target in ("one-cat", "two-cats", "three-cats"):
                    other = min(self._error(frame, candidate) for candidate in ("one-cat", "two-cats", "three-cats")
                                if candidate != target)
            # Native-resolution glyph resampling has its own threshold and count discrimination.
            if error < 12 and (other is None or error + 3 < other):
                frame.save(self.output_dir / name)
                logger.info(f"Phone visual {target}: error={error:.2f}, other={other}", also_console=True)
                return
            if time.monotonic() >= deadline:
                frame.save(self.output_dir / name)
                raise AssertionError(f"Phone {target} error={error:.2f}, other={other}")
            time.sleep(.4)

    def wait_for_affordable_milk(self):
        deadline = time.monotonic() + 120
        dismissed = False
        while time.monotonic() < deadline:
            frame = self._screenshot()
            if self._error(frame, "milk-affordable") < 12 and self._error(frame, "one-cat") < 12:
                frame.save(self.output_dir / "06-affordable.png")
                return
            if self._error(frame, "story-continue") < 12 and not dismissed:
                frame.save(self.output_dir / "05-achievement-cg.png")
                self.tap_ui("center", 800, 859)
                dismissed = True
            time.sleep(1)
        self.capture("06-affordable-timeout.png")
        raise AssertionError("Milk purchase did not become available")

    def buy_and_press_home(self):
        started = time.monotonic()
        self.tap_ui("left", 185, 300)
        self.press_key(3)
        elapsed = time.monotonic() - started
        logger.info(f"Phone purchase tap through HOME: {elapsed:.3f}s", also_console=True)
        assert elapsed < 3, elapsed

    def swipe_shop(self, before, after):
        self.capture(before)
        self.swipe(*self.point("left", 190, 675), *self.point("left", 190, 330))
        time.sleep(.7)
        self.capture(after)
        box = (*self.point("left", 46, 255), *self.point("left", 325, 700))
        with Image.open(self.output_dir / before) as first, Image.open(self.output_dir / after) as second:
            error = sum(ImageStat.Stat(ImageChops.difference(first.crop(box), second.crop(box))).mean) / 3
        assert error >= 10, error
        logger.info(f"Native shop swipe region changed, RGB error={error:.2f}", also_console=True)

    def begin_offline_test(self):
        self.network_original = {
            "airplane": self._shell("settings", "get", "global", "airplane_mode_on"),
            "wifi": self._shell("settings", "get", "global", "wifi_on")}
        assert self.network_original["airplane"] in ("0", "1")
        assert self.network_original["wifi"] in ("0", "1", "2")
        (self.output_dir / "network-original.json").write_text(json.dumps(self.network_original), encoding="utf-8")
        self._shell("cmd", "connectivity", "airplane-mode", "enable")
        self._shell("svc", "wifi", "disable")
        time.sleep(2)
        self.phone_should_be_offline()

    def phone_should_be_offline(self):
        assert self._shell("settings", "get", "global", "airplane_mode_on") == "1"
        connectivity = self._shell("dumpsys", "connectivity")
        active = [line.strip() for line in connectivity.splitlines() if "Active default network:" in line]
        assert active == ["Active default network: none"], active
        logger.info("Airplane mode enabled, Android has no active default network", also_console=True)

    def restore_network(self):
        original = getattr(self, "network_original", None)
        if original is None:
            return
        self._shell("cmd", "connectivity", "airplane-mode", "enable" if original["airplane"] == "1" else "disable")
        self._shell("svc", "wifi", "enable" if original["wifi"] in ("1", "2") else "disable")
        logger.info("Restored the phone's original airplane and Wi-Fi settings", also_console=True)
        self.network_original = None

    def close_appium(self):
        try:
            self.restore_network()
        finally:
            super().close_appium()

    def saved_state(self, name):
        path = f"/sdcard/Android/data/{self.PACKAGE}/files/CatGame/save.json"
        state = json.loads(self._shell("cat", path))
        (self.output_dir / name).write_text(json.dumps(state, indent=2), encoding="utf-8")
        return state

    def saved_cat_count_should_be(self, expected, name):
        state = self.saved_state(name)
        assert sum(cat["count"] for cat in state["cats"]) == int(expected)

    def pet_visible_cat(self):
        self.complete_pending_memories("pet-entry")
        before = self.saved_state("pet-before.json")
        frame = self._screenshot()
        frame.save(self.output_dir / "pet-before.png")
        pixels = frame.load()
        dark = set()
        for y in range(500, 1050, 4):
            for x in range(700, 2500, 4):
                r, g, b = pixels[x, y]
                if r < 95 and g < 95 and b < 115 and b > r and b > g:
                    dark.add((x, y))
        blobs = []
        while dark:
            pending = [dark.pop()]
            blob = []
            while pending:
                x, y = pending.pop()
                blob.append((x, y))
                for adjacent in ((x - 4, y), (x + 4, y), (x, y - 4), (x, y + 4)):
                    if adjacent in dark:
                        dark.remove(adjacent)
                        pending.append(adjacent)
            blobs.append(blob)
        assert blobs and max(map(len, blobs)) >= 30, "No dark cat body identified on the floor"
        blob = max(blobs, key=len)
        self.tap(round(sum(p[0] for p in blob) / len(blob)), round(sum(p[1] for p in blob) / len(blob)))
        time.sleep(.5)
        self.capture("pet-after-touch.png")
        self.press_key(3)
        time.sleep(1)
        after = self.saved_state("pet-after.json")
        assert after["clicks"] == before["clicks"] + 1, (before["clicks"], after["clicks"])
        logger.info("Touching the visible cat incremented the saved pet counter by one", also_console=True)
        self.start_cat_game()
        self.complete_pending_memories("pet-return")

    def complete_pending_memories(self, prefix):
        state = self.saved_state(f"{prefix}-pending.json")
        for index, story in enumerate(state["pendingStoryEvents"]):
            self.wait_for_visual("story-continue", f"{prefix}-{index}-cg.png")
            self.tap_ui("center", 800, 859)
            time.sleep(.3)
            current = self.saved_state(f"{prefix}-{index}-completed.json")
            assert story in current["seenStoryEvents"] and story not in current["pendingStoryEvents"]

    def buy_offline_cat(self):
        self.phone_should_be_offline()
        before = self.saved_state("offline-before-purchase.json")
        assert sum(cat["count"] for cat in before["cats"]) == 2
        assert before["catHair"] >= 33.8, before["catHair"]
        self.tap_ui("left", 185, 300)
        time.sleep(.5)
        self.capture("offline-04-purchased.png")
        self.press_key(3)
        time.sleep(1)
        self.saved_cat_count_should_be(3, "offline-after-purchase.json")
