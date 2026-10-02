from __future__ import annotations
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageChops
from pathlib import Path
from dataclasses import dataclass
import argparse, io, json, math, random, subprocess
import numpy as np
from scipy.ndimage import gaussian_filter, distance_transform_edt

ROOT = Path(__file__).resolve().parents[2]
APPAREL_ROOT = ROOT / "Textures/Things/Pawn/Humanlike/Apparel"
BUILDING_ROOT = ROOT / "Textures/Things/Building"

HI = 768
OUT = 192
BODIES = ["Male","Female","Thin","Fat","Hulk"]
DIRS = ["south","north","east"]
HIST_REF = "22ab5e03335aad03731814541361ab29d5124319"

@dataclass
class Profile:
    key: str
    faction: str
    output_dir: str
    basename: str
    vanilla_family: str
    historical_item: str
    accent_item: str
    material_refs: list[str]
    palette_shadow: tuple[int,int,int]
    palette_mid: tuple[int,int,int]
    palette_high: tuple[int,int,int]
    accent_rgb: tuple[int,int,int]
    tile_family: str
    garment_kind: str

PROFILES = {
    "hunter_coat": Profile(
        key="hunter_coat",
        faction="Wraith",
        output_dir="Wraith",
        basename="WNG_HunterCoat",
        vanilla_family="Duster",
        historical_item="WNG_HunterCoat",
        accent_item="WNG_QueenRaiment",
        material_refs=[
            "Wraith/Shuttle/WNG_WraithDart_south.png",
            "Wraith/Shuttle/WNG_WraithStrikeCraft_south.png",
            "Wraith/Shuttle/WNG_WraithCruiser_south.png",
        ],
        palette_shadow=(16,13,20),
        palette_mid=(66,49,74),
        palette_high=(173,136,188),
        accent_rgb=(192,184,255),
        tile_family="Duster",
        garment_kind="coat",
    ),
}

def old_png(path: str) -> Image.Image:
    raw = subprocess.check_output(["git","show",f"{HIST_REF}:{path}"])
    return Image.open(io.BytesIO(raw)).convert("RGBA")

def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")

def save_clean(im: Image.Image, path: Path):
    im = im.convert("RGBA")
    a = np.array(im)
    a[a[...,3] == 0,:3] = 0
    Image.fromarray(a.astype(np.uint8),"RGBA").save(path,optimize=True)

def visible_mask_from_vanilla(im: Image.Image) -> Image.Image:
    im = im.resize((HI,HI),Image.Resampling.LANCZOS)
    a = np.array(im.getchannel("A"))
    rgb = np.array(im)[...,:3]
    # public vanilla mirrors often include an opaque black preview background.
    m = ((a > 8) & (rgb.max(axis=2) > 28)).astype(np.uint8) * 255
    return Image.fromarray(m,"L").filter(ImageFilter.GaussianBlur(.45))

def bbox(mask: Image.Image):
    b = mask.getbbox()
    if not b: raise RuntimeError("Empty garment mask")
    return b

def fit_alpha(src: Image.Image, bb, canvas=(HI,HI)) -> Image.Image:
    sb = src.getchannel("A").getbbox()
    if not sb: return Image.new("RGBA",canvas,(0,0,0,0))
    crop = src.crop(sb).resize((bb[2]-bb[0],bb[3]-bb[1]),Image.Resampling.LANCZOS)
    out = Image.new("RGBA",canvas,(0,0,0,0))
    out.alpha_composite(crop,(bb[0],bb[1]))
    return out

def tonalize(im: Image.Image, low, mid, high) -> Image.Image:
    rgba=np.array(im.convert("RGBA"),dtype=np.float32)
    rgb=rgba[...,:3]
    lum=(.2126*rgb[...,0]+.7152*rgb[...,1]+.0722*rgb[...,2])/255.
    lo=np.array(low,float); mi=np.array(mid,float); hi=np.array(high,float)
    out=np.zeros_like(rgb)
    lower=lum<.5
    t=np.clip(lum*2,0,1)
    out[lower]=lo+(mi-lo)*t[lower,None]
    t2=np.clip((lum-.5)*2,0,1)
    out[~lower]=mi+(hi-mi)*t2[~lower,None]
    return Image.fromarray(np.dstack([np.clip(out,0,255).astype(np.uint8),rgba[...,3].astype(np.uint8)]),"RGBA")

