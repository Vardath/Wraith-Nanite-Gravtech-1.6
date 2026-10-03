from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageChops, ImageOps
import numpy as np
from scipy.ndimage import gaussian_filter
import random, hashlib
from functools import lru_cache

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/"ArtSource/Apparel/precursor_uniform"
OUT.mkdir(parents=True,exist_ok=True)

S=1536
STYLE_DIRS=[
    ROOT/"ArtSource/Apparel/human_form_uniform",
    ROOT/"ArtSource/Apparel/wraith_hunter_coat",
    ROOT/"ArtSource/Apparel/wraith_queen_raiment",
]

def _alpha_bbox(im):
    return im.getchannel("A").getbbox()

@lru_cache(maxsize=3)
def _style_field(view:str):
    rows=[]
    for d in STYLE_DIRS:
        p=d/f"master_{view}.png"
        if not p.exists():
            continue
        im=Image.open(p).convert("RGBA")
        bb=_alpha_bbox(im)
        if not bb:
            continue
        crop=im.crop(bb).resize((S,S),Image.Resampling.LANCZOS)
        arr=np.asarray(crop).astype(np.float32)
        a=arr[...,3]/255.
        rgb=arr[...,:3]
        lum=.2126*rgb[...,0]+.7152*rgb[...,1]+.0722*rgb[...,2]
        fill=np.median(lum[a>.15]) if np.any(a>.15) else 128.
        lum[a<.05]=fill
        low=gaussian_filter(lum,38)
        mid=gaussian_filter(lum,7)-gaussian_filter(lum,28)
        fine=lum-gaussian_filter(lum,2.4)
        for x in (low,mid,fine):
            x-=x.mean(); x/=x.std()+1e-6
        rows.append((low,mid,fine))
    if rows:
        return tuple(np.mean([r[i] for r in rows],axis=0) for i in range(3))
    rng=np.random.default_rng(12)
    n=rng.normal(0,1,(S,S))
    return gaussian_filter(n,38), gaussian_filter(n,7)-gaussian_filter(n,28), n-gaussian_filter(n,2.4)

def _poly(points,blur=1.4):
    m=Image.new("L",(S,S),0)
    d=ImageDraw.Draw(m)
    d.polygon([(int(x*S),int(y*S)) for x,y in points],fill=255)
    return m.filter(ImageFilter.GaussianBlur(blur)) if blur else m

def _ellipse(box,blur=1.4):
    m=Image.new("L",(S,S),0)
    ImageDraw.Draw(m).ellipse(tuple(int(v*S) for v in box),fill=255)
    return m.filter(ImageFilter.GaussianBlur(blur)) if blur else m

def _material(mask,shadow,mid,high,seed,view,rough=.13,weave=.016):
    lowf,midf,finef=_style_field(view)
    rng=np.random.default_rng(seed)
    yy,xx=np.mgrid[0:S,0:S]
    key=np.exp(-(((xx/S)-(.36 if view!="east" else .40))/.53)**2-(((yy/S)-.28)/.60)**2)
    side=np.clip(np.abs(xx/S-.5)-.20,0,.5)
    v=.29+.50*key-.16*np.clip(yy/S-.56,0,1)-.10*side
    n=rng.normal(0,1,(S,S))
    grain=gaussian_filter(n,1.1)
    v += .055*lowf + .060*midf + .018*finef + grain*.008
    v += weave*(np.sin(xx/4.0)+.72*np.sin(yy/5.4)+.36*np.sin((xx+yy)/8.5))
    v=np.clip(v,0,1)

    sh=np.array(shadow,float); mi=np.array(mid,float); hi=np.array(high,float)
    rgb=np.empty((S,S,3),np.float32)
    lo=v<.51
    q=np.clip(v/.51,0,1)
    rgb[lo]=sh+(mi-sh)*q[lo,None]
    q2=np.clip((v-.51)/.49,0,1)
    rgb[~lo]=mi+(hi-mi)*q2[~lo,None]

    a=np.asarray(mask,dtype=np.uint8)
    return Image.fromarray(np.dstack([np.uint8(np.clip(rgb,0,255)),a]),"RGBA")

def _bevel(base,mask,w=7,dark=(16,18,18,150),light=(247,239,220,92)):
    dil=mask.filter(ImageFilter.MaxFilter(w*2+1))
    ero=mask.filter(ImageFilter.MinFilter(w*2+1))
    outer=ImageChops.subtract(dil,mask)
    inner=ImageChops.subtract(mask,ero)
    sh=Image.new("RGBA",(S,S),dark)
    sh.putalpha(outer.point(lambda p:int(p*dark[3]/255)))
    hi=Image.new("RGBA",(S,S),light)
    hi.putalpha(inner.point(lambda p:int(p*light[3]/255)))
    base.alpha_composite(sh); base.alpha_composite(hi)

