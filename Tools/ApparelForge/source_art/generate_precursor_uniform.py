from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageChops, ImageOps
import numpy as np
from scipy.ndimage import gaussian_filter
import random, math

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/"ArtSource/Apparel/precursor_uniform"
OUT.mkdir(parents=True,exist_ok=True)

S=1536

# Finished WNG painted masters are used only as material/brush texture donors.
# Geometry below is new and comes from the Stargate Ancient/Lantean design pass.
DONORS=[
    ROOT/"ArtSource/Apparel/human_form_uniform",
    ROOT/"ArtSource/Apparel/wraith_hunter_coat",
    ROOT/"ArtSource/Apparel/wraith_queen_raiment",
]

def alpha_bbox(im):
    return im.getchannel("A").getbbox()

def donor_texture(view:str, seed:int)->Image.Image:
    imgs=[]
    for d in DONORS:
        p=d/f"master_{view}.png"
        if not p.exists():
            continue
        im=Image.open(p).convert("RGBA")
        bb=alpha_bbox(im)
        if not bb:
            continue
        crop=im.crop(bb)
        crop=crop.resize((S,S),Image.Resampling.LANCZOS)
        imgs.append(crop)
    if not imgs:
        raise RuntimeError("No accepted WNG painted apparel master available as material donor")

    rnd=random.Random(seed)
    # Combine several accepted paintings at different scales/orientations so no donor geometry survives.
    layers=[]
    for i,im in enumerate(imgs):
        if i%2:
            im=ImageOps.mirror(im)
        ang=rnd.uniform(-11,11)
        im=im.rotate(ang,Image.Resampling.BICUBIC,expand=False)
        sc=rnd.uniform(.92,1.12)
        nw=round(S*sc); nh=round(S*sc)
        im=im.resize((nw,nh),Image.Resampling.LANCZOS)
        x=(nw-S)//2; y=(nh-S)//2
        im=im.crop((x,y,x+S,y+S))
        layers.append(im)

    arrs=[]
    for im in layers:
        a=np.asarray(im).astype(np.float32)
        rgb=a[...,:3]
        lum=.2126*rgb[...,0]+.7152*rgb[...,1]+.0722*rgb[...,2]
        # extract painted value/detail only, removing donor colour identity
        low=gaussian_filter(lum,28)
        mid=gaussian_filter(lum,4)-gaussian_filter(lum,18)
        fine=lum-gaussian_filter(lum,1.7)
        arrs.append((low,mid,fine))
    low=np.mean([x[0] for x in arrs],axis=0)
    mid=np.mean([x[1] for x in arrs],axis=0)
    fine=np.mean([x[2] for x in arrs],axis=0)
    low=(low-low.mean())/(low.std()+1e-6)
    mid=(mid-mid.mean())/(mid.std()+1e-6)
    fine=(fine-fine.mean())/(fine.std()+1e-6)
    return low,mid,fine

def mask_poly(points, blur=1.2):
    m=Image.new("L",(S,S),0)
    ImageDraw.Draw(m).polygon([(int(x*S),int(y*S)) for x,y in points],fill=255)
    if blur:
        m=m.filter(ImageFilter.GaussianBlur(blur))
    return m

def mask_ellipse(box, blur=1.2):
    m=Image.new("L",(S,S),0)
    ImageDraw.Draw(m).ellipse(tuple(int(v*S) for v in box),fill=255)
    if blur:
        m=m.filter(ImageFilter.GaussianBlur(blur))
    return m

