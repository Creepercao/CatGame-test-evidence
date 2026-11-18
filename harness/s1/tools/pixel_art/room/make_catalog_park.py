"""Editable 64px JSON tiles for a 4:3 pixel garden background; no AI/image API."""
import json
import sys
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT))
from pixel_art import parse, load_palette

OUT = ROOT / 'output/catalog_park_v1'
OUT.mkdir(parents=True, exist_ok=True)
palette = ROOT / 'output/room_iso_v1/palette.json'
allowed = load_palette(palette)
grid = [['#8FA779'] * 256 for _ in range(192)]

def rect(x0, y0, x1, y1, color):
    for y in range(max(y0, 0), min(y1, 191) + 1):
        for x in range(max(x0, 0), min(x1, 255) + 1): grid[y][x] = color

def oval(cx, cy, rx, ry, color):
    for y in range(max(cy-ry, 0), min(cy+ry, 191)+1):
        for x in range(max(cx-rx, 0), min(cx+rx, 255)+1):
            if (x-cx)**2 * ry**2 + (y-cy)**2 * rx**2 <= rx**2 * ry**2: grid[y][x] = color

rect(0, 0, 255, 48, '#C1D8D3')
rect(0, 48, 255, 57, '#F1DDC4')
rect(0, 58, 255, 60, '#94735C')
for x in range(0, 256, 16): rect(x, 44, x+2, 65, '#C39A72')
for y in range(64, 192):
    for x in range(256):
        if abs(y - (150-x*.2)) < 12:
            grid[y][x] = '#C39A72' if (x//14 + y//6)%3 == 0 else '#E3BE92'
        elif (x*13+y*7)%97 < 2: grid[y][x] = '#B89F66'
for cx in (16, 238):
    rect(cx-3, 24, cx+3, 100, '#94735C')
    rect(cx, 24, cx+2, 100, '#C39A72')
    for dx, dy, rx, ry in ((0,0,30,22),(-15,15,22,19),(15,15,22,19),(0,29,28,18)):
        oval(cx+dx, 12+dy, rx, ry, '#536B54')
        oval(cx+dx-2, 9+dy, rx-3, ry-3, '#8FA779')
for x in (17, 234):
    for y in (113, 165, 179):
        oval(x, y, 13, 7, '#536B54')
        oval(x-2, y-2, 11, 6, '#8FA779')
        rect(x-5, y-4, x-3, y-2, '#BD8179')
        rect(x+3, y-2, x+5, y, '#FFF0D1')
rect(180, 73, 221, 88, '#5B4641')
for y in (74, 79, 84): rect(181, y, 220, y+2, '#C39A72')
rect(178, 91, 223, 95, '#94735C')
rect(181, 96, 184, 103, '#5B4641'); rect(217, 96, 220, 103, '#5B4641')
sheet = Image.new('RGBA', (256, 192))
for row in range(3):
    for col in range(4):
        tile = [line[col*64:(col+1)*64] for line in grid[row*64:(row+1)*64]]
        raw = json.dumps(tile, indent=0) + '\n'
        art = parse(raw, allowed)
        name = f'tile_{row}_{col}'
        (OUT / f'{name}.json').write_text(raw, encoding='utf-8')
        (OUT / f'{name}.png').write_bytes(art.png())
        sheet.paste(Image.open(OUT / f'{name}.png'), (col*64, row*64))
sheet.save(OUT / 'CatalogPark.png')
sheet.resize((768,576), Image.Resampling.NEAREST).save(OUT / 'preview-x3.png')
print(f'Wrote 12 full JSON tiles and 256x192 background: {OUT}')
