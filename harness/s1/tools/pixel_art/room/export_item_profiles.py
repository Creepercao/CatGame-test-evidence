"""Export opaque source bounds; runtime sprites need not be CPU-readable."""
import json
from pathlib import Path

root = Path(__file__).resolve().parents[3]
source = root / 'tools/pixel_art/output/room_iso_v1/assets'
profiles = []
for path in sorted(source.glob('item_*.json')):
    grid = json.loads(path.read_text())
    points = [(x,y) for y,row in enumerate(grid) for x,c in enumerate(row) if c is not None]
    left, right = min(x for x,y in points), max(x for x,y in points)+1
    top, bottom = min(y for x,y in points), max(y for x,y in points)+1
    profiles.append(dict(id=path.stem, width=right-left, height=bottom-top,
        centerX=(left+right)/2, bottom=64-bottom))
assert len(profiles) == 20
target = root / 'Assets/Resources/Art/Items/Pixel/world_profiles.json'
target.write_text(json.dumps(dict(profiles=profiles),indent=2)+'\n',encoding='utf-8')
print(f'Exported {len(profiles)} opaque bounds: {target}')
