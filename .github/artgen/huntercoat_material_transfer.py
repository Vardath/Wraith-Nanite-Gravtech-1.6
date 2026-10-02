from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageEnhance
from pathlib import Path
import numpy as np, subprocess, io, math, random
from scipy.ndimage import gaussian_filter, distance_transform_edt

ROOT=Path("Textures/Things/Pawn/Humanlike/Apparel/Wraith")
DUSTER=Path("/tmp/norbals/LegacyAssets/Textures/Apparel")
HIST="22ab5e03335aad03731814541361ab29d5124319"
BODIES=["Male","Female","Thin","Fat","Hulk"]
DIRS=["south","north","east"]
OUT=192
SS=4
HI=OUT*SS

def old_png(path):
    b=subprocess.check_output(["git","show",f"{HIST}:{path}"])
    return Image.open(io.BytesIO(b)).convert("RGBA")

def load_rgba(p):
    return Image.open(p).convert("RGBA")

def alpha_bbox(im):
    a=im.getchannel("A"); return a.getbbox()

def clean_alpha(im):
    arr=np.array(im)
    z=arr[...,3]==0
    arr[z,:3]=0
    return Image.fromarray(arr.astype(np.uint8),"RGBA")

def fit_to_bbox(src, target_bbox, canvas=(HI,HI), extra_y=0):
    x0,y0,x1,y1=target_bbox
    sb=alpha_bbox(src)
    if not sb: return Image.new("RGBA",canvas,(0,0,0,0))
    crop=src.crop(sb)
    tw=max(1,x1-x0); th=max(1,y1-y0)
    crop=crop.resize((tw,th+extra_y),Image.Resampling.LANCZOS)
    out=Image.new("RGBA",canvas,(0,0,0,0))
    out.alpha_composite(crop,(x0,y0))
    return out

def soft_region(size, bbox, relpoly, blur=10):
    x0,y0,x1,y1=bbox; w=x1-x0; h=y1-y0
    im=Image.new("L",size,0); d=ImageDraw.Draw(im)
    pts=[(int(x0+rx*w),int(y0+ry*h)) for rx,ry in relpoly]
    d.polygon(pts,fill=255)
    return im.filter(ImageFilter.GaussianBlur(blur))

def line_mask(size, points, width, blur=2):
    im=Image.new("L",size,0); d=ImageDraw.Draw(im)
    d.line(points,fill=255,width=width,joint="curve")
    if blur: im=im.filter(ImageFilter.GaussianBlur(blur))
    return im

