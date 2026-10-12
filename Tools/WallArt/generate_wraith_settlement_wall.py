#!/usr/bin/env python3
"""Author the WNG Wraith living WALL (not an ornament, pipe or fence).

Vector-drawn, full-size architectural slab with roof-facing chitin cap, organic
membrane and properly shaded exposed wall faces. Source is regenerated at 768px
per state, antialiased down to RimWorld's native 192px sample. The runtime
linked atlas is 1024px with 16 256px cells, a 32px clamped gutter, native
North=1, East=2, South=4, West=8 masks, and 1x1 in-world draw size.

Full-resolution editable SVG masters (one per mask) are generated alongside
the PNGs in ArtSource/Walls/Wraith/VectorMasters. No tiny WebP or AI concept
sprites are cropped, rotated or stretched to create game graphics.
"""
from __future__ import annotations
import argparse, io
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import cairosvg
from paint_wraith_chitin import extra_gradients, overlay_scutes

ROOT=Path(__file__).resolve().parents[2]
ATLAS=ROOT/'Textures/Things/Building/Wraith/Structure/WNG_LivingWall_Atlas.png'
ICON=ROOT/'Textures/UI/WNG/Build/WNG_WraithLivingWall.png'
LEGACY=ROOT/'Textures/Things/Building/Wraith/Structure/WNG_LivingWall.png'
HULL=ROOT/'Textures/Things/Building/Wraith/Gravship/WNG_OrganicHull_Atlas.png'
HULL_ICON=ROOT/'Textures/UI/WNG/Build/WNG_WraithGravshipHull.png'
MASTERS=ROOT/'ArtSource/Walls/Wraith/VectorMasters'

def path(d,fill='none',stroke='none',width=1,op=1):
    return f'<path d="{d}" fill="{fill}" stroke="{stroke}" stroke-width="{width}" opacity="{op}" stroke-linejoin="round" stroke-linecap="round"/>'

