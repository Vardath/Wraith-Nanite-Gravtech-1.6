from PIL import Image, ImageDraw, ImageFilter, ImageChops
import numpy as np, math, os, random

from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
OUTDIR=str(ROOT/"ArtSource"/"Apparel"/"human_form_uniform")
os.makedirs(OUTDIR, exist_ok=True)

S=1024
SS=2
W=H=S*SS

def pts_scaled(pts):
    return [(int(x*SS),int(y*SS)) for x,y in pts]

def rounded_mask(view):
    m=Image.new("L",(W,H),0)
    d=ImageDraw.Draw(m)
    if view in ("south","north"):
        d.polygon(pts_scaled([
            (298,216),(383,164),(512,148),(641,164),(726,216),
            (756,330),(742,572),(704,756),(628,855),(512,892),
            (396,855),(320,756),(282,572),(268,330)
        ]), fill=255)
        d.ellipse([290*SS,150*SS,734*SS,450*SS], fill=255)
        d.rounded_rectangle([294*SS,290*SS,730*SS,812*SS], radius=150*SS, fill=255)
    else:
        d.polygon(pts_scaled([
            (382,205),(449,167),(548,157),(641,196),(692,270),
            (708,392),(688,606),(650,780),(572,858),(487,870),
            (413,821),(365,704),(340,492),(350,300)
        ]), fill=255)
        d.ellipse([360*SS,158*SS,687*SS,430*SS], fill=255)
        d.rounded_rectangle([366*SS,283*SS,686*SS,812*SS], radius=125*SS, fill=255)
    return m.filter(ImageFilter.GaussianBlur(SS*1.2))

def gradient_rgba(mask, view, seed=0):
    rng=np.random.default_rng(seed)
    yy,xx=np.mgrid[0:H,0:W]
    base=np.zeros((H,W,4),dtype=np.uint8)
    cx=0.42 if view=="east" else 0.44
    nx=xx/W
    ny=yy/H
    light=0.55+0.28*np.exp(-((nx-cx)/0.36)**2 - ((ny-0.34)/0.48)**2)
    shadow=0.15*np.clip(ny-0.62,0,1)+0.09*np.clip(nx-0.72,0,1)
    textile=(np.sin(xx/8.5)+np.sin(yy/11.0)+np.sin((xx+yy)/16.0))*1.4
    noise=rng.normal(0,2.5,(H,W))
    L=np.clip(light-shadow,0.34,0.93)
    r=105+92*L+textile+noise
    g=114+94*L+textile*0.9+noise
    b=119+98*L+textile*0.7+noise
    base[...,0]=np.clip(r,0,255)
    base[...,1]=np.clip(g,0,255)
    base[...,2]=np.clip(b,0,255)
    base[...,3]=np.array(mask)
    return Image.fromarray(base,"RGBA")

def mask_poly(points, blur=0):
    m=Image.new("L",(W,H),0)
    ImageDraw.Draw(m).polygon(pts_scaled(points), fill=255)
    return m.filter(ImageFilter.GaussianBlur(blur*SS)) if blur else m

def add_panel(im, points, top=(48,54,58,255), bottom=(20,24,27,255), outline=(178,188,190,210), width=4, seed=0):
    m=mask_poly(points, 1)
    yy=np.arange(H)[:,None]/H
    rgba=np.zeros((H,W,4),dtype=np.uint8)
    for c in range(3):
        rgba[...,c]=(top[c]*(1-yy)+bottom[c]*yy)
    rgba[...,3]=np.array(m)
    panel=Image.fromarray(rgba,"RGBA")
    rng=np.random.default_rng(seed)
    a=np.array(panel)
    n=rng.normal(0,3.0,(H,W,1))
    a[...,:3]=np.clip(a[...,:3].astype(float)+n,0,255).astype(np.uint8)
    panel=Image.fromarray(a,"RGBA")
    im.alpha_composite(panel)
    d=ImageDraw.Draw(im)
    d.line(pts_scaled(points+[points[0]]), fill=outline, width=width*SS, joint="curve")

