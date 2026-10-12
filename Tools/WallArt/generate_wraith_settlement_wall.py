#!/usr/bin/env python3
"""Wraith settlement wall art: 16 native linked states, with full-tile coverage.

This is the independent settlement-wall master. It does not alter gravship hull
art. Uses a restrained Stargate Atlantis-inspired chitin/membrane material,
lit shell coping and edge-only organic ribbing. A wall has one full 1x1 footprint;
linked edges never receive end caps. Requires Pillow and NumPy.
"""
from __future__ import annotations
import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SIZE = 192
STRIDE = 256
INSET = 32
TAU = math.tau
ATLAS = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall_Atlas.png"
ICON = ROOT / "Textures/UI/WNG/Build/WNG_WraithLivingWall.png"
LEGACY = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall.png"
HULL_ATLAS = ROOT / "Textures/Things/Building/Wraith/Gravship/WNG_OrganicHull_Atlas.png"
HULL_ICON = ROOT / "Textures/UI/WNG/Build/WNG_WraithGravshipHull.png"


def save(image, path):
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, format="PNG", optimize=True)


def make_skin():
    """Seamless dark purple/grey bioceramic with subdued living growth texture."""
    yy, xx = np.indices((SIZE, SIZE), dtype=np.float32)
    u, v = TAU * xx / (SIZE - 1), TAU * yy / (SIZE - 1)
    folds = (5.3 * np.sin(2*u + 1.7*np.sin(v)) +
             3.4 * np.cos(3*v - u) + 2.2 * np.sin(5*u + 4*v) +
             1.7 * np.cos(8*v + 3*u))
    grain = (np.sin(31*u + 21*v) + np.cos(27*v - 19*u)) * 0.8
    deep = 2.6 * np.sin(2*u - 2*v)
    rgb = np.zeros((SIZE, SIZE, 4), dtype=np.uint8)
    for c, baseline in enumerate((52, 46, 59)):
        rgb[:, :, c] = np.clip(baseline + folds * (0.90 if c==0 else 0.73)
                               + grain + deep * (1 if c==2 else 0.48), 0, 255)
    rgb[:, :, 3] = 255
    rgb[:, -1] = rgb[:, 0]
    rgb[-1, :] = rgb[0, :]
    im = Image.fromarray(rgb, "RGBA")
    # Scarce, subdued subdermal filaments: avoid tile-centre medallions.
    growth = Image.new("RGBA", (SIZE, SIZE))
    d = ImageDraw.Draw(growth)
    for row in range(3):
        y = 24 + row * 68
        for j in range(3):
            x = j * 72 + 16 + (row % 2) * 19
            # The weave is quietly embedded in the cap rather than painted on it.
            d.arc((x-18, y-15, x+33, y+34), 212, 336, fill=(123, 103, 111, 18), width=3)
            d.arc((x-23, y-11, x+28, y+40), 45, 165, fill=(12, 15, 23, 24), width=4)
    composite = np.array(Image.alpha_composite(im, growth)).copy()
    # Filament overlay must be periodic too; otherwise linked edges get a
    # subtle but measurable discontinuity even if the structural skin tiles.
    composite[:, -1] = composite[:, 0]
    composite[-1, :] = composite[0, :]
    return Image.fromarray(composite, "RGBA")


def edge_surface(lightness=1.0):
    """Paint one exposed north-facing bevel; rotate this for other edges.

    The upper chitin lip climbs toward the quiet roof cap, then falls into
    a recessed plum membrane with narrow bioluminescent vertical fissures.
    """
    s = 3
    w = SIZE * s
    layer = Image.new("RGBA", (w, w))
    d = ImageDraw.Draw(layer)
    def line(points, color, width=1):
        d.line([(round(x*s), round(y*s)) for x,y in points],
               fill=color, width=max(1, round(width*s)), joint="curve")
    def poly(points, color):
        d.polygon([(round(x*s), round(y*s)) for x,y in points], fill=color)
    # The projected edge remains completely within its own wall cell.
    poly([(0,0),(192,0),(192,26),(0,26)], (22, 18, 30, 220))
    # Scalloped edge: a shell plate continues along a wall run, not a block pattern.
    contour = [(0, 13)]
    for x in range(0,193,4):
        h = 12 + 3*math.sin((x+11)*TAU/66) + 1.6*math.cos(x*TAU/31)
        contour.append((x,h))
    contour += [(192,0),(0,0)]
    poly(contour, (102, 94, 100, 247))
    line([(x, 3 + .75*math.sin(x*TAU/52)) for x in range(0,193,2)],
         (181, 173, 155, 194), 2.0)
    line([(x, 8 + 1.8*math.sin((x+5)*TAU/66)) for x in range(0,193,2)],
         (139, 124, 121, 220), 2.5)
    line([(x, 14 + 3*math.sin((x+11)*TAU/66)) for x in range(0,193,2)],
         (17, 16, 28, 236), 2.8)
    line([(x, 17 + 3*math.sin((x+11)*TAU/66)) for x in range(0,193,2)],
         (82, 56, 71, 137), 2.2)
    # Knuckled growth ribs meet the coping at irregular intervals.
    for cx, lean in ((17, -6),(69,4),(118,-5),(171,5)):
        line([(cx+lean,34),(cx+lean*.6,27),(cx,15),(cx+3,8)],
             (18,17,26,180),5.5)
        line([(cx+lean-2,31),(cx+lean*.6-2,25),(cx-2,14),(cx+1,7)],
             (155,139,135,166),3.0)
        line([(cx+lean+3,32),(cx+3,17),(cx+5,8)],
             (54,39,52,160),1.8)
        # Organic green breath vents, narrow enough not to dominate at colony scale.
        line([(cx+12,14),(cx+11,17),(cx+9,23)],
             (5,14,13,230),3.7)
        line([(cx+12,15),(cx+11,18),(cx+10,21)],
             (104,163,115,220),1.5)
        line([(cx+13,17),(cx+12,20)],
             (174,208,156,110),.7)
    # Sheer face beneath the coping and a bevel leading into the roof top.
    for yy in range(18,33):
        alpha = round(155 * (1 - (yy-18)/15)**2)
        line([(0,yy),(192,yy)], (29,22,35,alpha),1)
    layer = layer.resize((SIZE,SIZE),Image.Resampling.LANCZOS)
    if lightness != 1:
        a=np.array(layer).copy()
        a[:,:,:3]=np.clip(a[:,:,:3].astype(np.float32)*lightness,0,255).astype(np.uint8)
        layer=Image.fromarray(a,"RGBA")
    return layer


