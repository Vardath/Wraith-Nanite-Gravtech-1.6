#!/usr/bin/env python3
"""WNG Wraith living wall — native linked wall *architecture*, not a bordered tile.

Painted from the existing WNG Wraith wall-run master with independent structural
topology for exposed pieces, straight runs, ends, corners, T's, crossings and
solid four-neighbour masses. Four identical reference seam profiles mean ANY
linked-edge pairing works, irrespective of perpendicular neighbours.
The gravship hull and its corner overlays are intentionally not modified.
"""
from __future__ import annotations
import argparse
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Walls/Wraith/wall_run.png"
ATLAS_PATH = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall_Atlas.png"
LEGACY_PATH = ROOT / "Textures/Things/Building/Wraith/Structure/WNG_LivingWall.png"
ICON_PATH = ROOT / "Textures/UI/WNG/Build/WNG_WraithLivingWall.png"
HULL_PATH = ROOT / "Textures/Things/Building/Wraith/Gravship/WNG_OrganicHull_Atlas.png"
HULL_ICON = ROOT / "Textures/UI/WNG/Build/WNG_WraithGravshipHull.png"

SIZE, STRIDE, BORDER = 192, 256, 32
ART = 2  # High resolution vector-shaped strokes, downsampled for game textures.
TAU = math.tau


def save(im: Image.Image, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path, "PNG", optimize=True)


def authored_material():
    """Use the existing professionally painted six-cell Wraith wall-run master."""
    with Image.open(SOURCE) as im:
        run = im.convert("RGB")
    if run.size != (1152, 192):
        raise ValueError(f"Unexpected Wraith master dimensions: {run.size}")
    return run


def periodic(arr: np.ndarray) -> np.ndarray:
    """Matching north/south and east/west terminal colour samples."""
    a = np.asarray(arr, np.float32).copy()
    for axis in (1, 0):
        last = a.shape[axis] - 1
        left = np.take(a, 0, axis=axis)
        right = np.take(a, last, axis=axis)
        common = (left + right) * .5
        for k in range(16):
            w = max(0.0, (1 - k/16)**2)
            if axis == 1:
                a[:,k,:] = a[:,k,:]*(1-w) + common*w
                a[:,last-k,:] = a[:,last-k,:]*(1-w) + common*w
            else:
                a[k,:,:] = a[k,:,:]*(1-w) + common*w
                a[last-k,:,:] = a[last-k,:,:]*(1-w) + common*w
    a[:,-1,:] = a[:,0,:]
    a[-1,:,:] = a[0,:,:]
    return np.uint8(np.clip(a,0,255))


def cortex(run: Image.Image):
    """Continuous, dimensional chitin rooftop: no square panel and no corner frame."""
    # The Wraith art source already contains hand-painted cartilage and membrane.
    # Sample its broad top face, rather than the last generator's almost-flat mean.
    material = run.crop((384, 112, 768, 165)).resize((SIZE,SIZE),Image.Resampling.LANCZOS)
    sampled = np.asarray(material, np.float32)
    yy, xx = np.indices((SIZE,SIZE),np.float32)
    u, v = TAU*xx/(SIZE-1), TAU*yy/(SIZE-1)
    # Fine chitin laminations: physically quieter than the exposed edges.
    weave = (4.5*np.sin(3*u + 1.6*np.sin(v)) +
             2.9*np.cos(4*v - 1.5*u) +
             2.0*np.sin(7*u+6*v) + 1.2*np.cos(12*u-9*v))
    r = np.empty((SIZE,SIZE,4),np.uint8)
    baselines=(74.,62.,78.)
    for channel in range(3):
        # Preserve painted authored texture and avoid procedural checkerboards.
        target = baselines[channel] + weave*(1 if channel!=1 else .76)
        painted = sampled[:,:,channel]
        corrected = np.clip((painted - painted.mean())*.60, -28, 28)
        r[:,:,channel] = np.uint8(np.clip(target + corrected,0,255))
    r[:,:,3]=255
    return Image.fromarray(periodic(r),"RGBA")