def add_line(im, pts, fill, width, blur=0):
    layer=Image.new("RGBA",(W,H),(0,0,0,0))
    d=ImageDraw.Draw(layer)
    d.line(pts_scaled(pts), fill=fill, width=width*SS, joint="curve")
    if blur:
        layer=layer.filter(ImageFilter.GaussianBlur(blur*SS))
    im.alpha_composite(layer)

def add_ribbed(im, bbox, count=9, vertical=True, base=(39,44,47,240), hi=(100,111,114,140)):
    x0,y0,x1,y1=[int(v*SS) for v in bbox]
    layer=Image.new("RGBA",(W,H),(0,0,0,0))
    d=ImageDraw.Draw(layer)
    d.rounded_rectangle([x0,y0,x1,y1], radius=18*SS, fill=base)
    if vertical:
        step=(x1-x0)/count
        for i in range(1,count):
            x=int(x0+i*step)
            d.line([(x,y0+8*SS),(x,y1-8*SS)], fill=hi, width=max(1,SS))
    else:
        step=(y1-y0)/count
        for i in range(1,count):
            y=int(y0+i*step)
            d.line([(x0+8*SS,y),(x1-8*SS,y)], fill=hi, width=max(1,SS))
    im.alpha_composite(layer)

def add_edge_shading(im, mask):
    inner=mask.filter(ImageFilter.GaussianBlur(16*SS))
    edge=ImageChops.subtract(mask, inner)
    dark=Image.new("RGBA",(W,H),(0,0,0,0))
    dark.putalpha(edge.point(lambda p:min(110,int(p*0.5))))
    darkarr=np.array(dark); darkarr[...,:3]=[12,15,17]
    im.alpha_composite(Image.fromarray(darkarr,"RGBA"))
    rim=ImageChops.subtract(mask.filter(ImageFilter.GaussianBlur(2*SS)), mask.filter(ImageFilter.GaussianBlur(7*SS)))
    hl=Image.new("RGBA",(W,H),(196,204,204,0))
    hl.putalpha(rim.point(lambda p:min(45,int(p*0.33))))
    im.alpha_composite(hl)

