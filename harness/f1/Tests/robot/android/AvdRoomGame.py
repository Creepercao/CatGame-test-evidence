"""Explicit, isolated AVD fixtures for room-only aspect-ratio regression."""
import json
import subprocess
from pathlib import Path
from robot.api import logger
from AndroidGame import AndroidGame


class AvdRoomGame(AndroidGame):
    def restore_room_fixture(self, fixture_path, evidence_name):
        assert self.serial == "emulator-5564" and self.user != "0"
        assert self._shell("getprop", "ro.product.model") == "sdk_gphone64_x86_64"
        assert self._shell("am", "get-current-user") == self.user
        fixture = json.loads(Path(fixture_path).read_text(encoding="utf-8-sig"))
        assert fixture["introCompleted"] and not fixture["pendingStoryEvents"]
        assert fixture["lastSeenUnix"] == 0 and sum(cat["count"] for cat in fixture["cats"]) == 2
        primary = f"/data/media/{self.user}/Android/data/{self.PACKAGE}/files/CatGame/save.json"
        assert self._shell("su", "0", "test", "-f", primary) == ""
        subprocess.run([*self.adb, "su", "0", "tee", primary], input=json.dumps(fixture).encode(),
                       check=True, capture_output=True, timeout=30)
        actual = json.loads(self._shell("su", "0", "cat", primary))
        assert actual == fixture
        (self.output_dir / evidence_name).write_text(json.dumps(actual, indent=2), encoding="utf-8")
        logger.info("Restored two-cat room fixture only in the isolated AVD user", also_console=True)