def tile(base, mask, edges):
    out = base.copy()
    # Bits: north=1,east=2,south=4,west=8. Open rim only on exposed sides.
    for bit, rim in edges:
        if not mask & bit:
            out = Image.alpha_composite(out, rim)
    # Smoothly return to identical master colours at linked boundaries.
    # This makes any edge link continuous even beside an exposed perpendicular rim.
    a = np.array(out, dtype=np.float32)
    original=np.array(base,dtype=np.float32)
    yy,xx=np.indices((SIZE,SIZE))
    distance=np.full((SIZE,SIZE),100.,dtype=np.float32)
    if mask & 1: distance=np.minimum(distance,yy.astype(np.float32))
    if mask & 2: distance=np.minimum(distance,(SIZE-1-xx).astype(np.float32))
    if mask & 4: distance=np.minimum(distance,(SIZE-1-yy).astype(np.float32))
    if mask & 8: distance=np.minimum(distance,xx.astype(np.float32))
    weight=np.clip(distance/20.,0.,1.)[:,:,None]
    a[:,:,:3]=original[:,:,:3]*(1-weight)+a[:,:,:3]*weight
    a[:,:,3]=255
    return Image.fromarray(np.clip(a,0,255).astype(np.uint8),"RGBA")


def pack(tiles):
    atlas=Image.new("RGBA",(1024,1024))
    for mask, image in enumerate(tiles):
        # MaterialAtlasPool's printable 192x192 region is at +32 in each 256 cell.
        padded=Image.new("RGBA",(STRIDE,STRIDE))
        padded.paste(image,(INSET,INSET))
        padded.paste(image.crop((0,0,SIZE,1)).resize((SIZE,INSET)),(INSET,0))
        padded.paste(image.crop((0,SIZE-1,SIZE,SIZE)).resize((SIZE,INSET)),(INSET,INSET+SIZE))
        padded.paste(padded.crop((INSET,0,INSET+1,STRIDE)).resize((INSET,STRIDE)),(0,0))
        padded.paste(padded.crop((INSET+SIZE-1,0,INSET+SIZE,STRIDE)).resize((INSET,STRIDE)),(INSET+SIZE,0))
        x=mask%4*STRIDE
        y=(3-mask//4)*STRIDE
        atlas.paste(padded,(x,y))
    return atlas


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--preview",type=Path,help="Optional directory for reviewer previews, never shipped")
    args=parser.parse_args()
    base=make_skin()
    north=edge_surface()
    edges=[(1,north),(2,north.rotate(270)),(4,north.rotate(180)),(8,north.rotate(90))]
    tiles=[tile(base,n,edges) for n in range(16)]
    for mask, img in enumerate(tiles):
        if img.getchannel("A").getextrema() != (255,255):
            raise ValueError("Wall atlas tile fails full-cell opacity at mask %d" % mask)
    # Ensure the linked-edge seam is bit-identical, not merely visually similar.
    for m,a in enumerate(tiles):
        if m&2:
            for n,b in enumerate(tiles):
                if n&8 and list(a.crop((SIZE-1,0,SIZE,SIZE)).getdata()) != list(b.crop((0,0,1,SIZE)).getdata()):
                    raise ValueError("East-west seam mismatch: %s / %s" % (m,n))
        if m&4:
            for n,b in enumerate(tiles):
                if n&1 and list(a.crop((0,SIZE-1,SIZE,SIZE)).getdata()) != list(b.crop((0,0,SIZE,1)).getdata()):
                    raise ValueError("North-south seam mismatch: %s / %s" % (m,n))
    save(pack(tiles),ATLAS)
    save(tiles[0],ICON)
    save(tiles[0],LEGACY)
    # Preserve the original, separate Wraith gravship hull appearance in its
    # own architect button. The hull 16-state atlas and angled corners stay as-is.
    with Image.open(HULL_ATLAS) as hull:
        hull_icon = hull.convert("RGBA").crop((INSET, 3*STRIDE+INSET, INSET+SIZE, 3*STRIDE+INSET+SIZE))
    save(hull_icon, HULL_ICON)
    if args.preview:
        args.preview.mkdir(parents=True,exist_ok=True)
        samples=[0,1,2,3,5,7,10,11,13,14,15]
        sheet=Image.new("RGBA",(SIZE*4,SIZE*3),(42,41,42,255))
        for i,m in enumerate(samples): sheet.paste(tiles[m],(i%4*SIZE,i//4*SIZE))
        save(sheet,args.preview/"wraith-settlement-contact-sheet.png")
    print("Generated Wraith settlement native atlas, architect icon and legacy single wall: 16 linked 100%-coverage states.")


if __name__=="__main__":
    main()
