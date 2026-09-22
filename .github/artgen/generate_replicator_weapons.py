from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math

ROOT=Path(__file__).resolve().parents[2]
WEAP=ROOT/'Textures/Things/Item/Weapon/Replicator'; WEAP.mkdir(parents=True,exist_ok=True)
PROJ=ROOT/'Textures/Things/Projectile'; PROJ.mkdir(parents=True,exist_ok=True)
S=512
MET=(88,99,100,255); MET2=(128,140,141,255); DARK=(24,31,33,255); EDGE=(10,15,16,255)
HI=(178,190,188,255); CY=(52,222,199,255); CY2=(124,255,234,255); PUR=(154,92,245,255); PUR2=(220,182,255,255)

def glow(base,box,color,blur=10,a=160,ellipse=False):
    m=Image.new('L',base.size,0); d=ImageDraw.Draw(m)
    if ellipse: d.ellipse(box,fill=255)
    else: d.rounded_rectangle(box,radius=max(2,int((box[3]-box[1])/2)),fill=255)
    mm=m.filter(ImageFilter.GaussianBlur(blur)); layer=Image.new('RGBA',base.size,(*color,a)); layer.putalpha(mm.point(lambda x:int(x*a/255))); base.alpha_composite(layer)

def bevel_rect(d,box,r=9,body=MET):
    x0,y0,x1,y1=map(int,box); d.rounded_rectangle((x0,y0,x1,y1),radius=r,fill=EDGE)
    d.rounded_rectangle((x0+4,y0+4,x1-4,y1-4),radius=max(3,r-3),fill=body)
    d.line((x0+r,y0+5,x1-r,y0+5),fill=HI,width=3); d.line((x0+5,y0+r,x0+5,y1-r),fill=(145,158,158,255),width=2)
    d.line((x0+r,y1-5,x1-r,y1-5),fill=(19,25,27,255),width=4); d.line((x1-5,y0+r,x1-5,y1-r),fill=(18,24,26,255),width=3)

def root(base,cx=116,cy=256):
    for off,ang in [((-15,-56),-18),((-23,53),18),((12,-29),-5),((8,28),5)]:
        x=cx+off[0]; y=cy+off[1]; tile=Image.new('RGBA',(92,36),(0,0,0,0)); td=ImageDraw.Draw(tile); bevel_rect(td,(4,4,88,32),6,DARK)
        tile=tile.rotate(ang,Image.Resampling.BICUBIC,expand=True); base.alpha_composite(tile,(int(x-tile.width/2),int(y-tile.height/2)))
    d=ImageDraw.Draw(base); bevel_rect(d,(72,211,156,301),13,MET); d.rounded_rectangle((88,228,140,284),radius=8,fill=DARK,outline=(110,124,124,255),width=3)
    d.rectangle((96,246,132,266),fill=(42,53,54,255)); glow(base,(101,251,127,261),(52,222,199),8,110); d=ImageDraw.Draw(base); d.rounded_rectangle((103,253,125,259),radius=3,fill=CY)

def pulse():
    c=Image.new('RGBA',(S,S),(0,0,0,0)); root(c,118,256); d=ImageDraw.Draw(c)
    bevel_rect(d,(137,213,280,299),15,MET); d.rounded_rectangle((158,231,260,281),radius=9,fill=DARK,outline=(112,125,125,255),width=3)
    bevel_rect(d,(245,226,364,286),11,MET2); d.polygon([(348,229),(402,244),(426,256),(402,268),(348,283)],fill=EDGE); d.polygon([(354,236),(398,247),(414,256),(398,265),(354,276)],fill=(56,67,69,255))
    glow(c,(397,244,430,268),(52,222,199),11,180); d=ImageDraw.Draw(c); d.ellipse((405,248,427,270),fill=EDGE,outline=HI,width=2); d.ellipse((411,253,423,265),fill=CY2)
    bevel_rect(d,(128,186,319,216),6,DARK); bevel_rect(d,(128,296,319,326),6,DARK)
    for y in [200,310]:
        glow(c,(235,y-3,294,y+3),(52,222,199),6,80); ImageDraw.Draw(c).rounded_rectangle((240,y-2,289,y+2),radius=2,fill=CY)
    return c

