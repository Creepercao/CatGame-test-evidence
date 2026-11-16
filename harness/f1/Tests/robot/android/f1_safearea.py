"""Actual tall-cutout inset and native touch on the owned AVD, not a synthetic viewport."""
import argparse
import json
import re
import time
from pathlib import Path
from PIL import Image

import numpy as np
from AndroidGame import AndroidGame

parser = argparse.ArgumentParser()
parser.add_argument('--adb', required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=False)
game = AndroidGame('http://127.0.0.1:14723', args.adb, 15037,
                   'emulator-5564', 10, args.output, False, 18326)
overlay = 'com.android.internal.display.cutout.emulation.tall'
assert game._shell('getprop', 'ro.product.model') == 'sdk_gphone64_x86_64'
assert game._shell('am', 'get-current-user') == '10'
results = {'device': 'owned read-only Android 14 AVD', 'gameSource': 'f536871',
           'fixture': 'existing declared F1 QA ending save', 'passed': False}

def record(name, value):
    (args.output / name).write_text(value, encoding='utf-8')

try:
    game.force_stop_cat_game()
    # This AVD suppresses the logical cutout under wm size overrides. Use its real panel.
    record('initial-size.txt', game._shell('wm', 'size'))
    for user in ('0', '10'):
        game._shell('cmd', 'overlay', 'enable', '--user', user, overlay)
    game._shell('wm', 'size', 'reset')
    game.open_appium()
    game.start_cat_game()
    deadline = time.monotonic() + 45
    while True:
        frame = game._screenshot()
        if frame.size == (2220, 1080):
            pixels = np.asarray(frame).astype(int)
            # Above the text baseline: count background pixels, not holes made by white glyphs.
            mask = np.max(np.abs(pixels[420] - [232, 138, 106]), axis=1) < 3
            xs = np.where(mask)[0]
            if len(xs) > 300:
                break
        if time.monotonic() >= deadline:
            frame.save(args.output / 'menu-failure.png')
            record('menu-failure-window.txt', game._shell('dumpsys', 'window', 'displays'))
            raise AssertionError(f'Native menu did not settle at 2220x1080; actual {frame.size}')
        time.sleep(.4)
    frame.save(args.output / 'menu.png')
    window = game._shell('dumpsys', 'window', 'displays')
    record('window-displays.txt', window)
    cutout = re.search(r'mDisplayCutout=DisplayCutout\{insets=Rect\((\d+), (\d+) - (\d+), (\d+)\)', window)
    assert cutout, 'No actual WindowManager cutout record'
    left, top, right, bottom = map(int, cutout.groups())
    assert left > 0 and top == right == bottom == 0, cutout.group()
    center = (int(xs[0]) + int(xs[-1])) / 2
    expected = left + (2220 - left - right) * .825
    assert abs(center - expected) < 2, (center, expected)
    results.update(screen=[2220, 1080], insets=[left, top, right, bottom],
                   continueButtonBounds=[int(xs[0]), int(xs[-1])],
                   actualButtonCenter=center, expectedSafeCenter=expected)
    game.tap(round(center), 446)
    deadline = time.monotonic() + 30
    dismissed_offline = False
    while True:
        home = game._screenshot()
        home.save(args.output / 'home-last-observed.png')
        if home.size == (2220, 1080):
            # Native fonts are rerasterized at 1080p, so do not resize a text crop
            # and silently raise its threshold. Match the reviewed modal/button geometry.
            offline_samples = [((902, 410), (253, 246, 236)),
                               ((1450, 670), (253, 246, 236)),
                               ((1100, 610), (232, 138, 106))]
            offline_matches = all(max(abs(a-b) for a,b in zip(home.getpixel(point), color)) <= 2
                                  for point, color in offline_samples)
            if not dismissed_offline and offline_matches:
                home.save(args.output / 'offline-summary.png')
                game.tap(1176, 623)
                dismissed_offline = True
                results['offlineSummaryDismissed'] = True
                continue
            a = np.asarray(home)
            # Actual HUD info panel moved inside the cutout by 24 native pixels.
            if np.min(a[40, left+30]) > 250 and tuple(a[10, left+10]) == (253, 246, 236):
                home.save(args.output / 'home.png')
                break
        assert time.monotonic() < deadline, 'Home info panel absent inside safe area'
        time.sleep(.4)
    record('home-hierarchy.xml', game.capture_ui_hierarchy('home-native.xml'))
    game.press_key(3)
    time.sleep(.5)
    game.cat_game_is_backgrounded()
    raw = game._shell('su', '0', 'cat', '/data/media/10/Android/data/' + game.PACKAGE + '/files/CatGame/save.json')
    state = json.loads(raw)
    assert sum(cat['count'] for cat in state['cats']) == 2 and state['endingCompleted']
    results.update(passed=True, nativeContinueTouch=True, savedCats=2,
                   homeInfoProbe=[left+30, 40], endingRetained=True)
finally:
    game.force_stop_cat_game()
    for user in ('0', '10'):
        game._shell('cmd', 'overlay', 'disable', '--user', user, overlay)
    game._shell('wm', 'size', '900x1600')
    record('restored-size.txt', game._shell('wm', 'size'))
    record('restored-overlays.txt', game._shell('cmd', 'overlay', 'list', '--user', '10'))
    game.close_appium()
    record('result.json', json.dumps(results, indent=2))
assert results['passed']
print('PASS actual tall cutout, menu safe bounds, native Continue touch and Home HUD')
