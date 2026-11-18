"""64px JSON room/prop assets, a semantic layout and exact-pixel composition."""
import json
import sys
from pathlib import Path
from PIL import Image,ImageDraw

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from pixel_art import parse,load_palette
OUT=ROOT/'output/room_v1'; ASSETS=OUT/'assets'
ASSETS.mkdir(parents=True,exist_ok=True)
INK,WD,WOOD,WL,CREAM,PALE,WS,BD,BLUE,BL,GD,GREEN,CORAL,GOLD,FLOOR=(
 '#5B4641','#94735C','#C39A72','#E3BE92','#F1DDC4','#FFF0D1','#D0B49C',
 '#527484','#86AFB7','#C1D8D3','#536B54','#8FA779','#BD8179','#B89F66','#D4AB7E')
PALETTE=[None,INK,WD,WOOD,WL,CREAM,PALE,WS,BD,BLUE,BL,GD,GREEN,CORAL,GOLD,FLOOR]
(OUT/'palette.json').write_text(json.dumps(PALETTE,indent=2)+'\n')
(OUT/'ui-palette.json').write_text(json.dumps([None,'#FFFFFF'],indent=2)+'\n')
allowed=load_palette(OUT/'palette.json')
inventory=[]


class Grid:
    def __init__(self,color=None): self.pixels=[[color]*64 for _ in range(64)]
    def fill(self,points,color):
        for x,y in points:
            if 0<=x<64 and 0<=y<64: self.pixels[y][x]=color
    def rect(self,x0,y0,x1,y1,color): self.fill({(x,y) for y in range(y0,y1+1) for x in range(x0,x1+1)},color)
    def ellipse(self,cx,cy,rx,ry,color,outline=None):
        points={(x,y) for y in range(64) for x in range(64) if ((x-cx)/rx)**2+((y-cy)/ry)**2<=1}
        self.shape(points,color,outline)
    def poly(self,vertices,color,outline=None):
        points=set()
        for y in range(64):
            for x in range(64):
                inside=False; ax,ay=vertices[-1]
                for bx,by in vertices:
                    if (ay>y+.5)!=(by>y+.5) and x+.5<(bx-ax)*(y+.5-ay)/(by-ay)+ax: inside=not inside
                    ax,ay=bx,by
                if inside: points.add((x,y))
        self.shape(points,color,outline)
    def shape(self,points,color,outline):
        self.fill(points,outline or color)
        if outline:
            self.fill({p for p in points if all((p[0]+dx,p[1]+dy) in points for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)])},color)
    def line(self,x0,y0,x1,y1,color):
        dx,dy=abs(x1-x0),-abs(y1-y0); sx=1 if x0<x1 else -1; sy=1 if y0<y1 else -1; error=dx+dy
        while True:
            self.fill({(x0,y0)},color)
            if (x0,y0)==(x1,y1): break
            twice=2*error
            if twice>=dy: error+=dy; x0+=sx
            if twice<=dx: error+=dx; y0+=sy
    def save(self,name,kind='prop',ui=False):
        raw='[\n'+',\n'.join('  '+json.dumps(row) for row in self.pixels)+'\n]\n'
        art=parse(raw,load_palette(OUT/'ui-palette.json') if ui else allowed)
        (ASSETS/f'{name}.json').write_text(raw,encoding='utf-8')
        (ASSETS/f'{name}.png').write_bytes(art.png())
        inventory.append(dict(name=name,kind=kind,width=64,height=64,colors=len(art.colors),palette='ui-palette.json' if ui else 'palette.json'))


g=Grid(FLOOR)
for y in range(64):
    for x in range(64):
        phase=(x+2*y)%32
        g.pixels[y][x]=WD if phase in (0,1) or (y%16==0 and 3<=phase<=29) else WL if phase==2 else FLOOR
        if phase in (14,15,16) and y%16==7: g.pixels[y][x]=WOOD
g.save('floor','tile')
g=Grid(CREAM)
for x in range(0,64,16): g.line(x,0,x,45,WS); g.line(x+1,0,x+1,45,PALE)
g.rect(0,46,63,48,WL); g.rect(0,49,63,51,WOOD); g.rect(0,52,63,53,WD)
g.save('wall','tile')

