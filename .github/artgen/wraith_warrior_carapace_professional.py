from PIL import Image, ImageDraw, ImageFilter
from pathlib import Path
import numpy as np, math, random

OUT=Path("Textures/Things/Pawn/Humanlike/Apparel/Wraith")
OUT.mkdir(parents=True,exist_ok=True)
SS=5; C=192; W=C*SS
rng=np.random.default_rng(93217)
BODIES={
 'Male':(1.0,1.0,0), 'Female':(.92,.94,0), 'Thin':(.84,.88,1),
 'Fat':(1.14,1.10,0), 'Hulk':(1.26,1.18,0)
}

def S(v): return int(round(v*SS))
def canvas(): return Image.new('RGBA',(W,W),(0,0,0,0))
def pmask(points):
    im=Image.new('L',(W,W),0); ImageDraw.Draw(im).polygon([(S(x),S(y)) for x,y in points],fill=255); return im

def mask_tex(mask, base, seed=1, grain=5, ridge=16, gloss=10):
    a=np.asarray(mask,dtype=np.float32)/255.; yy,xx=np.mgrid[0:W,0:W]
    noise=rng.normal(0,1,(W,W))
    n1=np.asarray(Image.fromarray(((noise-noise.min())/(noise.max()-noise.min())*255).astype('uint8')).filter(ImageFilter.GaussianBlur(SS*0.7)),dtype=np.float32)
    n1=(n1-n1.mean())/(n1.std()+1e-5)
    n2=np.asarray(Image.fromarray(((noise[::-1]-noise.min())/(noise.max()-noise.min())*255).astype('uint8')).filter(ImageFilter.GaussianBlur(SS*3.5)),dtype=np.float32)
    n2=(n2-n2.mean())/(n2.std()+1e-5)
    light=(-0.65*(xx-W*.48)-0.95*(yy-W*.35))/W
    light=(light-light.mean())
    edge=np.asarray(mask.filter(ImageFilter.GaussianBlur(SS*1.2)),dtype=np.float32)/255.
    inner=np.clip((a-edge)*4,0,1)
    lum=light*gloss + n1*grain + n2*grain*.65 - inner*ridge
    out=np.zeros((W,W,4),dtype=np.uint8)
    for i,b in enumerate(base): out[...,i]=np.clip(b+lum,0,255)
    out[...,3]=(a*255).astype(np.uint8)
    return Image.fromarray(out,'RGBA')

def add_shadow(base,mask,dx=1.2,dy=1.5,opacity=110,blur=1.5):
    sh=Image.new('RGBA',(W,W),(0,0,0,0)); aa=mask.filter(ImageFilter.GaussianBlur(S(blur))).point(lambda p:int(p*opacity/255)); sh.putalpha(aa)
    base.alpha_composite(sh,(S(dx),S(dy)))