def painted_material(mask, view, seed, shadow, midc, high, rough=.13, cloth=True):
    low,mid,fine=donor_texture(view,seed)
    rng=np.random.default_rng(seed)
    yy,xx=np.mgrid[0:S,0:S]
    # Broad studio illumination and body form.
    key=np.exp(-(((xx/S)-(.34 if view!="east" else .40))/.52)**2-(((yy/S)-.28)/.58)**2)
    side=np.clip(np.abs(xx/S-.5)-.18,0,.5)
    down=np.clip(yy/S-.55,0,.5)
    form=.35+.42*key-.15*side-.14*down

    # Preserve painterly macro/mid/fine structure from accepted WNG art.
    form += .085*low + .075*mid + .030*fine

    # Real textile weave and irregular fibre noise.
    if cloth:
        weave=(np.sin(xx/3.6)+.65*np.sin(yy/4.8)+.36*np.sin((xx+yy)/7.5))
        fibre=gaussian_filter(rng.normal(0,1,(S,S)),.75)
        form += .010*weave + .010*fibre
    else:
        form += .004*gaussian_filter(rng.normal(0,1,(S,S)),1.5)

    form=np.clip(form,0,1)
    sh=np.array(shadow,float); mi=np.array(midc,float); hi=np.array(high,float)
    rgb=np.empty((S,S,3),np.float32)
    lo=form<.5
    t=np.clip(form*2,0,1)
    rgb[lo]=sh+(mi-sh)*t[lo,None]
    t2=np.clip((form-.5)*2,0,1)
    rgb[~lo]=mi+(hi-mi)*t2[~lo,None]

    arr=np.zeros((S,S,4),np.uint8)
    arr[...,:3]=np.uint8(np.clip(rgb,0,255))
    arr[...,3]=np.asarray(mask)
    return Image.fromarray(arr,"RGBA")

def bevel(base,mask,w=6,dark=(9,12,13,160),light=(242,235,214,86)):
    dil=mask.filter(ImageFilter.MaxFilter(w*2+1))
    ero=mask.filter(ImageFilter.MinFilter(w*2+1))
    outer=ImageChops.subtract(dil,mask)
    inner=ImageChops.subtract(mask,ero)
    sh=Image.new("RGBA",(S,S),dark); sh.putalpha(outer.point(lambda p:int(p*dark[3]/255)))
    hi=Image.new("RGBA",(S,S),light); hi.putalpha(inner.point(lambda p:int(p*light[3]/255)))
    base.alpha_composite(sh); base.alpha_composite(hi)

def stroke(base, pts, col, width, blur=.45):
    layer=Image.new("RGBA",(S,S),(0,0,0,0))
    ImageDraw.Draw(layer).line([(int(x*S),int(y*S)) for x,y in pts],fill=col,width=width,joint="curve")
    if blur:
        layer=layer.filter(ImageFilter.GaussianBlur(blur))
    base.alpha_composite(layer)

def stitch(base,a,b,count,seed):
    rnd=random.Random(seed)
    x1,y1=a; x2,y2=b; dx=x2-x1; dy=y2-y1
    ll=max(1e-6,(dx*dx+dy*dy)**.5); nx=-dy/ll; ny=dx/ll
    layer=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(layer)
    for i in range(count+1):
        t=i/count
        x=x1+dx*t+rnd.uniform(-.0015,.0015)
        y=y1+dy*t+rnd.uniform(-.0015,.0015)
        ln=.004+rnd.uniform(-.001,.001)
        d.line((int((x-nx*ln)*S),int((y-ny*ln)*S),int((x+nx*ln)*S),int((y+ny*ln)*S)),
               fill=(210,207,189,105),width=2)
    base.alpha_composite(layer)

def wrinkles(base, paths):
    for pts,strength in paths:
        stroke(base,pts,(7,10,10,int(72*strength)),22,8)
        stroke(base,[(x-.0025,y-.002) for x,y in pts],(249,243,222,int(34*strength)),7,4)

def grain_wear(base,mask,seed):
    rng=np.random.default_rng(seed)
    arr=np.asarray(base).astype(np.float32)
    m=np.asarray(mask)>30
    yy,xx=np.mgrid[0:S,0:S]
    for _ in range(120):
        x=int(rng.integers(180,S-180)); y=int(rng.integers(160,S-160))
        if not m[y,x]:
            continue
        rx=float(rng.integers(8,36)); ry=float(rng.integers(18,70))
        g=np.exp(-(((xx-x)/rx)**2+((yy-y)/ry)**2)/2)
        amt=float(rng.uniform(-9,8))
        arr[...,:3]=np.clip(arr[...,:3]+g[...,None]*amt*m[...,None],0,255)
    out=Image.fromarray(np.uint8(arr),"RGBA")
    out.putalpha(base.getchannel("A"))
    return out

