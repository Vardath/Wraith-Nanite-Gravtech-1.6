from PIL import Image, ImageDraw, ImageFilter
from pathlib import Path
import numpy as np
from scipy.ndimage import distance_transform_edt, gaussian_filter
from scipy.interpolate import splprep, splev

OUT=Path("Textures/Things/Pawn/Humanlike/Apparel/Wraith")
OUT.mkdir(parents=True,exist_ok=True)
S=4; C=192; H=C*S

BODIES={
    "Male":(1.0,1.0,0),
    "Female":(.92,.94,0),
    "Thin":(.83,.88,1),
    "Fat":(1.14,1.10,0),
    "Hulk":(1.25,1.17,0),
}

def smooth_curve(points,n=120,closed=False):
    pts=np.array(points,dtype=float)
    if closed: pts=np.vstack([pts,pts[0]])
    k=min(3,len(pts)-1)
    tck,u=splprep([pts[:,0],pts[:,1]],s=0,k=k,per=closed)
    x,y=splev(np.linspace(0,1,n),tck)
    return list(zip(x,y))

def poly_mask(points):
    im=Image.new("L",(H,H),0)
    ImageDraw.Draw(im).polygon([(int(x*S),int(y*S)) for x,y in points],fill=255)
    return im

def curve_mask(points,width_px):
    im=Image.new("L",(H,H),0)
    cr=smooth_curve(points,n=100)
    ImageDraw.Draw(im).line([(int(x*S),int(y*S)) for x,y in cr],fill=255,width=max(1,int(width_px*S)),joint="curve")
    return im

def paint_mask(mask,base_rgb,grain=5,edge_strength=18,seed=0):
    arr=np.array(mask,dtype=np.float32)/255.
    inside=arr>0.01
    dist=distance_transform_edt(inside)
    dn=dist/max(1,dist.max())
    yy,xx=np.mgrid[0:H,0:H]
    grad=(-.45*(xx-H/2)-1.0*(yy-H/2))/H
    grad=(grad-grad.min())/(grad.max()-grad.min()+1e-6)-.5
    rng=np.random.default_rng(seed)
    n1=gaussian_filter(rng.normal(0,1,(H,H)),sigma=1.2*S)
    n2=gaussian_filter(rng.normal(0,1,(H,H)),sigma=5*S)
    n1=(n1-n1.mean())/(n1.std()+1e-6)
    n2=(n2-n2.mean())/(n2.std()+1e-6)
    noise=.55*n1+.45*n2
    edge=np.clip(1-dn*4,0,1)
    lumin=grad*16+noise*grain+edge*edge_strength
    out=np.zeros((H,H,4),dtype=np.uint8)
    for i,b in enumerate(base_rgb):
        out[...,i]=np.clip(b+lumin,0,255)
    out[...,3]=(arr*255).astype(np.uint8)
    return Image.fromarray(out,"RGBA")