def apply_alpha(layer, mask):
    a=np.array(layer.getchannel("A"),dtype=np.uint16)
    m=np.array(mask,dtype=np.uint16)
    layer.putalpha(Image.fromarray(((a*m)//255).astype(np.uint8),"L"))
    return layer

def colorize_luminance(src, low, high):
    arr=np.array(src.convert("RGBA"),dtype=np.float32)
    rgb=arr[...,:3]
    lum=(0.2126*rgb[...,0]+0.7152*rgb[...,1]+0.0722*rgb[...,2])/255.0
    low=np.array(low,dtype=np.float32); high=np.array(high,dtype=np.float32)
    out=low[None,None,:]*(1-lum[...,None])+high[None,None,:]*lum[...,None]
    a=arr[...,3:4]
    out=np.concatenate([np.clip(out,0,255),a],axis=2)
    return Image.fromarray(out.astype(np.uint8),"RGBA")

def highpass_overlay(base, src, mask, strength=.22):
    s=np.array(src.resize(base.size,Image.Resampling.LANCZOS).convert("RGB"),dtype=np.float32)
    lum=0.2126*s[...,0]+0.7152*s[...,1]+0.0722*s[...,2]
    hp=lum-gaussian_filter(lum,12)+128
    hp=np.clip(hp,80,176)
    arr=np.array(base,dtype=np.float32)
    m=np.array(mask,dtype=np.float32)/255.0
    factor=(hp/128.0)
    for c in range(3):
        arr[...,c]=arr[...,c]*(1-strength*m+strength*m*factor)
    return Image.fromarray(np.clip(arr,0,255).astype(np.uint8),"RGBA")

def sample_ship_material(ship, target_bbox, tint=(72,55,82)):
    sb=alpha_bbox(ship)
    crop=ship.crop(sb) if sb else ship
    x0,y0,x1,y1=target_bbox
    tex=crop.resize((x1-x0,y1-y0),Image.Resampling.LANCZOS)
    tex=colorize_luminance(tex,(28,22,31),tint)
    out=Image.new("RGBA",(HI,HI),(0,0,0,0)); out.alpha_composite(tex,(x0,y0))
    return out

def add_glow_gem(im, x,y,r=5):
    g=Image.new("RGBA",im.size,(0,0,0,0)); d=ImageDraw.Draw(g)
    d.ellipse((x-r*4,y-r*4,x+r*4,y+r*4),fill=(119,100,255,50))
    g=g.filter(ImageFilter.GaussianBlur(r*2.5))
    im.alpha_composite(g)
    d=ImageDraw.Draw(im)
    d.ellipse((x-r,y-r,x+r,y+r),fill=(202,195,255,235),outline=(84,69,128,230),width=max(1,r//2))

def build(body, direction):
    # exact vanilla-style Duster silhouette from public vanilla mirror
    vpath=DUSTER/f"Duster_{body}_{direction}.png"
    van=load_rgba(vpath).resize((HI,HI),Image.Resampling.LANCZOS)
    va=np.array(van.getchannel("A"))
    # trim any solid black canvas artifacts by using non-black alpha-supported pixels from vanilla ref
    rgb=np.array(van)[...,:3]
    visible=((va>8)&(rgb.max(axis=2)>20)).astype(np.uint8)*255
    mask=Image.fromarray(visible,"L").filter(ImageFilter.MaxFilter(5))
    bb=mask.getbbox()
    if not bb: raise RuntimeError((body,direction,"no vanilla mask"))

    hist_path=f"Textures/Things/Pawn/Humanlike/Apparel/Wraith/WNG_HunterCoat_{body}_{direction}.png"
    try:
        hist=old_png(hist_path)
    except subprocess.CalledProcessError:
        hist=old_png(f"Textures/Things/Pawn/Humanlike/Apparel/Wraith/WNG_HunterCoat_Male_{direction}.png")
    hist=hist.resize((HI,HI),Image.Resampling.LANCZOS)

    # lower flowing leather texture borrowed only as material evidence from historical Queen Raiment
    q_path=f"Textures/Things/Pawn/Humanlike/Apparel/Wraith/WNG_QueenRaiment_{body}_{direction}.png"
    try:
        queen=old_png(q_path)
    except subprocess.CalledProcessError:
        queen=old_png(f"Textures/Things/Pawn/Humanlike/Apparel/Wraith/WNG_QueenRaiment_Male_{direction}.png")
    queen=queen.resize((HI,HI),Image.Resampling.LANCZOS)

    # painterly historical Wraith clothing forms the material basis
    hfit=fit_to_bbox(hist,bb)
    qfit=fit_to_bbox(queen,bb)
    hfit=colorize_luminance(hfit,(22,18,25),(105,82,115))
    qfit=colorize_luminance(qfit,(20,17,24),(91,71,102))

    out=Image.new("RGBA",(HI,HI),(0,0,0,0))
    base=Image.blend(hfit,qfit,.28)
    base.putalpha(mask)
    out.alpha_composite(base)

    x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
    # darker lower leather tail, feathered rather than hard geometry
    lower=soft_region((HI,HI),bb,[(0,.48),(1,.48),(1,1),(0,1)],blur=32)
    dark=Image.new("RGBA",(HI,HI),(14,12,17,105))
    dark.putalpha(ImageChops.multiply(lower,mask))
    out.alpha_composite(dark)

    # current approved Wraith ship art as micro-material source
    ship1=load_rgba(ROOT.parent.parent.parent.parent/"Building/Wraith/Shuttle/WNG_WraithDart_south.png").resize((HI,HI),Image.Resampling.LANCZOS)
    ship2=load_rgba(ROOT.parent.parent.parent.parent/"Building/Wraith/Shuttle/WNG_WraithCruiser_south.png").resize((HI,HI),Image.Resampling.LANCZOS)
    ship=Image.blend(ship1,ship2,.50)
    shipmat=sample_ship_material(ship,bb,(96,73,106))

    # shoulder/outer-panel chitin: soft, texture-rich, follows vanilla coat shell
    if direction in ("south","north"):
        left=soft_region((HI,HI),bb,[(0,.04),(.34,.02),(.42,.28),(.31,.50),(.08,.44)],blur=18)
        right=Image.fromarray(np.fliplr(np.array(left)).copy(),"L")
        chmask=ImageChops.lighter(left,right)
    else:
        chmask=soft_region((HI,HI),bb,[(.08,.06),(.72,.02),(.92,.25),(.76,.53),(.52,.46),(.28,.28)],blur=18)
    chmask=ImageChops.multiply(chmask,mask)
    ch=apply_alpha(shipmat.copy(),chmask)
    out.alpha_composite(ch)

    # leather lapels / spine built as textured bands, not flat fills
    if direction=="south":
        ptsL=[(int(x0+w*.43),int(y0+h*.10)),(int(x0+w*.35),int(y0+h*.27)),(int(x0+w*.42),int(y0+h*.49))]
        ptsR=[(int(x0+w*.57),int(y0+h*.10)),(int(x0+w*.65),int(y0+h*.27)),(int(x0+w*.58),int(y0+h*.49))]
        for pts in (ptsL,ptsR):
            lm=line_mask((HI,HI),pts,max(10,int(w*.055)),blur=4)
            lm=ImageChops.multiply(lm,mask)
            strip=colorize_luminance(shipmat,(31,24,34),(118,94,125)); strip=apply_alpha(strip,lm)
            out.alpha_composite(strip)
        # asymmetrical real-costume belt/strap
        beltpts=[(int(x0+w*.23),int(y0+h*.52)),(int(x0+w*.51),int(y0+h*.57)),(int(x0+w*.77),int(y0+h*.53))]
        bm=line_mask((HI,HI),beltpts,max(7,int(h*.03)),blur=2); bm=ImageChops.multiply(bm,mask)
        belt=colorize_luminance(hfit,(16,13,18),(73,60,76)); belt=apply_alpha(belt,bm); out.alpha_composite(belt)
        add_glow_gem(out,int(x0+w*.51),int(y0+h*.36),max(3,int(w*.018)))
    elif direction=="north":
        spine=line_mask((HI,HI),[(int(x0+w*.5),int(y0+h*.11)),(int(x0+w*.5),int(y0+h*.75))],max(8,int(w*.035)),blur=4)
        spine=ImageChops.multiply(spine,mask)
        strip=colorize_luminance(shipmat,(29,23,33),(103,81,111)); strip=apply_alpha(strip,spine); out.alpha_composite(strip)
        add_glow_gem(out,int(x0+w*.50),int(y0+h*.18),max(3,int(w*.015)))
    else:
        seam=line_mask((HI,HI),[(int(x0+w*.63),int(y0+h*.14)),(int(x0+w*.67),int(y0+h*.62))],max(7,int(w*.035)),blur=3)
        seam=ImageChops.multiply(seam,mask)
        strip=colorize_luminance(shipmat,(28,22,31),(101,80,109)); strip=apply_alpha(strip,seam); out.alpha_composite(strip)
        add_glow_gem(out,int(x0+w*.64),int(y0+h*.25),max(3,int(w*.014)))

    # ship-derived high-frequency detail so surfaces reach vehicle-art richness
    out=highpass_overlay(out,ship,mask,.20)

    # production-worn distress: irregular scratches and rubbed leather, restrained
    arr=np.array(out,dtype=np.float32); m=np.array(mask)>0
    dist=distance_transform_edt(m)
    edge=np.clip(1-dist/30,0,1)
    rng=np.random.default_rng(abs(hash(body+direction))%2**32)
    noise=gaussian_filter(rng.normal(0,1,(HI,HI)),2.4)
    wear=(noise>1.15)&m
    arr[wear,:3]=np.clip(arr[wear,:3]+12,0,255)
    arr[...,:3]=np.clip(arr[...,:3]*(1-.12*edge[...,None]),0,255)
    arr[...,3]=np.array(mask)
    out=Image.fromarray(arr.astype(np.uint8),"RGBA")

    # subtle textile/leather fold reinforcement from historical source luminance
    histlum=np.array(hfit.convert("L"),dtype=np.float32)
    hp=histlum-gaussian_filter(histlum,18)
    arr=np.array(out,dtype=np.float32)
    arr[...,:3]=np.clip(arr[...,:3]+hp[...,None]*.12*(np.array(mask)[...,None]/255),0,255)
    arr[...,3]=np.array(mask)
    out=Image.fromarray(arr.astype(np.uint8),"RGBA")

    out=out.resize((OUT,OUT),Image.Resampling.LANCZOS)
    return clean_alpha(out)

# tile uses vanilla Duster ground silhouette, not a portrait
tile=load_rgba(DUSTER/"Duster.png").resize((HI,HI),Image.Resampling.LANCZOS)
trgb=np.array(tile)[...,:3]; ta=np.array(tile.getchannel("A"))
tmask=Image.fromarray((((ta>8)&(trgb.max(axis=2)>20)).astype(np.uint8)*255),"L").filter(ImageFilter.MaxFilter(5))
tbb=tmask.getbbox()
histtile=old_png("Textures/Things/Pawn/Humanlike/Apparel/Wraith/WNG_HunterCoat.png").resize((HI,HI),Image.Resampling.LANCZOS)
ht=fit_to_bbox(histtile,tbb); ht=colorize_luminance(ht,(20,17,23),(106,83,115)); ht.putalpha(tmask)
ship=Image.blend(load_rgba(ROOT.parent.parent.parent.parent/"Building/Wraith/Shuttle/WNG_WraithDart_south.png").resize((HI,HI),Image.Resampling.LANCZOS),load_rgba(ROOT.parent.parent.parent.parent/"Building/Wraith/Shuttle/WNG_WraithCruiser_south.png").resize((HI,HI),Image.Resampling.LANCZOS),.5)
ht=highpass_overlay(ht,ship,tmask,.22)
x0,y0,x1,y1=tbb; w=x1-x0; h=y1-y0
bm=line_mask((HI,HI),[(int(x0+w*.26),int(y0+h*.53)),(int(x0+w*.75),int(y0+h*.53))],max(6,int(h*.035)),2)
bm=ImageChops.multiply(bm,tmask)
belt=Image.new("RGBA",(HI,HI),(33,27,34,255)); belt.putalpha(bm); ht.alpha_composite(belt)
add_glow_gem(ht,int(x0+w*.52),int(y0+h*.34),max(3,int(w*.018)))
tileout=clean_alpha(ht.resize((OUT,OUT),Image.Resampling.LANCZOS))
tileout.save(ROOT/"WNG_HunterCoat.png",optimize=True)

for body in BODIES:
    south=build(body,"south")
    south.save(ROOT/f"WNG_HunterCoat_{body}.png",optimize=True)
    south.save(ROOT/f"WNG_HunterCoat_{body}_south.png",optimize=True)
    build(body,"north").save(ROOT/f"WNG_HunterCoat_{body}_north.png",optimize=True)
    east=build(body,"east")
    east.save(ROOT/f"WNG_HunterCoat_{body}_east.png",optimize=True)
    east.transpose(Image.Transpose.FLIP_LEFT_RIGHT).save(ROOT/f"WNG_HunterCoat_{body}_west.png",optimize=True)

files=sorted(ROOT.glob("WNG_HunterCoat*.png"))
assert len(files)==26,len(files)
for p in files:
    im=Image.open(p).convert("RGBA"); im.load()
    assert im.size==(192,192)
    a=np.array(im.getchannel("A")); ys,xs=np.nonzero(a>0)
    assert len(xs)>0
    arr=np.array(im); assert np.all(arr[arr[...,3]==0,:3]==0)

# internal contact sheet for visual QA, removed after sign-off
sel=[ROOT/"WNG_HunterCoat.png",ROOT/"WNG_HunterCoat_Male_south.png",ROOT/"WNG_HunterCoat_Male_north.png",ROOT/"WNG_HunterCoat_Male_east.png",ROOT/"WNG_HunterCoat_Female_south.png",ROOT/"WNG_HunterCoat_Hulk_south.png"]
sheet=Image.new("RGBA",(192*3,192*2),(18,16,22,255))
for i,p in enumerate(sel):
    sheet.alpha_composite(Image.open(p).convert("RGBA"),((i%3)*192,(i//3)*192))
sheet.save(".github/huntercoat_preview.png")
print("HUNTER_COAT_MATERIAL_TRANSFER_OK",len(files))