def crop_visible(im: Image.Image, rel):
    a=np.array(im.getchannel("A"))
    ys,xs=np.nonzero(a>8)
    if len(xs)==0: return im
    x0,x1=xs.min(),xs.max()+1; y0,y1=ys.min(),ys.max()+1
    w=x1-x0; h=y1-y0
    rx0,ry0,rx1,ry1=rel
    return im.crop((int(x0+rx0*w),int(y0+ry0*h),int(x0+rx1*w),int(y0+ry1*h)))

def patch_to_canvas(src: Image.Image, target_bb, mirror=False):
    if mirror: src=src.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    tw=max(1,target_bb[2]-target_bb[0]); th=max(1,target_bb[3]-target_bb[1])
    src=src.resize((tw,th),Image.Resampling.LANCZOS)
    out=Image.new("RGBA",(HI,HI),(0,0,0,0))
    out.alpha_composite(src,(target_bb[0],target_bb[1]))
    return out

def polygon_mask(bb, rel, feather=7):
    x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
    pts=[(int(x0+x*w),int(y0+y*h)) for x,y in rel]
    m=Image.new("L",(HI,HI),0); ImageDraw.Draw(m).polygon(pts,fill=255)
    if feather: m=m.filter(ImageFilter.GaussianBlur(feather))
    return m

def line_mask(points,width,blur=2):
    m=Image.new("L",(HI,HI),0)
    ImageDraw.Draw(m).line(points,fill=255,width=max(1,int(width)),joint="curve")
    if blur: m=m.filter(ImageFilter.GaussianBlur(blur))
    return m