def apply_mask(layer,mask):
    a=np.array(layer.getchannel("A"),dtype=np.uint16)
    m=np.array(mask,dtype=np.uint16)
    layer.putalpha(Image.fromarray(((a*m)//255).astype(np.uint8),"L"))
    return layer

def composite_layer(base,layer,shadow=(0,0),alpha=0):
    if alpha:
        a=layer.getchannel("A")
        sh=Image.new("RGBA",(H,H),(0,0,0,0))
        sa=Image.new("L",(H,H),0)
        sa.paste(a,(int(shadow[0]*S),int(shadow[1]*S)))
        sa=sa.filter(ImageFilter.GaussianBlur(1.2*S)).point(lambda p:int(p*alpha/255))
        sh.putalpha(sa); base.alpha_composite(sh)
    base.alpha_composite(layer)

def add_strip(base,points,width,color,hi,seed=0):
    m=curve_mask(points,width)
    sh=Image.new("RGBA",(H,H),(0,0,0,0))
    sha=m.filter(ImageFilter.GaussianBlur(1.2*S)).point(lambda p:int(p*.45))
    sh.putalpha(sha)
    off=Image.new("RGBA",(H,H),(0,0,0,0)); off.alpha_composite(sh,(S,S))
    base.alpha_composite(off)
    base.alpha_composite(paint_mask(m,color,grain=3,edge_strength=14,seed=seed))
    cr=smooth_curve(points,n=120)
    ImageDraw.Draw(base).line([(int(x*S),int(y*S-.5*S)) for x,y in cr],fill=hi+(145,),width=max(1,int(.8*S)),joint="curve")

def add_stitch(base,points,spacing=6,color=(120,105,114,180)):
    pts=smooth_curve(points,n=300)
    d=ImageDraw.Draw(base); prev=np.array(pts[0]); acc=0
    for p in pts[1:]:
        p=np.array(p); acc+=np.linalg.norm(p-prev)
        if acc>=spacing:
            x,y=p; r=1.0*S
            d.ellipse((x*S-r,y*S-r,x*S+r,y*S+r),fill=color)
            acc=0
        prev=p

def add_bio(base,points,color=(76,158,139),width=.55):
    cr=smooth_curve(points,n=120)
    g=Image.new("RGBA",(H,H),(0,0,0,0))
    gd=ImageDraw.Draw(g)
    gd.line([(int(x*S),int(y*S)) for x,y in cr],fill=color+(90,),width=max(1,int(width*6*S)),joint="curve")
    base.alpha_composite(g.filter(ImageFilter.GaussianBlur(2.5*S)))
    ImageDraw.Draw(base).line([(int(x*S),int(y*S)) for x,y in cr],fill=color+(220,),width=max(1,int(width*S)),joint="curve")

def down(im):
    im=im.resize((C,C),Image.Resampling.LANCZOS)
    arr=np.array(im)
    arr[arr[...,3]==0,:3]=0
    return Image.fromarray(arr,"RGBA")

def outline(direction,body):
    wx,shf,yoff=BODIES[body]
    cx=96; top=72+yoff; bottom=178+yoff
    if direction in ("south","north"):
        sh=36*shf; waist=27*wx; hem=34*wx
        return [(cx-10,top),(cx-20,top+5),(cx-sh,top+10),(cx-sh-8,top+20),(cx-sh-7,top+36),
                (cx-waist-4,top+47),(cx-hem,bottom-14),(cx-16,bottom),(cx,bottom-8),
                (cx+16,bottom),(cx+hem,bottom-14),(cx+waist+4,top+47),(cx+sh+7,top+36),
                (cx+sh+8,top+20),(cx+sh,top+10),(cx+20,top+5),(cx+10,top)]
    sx=1 if direction=="east" else -1
    fw=28*wx; bw=23*wx; front=cx+sx*fw; back=cx-sx*bw
    if sx>0:
        return [(back,top+7),(cx-7,top+1),(cx+9,top+3),(front,top+10),(front+7,top+20),
                (front+6,top+36),(front+1,top+51),(front+5,bottom-14),(cx+11,bottom),
                (cx-5,bottom-6),(back-9,bottom-13),(back-4,top+45),(back-9,top+22)]
    return [(back,top+7),(cx+7,top+1),(cx-9,top+3),(front,top+10),(front-7,top+20),
            (front-6,top+36),(front-1,top+51),(front-5,bottom-14),(cx-11,bottom),
            (cx+5,bottom-6),(back+9,bottom-13),(back+4,top+45),(back+9,top+22)]

def render_worn(direction,body):
    pts=outline(direction,body)
    m=poly_mask(pts)
    base=Image.new("RGBA",(H,H),(0,0,0,0))
    base.alpha_composite(paint_mask(m,(38,31,40),grain=5,edge_strength=18,seed=400+abs(hash(body+direction))%1000))
    cr=smooth_curve(pts,closed=True,n=300)
    ImageDraw.Draw(base).line([(int(x*S),int(y*S)) for x,y in cr],fill=(9,7,11,210),width=int(1.5*S),joint="curve")
    x0,y0,x1,y1=[v/S for v in m.getbbox()]; w=x1-x0; h=y1-y0; cx=(x0+x1)/2

    if direction=="south":
        lm=poly_mask([(x0+w*.17,y0+h*.10),(x0+w*.39,y0+h*.04),(cx-2,y0+h*.18),(cx-8,y0+h*.50),(x0+w*.25,y0+h*.73),(x0+w*.18,y0+h*.91)])
        rm=poly_mask([(x1-w*.17,y0+h*.10),(x1-w*.39,y0+h*.04),(cx+2,y0+h*.18),(cx+8,y0+h*.50),(x1-w*.25,y0+h*.73),(x1-w*.18,y0+h*.91)])
        composite_layer(base,paint_mask(lm,(55,42,56),grain=4,edge_strength=13,seed=401),(1,1),80)
        composite_layer(base,paint_mask(rm,(45,35,48),grain=4,edge_strength=13,seed=402),(-1,1),75)
        add_strip(base,[(x0+w*.12,y0+h*.18),(x0+w*.24,y0+h*.09),(x0+w*.35,y0+h*.14)],5.2,(86,80,78),(143,135,128),403)
        add_strip(base,[(x1-w*.12,y0+h*.18),(x1-w*.24,y0+h*.09),(x1-w*.35,y0+h*.14)],5.2,(82,76,75),(138,130,125),404)
        for i,(yy,off) in enumerate([(.36,0),(.49,2),(.63,-1)]):
            add_strip(base,[(x0+w*.24,y0+h*yy),(x0+w*.39,y0+h*(yy+.04)),(cx-5,y0+h*(yy+.08)+off)],1.8,(72,65,70),(116,108,112),405+i)
            add_strip(base,[(x1-w*.24,y0+h*(yy+.015)),(x1-w*.39,y0+h*(yy+.05)),(cx+5,y0+h*(yy+.09)+off)],1.8,(69,62,67),(111,104,109),410+i)
        add_strip(base,[(x0+w*.18,y0+h*.57),(cx,y0+h*.59),(x1-w*.18,y0+h*.57)],3.0,(45,38,44),(91,83,88),420)
        add_stitch(base,[(x0+w*.23,y0+h*.24),(x0+w*.29,y0+h*.52),(x0+w*.23,y0+h*.85)],6)
        add_stitch(base,[(x1-w*.24,y0+h*.28),(x1-w*.27,y0+h*.66)],8,(115,100,108,170))
        ImageDraw.Draw(base).line([(int(cx*S),int((y0+h*.34)*S)),(int((cx-1)*S),int((y0+h*.92)*S))],fill=(13,10,15,210),width=max(1,int(1.2*S)))
        add_bio(base,[(cx,y0+h*.23),(cx-1,y0+h*.38),(cx+1,y0+h*.49)])
    elif direction=="north":
        left=poly_mask([(x0+w*.20,y0+h*.15),(cx-5,y0+h*.07),(cx-3,y0+h*.91),(x0+w*.24,y0+h*.88)])
        right=poly_mask([(x1-w*.20,y0+h*.15),(cx+5,y0+h*.07),(cx+3,y0+h*.91),(x1-w*.24,y0+h*.88)])
        composite_layer(base,paint_mask(left,(48,37,51),grain=4,edge_strength=12,seed=430),(1,1),50)
        composite_layer(base,paint_mask(right,(43,34,46),grain=4,edge_strength=12,seed=431),(-1,1),50)
        add_strip(base,[(cx,y0+h*.10),(cx,y0+h*.35),(cx-1,y0+h*.62),(cx,y0+h*.87)],4.6,(74,67,71),(125,116,120),432)
        for i,yy in enumerate([.28,.43,.58,.73]):
            add_strip(base,[(cx-3,y0+h*yy),(x0+w*.35,y0+h*(yy+.04)),(x0+w*.22,y0+h*(yy+.08))],1.7,(67,61,66),(108,101,105),433+i)
            add_strip(base,[(cx+3,y0+h*(yy+.01)),(x1-w*.35,y0+h*(yy+.05)),(x1-w*.22,y0+h*(yy+.09))],1.7,(67,61,66),(108,101,105),438+i)
        add_strip(base,[(x0+w*.12,y0+h*.17),(x0+w*.27,y0+h*.08),(cx-6,y0+h*.15)],5.2,(83,77,76),(138,130,126),445)
        add_strip(base,[(x1-w*.12,y0+h*.17),(x1-w*.27,y0+h*.08),(cx+6,y0+h*.15)],5.2,(83,77,76),(138,130,126),446)
        add_stitch(base,[(x0+w*.25,y0+h*.23),(x0+w*.23,y0+h*.81)],6)
        add_bio(base,[(cx,y0+h*.18),(cx,y0+h*.61)],(74,150,132),.48)
    else:
        sx=1 if direction=="east" else -1
        if sx>0:
            front=x1-w*.20
            pm=poly_mask([(x0+w*.23,y0+h*.13),(x0+w*.50,y0+h*.04),(front,y0+h*.15),(front-2,y0+h*.42),
                          (x0+w*.58,y0+h*.53),(x0+w*.44,y0+h*.92),(x0+w*.23,y0+h*.88)])
            composite_layer(base,paint_mask(pm,(52,40,54),grain=4,edge_strength=13,seed=450),(1,1),70)
            for i,yy in enumerate([.38,.54,.70]):
                add_strip(base,[(x0+w*.41,y0+h*yy),(x0+w*.62,y0+h*(yy+.02)),(front-2,y0+h*(yy+.06))],1.7,(70,64,68),(113,106,110),451+i)
            add_strip(base,[(x0+w*.49,y0+h*.12),(front,y0+h*.18),(front-3,y0+h*.29)],5.0,(85,79,78),(140,132,127),455)
            add_stitch(base,[(x0+w*.34,y0+h*.22),(x0+w*.39,y0+h*.81)],6)
            add_bio(base,[(x0+w*.59,y0+h*.24),(x0+w*.61,y0+h*.49)],(75,154,135),.48)
        else:
            front=x0+w*.20
            pm=poly_mask([(x1-w*.23,y0+h*.13),(x1-w*.50,y0+h*.04),(front,y0+h*.15),(front+2,y0+h*.42),
                          (x1-w*.58,y0+h*.53),(x1-w*.44,y0+h*.92),(x1-w*.23,y0+h*.88)])
            composite_layer(base,paint_mask(pm,(52,40,54),grain=4,edge_strength=13,seed=460),(-1,1),70)
            for i,yy in enumerate([.38,.54,.70]):
                add_strip(base,[(x1-w*.41,y0+h*yy),(x1-w*.62,y0+h*(yy+.02)),(front+2,y0+h*(yy+.06))],1.7,(70,64,68),(113,106,110),461+i)
            add_strip(base,[(x1-w*.49,y0+h*.12),(front,y0+h*.18),(front+3,y0+h*.29)],5.0,(85,79,78),(140,132,127),465)
            add_stitch(base,[(x1-w*.34,y0+h*.22),(x1-w*.39,y0+h*.81)],6)
            add_bio(base,[(x1-w*.59,y0+h*.24),(x1-w*.61,y0+h*.49)],(75,154,135),.48)
    base.putalpha(m)
    return down(base)

def render_icon():
    outer=[(38,43),(62,25),(89,42),(103,42),(130,25),(154,43),(148,63),(136,75),
           (142,112),(135,159),(111,174),(96,151),(81,174),(57,159),(50,112),(56,75),(44,63)]
    m=poly_mask(outer)
    base=Image.new("RGBA",(H,H),(0,0,0,0))
    sh=Image.new("RGBA",(H,H),(0,0,0,0))
    sha=m.filter(ImageFilter.GaussianBlur(5*S)).point(lambda p:int(p*.35))
    sh.putalpha(sha); base.alpha_composite(sh,(2*S,4*S))
    base.alpha_composite(paint_mask(m,(39,31,42),grain=5,edge_strength=18,seed=300))
    lm=poly_mask([(40,44),(62,26),(72,36),(63,63),(52,88),(42,113),(34,103),(39,73)])
    rm=poly_mask([(152,44),(130,26),(120,36),(129,63),(140,88),(150,113),(158,103),(153,73)])
    composite_layer(base,paint_mask(lm,(45,35,47),grain=5,edge_strength=16,seed=301),(1,1),80)
    composite_layer(base,paint_mask(rm,(43,34,46),grain=5,edge_strength=16,seed=302),(-1,1),80)
    l=poly_mask([(53,43),(69,27),(93,45),(82,69),(72,104),(67,153),(57,158)])
    r=poly_mask([(139,43),(123,27),(99,45),(110,69),(120,104),(125,153),(135,158)])
    composite_layer(base,paint_mask(l,(59,45,60),grain=4,edge_strength=14,seed=303),(1,1),90)
    composite_layer(base,paint_mask(r,(47,37,51),grain=4,edge_strength=14,seed=304),(-1,1),90)
    col=poly_mask([(77,28),(88,18),(96,31),(104,18),(115,28),(108,45),(96,52),(84,45)])
    composite_layer(base,paint_mask(col,(68,53,70),grain=4,edge_strength=13,seed=305),(0,1),80)
    ImageDraw.Draw(base).ellipse((90*S,28*S,102*S,40*S),fill=(10,8,12,230))
    add_strip(base,[(42,45),(59,30),(78,38)],5.5,(89,83,81),(147,138,132),310)
    add_strip(base,[(150,45),(133,30),(114,38)],5.5,(85,79,78),(142,133,128),311)
    for i,p in enumerate([[(66,78),(80,82),(91,88)],[(64,94),(79,98),(91,104)],[(63,111),(78,114),(91,120)]]):
        add_strip(base,p,2.1,(76,69,73),(126,116,118),320+i)
    for i,p in enumerate([[(126,81),(111,84),(101,90)],[(128,99),(112,101),(101,106)],[(130,118),(113,119),(101,123)]]):
        add_strip(base,p,2.1,(72,66,70),(119,111,114),330+i)
    add_strip(base,[(61,105),(96,107),(131,104)],3.6,(45,38,44),(91,83,88),340)
    d=ImageDraw.Draw(base)
    d.rounded_rectangle((91*S,102*S,101*S,112*S),radius=2*S,fill=(92,86,78,240),outline=(147,138,124,240),width=S)
    add_stitch(base,[(69,51),(76,79),(71,146)],6)
    add_stitch(base,[(120,54),(114,88),(121,139)],8,(117,102,111,180))
    add_bio(base,[(96,55),(95,78),(97,93)],(76,158,139),.55)
    cr=smooth_curve(outer,closed=True,n=300)
    d.line([(int(x*S),int(y*S)) for x,y in cr],fill=(9,7,11,220),width=int(1.5*S),joint="curve")
    return down(base)

def save_clean(im,path):
    arr=np.array(im)
    arr[arr[...,3]==0,:3]=0
    Image.fromarray(arr,"RGBA").save(path,optimize=True)

save_clean(render_icon(),OUT/"WNG_HunterCoat.png")
for body in BODIES:
    save_clean(render_worn("south",body),OUT/f"WNG_HunterCoat_{body}.png")
    for direction in ("north","south","east","west"):
        save_clean(render_worn(direction,body),OUT/f"WNG_HunterCoat_{body}_{direction}.png")

files=sorted(OUT.glob("WNG_HunterCoat*.png"))
assert len(files)==26,len(files)
for p in files:
    im=Image.open(p).convert("RGBA"); im.load()
    assert im.size==(192,192)
    a=np.array(im.getchannel("A"))
    ys,xs=np.nonzero(a>0)
    assert len(xs)>0
    assert xs.min()>0 and ys.min()>0 and xs.max()<191 and ys.max()<191
    arr=np.array(im); z=arr[...,3]==0
    assert np.all(arr[z,:3]==0)
print("WNG_HUNTER_COAT_PROFESSIONAL_ART_OK",len(files))
