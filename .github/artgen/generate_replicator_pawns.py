from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
from pathlib import Path
import math, random
ROOT=Path(__file__).resolve().parents[2]
SRC=Image.open(ROOT/'.github/artgen/assets/replicator_master.png').convert('RGBA').resize((1254,1254),Image.Resampling.LANCZOS)
OUT=ROOT/'Textures/Things/Pawn/Replicator'; OUT.mkdir(parents=True,exist_ok=True)
S=512

def trim(im):
    b=im.getchannel('A').getbbox(); return im.crop(b) if b else im

def extra_leg_pair(scale=.72, angle=18):
    ll=trim(SRC.crop((0,430,520,1254)))
    lr=trim(SRC.crop((730,430,1254,1254)))
    ll=ll.resize((int(ll.width*scale),int(ll.height*scale)),Image.Resampling.LANCZOS).rotate(-angle,Image.Resampling.BICUBIC,expand=True)
    lr=lr.resize((int(lr.width*scale),int(lr.height*scale)),Image.Resampling.LANCZOS).rotate(angle,Image.Resampling.BICUBIC,expand=True)
    return ll,lr

def base_high(role):
    cfg={
      'Drone':(.72,18,1.00,1.00),
      'Hunter':(.67,23,.92,1.07),
      'Bulwark':(.76,15,1.08,1.03),
      'Titan':(.80,14,1.13,1.08),
      'SiegeMass':(.83,12,1.18,1.02),
      'Controller':(.72,17,1.02,1.02),
      'Repairer':(.69,20,.98,1.03),
      'Burrower':(.70,19,1.00,1.07),
      'Artillery':(.76,16,1.07,1.05),
    }
    escale,ang,sx,sy=cfg[role]
    ll,lr=extra_leg_pair(escale,ang)
    can=Image.new('RGBA',(1500,1380),(0,0,0,0))
    can.alpha_composite(ll,(35,335)); can.alpha_composite(lr,(1500-lr.width-35,335))
    can.alpha_composite(SRC,(123,55))
    can=trim(can)
    if sx!=1 or sy!=1:
        can=can.resize((int(can.width*sx),int(can.height*sy)),Image.Resampling.LANCZOS)
    return can

