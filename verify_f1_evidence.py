"""Independently check published F1 records; this does not re-run Unity/Appium."""
import hashlib
import json
import sys
from pathlib import Path
from xml.etree import ElementTree as ET

ROOT = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parent
BASE = ROOT / 'evidence/f1'
def read(name):
    return json.loads((BASE/name).read_text(encoding='utf-8-sig'))
def robot(name, passed):
    stats = ET.parse(BASE/name/'output.xml').find('.//statistics/total/stat')
    assert int(stats.get('pass')) == passed and int(stats.get('fail')) == 0, name
for run, count in [('edit-full-03',81),('play-full-02',92),('contracts-green-02',8)]:
    xml = ET.parse(BASE/run/'results.xml').getroot()
    assert int(xml.get('passed')) == count and int(xml.get('failed')) == 0, run
red = ET.parse(BASE/'contracts-red-02/results.xml').getroot()
assert int(red.get('passed')) == int(red.get('failed')) == 1
old = ET.parse(BASE/'play-full-01/results.xml').getroot()
assert int(old.get('passed')) == 91 and int(old.get('failed')) == 1
for run, total in [('native-grid-02',1),('visual-grid-02',1),('pointer-manual-02',1),
                   ('android-old-smoke-02',1),('android-upgrade-01',1),
                   ('android-touch-09',3),('android-ending-03',1),('android-safearea-07',1)]:
    robot(run, total)
migration = read('migration-comparison.json')
assert migration['identitiesCompared'] == migration['baselineUses'] == migration['afterUses'] == 528
assert migration['changedPngs'] == migration['missingOrChangedIdentities'] == []
assert len(list((BASE/'baseline-01/poses-02').glob('*.png'))) == 44
native = read('native-grid-02/runtime/art-metrics.json')
assert native['complete'] and native['requestedSizesHonored']
assert [(s['width'],s['height']) for s in native['screens']] == [(1920,1080),(1600,900),(2560,1440)]
upgrade = read('android-upgrade-01/upgrade.json')
assert upgrade['bytesIdentical'] and upgrade['fromCode'] == 5 and upgrade['toCode'] == 6
assert upgrade['beforeSha256'] == upgrade['afterSha256']
assert (BASE/'android-upgrade-01/before-install-save.json').read_bytes() == (BASE/'android-upgrade-01/after-install-before-launch-save.json').read_bytes()
for tag, before_count in [('standard',1),('wide',2),('compact',3)]:
    before = read('android-touch-09/'+tag+'-save-before.json')
    after = read('android-touch-09/'+tag+'-save-after.json')
    assert before['clicks'] == before_count and after['clicks'] == before_count+1
assert read('android-touch-09/standard-cg-1-save-before.json')['clicks'] == read('android-touch-09/standard-cg-1-save-after.json')['clicks']
ending = read('android-ending-03/runtime/ending-result.json')
assert [(c['width'],c['action']) for c in ending] == [(2000,'continue'),(1200,'menu')]
assert all(c['completed'] and c['firstEnding']['totalCats'] == 2 for c in ending)
safe = read('android-safearea-07/runtime/result.json')
assert safe['passed'] and safe['insets'] == [132,0,0,0] and safe['nativeContinueTouch']
assert abs(safe['actualButtonCenter'] - safe['expectedSafeCenter']) < 2
assert read('windows-release-probe-02/result.json') == dict(passed=True, restored=True, gameSource='f536871')
debug = read('windows-release-probe-02/debugger-smoke.json')
assert debug['connected'] and debug['frozen'] and debug['pid'] > 0
package = read('distribution/package-manifest.json')
assert len(package['files']) == 152 and package['all152ZipFilesReadBack']
assert package['apkSha256'] == upgrade['apkSha256']
cleanup = read('cleanup.json')
assert cleanup['shutdownComplete'] and cleanup['remainingOwnedProcesses'] == []
assert not cleanup['phoneOperated'] and not cleanup['mumuOperated']
manifest = json.loads((ROOT/'provenance.json').read_text(encoding='utf-8-sig'))
for item in manifest['files']:
    assert hashlib.sha256((ROOT/item['path']).read_bytes()).hexdigest() == item['publishedSha256'], item['path']
    assert 'windows-user-save-backup' not in item['path'] and '/registry/' not in item['path']
print('PASS F1 red/green, full engine suites, native Player, upgrade, Appium touches/ending/cutout, packaging and cleanup')
print('PASS public manifest SHA256:', len(manifest['files']), 'files; historical rows retained')
