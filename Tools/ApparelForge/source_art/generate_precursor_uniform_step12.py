from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageChops, ImageOps
import numpy as np, hashlib, math

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/"Textures/Things/Pawn/Humanlike/Apparel/Precursor"
SRC=ROOT/"Tools/ApparelForge/source_art/final"
OUT.mkdir(parents=True,exist_ok=True)
SRC.mkdir(parents=True,exist_ok=True)

PREFIX="WNG_PrecursorUniform"
W,H=720,960

def pearlescent(seed, tint=(220,226,224), dark=(118,132,138)):
    rng=np.random.default_rng(seed)
    yy=np.linspace(0,1,H)[:,None]
    xx=np.linspace(0,1,W)[None,:]
    base=np.zeros((H,W,4),dtype=np.float32)
    # vertical textile light falloff + subtle central sheen
    sheen=0.78+0.20*np.exp(-((xx-.5)/.34)**2)
    vert=0.98-0.08*yy
    for i,c in enumerate(tint):
        base[...,i]=c*sheen*vert
    # fine fibre grain and broad clouding
    fine=rng.normal(0,1,(H,W))
    cloud=rng.normal(0,1,(H//16+1,W//16+1))
    cloud_img=Image.fromarray(np.uint8((cloud-cloud.min())/(cloud.max()-cloud.min()+1e-9)*255),"L").resize((W,H),Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(14))
    cloud=(np.asarray(cloud_img,np.float32)/255-.5)*2
    base[...,:3]+=fine[...,None]*3.6 + cloud[...,None]*5.0
    # micro longitudinal weave
    weave=(np.sin(np.arange(W)[None,:]*0.55)+np.sin(np.arange(H)[:,None]*0.35))*0.75
    base[...,:3]+=weave[...,None]
    base[...,3]=255
    return Image.fromarray(np.uint8(np.clip(base,0,255)),"RGBA")

MAT=pearlescent(12012)

def mpoly(points, blur=0):
    m=Image.new("L",(W,H),0)
    d=ImageDraw.Draw(m)
    d.polygon([(int(x*W),int(y*H)) for x,y in points],fill=255)
    return m.filter(ImageFilter.GaussianBlur(blur)) if blur else m

def mellipse(box):
    m=Image.new("L",(W,H),0)
    d=ImageDraw.Draw(m)
    d.ellipse((int(box[0]*W),int(box[1]*H),int(box[2]*W),int(box[3]*H)),fill=255)
    return m

def fill_tex(base,mask,brightness=1.0,tint=None):
    tex=MAT.copy()
    if brightness!=1:
        tex=ImageEnhance.Brightness(tex).enhance(brightness)
    if tint:
        arr=np.array(tex).astype(np.float32)
        arr[...,:3]=arr[...,:3]*.72+np.array(tint,np.float32)*.28
        tex=Image.fromarray(np.uint8(np.clip(arr,0,255)),"RGBA")
    tex.putalpha(mask)
    base.alpha_composite(tex)

def bevel(base,mask,width=5):
    dil=mask.filter(ImageFilter.MaxFilter(width*2+1))
    ero=mask.filter(ImageFilter.MinFilter(width*2+1))
    outer=ImageChops.subtract(dil,mask)
    inner=ImageChops.subtract(mask,ero)
    lo=Image.new("RGBA",(W,H),(50,60,64,180)); lo.putalpha(outer.point(lambda p:int(p*.72)))
    hi=Image.new("RGBA",(W,H),(255,250,230,165)); hi.putalpha(inner.point(lambda p:int(p*.52)))
    base.alpha_composite(lo); base.alpha_composite(hi)

def cyan(base,pts,width=7,glow=18):
    p=[(int(x*W),int(y*H)) for x,y in pts]
    g=Image.new("RGBA",(W,H),(0,0,0,0)); d=ImageDraw.Draw(g)
    d.line(p,fill=(40,170,255,150),width=glow,joint="curve")
    g=g.filter(ImageFilter.GaussianBlur(max(2,glow//2))); base.alpha_composite(g)
    d=ImageDraw.Draw(base)
    d.line(p,fill=(74,190,255,235),width=width,joint="curve")
    d.line(p,fill=(220,250,255,190),width=max(2,width//3),joint="curve")

def gold(base,pts,width=4):
    p=[(int(x*W),int(y*H)) for x,y in pts]
    d=ImageDraw.Draw(base)
    d.line(p,fill=(157,141,102,205),width=width,joint="curve")
    d.line(p,fill=(235,228,198,110),width=max(1,width//2),joint="curve")

def front():
    c=Image.new("RGBA",(W,H),(0,0,0,0))
    shell=mpoly([
        (.34,.07),(.66,.07),(.72,.10),(.83,.14),(.90,.24),(.86,.48),(.79,.56),
        (.76,.79),(.68,.92),(.53,.88),(.50,.94),(.47,.88),(.32,.92),(.24,.79),
        (.21,.56),(.14,.48),(.10,.24),(.17,.14),(.28,.10)
    ],2)
    fill_tex(c,shell,1.05,(218,224,222)); bevel(c,shell,6)

    # underarm flex zones
    for p in [
        [(.14,.26),(.25,.20),(.31,.43),(.25,.58),(.16,.50)],
        [(.86,.26),(.75,.20),(.69,.43),(.75,.58),(.84,.50)]
    ]:
        m=mpoly(p,1); fill_tex(c,m,.56,(86,101,108)); bevel(c,m,3)

    # upper torso pale panels
    for p in [
        [(.28,.13),(.47,.10),(.49,.34),(.38,.49),(.27,.36)],
        [(.72,.13),(.53,.10),(.51,.34),(.62,.49),(.73,.36)]
    ]:
        m=mpoly(p,1); fill_tex(c,m,1.18,(236,239,231)); bevel(c,m,4)

    # long uniform skirts
    for p in [
        [(.25,.48),(.40,.42),(.47,.53),(.42,.86),(.29,.91),(.22,.77)],
        [(.75,.48),(.60,.42),(.53,.53),(.58,.86),(.71,.91),(.78,.77)]
    ]:
        m=mpoly(p,1); fill_tex(c,m,1.01,(205,216,216)); bevel(c,m,4)

    # central self-aligning fibre placket
    m=mpoly([(.45,.18),(.55,.18),(.57,.72),(.50,.88),(.43,.72)],1)
    fill_tex(c,m,.82,(125,151,160)); bevel(c,m,3)

    # open Ancient high collar
    collar=ImageChops.subtract(mellipse((.36,.045,.64,.15)),mellipse((.41,.066,.59,.125)))
    fill_tex(c,collar,.90,(143,158,162)); bevel(c,collar,3)

    cyan(c,[(.50,.14),(.50,.82)],7,18)
    cyan(c,[(.31,.17),(.42,.32),(.38,.58),(.34,.82)],5,14)
    cyan(c,[(.69,.17),(.58,.32),(.62,.58),(.66,.82)],5,14)
    cyan(c,[(.22,.30),(.25,.48)],4,12)
    cyan(c,[(.78,.30),(.75,.48)],4,12)

    d=ImageDraw.Draw(c)
    diamond=[(int(.50*W),int(.24*H)),(int(.535*W),int(.285*H)),(int(.50*W),int(.33*H)),(int(.465*W),int(.285*H))]
    d.polygon(diamond,fill=(52,148,205,235),outline=(214,245,255,250))
    d.line([diamond[0],diamond[2]],fill=(231,252,255,200),width=3)

    gold(c,[(.28,.13),(.38,.49),(.29,.91)],4)
    gold(c,[(.72,.13),(.62,.49),(.71,.91)],4)
    gold(c,[(.43,.72),(.50,.88),(.57,.72)],3)
    return c

def back():
    c=front()
    m=mpoly([(.34,.12),(.66,.12),(.66,.47),(.58,.58),(.50,.70),(.42,.58),(.34,.47)],1)
    fill_tex(c,m,1.10,(220,225,221)); bevel(c,m,4)
    cyan(c,[(.34,.16),(.50,.31),(.66,.16)],6,16)
    cyan(c,[(.50,.31),(.50,.80)],7,18)
    cyan(c,[(.39,.48),(.34,.82)],4,12)
    cyan(c,[(.61,.48),(.66,.82)],4,12)
    gold(c,[(.34,.13),(.50,.29),(.66,.13)],4)
    return c

def side():
    c=Image.new("RGBA",(W,H),(0,0,0,0))
    shell=mpoly([
        (.35,.07),(.62,.08),(.77,.13),(.86,.23),(.84,.49),(.76,.58),(.73,.83),
        (.58,.92),(.36,.87),(.22,.73),(.20,.48),(.16,.30),(.22,.16)
    ],2)
    fill_tex(c,shell,1.05,(216,223,221)); bevel(c,shell,6)

    for p,t,b in [
        ([ (.21,.20),(.46,.12),(.60,.25),(.52,.45),(.27,.42) ],(235,238,229),1.16),
        ([ (.28,.43),(.63,.35),(.75,.58),(.59,.86),(.30,.78) ],(202,214,215),1.0),
        ([ (.18,.29),(.30,.22),(.36,.53),(.27,.65),(.19,.51) ],(91,105,111),.60),
    ]:
        m=mpoly(p,1); fill_tex(c,m,b,t); bevel(c,m,4)

    collar=ImageChops.subtract(mellipse((.32,.045,.60,.14)),mellipse((.37,.065,.56,.12)))
    fill_tex(c,collar,.9,(145,160,163)); bevel(c,collar,3)
    cyan(c,[(.38,.12),(.52,.28),(.56,.50),(.50,.82)],6,16)
    cyan(c,[(.27,.29),(.30,.52)],4,12)
    gold(c,[(.23,.18),(.48,.11),(.61,.27),(.54,.47),(.59,.84)],4)
    return c

FRONT=front()
BACK=back()
SIDE=side()

FRONT.save(SRC/"PrecursorUniform_Master_Front.png",optimize=True)
BACK.save(SRC/"PrecursorUniform_Master_Back.png",optimize=True)
SIDE.save(SRC/"PrecursorUniform_Master_Side.png",optimize=True)
sheet=Image.new("RGBA",(W*3,H),(0,0,0,0))
sheet.alpha_composite(FRONT,(0,0)); sheet.alpha_composite(BACK,(W,0)); sheet.alpha_composite(SIDE,(W*2,0))
sheet.save(SRC/"PrecursorUniform_Master.png",optimize=True)

BODIES={
    "Male":{"front":(43,73,149,169),"side":(58,73,134,169)},
    "Female":{"front":(46,74,146,169),"side":(61,74,131,169)},
    "Thin":{"front":(51,74,141,169),"side":(64,74,128,169)},
    "Fat":{"front":(36,73,156,170),"side":(51,73,141,170)},
    "Hulk":{"front":(31,72,161,170),"side":(47,72,145,170)},
}

def fit(master,box,mirror=False):
    im=ImageOps.mirror(master) if mirror else master
    bb=im.getchannel("A").getbbox(); im=im.crop(bb)
    x0,y0,x1,y1=box; tw=x1-x0; th=y1-y0
    sc=min(tw/im.width,th/im.height)
    nw,nh=max(1,round(im.width*sc)),max(1,round(im.height*sc))
    im=im.resize((nw,nh),Image.Resampling.LANCZOS)
    im=ImageEnhance.Sharpness(im).enhance(1.20)
    out=Image.new("RGBA",(192,192),(0,0,0,0))
    px=x0+(tw-nw)//2; py=y0+(th-nh)//2
    a=im.getchannel("A")
    edge=ImageChops.subtract(a.filter(ImageFilter.MaxFilter(3)),a)
    e=Image.new("RGBA",im.size,(18,25,29,155)); e.putalpha(edge)
    out.alpha_composite(e,(px,py)); out.alpha_composite(im,(px,py))
    return out

male_front=None
for body in ["Male","Female","Thin","Fat","Hulk"]:
    south=fit(FRONT,BODIES[body]["front"])
    north=fit(BACK,BODIES[body]["front"])
    east=fit(SIDE,BODIES[body]["side"])
    west=fit(SIDE,BODIES[body]["side"],True)
    if body=="Male": male_front=south.copy()
    variants={
        f"{PREFIX}_{body}.png":south,
        f"{PREFIX}_{body}_south.png":south,
        f"{PREFIX}_{body}_north.png":north,
        f"{PREFIX}_{body}_east.png":east,
        f"{PREFIX}_{body}_west.png":west,
    }
    for name,im in variants.items():
        im.save(OUT/name,optimize=True)

# inventory tile rebuilt from the finished master art
tile=fit(FRONT,(38,38,154,174))
tile.save(OUT/f"{PREFIX}.png",optimize=True)

# validation
expected=[OUT/f"{PREFIX}.png"]
for b in ["Male","Female","Thin","Fat","Hulk"]:
    expected.append(OUT/f"{PREFIX}_{b}.png")
    expected.extend(OUT/f"{PREFIX}_{b}_{d}.png" for d in ["north","south","east","west"])
assert len(expected)==26
for p in expected:
    im=Image.open(p)
    assert im.size==(192,192) and im.mode=="RGBA" and im.getchannel("A").getbbox()
print("generated",len(expected),"Step 12 Precursor Uniform textures from finished master art")
