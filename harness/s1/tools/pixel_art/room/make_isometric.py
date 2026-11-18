"""Preserve the existing room's two-wall corner using editable 64px grid pieces."""
import json
import shutil
import sys
from pathlib import Path
from PIL import Image,ImageDraw

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
from pixel_art import parse,load_palette
SOURCE=ROOT/'output/room_v1'; OUT=ROOT/'output/room_iso_v1'; ASSETS=OUT/'assets'
ASSETS.mkdir(parents=True,exist_ok=True)
for name in ['palette.json','ui-palette.json']: shutil.copyfile(SOURCE/name,OUT/name)
inventory=json.loads((SOURCE/'inventory.json').read_text())
for entry in inventory:
    for ext in ['json','png']: shutil.copyfile(SOURCE/'assets'/f"{entry['name']}.{ext}",ASSETS/f"{entry['name']}.{ext}")
INK,WD,WOOD,WL,CREAM,PALE,WS,FLOOR='#5B4641','#94735C','#C39A72','#E3BE92','#F1DDC4','#FFF0D1','#D0B49C','#D4AB7E'
allowed=load_palette(OUT/'palette.json')


def save(name,grid,kind):
    raw='[\n'+',\n'.join('  '+json.dumps(row) for row in grid)+'\n]\n'; art=parse(raw,allowed)
    (ASSETS/f'{name}.json').write_text(raw,encoding='utf-8'); (ASSETS/f'{name}.png').write_bytes(art.png())
    inventory.append(dict(name=name,kind=kind,width=64,height=64,colors=len(art.colors),palette='palette.json'))


wall=json.loads((ASSETS/'wall.json').read_text())
plain=[wall[y%46][:] for y in range(64)]
save('wall_plain',plain,'tile')
cabinet=[[None]*64 for _ in range(128)]
vertices=[(7,12),(49,5),(56,9),(56,116),(14,123),(7,119)]
points=set()
for y in range(128):
    for x in range(64):
        inside=False; ax,ay=vertices[-1]
        for bx,by in vertices:
            if (ay>y+.5)!=(by>y+.5) and x+.5<(bx-ax)*(y+.5-ay)/(by-ay)+ax: inside=not inside
            ax,ay=bx,by
        if inside: points.add((x,y))
for x,y in points:
    cabinet[y][x]=INK if any((x+dx,y+dy) not in points for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]) else WOOD
def rect(x0,y0,x1,y1,color):
    for y in range(y0,y1+1):
        for x in range(x0,x1+1): cabinet[y][x]=color
rect(14,16,49,113,WD)
for y in (31,53,75,97):
    rect(14,y,49,y+1,WL); rect(14,y+2,49,y+3,INK)
for row,y in enumerate((17,39,61,83)):
    for i,color in enumerate(['#86AFB7','#BD8179','#8FA779','#B89F66']):
        x=17+i*8; top=y+(i+row)%3
        rect(x,top,x+5,y+12,color); rect(x+1,top+1,x+1,y+11,PALE)
for x in (15,33):
    rect(x,103,x+15,115,INK); rect(x+1,104,x+14,114,WOOD); rect(x+10,108,x+12,109,'#B89F66')
upper=cabinet[:64]; lower=cabinet[64:]
save('bookshelf_upper',upper,'prop_piece'); save('bookshelf_lower',lower,'prop_piece')

layout=dict(width=512,height=288,cell=64,view='existing-two-wall-corner',layers=[
    dict(name='Architecture',floor='floor',wall='wall_plain',corner=[256,40],wall_slope=.36,
         left_remap={CREAM:WS,WS:WOOD,PALE:CREAM},molding=[WD,WOOD,WL,WL]),
    dict(name='BackWallFurniture',placements=[
        dict(asset='bookshelf_upper',x=112,y=-2),dict(asset='bookshelf_lower',x=112,y=62),
        dict(asset='plant',x=184,y=10),dict(asset='cat_bed',x=252,y=10),
        dict(asset='window',x=352,y=-3),dict(asset='scratching_post',x=452,y=64)])])
(OUT/'layout.json').write_text(json.dumps(layout,indent=2)+'\n')
(OUT/'inventory.json').write_text(json.dumps(inventory,indent=2)+'\n')
floor=Image.open(ASSETS/'floor.png').convert('RGBA'); wall_image=Image.open(ASSETS/'wall_plain.png').convert('RGBA')
room=Image.new('RGBA',(512,288)); arch=layout['layers'][0]
rgba=lambda c:tuple(bytes.fromhex(c[1:]))+(255,)
for x in range(512):
    boundary=round(40+abs(x-256)*.36)
    for y in range(288):
        if y<boundary:
            color=wall_image.getpixel((x%64,y%64))
            if x<256:
                key='#'+''.join(f'{c:02X}' for c in color[:3]); color=rgba(arch['left_remap'].get(key,key))
        elif y<boundary+4: color=rgba(arch['molding'][y-boundary])
        else: color=floor.getpixel((x%64,y%64))
        room.putpixel((x,y),color)
room.save(OUT/'architecture.png')
for placement in layout['layers'][1]['placements']:
    image=Image.open(ASSETS/(placement['asset']+'.png')).convert('RGBA')
    box=image.getbbox()
    assert box[0]+placement['x']>=0 and box[1]+placement['y']>=0 and box[2]+placement['x']<=512 and box[3]+placement['y']<=288
    room.alpha_composite(image,(placement['x'],placement['y']))
room.save(OUT/'room.png'); room.resize((1024,576),Image.Resampling.NEAREST).save(OUT/'room.preview-x2.png')
board=Image.new('RGB',(1024,760),'#E9E4E0'); draw=ImageDraw.Draw(board)
for i,entry in enumerate(inventory):
    im=Image.open(ASSETS/(entry['name']+'.png')).convert('RGBA').resize((128,128),Image.Resampling.NEAREST)
    x,y=(i%8)*128,(i//8)*152; board.paste(im,(x,y+24),im); draw.text((x+4,y+6),entry['name'],fill=INK)
board.save(OUT/'contact-sheet.png')
preview=room.copy()
for name,x,y in [('milk',224,152),('tabby',342,144),('snow',390,212)]:
    preview.alpha_composite(Image.open(ROOT/f'output/{name}_actions_v1/idle-jump/Idle/00.png').convert('RGBA'),(x,y))
preview.resize((1024,576),Image.Resampling.NEAREST).save(OUT/'room-with-cats.preview-x2.png')
print('Generated isometric room: 33 editable 64px assets; no furniture clipping')