def bezier(points, samples=28):
    """Cubic Bezier in native cell coordinates."""
    p0,p1,p2,p3 = points
    out=[]
    for n in range(samples+1):
        t=n/samples
        inv=1-t
        out.append((
            inv**3*p0[0] + 3*inv*inv*t*p1[0] + 3*inv*t*t*p2[0] + t**3*p3[0],
            inv**3*p0[1] + 3*inv*inv*t*p1[1] + 3*inv*t*t*p2[1] + t**3*p3[1],
        ))
    return out


def coords(path):
    return [(round(x*ART),round(y*ART)) for x,y in path]


def stroke(d, path, rgb, width, opacity=255):
    d.line(coords(path), fill=(*rgb,opacity),
           width=max(1,round(width*ART)),joint="curve")


def ribs(mask, run):
    """Connected Wraith bone framework: branches grow into adjacent cells."""
    connected=[(name,mask&bit!=0) for name,bit in (("N",1),("E",2),("S",4),("W",8))]
    count=sum(c for _,c in connected)
    layer=Image.new("RGBA",(SIZE*ART,SIZE*ART))
    draw=ImageDraw.Draw(layer)
    # Neighbour directions use three continuous parallel chitin stalks,
    # not a decorative medallion repeated in every cell.
    anchors={
      "N": [(96,90),(94,60),(96,-12)],
      "S": [(96,99),(99,141),(96,205)],
      "W": [(92,96),(49,92),(-12,96)],
      "E": [(100,96),(143,99),(205,96)],
    }
    for direction,active in connected:
        if not active:
            continue
        c0,c1,c2=anchors[direction]
        for lane in (-53,0,53):
            if direction in ("N","S"):
                a0=(c0[0]+lane,c0[1]+(3 if lane else 0))
                a1=(c1[0]+lane*.96,c1[1])
                a2=(c2[0]+lane,c2[1])
            else:
                a0=(c0[0]+(3 if lane else 0),c0[1]+lane)
                a1=(c1[0],c1[1]+lane*.96)
                a2=(c2[0],c2[1]+lane)
            mid=((a0[0]+a1[0])*.5,(a0[1]+a1[1])*.5)
            path=bezier((a0,mid,a1,a2),48)
            central=lane==0
            # undercut, cartilage support, bright off-centre bevel, hairline
            stroke(draw,path,(18,15,26),19 if central else 15,218 if count<4 else 153)
            stroke(draw,path,(91,81,86),14 if central else 10,228 if count<4 else 160)
            offset=[(x-2.6,y-3.1) for x,y in path]
            stroke(draw,offset,(168,153,139),4.6 if central else 3.5,185 if count<4 else 135)
            inner=[(x+2,y+2) for x,y in path]
            stroke(draw,inner,(47,33,53),3.4,195)
            # Subtle luminous synapses along side branch, not a bright outline.
            if lane==0 and count<=3:
                glow=[(x+5,y+4) for x,y in path[10:28]]
                stroke(draw,glow,(47,95,63),2.5,136)
                stroke(draw,glow,(143,183,133),.9,175)
    if count in (2,3) and mask not in (5,10):
        # Wraith T/L junction knuckle, not a separate square end-post.
        cx,cy=96,96
        draw.ellipse((int(72*ART),int(76*ART),int(123*ART),int(116*ART)),
                     fill=(23,18,30,190))
        draw.ellipse((int(79*ART),int(79*ART),int(115*ART),int(107*ART)),
                     fill=(103,88,95,188))
        draw.arc((int(79*ART),int(80*ART),int(114*ART),int(108*ART)),
                 185,347,fill=(170,154,140,200),width=round(3*ART))
    if count==1:
        # Organic terminating shell, with a closed graft valve.
        draw.ellipse((79*ART,79*ART,113*ART,113*ART),
                     fill=(30,23,37,230),outline=(145,132,119,235),
                     width=round(3*ART))
        draw.ellipse((89*ART,87*ART,103*ART,103*ART),fill=(61,91,65,205))
        draw.arc((86*ART,85*ART,106*ART,105*ART),190,330,
                 fill=(171,183,138,210),width=round(2*ART))
    # The full-mass X tile should be quiet, with junction detail only where
    # neighbours truly terminate; otherwise thick blocks look quilted.
    if count==4:
        muted = np.asarray(layer,np.uint8).copy()
        muted[:,:,3] = np.uint8(muted[:,:,3].astype(float)*.54)
        layer=Image.fromarray(muted,"RGBA")
    return layer.resize((SIZE,SIZE),Image.Resampling.LANCZOS)


