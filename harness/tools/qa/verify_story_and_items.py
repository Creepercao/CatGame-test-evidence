"""Independent source/profile checks; no image transformations."""
import hashlib
import json
import sys
from pathlib import Path
from PIL import Image

root=Path(__file__).resolve().parents[2]
out=Path(sys.argv[1]); out.mkdir(parents=True,exist_ok=False)
profiles=json.loads((root/'Assets/Resources/Art/Items/Pixel/world_profiles.json').read_text())['profiles']
assert len(profiles)==20 and len({p['id'] for p in profiles})==20
for p in profiles:
    image=Image.open(root/f"Assets/Resources/Art/Items/Pixel/{p['id']}.png").convert('RGBA')
    left,top,right,bottom=image.getchannel('A').getbbox()
    assert (p['width'],p['height'],p['centerX'],p['bottom'])==(right-left,bottom-top,(left+right)/2,64-bottom), p['id']
records=[]
pairs=[('menu-momo-v1','MenuMomo_v1'),('momo-portrait-v1','MomoPortrait_v1'),
    ('milestone-three-v1','MilestoneThree_v1'),('milestone-six-v1','MilestoneSix_v1'),
    ('milestone-eleven-v1','MilestoneEleven_v1'),('milestone-planet-v1','MilestonePlanet_v1'),
    ('ending-rocket-v1','EndingRocket_v1')]
manifest=json.loads((root/'tools/story_art/achievements-v1.json').read_text(encoding='utf-8'))
assert len(manifest['assets'])==16
for asset in manifest['assets']:
    assert asset['status']=='installed', asset['id']
    pairs.append((asset['source'],asset['runtime']))
assert len({runtime for _,runtime in pairs})==23
for name,runtime in pairs:
    source=root/f'art/generated/story/{name}.png'
    target=root/f'Assets/Resources/Art/Story/{runtime}.png'
    assert source.read_bytes()==target.read_bytes()
    assert source.with_suffix('.prompt.md').exists()
    image=Image.open(source).convert('RGBA')
    alpha=image.getchannel('A')
    if name.startswith('momo-portrait'):
        assert alpha.getextrema()==(0,255), 'portrait needs genuine alpha, not a painted backdrop'
        transparent=alpha.histogram()[0]/(image.width*image.height)
        assert .15<transparent<.9
    else: assert alpha.getextrema()==(255,255)
    records.append(dict(source=str(source.relative_to(root)),runtime=str(target.relative_to(root)),size=image.size,
        alpha_range=alpha.getextrema(),sha256=hashlib.sha256(source.read_bytes()).hexdigest()))
assert len({record['sha256'] for record in records})==23, 'Each illustration must have independent image bytes'
catalog=(root/'Assets/Scripts/Idle/StoryCatalog.cs').read_text(encoding='utf-8')
for asset in manifest['assets']:
    assert f'new StoryDefinition("{asset["id"]}",' in catalog
    assert f'"{asset["runtime"]}")' in catalog
    assert hashlib.sha256((root/f'art/generated/story/{asset["source"]}.png').read_bytes()).hexdigest()==asset['sha256']
(out/'sources.json').write_text(json.dumps(dict(profiles=profiles,illustrations=records),indent=2),encoding='utf-8')
sys.path.insert(0,str(root/'tools/pixel_art'))
from pixel_art import load
rocket=load(root/'tools/pixel_art/output/rocket_v1/rocket.json')
assert rocket.size==64 and len(rocket.colors)<=16
assert rocket.png()==(root/'Assets/Resources/Art/Items/Pixel/rocket.png').read_bytes()
print('PASS: 20 opaque item profiles; 23 independent illustrations (22 active CGs + historical portrait) copied byte-identically with prompts; all 16 new achievement mappings; historical portrait alpha; 64px rocket JSON.')