def clipped(layer: Image.Image, mask: Image.Image):
    a=np.array(layer.getchannel("A"),dtype=np.uint16)
    m=np.array(mask,dtype=np.uint16)
    layer=layer.copy()
    layer.putalpha(Image.fromarray(((a*m)//255).astype(np.uint8),"L"))
    return layer

def history_material(profile: Profile, body: str, direction: str, item: str, bb):
    p=f"Textures/Things/Pawn/Humanlike/Apparel/{profile.output_dir}/{item}_{body}_{direction}.png"
    try: src=old_png(p)
    except Exception: src=old_png(f"Textures/Things/Pawn/Humanlike/Apparel/{profile.output_dir}/{item}_Male_{direction}.png")
    return fit_alpha(src.resize((HI,HI),Image.Resampling.LANCZOS),bb)

def ship_materials(profile: Profile):
    ims=[]
    for rel in profile.material_refs:
        ims.append(load_rgba(BUILDING_ROOT / rel).resize((HI,HI),Image.Resampling.LANCZOS))
    return ims

def apply_fold_model(base: Image.Image, hist: Image.Image, mask: Image.Image, strength=.32):
    lum=np.array(hist.convert("L"),dtype=np.float32)
    folds=(lum-gaussian_filter(lum,7)) + .35*(gaussian_filter(lum,18)-gaussian_filter(lum,46))
    a=np.array(base,dtype=np.float32); m=np.array(mask,dtype=np.float32)/255.
    for c in range(3):
        a[...,c]=np.clip(a[...,c]+folds*strength*m,0,255)
    a[...,3]=np.array(mask)
    return Image.fromarray(a.astype(np.uint8),"RGBA")

def apply_microtexture(base: Image.Image, src: Image.Image, mask: Image.Image, strength=.25):
    s=np.array(src.convert("RGB"),dtype=np.float32)
    lum=.2126*s[...,0]+.7152*s[...,1]+.0722*s[...,2]
    hp=lum-gaussian_filter(lum,10)
    a=np.array(base,dtype=np.float32); m=np.array(mask,dtype=np.float32)/255.
    factor=hp*strength
    for c in range(3): a[...,c]=np.clip(a[...,c]+factor*m,0,255)
    return Image.fromarray(a.astype(np.uint8),"RGBA")

def edge_finish(im: Image.Image, mask: Image.Image):
    m=np.array(mask)>16
    dist=distance_transform_edt(m)
    a=np.array(im,dtype=np.float32)
    edge=(dist>0)&(dist<5)
    bevel=(dist>=5)&(dist<13)
    a[edge,:3]=a[edge,:3]*.45+np.array([15,12,18])*.55
    a[bevel,:3]=a[bevel,:3]*.86+np.array([188,148,200])*.14
    a[...,3]=np.array(mask)
    return Image.fromarray(np.clip(a,0,255).astype(np.uint8),"RGBA")

def add_gem(im: Image.Image, x:int,y:int,r:int,color):
    glow=Image.new("RGBA",(HI,HI),(0,0,0,0))
    d=ImageDraw.Draw(glow)
    d.ellipse((x-r*5,y-r*5,x+r*5,y+r*5),fill=color+(46,))
    im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(r*2.4)))
    d=ImageDraw.Draw(im)
    d.ellipse((x-r,y-r,x+r,y+r),fill=(230,226,255,242),outline=(72,56,105,255),width=max(2,r//3))
    d.ellipse((x-r//2,y-r//2,x+r//2,y+r//2),fill=(255,255,255,250))

def add_stitch(im: Image.Image, pts, spacing=20):
    d=ImageDraw.Draw(im)
    # polyline samples
    segs=[]
    total=0
    for a,b in zip(pts[:-1],pts[1:]):
        dx=b[0]-a[0]; dy=b[1]-a[1]; L=(dx*dx+dy*dy)**.5
        segs.append((a,b,L)); total+=L
    pos=0
    while pos<total:
        remain=pos
        for a,b,L in segs:
            if remain<=L:
                t=remain/max(L,1)
                x=a[0]+(b[0]-a[0])*t; y=a[1]+(b[1]-a[1])*t
                d.ellipse((x-2,y-2,x+2,y+2),fill=(153,130,151,150))
                break
            remain-=L
        pos+=spacing

def paint_hunter(profile: Profile, body: str, direction: str, vanilla_dir: Path, ships):
    vm=visible_mask_from_vanilla(load_rgba(vanilla_dir/f"{profile.vanilla_family}_{body}_{direction}.png"))
    bb=bbox(vm); x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
    hist=history_material(profile,body,direction,profile.historical_item,bb)
    accent=history_material(profile,body,direction,profile.accent_item,bb)

    # base = historical garment fold field, retuned to current approved Wraith palette.
    base=tonalize(hist,profile.palette_shadow,profile.palette_mid,profile.palette_high)
    base=apply_fold_model(base,hist,vm,.42)
    base.putalpha(vm)
    out=Image.new("RGBA",(HI,HI),(0,0,0,0)); out.alpha_composite(base)

    dart,strike,cruiser=ships
    # approved ship-painted chitin patches (actual painted pixels) for hard biological trim.
    sourceL=crop_visible(strike,(.03,.28,.43,.80))
    sourceR=crop_visible(strike,(.57,.28,.97,.80))
    sourceSpine=crop_visible(cruiser,(.36,.08,.64,.92))

    if direction=="south":
        lm=polygon_mask(bb,[(.00,.04),(.38,.02),(.44,.30),(.32,.43),(.06,.36)],6)
        rm=polygon_mask(bb,[(1,.04),(.62,.02),(.56,.30),(.68,.43),(.94,.36)],6)
        lm=ImageChops.multiply(lm,vm); rm=ImageChops.multiply(rm,vm)
        lp=patch_to_canvas(sourceL,(x0,y0,x0+int(.46*w),y0+int(.46*h)))
        rp=patch_to_canvas(sourceR,(x1-int(.46*w),y0,x1,y0+int(.46*h)))
        out.alpha_composite(clipped(lp,lm)); out.alpha_composite(clipped(rp,rm))

        # contrast leather lapels: real historical coat surface, sharpened, asymmetrical.
        lpts=[(int(x0+.41*w),int(y0+.08*h)),(int(x0+.34*w),int(y0+.30*h)),(int(x0+.43*w),int(y0+.51*h))]
        rpts=[(int(x0+.59*w),int(y0+.08*h)),(int(x0+.66*w),int(y0+.27*h)),(int(x0+.58*w),int(y0+.49*h))]
        for pts in (lpts,rpts):
            m=line_mask(pts,max(10,int(.045*w)),2); m=ImageChops.multiply(m,vm)
            mat=tonalize(hist,(24,19,27),(83,63,87),(166,133,173))
            out.alpha_composite(clipped(mat,m))
        # diagonal Wraith belt/closure from costume language.
        belt=[(int(x0+.20*w),int(y0+.54*h)),(int(x0+.51*w),int(y0+.58*h)),(int(x0+.79*w),int(y0+.53*h))]
        bm=line_mask(belt,max(8,int(.026*h)),1); bm=ImageChops.multiply(bm,vm)
        bmat=tonalize(accent,(15,12,17),(48,39,51),(104,90,106))
        out.alpha_composite(clipped(bmat,bm))
        add_stitch(out,[(int(x0+.25*w),int(y0+.27*h)),(int(x0+.29*w),int(y0+.82*h))],max(12,int(.045*h)))
        add_gem(out,int(x0+.50*w),int(y0+.35*h),max(4,int(.018*w)),profile.accent_rgb)

    elif direction=="north":
        shoulder=polygon_mask(bb,[(.00,.05),(.38,.02),(.45,.30),(.32,.42),(.06,.36)],6)
        both=ImageChops.lighter(shoulder,Image.fromarray(np.fliplr(np.array(shoulder)).copy(),"L"))
        both=ImageChops.multiply(both,vm)
        mat=patch_to_canvas(sourceSpine,(x0,y0,x1,y0+int(.47*h)))
        out.alpha_composite(clipped(mat,both))
        spinepts=[(int(x0+.50*w),int(y0+.10*h)),(int(x0+.50*w),int(y0+.74*h))]
        sm=line_mask(spinepts,max(9,int(.03*w)),2); sm=ImageChops.multiply(sm,vm)
        out.alpha_composite(clipped(patch_to_canvas(sourceSpine,bb),sm))
        add_stitch(out,[(int(x0+.27*w),int(y0+.26*h)),(int(x0+.24*w),int(y0+.78*h))],max(12,int(.045*h)))
        add_gem(out,int(x0+.50*w),int(y0+.18*h),max(4,int(.015*w)),profile.accent_rgb)

    else:
        side=polygon_mask(bb,[(.07,.05),(.68,.03),(.92,.25),(.73,.52),(.53,.47),(.26,.30)],6)
        side=ImageChops.multiply(side,vm)
        mat=patch_to_canvas(crop_visible(dart,(.18,.12,.78,.84)),bb)
        out.alpha_composite(clipped(mat,side))
        seam=[(int(x0+.63*w),int(y0+.16*h)),(int(x0+.66*w),int(y0+.65*h))]
        sm=line_mask(seam,max(8,int(.035*w)),2); sm=ImageChops.multiply(sm,vm)
        out.alpha_composite(clipped(tonalize(accent,(23,18,27),(79,59,84),(153,122,164)),sm))
        add_gem(out,int(x0+.64*w),int(y0+.26*h),max(4,int(.015*w)),profile.accent_rgb)

    # carry real ship microtexture over painted garment while keeping cloth fold hierarchy.
    out=apply_microtexture(out,cruiser,vm,.19)
    out=apply_fold_model(out,hist,vm,.23)
    out=edge_finish(out,vm)
    out=ImageEnhance.Contrast(out).enhance(1.12)
    out=ImageEnhance.Brightness(out).enhance(1.06)
    out=out.filter(ImageFilter.UnsharpMask(radius=3.6,percent=95,threshold=4))
    out.putalpha(vm)
    return save_size(out)

def save_size(im):
    im=im.resize((OUT,OUT),Image.Resampling.LANCZOS)
    return clean(im)

def clean(im):
    a=np.array(im.convert("RGBA")); a[a[...,3]==0,:3]=0
    return Image.fromarray(a.astype(np.uint8),"RGBA")

def render_tile(profile, vanilla_dir, ships):
    van=load_rgba(vanilla_dir/f"{profile.tile_family}.png").resize((HI,HI),Image.Resampling.LANCZOS)
    vm=visible_mask_from_vanilla(van); bb=bbox(vm)
    hist=old_png(f"Textures/Things/Pawn/Humanlike/Apparel/{profile.output_dir}/{profile.historical_item}.png").resize((HI,HI),Image.Resampling.LANCZOS)
    hist=fit_alpha(hist,bb)
    base=tonalize(hist,profile.palette_shadow,profile.palette_mid,profile.palette_high)
    base=apply_fold_model(base,hist,vm,.40); base.putalpha(vm)
    out=base.copy()
    # ship-painted shoulder/chitin framing on tile.
    strike=ships[1]
    left=patch_to_canvas(crop_visible(strike,(.04,.29,.43,.78)),(bb[0],bb[1],bb[0]+int((bb[2]-bb[0])*.44),bb[1]+int((bb[3]-bb[1])*.42)))
    lm=polygon_mask(bb,[(.00,.04),(.40,.02),(.44,.30),(.30,.44),(.04,.36)],6)
    lm=ImageChops.multiply(lm,vm)
    out.alpha_composite(clipped(left,lm))
    rm=Image.fromarray(np.fliplr(np.array(lm)).copy(),"L")
    out.alpha_composite(clipped(left.transpose(Image.Transpose.FLIP_LEFT_RIGHT),rm))
    add_gem(out,int(bb[0]+(bb[2]-bb[0])*.50),int(bb[1]+(bb[3]-bb[1])*.34),max(4,int((bb[2]-bb[0])*.018)),profile.accent_rgb)
    out=edge_finish(out,vm)
    out=ImageEnhance.Contrast(out).enhance(1.12)
    out=ImageEnhance.Brightness(out).enhance(1.06)
    out.putalpha(vm)
    return save_size(out)

def build(profile: Profile, vanilla_dir: Path, preview: Path|None):
    outdir=APPAREL_ROOT/profile.output_dir
    outdir.mkdir(parents=True,exist_ok=True)
    ships=ship_materials(profile)

    render_tile(profile,vanilla_dir,ships).save(outdir/f"{profile.basename}.png",optimize=True)
    for body in BODIES:
        south=paint_hunter(profile,body,"south",vanilla_dir,ships)
        south.save(outdir/f"{profile.basename}_{body}.png",optimize=True)
        south.save(outdir/f"{profile.basename}_{body}_south.png",optimize=True)
        north=paint_hunter(profile,body,"north",vanilla_dir,ships)
        north.save(outdir/f"{profile.basename}_{body}_north.png",optimize=True)
        east=paint_hunter(profile,body,"east",vanilla_dir,ships)
        east.save(outdir/f"{profile.basename}_{body}_east.png",optimize=True)
        east.transpose(Image.Transpose.FLIP_LEFT_RIGHT).save(outdir/f"{profile.basename}_{body}_west.png",optimize=True)

    files=sorted(outdir.glob(f"{profile.basename}*.png"))
    expected=26
    if len(files)!=expected: raise RuntimeError(f"{profile.basename}: expected {expected}, got {len(files)}")

    for p in files:
        im=load_rgba(p)
        if im.size!=(192,192): raise RuntimeError((p,im.size))
        a=np.array(im.getchannel("A"))
        if not np.any(a>0): raise RuntimeError(f"blank {p}")
        rgba=np.array(im)
        if not np.all(rgba[rgba[...,3]==0,:3]==0): raise RuntimeError(f"dirty alpha {p}")

    if preview:
        chosen=[
          outdir/f"{profile.basename}.png",
          outdir/f"{profile.basename}_Male_south.png",
          outdir/f"{profile.basename}_Male_north.png",
          outdir/f"{profile.basename}_Male_east.png",
          outdir/f"{profile.basename}_Female_south.png",
          outdir/f"{profile.basename}_Hulk_south.png",
        ]
        sheet=Image.new("RGBA",(OUT*3,OUT*2),(18,16,22,255))
        for i,p in enumerate(chosen):
            sheet.alpha_composite(load_rgba(p),((i%3)*OUT,(i//3)*OUT))
        preview.parent.mkdir(parents=True,exist_ok=True)
        sheet.save(preview,optimize=True)

    print(json.dumps({"profile":profile.key,"files":len(files),"status":"ok"}))

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("profile",choices=PROFILES)
    ap.add_argument("--vanilla-dir",required=True,type=Path)
    ap.add_argument("--preview",type=Path)
    args=ap.parse_args()
    build(PROFILES[args.profile],args.vanilla_dir,args.preview)

if __name__=="__main__":
    main()