def edge_strip(run, name):
    """Directional, inward-facing 3D wall edge. Nothing extends outside 1x1."""
    upper = Image.new("RGBA",(SIZE*ART,SIZE*ART))
    d=ImageDraw.Draw(upper)
    # Exposed 10px silhouette with sloped coping above a recessed face.
    path=[(0,0)]
    for x in range(0,SIZE+1,2):
        y=8.5 + 2.2*math.sin((x+8)*TAU/62) + 1.15*math.cos(x*TAU/35)
        path.append((x,y))
    path.extend([(SIZE,0),(0,0)])
    d.polygon(coords(path),fill=(174,157,138,238))
    d.line(coords([(x,2.2+1.2*math.sin(x*TAU/53)) for x in range(0,193,2)]),
           fill=(209,190,160,225),width=3*ART,joint="curve")
    d.line(coords([(x,10+2.2*math.sin((x+8)*TAU/62)) for x in range(0,193,2)]),
           fill=(24,19,29,237),width=3*ART,joint="curve")
    d.line(coords([(x,14+1.5*math.sin(x*TAU/62)) for x in range(0,193,2)]),
           fill=(103,68,84,198),width=3*ART,joint="curve")
    # Painted arch/underface inset. Not the four walls of a square picture frame.
    for x in range(17,193,68):
        d.arc(((x-30)*ART, -7*ART,(x+31)*ART,33*ART),196,337,
              fill=(132,119,116,165),width=4*ART)
        d.line(coords([(x,5),(x+4,11),(x+5,18)]),
               fill=(32,27,35,160),width=3*ART)
    upper=upper.resize((SIZE,SIZE),Image.Resampling.LANCZOS)
    transforms={"N":None,"E":Image.Transpose.ROTATE_270,
                "S":Image.Transpose.ROTATE_180,"W":Image.Transpose.ROTATE_90}
    return upper if transforms[name] is None else upper.transpose(transforms[name])


def edge_seam(base):
    """Reference cross-sections printed where any connected wall crosses a cell."""
    b=np.array(base,np.float32)
    yy,xx=np.indices((SIZE,SIZE),dtype=np.float32)
    # Three parallel seams on the roof, like linked cartilage vertebrae.
    def section(x):
        bone=np.zeros_like(x)
        shadow=np.zeros_like(x)
        for c,w in ((43,8),(96,11),(149,8)):
            bone += 48*np.exp(-((x-c)/w)**2)
            shadow += 20*np.exp(-((x-(c+8))/7)**2)
        return bone-shadow
    vertical=section(yy)
    horizontal=section(xx)
    v=np.clip(b[:,:,:3]+vertical[:,:,None]*np.array([1.,.97,.83]),0,255)
    h=np.clip(b[:,:,:3]+horizontal[:,:,None]*np.array([1.,.97,.83]),0,255)
    return v,h


def build_tile(base, painted_run, mask, seam_v, seam_h):
    t=Image.alpha_composite(base,ribs(mask,painted_run))
    # Individual exposed ends are treated as architectural faces, not framed
    # squares. Shared linked edges have no end treatment.
    for name,bit in (("N",1),("E",2),("S",4),("W",8)):
        if not mask&bit:
            t=Image.alpha_composite(t,edge_strip(painted_run,name))
    a=np.asarray(t,np.float32).copy()
    yy,xx=np.indices((SIZE,SIZE),dtype=np.float32)
    # Each connected direction returns to the same authored boundary section.
    # The reference strips include genuine ribs rather than erasing them at joins.
    # The blend area is 15px, invisible as a seam even for unlike L/T neighbours.
    weights={
      1:np.clip((15-yy)/15,0,1),
      2:np.clip((15-(SIZE-1-xx))/15,0,1),
      4:np.clip((15-(SIZE-1-yy))/15,0,1),
      8:np.clip((15-xx)/15,0,1),
    }
    for bit,profile in ((1,seam_h),(4,seam_h),(2,seam_v),(8,seam_v)):
        if mask&bit:
            w=weights[bit][:,:,None]
            a[:,:,:3]=a[:,:,:3]*(1-w)+profile*w
    a[:,:,3]=255
    return Image.fromarray(np.uint8(np.clip(a,0,255)),"RGBA")