def artillery():
    c=Image.new('RGBA',(S,S),(0,0,0,0)); root(c,105,256); d=ImageDraw.Draw(c); bevel_rect(d,(129,207,260,305),15,MET)
    for y in [221,274]:
        bevel_rect(d,(222,y-20,399,y+20),8,MET2); d.rounded_rectangle((246,y-10,370,y+10),radius=4,fill=DARK)
        glow(c,(300,y-4,371,y+4),(52,222,199),8,105); ImageDraw.Draw(c).rounded_rectangle((306,y-3,367,y+3),radius=2,fill=CY)
        d=ImageDraw.Draw(c); d.polygon([(389,y-18),(435,y-10),(454,y),(435,y+10),(389,y+18)],fill=EDGE); d.polygon([(396,y-11),(430,y-6),(441,y),(430,y+6),(396,y+11)],fill=(55,66,68,255)); glow(c,(430,y-9,454,y+9),(52,222,199),10,150)
    bevel_rect(d,(181,240,384,272),6,DARK); d.rectangle((214,248,365,264),fill=(47,57,59,255)); return c

def disruptor():
    c=Image.new('RGBA',(S,S),(0,0,0,0)); root(c,120,256); d=ImageDraw.Draw(c); bevel_rect(d,(139,220,271,292),13,MET)
    for pts in [[(247,222),(365,188),(409,206),(303,248)],[(247,290),(365,324),(409,306),(303,264)]]:
        d.polygon(pts,fill=EDGE); inner=[(x+(3 if x<330 else -3),y+(3 if y<256 else -3)) for x,y in pts]; d.polygon(inner,fill=(78,89,91,255))
    glow(c,(326,207,432,313),(154,92,245),18,150,True); glow(c,(340,221,418,299),(52,222,199),11,110,True)
    d=ImageDraw.Draw(c); d.ellipse((331,211,427,307),fill=EDGE,outline=HI,width=3); d.ellipse((344,224,414,294),fill=(31,38,42,255),outline=PUR,width=7); d.ellipse((362,242,396,276),fill=(20,25,29,255),outline=CY,width=5); d.ellipse((373,253,385,265),fill=PUR2)
    for a in [-55,55]:
        r=77; x=379+math.cos(math.radians(a))*r; y=259+math.sin(math.radians(a))*r; glow(c,(x-9,y-9,x+9,y+9),(154,92,245),7,130,True); ImageDraw.Draw(c).ellipse((x-5,y-5,x+5,y+5),fill=PUR2)
    return c

def projectile():
    c=Image.new('RGBA',(S,S),(0,0,0,0)); glow(c,(157,157,355,355),(154,92,245),28,170,True); glow(c,(182,182,330,330),(52,222,199),18,140,True)
    d=ImageDraw.Draw(c); d.ellipse((173,173,339,339),fill=(20,24,30,210),outline=PUR2,width=12); d.ellipse((205,205,307,307),fill=(15,20,23,235),outline=CY2,width=9); d.ellipse((235,235,277,277),fill=PUR2); d.ellipse((245,245,267,267),fill=(244,235,255,255))
    for a in [0,90,180,270]:
        x=256+101*math.cos(math.radians(a)); y=256+101*math.sin(math.radians(a)); d.polygon([(x-12,y-7),(x+12,y),(x-12,y+7)],fill=MET2)
    return c

def finish(im,path,max_content=448):
    box=im.getchannel('A').getbbox(); crop=im.crop(box); sc=min(max_content/crop.width,max_content/crop.height,1)
    if sc<1: crop=crop.resize((round(crop.width*sc),round(crop.height*sc)),Image.Resampling.LANCZOS)
    out=Image.new('RGBA',(S,S),(0,0,0,0)); out.alpha_composite(crop,((S-crop.width)//2,(S-crop.height)//2)); px=out.load()
    for y in range(S):
        for x in range(S):
            if px[x,y][3]==0: px[x,y]=(0,0,0,0)
    a=out.getchannel('A'); edges=[a.crop((0,0,S,1)).getextrema()[1],a.crop((0,S-1,S,S)).getextrema()[1],a.crop((0,0,1,S)).getextrema()[1],a.crop((S-1,0,S,S)).getextrema()[1]]
    if any(edges): raise RuntimeError(f'{path}: edge alpha {edges}')
    out.save(path,'PNG',optimize=True); chk=Image.open(path); chk.load()
    if chk.mode!='RGBA' or chk.size!=(512,512): raise RuntimeError(f'{path}: expected 512x512 RGBA')

finish(pulse(),WEAP/'WNG_ReplicatorPulseCaster.png')
finish(artillery(),WEAP/'WNG_ReplicatorArtilleryCaster.png')
finish(disruptor(),WEAP/'WNG_ReplicatorShieldDisruptor.png')
finish(projectile(),PROJ/'WNG_ReplicatorShieldDisruptor.png',390)
print('Generated distinct integrated Replicator pulse, artillery and phase-disruption weapon art.')
