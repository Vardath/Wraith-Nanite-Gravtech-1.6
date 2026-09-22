from pathlib import Path
from PIL import Image,ImageDraw,ImageFilter
import math

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Textures/Things/Pawn/Replicator'; OUT.mkdir(parents=True,exist_ok=True)
S=512
MET=(84,94,96,255); DARK=(25,32,34,255); EDGE=(9,14,15,255); HI=(175,188,186,255)
CY=(50,219,198,255); CY2=(116,255,232,255)
DIRANG={'north':0,'east':90,'south':180,'west':270}
ROLES=['Drone','Hunter','Bulwark','Titan','SiegeMass','Controller','Repairer','Burrower','Artillery']

def rotpt(p,ang,c=(256,256)):
    x,y=p; cx,cy=c; r=math.radians(ang); dx=x-cx; dy=y-cy
    return (cx+dx*math.cos(r)-dy*math.sin(r),cy+dx*math.sin(r)+dy*math.cos(r))

def tile(w,h,cyan=False,dark=False):
    pad=10; im=Image.new('RGBA',(w+20,h+20),(0,0,0,0)); d=ImageDraw.Draw(im); r=max(3,min(w,h)//7)
    d.rounded_rectangle((pad,pad,pad+w,pad+h),radius=r,fill=EDGE)
    d.rounded_rectangle((pad+3,pad+3,pad+w-3,pad+h-3),radius=max(2,r-2),fill=DARK if dark else MET)
    d.line((pad+r,pad+4,pad+w-r,pad+4),fill=HI,width=2); d.line((pad+r,pad+h-4,pad+w-r,pad+h-4),fill=(20,26,28,255),width=3)
    d.rounded_rectangle((pad+w*.18,pad+h*.28,pad+w*.82,pad+h*.72),radius=max(2,r//2),fill=(42,51,53,255),outline=(104,116,117,255),width=2)
    if cyan:
        y=pad+h//2; x0=pad+int(w*.54); x1=pad+int(w*.79); m=Image.new('L',im.size,0); md=ImageDraw.Draw(m); md.rounded_rectangle((x0,y-3,x1,y+3),radius=2,fill=255)
        blur=m.filter(ImageFilter.GaussianBlur(5)); lay=Image.new('RGBA',im.size,(50,219,198,95)); lay.putalpha(blur.point(lambda x:int(x*.42))); im.alpha_composite(lay); d=ImageDraw.Draw(im); d.rounded_rectangle((x0,y-2,x1,y+2),radius=2,fill=CY)
    return im

def place(c,xy,wh,angle,cyan=False,dark=False):
    im=tile(int(wh[0]),int(wh[1]),cyan,dark).rotate(angle,Image.Resampling.BICUBIC,expand=True)
    c.alpha_composite(im,(round(xy[0]-im.width/2),round(xy[1]-im.height/2)))

def segment(c,a,b,width=24,cyan=False,dark=False):
    x=(a[0]+b[0])/2; y=(a[1]+b[1])/2; length=math.hypot(b[0]-a[0],b[1]-a[1]); ang=math.degrees(math.atan2(b[1]-a[1],b[0]-a[0])); place(c,(x,y),(length,width),ang,cyan,dark)

def limb(c,pts,width=19,cyan=False):
    d=ImageDraw.Draw(c); d.line(pts,fill=(16,22,24,255),width=max(6,width//2),joint='curve')
    for p in pts[1:-1]: d.ellipse((p[0]-5,p[1]-5,p[0]+5,p[1]+5),fill=(45,55,57,255),outline=(140,150,150,255),width=2)
    for i in range(len(pts)-1): segment(c,pts[i],pts[i+1],max(12,width-i*2),cyan and i==len(pts)-2,i%2==1)

def glow_ring(c,center,r,accent=CY):
    x,y=center; m=Image.new('L',c.size,0); md=ImageDraw.Draw(m); md.ellipse((x-r,y-r,x+r,y+r),fill=180)
    blur=m.filter(ImageFilter.GaussianBlur(9)); lay=Image.new('RGBA',c.size,(*accent[:3],105)); lay.putalpha(blur.point(lambda q:int(q*.42))); c.alpha_composite(lay)
    d=ImageDraw.Draw(c); d.ellipse((x-r,y-r,x+r,y+r),fill=EDGE,outline=HI,width=3); d.ellipse((x-r+8,y-r+8,x+r-8,y+r-8),fill=DARK,outline=accent,width=4)

def xform(points,ang): return [rotpt(p,ang) for p in points]

def draw_legs(c,ang,kind):
    cfg={
      'Drone':[(205,244,158,210),(307,244,354,210),(197,270,151,290),(315,270,361,290),(220,300,185,337),(292,300,327,337)],
      'Hunter':[(210,236,148,185),(302,236,364,185),(196,266,132,256),(316,266,380,256),(218,301,162,352),(294,301,350,352)],
      'Bulwark':[(196,244,151,214),(316,244,361,214),(188,276,139,286),(324,276,373,286),(210,314,172,350),(302,314,340,350)],
      'Titan':[(194,232,137,187),(318,232,375,187),(178,261,118,250),(334,261,394,250),(185,293,127,324),(327,293,385,324),(212,326,171,376),(300,326,341,376)],
      'SiegeMass':[(183,229,116,195),(329,229,396,195),(170,257,101,253),(342,257,411,253),(174,287,105,310),(338,287,407,310),(200,320,144,367),(312,320,368,367)],
      'Controller':[(202,235,153,199),(310,235,359,199),(190,268,139,268),(322,268,373,268),(211,306,174,351),(301,306,338,351)],
      'Repairer':[(207,240,157,208),(305,240,355,208),(196,272,147,286),(316,272,365,286),(217,307,180,347),(295,307,332,347)],
      'Burrower':[(204,247,155,221),(308,247,357,221),(194,278,145,298),(318,278,367,298),(219,311,181,346),(293,311,331,346)],
      'Artillery':[(202,242,148,209),(310,242,364,209),(190,278,137,290),(322,278,375,290),(213,315,173,355),(299,315,339,355)]}
    limbs=[]
    for sx,sy,ex,ey in cfg[kind]:
        knee=((sx+ex)/2,(sy+ey)/2+(8 if ey>sy else -8)); limbs.append(xform([(sx,sy),knee,(ex,ey)],ang))
    limbs.sort(key=lambda pts:sum(p[1] for p in pts)/len(pts))
    for pts in limbs: limb(c,pts,20 if kind not in ('Titan','SiegeMass') else 25)

def draw_body(c,ang,kind):
    dims={'Drone':(92,125),'Hunter':(82,148),'Bulwark':(150,150),'Titan':(170,184),'SiegeMass':(212,188),'Controller':(130,151),'Repairer':(116,145),'Burrower':(122,150),'Artillery':(145,165)}
    w,h=dims[kind]; place(c,rotpt((256,270),ang),(w,h),ang+90,True)
    for y,ww,hh in [(216,w*.72,32),(255,w*.86,36),(298,w*.76,34)]: place(c,rotpt((256,y),ang),(ww,hh),ang,y==255,y!=255)
    for x in [256-w*.42,256+w*.42]: place(c,rotpt((x,268),ang),(h*.46,26),ang+90,False,True)

def draw_front(c,ang,kind):
    if kind in ('Drone','Hunter','Titan'):
        spread={'Drone':34,'Hunter':43,'Titan':55}[kind]; length={'Drone':48,'Hunter':70,'Titan':82}[kind]
        for s in [-1,1]: limb(c,xform([(256+s*22,220),(256+s*spread,190),(256+s*(spread+16),220-length)],ang),18 if kind!='Titan' else 24,kind=='Hunter')
    elif kind=='Bulwark':
        for s in [-1,1]: limb(c,xform([(256+s*45,221),(256+s*70,190),(256+s*48,161)],ang),25)
    elif kind=='SiegeMass':
        for s in [-1,0,1]: segment(c,rotpt((256+s*45,215),ang),rotpt((256+s*24,147),ang),30,s==0)
    elif kind=='Controller':
        glow_ring(c,rotpt((256,205),ang),35,CY)
        for s in [-1,1]: segment(c,rotpt((256+s*32,217),ang),rotpt((256+s*66,177),ang),14,True,True)
    elif kind=='Repairer':
        for s in [-1,1]:
            limb(c,xform([(256+s*27,220),(256+s*52,181),(256+s*76,169)],ang),14,True); glow_ring(c,rotpt((256+s*79,166),ang),10,CY)
    elif kind=='Burrower':
        for s in [-1,0,1]: segment(c,rotpt((256+s*30,225),ang),rotpt((256+s*10,162),ang),22,s==0,s!=0)
        glow_ring(c,rotpt((256,145),ang),11,CY)
    elif kind=='Artillery':
        for s in [-1,1]:
            place(c,rotpt((256+s*30,205),ang),(118,24),ang+90,True); glow_ring(c,rotpt((256+s*30,146),ang),8,CY)

def render(kind,direction):
    ang=DIRANG[direction]; c=Image.new('RGBA',(S,S),(0,0,0,0)); draw_legs(c,ang,kind); draw_body(c,ang,kind); draw_front(c,ang,kind)
    box=c.getchannel('A').getbbox(); crop=c.crop(box); sc=420/max(crop.width,crop.height)
    if abs(sc-1)>.01: crop=crop.resize((round(crop.width*sc),round(crop.height*sc)),Image.Resampling.LANCZOS)
    out=Image.new('RGBA',(S,S),(0,0,0,0)); out.alpha_composite(crop,((S-crop.width)//2,(S-crop.height)//2)); px=out.load()
    for y in range(S):
        for x in range(S):
            if px[x,y][3]==0: px[x,y]=(0,0,0,0)
    a=out.getchannel('A'); edges=[a.crop((0,0,S,1)).getextrema()[1],a.crop((0,S-1,S,S)).getextrema()[1],a.crop((0,0,1,S)).getextrema()[1],a.crop((S-1,0,S,S)).getextrema()[1]]
    if any(edges): raise RuntimeError((kind,direction,edges))
    return out

for role in ROLES:
    for direction in ['north','east','south','west']:
        path=OUT/f'WNG_Replicator{role}_{direction}.png'; render(role,direction).save(path,'PNG',optimize=True); chk=Image.open(path); chk.load()
        if chk.mode!='RGBA' or chk.size!=(512,512): raise RuntimeError(path)
    render(role,'south').save(OUT/f'WNG_Replicator{role}.png','PNG',optimize=True)
print(f'Generated {len(ROLES)} professional block-form Replicator directional families.')
