"""Author room growth on the existing JSON pixel grid; never resample concept illustrations."""
import hashlib
import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
PIXEL = ROOT / 'tools/pixel_art'
sys.path.insert(0, str(PIXEL))
from pixel_art import parse, load_palette

SOURCE = PIXEL / 'output/room_iso_v1'
OUT = PIXEL / 'output/room_growth_v1'
DEST = ROOT / 'Assets/Resources/Art/Room/Pixel/Growth'
INK, WD, WOOD, WL, CREAM, PALE, WS, BD, BLUE, BL, GD, GREEN, CORAL, GOLD, FLOOR = (
    '#5B4641', '#94735C', '#C39A72', '#E3BE92', '#F1DDC4', '#FFF0D1', '#D0B49C',
    '#527484', '#86AFB7', '#C1D8D3', '#536B54', '#8FA779', '#BD8179', '#B89F66', '#D4AB7E')

class Grid:
    def __init__(self, w=64, h=64):
        self.w, self.h = w, h
        self.p = [[None] * w for _ in range(h)]

    def shape(self, points, color, outline=None):
        points = {(x,y) for x,y in points if 0 <= x < self.w and 0 <= y < self.h}
        for x,y in points:
            self.p[y][x] = outline if outline and any((x+dx,y+dy) not in points for dx,dy in ((1,0),(-1,0),(0,1),(0,-1))) else color

    def rect(self, x0,y0,x1,y1,color):
        self.shape(((x,y) for y in range(y0,y1+1) for x in range(x0,x1+1)),color)

    def ellipse(self,cx,cy,rx,ry,color,outline=None):
        self.shape(((x,y) for y in range(self.h) for x in range(self.w)
                    if ((x-cx)/rx)**2+((y-cy)/ry)**2 <= 1), color,outline)

    def poly(self, vertices, color, outline=None):
        points=[]
        for y in range(self.h):
            for x in range(self.w):
                inside=False; ax,ay=vertices[-1]
                for bx,by in vertices:
                    if (ay>y+.5)!=(by>y+.5) and x+.5<(bx-ax)*(y+.5-ay)/(by-ay)+ax:
                        inside=not inside
                    ax,ay=bx,by
                if inside: points.append((x,y))
        self.shape(points,color,outline)

def write_json(path, data):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_bytes((json.dumps(data,indent=2)+'\n').encode())

def author_props():
    result={}
    g=Grid(128,64)
    g.rect(12,48,17,60,INK);g.rect(104,48,109,60,INK)
    g.ellipse(60,31,51,23,CREAM,INK);g.rect(12,30,109,52,CREAM)
    g.rect(20,33,101,49,INK);g.rect(21,34,100,48,GREEN)
    for cx in (36,79):
        g.rect(cx-13,16,cx+13,35,INK);g.rect(cx-12,17,cx+12,34,CORAL if cx==36 else BL)
    for cx in (13,108):
        g.ellipse(cx,39,8,14,PALE,INK)
    g.rect(22,50,99,53,WOOD)
    result['sofa']=g
    g=Grid(128,64)
    g.ellipse(64,49,51,12,WOOD,INK)
    g.ellipse(64,39,48,18,GREEN,INK)
    g.ellipse(64,34,43,14,GD)
    g.ellipse(64,35,39,10,CREAM)
    for cx,col in ((46,CORAL),(79,BLUE)):
        g.rect(cx-9,18,cx+9,34,INK);g.rect(cx-8,19,cx+8,33,col)
    g.ellipse(64,48,46,8,GREEN,INK)
    g.ellipse(64,44,40,5,GREEN)
    result['double_bed']=g
    g=Grid(64,128)
    g.poly([(6,113),(31,105),(58,114),(33,124)],WOOD,INK)
    for x,y,h in ((17,35,77),(42,21,91)):
        g.rect(x,y,x+6,y+h,INK);g.rect(x+1,y,x+5,y+h-1,WL)
        for yy in range(y+4,y+h,5):g.rect(x+1,yy,x+5,yy,WOOD)
    for x,y in ((30,23),(20,71)):
        g.poly([(x-20,y),(x+1,y-7),(x+23,y),(x+1,y+9)],WL,INK)
        g.poly([(x-20,y+1),(x+1,y+10),(x+23,y+1),(x+23,y+5),(x+1,y+14),(x-20,y+5)],WOOD,INK)
    result['cat_tree']=g
    g=Grid();g.rect(14,8,49,43,INK);g.rect(15,9,48,42,WOOD);g.rect(18,12,45,39,PALE)
    g.ellipse(32,28,8,6,CORAL)
    for cx,cy in ((22,22),(28,17),(36,17),(42,22)):g.ellipse(cx,cy,3,4,CORAL)
    result['paw_picture']=g
    return result

