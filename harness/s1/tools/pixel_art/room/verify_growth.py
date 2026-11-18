"""Independent room-growth source, palette, floor and semantic grouping checks."""
import json
import sys
import unittest
from pathlib import Path
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]
sys.path.insert(0,str(ROOT/'tools/pixel_art'))
from pixel_art import load,load_palette
SOURCE=ROOT/'tools/pixel_art/output/room_growth_v1'
ASSETS=ROOT/'Assets/Resources/Art/Room/Pixel/Growth'
PALETTE=load_palette(ROOT/'tools/pixel_art/output/room_iso_v1/palette.json')

class GrowthTests(unittest.TestCase):
    def test_json_pixel_sources_exactly_match_each_runtime_prop(self):
        for theme in ('Starter','Companions'):
            layout=json.loads((ASSETS/theme/'layout.json').read_text())
            for name in {p['asset'] for p in layout['placements']}:
                art=load(SOURCE/theme/(name+'.json'),PALETTE)
                with Image.open(ASSETS/theme/(name+'.png')) as image:
                    self.assertEqual((64,64),image.size)
                    self.assertEqual(bytes(v for row in art.pixels for color in row for v in color),image.tobytes())

    def test_themes_share_floor_geometry_and_have_no_transparent_architecture(self):
        for theme in ('Starter','Companions'):
            grid=json.loads((SOURCE/theme/'architecture.json').read_text())
            layout=json.loads((ASSETS/theme/'layout.json').read_text())
            self.assertEqual((256,40,.36),(layout['cornerX'],layout['cornerY'],layout['wallSlope']))
            with Image.open(ASSETS/theme/'architecture.png') as image:
                self.assertEqual((512,288),image.size)
                for y in range(288):
                    for x in range(512):
                        c=image.getpixel((x,y));self.assertEqual(255,c[3])
                        self.assertEqual('#'+bytes(c[:3]).hex().upper(),grid[y][x])
        with Image.open(ASSETS/'Starter/architecture.png') as a,Image.open(ASSETS/'Companions/architecture.png') as b:
            self.assertEqual(a.crop((0,140,512,288)).tobytes(),b.crop((0,140,512,288)).tobytes())

    def test_all_visible_furniture_pixels_fit_and_split_groups_share_ground_depth(self):
        for theme in ('Starter','Companions'):
            layout=json.loads((ASSETS/theme/'layout.json').read_text());groups={}
            for p in layout['placements']:
                with Image.open(ASSETS/theme/(p['asset']+'.png')) as image:
                    x0,y0,x1,y1=image.getbbox()
                    self.assertGreaterEqual(p['x']+x0,0,p['asset']);self.assertGreaterEqual(p['y']+y0,0,p['asset'])
                    self.assertLessEqual(p['x']+x1,512,p['asset']);self.assertLessEqual(p['y']+y1,288,p['asset'])
                key=(p['groundX'],p['groundY'],p['wallDecor'])
                self.assertEqual(groups.setdefault(p['group'],key),key)
            for rect in layout['obstacles']:
                self.assertFalse(rect['x']<=315<rect['x']+rect['width'] and rect['y']<=151<rect['y']+rect['height'])

    def test_review_previews_use_integer_pixels_and_limited_palette(self):
        for theme in ('Starter','Companions'):
            with Image.open(SOURCE/theme/'preview-x2.png') as preview:
                self.assertEqual((1024,576),preview.size)
                self.assertLessEqual(len(preview.getcolors(preview.width*preview.height)),16)
                for y in range(0,576,2):
                    for x in range(0,1024,2):
                        self.assertEqual(1,len(preview.crop((x,y,x+2,y+2)).getcolors(4)))

if __name__=='__main__':unittest.main(verbosity=2)