def front_master():
    c=Image.new("RGBA",(S,S),(0,0,0,0))

    # Main Lantean off-white tailored coat, inspired by Aurora crew clothing.
    main=mask_poly([
        (.35,.105),(.28,.145),(.235,.22),(.23,.37),(.275,.49),(.30,.61),(.29,.80),(.33,.92),
        (.41,.965),(.49,.87),(.50,.83),(.51,.87),(.59,.965),(.67,.92),(.71,.80),(.70,.61),
        (.725,.49),(.77,.37),(.765,.22),(.72,.145),(.65,.105),(.58,.13),(.50,.205),(.42,.13)
    ])
    c.alpha_composite(painted_material(main,"south",1201,(30,34,34),(139,145,139),(225,219,202),.14,True))
    bevel(c,main,7)

    # Charcoal close-fit sleeves.
    for i,pts in enumerate([
        [(.28,.18),(.19,.185),(.12,.25),(.095,.38),(.12,.53),(.18,.60),(.24,.58),(.285,.43)],
        [(.72,.18),(.81,.185),(.88,.25),(.905,.38),(.88,.53),(.82,.60),(.76,.58),(.715,.43)]
    ]):
        m=mask_poly(pts)
        c.alpha_composite(painted_material(m,"south",1210+i,(5,8,9),(34,42,43),(91,99,95),.18,True))
        bevel(c,m,5,dark=(3,5,6,165),light=(128,132,125,58))

    # Pale segmented forearm wraps.
    for i,pts in enumerate([
        [(.11,.34),(.19,.30),(.245,.36),(.235,.52),(.18,.58),(.125,.52)],
        [(.89,.34),(.81,.30),(.755,.36),(.765,.52),(.82,.58),(.875,.52)]
    ]):
        m=mask_poly(pts)
        c.alpha_composite(painted_material(m,"south",1220+i,(43,47,45),(155,158,148),(231,225,207),.12,True))
        bevel(c,m,4)

    # Dark high split collar.
    collar=mask_poly([(.385,.105),(.445,.072),(.50,.06),(.555,.072),(.615,.105),(.585,.175),(.50,.218),(.415,.175)])
    c.alpha_composite(painted_material(collar,"south",1230,(2,4,5),(24,29,29),(68,74,70),.10,True))
    bevel(c,collar,4,dark=(1,2,3,180),light=(133,137,129,64))
    neck=mask_poly([(.445,.071),(.50,.055),(.555,.071),(.548,.125),(.50,.165),(.452,.125)],0)
    c.putalpha(ImageChops.subtract(c.getchannel("A"),neck))

    # Layered ivory shoulder yoke.
    yoke=mask_poly([(.255,.205),(.345,.125),(.42,.115),(.50,.202),(.58,.115),(.655,.125),(.745,.205),
                    (.69,.285),(.60,.302),(.50,.265),(.40,.302),(.31,.285)])
    c.alpha_composite(painted_material(yoke,"south",1240,(46,49,46),(161,163,153),(235,228,209),.12,True))
    bevel(c,yoke,5)

    # Deep graphite inner vest.
    inner=mask_poly([(.405,.22),(.50,.19),(.595,.22),(.602,.66),(.565,.78),(.50,.835),(.435,.78),(.398,.66)])
    c.alpha_composite(painted_material(inner,"south",1250,(2,5,6),(27,35,36),(82,90,86),.15,True))
    bevel(c,inner,4,dark=(1,2,3,180),light=(118,126,122,58))

    # Tapered outer lapels, separately painted for depth.
    for pts,seed in [
        ([ (.285,.285),(.395,.22),(.445,.33),(.425,.67),(.385,.88),(.31,.925),(.275,.82),(.292,.55) ],1260),
        ([ (.715,.285),(.605,.22),(.555,.33),(.575,.67),(.615,.88),(.69,.925),(.725,.82),(.708,.55) ],1261)
    ]:
        m=mask_poly(pts)
        c.alpha_composite(painted_material(m,"south",seed,(38,43,41),(147,153,146),(229,222,203),.14,True))
        bevel(c,m,4)

    # Long skirt/tunic tails with cloth depth.
    for pts,seed in [
        ([ (.30,.57),(.43,.55),(.47,.70),(.45,.88),(.395,.965),(.32,.94),(.275,.82) ],1270),
        ([ (.70,.57),(.57,.55),(.53,.70),(.55,.88),(.605,.965),(.68,.94),(.725,.82) ],1271)
    ]:
        m=mask_poly(pts)
        c.alpha_composite(painted_material(m,"south",seed,(42,47,45),(158,163,154),(235,228,209),.15,True))
        bevel(c,m,4)

    # Antique silver/champagne edge hardware.
    hardware=(179,165,132,155)
    stroke(c,[ (.31,.205),(.405,.255),(.50,.218),(.595,.255),(.69,.205) ],hardware,5,.35)
    stroke(c,[ (.40,.225),(.445,.34),(.425,.665) ],(170,153,112,120),4,.3)
    stroke(c,[ (.60,.225),(.555,.34),(.575,.665) ],(170,153,112,120),4,.3)

    # Restrained blue-grey Ancient conductor embroidery, not neon.
    stroke(c,[ (.50,.225),(.50,.79) ],(67,111,123,175),7,.55)
    stroke(c,[ (.50,.225),(.50,.79) ],(182,201,198,78),2,.25)
    stroke(c,[ (.31,.49),(.40,.53),(.50,.505),(.60,.53),(.69,.49) ],(88,116,119,118),4,.4)
    stroke(c,[ (.34,.73),(.42,.765),(.50,.75),(.58,.765),(.66,.73) ],(88,116,119,105),4,.4)

    # Ancient diamond clasp.
    d=ImageDraw.Draw(c)
    q=[(.50,.31),(.522,.345),(.50,.383),(.478,.345)]
    d.polygon([(int(x*S),int(y*S)) for x,y in q],fill=(66,94,101,238),outline=(198,183,144,220))
    q2=[(.50,.323),(.512,.345),(.50,.369),(.488,.345)]
    d.polygon([(int(x*S),int(y*S)) for x,y in q2],fill=(116,148,151,210))

    wrinkles(c,[
        ([(.17,.25),(.15,.39),(.18,.55)],1.0),
        ([(.83,.25),(.85,.39),(.82,.55)],1.0),
        ([(.34,.31),(.33,.48),(.35,.69),(.36,.87)],1.0),
        ([(.66,.31),(.67,.48),(.65,.69),(.64,.87)],1.0),
        ([(.415,.36),(.405,.54),(.42,.72)],.75),
        ([(.585,.36),(.595,.54),(.58,.72)],.75),
    ])
    stitch(c,(.31,.31),(.35,.84),28,401)
    stitch(c,(.69,.31),(.65,.84),28,402)

    c=grain_wear(c,main,1300)
    c=ImageEnhance.Contrast(c).enhance(1.08)
    return c

