"""Appium UI and isolated Android lifecycle keywords for CatGame's landscape APK."""

import base64
import io
import json
import subprocess
import time
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from PIL import Image, ImageChops, ImageStat
from robot.api import logger


class AndroidGame:
    ROBOT_LIBRARY_SCOPE = "SUITE"
    PACKAGE = "com.catgamestudio.catgame"
    ACTIVITY = "com.unity3d.player.UnityPlayerActivity"
    USER_NAME = "CatGame-Appium-QA"

    def __init__(self, server, adb, adb_port, serial, user, output_dir):
        self.server = server.rstrip("/")
        self.adb = [adb, "-P", str(adb_port), "-s", serial, "shell"]
        self.adb_port = int(adb_port)
        self.serial = serial
        self.user = str(user)
        self.output_dir = Path(output_dir)
        self.session = None
        self.original_override = None
        self.reference_dir = Path(__file__).with_name("references")
        self.references = json.loads((self.reference_dir / "regions.json").read_text())

    def _request(self, path, data=None, method=None):
        body = None if data is None else json.dumps(data).encode("utf-8")
        request = Request(self.server + path, body, {"Content-Type": "application/json"}, method=method)
        try:
            with urlopen(request, timeout=120) as response:
                return json.load(response)["value"]
        except HTTPError as error:
            raise AssertionError(f"Appium {request.get_method()} {path}: {error.read().decode()}") from error

    def _command(self, path, data=None, method=None):
        return self._request(f"/session/{self.session}{path}", data, method)

    def _mobile(self, name, **args):
        return self._command("/execute/sync", {"script": f"mobile: {name}", "args": [args]})

    def _shell(self, *args):
        return subprocess.check_output([*self.adb, *args], timeout=30, encoding="utf-8").strip()

    def open_appium(self):
        users = self._shell("pm", "list", "users")
        current = self._shell("am", "get-current-user")
        assert self.user != "0" and f"UserInfo{{{self.user}:{self.USER_NAME}:" in users, users
        assert current == self.user, f"Switch to Android QA user {self.user}, current user is {current}"
        capabilities = {"platformName": "Android", "appium:automationName": "UiAutomator2",
                        "appium:udid": self.serial, "appium:adbPort": self.adb_port,
                        "appium:userProfile": int(self.user), "appium:noReset": True,
                        "appium:skipDeviceInitialization": True}
        self.session = self._request("/session", {"capabilities": {"alwaysMatch": capabilities}})["sessionId"]
        logger.info(f"Appium session {self.session}, Android test user {self.user}", also_console=True)

    def close_appium(self):
        if self.session:
            self._command("", method="DELETE")
            self.session = None

    def start_cat_game(self):
        result = self._shell("am", "start", "-W", "--user", self.user, "-n", f"{self.PACKAGE}/{self.ACTIVITY}")
        logger.info(result, also_console=True)
        assert "Status: ok" in result, result

    def cat_game_pid(self):
        for line in self._shell("ps", "-A", "-o", "USER,PID,NAME").splitlines():
            owner, pid, name = line.split(maxsplit=2)
            if owner.startswith(f"u{self.user}_") and name == self.PACKAGE:
                logger.info(f"CatGame QA PID {pid}", also_console=True)
                return pid
        return ""

    def force_stop_cat_game(self):
        self._shell("am", "force-stop", "--user", self.user, self.PACKAGE)
        assert not self.cat_game_pid(), "QA process still running after force-stop"

    def cat_game_is_backgrounded(self):
        activities = self._shell("dumpsys", "activity", "activities")
        resumed = [line for line in activities.splitlines() if "topResumedActivity=" in line]
        assert resumed and all(self.PACKAGE not in line for line in resumed), resumed
        logger.info("CatGame is backgrounded; " + resumed[0].strip(), also_console=True)

    def capture_ui_hierarchy(self, name):
        source = self._command("/source")
        (self.output_dir / name).write_text(source, encoding="utf-8")
        logger.info(f"Appium native hierarchy: {name}", also_console=True)
        return source

    def _screenshot(self):
        png = base64.b64decode(self._command("/screenshot"))
        return Image.open(io.BytesIO(png)).convert("RGB")

    def capture(self, name):
        frame = self._screenshot()
        frame.save(self.output_dir / name)
        logger.info(f"Appium screenshot {name}, {frame.width}x{frame.height}", also_console=True)
        return frame.size

    def _error(self, frame, target, offset_x=0):
        left, top, right, bottom = self.references[target]
        with Image.open(self.reference_dir / f"{target}.png") as reference:
            diff = ImageChops.difference(frame.crop((left + offset_x, top, right + offset_x, bottom)), reference)
        return sum(ImageStat.Stat(diff).mean) / 3

    def wait_for_visual(self, target, name, timeout=12, width=1600, offset_x=0):
        deadline = time.monotonic() + float(timeout)
        expected_size = (int(width), 900)
        while True:
            frame = self._screenshot()
            if frame.size == expected_size:
                error = self._error(frame, target, int(offset_x))
                if error <= 2.5:
                    frame.save(self.output_dir / name)
                    logger.info(f"Visual check {target} passed (RGB error {error:.2f}): {name}", also_console=True)
                    return
            if time.monotonic() >= deadline:
                frame.save(self.output_dir / name)
                assert frame.size == expected_size, f"Visual baseline expects {expected_size}, got {frame.size} in {name}"
                raise AssertionError(f"{target} absent, mean RGB error {error:.2f} in {name}")
            time.sleep(0.4)

    def wait_for_affordable_milk(self):
        deadline = time.monotonic() + 100
        dismissed = False
        while time.monotonic() < deadline:
            frame = self._screenshot()
            if self._error(frame, "milk-affordable") <= 2.5 and self._error(frame, "one-cat") <= 2.5:
                frame.save(self.output_dir / "06-milk-affordable.png")
                logger.info("Milk became affordable from passive production, with one cat", also_console=True)
                return
            if self._error(frame, "story-continue") <= 2.5 and not dismissed:
                frame.save(self.output_dir / "05-achievement-cg.png")
                self.tap(800, 859)
                dismissed = True
            time.sleep(1)
        self.capture("06-milk-timeout.png")
        raise AssertionError("Milk was never available with one cat after 100 seconds")

    def tap(self, x, y):
        logger.info(f"Appium clickGesture ({x}, {y})", also_console=True)
        self._mobile("clickGesture", x=int(x), y=int(y))

    def swipe(self, x, y, to_x, to_y):
        logger.info(f"Appium touch swipe ({x}, {y}) -> ({to_x}, {to_y})", also_console=True)
        self._command("/actions", {"actions": [{
            "type": "pointer", "id": "finger", "parameters": {"pointerType": "touch"},
            "actions": [{"type": "pointerMove", "duration": 0, "x": int(x), "y": int(y)},
                        {"type": "pointerDown", "button": 0},
                        {"type": "pointerMove", "duration": 450, "x": int(to_x), "y": int(to_y)},
                        {"type": "pointerUp", "button": 0}]}]})

    def press_key(self, keycode):
        logger.info(f"Appium Android key {keycode}", also_console=True)
        self._mobile("pressKey", keycode=int(keycode))

    def lock_screen(self):
        self._mobile("lock")

    def unlock_screen(self):
        if self.session:
            self._mobile("unlock")

    def screen_should_be_locked(self, locked):
        actual = self._mobile("isLocked")
        expected = str(locked).lower() == "true"
        assert actual is expected, f"Appium isLocked expected {expected}, got {actual}"
        power = next(line.strip() for line in self._shell("dumpsys", "power").splitlines()
                     if line.strip().startswith("mWakefulness="))
        assert (power == "mWakefulness=Awake") is not expected, power
        logger.info(f"Appium isLocked={actual}; Android {power}", also_console=True)

    def buy_and_press_home(self):
        started = time.monotonic()
        self.tap(185, 300)
        self.press_key(3)
        elapsed = time.monotonic() - started
        logger.info(f"Purchase tap through HOME: {elapsed:.3f}s", also_console=True)
        assert elapsed < 3, f"Input pair took {elapsed:.3f}s, outside the immediate-interruption budget"

    def set_display_width(self, width):
        assert self.session and self._shell("am", "get-current-user") == self.user
        if self.original_override is None:
            size = self._shell("wm", "size")
            self.original_override = next((line.split(": ")[1] for line in size.splitlines()
                                           if line.startswith("Override size:")), "reset")
        self._shell("wm", "size", f"900x{int(width)}")
        logger.info(self._shell("wm", "size"), also_console=True)

    def restore_display_size(self):
        if self.original_override is not None:
            self._shell("wm", "size", self.original_override)
            logger.info("Restored device display: " + self._shell("wm", "size"), also_console=True)
            self.original_override = None

    def shop_scroll_should_move(self, before_name, after_name):
        box = (46, 255, 325, 700)
        with Image.open(self.output_dir / before_name) as before, Image.open(self.output_dir / after_name) as after:
            error = sum(ImageStat.Stat(ImageChops.difference(before.crop(box), after.crop(box))).mean) / 3
        assert error >= 10, f"Shop region barely changed after swipe (RGB error {error:.2f})"
        logger.info(f"Shop region moved after touch swipe (RGB error {error:.2f})", also_console=True)