def assemble(tiles):
    """Exact 1024 atlas: four columns, bottom-to-top link masks, padded UVs."""
    dst=Image.new("RGBA",(1024,1024))
    for mask,im in enumerate(tiles):
        core=np.asarray(im,np.uint8)
        padded=np.pad(core,((BORDER,BORDER),(BORDER,BORDER),(0,0)),mode="edge")
        padded=Image.fromarray(padded,"RGBA")
        dst.paste(padded,(mask%4*STRIDE,(3-mask//4)*STRIDE))
    return dst


def assert_seams(tiles):
    for m,a in enumerate(tiles):
        aa=np.asarray(a,np.int16)
        if m&2:
            for n,b in enumerate(tiles):
                if n&8:
                    bb=np.asarray(b,np.int16)
                    if np.abs(aa[24:-24,-1,:3]-bb[24:-24,0,:3]).max()>5:
                        raise AssertionError(f"east/west Wraith seam {m},{n}")
        if m&4:
            for n,b in enumerate(tiles):
                if n&1:
                    bb=np.asarray(b,np.int16)
                    if np.abs(aa[-1,24:-24,:3]-bb[0,24:-24,:3]).max()>5:
                        raise AssertionError(f"north/south Wraith seam {m},{n}")


def preview(tiles, out):
    """Check actual MULTI-CELL architecture, not an atlas of isolated squares."""
    layout=[
      "................",
      ".##############.",
      ".#.....#......#.",
      ".#.....#......#.",
      ".###.#####.####.",
      "...#...#...#....",
      "...#####.###....",
      ".....#...#......",
      "...#######......",
      "...#.#.#.#......",
      "...#######......",
      "................",
    ]
    scale=SIZE
    board=Image.new("RGB",(len(layout[0])*scale,len(layout)*scale),(39,38,46))
    for y,row in enumerate(layout):
        for x,ch in enumerate(row):
            if ch != "#": continue
            mask=(1 if y>0 and layout[y-1][x]=="#" else 0)
            mask|=(2 if x<len(row)-1 and row[x+1]=="#" else 0)
            mask|=(4 if y<len(layout)-1 and layout[y+1][x]=="#" else 0)
            mask|=(8 if x>0 and row[x-1]=="#" else 0)
            board.paste(tiles[mask],(x*scale,y*scale))
    save(board,out)


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--preview",type=Path,
                        help="Write multi-tile topology QA image outside runtime Textures")
    args=parser.parse_args()
    run=authored_material()
    base=cortex(run)
    v,h=edge_seam(base)
    tiles=[build_tile(base,run,mask,v,h) for mask in range(16)]
    assert_seams(tiles)
    for i,t in enumerate(tiles):
        if t.getchannel("A").getextrema()!=(255,255):
            raise AssertionError(f"wall {i} cannot leave uncovered portions of a cell")
    save(assemble(tiles),ATLAS_PATH)
    save(tiles[0],ICON_PATH)
    save(tiles[0],LEGACY_PATH)
    # Preserve the original Wraith hull art in its own build menu.
    with Image.open(HULL_PATH) as hull:
        original=hull.convert("RGBA").crop((BORDER,3*STRIDE+BORDER,
                                           BORDER+SIZE,3*STRIDE+BORDER+SIZE))
    save(original,HULL_ICON)
    if args.preview: preview(tiles,args.preview)
    print("Wraith living walls rebuilt: 16 organic structural states, true linked runs, ends, L/T/X joints, 100% cell coverage.")


if __name__=="__main__":
    main()
