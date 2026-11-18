"""Independent pixel reconstruction from raw JSON, not generator drawing functions."""
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[1]
out = root / 'output/catalog_park_v1'
sheet = Image.open(out / 'CatalogPark.png').convert('RGBA')
assert sheet.size == (256, 192)
colors = set()
for row in range(3):
    for col in range(4):
        name = f'tile_{row}_{col}'
        raw = json.loads((out / f'{name}.json').read_text())
        tile = Image.open(out / f'{name}.png').convert('RGBA')
        assert len(raw) == 64 and all(len(line) == 64 for line in raw)
        for y, line in enumerate(raw):
            for x, color in enumerate(line):
                expected = tuple(bytes.fromhex(color[1:])) + (255,)
                assert tile.getpixel((x,y)) == expected
                assert sheet.getpixel((col*64+x,row*64+y)) == expected
                colors.add(color)
assert len(colors) <= 16
assert Image.open(out/'preview-x3.png').tobytes() == sheet.resize((768,576),Image.Resampling.NEAREST).tobytes()
print(f'PASS: 12 tiles, 49152 source/composite pixels, {len(colors)} colors, exact 3x preview')