def texture_block(w,h,seed=0,brightness=.9):
    rnd=random.Random(seed)
    atlas=[SRC.crop((420,150,830,600)),SRC.crop((220,520,600,1050)),SRC.crop((700,500,1070,1100)),SRC.crop((450,700,800,1180))]
    patch=rnd.choice(atlas).copy(); pw,ph=patch.size
    x0=rnd.randint(0,max(0,pw//5)); y0=rnd.randint(0,max(0,ph//5))
    patch=patch.crop((x0,y0,pw,ph)).resize((w,h),Image.Resampling.LANCZOS)
    patch=ImageEnhance.Brightness(patch).enhance(brightness)
    base=Image.new('RGBA',(w,h),(55,58,61,255))
    base.alpha_composite(patch)
    m=Image.new('L',(w,h),0); d=ImageDraw.Draw(m); r=max(4,min(w,h)//8); d.rounded_rectangle((2,2,w-3,h-3),radius=r,fill=255)
    base.putalpha(m)
    d=ImageDraw.Draw(base); d.rounded_rectangle((2,2,w-3,h-3),radius=r,outline=(205,208,210,240),width=max(2,min(w,h)//18)); d.line((8,8,w-8,8),fill=(230,233,235,150),width=2)
    return base

def add_glow(im,xy,r,color=(185,225,255)):
    x,y=xy
    m=Image.new('L',im.size,0); d=ImageDraw.Draw(m); d.ellipse((x-r,y-r,x+r,y+r),fill=240)
    blur=m.filter(ImageFilter.GaussianBlur(r*.85)); lay=Image.new('RGBA',im.size,(*color,0)); lay.putalpha(blur.point(lambda q:int(q*.38))); im.alpha_composite(lay)
    d=ImageDraw.Draw(im); d.ellipse((x-r,y-r,x+r,y+r),fill=(17,21,24,255),outline=(200,205,210,255),width=2); d.ellipse((x-r+4,y-r+4,x+r-4,y+r-4),fill=(*color,240))

def overlay_center(can, comp, center):
    can.alpha_composite(comp,(int(center[0]-comp.width/2),int(center[1]-comp.height/2)))

def source_piece(box, size, brightness=1.0, flip=False, rotate=0):
    piece=trim(SRC.crop(box))
    if flip: piece=piece.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    piece=piece.resize(size,Image.Resampling.LANCZOS)
    if brightness!=1.0: piece=ImageEnhance.Brightness(piece).enhance(brightness)
    if rotate: piece=piece.rotate(rotate,Image.Resampling.BICUBIC,expand=True)
    return piece

def role_details(high,role):
    W,H=high.size; cx=W//2
    if role=='Hunter':
        tipL=source_piece((420,700,640,1180),(150,330),1.0,False,-9)
        tipR=tipL.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        high.alpha_composite(tipL,(cx-170,20)); high.alpha_composite(tipR,(cx+20,20))
    elif role=='Bulwark':
        armor=source_piece((360,120,900,1030),(int(W*.36),int(H*.40)),.80)
        overlay_center(high,armor,(cx,H*.52))
        for s in (-1,1):
            shoulder=source_piece((90,210,470,690),(int(W*.13),int(H*.28)),.78,s>0,0)
            overlay_center(high,shoulder,(cx+s*W*.19,H*.49))
    elif role=='Titan':
        armor=source_piece((330,80,930,1110),(int(W*.42),int(H*.45)),.76)
        overlay_center(high,armor,(cx,H*.52))
        for s in (-1,1):
            shoulder=source_piece((70,180,500,760),(int(W*.16),int(H*.33)),.72,s>0,0)
            overlay_center(high,shoulder,(cx+s*W*.21,H*.49))
    elif role=='SiegeMass':
        core=source_piece((320,70,940,1150),(int(W*.50),int(H*.46)),.70)
        overlay_center(high,core,(cx,H*.55))
        ram=source_piece((470,20,790,640),(int(W*.18),int(H*.34)),.80)
        overlay_center(high,ram,(cx,H*.24))
        for s in (-1,1):
            slab=source_piece((20,430,520,1210),(int(W*.16),int(H*.30)),.68,s>0,0)
            overlay_center(high,slab,(cx+s*W*.22,H*.57))
    elif role=='Controller':
        crown=source_piece((450,30,810,580),(int(W*.22),int(H*.26)),.92)
        overlay_center(high,crown,(cx,H*.30))
        add_glow(high,(cx,int(H*.49)),int(min(W,H)*.040),(160,220,255))
        for s in (-1,1): add_glow(high,(int(cx+s*W*.15),int(H*.37)),int(min(W,H)*.014),(140,215,255))
    elif role=='Repairer':
        for s in (-1,1):
            pod=source_piece((120,210,470,760),(int(W*.10),int(H*.25)),.91,s>0,0)
            overlay_center(high,pod,(cx+s*W*.17,H*.31)); add_glow(high,(int(cx+s*W*.17),int(H*.19)),int(min(W,H)*.012),(140,220,255))
        add_glow(high,(cx,int(H*.49)),int(min(W,H)*.021),(160,225,255))
    elif role=='Burrower':
        drill=source_piece((470,20,790,650),(int(W*.18),int(H*.33)),.86)
        overlay_center(high,drill,(cx,H*.25)); add_glow(high,(cx,int(H*.105)),int(min(W,H)*.012),(175,220,255))
    elif role=='Artillery':
        railL=source_piece((470,20,650,720),(int(W*.085),int(H*.39)),.82)
        railR=railL.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
        overlay_center(high,railL,(cx-W*.09,H*.27)); overlay_center(high,railR,(cx+W*.09,H*.27))
        add_glow(high,(int(cx-W*.09),int(H*.07)),int(min(W,H)*.010),(165,215,255)); add_glow(high,(int(cx+W*.09),int(H*.07)),int(min(W,H)*.010),(165,215,255))
    return high

def finalize(role):
    high=role_details(base_high(role),role)
    high=ImageEnhance.Contrast(high).enhance(1.06)
    high=ImageEnhance.Sharpness(high).enhance(1.12)
    b=high.getchannel('A').getbbox(); high=high.crop(b)
    maxd=438; scale=min(maxd/high.width,maxd/high.height)
    high=high.resize((max(1,int(high.width*scale)),max(1,int(high.height*scale))),Image.Resampling.LANCZOS)
    out=Image.new('RGBA',(S,S),(0,0,0,0)); out.alpha_composite(high,((S-high.width)//2,(S-high.height)//2))
    return out

roles=['Drone','Hunter','Bulwark','Titan','SiegeMass','Controller','Repairer','Burrower','Artillery']
for role in roles:
    north=finalize(role)
    views={'north':north,'east':north.transpose(Image.Transpose.ROTATE_270),'south':north.transpose(Image.Transpose.ROTATE_180),'west':north.transpose(Image.Transpose.ROTATE_90)}
    for d,im in views.items(): im.save(OUT/f'WNG_Replicator{role}_{d}.png','PNG',optimize=True)
    views['south'].save(OUT/f'WNG_Replicator{role}.png','PNG',optimize=True)
for role in roles:
    for suffix in ['', '_north', '_east', '_south', '_west']:
        p=OUT/f'WNG_Replicator{role}{suffix}.png'
        with Image.open(p) as chk:
            chk.load()
            if chk.mode!='RGBA' or chk.size!=(512,512): raise RuntimeError(f'{p}: expected 512x512 RGBA')
            a=chk.getchannel('A')
            if not a.getbbox(): raise RuntimeError(f'{p}: empty alpha')
            edges=[a.crop((0,0,512,1)).getextrema()[1],a.crop((0,511,512,512)).getextrema()[1],a.crop((0,0,1,512)).getextrema()[1],a.crop((511,0,512,512)).getextrema()[1]]
            if any(edges): raise RuntimeError(f'{p}: alpha touches edge {edges}')
print(f'Generated {len(roles)} six-legged professional Replicator pawn families ({len(roles)*5} PNGs).')
