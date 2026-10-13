#!/usr/bin/env python3
"""Vanilla-reference Wraith living wall: all 16 genuine linked-wall geometries.

Template measured from the 1024px ReBuild copy of RimWorld Wall_Atlas_Smooth,
Bricks and Planks. 192x192 rendered sample in a 256x256 atlas cell.
Exposed edge inset: 9 pixels. Roof-to-face transition: y102..114.
Lower wall face: y115..181. Side bevels: x9..54 / x137..182.
This is a material-specific repaint from existing approved Wraith artwork,
not a reskin of the copyrighted reference texture.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np

S = 192
EDGE = 9
TOP_END = 22
FACE_TRANSITION = 102
FACE_START = 115

def rectangle(mask):
    return (0 if mask & 8 else EDGE, 0 if mask & 1 else EDGE,
            S if mask & 2 else S-EDGE, S if mask & 4 else S-EDGE)

def approved_source(repo):
    image = Image.open(Path(repo)/"ArtSource/Walls/Wraith/Approved/WraithLivingWall_link00_approved.jpg").convert("RGB")
    rgb = np.asarray(image)
    visible = rgb.max(axis=2)>17
    if not visible.any():
        raise ValueError("Approved Wraith original painting missing or empty")
    filled = np.maximum.accumulate(visible,axis=1)&np.maximum.accumulate(visible[:,::-1],axis=1)[:,::-1]
    return Image.fromarray(np.dstack((rgb, np.where(filled,255,0).astype(np.uint8))),"RGBA")

def build_material(master):
    bbox = master.getchannel("A").getbbox()
    if bbox is None: raise ValueError("Approved Wraith master has no living material")
    image = master.crop(bbox).convert("RGBA")
    w,h = image.size
    def patch(a,b,c,d):
        return image.crop((int(a*w),int(b*h),int(c*w),int(d*h))).convert("RGB")
    # True Wraith painted material. Restrict to unframed pigment rather than
    # enlarging the whole ornament onto every linked tile.
    roof=patch(.43,.36,.52,.47)
    face=patch(.32,.81,.68,.95)
    cap=patch(.37,.81,.63,.88)
    west=patch(.015,.24,.23,.65)
    east=patch(.77,.24,.985,.65)
    def periodic_x(original,height):
        half=original.resize((96,height),Image.Resampling.LANCZOS)
        out=Image.new("RGB",(S,height));out.paste(half,(0,0))
        out.paste(half.transpose(Image.Transpose.FLIP_LEFT_RIGHT),(96,0))
        return np.asarray(out)
    def periodic_y(original,width):
        half=original.resize((width,96),Image.Resampling.LANCZOS)
        out=Image.new("RGB",(width,S));out.paste(half,(0,0))
        out.paste(half.transpose(Image.Transpose.FLIP_TOP_BOTTOM),(0,96))
        return np.asarray(out)
    half=periodic_x(roof,96)
    surface=np.empty((S,S,3),dtype=np.uint8)
    surface[:96]=half;surface[96:]=half[::-1]
    return surface,periodic_x(face,S),periodic_x(cap,S),periodic_y(west,55),periodic_y(east,55)

def generate(source):
    roof,face,cap,west,east=build_material(source)
    states=[]
    for mask in range(16):
        x0,y0,x1,y1=rectangle(mask)
        pix=roof.astype(np.float32).copy()
        # South-facing facade: almost 40% of vanilla wall height is not roof.
        if not (mask&4):
            for y in range(FACE_TRANSITION,min(y1,FACE_START)):
                t=(y-FACE_TRANSITION)/(FACE_START-FACE_TRANSITION)
                lifted=np.clip(cap[y].astype(np.float32)*(.83+.12*t),0,255)
                pix[y]=pix[y]*(1-t)+lifted*t
            for y in range(FACE_START,y1):
                shade=.64+.18*(y-FACE_START)/max(1,y1-FACE_START)
                pix[y]=np.clip(face[y].astype(np.float32)*shade,0,255)
        if not(mask&1):
            for y in range(y0,min(TOP_END,y1)):
                brightness=.65+.30*(y-y0)/max(1,TOP_END-y0)
                pix[y]=np.clip(cap[y].astype(np.float32)*brightness+17,0,255)
        # Exposed chitin bevel: 46 px across the roof, 9 px across the facade;
        # NEVER add one of these at a linked border.
        for bit,edge_tex,is_west in ((8,west,True),(2,east,False)):
            if mask&bit:continue
            for y in range(y0,y1):
                depth=46 if (mask&4 or y<FACE_START) else 9
                for k in range(depth):
                    x=x0+k if is_west else x1-1-k
                    t=k/max(1,depth-1)
                    pigment=edge_tex[y,int(t*(edge_tex.shape[1]-1))].astype(np.float32)
                    weight=(1-t)**.75
                    shade=(.85+.30*(1-t)) if y<FACE_TRANSITION else (.54+.20*(1-t))
                    pix[y,x]=pix[y,x]*(1-weight)+np.clip(pigment*shade,0,255)*weight
        rgba=np.zeros((S,S,4),dtype=np.uint8)
        rgba[y0:y1,x0:x1,:3]=np.clip(pix[y0:y1,x0:x1],0,255).astype(np.uint8)
        rgba[y0:y1,x0:x1,3]=255
        # MaterialAtlasPool-compatible cardinal joins; exact matching texels.
        if mask&8:rgba[EDGE:S-EDGE,0,:3]=roof[EDGE:S-EDGE,0]
        if mask&2:rgba[EDGE:S-EDGE,-1,:3]=roof[EDGE:S-EDGE,0]
        if mask&1:rgba[0,EDGE:S-EDGE,:3]=roof[0,EDGE:S-EDGE]
        if mask&4:rgba[-1,EDGE:S-EDGE,:3]=roof[0,EDGE:S-EDGE]
        states.append(Image.fromarray(rgba,"RGBA"))
    verify(states)
    return states

def verify(states):
    if len(states)!=16:raise ValueError("Not sixteen Wraith masks")
    images=[np.asarray(im.convert("RGBA")) for im in states]
    for mask,image in enumerate(images):
        x0,y0,x1,y1=rectangle(mask)
        truth=np.zeros((S,S),dtype=bool)
        truth[y0:y1,x0:x1]=True
        if not np.array_equal(image[...,3]==255,truth):
            raise ValueError(f"Wraith mask {mask:02d} differs from measured reference silhouette")
    area=slice(EDGE,S-EDGE)
    for a in range(16):
        for b in range(16):
            if (a&2) and (b&8):
                if not np.array_equal(images[a][area,-1],images[b][area,0]):
                    raise ValueError(f"East/west incompatible Wraith seam: {a:02d}/{b:02d}")
            if (a&4) and (b&1):
                if not np.array_equal(images[a][-1,area],images[b][0,area]):
                    raise ValueError(f"South/north incompatible Wraith seam: {a:02d}/{b:02d}")

def pack(states):
    atlas=Image.new("RGBA",(1024,1024))
    for mask,im in enumerate(states):
        area=np.asarray(im.convert("RGBA"))
        cell=np.pad(area,((32,32),(32,32),(0,0)),mode="edge")
        atlas.paste(Image.fromarray(cell,"RGBA"),((mask%4)*256,(3-mask//4)*256))
    return atlas