def back_master():
    c=front_master()
    # Overlay a true back construction so it is not a mirrored front.
    back=mask_poly([(.355,.205),(.50,.16),(.645,.205),(.66,.56),(.61,.78),(.50,.865),(.39,.78),(.34,.56)])
    c.alpha_composite(painted_material(back,"north",1400,(37,42,40),(145,151,144),(225,218,200),.14,True))
    bevel(c,back,4)
    y=mask_poly([(.285,.225),(.385,.145),(.50,.198),(.615,.145),(.715,.225),(.66,.31),(.50,.275),(.34,.31)])
    c.alpha_composite(painted_material(y,"north",1410,(43,47,44),(157,161,151),(231,224,205),.13,True))
    bevel(c,y,4)
    stroke(c,[ (.50,.195),(.50,.835) ],(67,111,123,170),7,.55)
    stroke(c,[ (.50,.195),(.50,.835) ],(184,202,199,72),2,.25)
    stroke(c,[ (.335,.39),(.50,.47),(.665,.39) ],(93,118,119,112),4,.4)
    stroke(c,[ (.36,.61),(.50,.68),(.64,.61) ],(93,118,119,102),4,.4)
    wrinkles(c,[
        ([(.39,.31),(.375,.53),(.40,.78)],.82),
        ([(.61,.31),(.625,.53),(.60,.78)],.82)
    ])
    return ImageEnhance.Contrast(c).enhance(1.06)