g=Grid(); g.poly([(7,16),(49,11),(56,15),(56,57),(14,61),(7,58)],WOOD,INK)
g.poly([(13,18),(49,14),(50,55),(14,58)],WD,INK)
for y in (21,35,49):
    g.poly([(14,y),(49,y-3),(49,y),(14,y+3)],WL,INK)
for x,y,w,h,c in [(17,22,5,10,BLUE),(23,21,5,10,CORAL),(29,21,4,9,GREEN),(36,19,6,10,GOLD),
                  (17,39,9,7,BL),(28,37,7,8,CREAM),(39,36,6,10,BLUE)]:
    g.rect(x,y,x+w,y+h,c); g.line(x+1,y+1,x+1,y+h-1,PALE)
g.ellipse(24,53,7,3,CORAL,INK); g.ellipse(42,51,5,4,GREEN,INK)
g.save('bookshelf')

g=Grid(); g.ellipse(32,32,25,24,WOOD,INK); g.ellipse(32,31,21,20,BD,WD); g.ellipse(32,30,18,17,BL)
g.ellipse(26,23,9,4,PALE); g.ellipse(37,22,7,5,PALE)
g.poly([(17,40),(24,31),(32,39),(38,32),(48,41),(47,45),(18,45)],GREEN)
g.rect(30,12,33,50,WOOD); g.rect(13,29,50,32,WOOD); g.line(31,13,31,28,WL)
g.save('window')

g=Grid(); g.line(32,20,32,48,GD); g.line(31,21,31,47,WOOD)
for cx,cy,rx,ry in [(23,20,10,6),(40,18,10,7),(20,31,11,6),(42,29,11,7),(31,12,6,7)]:
    g.ellipse(cx,cy,rx,ry,GD,INK); g.ellipse(cx-1,cy-1,max(2,rx-2),max(2,ry-2),GREEN)
g.poly([(20,42),(44,42),(40,58),(24,58)],CORAL,INK); g.ellipse(32,43,13,4,WL,INK)
g.ellipse(32,43,10,2,WD); g.line(24,48,26,55,PALE); g.save('plant')

g=Grid(); g.ellipse(32,48,27,12,BD,INK); g.ellipse(32,42,26,13,BLUE,INK)
g.ellipse(32,40,21,9,BD); g.ellipse(31,40,18,7,CREAM); g.ellipse(28,38,13,4,PALE)
g.ellipse(32,49,24,7,BLUE,INK); g.ellipse(32,45,21,4,BLUE)
g.line(13,50,20,53,BL); g.line(44,50,49,47,BL); g.save('cat_bed')

g=Grid(); g.poly([(6,53),(30,46),(57,53),(33,61)],WOOD,INK); g.line(11,53,32,59,WL)
g.rect(29,24,37,52,WD); g.rect(29,24,34,52,WL)
for y in range(27,51,4): g.line(29,y,37,y+2,WOOD)
g.poly([(9,20),(32,13),(55,20),(32,28)],WL,INK); g.poly([(9,21),(32,29),(55,21),(55,25),(32,33),(9,25)],WOOD,INK)
g.line(32,28,52,21,PALE); g.save('scratching_post')

