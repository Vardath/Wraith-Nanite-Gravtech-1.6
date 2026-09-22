from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import math, random

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Textures'/'Things'/'Pawn'/'Replicator'; OUT.mkdir(parents=True,exist_ok=True)
S=1024
FINAL=512
ROLES=['Drone','Hunter','Bulwark','Titan','SiegeMass','Controller','Repairer','Burrower','Artillery']
DIRANG={'south':0,'west':90,'north':180,'east':270}
# restrained steel palette, closer to Stargate block Replicators than neon mech art
EDGE=(20,24,25,255); DEEP=(34,39,40,255); STEEL=(92,99,99,255); MID=(110,118,117,255); LIGHT=(172,181,178,255)
BRIGHT=(215,220,216,255); ACCENT=(91,188,184,255); ACCENT_HI=(151,232,222,255); AMBER=(191,135,70,255)

RNG=random.Random(137)

def ptrot(p,ang,c=(S/2,S/2)):
    x,y=p; cx,cy=c; r=math.radians(ang); dx=x-cx; dy=y-cy
    return (cx+dx*math.cos(r)-dy*math.sin(r), cy+dx*math.sin(r)+dy*math.cos(r))

def rounded_mask(size,r):
    m=Image.new('L',size,0); d=ImageDraw.Draw(m); d.rounded_rectangle((0,0,size[0]-1,size[1]-1),radius=r,fill=255); return m

