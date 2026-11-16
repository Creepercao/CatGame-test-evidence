"""Curate F1 artifacts, preserve historical manifest rows, exclude user saves and debugger secrets."""
import hashlib
import json
import os
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REPO = ROOT / 'Build/TestEvidence'
LOCAL = ROOT / 'Build/F1'
manifest = json.loads((REPO / 'provenance.json').read_text(encoding='utf-8-sig'))
rows = {item['path']: item for item in manifest['files']}
text_suffixes = {'.json', '.jsonl', '.xml', '.csv', '.txt', '.log', '.cs', '.py', '.robot', '.md', '.asmdef'}
allowed = text_suffixes | {'.png'}

def publish(source, destination):
    raw = source.read_bytes()
    data = raw
    if source.suffix in text_suffixes:
        text = raw.decode('utf-8-sig')
        for old, new in [(str(ROOT), 'PROJECT_ROOT'), (ROOT.as_posix(), 'PROJECT_ROOT'),
                         ('C:/Users/USER', 'C:/Users/USER'), ('C:\\Users\\USER', 'C:\\Users\\USER'),
                         ('PYTHON_ROOT', 'PYTHON_ROOT'), ('PYTHON_ROOT', 'PYTHON_ROOT')]:
            text = text.replace(old.replace('\\', '\\\\'), new.replace('\\', '\\\\')).replace(old, new)
        text = re.sub(r'(?i)(["\']?(?:token|secret|authorization)["\']?\s*[:=]\s*["\'])[^"\'\r\n]+', r'\1REDACTED', text)
        host = os.environ.get('COMPUTERNAME')
        if host:
            text = re.sub(re.escape(host), 'HOST_NAME', text, flags=re.I)
        text = re.sub(r'(?i)(/session/|Appium session )([a-f0-9-]{36})', r'\1SESSION_ID', text)
        text = re.sub(r'(?i)((?:machine\s*id|serial number|accessToken|refreshToken)\s*[:=]\s*)\S+', r'\1REDACTED', text)
        text = re.sub(r'\b(?:192\.168|10\.0)\.\d+\.\d+\b', 'LAN_ADDRESS', text)
        data = text.encode('utf-8')
    target = REPO / destination
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(data)
    rows[destination] = dict(source=source.relative_to(ROOT).as_posix(), path=destination,
                             sourceSha256=hashlib.sha256(raw).hexdigest(),
                             publishedSha256=hashlib.sha256(data).hexdigest(), transformed=raw != data)

def directory(source, destination):
    for item in sorted(source.rglob('*')):
        if item.is_file() and item.suffix in allowed and not {'registry', '__pycache__'} & set(item.parts):
            publish(item, destination + '/' + item.relative_to(source).as_posix())

runs = ['baseline-01', 'contracts-red-01', 'contracts-red-02', 'contracts-green-01', 'contracts-green-02',
        'edit-full-01', 'edit-full-02', 'edit-full-03', 'play-full-01', 'play-full-02',
        'migration-01', 'import-after-01', 'native-grid-02', 'pointer-manual-02', 'visual-grid-01', 'visual-grid-02',
        'android-old-smoke-01', 'android-old-smoke-02', 'android-upgrade-01',
        'android-ending-01', 'android-ending-02', 'android-ending-03']
runs += [f'android-touch-{i:02}' for i in range(1, 10)]
runs += [f'android-safearea-{i:02}' for i in range(1, 8)]
for run in runs:
    directory(LOCAL / run, 'evidence/f1/' + run)
for name in ['migration-comparison.json', 'pure-source-staging.json', 'cleanup.json', 'privacy-scan.json',
             'appium-http-steps.txt', 'evidence-verification-failure.txt']:
    publish(LOCAL / name, 'evidence/f1/' + name)
for run in ['windows-release-probe-01', 'windows-release-probe-02']:
    for name in ['result.json', 'steps.json', 'debugger-smoke.json']:
        source = LOCAL / run / name
        if source.exists():
            publish(source, 'evidence/f1/' + run + '/' + name)
for run in ['windows-candidate-01', 'windows-candidate-02', 'android-candidate-01', 'android-candidate-02',
            'qa-grid-01', 'qa-grid-02', 'qa-visual-final']:
    for source in (LOCAL / run).glob('*'):
        if source.is_file() and source.suffix in text_suffixes:
            publish(source, 'evidence/f1/builds/' + run + '/' + source.name)
for name in ['Assets/Tests', 'Tests/robot/android', 'Tests/robot/art', 'tools/art_audit']:
    directory(ROOT / name, 'harness/f1/' + name)
for source in (ROOT / 'Assets/Scripts').glob('Player*.cs'):
    publish(source, 'harness/f1/Assets/Scripts/' + source.name)
for source in (ROOT / 'Assets/Editor').glob('F1*.cs'):
    publish(source, 'harness/f1/Assets/Editor/' + source.name)
for name in ['Assets/Editor/CatGameSetup.cs', 'Tests/robot/tests/player_visual.robot',
             'Assets/Scripts/CatActor.cs']:
    publish(ROOT / name, 'harness/f1/' + name)
for name in ['package-manifest.json', 'scope-audit.json', 'apksigner.txt', 'SHA256SUMS.txt', 'Install-Android.txt',
             'release-notes.md', 'release-draft.json', 'release-published.json', 'release-verified.json']:
    source = LOCAL / 'distribution-v1.1.0' / name
    if source.exists():
        publish(source, 'evidence/f1/distribution/' + name)
for src, dest in [('F1素材尺寸与像素显示统一方案.md', 'f1-plan.md'),
                  ('F1素材坐标与像素显示测试记录.md', 'f1-test-history.md'),
                  ('美术尺寸配置说明.md', 'f1-art-configuration.md'),
                  ('F1素材统一交付报告.md', 'f1-release-verification.md'),
                  ('版本与发行对应说明.md', 'version-management.md')]:
    publish(ROOT / 'docs' / src, 'reports/' + dest)
manifest['files'] = list(rows.values())
manifest['f1'] = dict(runtimeGameSource='f536871', testsSource='see source.txt / harness snapshot',
                      publicTagCommit='1b82df94157bbcf370421207ed1d342ac272bb48',
                      harnessSnapshotRootCommit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
                      originalUserSavePublished=False, debuggerRegistryPublished=False,
                      rawSuccessAndFailurePreserved=True)
# Match the existing manifest's explicit Windows newline convention, preserving historical diff readability.
(REPO / 'provenance.json').write_bytes((json.dumps(manifest, ensure_ascii=False, indent=2) + '\n').replace('\n','\r\n').encode())
print('Curated manifest files:', len(rows), 'F1 files:', sum('/f1/' in x for x in rows))
