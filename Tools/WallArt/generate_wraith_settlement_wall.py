#!/usr/bin/env python3
"""Assemble HAND-PAINTED Wraith walls as native RimWorld 1.6 linked-wall artwork.

Input is a curated painterly 288x288 source sheet in ArtSource/Walls/Wraith.
The 4x4 native atlas uses 256px cells, 32px UV padding and a 192px printable
area. Mask bits N=1,E=2,S=4,W=8; row index is 3-(mask//4).
This intentionally makes 16 different silhouettes, not one square plus rims.
"""
from __future__ import annotations
import argparse
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "ArtSource/Walls/Wraith/wraith_architectural_painting.webp"
ATLAS = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall_Atlas.png"
ICON = ROOT / "Textures/UI/WNG/Build/WNG_WraithLivingWall.png"
LEGACY = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall.png"
HULL_ATLAS = ROOT / "Textures/Things/Building/Wraith/Gravship/WNG_OrganicHull_Atlas.png"
HULL_ICON = ROOT / "Textures/UI/WNG/Build/WNG_WraithGravshipHull.png"
SIZE, STRIDE, INSET = 192, 256, 32
# Coordinates on original 1254px hand-painted reference, scaled to committed 288px source.
REGIONS = {
    "post": (28,20,206,263), "horizontal": (233,45,593,251),
    "horizontal2": (650,43,1015,253), "vertical": (50,274,188,554),
    "junction_up": (226,274,607,575), "junction_down": (642,610,1016,913),
    "cross": (287,606,617,915), "corner": (40,871,273,1198),
}
# Native mask -> painted piece, native 192px bounding box, reflect, rotation.
GEOMETRY = {
    0:("post",(36,16,156,176),False,0),
    1:("vertical",(50,-2,142,165),False,0),
    2:("horizontal",(26,52,195,147),False,0),
    3:("corner",(10,-8,199,198),False,0),
    4:("vertical",(50,30,142,194),False,180),
    5:("vertical",(50,-8,142,200),False,0),
    6:("corner",(0,-8,200,197),False,270),
    7:("junction_up",(-8,-7,201,200),False,270),
    8:("horizontal",(-3,52,166,147),True,0),
    9:("corner",(-8,-8,182,198),True,0),
    10:("horizontal",(-10,52,202,147),False,0),
    11:("junction_up",(-6,-7,200,201),False,0),
    12:("corner",(-8,0,198,199),False,90),
    13:("junction_up",(-8,-8,198,202),False,90),
    14:("junction_down",(-7,-7,201,201),False,0),
    15:("cross",(-9,-8,201,201),False,0),
}

def crop(image, name):
    w,h=image.size
    values=REGIONS[name]
    return image.crop((round(values[0]*w/1254),round(values[1]*h/1254),
                       round(values[2]*w/1254),round(values[3]*h/1254)))

def extract(image):
    """Remove reference-sheet black matte, preserving dark membrane and antialiasing."""
    rgba=image.convert("RGBA")
    rgb=np.asarray(image.convert("RGB")).astype(np.float32)
    alpha=np.uint8(np.clip((rgb.max(axis=2)-8)*255/18,0,255))
    a=Image.fromarray(alpha,"L").filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(.55))
    rgba.putalpha(a)
    return rgba

def painted_piece(image,name,bounds,flip,angle):
    part=extract(crop(image,name))
    if flip:part=ImageOps.mirror(part)
    if angle:part=part.rotate(angle,expand=True,resample=Image.Resampling.BICUBIC)
    return part.resize((bounds[2]-bounds[0],bounds[3]-bounds[1]),Image.Resampling.LANCZOS)