def side_master():
    c=Image.new("RGBA",(S,S),(0,0,0,0))
    main=mask_poly([(.37,.10),(.49,.075),(.61,.095),(.715,.16),(.785,.255),(.805,.42),(.77,.565),
                    (.72,.675),(.72,.82),(.635,.94),(.50,.925),(.365,.875),(.285,.735),(.265,.575),
                    (.215,.455),(.195,.30),(.245,.19)])
    c.alpha_composite(painted_material(main,"east",1500,(30,34,34),(139,145,139),(225,219,202),.14,True))
    bevel(c,main,7)

    sleeve=mask_poly([(.22,.23),(.34,.17),(.43,.25),(.42,.43),(.36,.58),(.28,.59),(.21,.49),(.18,.34)])
    c.alpha_composite(painted_material(sleeve,"east",1510,(5,8,9),(34,42,43),(91,99,95),.18,True))
    bevel(c,sleeve,5,dark=(3,5,6,165),light=(128,132,125,58))

    cuff=mask_poly([(.20,.35),(.29,.30),(.36,.39),(.34,.54),(.27,.58),(.20,.49)])
    c.alpha_composite(painted_material(cuff,"east",1520,(43,47,45),(155,158,148),(231,225,207),.12,True))
    bevel(c,cuff,4)

    collar=mask_poly([(.36,.10),(.47,.075),(.58,.085),(.63,.13),(.59,.20),(.48,.23),(.39,.19)])
    c.alpha_composite(painted_material(collar,"east",1530,(2,4,5),(24,29,29),(68,74,70),.10,True))
    bevel(c,collar,4,dark=(1,2,3,180),light=(133,137,129,64))

    yoke=mask_poly([(.24,.22),(.39,.13),(.56,.12),(.70,.18),(.73,.27),(.60,.33),(.41,.32)])
    c.alpha_composite(painted_material(yoke,"east",1540,(46,49,46),(161,163,153),(235,228,209),.12,True))
    bevel(c,yoke,5)

    panel=mask_poly([(.35,.30),(.60,.24),(.69,.35),(.66,.68),(.61,.88),(.47,.92),(.34,.84),(.30,.60)])
    c.alpha_composite(painted_material(panel,"east",1550,(38,43,41),(147,153,146),(229,222,203),.14,True))
    bevel(c,panel,4)

    stroke(c,[ (.41,.26),(.57,.31),(.62,.48),(.58,.82) ],(68,112,123,162),6,.45)
    stroke(c,[ (.28,.25),(.40,.29),(.56,.24),(.68,.22) ],(178,165,132,138),4,.4)
    stroke(c,[ (.37,.50),(.57,.55),(.65,.49) ],(91,118,120,110),4,.4)

    wrinkles(c,[
        ([(.27,.27),(.26,.42),(.30,.56)],1.0),
        ([(.43,.34),(.41,.54),(.44,.79)],.90),
        ([(.60,.31),(.63,.50),(.60,.73)],.72)
    ])
    stitch(c,(.38,.32),(.44,.82),28,501)
    c=grain_wear(c,main,1560)
    return ImageEnhance.Contrast(c).enhance(1.08)

for view,fn in (("south",front_master),("north",back_master),("east",side_master)):
    im=fn()
    p=OUT/f"master_{view}.png"
    im.save(p,optimize=True,compress_level=9)
    print("painted",p,im.size,im.getchannel("A").getbbox())