def svg_master(mask):
    n,e,s,w=[bool(mask & x) for x in (1,2,4,8)]
    x0=0 if w else 7; x1=192 if e else 185
    y0=0 if n else 7; y1=192 if s else 185
    tl=5 if not n and not w else 0
    tr=5 if not n and not e else 0
    br=5 if not s and not e else 0
    bl=5 if not s and not w else 0
    shell=(f'M {x0+tl} {y0} H {x1-tr} Q {x1} {y0} {x1} {y0+tr} '
           f'V {y1-br} Q {x1} {y1} {x1-br} {y1} H {x0+bl} '
           f'Q {x0} {y1} {x0} {y1-bl} V {y0+tl} Q {x0} {y0} {x0+tl} {y0} Z')
    bits=[f'<svg xmlns="http://www.w3.org/2000/svg" width="192" height="192" viewBox="0 0 192 192">',
          '<defs>',
          '<linearGradient id="roof" x1="0" y1="0" x2=".7" y2="1">'
          '<stop offset="0" stop-color="#695967"/><stop offset=".22" stop-color="#514354"/>'
          '<stop offset=".63" stop-color="#3c303f"/><stop offset="1" stop-color="#26222e"/></linearGradient>',
          '<linearGradient id="face" x1="0" y1="0" x2="0" y2="1">'
          '<stop offset="0" stop-color="#867583"/><stop offset=".28" stop-color="#4b3347"/>'
          '<stop offset="1" stop-color="#211a2b"/></linearGradient>',
          '<linearGradient id="chitin" x1="0" y1="0" x2="0" y2="1">'
          '<stop offset="0" stop-color="#ead2bc"/><stop offset=".2" stop-color="#cbb4a8"/>'
          '<stop offset=".56" stop-color="#76616c"/><stop offset="1" stop-color="#453748"/></linearGradient>',
          extra_gradients(),
          f'<clipPath id="shape"><path d="{shell}"/></clipPath>',
          '</defs>']
    bits.append(path(shell,fill='#211928',stroke='#17151f',width=3.6))
    bits.append(f'<g clip-path="url(#shape)"><rect width="192" height="192" fill="url(#roof)"/>')
    # Hand-composed large living keratin lamellae. These read as the *roof* of
    # a heavy wall, with organic folds and chitin undertones across the slab.
    for d,c,width,op in (
        ('M -28 34 C 12 15 42 53 87 39 S 157 17 219 39','#241f36',16,.24),
        ('M -27 49 C 21 32 52 67 88 54 S 165 31 220 49','#b292a1',4,.20),
        ('M -28 112 C 19 137 52 98 96 119 S 170 139 218 109','#281f35',18,.26),
        ('M -28 128 C 19 149 55 113 98 132 S 174 145 218 122','#af899d',3,.22),
        ('M 22 -26 C 51 17 38 48 57 81 S 81 148 49 219','#ae8b9c',6,.17),
        ('M 98 -25 C 81 12 120 45 111 78 S 133 147 100 218','#1e1c2e',14,.23),
        ('M 115 -26 C 97 14 135 40 129 78 S 150 148 115 219','#ac94a0',3,.22),
        ('M 172 -25 C 152 24 184 54 166 107 S 176 166 163 217','#262030',11,.19),
        ('M -18 177 Q 37 139 87 170 T 213 148','#967487',4,.15),
        ('M 0 2 C 54 29 79 0 123 30 S 189 1 209 23','#baa7ae',3,.15)
    ):bits.append(path(d,stroke=c,width=width,op=op))
    # Freehand membrane seams and oblique living plate divisions; no repeated
    # framed tile medallion and no decorative post at link boundaries.
    for d in ('M 39 54 l 12 9 l 18 -4','M 146 157 l -14 -8 l -15 4',
              'M 77 163 l 12 -5 l 8 9','M 165 49 l -11 8 l -3 14'):
        bits.append(path(d,stroke='#1e1a2d',width=4,op=.34))
        bits.append(path(d,stroke='#b1a0a7',width=1.2,op=.19))
    bits.extend(overlay_scutes(mask, path))
    bits.append('</g>')
    # Only EXPOSED sides receive bevels, lips, wall faces and chitin tendons.
    # Two adjacent linked cells have no repeated internal border.
    if not n:
        bits.extend([
            path(f'M {x0} {y0+18} C 31 {y0+13} 61 {y0+20} 96 {y0+15} '
                 f'S 157 {y0+12} {x1} {y0+16} L {x1} {y0} L {x0} {y0} Z',fill='url(#chitin)'),
            path(f'M {x0+2} {y0+5} C 40 {y0+3} 67 {y0+7} 99 {y0+4} '
                 f'S 154 {y0+5} {x1-2} {y0+4}',stroke='#edd6bd',width=3.5,op=.82),
            path(f'M {x0} {y0+17} C 47 {y0+14} 65 {y0+18} 96 {y0+15} '
                 f'S 149 {y0+13} {x1} {y0+16}',stroke='#302436',width=5.5,op=.90),
            path(f'M {x0+5} {y0+21} C 45 {y0+16} 75 {y0+20} 104 {y0+19} '
                 f'S 170 {y0+18} {x1-5} {y0+21}',stroke='#b29da8',width=2,op=.46)
        ])
    if not s:
        bits.extend([
            path(f'M {x0} {y1-21} C 41 {y1-24} 76 {y1-18} 107 {y1-21} '
                 f'S 169 {y1-18} {x1} {y1-20} L {x1} {y1} L {x0} {y1} Z',fill='url(#face)'),
            path(f'M {x0+1} {y1-20} C 42 {y1-22} 76 {y1-17} 106 {y1-20} '
                 f'S 167 {y1-18} {x1-2} {y1-20}',stroke='#bea5ac',width=3.2,op=.78),
            path(f'M {x0+1} {y1-12} C 42 {y1-13} 75 {y1-10} 108 {y1-13} '
                 f'S 165 {y1-12} {x1-2} {y1-13}',stroke='#1a1727',width=5,op=.84),
            path(f'M {x0+2} {y1-4} L {x1-2} {y1-4}',stroke='#b3939f',width=2,op=.46)
        ])
    if not w:
        bits.extend([
            path(f'M {x0} {y0} L {x0+15} {y0} C {x0+14} 40 {x0+18} 71 {x0+15} 97 '
                 f'S {x0+15} 154 {x0+14} {y1} L {x0} {y1} Z',fill='url(#chitin)'),
            path(f'M {x0+3} {y0+3} C {x0+3} 48 {x0+6} 70 {x0+4} 96 '
                 f'S {x0+4} 156 {x0+3} {y1-4}',stroke='#e5c9ae',width=3,op=.8),
            path(f'M {x0+15} {y0} C {x0+14} 52 {x0+17} 77 {x0+15} 100 '
                 f'S {x0+15} 158 {x0+14} {y1}',stroke='#302539',width=5,op=.77)
        ])
    if not e:
        bits.extend([
            path(f'M {x1-20} {y0} L {x1} {y0} L {x1} {y1} L {x1-20} {y1} '
                 f'C {x1-17} 147 {x1-15} 131 {x1-18} 97 S {x1-17} 37 {x1-20} {y0} Z',fill='url(#face)'),
            path(f'M {x1-19} {y0+2} C {x1-16} 45 {x1-15} 67 {x1-18} 98 '
                 f'S {x1-17} 154 {x1-19} {y1-2}',stroke='#bda1a9',width=3.5,op=.6),
            path(f'M {x1-5} {y0+3} L {x1-5} {y1-3}',stroke='#2b2231',width=5,op=.73)
        ])
    # Four structurally growing corner plates, recessed into cap rather than
    # huge isolated decorative prongs. Nothing protrudes beyond one wall cell.
    if not n and not w:
        bits.append(path(f'M {x0+3} {y0+29} Q {x0+14} {y0+7} {x0+31} {y0+4} '
                         f'Q {x0+19} {y0+17} {x0+18} {y0+32} Z',fill='#bda5a0',op=.78))
    if not n and not e:
        bits.append(path(f'M {x1-30} {y0+4} Q {x1-12} {y0+7} {x1-3} {y0+28} '
                         f'L {x1-17} {y0+26} Z',fill='#b39b9b',op=.78))
    if not s and not w:
        bits.append(path(f'M {x0+4} {y1-27} Q {x0+11} {y1-7} {x0+32} {y1-4} '
                         f'Q {x0+16} {y1-15} {x0+18} {y1-27} Z',fill='#65495e',op=.8))
    if not s and not e:
        bits.append(path(f'M {x1-32} {y1-5} Q {x1-11} {y1-8} {x1-3} {y1-28} '
                         f'L {x1-17} {y1-26} Z',fill='#61485c',op=.75))
    # Very restrained bioluminescent scars, legible as organic material rather
    # than a green light border around every tile.
    if not n:
        bits.append(path(f'M 51 {y0+17} Q 57 {y0+20} 63 {y0+15}',stroke='#9ab48e',width=2,op=.45))
    if not s:
        bits.append(path(f'M 107 {y1-18} Q 112 {y1-11} 119 {y1-18}',stroke='#8ca68a',width=2,op=.42))
    if not e:
        bits.append(path(f'M {x1-18} 71 Q {x1-12} 76 {x1-18} 82',stroke='#89a080',width=2,op=.4))
    bits.append('</svg>')
    return '\n'.join(bits)