accents=[WOOD,BLUE,GREEN,CORAL,GOLD]
for tier,accent in enumerate(accents,1):
    for kind in range(1,5):
        g=Grid()
        if kind==1:
            g.poly([(9,42),(42,26),(56,36),(23,53)],accent,INK)
            g.poly([(9,43),(23,54),(56,37),(56,42),(23,59),(9,48)],WD,INK)
            for k in range(6): g.line(17+k*5,40-k*2,25+k*5,46-k*2,WL)
            g.line(12,43,23,51,PALE)
        elif kind==2:
            g.ellipse(32,44,26,14,BD,INK); g.ellipse(32,39,25,13,accent,INK)
            g.ellipse(32,38,19,8,CREAM); g.ellipse(29,36,13,4,PALE)
            g.ellipse(32,47,23,7,accent,INK); g.ellipse(32,43,20,4,accent)
        elif kind==3:
            g.poly([(17,18),(39,15),(46,20),(46,45),(38,50),(16,45)],accent,INK)
            g.rect(21,21,39,35,BL); g.rect(23,23,36,33,PALE)
            g.rect(20,36,42,43,WD); g.rect(23,38,38,41,INK)
            g.ellipse(34,50,21,9,WD,INK); g.ellipse(34,47,20,7,accent,INK)
            g.ellipse(34,46,15,4,CREAM)
            for x,y in [(27,45),(33,46),(39,45)]: g.rect(x,y,x+2,y+1,WOOD)
        else:
            g.ellipse(30,36,19,19,accent,INK)
            for dy in (-8,0,8): g.line(14,33+dy,40,44+dy,PALE)
            g.line(21,20,37,51,WD); g.line(30,18,45,40,WD)
            g.line(47,43,54,49,accent); g.line(54,49,51,56,accent); g.line(51,56,43,56,accent)
        # Material studs distinguish named variants without a stage/lock label.
        for n in range(tier): g.rect(24+n*4,55,25+n*4,56,PALE)
        g.save(f'item_{tier}_{kind}','item')

for name,cut in [('panel_soft',4),('panel_pill',10)]:
    g=Grid()
    for y in range(64):
        for x in range(64):
            distance=min(x,63-x)+min(y,63-y)
            if distance>=cut: g.pixels[y][x]='#FFFFFF'
    g.save(name,'ui',True)
g=Grid(); g.ellipse(31.5,31.5,30.5,30.5,'#FFFFFF'); g.save('circle','ui',True)

layout=dict(width=512,height=288,cell=64,layers=[
    dict(name='Architecture',floor='floor',wall='wall',wall_height=54),
    dict(name='BackWallFurniture',placements=[
        dict(asset='bookshelf',x=128,y=-7),dict(asset='cat_bed',x=200,y=-4),
        dict(asset='plant',x=272,y=-6),dict(asset='window',x=344,y=-3),dict(asset='scratching_post',x=416,y=-7)])])
(OUT/'layout.json').write_text(json.dumps(layout,indent=2)+'\n')
(OUT/'inventory.json').write_text(json.dumps(inventory,indent=2)+'\n')
floor=Image.open(ASSETS/'floor.png').convert('RGBA'); wall=Image.open(ASSETS/'wall.png').convert('RGBA')
room=Image.new('RGBA',(512,288))
for y in range(0,288,64):
    for x in range(0,512,64): room.paste(floor,(x,y))
for x in range(0,512,64): room.paste(wall.crop((0,0,64,54)),(x,0))
for placement in layout['layers'][1]['placements']:
    room.alpha_composite(Image.open(ASSETS/(placement['asset']+'.png')).convert('RGBA'),(placement['x'],placement['y']))
room.save(OUT/'room.png'); room.resize((1024,576),Image.Resampling.NEAREST).save(OUT/'room.preview-x2.png')
board=Image.new('RGB',(8*128,4*152),'#E9E4E0'); draw=ImageDraw.Draw(board)
for i,entry in enumerate(inventory):
    im=Image.open(ASSETS/(entry['name']+'.png')).convert('RGBA').resize((128,128),Image.Resampling.NEAREST)
    x,y=(i%8)*128,(i//8)*152
    board.paste(im,(x,y+24),im); draw.text((x+4,y+6),entry['name'],fill=INK)
board.save(OUT/'contact-sheet.png')
preview=room.copy()
for name,x,y in [('milk',224,152),('tabby',342,114),('snow',390,212)]:
    preview.alpha_composite(Image.open(ROOT/f'output/{name}_actions_v1/idle-jump/Idle/00.png').convert('RGBA'),(x,y))
preview.resize((1024,576),Image.Resampling.NEAREST).save(OUT/'room-with-cats.preview-x2.png')
print(f'Generated {len(inventory)} full 64px JSON assets, semantic layout and 512x288 room')