def build():
    allowed=load_palette(SOURCE/'palette.json')
    wall=json.loads((SOURCE/'assets/wall_plain.json').read_text())
    floor=json.loads((SOURCE/'assets/floor.json').read_text())
    raw_props=author_props()
    inventory=[]
    for theme in ('Starter','Companions'):
        out=OUT/theme; prod=DEST/theme
        out.mkdir(parents=True,exist_ok=True);prod.mkdir(parents=True,exist_ok=True)
        grids={}
        for name in ('bookshelf_upper','bookshelf_lower','plant','cat_bed','window','scratching_post'):
            grids[name]=json.loads((SOURCE/'assets'/f'{name}.json').read_text())
        if theme=='Companions':
            for name,g in raw_props.items():
                for ty in range(g.h//64):
                    for tx in range(g.w//64):
                        key=f'{name}_{tx}_{ty}'
                        grids[key]=[row[tx*64:tx*64+64] for row in g.p[ty*64:ty*64+64]]
            # Curtains are authored into the window JSON grid, not painted over a screenshot.
            grid=grids['window']
            for y in range(6,54):
                for x in range(2,12):
                    if x <= 9-(y//12)%3:
                        grid[y][x]=INK if x==2 else (CREAM if x%3 else WL)
                        grid[y][63-x]=grid[y][x]
            for x in range(2,62):grid[3][x]=WOOD
        placements=[]
        def prop(name,x,y,group,gx=0,gy=0,wall_decor=False):
            placements.append(dict(asset=name,x=x,y=y,group=group,groundX=gx,groundY=gy,wallDecor=wall_decor))
        prop('bookshelf_upper',112,-2,'Bookshelf',144,125)
        prop('bookshelf_lower',112,62,'Bookshelf',144,125)
        prop('window',352,-3,'Window',wall_decor=True)
        if theme=='Starter':
            prop('plant',184,10,'Plant',216,68);prop('cat_bed',252,10,'Bed',284,70)
            prop('scratching_post',452,64,'CatTree',484,126)
            obstacles=[[118,110,52,15],[204,60,25,10],[257,61,54,11],[458,117,52,10]]
        else:
            prop('plant',256,10,'Plant',288,68)
            prop('paw_picture_0_0',212,-2,'PawPicture',wall_decor=True)
            for tx in range(2):
                prop(f'sofa_{tx}_0',160+64*tx,34,'Sofa',220,94)
                prop(f'double_bed_{tx}_0',272+64*tx,35,'Bed',336,94)
            for ty in range(2):prop(f'cat_tree_0_{ty}',452,8+64*ty,'CatTree',484,132)
            obstacles=[[118,110,52,15],[276,60,25,10],[170,86,104,10],[287,87,98,10],[458,124,52,12]]
        used={p['asset'] for p in placements}
        for name in sorted(used):
            grid=grids[name];raw=json.dumps(grid)
            art=parse(raw,allowed)
            write_json(out/f'{name}.json',grid)
            (prod/f'{name}.png').write_bytes(art.png())
        architecture=[]
        for y in range(288):
            row=[]
            for x in range(512):
                edge=round(40+abs(x-256)*.36)
                if y<edge:
                    c=wall[y%64][x%64]
                    if x<256:c={CREAM:WS,WS:WOOD,PALE:CREAM}.get(c,c)
                    if theme=='Companions' and edge-37<=y<edge-4:
                        c=GD if x%32==0 or y==edge-37 else (GREEN if x<256 else BL)
                elif y<edge+4:c=[WD,WOOD,WL,WL][y-edge]
                else:c=floor[y%64][x%64]
                row.append(c)
            architecture.append(row)
        write_json(out/'architecture.json',architecture)
        rgba=lambda c:tuple(bytes.fromhex(c[1:]))+(255,)
        image=Image.new('RGBA',(512,288));image.putdata([rgba(c) for row in architecture for c in row])
        image.save(prod/'architecture.png')
        preview=image.copy()
        # Composition is for review only; runtime still loads all prop pieces individually.
        for p in sorted(placements,key=lambda p:-1000 if p['wallDecor'] else p['groundY']):
            with Image.open(prod/(p['asset']+'.png')) as part:preview.alpha_composite(part,(p['x'],p['y']))
        preview.resize((1024,576),Image.Resampling.NEAREST).save(out/'preview-x2.png')
        layout=dict(width=512,height=288,cell=64,cornerX=256,cornerY=40,wallSlope=.36,placements=placements,
                    obstacles=[dict(x=x,y=y,width=w,height=h) for x,y,w,h in obstacles])
        write_json(prod/'layout.json',layout);write_json(out/'layout.json',layout)
        inventory.append(dict(id=theme,props=len(used),colors=len(preview.getcolors(preview.width*preview.height)),source=out.relative_to(ROOT).as_posix()))
    write_json(OUT/'inventory.json',inventory)
    print(json.dumps(inventory))

if __name__=='__main__':build()
