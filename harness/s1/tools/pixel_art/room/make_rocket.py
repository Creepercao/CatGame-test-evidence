"""Author a separate 64x64, <=16-color JSON rocket icon using the existing pixel renderer."""
import json
import sys
from pathlib import Path

root=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(root))
from pixel_art import parse

out=root/'output/rocket_v1'
out.mkdir(parents=True,exist_ok=True)
grid=[[None]*64 for _ in range(64)]
ink='#5B4641'; cream='#F1DDC4'; pale='#FFF0D1'; coral='#BD8179'; gold='#E3BE92'; blue='#86AFB7'; dark='#527484'
def rect(x0,y0,x1,y1,color):
    for y in range(y0,y1+1):
        for x in range(x0,x1+1): grid[y][x]=color
def oval(cx,cy,rx,ry,color):
    for y in range(64):
        for x in range(64):
            if ((x-cx)/rx)**2+((y-cy)/ry)**2<=1: grid[y][x]=color
for y in range(34,55):
    extent=min(18,(y-30)//2)
    rect(32-extent,y,32+extent,y,ink)
    if y<52: rect(33-extent,y,31+extent,y,coral)
oval(32,30,12,24,ink); oval(32,29,10,21,cream)
for y in range(9,21):
    extent=min(9,(y-7)//2)
    rect(32-extent,y,32+extent,y,coral)
rect(25,42,39,45,coral); rect(24,46,40,48,ink); rect(27,46,37,47,gold)
oval(32,29,8,8,ink); oval(32,29,6,6,dark); oval(31,28,5,5,blue)
rect(28,25,30,27,pale); rect(25,21,26,23,pale)
rect(30,49,34,53,gold); rect(31,54,33,57,pale)
raw=json.dumps(grid,indent=0)+'\n'
art=parse(raw)
(out/'rocket.json').write_text(raw,encoding='utf-8')
(out/'rocket.png').write_bytes(art.png())
print(f'rocket: {art.size}x{art.size}, {len(art.colors)}/16 colors -> {out}')