def save(image,p):
    p.parent.mkdir(parents=True,exist_ok=True)
    image.save(p,format='PNG',optimize=True)

def generate():
    MASTERS.mkdir(parents=True,exist_ok=True)
    tiles=[]
    for mask in range(16):
        svg=svg_master(mask)
        (MASTERS/f'WraithLivingWall_link{mask:02}.svg').write_text(svg,encoding='utf-8')
        full=Image.open(io.BytesIO(cairosvg.svg2png(bytestring=svg.encode('utf-8'),
                output_width=768,output_height=768))).convert('RGBA')
        tiles.append(full.resize((192,192),Image.Resampling.LANCZOS))
    material=np.asarray(tiles[15]).copy()
    updated=[]
    # Exact boundary profiles along connected sides, regardless of wall topology.
    for mask,img in enumerate(tiles):
        a=np.array(img,dtype=np.uint8)
        if mask&1:a[0,:,:]=material[0,:,:]
        if mask&4:a[-1,:,:]=material[0,:,:]
        if mask&8:a[:,0,:]=material[:,0,:]
        if mask&2:a[:,-1,:]=material[:,0,:]
        updated.append(Image.fromarray(a,'RGBA'))
    atlas=Image.new('RGBA',(1024,1024))
    for mask,img in enumerate(updated):
        padded=np.pad(np.asarray(img),((32,32),(32,32),(0,0)),mode='edge')
        atlas.paste(Image.fromarray(padded,'RGBA'),((mask%4)*256,(3-mask//4)*256))
    return updated,atlas

def preview(tiles,folder):
    scene=["..#########........",
           "..#.......#........",
           "..#...#####........",
           "..#...#...#........",
           "..#...#...#........",
           "..#####...#........",
           "..#.......#........",
           "..#########........",
           "......#............",
           "....#####..........",
           "......#............"]
    size=48
    w=max(map(len,scene));h=len(scene)
    out=Image.new('RGBA',(size*w,size*h),(84,92,86,255))
    d=ImageDraw.Draw(out)
    for y in range(h):
        for x in range(w):
            c=(84,92,86,255) if (x+y)%2 else (91,98,92,255)
            d.rectangle((x*size,y*size,(x+1)*size-1,(y+1)*size-1),fill=c)
    for y,line in enumerate(scene):
        for x,c in enumerate(line):
            if c!='#':continue
            mask=sum(bit for bit,dx,dy in ((1,0,-1),(2,1,0),(4,0,1),(8,-1,0))
                     if 0<=y+dy<h and 0<=x+dx<len(scene[y+dy]) and scene[y+dy][x+dx]=='#')
            img=tiles[mask].resize((size,size),Image.Resampling.LANCZOS)
            out.alpha_composite(img,(x*size,y*size))
    save(out,folder/'Wraith-Living-Wall-Room-Test.png')
    sheet=Image.new('RGBA',(4*192,4*192))
    for m,img in enumerate(tiles):sheet.alpha_composite(img,((m%4)*192,(3-m//4)*192))
    save(sheet,folder/'Wraith-Living-Wall-16-States.png')

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--preview',type=Path)
    args=parser.parse_args()
    tiles,atlas=generate()
    save(atlas,ATLAS);save(tiles[0],ICON);save(tiles[0],LEGACY)
    if args.preview:preview(tiles,args.preview)
    if HULL.is_file():
        with Image.open(HULL) as img:
            save(img.convert('RGBA').crop((32,800,224,992)),HULL_ICON)
    coverage=np.array([np.mean(np.asarray(img)[:,:,3]>=250) for img in tiles])
    print('Wraith full-footprint painted living WALL:',ATLAS)
    print('isolated coverage',round(float(coverage[0]),4),
          'all-linked coverage',round(float(coverage[15]),4),
          'actual native in-game drawSize (1,1)')

if __name__=='__main__':main()