def metal_block(canvas, center, size, angle, tone=0, accent=False, inset=True, rivets=True, glow=False):
    w,h=max(10,int(size[0])),max(10,int(size[1])); pad=18
    tile=Image.new('RGBA',(w+pad*2,h+pad*2),(0,0,0,0));
    sh=Image.new('RGBA',tile.size,(0,0,0,0)); sd=ImageDraw.Draw(sh); r=max(5,min(w,h)//8)
    sd.rounded_rectangle((pad+5,pad+7,pad+w+5,pad+h+7),radius=r,fill=(0,0,0,105)); sh=sh.filter(ImageFilter.GaussianBlur(5)); tile.alpha_composite(sh)
    d=ImageDraw.Draw(tile)
    base_choices=[(75,82,83,255),(90,97,97,255),(105,111,110,255)]
    base=base_choices[max(-1,min(1,tone))+1]
    d.rounded_rectangle((pad,pad,pad+w,pad+h),radius=r,fill=EDGE)
    d.rounded_rectangle((pad+4,pad+4,pad+w-4,pad+h-4),radius=max(3,r-3),fill=base)
    for i in range(0,max(1,h-10),4):
        t=i/max(1,h-10)
        v=int(18*(0.5-t))
        col=tuple(max(0,min(255,c+v)) for c in base[:3])+(255,)
        y=pad+5+i
        d.line((pad+8,y,pad+w-8,y),fill=col,width=4)
    d.line((pad+r,pad+5,pad+w-r,pad+5),fill=(190,199,196,220),width=3)
    d.line((pad+5,pad+r,pad+5,pad+h-r),fill=(154,164,163,180),width=2)
    d.line((pad+r,pad+h-5,pad+w-r,pad+h-5),fill=(18,22,23,230),width=4)
    d.line((pad+w-5,pad+r,pad+w-5,pad+h-r),fill=(24,29,30,220),width=3)
    if inset and w>35 and h>24:
        ix0=pad+int(w*.18); ix1=pad+int(w*.82); iy0=pad+int(h*.30); iy1=pad+int(h*.70)
        d.rounded_rectangle((ix0,iy0,ix1,iy1),radius=max(2,r//3),fill=(42,48,49,255),outline=(128,137,136,220),width=2)
        d.line((ix0+4,iy0+3,ix1-4,iy0+3),fill=(170,178,175,150),width=2)
        if accent:
            cy=(iy0+iy1)//2
            if glow:
                gm=Image.new('L',tile.size,0); gd=ImageDraw.Draw(gm); gd.rounded_rectangle((ix0+8,cy-4,ix1-8,cy+4),radius=3,fill=210); gm=gm.filter(ImageFilter.GaussianBlur(10))
                gl=Image.new('RGBA',tile.size,(*ACCENT[:3],0)); gl.putalpha(gm.point(lambda x:int(x*.38))); tile.alpha_composite(gl); d=ImageDraw.Draw(tile)
            d.rounded_rectangle((ix0+8,cy-3,ix1-8,cy+3),radius=2,fill=ACCENT_HI)
    if rivets and w>28 and h>20:
        rr=max(2,min(w,h)//16)
        for x,y in [(pad+10,pad+10),(pad+w-10,pad+h-10)]:
            d.ellipse((x-rr,y-rr,x+rr,y+rr),fill=(28,34,35,255),outline=(176,184,181,230),width=1)
    for _ in range(max(0,(w*h)//7000)):
        x=RNG.randint(pad+7,pad+w-7); y=RNG.randint(pad+7,pad+h-7)
        d.line((x,y,min(pad+w-7,x+RNG.randint(3,10)),y),fill=(206,211,206,70),width=1)
    rot=tile.rotate(angle,Image.Resampling.BICUBIC,expand=True)
    canvas.alpha_composite(rot,(round(center[0]-rot.width/2),round(center[1]-rot.height/2)))

def chain(canvas,a,b,width,blocks=3,accent_last=False,tone=0):
    ax,ay=a; bx,by=b
    dx,dy=bx-ax,by-ay; L=math.hypot(dx,dy)
    if L<1:return
    ang=math.degrees(math.atan2(dy,dx)); ux,uy=dx/L,dy/L
    gap=max(4,width*.11); seg=(L-gap*(blocks-1))/blocks
    for i in range(blocks):
        s=i*(seg+gap); cx=ax+ux*(s+seg/2); cy=ay+uy*(s+seg/2)
        metal_block(canvas,(cx,cy),(seg,width),ang,tone=tone,accent=(accent_last and i==blocks-1),glow=False)

def joint(canvas,p,r=13,accent=False):
    x,y=p
    lay=Image.new('RGBA',canvas.size,(0,0,0,0)); d=ImageDraw.Draw(lay)
    if accent:
        gm=Image.new('L',canvas.size,0); gd=ImageDraw.Draw(gm); gd.ellipse((x-r*1.8,y-r*1.8,x+r*1.8,y+r*1.8),fill=140); gm=gm.filter(ImageFilter.GaussianBlur(r)); gl=Image.new('RGBA',canvas.size,(*ACCENT[:3],0)); gl.putalpha(gm.point(lambda q:int(q*.28))); canvas.alpha_composite(gl)
    d.ellipse((x-r,y-r,x+r,y+r),fill=EDGE,outline=(178,185,182,255),width=3)
    d.ellipse((x-r*.58,y-r*.58,x+r*.58,y+r*.58),fill=ACCENT if accent else (62,69,69,255),outline=(205,211,207,230),width=2)
    canvas.alpha_composite(lay)

def leg(canvas,hip,knee,ankle,foot,width=30,blade=False,accent=False):
    chain(canvas,hip,knee,width,blocks=3,tone=0)
    joint(canvas,knee,width*.32)
    chain(canvas,knee,ankle,width*.88,blocks=3,tone=-1)
    joint(canvas,ankle,width*.27,accent=accent)
    chain(canvas,ankle,foot,width*.72,blocks=2,tone=-1)
    fx,fy=foot; vx,vy=foot[0]-ankle[0],foot[1]-ankle[1]; L=math.hypot(vx,vy) or 1; ux,uy=vx/L,vy/L
    tip=(fx+ux*(36 if blade else 24),fy+uy*(36 if blade else 24))
    chain(canvas,foot,tip,max(11,width*.42),blocks=1,tone=1)

def body_plate(canvas,center,size,angle=0,accent=False,tone=0):
    metal_block(canvas,center,size,angle,tone=tone,accent=accent,glow=accent)

def glow_core(canvas,center,r=28,amber=False):
    x,y=center; col=AMBER if amber else ACCENT
    gm=Image.new('L',canvas.size,0); gd=ImageDraw.Draw(gm); gd.ellipse((x-r*1.7,y-r*1.7,x+r*1.7,y+r*1.7),fill=170); gm=gm.filter(ImageFilter.GaussianBlur(r*.9)); gl=Image.new('RGBA',canvas.size,(*col[:3],0)); gl.putalpha(gm.point(lambda q:int(q*.30))); canvas.alpha_composite(gl)
    d=ImageDraw.Draw(canvas); d.ellipse((x-r,y-r,x+r,y+r),fill=EDGE,outline=(180,187,184,255),width=4); d.ellipse((x-r*.66,y-r*.66,x+r*.66,y+r*.66),fill=(37,45,46,255),outline=col,width=5); d.ellipse((x-r*.25,y-r*.25,x+r*.25,y+r*.25),fill=(*col[:3],235))

def cfg(role):
    return {
        'Drone':dict(body=(170,205),y=520,legw=29,span=300,fore=205,rear=185,scale=0.88),
        'Hunter':dict(body=(164,235),y=515,legw=27,span=355,fore=245,rear=230,scale=0.97),
        'Bulwark':dict(body=(235,225),y=520,legw=40,span=320,fore=210,rear=205,scale=1.05),
        'Titan':dict(body=(270,270),y=520,legw=48,span=370,fore=250,rear=245,scale=1.12),
        'SiegeMass':dict(body=(330,285),y=525,legw=56,span=405,fore=260,rear=250,scale=1.18),
        'Controller':dict(body=(215,240),y=515,legw=34,span=320,fore=220,rear=205,scale=1.00),
        'Repairer':dict(body=(185,220),y=520,legw=29,span=315,fore=215,rear=210,scale=0.96),
        'Burrower':dict(body=(200,245),y=520,legw=32,span=325,fore=210,rear=205,scale=0.98),
        'Artillery':dict(body=(230,255),y=530,legw=38,span=355,fore=230,rear=230,scale=1.06),
    }[role]

def six_legs(canvas,role):
    c=cfg(role); cx=512; cy=c['y']; span=c['span']; w=c['legw']
    pairs=[
        ((cx-70,cy-72),(cx-span*.52,cy-155),(cx-span*.69,cy-260),(cx-span*.82,cy-315)),
        ((cx-92,cy+5),(cx-span*.63,cy-8),(cx-span*.78,cy+28),(cx-span*.91,cy+42)),
        ((cx-70,cy+75),(cx-span*.52,cy+150),(cx-span*.67,cy+235),(cx-span*.78,cy+295)),
    ]
    right=[]
    for pts in pairs:
        right.append(tuple((1024-x,y) for x,y in pts))
    alllegs=[]
    for idx,pts in enumerate(pairs+right):
        side_idx=idx%3
        alllegs.append((sum(p[1] for p in pts)/4, side_idx, pts))
    alllegs.sort()
    for _,pair_idx,pts in alllegs:
        blade=(role=='Hunter' and pair_idx==0) or (role in ('Titan','SiegeMass') and pair_idx==0)
        acc=(role=='Repairer' and pair_idx==0)
        leg(canvas,*pts,width=w,blade=blade,accent=acc)

def body(canvas,role):
    c=cfg(role); cx=512; cy=c['y']; bw,bh=c['body']
    body_plate(canvas,(cx,cy+28),(bw,bh),90,accent=False,tone=-1)
    body_plate(canvas,(cx,cy-40),(bw*.80,bh*.44),0,accent=(role in ('Controller','Artillery')),tone=0)
    body_plate(canvas,(cx-0.30*bw,cy+15),(bh*.48,bw*.22),90,tone=1)
    body_plate(canvas,(cx+0.30*bw,cy+15),(bh*.48,bw*.22),90,tone=1)
    for yy,scale in [(cy-75,.58),(cy-8,.70),(cy+60,.58)]:
        body_plate(canvas,(cx,yy),(bw*scale,30),0,accent=(role=='Controller' and yy==cy-8),tone=0)
    if role=='Drone':
        for s in (-1,1): chain(canvas,(cx+s*45,cy-105),(cx+s*72,cy-178),22,blocks=2,tone=-1)
    elif role=='Hunter':
        for s in (-1,1): chain(canvas,(cx+s*43,cy-115),(cx+s*67,cy-205),25,blocks=3,tone=-1)
    elif role=='Bulwark':
        body_plate(canvas,(cx,cy-118),(bw*.74,52),0,tone=1)
        body_plate(canvas,(cx,cy+125),(bw*.68,46),0,tone=-1)
    elif role=='Titan':
        body_plate(canvas,(cx,cy-150),(bw*.72,58),0,accent=True,tone=1)
        for s in (-1,1): body_plate(canvas,(cx+s*108,cy-35),(110,48),90,tone=-1)
    elif role=='SiegeMass':
        for yy in (cy-150,cy-105): body_plate(canvas,(cx,yy),(bw*.76,58),0,tone=1)
        body_plate(canvas,(cx,cy+155),(bw*.82,58),0,tone=-1)
    elif role=='Controller':
        glow_core(canvas,(cx,cy-95),34)
        for s in (-1,1): chain(canvas,(cx+s*55,cy-105),(cx+s*95,cy-178),16,blocks=3,accent_last=True,tone=-1)
    elif role=='Repairer':
        glow_core(canvas,(cx,cy-72),24)
        for s in (-1,1): body_plate(canvas,(cx+s*78,cy-78),(58,30),0,accent=True,tone=-1)
    elif role=='Burrower':
        for i,(ww,hh) in enumerate([(120,46),(92,40),(64,34),(38,26)]): body_plate(canvas,(cx,cy-135-i*40),(ww,hh),0,accent=(i==3),tone=1 if i<2 else -1)
    elif role=='Artillery':
        glow_core(canvas,(cx,cy-72),22,amber=True)
        for i,(ww,hh) in enumerate([(92,50),(76,44),(62,38),(50,32),(42,28)]): body_plate(canvas,(cx,cy-145-i*43),(ww,hh),0,accent=(i==4),tone=1 if i<2 else -1)
        for s in (-1,1): body_plate(canvas,(cx+s*112,cy+18),(90,44),90,tone=-1)

def render(role):
    canvas=Image.new('RGBA',(S,S),(0,0,0,0))
    six_legs(canvas,role)
    body(canvas,role)
    box=canvas.getchannel('A').getbbox()
    crop=canvas.crop(box)
    target={'Drone':700,'Hunter':760,'Bulwark':760,'Titan':800,'SiegeMass':830,'Controller':750,'Repairer':735,'Burrower':750,'Artillery':800}[role]
    sc=min(target/crop.width,target/crop.height)
    crop=crop.resize((max(1,round(crop.width*sc)),max(1,round(crop.height*sc))),Image.Resampling.LANCZOS)
    hi=Image.new('RGBA',(S,S),(0,0,0,0)); hi.alpha_composite(crop,((S-crop.width)//2,(S-crop.height)//2))
    final=hi.resize((FINAL,FINAL),Image.Resampling.LANCZOS)
    px=final.load()
    for y in range(FINAL):
        for x in range(FINAL):
            if px[x,y][3]==0: px[x,y]=(0,0,0,0)
    return final

for role in ROLES:
    south=render(role)
    south.save(OUT/f'WNG_Replicator{role}.png','PNG',optimize=True)
    south.save(OUT/f'WNG_Replicator{role}_south.png','PNG',optimize=True)
    south.rotate(180,Image.Resampling.BICUBIC).save(OUT/f'WNG_Replicator{role}_north.png','PNG',optimize=True)
    south.rotate(90,Image.Resampling.BICUBIC).save(OUT/f'WNG_Replicator{role}_west.png','PNG',optimize=True)
    south.rotate(270,Image.Resampling.BICUBIC).save(OUT/f'WNG_Replicator{role}_east.png','PNG',optimize=True)
print('Generated',len(ROLES)*5,'professional six-legged Replicator sprites')

for role in ROLES:
    for suffix in ['', '_north', '_east', '_south', '_west']:
        p=OUT/f'WNG_Replicator{role}{suffix}.png'
        with Image.open(p) as im:
            im.load()
            if im.mode!='RGBA' or im.size!=(512,512): raise RuntimeError(f'{p}: bad mode/size')
            if not im.getchannel('A').getbbox(): raise RuntimeError(f'{p}: empty alpha')
print('Validated',len(ROLES)*5,'Replicator sprites')
