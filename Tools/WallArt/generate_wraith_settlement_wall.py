#!/usr/bin/env python3
"""Wraith Living Wall: vanilla-like wall construction with Stargate Wraith skin.

Authoritative art source for the Wraith settlement wall only. The 16 native
NESW linked states share the same detailed sculpted chitin / living membrane
surface. A raised lip and dark face appear ONLY on exposed wall edges,
never at wall-to-wall joins. No centre emblems or free-standing sprite shapes.

192px sampled tiles / 32px UV gutters / 256px atlas cells / 1024px atlas.
In-game ThingDef is an unchanged 1x1 Wall with CornerFiller, Wall/Rock links,
roof/room/door collision, regeneration, and other vanilla mechanics.

This source replaces rejected earlier artist experiments; it does NOT modify
Wraith gravship hulls, Asuran or Goa'uld art.
"""
from __future__ import annotations
import argparse
import math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ATLAS = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall_Atlas.png"
ICON = ROOT / "Textures/UI/WNG/Build/WNG_WraithLivingWall.png"
LEGACY = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall.png"
S, STRIDE, INSET, OPEN_MARGIN = 192, 256, 32, 6


def save(image: Image.Image, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    image.save(dest, format="PNG", optimize=True)


def membrane_material() -> Image.Image:
    yy, xx = np.mgrid[0:S, 0:S].astype(np.float32)
    x, y = xx / (S - 1), yy / (S - 1)
    grain = (
        .40*np.sin(2*np.pi*(x*.75+y*.35+.12))
        +.20*np.cos(2*np.pi*(x*.31-y*.68+.41))
        +.14*np.sin(2*np.pi*(x*1.7-y*.17+.19))
        +.08*np.cos(2*np.pi*(x*2.4+y*1.9+.2))
    )
    r, g, b = 72.+grain*10, 56.+grain*7, 83.+grain*12

    # Anatomical Wraith living-hull language: calcified chitin ribs with
    # recessed shadow side and warmer ivory-lavender highlight.
    ribs = [
        (.12,.038,.022,2.1,20,11),
        (.31,.032,-.018,2.7,16,10),
        (.54,.041,.021,2.2,21,12),
        (.76,.036,-.016,2.9,18,10),
    ]
    for i, (center, width, lean, frequency, amplitude, shadow_value) in enumerate(ribs):
        spine = center + lean*np.sin(2*np.pi*(y*frequency+i*.19))
        shell = np.exp(-((x-spine)**2)/(2*width**2))
        shadow = np.exp(-((x-(spine+.028))**2)/(2*(width*.65)**2))
        root = np.exp(-((x-(spine-.023))**2)/(2*(width*.45)**2))
        r = r + shell*amplitude + root*7 - shadow*shadow_value
        g = g + shell*amplitude*.73 + root*5 - shadow*shadow_value*.70
        b = b + shell*amplitude*.85 + root*6 - shadow*shadow_value*.82
        highlight = np.exp(-((x-(spine-.010))**2)/(2*.0065**2))
        highlight *= .70+.18*np.cos(2*np.pi*(y*(2.8+i*.25)+i*.1))
        dark_seam = np.exp(-((x-(spine+.012))**2)/(2*.0065**2))
        dark_seam *= .75+.20*np.sin(2*np.pi*(y*(3.1+i*.17)+.25))
        r = r + highlight*24 - dark_seam*10
        g = g + highlight*21 - dark_seam*8
        b = b + highlight*15 - dark_seam*10

    # Growing membrane tension scars. Smaller than ribs, intentionally
    # subordinate to the vanilla wall's structural top surface.
    for k, center in enumerate([.12,.27,.44,.61,.78,.90]):
        arc = np.exp(-((y-(center+.014*np.sin(2*np.pi*(x*(1.65+k*.08)+k*.19))))**2)/(2*(.010+.001*(k%2))**2))
        r, g, b = r-arc*4.5, g-arc*3.8, b-arc*3.2
        glint = np.exp(-((y-(center-.008+.010*np.sin(2*np.pi*(x*(1.4+k*.09)+k*.27))))**2)/(2*.0038**2))
        r, g, b = r+glint*4.2, g+glint*3.8, b+glint*2.7

    # Restrained Wraith bio-luminescent vein work. These are part of the
    # material, not separate repeated green emblems.
    for center, phase, strength in [(.22,0.,.13),(.47,.7,.10),(.68,1.3,.09)]:
        spine = center+.014*np.sin(2*np.pi*(y*(4.2+strength*2)+phase))
        vein = np.exp(-((x-spine)**2)/(2*.0068**2))
        pulse = .42+.28*np.cos(2*np.pi*(y*(2.8+strength)+phase))
        g, r, b = (g+vein*pulse*56*strength*10,
                   r+vein*pulse*8*strength*10,
                   b+vein*pulse*5*strength*10)

    vignette = (x-.5)**2+(y-.5)**2
    r, g, b = r-vignette*13, g-vignette*10, b-vignette*12
    rgb = np.stack([np.clip(r,0,255),np.clip(g,0,255),
                    np.clip(b,0,255),np.full_like(r,255)],axis=2).astype(np.uint8)

    # Periodic colour on every linked seam. This is separate from masking
    # the exposed edges, which only happens in the 16 topology variants.
    for idx in range(3):
        rgb[:, -1-idx, :] = rgb[:, idx, :]
        rgb[-1-idx, :, :] = rgb[idx, :, :]
    material = Image.fromarray(rgb,"RGBA")
    d = ImageDraw.Draw(material)
    for points in [
        [(14,48),(25,42),(34,46)], [(29,130),(41,124),(52,126)],
        [(55,76),(63,72),(72,74)], [(83,30),(91,35),(98,31)],
        [(102,144),(111,138),(121,141)], [(128,54),(137,58),(145,54)],
        [(148,119),(159,114),(169,116)], [(112,90),(120,84),(129,86)]
    ]:
        d.line(points,fill=(31,23,37,126),width=4)
        d.line(points,fill=(173,152,152,98),width=1)
    for cx in [24,63,104,146,176]:
        d.arc((cx-11,4,cx+11,23),start=10,end=170,fill=(201,183,169,86),width=1)
    for cx in [18,49,91,131,171]:
        d.arc((cx-13,166,cx+13,188),start=190,end=350,fill=(110,86,99,76),width=1)
    return material


def linked_wall_tile(mask: int, skin: Image.Image) -> Image.Image:
    """Exposed edge bevels, using native 1-tile wall cap and link masks."""
    n, e, s, w = [not bool(mask & bit) for bit in (1,2,4,8)]
    l = OPEN_MARGIN if w else 0
    r = S-OPEN_MARGIN if e else S
    t = OPEN_MARGIN if n else 0
    b = S-OPEN_MARGIN if s else S
    face = Image.new("RGBA",(S,S))
    d = ImageDraw.Draw(face)
    if n:
        pts = [(l,t+6)]
        pts += [(xi,t+6+int(1.4*math.sin((xi-l)/17.))) for xi in range(l,r+1,8)]
        d.polygon([(l,t),(r,t),(r,t+6)]+pts[::-1]+[(l,t+6)],fill=(165,148,145,160))
        d.line(pts,fill=(224,209,187,182),width=2)
        d.line([(px,py+7) for px,py in pts],fill=(54,41,58,118),width=3)
    if s:
        pts = [(xi,b-7+int(1.3*math.sin((xi-l)/19.+.6))) for xi in range(l,r+1,8)]
        d.polygon(pts+[(r,b),(l,b)],fill=(29,22,36,150))
        d.line(pts,fill=(158,130,140,150),width=2)
        d.line([(px,min(b-1,py+7)) for px,py in pts],fill=(11,10,16,104),width=3)
    if w:
        pts = [(l+7+int(1.4*math.sin((yi-t)/18.+.2)),yi) for yi in range(t,b+1,8)]
        d.polygon([(l,t),(l,b)]+pts[::-1],fill=(166,145,140,92))
        d.line(pts,fill=(208,190,170,138),width=2)
        d.line([(min(r-1,px+7),py) for px,py in pts],fill=(47,35,47,110),width=3)
    if e:
        pts = [(r-7+int(1.4*math.sin((yi-t)/18.+1.1)),yi) for yi in range(t,b+1,8)]
        d.polygon(pts+[(r,b),(r,t)],fill=(27,22,34,124))
        d.line(pts,fill=(146,121,134,136),width=2)
        d.line([(px+7,py) for px,py in pts if px+7<r],fill=(10,9,15,104),width=3)

    # Small separate calcified corner plates. They do not form a panel frame.
    if n and w:
        d.polygon([(l+1,t+18),(l+14,t+2),(l+27,t+4),(l+12,t+19)],fill=(203,188,174,96))
    if n and e:
        d.polygon([(r-18,t+2),(r-2,t+18),(r-13,t+19),(r-27,t+4)],fill=(203,188,174,96))
    if s and w:
        d.polygon([(l+1,b-18),(l+14,b-2),(l+12,b-19),(l+27,b-14)],fill=(99,78,92,74))
    if s and e:
        d.polygon([(r-18,b-2),(r-2,b-18),(r-27,b-14),(r-13,b-20)],fill=(99,78,92,74))

    tile = np.asarray(Image.alpha_composite(skin,face)).copy()
    tile[:,:,3]=255
    if n: tile[:OPEN_MARGIN,:,3]=0
    if e: tile[:,-OPEN_MARGIN:,3]=0
    if s: tile[-OPEN_MARGIN:,:,3]=0
    if w: tile[:,:OPEN_MARGIN,3]=0
    return Image.fromarray(tile,"RGBA")


def generate() -> list[Image.Image]:
    skin=membrane_material()
    tiles=[linked_wall_tile(mask,skin) for mask in range(16)]

    # All connected boundaries share the same UV samples. Normalising the
    # outermost texels prevents a corner bevel from creating a stitching line
    # against a different mask, e.g. a Wraith/vanilla T-junction.
    common=np.asarray(tiles[15])
    result=[]
    for mask,im in enumerate(tiles):
        arr=np.asarray(im).copy()
        if mask & 1: arr[0,:,:]=common[0,:,:]
        if mask & 4: arr[-1,:,:]=common[0,:,:]
        if mask & 8: arr[:,0,:]=common[:,0,:]
        if mask & 2: arr[:,-1,:]=common[:,0,:]
        result.append(Image.fromarray(arr,"RGBA"))

    atlas=Image.new("RGBA",(1024,1024))
    for mask,tile in enumerate(result):
        clamped=np.pad(np.asarray(tile),((INSET,INSET),(INSET,INSET),(0,0)),mode="edge")
        atlas.paste(Image.fromarray(clamped,"RGBA"),((mask%4)*STRIDE,(3-mask//4)*STRIDE))
    save(atlas,ATLAS)
    save(result[0],ICON)
    save(result[0],LEGACY)
    return result


def preview(states: list[Image.Image],folder: Path) -> None:
    folder.mkdir(parents=True,exist_ok=True)
    scene=[
      "..#########.......",
      "..#.......#.......",
      "..#.####..#.......",
      "..#.#..#..#.......",
      "..#.####..#.......",
      "..#.......#.......",
      "..#########.......",
      "......#...........",
      "....#####.........",
      "......#...........",
    ]
    ts=48
    out=Image.new("RGBA",(len(scene[0])*ts,len(scene)*ts),(84,91,84,255))
    d=ImageDraw.Draw(out)
    for y,line in enumerate(scene):
        for x in range(len(line)):
            d.rectangle((x*ts,y*ts,(x+1)*ts-1,(y+1)*ts-1),
                       fill=(88,94,88,255) if (x+y)%2==0 else (82,88,82,255))
    for y,line in enumerate(scene):
        for x,c in enumerate(line):
            if c!="#": continue
            links=sum(bit for bit,dx,dy in ((1,0,-1),(2,1,0),(4,0,1),(8,-1,0))
                      if 0<=y+dy<len(scene) and 0<=x+dx<len(scene[y+dy])
                      and scene[y+dy][x+dx]=="#")
            out.alpha_composite(states[links].resize((ts,ts),Image.Resampling.LANCZOS),
                                (x*ts,y*ts))
    save(out,folder/"Wraith-Living-Wall-Room-Test.png")
    sheet=Image.new("RGBA",(4*S,4*S))
    for mask,t in enumerate(states):
        sheet.alpha_composite(t,((mask%4)*S,(3-mask//4)*S))
    save(sheet,folder/"Wraith-Living-Wall-16-States.png")


def main() -> None:
    parser=argparse.ArgumentParser()
    parser.add_argument("--preview",type=Path)
    args=parser.parse_args()
    states=generate()
    if args.preview: preview(states,args.preview)
    coverage=[float((np.asarray(t)[:,:,3]>=250).mean()) for t in states]
    assert len(states)==16
    assert coverage[0]>=.875,coverage[0]
    assert coverage[15]>=.995,coverage[15]
    print("Wraith settlement mod atlas committed path:",ATLAS)
    print("Coverage isolated:",round(coverage[0],4),"all linked:",round(coverage[15],4))


if __name__=="__main__":
    main()
