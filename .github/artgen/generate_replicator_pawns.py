from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
from pathlib import Path
import math, random

# Professional SG-1 block-form Replicator renderer.
# Six articulated legs (three pairs), built from individual machine blocks.
# Oversampled for crisp RimWorld-scale sprites while retaining worn metallic detail.
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Textures/Things/Pawn/Replicator'
OUT.mkdir(parents=True, exist_ok=True)
HI = 1024
OUTSIZE = 512
GLOW=(184,224,248,255)
EDGE_DARK=(13,15,17,245)


def metal_block(w,h,seed=0,corner=10,damage=1.0,light=1.0):
    w=max(8,int(w)); h=max(8,int(h))
    im=Image.new('RGBA',(w,h),(0,0,0,0))
    m=Image.new('L',(w,h),0)
    md=ImageDraw.Draw(m)
    c=max(3,int(corner))
    md.rounded_rectangle((2,2,w-3,h-3),radius=c,fill=255)
    pix=Image.new('RGBA',(w,h),(0,0,0,0)); pp=pix.load()
    rnd=random.Random(seed)
    for y in range(h):
        ny=abs((y/(max(1,h-1)))-.5)*2
        for x in range(w):
            nx=abs((x/(max(1,w-1)))-.5)*2
            edge=max(nx,ny)
            base=int((82-36*edge)*light)
            streak=8*math.sin((x*.07)+(seed%11))+4*math.sin((y*.13)+(seed%7))
            grain=rnd.randint(-7,7)
            v=max(18,min(150,int(base+streak+grain)))
            pp[x,y]=(v,min(160,v+3),min(165,v+5),255)
    pix.putalpha(m)
    im.alpha_composite(pix)
    d=ImageDraw.Draw(im)
    d.rounded_rectangle((2,2,w-3,h-3),radius=c,outline=EDGE_DARK,width=max(3,min(w,h)//18))
    d.line((c+3,5,w-c-5,5),fill=(205,209,211,180),width=max(2,min(w,h)//28))
    d.line((5,c+3,5,h-c-5),fill=(144,150,154,120),width=max(2,min(w,h)//32))
    d.line((c+4,h-6,w-c-6,h-6),fill=(5,7,8,180),width=max(2,min(w,h)//24))
    rnd=random.Random(seed*131+17)
    for _ in range(max(2,int((w+h)/70*damage))):
        x=rnd.randint(8,max(8,w-9)); y=rnd.randint(7,max(7,h-8)); ln=rnd.randint(8,max(9,min(34,w//2)))
        if rnd.random()<.5:
            d.line((x,y,min(w-7,x+ln),y+rnd.randint(-2,2)),fill=(205,207,206,rnd.randint(45,95)),width=1)
        else:
            d.line((x,y,x+rnd.randint(-2,2),min(h-7,y+ln)),fill=(6,8,9,rnd.randint(55,110)),width=1)
    if w>38 and h>28:
        pos=max(10,int(h*.63))
        d.line((9,pos,w-10,pos),fill=(16,18,20,150),width=2)
        d.line((10,pos+2,w-11,pos+2),fill=(120,124,126,55),width=1)
    return im


def joint(radius,seed=0):
    s=radius*2+12
    im=Image.new('RGBA',(s,s),(0,0,0,0)); d=ImageDraw.Draw(im)
    d.ellipse((5,5,s-6,s-6),fill=(13,15,17,255),outline=(132,136,139,230),width=4)
    d.ellipse((10,10,s-11,s-11),fill=(44,48,51,255),outline=(8,9,10,255),width=3)
    d.ellipse((radius*.55+6,radius*.55+6,s-radius*.55-7,s-radius*.55-7),fill=(27,30,32,255),outline=(170,174,176,150),width=2)
    return im


def glow_node(radius=13,color=GLOW):
    s=radius*6
    im=Image.new('RGBA',(s,s),(0,0,0,0)); cx=s//2
    mask=Image.new('L',(s,s),0); md=ImageDraw.Draw(mask)
    md.ellipse((cx-radius*2,cx-radius*2,cx+radius*2,cx+radius*2),fill=170)
    blur=mask.filter(ImageFilter.GaussianBlur(radius*1.25))
    halo=Image.new('RGBA',(s,s),(*color[:3],0)); halo.putalpha(blur)
    im.alpha_composite(halo)
    d=ImageDraw.Draw(im)
    d.ellipse((cx-radius-4,cx-radius-4,cx+radius+4,cx+radius+4),fill=(14,17,19,255),outline=(180,186,190,255),width=3)
    d.ellipse((cx-radius,cx-radius,cx+radius,cx+radius),fill=color)
    d.ellipse((cx-radius//2,cx-radius//2,cx+radius//2,cx+radius//2),fill=(239,249,255,250))
    return im


def paste_rot(canvas,comp,center,angle,shadow=True):
    obj=comp.rotate(angle,Image.Resampling.BICUBIC,expand=True)
    x=int(center[0]-obj.width/2); y=int(center[1]-obj.height/2)
    if shadow:
        a=obj.getchannel('A')
        sh=Image.new('RGBA',obj.size,(0,0,0,0)); sh.putalpha(a.filter(ImageFilter.GaussianBlur(7)).point(lambda p:int(p*.42)))
        canvas.alpha_composite(sh,(x+8,y+10))
    canvas.alpha_composite(obj,(x,y))


def limb(canvas,hip,knee,foot,thick,seed,blade=False,tool=False):
    def seg(a,b,width,s):
        dx=b[0]-a[0]; dy=b[1]-a[1]
        length=math.hypot(dx,dy); angle=math.degrees(math.atan2(dy,dx))+90
        block=metal_block(width,length,seed=s,corner=max(6,width//7),damage=1.15)
        paste_rot(canvas,block,((a[0]+b[0])/2,(a[1]+b[1])/2),angle)
    seg(knee,foot,thick,seed+2)
    seg(hip,knee,int(thick*1.05),seed+1)
    j=joint(max(9,int(thick*.28)),seed)
    paste_rot(canvas,j,knee,0,False); paste_rot(canvas,j,hip,0,False)
    if blade:
        f=metal_block(int(thick*.72),int(thick*2.0),seed+8,corner=5,damage=1.25,light=.9)
    else:
        f=metal_block(int(thick*.95),int(thick*1.35),seed+8,corner=6,damage=1.2,light=.86)
    paste_rot(canvas,f,foot,math.degrees(math.atan2(foot[1]-knee[1],foot[0]-knee[0]))+90)
    if tool:
        paste_rot(canvas,glow_node(max(5,int(thick*.13))),foot,0,False)


def body_block(canvas,center,size,angle,seed,light=1.0,damage=1.0):
    paste_rot(canvas,metal_block(size[0],size[1],seed=seed,corner=max(8,int(min(size)//9)),damage=damage,light=light),center,angle)


def render(role):
    cfg={
      'Drone':dict(scale=1.00,leg=72,reach=1.00,width=.86),
      'Hunter':dict(scale=.91,leg=58,reach=1.16,width=.72),
      'Bulwark':dict(scale=1.08,leg=82,reach=.93,width=1.03),
      'Titan':dict(scale=1.15,leg=92,reach=1.00,width=1.09),
      'SiegeMass':dict(scale=1.20,leg=98,reach=.92,width=1.17),
      'Controller':dict(scale=1.01,leg=72,reach=.98,width=.88),
      'Repairer':dict(scale=.94,leg=64,reach=1.05,width=.82),
      'Burrower':dict(scale=.96,leg=69,reach=1.02,width=.84),
      'Artillery':dict(scale=1.05,leg=78,reach=.98,width=.94),
    }[role]
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); cx,cy=512,500
    scale=cfg['scale']; reach=cfg['reach']; thick=cfg['leg']; rseed=sum((i+1)*ord(ch) for i,ch in enumerate(role))
    pairs=[(-145,-125,-300,-225,-405,-300),(-168,0,-330,20,-442,82),(-140,130,-292,230,-390,330)]
    if role=='Hunter': pairs=[(-130,-140,-310,-255,-438,-340),(-160,0,-350,28,-470,80),(-128,132,-300,248,-410,355)]
    if role in ('Bulwark','Titan','SiegeMass'): pairs=[(-164,-130,-315,-225,-410,-292),(-185,0,-350,15,-445,78),(-160,135,-310,235,-405,330)]
    for i,p in enumerate(pairs):
        hx,hy,kx,ky,fx,fy=p
        for side in (-1,1):
            hip=(cx+side*abs(hx)*scale,cy+hy*scale)
            knee=(cx+side*abs(kx)*scale*reach,cy+ky*scale)
            foot=(cx+side*abs(fx)*scale*reach,cy+fy*scale)
            limb(c,hip,knee,foot,int(thick*scale),100+i*20+(1 if side>0 else 0)+(rseed&255),blade=(role=='Hunter'),tool=(role=='Repairer' and i==0))
    bw=200*cfg['width']*scale; bh=390*scale
    body_block(c,(cx,cy+20*scale),(bw,bh),0,301+rseed%200,light=.82)
    body_block(c,(cx,cy-145*scale),(bw*.70,bh*.42),0,302+rseed%200,light=.88)
    for side in (-1,1):
        body_block(c,(cx+side*bw*.53,cy+30*scale),(bw*.34,bh*.58),side*3,310+(side+1)*7+rseed%100,light=.80)
        body_block(c,(cx+side*bw*.43,cy-170*scale),(bw*.26,bh*.26),side*6,320+(side+1)*11+rseed%100,light=.86)
    for j,(yy,ww,hh) in enumerate([(-270,92,110),(-215,105,95),(-155,115,86),(-92,120,72)]):
        body_block(c,(cx,cy+yy*scale),(ww*scale,hh*scale),0,350+j+rseed%90,light=.93)
    if role=='Hunter':
        for side in (-1,1): body_block(c,(cx+side*70,cy-320),(42,190),side*12,510+(side+1),light=.95,damage=1.2)
    elif role=='Bulwark':
        for side in (-1,1): body_block(c,(cx+side*145,cy+15),(105,325),side*2,520+(side+1),light=.69,damage=1.4)
    elif role=='Titan':
        body_block(c,(cx,cy+35),(270,280),0,530,light=.68,damage=1.45)
        for side in (-1,1): body_block(c,(cx+side*165,cy+30),(110,340),side*2,532+(side+1),light=.65,damage=1.5)
    elif role=='SiegeMass':
        body_block(c,(cx,cy+65),(330,320),0,540,light=.60,damage=1.65)
        body_block(c,(cx,cy-330),(125,260),0,541,light=.74,damage=1.6)
        for side in (-1,1): body_block(c,(cx+side*92,cy-330),(65,220),side*8,542+(side+1),light=.70,damage=1.5)
    elif role=='Controller':
        body_block(c,(cx,cy-310),(140,210),0,550,light=.98,damage=.8)
        paste_rot(c,glow_node(18),(cx,cy-70),0,False)
        for side in (-1,1): paste_rot(c,glow_node(9),(cx+side*118,cy-125),0,False)
    elif role=='Repairer':
        for side in (-1,1):
            body_block(c,(cx+side*118,cy-155),(58,180),side*5,560+(side+1),light=.92,damage=.9)
            paste_rot(c,glow_node(8),(cx+side*118,cy-250),0,False)
        paste_rot(c,glow_node(11),(cx,cy-50),0,False)
    elif role=='Burrower':
        for j,(ww,hh,yy) in enumerate([(120,130,-300),(90,105,-395),(60,90,-475),(35,70,-545)]): body_block(c,(cx,cy+yy),(ww,hh),0,570+j,light=.86+.025*j,damage=1.2)
    elif role=='Artillery':
        for side in (-1,1):
            body_block(c,(cx+side*78,cy-300),(48,310),side*1.5,580+(side+1),light=.83,damage=1.15)
            paste_rot(c,glow_node(7),(cx+side*78,cy-470),0,False)
    if role not in ('Controller','Repairer'): paste_rot(c,glow_node(8),(cx,cy-120*scale),0,False)
    bbox=c.getchannel('A').getbbox(); c=c.crop(bbox)
    sc=min(452/c.width,452/c.height)
    c=c.resize((max(1,int(c.width*sc)),max(1,int(c.height*sc))),Image.Resampling.LANCZOS)
    out=Image.new('RGBA',(OUTSIZE,OUTSIZE),(0,0,0,0))
    a=c.getchannel('A'); sh=a.filter(ImageFilter.GaussianBlur(5)).point(lambda p:int(p*.16))
    shadow=Image.new('RGBA',c.size,(0,0,0,0)); shadow.putalpha(sh)
    x=(OUTSIZE-c.width)//2; y=(OUTSIZE-c.height)//2
    out.alpha_composite(shadow,(x+3,y+5)); out.alpha_composite(c,(x,y))
    return out

roles=['Drone','Hunter','Bulwark','Titan','SiegeMass','Controller','Repairer','Burrower','Artillery']
for role in roles:
    north=render(role)
    views={'north':north,'east':north.transpose(Image.Transpose.ROTATE_270),'south':north.transpose(Image.Transpose.ROTATE_180),'west':north.transpose(Image.Transpose.ROTATE_90)}
    for direction,im in views.items(): im.save(OUT/f'WNG_Replicator{role}_{direction}.png','PNG',optimize=True)
    views['south'].save(OUT/f'WNG_Replicator{role}.png','PNG',optimize=True)

for role in roles:
    for suffix in ['', '_north', '_east', '_south', '_west']:
        p=OUT/f'WNG_Replicator{role}{suffix}.png'
        with Image.open(p) as chk:
            chk.load()
            if chk.mode!='RGBA' or chk.size!=(512,512): raise RuntimeError(f'{p}: expected 512x512 RGBA')
            alpha=chk.getchannel('A')
            if not alpha.getbbox(): raise RuntimeError(f'{p}: empty alpha')
            edges=[alpha.crop((0,0,512,1)).getextrema()[1],alpha.crop((0,511,512,512)).getextrema()[1],alpha.crop((0,0,1,512)).getextrema()[1],alpha.crop((511,0,512,512)).getextrema()[1]]
            if any(edges): raise RuntimeError(f'{p}: alpha touches edge {edges}')
print(f'Generated {len(roles)} professional six-legged Replicator families ({len(roles)*5} PNGs).')
