"""Record hashes of delivered binaries and source/imported room art without modifying either."""
import argparse
import hashlib
import json
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument('--output', required=True)
a = p.parse_args()
root = Path(__file__).resolve().parents[2]
paths = [
    'Build/CatGame.exe', 'Build/CatGame_Data/Managed/CatGame.Runtime.dll', 'Build/CatGame_Data/resources.assets',
    'Build/QA/CatGameQA.exe', 'Build/QA/CatGameQA_Data/Managed/CatGame.Runtime.dll', 'Build/QA/CatGameQA_Data/resources.assets',
]
paths += [str(f.relative_to(root)).replace('\\', '/') for f in (root / 'Assets/Resources/Art/Room/Pixel').glob('*') if f.suffix in ('.png', '.json')]
paths += [str(f.relative_to(root)).replace('\\', '/') for f in (root / 'Assets/Resources/Art/Story').glob('*.png')]
profile = root / 'Assets/Resources/Art/Items/Pixel/world_profiles.json'
if profile.exists(): paths.append(str(profile.relative_to(root)).replace('\\', '/'))
paths += ['Assets/Resources/Art/Items/Pixel/rocket.png','tools/pixel_art/output/rocket_v1/rocket.json']
paths += [str(f.relative_to(root)).replace('\\', '/') for f in (root / 'art/generated/story').glob('*') if f.suffix == '.png' or f.name.endswith('.prompt.md')]
records = []
for name in paths:
    path = root / name
    records.append(dict(path=name, bytes=path.stat().st_size, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
out = root / a.output
with out.open('x', encoding='utf-8') as stream:
    json.dump(records, stream, indent=2)
print(f'Recorded {len(records)} files: {out}')