def render(view):
    mask=rounded_mask(view)
    im=gradient_rgba(mask,view,{"south":10,"north":20,"east":30}[view])
    if view=="south":
        add_ribbed(im,(286,355,352,716),7,True)
        add_ribbed(im,(672,355,738,716),7,True)
        add_panel(im,[(414,203),(512,166),(610,203),(589,282),(512,318),(435,282)],
                  top=(42,48,52,255),bottom=(18,22,25,255),outline=(160,169,171,220),width=3,seed=3)
        add_panel(im,[(447,320),(512,354),(577,320),(594,530),(556,706),(512,768),(468,706),(430,530)],
                  top=(58,65,69,238),bottom=(30,34,37,245),outline=(136,148,151,210),width=3,seed=5)
        add_line(im,[(330,320),(410,364),(430,530),(392,702)],(188,198,199,210),4)
        add_line(im,[(694,320),(614,364),(594,530),(632,702)],(188,198,199,210),4)
        add_line(im,[(364,430),(430,480),(512,454),(594,480),(660,430)],(79,105,110,160),5)
        add_line(im,[(389,604),(468,638),(512,620),(556,638),(635,604)],(81,110,114,160),4)
        add_line(im,[(356,720),(430,758),(512,744),(594,758),(668,720)],(150,160,162,190),5)
        add_line(im,[(404,782),(512,812),(620,782)],(72,89,93,180),5)
        layer=Image.new("RGBA",(W,H),(0,0,0,0));d=ImageDraw.Draw(layer)
        d.rounded_rectangle([478*SS,717*SS,546*SS,748*SS],radius=10*SS,fill=(30,36,39,255),outline=(182,192,194,255),width=3*SS)
        d.rounded_rectangle([496*SS,726*SS,528*SS,740*SS],radius=4*SS,fill=(91,119,123,255))
        im.alpha_composite(layer)
        for x,y,s in [(382,505,12),(641,558,10),(405,665,8),(615,675,8)]:
            layer=Image.new("RGBA",(W,H),(0,0,0,0));d=ImageDraw.Draw(layer)
            d.polygon(pts_scaled([(x,y-s),(x+s,y),(x,y+s),(x-s,y)]),outline=(180,190,190,150),width=2*SS)
            im.alpha_composite(layer)
    elif view=="north":
        add_ribbed(im,(288,355,351,720),7,True)
        add_ribbed(im,(673,355,736,720),7,True)
        add_panel(im,[(412,207),(512,171),(612,207),(594,278),(512,310),(430,278)],
                  top=(40,46,50,255),bottom=(17,20,23,255),outline=(156,166,168,220),width=3,seed=8)
        add_panel(im,[(452,310),(512,340),(572,310),(590,544),(558,704),(512,760),(466,704),(434,544)],
                  top=(52,59,63,236),bottom=(27,31,34,246),outline=(128,141,145,210),width=3,seed=11)
        add_line(im,[(512,313),(512,754)],(183,192,193,190),3)
        add_line(im,[(336,342),(420,388),(434,544),(391,714)],(175,186,188,195),4)
        add_line(im,[(688,342),(604,388),(590,544),(633,714)],(175,186,188,195),4)
        add_line(im,[(364,448),(434,500),(512,476),(590,500),(660,448)],(76,101,106,150),5)
        add_line(im,[(384,620),(466,648),(512,632),(558,648),(640,620)],(80,105,110,150),4)
        add_line(im,[(360,718),(430,758),(512,744),(594,758),(664,718)],(146,157,159,190),5)
        layer=Image.new("RGBA",(W,H),(0,0,0,0));d=ImageDraw.Draw(layer)
        d.ellipse([485*SS,694*SS,539*SS,748*SS],fill=(30,36,39,255),outline=(178,188,191,220),width=3*SS)
        d.ellipse([499*SS,708*SS,525*SS,734*SS],fill=(82,109,114,255))
        im.alpha_composite(layer)
    else:
        add_ribbed(im,(359,362,418,716),7,True)
        add_panel(im,[(434,210),(512,171),(589,204),(628,264),(606,324),(468,314)],
                  top=(42,48,52,255),bottom=(18,22,25,255),outline=(158,168,170,220),width=3,seed=14)
        add_panel(im,[(449,326),(523,344),(596,316),(627,520),(604,704),(535,780),(464,726),(430,540)],
                  top=(55,62,66,238),bottom=(28,32,35,246),outline=(132,144,147,210),width=3,seed=17)
        add_line(im,[(409,334),(450,392),(430,540),(454,724)],(180,190,191,200),4)
        add_line(im,[(604,326),(628,420),(625,575),(596,714)],(176,186,188,190),4)
        add_line(im,[(442,458),(522,488),(612,450)],(79,106,111,160),5)
        add_line(im,[(449,612),(535,642),(612,610)],(80,107,112,160),4)
        layer=Image.new("RGBA",(W,H),(0,0,0,0));d=ImageDraw.Draw(layer)
        d.rounded_rectangle([500*SS,706*SS,563*SS,736*SS],radius=9*SS,fill=(29,35,38,255),outline=(181,191,193,240),width=3*SS)
        im.alpha_composite(layer)
    im.putalpha(ImageChops.multiply(im.getchannel("A"),mask))
    add_edge_shading(im,mask)
    return im.resize((S,S),Image.Resampling.LANCZOS)

for v in ("south","north","east"):
    im=render(v)
    p=os.path.join(OUTDIR,f"master_{v}.png")
    im.save(p,optimize=True)
    print(v,p,os.path.getsize(p))

print("fresh Step 5 masters generated: art-first pawn-fit build final")
