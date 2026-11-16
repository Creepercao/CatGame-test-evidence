"""Reuse the existing actual-UI ending flow with isolated F1 ports and a declared fixture."""
import argparse
import json
import time
from pathlib import Path
from AndroidGame import AndroidGame
from check_ending import run, ROCKET_PRICE

parser = argparse.ArgumentParser()
parser.add_argument('--adb', required=True)
parser.add_argument('--fixture', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
fixture = json.loads(args.fixture.read_text(encoding='utf-8-sig'))
assert fixture['introCompleted'] and sum(c['count'] for c in fixture['cats']) == 2
assert not fixture['rocketPurchased'] and not fixture['endingCompleted']
fixture.update(catHair=ROCKET_PRICE + 500, lastSeenUnix=0, pendingStoryEvents=[])
args.output.mkdir(parents=True, exist_ok=False)
(args.output / 'seeded-fixture.json').write_text(json.dumps(fixture, indent=2), encoding='utf-8')
class EndingGame(AndroidGame):
    def start_cat_game(self):
        # Width changes can leave Android's compatibility view awaiting an Activity restart.
        # Each ending branch must start in its requested geometry, including the initial check.
        self.force_stop_cat_game()
        return super().start_cat_game()

    def restore_display_size(self):
        self.force_stop_cat_game()
        return super().restore_display_size()

    def wait_for_visual(self, target, name, timeout=12, **kwargs):
        started = time.monotonic()
        try:
            return super().wait_for_visual(target, name, timeout=45 if target == 'menu' else timeout, **kwargs)
        finally:
            with (self.output_dir / 'visual-timings.jsonl').open('a', encoding='utf-8') as stream:
                stream.write(json.dumps({'target': target, 'name': name,
                                         'seconds': time.monotonic() - started}) + '\n')

game = EndingGame('http://127.0.0.1:14723', args.adb, 15037, 'emulator-5564', 10,
                   args.output, False, 18326)
assert game._shell('getprop', 'ro.product.model') == 'sdk_gphone64_x86_64'
assert game._shell('am', 'get-current-user') == '10'
run(game, fixture)