def connectors(image):
    h=extract(crop(image,"horizontal2"))
    horizontal=h.crop((h.width//3,5,2*h.width//3,h.height-4)).resize((54,98),Image.Resampling.LANCZOS)
    v=extract(crop(image,"vertical"))
    vertical=v.crop((3,v.height//3,v.width-3,2*v.height//3)).resize((99,55),Image.Resampling.LANCZOS)
    return horizontal,vertical

def native_seams(images):
    """Consistent connector ports at every E/W or N/S neighbour boundary."""
    h,v=np.array(images[10]),np.array(images[5])
    left,right=h[35:157,0:12,:],h[35:157,-12:,:]
    east=np.round((left.astype(float)+right[:,::-1].astype(float))/2).astype("uint8")
    top,bottom=v[:12,40:152,:],v[-12:,40:152,:]
    north=np.round((top.astype(float)+bottom[::-1,:,:].astype(float))/2).astype("uint8")
    arrays=[np.array(i,dtype=np.uint8).copy() for i in images]
    for mask,a in enumerate(arrays):
        for depth in range(8):
            fac=(8-depth)/8
            if mask&8:
                a[35:157,depth,:]=np.round(east[:,depth]*fac+a[35:157,depth,:]*(1-fac)).astype("uint8")
            if mask&2:
                a[35:157,SIZE-1-depth,:]=np.round(east[:,depth]*fac+a[35:157,SIZE-1-depth,:]*(1-fac)).astype("uint8")
            if mask&1:
                a[depth,40:152,:]=np.round(north[depth]*fac+a[depth,40:152,:]*(1-fac)).astype("uint8")
            if mask&4:
                a[SIZE-1-depth,40:152,:]=np.round(north[depth]*fac+a[SIZE-1-depth,40:152,:]*(1-fac)).astype("uint8")
    # Exact 1-pixel boundary profiles for every valid native neighbour pairing.
    for mask,a in enumerate(arrays):
        if mask&8:a[:,0,:]=arrays[10][:,0,:]
        if mask&2:a[:,-1,:]=arrays[10][:,0,:]
        if mask&1:a[0,:,:]=arrays[5][0,:,:]
        if mask&4:a[-1,:,:]=arrays[5][0,:,:]
    return [Image.fromarray(a,"RGBA") for a in arrays]

def build(image):
    horizontal,vertical=connectors(image)
    images=[]
    for mask in range(16):
        name,bounds,flip,angle=GEOMETRY[mask]
        sprite=painted_piece(image,name,bounds,flip,angle)
        extra=Image.new("RGBA",(SIZE+40,SIZE+40))
        extra.alpha_composite(sprite,(bounds[0]+20,bounds[1]+20))
        tile=extra.crop((20,20,SIZE+20,SIZE+20))
        if mask&8:tile.alpha_composite(horizontal,(0,47))
        if mask&2:tile.alpha_composite(ImageOps.mirror(horizontal),(SIZE-54,47))
        if mask&1:tile.alpha_composite(vertical,(46,0))
        if mask&4:tile.alpha_composite(ImageOps.flip(vertical),(46,SIZE-55))
        # An ornamental claw in a painted corner must never accidentally
        # create a cardinal connector on an unlinked side. Keep the raised
        # chitin terminal inside its own native map tile.
        px=np.array(tile,dtype=np.uint8).copy()
        ramp=np.linspace(0,1,13,dtype=np.float32)
        alpha=px[:,:,3].astype(np.float32)
        if not mask&1:alpha[:13,:]*=ramp[:,None]
        if not mask&2:alpha[:,-13:]*=ramp[None,::-1]
        if not mask&4:alpha[-13:,:]*=ramp[::-1,None]
        if not mask&8:alpha[:,:13]*=ramp[None,:]
        px[:,:,3]=np.rint(alpha).astype(np.uint8)
        images.append(Image.fromarray(px,"RGBA"))
    return native_seams(images)

def packed(tiles):
    output=Image.new("RGBA",(1024,1024))
    for mask,tile in enumerate(tiles):
        pixels=np.pad(np.asarray(tile),((INSET,INSET),(INSET,INSET),(0,0)),mode="edge")
        output.paste(Image.fromarray(pixels,"RGBA"),((mask%4)*STRIDE,(3-mask//4)*STRIDE))
    return output

def save(image,path):
    path.parent.mkdir(parents=True,exist_ok=True)
    image.save(path,format="PNG",optimize=True)

def preview(tiles,directory):
    # Deliberate real wall configurations rather than an isolated-sheet inspection.
    scene=["............","..########..","..#......#..","..#.####.#..",
           "..#.#..#.#..","..#.#..#.#..","..#.####.#..","..#......#..",
           "..########..","......#.....","....#####...","......#....."]
    out=Image.new("RGBA",(len(scene[0])*SIZE,len(scene)*SIZE),(49,50,52,255))
    for y,line in enumerate(scene):
        for x,ch in enumerate(line):
            if ch!="#":continue
            bits=0
            for bit,dx,dy in ((1,0,-1),(2,1,0),(4,0,1),(8,-1,0)):
                if 0<=y+dy<len(scene) and 0<=x+dx<len(line) and scene[y+dy][x+dx]=="#":bits|=bit
            out.alpha_composite(tiles[bits],(x*SIZE,y*SIZE))
    directory.mkdir(parents=True,exist_ok=True)
    save(out.resize((720,720),Image.Resampling.LANCZOS),directory/"Wraith-Living-Wall-Room-Test.png")

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--preview",type=Path)
    args=parser.parse_args()
    source=Image.open(ART).convert("RGB")
    tiles=build(source)
    save(packed(tiles),ATLAS)
    save(tiles[0],ICON)
    save(tiles[0],LEGACY)
    # Keep the Wraith gravship hull exactly as it was: distinct menu icon only.
    with Image.open(HULL_ATLAS) as hull:
        hull_icon=hull.convert("RGBA").crop((INSET,3*STRIDE+INSET,INSET+SIZE,3*STRIDE+INSET+SIZE))
    save(hull_icon,HULL_ICON)
    if args.preview:preview(tiles,args.preview)
    print("Wraith hand-painted linked wall installed:",ATLAS)

if __name__=="__main__":
    main()
