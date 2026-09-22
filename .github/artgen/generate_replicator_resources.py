from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math, random

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Textures/Things/Item/Resource/Replicator"
OUT.mkdir(parents=True, exist_ok=True)
SIZE = 512
METAL=(92,101,102,255); DARK=(28,34,36,255); DARK2=(45,53,55,255)
EDGE=(12,17,18,255); HI=(176,187,185,255); CYAN=(48,218,196,255); CYAN2=(102,255,232,255)

def add_glow(base, mask, radius=8, alpha=105):
    blur=mask.filter(ImageFilter.GaussianBlur(radius))
    solid=Image.new('RGBA', base.size, (48,218,196,alpha))
    solid.putalpha(blur.point(lambda x: min(255, int(x*alpha/255))))
    base.alpha_composite(solid)

def module(w,h,seed=0,active=True,broken=False):
    random.Random(seed)
    pad=18; im=Image.new('RGBA',(w+pad*2,h+pad*2),(0,0,0,0)); d=ImageDraw.Draw(im)
    x0=y0=pad; x1=pad+w; y1=pad+h; r=max(5,min(w,h)//8)
    sh=Image.new('RGBA',im.size,(0,0,0,0)); sd=ImageDraw.Draw(sh)
    sd.rounded_rectangle((x0+5,y0+7,x1+8,y1+10),radius=r,fill=(0,0,0,135))
    im.alpha_composite(sh.filter(ImageFilter.GaussianBlur(5))); d=ImageDraw.Draw(im)
    d.rounded_rectangle((x0,y0,x1,y1),radius=r,fill=EDGE)
    d.rounded_rectangle((x0+3,y0+3,x1-3,y1-3),radius=max(3,r-2),fill=METAL)
    d.line((x0+r,y0+4,x1-r,y0+4),fill=HI,width=3)
    d.line((x0+4,y0+r,x0+4,y1-r),fill=(150,160,159,255),width=2)
    d.line((x0+r,y1-4,x1-r,y1-4),fill=(24,30,31,255),width=4)
    d.line((x1-4,y0+r,x1-4,y1-r),fill=(25,31,33,255),width=3)
    mx=max(8,w//8); my=max(7,h//7)
    d.rounded_rectangle((x0+mx,y0+my,x1-mx,y1-my),radius=max(3,r//2),fill=DARK2,outline=(20,25,26,255),width=2)
    d.rounded_rectangle((x0+mx+4,y0+my+4,x1-mx-4,y1-my-4),radius=max(2,r//3),fill=(55,63,65,255))
    if w>=75 and h>=34:
        if w>=h:
            yy=(y0+y1)//2; d.rectangle((x0+mx+7,yy-3,x1-mx-7,yy+3),fill=(15,21,22,255))
            d.line((x0+mx+9,yy-1,x1-mx-9,yy-1),fill=(100,110,110,255),width=1)
        else:
            xx=(x0+x1)//2; d.rectangle((xx-3,y0+my+7,xx+3,y1-my-7),fill=(15,21,22,255))
            d.line((xx-1,y0+my+9,xx-1,y1-my-9),fill=(100,110,110,255),width=1)
    if active:
        mask=Image.new('L',im.size,0); md=ImageDraw.Draw(mask)
        if w>=h:
            sx0=x0+w*.56; sx1=x0+w*.82; sy=(y0+y1)//2
            md.rounded_rectangle((int(sx0),sy-3,int(sx1),sy+3),radius=2,fill=255); add_glow(im,mask)
            d=ImageDraw.Draw(im); d.rounded_rectangle((int(sx0),sy-2,int(sx1),sy+2),radius=2,fill=CYAN)
            d.line((int(sx0)+2,sy-1,int(sx1)-2,sy-1),fill=CYAN2,width=1)
        else:
            sy0=y0+h*.56; sy1=y0+h*.82; sx=(x0+x1)//2
            md.rounded_rectangle((sx-3,int(sy0),sx+3,int(sy1)),radius=2,fill=255); add_glow(im,mask)
            d=ImageDraw.Draw(im); d.rounded_rectangle((sx-2,int(sy0),sx+2,int(sy1)),radius=2,fill=CYAN)
    d=ImageDraw.Draw(im)
    for px,py in [(x0+9,y0+9),(x1-9,y1-9)]: d.ellipse((px-2,py-2,px+2,py+2),fill=(12,17,18,255),outline=(140,150,150,255))
    if broken:
        cut=[(x1-22,y0),(x1,y0),(x1,y0+26),(x1-8,y0+22),(x1-13,y0+15),(x1-20,y0+17)]
        d.polygon(cut,fill=(0,0,0,0)); d.line([(x1-22,y0+2),(x1-20,y0+17),(x1-13,y0+15),(x1-8,y0+22),(x1-2,y0+26)],fill=(22,27,28,255),width=3)
    return im

def place(canvas,tile,cx,cy,angle=0,scale=1):
    if scale!=1: tile=tile.resize((max(1,round(tile.width*scale)),max(1,round(tile.height*scale))),Image.Resampling.LANCZOS)
    if angle: tile=tile.rotate(angle,resample=Image.Resampling.BICUBIC,expand=True)
    canvas.alpha_composite(tile,(round(cx-tile.width/2),round(cy-tile.height/2)))

def finish(canvas,path):
    box=canvas.getchannel('A').getbbox()
    if not box: raise RuntimeError('empty alpha')
    crop=canvas.crop(box); scale=min(456/crop.width,456/crop.height,1)
    if scale<1: crop=crop.resize((round(crop.width*scale),round(crop.height*scale)),Image.Resampling.LANCZOS)
    out=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0)); out.alpha_composite(crop,((SIZE-crop.width)//2,(SIZE-crop.height)//2))
    px=out.load()
    for y in range(SIZE):
        for x in range(SIZE):
            if px[x,y][3]==0: px[x,y]=(0,0,0,0)
    aa=out.getchannel('A'); edges=[aa.crop((0,0,SIZE,1)).getextrema()[1],aa.crop((0,SIZE-1,SIZE,SIZE)).getextrema()[1],aa.crop((0,0,1,SIZE)).getextrema()[1],aa.crop((SIZE-1,0,SIZE,SIZE)).getextrema()[1]]
    if any(edges): raise RuntimeError(f'{path}: edge alpha {edges}')
    out.save(path,'PNG',optimize=True)
    chk=Image.open(path); chk.load()
    if chk.mode!='RGBA' or chk.size!=(512,512): raise RuntimeError(f'{path}: expected 512x512 RGBA')

def matter():
    c=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0))
    specs=[(210,173,92,43,-18,.94),(292,168,105,44,17,.92),(350,205,78,40,-10,.9),(151,220,102,46,25,.9),(249,213,112,47,-6,1),(322,245,96,44,22,.94),(185,277,90,45,-18,.96),(267,276,118,48,14,1.03),(365,292,78,42,-24,.9),(126,311,86,42,12,.88),(218,334,104,44,-10,.95),(306,342,108,47,20,.98),(387,345,72,38,-5,.86),(170,375,76,39,29,.86),(270,390,84,41,-26,.88)]
    for i,(x,y,w,h,a,s) in enumerate(specs): place(c,module(w,h,100+i,i%3!=1),x,y,a,s)
    for i,(x,y,a) in enumerate([(113,260,18),(395,248,-8),(115,355,-22),(390,383,24),(235,137,7)]): place(c,module(44,32,300+i,i%2==0),x,y,a,.82)
    return c

def core_fragment():
    c=Image.new('RGBA',(SIZE,SIZE),(0,0,0,0)); glow=Image.new('L',(SIZE,SIZE),0); gd=ImageDraw.Draw(glow)
    gd.ellipse((213,213,299,299),fill=180); add_glow(c,glow,15,110); d=ImageDraw.Draw(c)
    d.ellipse((212,212,300,300),fill=EDGE,outline=HI,width=3); d.ellipse((222,222,290,290),fill=(25,33,34,255),outline=(88,103,104,255),width=4)
    d.ellipse((239,239,273,273),fill=(17,25,26,255),outline=CYAN,width=4); d.ellipse((248,248,264,264),fill=CYAN2)
    for ang in [0,90,180,270]:
        if ang==0: place(c,module(82,24,400,True,True),330,256,0,.95)
        else:
            r=math.radians(ang); place(c,module(106,25,400+ang,True),256+82*math.cos(r),256+82*math.sin(r),ang,.94)
    specs=[(190,164,92,52,-3,1,False),(278,153,86,48,8,.98,False),(350,185,70,42,18,.9,True),(153,225,82,48,-4,.95,False),(360,235,66,40,-15,.88,True),(150,310,86,48,7,.96,False),(362,313,66,41,22,.87,True),(200,365,96,50,-8,.98,False),(292,372,91,49,7,.96,False),(347,355,60,38,-22,.82,True),(190,255,62,38,90,.88,False),(319,286,64,39,90,.88,False)]
    for i,(x,y,w,h,a,s,br) in enumerate(specs): place(c,module(w,h,500+i,True,br),x,y,a,s)
    d=ImageDraw.Draw(c)
    for y in [215,240,286,327]:
        d.line((329,y,385,y-12),fill=(24,30,31,230),width=5); d.line((331,y+2,380,y-9),fill=(102,113,113,210),width=1)
    return c

finish(matter(),OUT/'WNG_ReplicatorMatter.png')
finish(core_fragment(),OUT/'WNG_ReplicatorCoreFragment.png')
print('Generated Replicator blocks and fractured command-lattice core fragment.')
