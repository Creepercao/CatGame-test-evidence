"""Independent JSON-to-pixel and semantic-layout checks for the isometric prototype."""
import json
import sys
import unittest
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from pixel_art import load,load_palette
OUT=ROOT/'output/room_iso_v1'; ASSETS=OUT/'assets'


def rgba(c): return (0,0,0,0) if c is None else tuple(bytes.fromhex(c[1:]))+(255,)


class RoomTests(unittest.TestCase):
    def test_all_33_asset_grids_pngs_palettes_and_twenty_item_ids(self):
        inventory=json.loads((OUT/'inventory.json').read_text())
        self.assertEqual(33,len(inventory)); self.assertEqual(33,len({e['name'] for e in inventory}))
        item_names={e['name'] for e in inventory if e['kind']=='item'}
        self.assertEqual({f'item_{g}_{k}' for g in range(1,6) for k in range(1,5)},item_names)
        pictures={}
        for entry in inventory:
            name=entry['name']; art=load(ASSETS/f'{name}.json',load_palette(OUT/entry['palette']))
            self.assertEqual(64,art.size); self.assertEqual(entry['colors'],len(art.colors)); self.assertLessEqual(len(art.colors),16)
            with Image.open(ASSETS/f'{name}.png') as image:
                self.assertEqual((64,64),image.size); self.assertEqual('RGBA',image.mode)
                expected=bytes(v for row in art.pixels for color in row for v in color)
                self.assertEqual(expected,image.tobytes()); pictures[name]=image.tobytes()
                if entry['kind'] in ('item','prop'):
                    x0,y0,x1,y1=image.getbbox(); self.assertTrue(x0>=2 and y0>=2 and x1<=62 and y1<=62,name)
            if entry['kind']=='ui': self.assertLessEqual(set(art.colors),{(0,0,0,0),(255,255,255,255)})
        self.assertEqual(20,len({pictures[n] for n in item_names}),'Every catalog item should have its own material/variant art')
        upper=load(ASSETS/'bookshelf_upper.json'); lower=load(ASSETS/'bookshelf_lower.json')
        self.assertEqual([p[3] for p in upper.pixels[63]],[p[3] for p in lower.pixels[0]],'Cabinet join must have no alpha gap')

    def test_room_matches_raw_json_layers_without_clipped_furniture(self):
        layout=json.loads((OUT/'layout.json').read_text()); w,h=layout['width'],layout['height']
        self.assertEqual((512,288),(w,h)); self.assertEqual(16/9,w/h)
        self.assertEqual(['Architecture','BackWallFurniture'],[l['name'] for l in layout['layers']])
        a=layout['layers'][0]; self.assertEqual([256,40],a['corner']); self.assertEqual(.36,a['wall_slope'])
        grids={p.stem:json.loads(p.read_text()) for p in ASSETS.glob('*.json')}
        expected=[]
        for y in range(h):
            row=[]
            for x in range(w):
                edge=round(a['corner'][1]+abs(x-a['corner'][0])*a['wall_slope'])
                if y<edge:
                    c=grids[a['wall']][y%64][x%64]
                    if x<a['corner'][0]: c=a['left_remap'].get(c,c)
                elif y<edge+len(a['molding']): c=a['molding'][y-edge]
                else: c=grids[a['floor']][y%64][x%64]
                row.append(c)
            expected.append(row)
        with Image.open(OUT/'architecture.png') as architecture:
            self.assertEqual(bytes(v for row in expected for c in row for v in rgba(c)),architecture.tobytes())
        for p in layout['layers'][1]['placements']:
            source=grids[p['asset']]
            for sy in range(64):
                for sx in range(64):
                    c=source[sy][sx]
                    if c is None: continue
                    x,y=p['x']+sx,p['y']+sy
                    self.assertTrue(0<=x<w and 0<=y<h,p['asset'])
                    expected[y][x]=c
        with Image.open(OUT/'room.png') as image:
            self.assertEqual((w,h),image.size)
            self.assertEqual(bytes(v for row in expected for c in row for v in rgba(c)),image.tobytes())
            colors=set(image.get_flattened_data()) if hasattr(image,'get_flattened_data') else set(image.getdata())
            self.assertLessEqual(len(colors),16); self.assertEqual({255},{c[3] for c in colors})
            for y in range(140,h):
                for x in range(w): self.assertEqual(rgba(grids[a['floor']][y%64][x%64]),image.getpixel((x,y)))

    def test_integer_preview_and_original_prototype_assets_preserved(self):
        with Image.open(OUT/'room.png') as original,Image.open(OUT/'room.preview-x2.png') as preview:
            self.assertEqual((1024,576),preview.size)
            for y in range(576):
                for x in range(1024): self.assertEqual(original.getpixel((x//2,y//2)),preview.getpixel((x,y)))
        for entry in json.loads((ROOT/'output/room_v1/inventory.json').read_text()):
            for ext in ['json','png']:
                name=entry['name']+'.'+ext
                self.assertEqual((ROOT/'output/room_v1/assets'/name).read_bytes(),(ASSETS/name).read_bytes())


if __name__=='__main__': unittest.main(verbosity=2)
