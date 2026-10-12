#!/usr/bin/env python3
"""Wraith Living Wall: vanilla-like wall construction with Stargate Wraith skin.

Authoritative art source for the Wraith settlement wall only. The 16 native
NESW linked states share the same detailed sculpted chitin / living membrane
surface. A raised lip and dark face appear ONLY on exposed wall edges,
never at wall-to-wall joins. No centre emblems or free-standing sprite shapes.

192px sampled tiles / 32px UV gutters / 256px atlas cells / 1024px atlas.
In-game ThingDef is an unchanged 1x1 Wall with CornerFiller, Wall/Rock links,
roof/room/door collision, regeneration, and other vanilla mechanics.

New original material: ArtSource/Walls/Wraith/OriginalWraithMaterial.svg.
Never use earlier portrait-like image generations, ornaments or procedural
striped experiments. All other wall families remain unchanged.
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
    """Render newly painted original chitin/membrane, NEVER old PNG/SVG art."""
    import cairosvg
    import io
    material = ROOT / "ArtSource/Walls/Wraith/OriginalWraithMaterial.svg"
    if not material.is_file():
        raise FileNotFoundError("New original Wraith wall master missing: " + str(material))
    raw = cairosvg.svg2png(bytestring=material.read_bytes(),
                          output_width=768, output_height=768)
    image = Image.open(io.BytesIO(raw)).convert("RGBA").resize(
        (S, S), Image.Resampling.LANCZOS)
    # All linked faces must have the *same* actual material at their borders.
    # Feather artwork only in the extreme 7px band. Never fabricate a framed
    # endpiece or use cropped old decorative medallions.
    rgba = np.asarray(image).copy()
    for i in range(7):
        alpha = i / 7.
        rgba[:, i, :3] = (rgba[:, i, :3] * alpha +
                          rgba[:, -8+i, :3] * (1-alpha)).astype(np.uint8)
    rgba[:, -7:, :3] = rgba[:, :7, :3]
    for i in range(7):
        alpha = i / 7.
        rgba[i, :, :3] = (rgba[i, :, :3] * alpha +
                          rgba[-8+i, :, :3] * (1-alpha)).astype(np.uint8)
    rgba[-7:, :, :3] = rgba[:7, :, :3]
    rgba[:, :, 3] = 255
    return Image.fromarray(rgba, "RGBA")


def linked_wall_tile(mask: int, material: Image.Image) -> Image.Image:
    """Native 1x1 wall slab; bone bevels only on *unlinked* external faces."""
    n, e, s, w = [not bool(mask & bit) for bit in (1, 2, 4, 8)]
    inset = 6
    left = inset if w else 0
    right = S-inset if e else S
    top = inset if n else 0
    bottom = S-inset if s else S
    out = Image.new("RGBA", (S, S))
    brush = ImageDraw.Draw(out)
    if n:
        brush.polygon([(left,top),(right,top),(right,top+7),(left,top+7)],fill=(171,145,153,142))
        brush.line([(left+1,top+2),(right-2,top+2)],fill=(221,189,177,192),width=2)
        brush.line([(left+1,top+9),(right-1,top+9)],fill=(32,24,39,144),width=3)
    if s:
        brush.polygon([(left,bottom-15),(right,bottom-15),(right,bottom),(left,bottom)],fill=(20,18,31,150))
        brush.line([(left+2,bottom-15),(right-2,bottom-15)],fill=(171,134,145,164),width=2)
        brush.line([(left+2,bottom-4),(right-2,bottom-4)],fill=(13,10,21,144),width=3)
    if w:
        brush.polygon([(left,top),(left+9,top),(left+9,bottom),(left,bottom)],fill=(173,151,149,100))
        brush.line([(left+2,top+2),(left+2,bottom-2)],fill=(215,189,169,161),width=2)
        brush.line([(left+11,top+1),(left+11,bottom-1)],fill=(32,26,41,109),width=3)
    if e:
        brush.polygon([(right-12,top),(right,top),(right,bottom),(right-12,bottom)],fill=(30,23,41,114))
        brush.line([(right-12,top+1),(right-12,bottom-1)],fill=(168,136,150,144),width=2)
        brush.line([(right-3,top+2),(right-3,bottom-2)],fill=(14,11,22,133),width=3)
    tile = np.asarray(Image.alpha_composite(material, out)).copy()
    if n: tile[:inset,:,3] = 0
    if s: tile[-inset:,:,3] = 0
    if w: tile[:,:inset,3] = 0
    if e: tile[:,-inset:,3] = 0
    return Image.fromarray(tile, "RGBA")


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
