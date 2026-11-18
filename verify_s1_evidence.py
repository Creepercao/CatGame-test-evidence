"""Independent S1 evidence consistency check; does not rerun Unity or Appium."""
import hashlib,json,sys
from pathlib import Path
from xml.etree import ElementTree as E
ROOT=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).resolve().parent
BASE=ROOT/'evidence/s1'
def read(name):return json.loads((BASE/name).read_text(encoding='utf-8-sig'))
def robot(run,passed,failed=0):
    s=E.parse(BASE/run/'output.xml').find('.//statistics/total/stat')
    assert (int(s.get('pass')),int(s.get('fail')))==(passed,failed),run
for run,n in [('edit-full-01',89),('play-full-02',99),('play-theme-06',8)]:
    r=E.parse(BASE/run/'results.xml').getroot();assert int(r.get('passed'))==n and int(r.get('failed'))==0
red=E.parse(BASE/'play-theme-05/results.xml').getroot();assert red.get('failed')=='1'
assert (BASE/'play-theme-04/exit-code.txt').read_text(encoding='utf-8-sig').strip()=='6'
for run,n in [('windows-growth-01',1),('windows-restart-01',1),('android-baseline-smoke-02',1),('android-upgrade-01',1),('android-growth-01',4)]:robot(run,n)
robot('windows-growth-02',0,1);robot('android-baseline-smoke-01',0,1)
first=read('windows-growth-01/runtime/result.json')
assert first['complete'] and first['visible']==100 and first['owned']==440
assert first['footErrors']==first['depthErrors']==0 and first['samples']>0
restart=read('windows-restart-01/runtime/verification.json')
assert restart['passed'] and restart['freshPlayableLoad'] and restart['originalNativeInputRetained']
for n in ['02','03','04']:
    r=read(f'windows-release-probe-{n}/result.json');assert not r['passed'] and r['restored']
ordinary=read('windows-menu-debugger-01/result.json');assert ordinary['passed'] and ordinary['restored']
debug=read('windows-menu-debugger-01/debugger-smoke.json');assert debug['connected'] and debug['frozen']
upgrade=read('android-upgrade-01/upgrade.json')
assert upgrade['bytesIdentical'] and (upgrade['fromCode'],upgrade['toCode'])==(6,7)
assert upgrade['beforeSha256']==upgrade['afterSha256']
for tag,n in [('standard',0),('wide',1),('compact',2)]:
    assert read(f'android-growth-01/{tag}-save-before.json')['clicks']==n
    assert read(f'android-growth-01/{tag}-save-after.json')['clicks']==n+1
assert all(r['covered'] for r in read('android-growth-01/room-coverage.json').values())
package=read('distribution-v1.2.0/package-manifest.json')
assert package['allZipEntriesReadBack'] and package['entryCount']==153
assert package['apkSha256']==upgrade['apkSha256']
scope=read('distribution-v1.2.0/scope-audit.json')
assert not scope['forbiddenPaths'] and not scope['privateQaMarkers'] and scope['onlyTagSourceDifference']==['README.md']
manifest=json.loads((ROOT/'provenance.json').read_text(encoding='utf-8-sig'))
for row in manifest['files']:
    assert hashlib.sha256((ROOT/row['path']).read_bytes()).hexdigest()==row['publishedSha256'],row['path']
    assert 'windows-user-save-backup' not in row['path'] and '/registry/' not in row['path']
release=BASE/'distribution-v1.2.0/release-verified.json'
if release.exists():
    r=json.loads(release.read_text(encoding='utf-8-sig'))
    assert r['tag_name']=='v1.2.0' and not r['draft'] and r['tagCommit']==package['releaseSource']
    assets={a['name']:a for a in r['assets']}
    assert assets['CatGame-Android.apk']['digest']=='sha256:'+package['apkSha256']
    assert assets['CatGame-Windows-x64-v1.2.0.zip']['digest']=='sha256:'+package['zipSha256']
cleanup=read('cleanup.json');assert cleanup['shutdownComplete'] and not cleanup['remainingOwnedProcesses']
assert not cleanup['phoneOperated'] and not cleanup['mumuOperated']
print('PASS S1 scope/source, engine, qualified Windows input/restart, ordinary startup/debugger, Android upgrade/touches, retained failures, package and cleanup')
print('Manifest SHA256 verified:',len(manifest['files']),'files; F1 history retained. This is a consistency check, not a test rerun.')