def _stroke(base,pts,col,w,blur=0):
    lay=Image.new("RGBA",(S,S),(0,0,0,0))
    d=ImageDraw.Draw(lay)
    d.line([(int(x*S),int(y*S)) for x,y in pts],fill=col,width=w,joint="curve")
    if blur: lay=lay.filter(ImageFilter.GaussianBlur(blur))
    base.alpha_composite(lay)

def _wrinkle(base,pts,power=.8,w=14):
    _stroke(base,pts,(9,12,12,int(70*power)),w+18,8)
    _stroke(base,[(x-.003,y-.003) for x,y in pts],(255,247,228,int(42*power)),max(4,w//2),5)

def _stitches(base,a,b,count,seed):
    rnd=random.Random(seed)
    x1,y1=a; x2,y2=b
    dx=x2-x1; dy=y2-y1; ll=(dx*dx+dy*dy)**.5
    nx=-dy/ll; ny=dx/ll
    lay=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(lay)
    for i in range(count+1):
        t=i/count
        x=x1+dx*t+rnd.uniform(-.001,.001)
        y=y1+dy*t+rnd.uniform(-.001,.001)
        ln=.004+rnd.uniform(-.001,.001)
        d.line((int((x-nx*ln)*S),int((y-ny*ln)*S),int((x+nx*ln)*S),int((y+ny*ln)*S)),
               fill=(205,207,195,90),width=2)
    base.alpha_composite(lay)

def _common_front(view="south"):
    c=Image.new("RGBA",(S,S),(0,0,0,0))
    # Long off-white Lantean field tunic: Aurora-like practical uniform, not armour.
    torso=_poly([
        (.35,.11),(.28,.15),(.25,.23),(.28,.40),(.31,.55),(.33,.67),(.31,.86),(.38,.94),
        (.48,.89),(.50,.84),(.52,.89),(.62,.94),(.69,.86),(.67,.67),(.69,.55),(.72,.40),
        (.75,.23),(.72,.15),(.65,.11),(.58,.14),(.50,.20),(.42,.14)
    ])
    c.alpha_composite(_material(torso,(29,34,34),(129,139,136),(226,223,208),101,view,.14,.018))
    _bevel(c,torso,7)

    # Long sleeves.
    sleeves=[
        _poly([(.27,.18),(.18,.20),(.12,.28),(.10,.42),(.14,.55),(.20,.59),(.26,.54),(.29,.38)]),
        _poly([(.73,.18),(.82,.20),(.88,.28),(.90,.42),(.86,.55),(.80,.59),(.74,.54),(.71,.38)])
    ]
    for i,m in enumerate(sleeves):
        c.alpha_composite(_material(m,(15,20,21),(63,73,73),(131,138,133),110+i,view,.14,.016))
        _bevel(c,m,5,dark=(7,9,9,145),light=(181,184,173,76))

    # Pale forearm wraps from off-white Aurora-style cloth.
    for i,pts in enumerate([
        [(.13,.34),(.21,.30),(.25,.39),(.23,.53),(.17,.57),(.12,.48)],
        [(.87,.34),(.79,.30),(.75,.39),(.77,.53),(.83,.57),(.88,.48)]
    ]):
        m=_poly(pts)
        c.alpha_composite(_material(m,(43,49,48),(151,159,155),(228,224,209),120+i,view,.13,.017))
        _bevel(c,m,4)

    # Dark high collar/undershirt.
    collar=_poly([(.39,.10),(.46,.075),(.50,.07),(.54,.075),(.61,.10),(.58,.18),(.50,.22),(.42,.18)])
    c.alpha_composite(_material(collar,(5,7,8),(24,31,32),(65,73,72),130,view,.10,.012))
    _bevel(c,collar,4,dark=(3,5,5,160),light=(141,148,144,70))
    neck=_poly([(.44,.075),(.50,.055),(.56,.075),(.55,.13),(.50,.165),(.45,.13)],0)
    c.putalpha(ImageChops.subtract(c.getchannel("A"),neck))

    # Layered shoulder yoke.
    yoke=_poly([(.25,.20),(.35,.12),(.42,.12),(.50,.20),(.58,.12),(.65,.12),(.75,.20),
                (.69,.29),(.59,.31),(.50,.27),(.41,.31),(.31,.29)])
    c.alpha_composite(_material(yoke,(43,48,48),(158,164,158),(234,229,213),140,view,.14,.018))
    _bevel(c,yoke,5)

    # Dark inner technical weave.
    inner=_poly([(.40,.22),(.50,.19),(.60,.22),(.60,.65),(.56,.77),(.50,.83),(.44,.77),(.40,.65)])
    c.alpha_composite(_material(inner,(6,10,11),(31,42,44),(85,96,95),150,view,.14,.014))
    _bevel(c,inner,4,dark=(3,4,4,165),light=(137,148,145,70))

    # Fitted split outer cloth.
    panels=[
        ([ (.29,.28),(.40,.22),(.44,.35),(.42,.67),(.38,.89),(.31,.92),(.27,.81),(.29,.55) ],160),
        ([ (.71,.28),(.60,.22),(.56,.35),(.58,.67),(.62,.89),(.69,.92),(.73,.81),(.71,.55) ],161)
    ]
    for pts,seed in panels:
        m=_poly(pts)
        c.alpha_composite(_material(m,(41,47,46),(154,162,157),(232,227,211),seed,view,.14,.018))
        _bevel(c,m,4)

    # Long split tails, derived from the finished garment rather than a vanilla tile.
    tails=[
        ([ (.31,.58),(.43,.55),(.47,.70),(.45,.88),(.37,.96),(.29,.90),(.28,.78) ],170),
        ([ (.69,.58),(.57,.55),(.53,.70),(.55,.88),(.63,.96),(.71,.90),(.72,.78) ],171)
    ]
    for pts,seed in tails:
        m=_poly(pts)
        c.alpha_composite(_material(m,(46,52,51),(167,174,168),(237,232,216),seed,view,.15,.019))
        _bevel(c,m,4)

    # Ancient seam language: restrained blue-grey and aged champagne metal, no neon.
    _stroke(c,[(.50,.21),(.50,.79)],(71,116,128,170),7,.6)
    _stroke(c,[(.50,.22),(.50,.79)],(198,210,205,90),2,.3)
    _stroke(c,[(.29,.28),(.40,.35),(.39,.69),(.34,.88)],(116,134,132,115),4,.5)
    _stroke(c,[(.71,.28),(.60,.35),(.61,.69),(.66,.88)],(116,134,132,115),4,.5)
    _stroke(c,[(.31,.20),(.42,.25),(.50,.22),(.58,.25),(.69,.20)],(178,171,147,145),5,.6)
    _stroke(c,[(.33,.50),(.42,.54),(.50,.51),(.58,.54),(.67,.50)],(110,130,129,110),4,.5)
    _stroke(c,[(.40,.23),(.44,.35),(.42,.68)],(160,143,103,110),3,.3)
    _stroke(c,[(.60,.23),(.56,.35),(.58,.68)],(160,143,103,110),3,.3)

    # Small Lantean diamond clasp.
    d=ImageDraw.Draw(c)
    q=[(.50,.32),(.522,.355),(.50,.39),(.478,.355)]
    d.polygon([(int(x*S),int(y*S)) for x,y in q],fill=(79,112,120,232),outline=(203,188,151,220))
    q2=[(.50,.333),(.512,.355),(.50,.377),(.488,.355)]
    d.polygon([(int(x*S),int(y*S)) for x,y in q2],fill=(128,158,158,210))

    for pts,pow in [
        ([(.18,.25),(.16,.39),(.19,.54)],1.0),
        ([(.82,.25),(.84,.39),(.81,.54)],1.0),
        ([(.34,.31),(.33,.49),(.35,.70),(.36,.87)],.95),
        ([(.66,.31),(.67,.49),(.65,.70),(.64,.87)],.95),
        ([(.42,.31),(.41,.50),(.42,.72)],.72),
        ([(.58,.31),(.59,.50),(.58,.72)],.72),
    ]:
        _wrinkle(c,pts,pow,13)

    _stitches(c,(.31,.31),(.35,.82),26,201)
    _stitches(c,(.69,.31),(.65,.82),26,202)
    _stroke(c,[(.13,.45),(.18,.50),(.23,.52)],(177,168,143,120),4,.4)
    _stroke(c,[(.87,.45),(.82,.50),(.77,.52)],(177,168,143,120),4,.4)

    return ImageEnhance.Contrast(c).enhance(1.10)

def _south():
    return _common_front("south")

def _north():
    c=_common_front("north")
    # Replace front placket with broad rear yoke/spine.
    back=_poly([(.36,.20),(.50,.16),(.64,.20),(.66,.55),(.61,.76),(.50,.86),(.39,.76),(.34,.55)])
    c.alpha_composite(_material(back,(38,44,44),(145,154,150),(226,223,209),250,"north",.14,.017))
    _bevel(c,back,4)
    yoke=_poly([(.28,.23),(.38,.15),(.50,.20),(.62,.15),(.72,.23),(.66,.31),(.50,.28),(.34,.31)])
    c.alpha_composite(_material(yoke,(45,50,49),(160,166,160),(234,230,214),251,"north",.14,.018))
    _bevel(c,yoke,4)
    _stroke(c,[(.50,.20),(.50,.82)],(72,118,129,165),7,.6)
    _stroke(c,[(.33,.38),(.50,.47),(.67,.38)],(116,134,132,110),4,.5)
    _stroke(c,[(.35,.61),(.50,.68),(.65,.61)],(116,134,132,105),4,.5)
    _wrinkle(c,[(.40,.31),(.38,.53),(.40,.77)],.75,13)
    _wrinkle(c,[(.60,.31),(.62,.53),(.60,.77)],.75,13)
    return ImageEnhance.Contrast(c).enhance(1.08)

def _east():
    c=Image.new("RGBA",(S,S),(0,0,0,0))
    body=_poly([(.38,.10),(.49,.08),(.61,.10),(.71,.16),(.78,.25),(.80,.42),(.77,.56),
                (.72,.67),(.72,.82),(.63,.93),(.50,.92),(.37,.87),(.29,.73),(.27,.57),(.22,.45),
                (.20,.29),(.25,.19)])
    c.alpha_composite(_material(body,(29,34,34),(129,139,136),(226,223,208),301,"east",.14,.018))
    _bevel(c,body,7)

    sleeve=_poly([(.22,.23),(.34,.17),(.43,.25),(.42,.43),(.36,.58),(.28,.59),(.21,.49),(.18,.34)])
    c.alpha_composite(_material(sleeve,(15,20,21),(63,73,73),(131,138,133),302,"east",.14,.016))
    _bevel(c,sleeve,5,dark=(7,9,9,145),light=(181,184,173,76))

    cuff=_poly([(.20,.35),(.29,.30),(.36,.39),(.34,.54),(.27,.58),(.20,.49)])
    c.alpha_composite(_material(cuff,(43,49,48),(151,159,155),(228,224,209),303,"east",.13,.017))
    _bevel(c,cuff,4)

    collar=_poly([(.36,.10),(.47,.075),(.58,.085),(.63,.13),(.59,.20),(.48,.23),(.39,.19)])
    c.alpha_composite(_material(collar,(5,7,8),(24,31,32),(65,73,72),304,"east",.10,.012))
    _bevel(c,collar,4,dark=(3,5,5,160),light=(141,148,144,70))

    yoke=_poly([(.24,.22),(.39,.13),(.56,.12),(.70,.18),(.73,.27),(.60,.33),(.41,.32)])
    c.alpha_composite(_material(yoke,(43,48,48),(158,164,158),(234,229,213),305,"east",.14,.018))
    _bevel(c,yoke,5)

    side=_poly([(.35,.30),(.60,.24),(.69,.35),(.66,.68),(.61,.88),(.47,.92),(.34,.84),(.30,.60)])
    c.alpha_composite(_material(side,(41,47,46),(154,162,157),(232,227,211),306,"east",.14,.018))
    _bevel(c,side,4)

    _stroke(c,[(.41,.26),(.57,.31),(.62,.48),(.58,.82)],(72,117,128,155),6,.5)
    _stroke(c,[(.28,.25),(.40,.29),(.56,.24),(.68,.22)],(178,171,147,135),4,.5)
    _stroke(c,[(.37,.50),(.57,.55),(.65,.49)],(111,130,129,105),4,.5)
    _wrinkle(c,[(.27,.27),(.26,.42),(.30,.56)],.95,13)
    _wrinkle(c,[(.43,.34),(.41,.54),(.44,.79)],.85,13)
    _wrinkle(c,[(.60,.31),(.63,.50),(.60,.73)],.65,12)
    _stitches(c,(.38,.32),(.44,.82),26,307)

    return ImageEnhance.Contrast(c).enhance(1.10)

for view,fn in (("south",_south),("north",_north),("east",_east)):
    im=fn()
    p=OUT/f"master_{view}.png"
    im.save(p,optimize=True,compress_level=9)
    print("painted",p,im.size,im.getchannel("A").getbbox())