def poly_layer(base,pts,color,seed,shadow=True,outline=(12,9,14,230),highlight=True):
    m=pmask(pts)
    if shadow: add_shadow(base,m)
    lay=mask_tex(m,color,seed,grain=4,ridge=12,gloss=13); base.alpha_composite(lay)
    d=ImageDraw.Draw(base)
    p=[(S(x),S(y)) for x,y in pts]
    d.line(p+[p[0]],fill=outline,width=max(1,S(1.2)),joint='curve')
    if highlight:
        d.line(p[:max(2,len(p)//2+1)],fill=(150,140,150,110),width=max(1,S(.55)),joint='curve')
    return m

def curve(base,pts,color,width=1.5,glow=False):
    if len(pts)==3:
        p0=np.array(pts[0],float); p1=np.array(pts[1],float); p2=np.array(pts[2],float)
        q=[]
        for t in np.linspace(0,1,48):
            v=(1-t)**2*p0+2*(1-t)*t*p1+t*t*p2; q.append((S(v[0]),S(v[1])))
    else:
        q=[(S(x),S(y)) for x,y in pts]
    d=ImageDraw.Draw(base)
    if glow:
        g=canvas(); ImageDraw.Draw(g).line(q,fill=color[:3]+(75,),width=max(1,S(width*4)),joint='curve'); base.alpha_composite(g.filter(ImageFilter.GaussianBlur(S(2))))
    d.line(q,fill=color,width=max(1,S(width)),joint='curve')
    if width>=1.5 and color[0]>85:
        d.line([(x,y-S(.45)) for x,y in q],fill=(190,181,174,75),width=max(1,S(.38)),joint='curve')

def stitch(base,pts,n=9):
    ps=np.array(pts,float); seg=np.linalg.norm(np.diff(ps,axis=0),axis=1); total=seg.sum(); targets=np.linspace(0,total,n+2)[1:-1]
    acc=0; j=0; d=ImageDraw.Draw(base)
    for t in targets:
        while j<len(seg)-1 and acc+seg[j]<t: acc+=seg[j]; j+=1
        frac=(t-acc)/(seg[j] or 1); p=ps[j]*(1-frac)+ps[j+1]*frac
        x,y=p; r=S(.8); d.ellipse((S(x)-r,S(y)-r,S(x)+r,S(y)+r),fill=(126,113,118,150))

def down(im):
    im=im.resize((C,C),Image.Resampling.LANCZOS); arr=np.array(im); arr[arr[...,3]==0,:3]=0; return Image.fromarray(arr,'RGBA')

def outline(body,direction):
    wx,shf,yoff=BODIES[body]; cx=96; top=70+yoff; bottom=177+yoff
    if direction in ('south','north'):
        sh=40*shf; waist=29*wx; hem=31*wx
        return [(cx-11,top),(cx-21,top+5),(cx-sh,top+11),(cx-sh-10,top+23),(cx-sh-8,top+43),
                (cx-waist-3,top+55),(cx-hem,bottom-13),(cx-13,bottom),(cx,bottom-6),(cx+13,bottom),
                (cx+hem,bottom-13),(cx+waist+3,top+55),(cx+sh+8,top+43),(cx+sh+10,top+23),(cx+sh,top+11),(cx+21,top+5),(cx+11,top)]
    sx=1 if direction=='east' else -1; front=cx+sx*31*wx; back=cx-sx*24*wx
    if sx>0:
        return [(back,top+7),(cx-8,top+1),(cx+10,top+3),(front,top+11),(front+8,top+22),(front+5,top+39),(front,top+54),
                (front+5,bottom-13),(cx+12,bottom),(cx-5,bottom-6),(back-10,bottom-12),(back-5,top+48),(back-11,top+24)]
    return [(back,top+7),(cx+8,top+1),(cx-10,top+3),(front,top+11),(front-8,top+22),(front-5,top+39),(front,top+54),
            (front-5,bottom-13),(cx-12,bottom),(cx+5,bottom-6),(back+10,bottom-12),(back+5,top+48),(back+11,top+24)]

def render_worn(body,direction):
    pts=outline(body,direction); m=pmask(pts); base=canvas(); add_shadow(base,m,0,1.5,80,1.4); base.alpha_composite(mask_tex(m,(27,24,31),1,grain=4,ridge=12,gloss=12))
    x0,y0,x1,y1=[v/SS for v in m.getbbox()]; w=x1-x0; h=y1-y0; cx=(x0+x1)/2
    if direction=='south':
        poly_layer(base,[(x0+w*.05,y0+h*.17),(x0+w*.17,y0+h*.07),(x0+w*.34,y0+h*.11),(x0+w*.39,y0+h*.25),(x0+w*.23,y0+h*.31),(x0+w*.09,y0+h*.28)],(74,66,74),11)
        poly_layer(base,[(x1-w*.05,y0+h*.18),(x1-w*.15,y0+h*.08),(x1-w*.31,y0+h*.12),(x1-w*.37,y0+h*.27),(x1-w*.22,y0+h*.34),(x1-w*.08,y0+h*.29)],(65,58,67),12)
        poly_layer(base,[(cx-5,y0+h*.11),(x0+w*.31,y0+h*.21),(x0+w*.36,y0+h*.50),(cx-3,y0+h*.60)],(58,49,61),13)
        poly_layer(base,[(cx+5,y0+h*.11),(x1-w*.31,y0+h*.21),(x1-w*.36,y0+h*.50),(cx+3,y0+h*.60)],(49,43,53),14)
        for yy in [.28,.39,.50]:
            curve(base,[(x0+w*.18,y0+h*yy),(x0+w*.37,y0+h*(yy+.025)),(cx-4,y0+h*(yy+.055))],(126,119,111,235),2.2)
            curve(base,[(x1-w*.18,y0+h*(yy+.01)),(x1-w*.37,y0+h*(yy+.035)),(cx+4,y0+h*(yy+.065))],(113,107,103,230),2.0)
        for i,xx in enumerate([.28,.40,.60,.72]):
            poly_layer(base,[(x0+w*xx,y0+h*.58),(x0+w*(xx+.09),y0+h*.57),(x0+w*(xx+.08),y0+h*.84),(x0+w*(xx+.01),y0+h*.89)],(48,42,51),20+i,shadow=True,highlight=False)
        curve(base,[(cx,y0+h*.17),(cx-1,y0+h*.42),(cx+1,y0+h*.64)],(104,96,98,235),2.0)
        curve(base,[(cx,y0+h*.20),(cx-1,y0+h*.36),(cx+1,y0+h*.49)],(65,137,125,210),.65,True)
        stitch(base,[(x0+w*.17,y0+h*.34),(x0+w*.23,y0+h*.72)],8)
    elif direction=='north':
        poly_layer(base,[(x0+w*.07,y0+h*.18),(x0+w*.20,y0+h*.08),(x0+w*.38,y0+h*.14),(x0+w*.40,y0+h*.32),(x0+w*.24,y0+h*.35)],(70,63,72),31)
        poly_layer(base,[(x1-w*.07,y0+h*.18),(x1-w*.20,y0+h*.08),(x1-w*.38,y0+h*.14),(x1-w*.40,y0+h*.32),(x1-w*.24,y0+h*.35)],(63,57,65),32)
        for i,yy in enumerate([.12,.25,.38,.51,.64,.77]):
            poly_layer(base,[(cx-9,y0+h*yy),(cx,y0+h*(yy-.04)),(cx+9,y0+h*yy),(cx+7,y0+h*(yy+.08)),(cx,y0+h*(yy+.11)),(cx-7,y0+h*(yy+.08))],(78-3*i,70-2*i,77-2*i),40+i,shadow=True,highlight=True)
        curve(base,[(cx,y0+h*.15),(cx,y0+h*.74)],(61,129,118,200),.55,True)
        stitch(base,[(x0+w*.24,y0+h*.28),(x0+w*.25,y0+h*.75)],7)
    else:
        east=direction=='east'
        if east:
            front=x1-w*.10; back=x0+w*.13
            poly_layer(base,[(back,y0+h*.16),(x0+w*.34,y0+h*.06),(x0+w*.55,y0+h*.10),(x0+w*.60,y0+h*.28),(x0+w*.37,y0+h*.33)],(70,63,72),61)
            poly_layer(base,[(x0+w*.47,y0+h*.17),(front,y0+h*.18),(front-2,y0+h*.39),(x0+w*.57,y0+h*.49)],(55,48,59),62)
            for yy in [.40,.53,.66]: curve(base,[(x0+w*.37,y0+h*yy),(x0+w*.59,y0+h*(yy+.02)),(front-3,y0+h*(yy+.055))],(119,112,108,225),1.8)
            curve(base,[(x0+w*.58,y0+h*.23),(x0+w*.62,y0+h*.51)],(63,134,121,205),.55,True)
            stitch(base,[(x0+w*.30,y0+h*.28),(x0+w*.34,y0+h*.74)],7)
        else:
            front=x0+w*.10; back=x1-w*.13
            poly_layer(base,[(back,y0+h*.16),(x1-w*.34,y0+h*.06),(x1-w*.55,y0+h*.10),(x1-w*.60,y0+h*.28),(x1-w*.37,y0+h*.33)],(70,63,72),71)
            poly_layer(base,[(x1-w*.47,y0+h*.17),(front,y0+h*.18),(front+2,y0+h*.39),(x1-w*.57,y0+h*.49)],(55,48,59),72)
            for yy in [.40,.53,.66]: curve(base,[(x1-w*.37,y0+h*yy),(x1-w*.59,y0+h*(yy+.02)),(front+3,y0+h*(yy+.055))],(119,112,108,225),1.8)
            curve(base,[(x1-w*.58,y0+h*.23),(x1-w*.62,y0+h*.51)],(63,134,121,205),.55,True)
            stitch(base,[(x1-w*.30,y0+h*.28),(x1-w*.34,y0+h*.74)],7)
    for frac in [.32,.48,.66]:
        if direction in ('south','north'):
            curve(base,[(x0+w*frac,y0+h*.62),(x0+w*(frac-.015),y0+h*.88)],(8,7,10,80),.7)
    base.putalpha(m)
    return down(base)

def render_icon():
    base=canvas()
    outer=[(37,48),(58,27),(83,36),(96,49),(109,36),(134,27),(155,48),(147,73),(136,81),(137,142),(116,162),(96,153),(76,162),(55,142),(56,81),(45,73)]
    m=pmask(outer); add_shadow(base,m,1.5,3,115,3); base.alpha_composite(mask_tex(m,(30,26,34),90,grain=5,ridge=14,gloss=14))
    poly_layer(base,[(38,49),(58,27),(80,35),(83,53),(63,65),(47,64)],(76,68,76),91)
    poly_layer(base,[(154,49),(134,27),(112,35),(109,53),(129,65),(145,64)],(68,61,69),92)
    poly_layer(base,[(83,38),(96,50),(92,101),(72,120),(61,110),(63,67)],(57,48,59),93)
    poly_layer(base,[(109,38),(96,50),(100,101),(120,120),(131,110),(129,67)],(49,43,53),94)
    for yy in [70,85,100]:
        curve(base,[(61,yy),(79,yy+4),(92,yy+10)],(128,120,112,240),2.6)
        curve(base,[(131,yy+1),(113,yy+5),(100,yy+11)],(115,109,104,235),2.4)
    for i,x in enumerate([70,82,98,110]):
        poly_layer(base,[(x,111),(x+12,110),(x+10,147),(x+2,154)],(47,41,50),100+i,highlight=False)
    curve(base,[(96,57),(95,83),(97,107)],(68,143,128,210),.65,True)
    stitch(base,[(61,68),(67,110)],6)
    ImageDraw.Draw(base).ellipse((S(88),S(36),S(104),S(52)),fill=(8,6,9,255))
    return down(base)

def save(im,path):
    arr=np.array(im); arr[arr[...,3]==0,:3]=0; Image.fromarray(arr,'RGBA').save(path,optimize=True)

save(render_icon(),OUT/'WNG_WarriorCarapace.png')
for direction in ('north','south','east','west'):
    save(render_worn('Male',direction),OUT/f'WNG_WarriorCarapace_{direction}.png')
for body in BODIES:
    save(render_worn(body,'south'),OUT/f'WNG_WarriorCarapace_{body}.png')
    for direction in ('north','south','east','west'):
        save(render_worn(body,direction),OUT/f'WNG_WarriorCarapace_{body}_{direction}.png')

files=sorted(OUT.glob('WNG_WarriorCarapace*.png'))
assert len(files)==30,len(files)
for p in files:
    im=Image.open(p).convert('RGBA'); im.load(); assert im.size==(192,192)
    a=np.array(im.getchannel('A')); ys,xs=np.nonzero(a>0); assert len(xs)
    assert xs.min()>0 and ys.min()>0 and xs.max()<191 and ys.max()<191
    arr=np.array(im); assert np.all(arr[arr[...,3]==0,:3]==0)
print('WNG_WARRIOR_CARAPACE_OK',len(files))
