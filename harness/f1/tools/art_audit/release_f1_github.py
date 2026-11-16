"""Publish/verify a unified release using existing Git credentials only in child-process memory."""
import argparse
import hashlib
import json
import os
import subprocess
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('action', choices=['draft', 'publish', 'verify'])
args = p.parse_args()
root = Path(__file__).resolve().parents[2]
base = root / 'Build/F1/distribution-v1.1.0'
repo = root / 'Build/PublishSource'
tag = 'v1.1.0'
sha = '1b82df94157bbcf370421207ed1d342ac272bb48'
credentials = subprocess.run(['git', '-c', 'safe.directory='+repo.as_posix(), 'credential', 'fill'],
    input='protocol=https\nhost=github.com\n\n', text=True, capture_output=True, cwd=repo, check=True, timeout=30)
values = dict(line.split('=', 1) for line in credentials.stdout.splitlines() if '=' in line)
assert values.get('password'), 'Existing Git credential missing'
env = dict(os.environ, GH_TOKEN=values['password'], HTTPS_PROXY='http://127.0.0.1:7897')
env.pop('GH_DEBUG', None)
def gh(*argv, allow_failure=False):
    result = subprocess.run(['gh', *argv], cwd=repo, env=env, text=True, encoding='utf-8',
                            capture_output=True, timeout=300)
    if result.returncode and not allow_failure:
        raise RuntimeError(result.stderr[:1000])
    return result
assert gh('api','user','--jq','.login').stdout.strip().lower() == 'creepercao'
repository = 'Creepercao/CatGame'
names = ['CatGame-Windows-x64-v1.1.0.zip', 'CatGame-Android.apk', 'Install-Android.txt', 'SHA256SUMS.txt']
if args.action == 'draft':
    assert gh('release','view',tag,'--repo',repository,allow_failure=True).returncode != 0
    gh('release','create',tag,*[str(base/name) for name in names], '--repo',repository,
       '--verify-tag','--target',sha,'--draft','--title','喵屋 1.1.0 · Windows / Android 素材显示统一',
       '--notes-file',str(base/'release-notes.md'))
all_releases = json.loads(gh('api',f'repos/{repository}/releases').stdout)
current = next(item for item in all_releases if item['tag_name'] == tag)
assert current['target_commitish'] == sha
if args.action == 'publish':
    assert current['draft']
    gh('api','--method','PATCH',f"repos/{repository}/releases/{current['id']}",
       '-F','draft=false','-F','prerelease=false','-f','make_latest=true')
release = json.loads(gh('api',f"repos/{repository}/releases/{current['id']}").stdout)
assert release['draft'] == (args.action == 'draft') and not release['prerelease']
assets = []
for name in names:
    data = (base/name).read_bytes()
    asset = next(item for item in release['assets'] if item['name'] == name)
    assert asset['size'] == len(data) and asset['digest'] == 'sha256:'+hashlib.sha256(data).hexdigest(), name
    assets.append({k:asset[k] for k in ['name','size','digest','browser_download_url']})
record = {k:release[k] for k in ['tag_name','draft','prerelease','target_commitish','html_url']}
record['assets'] = assets
if args.action == 'verify':
    ref = json.loads(gh('api',f'repos/{repository}/git/ref/tags/{tag}').stdout)['object']
    if ref['type'] == 'tag':
        ref = json.loads(gh('api',f"repos/{repository}/git/tags/{ref['sha']}").stdout)['object']
    assert ref['type'] == 'commit' and ref['sha'] == sha
    record['tagCommit'] = ref['sha']
    latest = json.loads(gh('api',f'repos/{repository}/releases/latest').stdout)
    assert latest['tag_name'] == tag
    for old in ['v1.0.0','v1.0.1','android-v1.0.0-preview.1','android-v1.0.2-preview.2']:
        prior = json.loads(gh('api',f'repos/{repository}/releases/tags/{old}').stdout)
        assert not prior['draft']
    record.update(latestIsUnified=True, fourOldReleasesStillPublished=True)
filename = {'draft':'release-draft.json', 'publish':'release-published.json', 'verify':'release-verified.json'}[args.action]
(base/filename).write_bytes((json.dumps(record,ensure_ascii=False,indent=2)+'\n').encode())
print(json.dumps(record,ensure_ascii=False))
