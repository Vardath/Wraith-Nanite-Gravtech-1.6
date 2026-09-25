from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math, random

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Textures" / "Things" / "Pawn" / "Replicator"
OUT.mkdir(parents=True, exist_ok=True)

HI = 1024
FINAL = 512
rng = random.Random(137)

COL = {
    'outline': (18,22,24,255),
    'deep': (35,41,44,255),
    'steel0': (68,75,78,255),
    'steel1': (90,99,102,255),
    'steel2': (119,128,130,255),
    'light': (176,184,184,255),
    'spec': (220,225,222,255),
    'cyan': (76,172,179,255),
    'cyan_hi': (155,229,229,255),
    'amber': (184,126,63,255),
    'amber_hi': (238,188,96,255),
}

def rotpt(p, ang, center=(HI/2,HI/2)):
    x,y=p; cx,cy=center
    r=math.radians(ang); dx=x-cx; dy=y-cy
    return (cx+dx*math.cos(r)-dy*math.sin(r),cy+dx*math.sin(r)+dy*math.cos(r))

def shadow_blob(canvas, bbox, blur=18, alpha=105):
    lay=Image.new('RGBA', canvas.size, (0,0,0,0))
    d=ImageDraw.Draw(lay)
    x0,y0,x1,y1=bbox
    d.ellipse((x0+8,y0+15,x1+18,y1+28), fill=(0,0,0,alpha))
    lay=lay.filter(ImageFilter.GaussianBlur(blur))
    canvas.alpha_composite(lay)

def module(canvas, center, size, angle=0, tone=1, accent=None, seams=True, bevel=7, chamfer=True):
    w,h=max(18,int(size[0])),max(18,int(size[1]))
    pad=max(24,int(max(w,h)*.16))
    tile=Image.new('RGBA',(w+2*pad,h+2*pad),(0,0,0,0))
    d=ImageDraw.Draw(tile)
    rr=max(5,int(min(w,h)*.11))
    d.rounded_rectangle((pad+7,pad+9,pad+w+7,pad+h+9),radius=rr,fill=(0,0,0,95))
    d.rounded_rectangle((pad,pad,pad+w,pad+h),radius=rr,fill=COL['outline'])
    base=[COL['steel0'],COL['steel1'],COL['steel2']][max(0,min(2,tone))]
    d.rounded_rectangle((pad+4,pad+4,pad+w-4,pad+h-4),radius=max(3,rr-3),fill=base)
    d.line((pad+rr,pad+5,pad+w-rr,pad+5),fill=COL['light'],width=max(2,bevel//2))
    d.line((pad+5,pad+rr,pad+5,pad+h-rr),fill=(149,159,160,220),width=max(2,bevel//3))
    d.line((pad+rr,pad+h-5,pad+w-rr,pad+h-5),fill=(23,28,31,245),width=max(3,bevel//2))
    d.line((pad+w-5,pad+rr,pad+w-5,pad+h-rr),fill=(31,37,39,240),width=max(3,bevel//2))
    if seams and w>55 and h>40:
        insetx=max(11,int(w*.15)); insety=max(10,int(h*.20))
        d.rounded_rectangle((pad+insetx,pad+insety,pad+w-insetx,pad+h-insety),
                            radius=max(3,rr//2), fill=COL['deep'], outline=(128,139,140,255), width=2)
        n=max(1,int(w//58))
        for i in range(1,n+1):
            x=pad+insetx+(w-2*insetx)*i/(n+1)
            d.line((x,pad+insety+4,x,pad+h-insety-4), fill=(87,98,100,180), width=2)
        d.line((pad+insetx+3,pad+insety+3,pad+w-insetx-3,pad+insety+3), fill=(175,184,184,135), width=2)
    if accent:
        col=COL['cyan'] if accent=='cyan' else COL['amber']
        hi=COL['cyan_hi'] if accent=='cyan' else COL['amber_hi']
        cx=pad+w/2; cy=pad+h/2
        d.rounded_rectangle((cx-max(10,w*.22),cy-3,cx+max(10,w*.22),cy+3),radius=2,fill=hi)
        d.ellipse((cx-5,cy-5,cx+5,cy+5),fill=col,outline=hi,width=2)
    if w>42 and h>34:
        for x,y in [(pad+10,pad+11),(pad+w-10,pad+h-11)]:
            d.ellipse((x-2,y-2,x+2,y+2),fill=(25,30,32,255),outline=(190,197,195,220),width=1)
    tile=tile.filter(ImageFilter.GaussianBlur(0.25))
    rot=tile.rotate(angle,Image.Resampling.BICUBIC,expand=True)
    canvas.alpha_composite(rot,(round(center[0]-rot.width/2),round(center[1]-rot.height/2)))

def joint(canvas, p, r=14, accent=False):
    x,y=p
    lay=Image.new('RGBA',canvas.size,(0,0,0,0))
    d=ImageDraw.Draw(lay)
    d.ellipse((x-r-3,y-r+2,x+r+3,y+r+8),fill=(0,0,0,100))
    d.ellipse((x-r,y-r,x+r,y+r),fill=COL['outline'],outline=COL['light'],width=2)
    d.ellipse((x-r*.58,y-r*.58,x+r*.58,y+r*.58),
              fill=COL['cyan'] if accent else COL['deep'],
              outline=COL['cyan_hi'] if accent else (143,151,151,255),width=2)
    canvas.alpha_composite(lay)

def chain(canvas,a,b,width=28,segments=3,tone=1,accent_end=False):
    ax,ay=a; bx,by=b; dx=bx-ax; dy=by-ay
    L=max(1,math.hypot(dx,dy)); ang=math.degrees(math.atan2(dy,dx))
    ux,uy=dx/L,dy/L
    gap=max(5,width*.15)
    seg=(L-gap*(segments-1))/segments
    for i in range(segments):
        start=i*(seg+gap)
        cx=ax+ux*(start+seg/2); cy=ay+uy*(start+seg/2)
        module(canvas,(cx,cy),(seg,width),ang,tone=tone,seams=False,
               accent='cyan' if accent_end and i==segments-1 else None)

def limb(canvas, hip, knee, ankle, foot, width=28, claw=False, heavy=False, accent=False):
    chain(canvas,hip,knee,width,3,tone=2 if heavy else 1)
    joint(canvas,knee,width*.32)
    chain(canvas,knee,ankle,width*.88,2,tone=1)
    joint(canvas,ankle,width*.28,accent=accent)
    chain(canvas,ankle,foot,width*.66,2,tone=0)
    vx=foot[0]-ankle[0]; vy=foot[1]-ankle[1]; L=max(1,math.hypot(vx,vy)); ux,uy=vx/L,vy/L
    end=(foot[0]+ux*(45 if claw else 24),foot[1]+uy*(45 if claw else 24))
    chain(canvas,foot,end,width*.42,1,tone=2 if claw else 0)

def mandible(canvas, root, side, length=110, spread=55, width=26, heavy=False):
    x,y=root
    s=1 if side>0 else -1
    mid=(x+s*spread*.55,y+length*.48)
    tip=(x+s*spread,y+length)
    chain(canvas,root,mid,width,2,tone=2 if heavy else 1)
    joint(canvas,mid,width*.30)
    chain(canvas,mid,tip,width*.65,2,tone=0)
    hook=(tip[0]-s*28,tip[1]+24)
    chain(canvas,tip,hook,width*.38,1,tone=2)

def sensor_core(canvas, center, r=26, color='cyan'):
    x,y=center
    col=COL['cyan'] if color=='cyan' else COL['amber']
    hi=COL['cyan_hi'] if color=='cyan' else COL['amber_hi']
    glow=Image.new('L',canvas.size,0); gd=ImageDraw.Draw(glow)
    gd.ellipse((x-r*2,y-r*2,x+r*2,y+r*2),fill=120)
    glow=glow.filter(ImageFilter.GaussianBlur(r*.8))
    lay=Image.new('RGBA',canvas.size,(*col[:3],0)); lay.putalpha(glow.point(lambda q:int(q*.23)))
    canvas.alpha_composite(lay)
    d=ImageDraw.Draw(canvas)
    d.ellipse((x-r,y-r,x+r,y+r),fill=COL['outline'],outline=COL['light'],width=3)
    d.ellipse((x-r*.62,y-r*.62,x+r*.62,y+r*.62),fill=COL['deep'],outline=col,width=4)
    d.ellipse((x-r*.22,y-r*.22,x+r*.22,y+r*.22),fill=hi)

def add_shadow_for_machine(canvas, scale=1.0):
    shadow_blob(canvas,(180,265,845,770),blur=int(26*scale),alpha=90)

def legs_radial(canvas, specs, width=28, heavy=False, claws=False):
    for pts in sorted(specs,key=lambda p:sum(q[1] for q in p)/4):
        limb(canvas,*pts,width=width,claw=claws,heavy=heavy)

def mirror_specs(left_specs):
    return left_specs+[tuple((HI-x,y) for x,y in pts) for pts in left_specs]

def render_drone():
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); add_shadow_for_machine(c,.8)
    left=[
        ((455,430),(350,360),(260,330),(205,315)),
        ((445,520),(325,515),(235,555),(185,575)),
        ((462,605),(365,675),(285,735),(235,775)),
    ]
    legs_radial(c,mirror_specs(left),width=25,claws=False)
    module(c,(512,520),(168,210),90,tone=1)
    module(c,(512,438),(124,82),0,tone=2)
    module(c,(512,596),(136,72),0,tone=0)
    mandible(c,(485,626),-1,length=78,spread=34,width=18)
    mandible(c,(539,626),1,length=78,spread=34,width=18)
    sensor_core(c,(512,452),18,'cyan')
    return c

def render_hunter():
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); add_shadow_for_machine(c,.9)
    left=[
        ((462,405),(345,315),(235,255),(170,220)),
        ((448,505),(310,500),(205,545),(145,570)),
        ((463,620),(350,700),(245,780),(190,835)),
    ]
    legs_radial(c,mirror_specs(left),width=24)
    module(c,(512,520),(148,286),90,tone=1)
    module(c,(512,402),(112,96),0,tone=2)
    module(c,(512,655),(128,92),0,tone=0)
    for side in (-1,1):
        root=(512+side*58,620)
        elbow=(512+side*125,685)
        tip=(512+side*205,770)
        chain(c,root,elbow,22,2,tone=1); joint(c,elbow,8)
        chain(c,elbow,tip,16,2,tone=2)
        hook=(tip[0]-side*38,tip[1]+42); chain(c,tip,hook,11,1,tone=2)
    sensor_core(c,(512,414),16)
    return c

def render_bulwark():
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); add_shadow_for_machine(c,1.0)
    left=[
        ((430,390),(325,320),(240,300),(190,290)),
        ((420,465),(300,430),(205,445),(155,455)),
        ((420,570),(295,610),(205,650),(155,675)),
        ((440,650),(340,735),(270,810),(225,855)),
    ]
    legs_radial(c,mirror_specs(left),width=38,heavy=True)
    module(c,(512,525),(270,226),0,tone=1)
    module(c,(512,420),(230,74),0,tone=2)
    module(c,(512,630),(248,82),0,tone=0)
    module(c,(395,520),(74,188),90,tone=2)
    module(c,(629,520),(74,188),90,tone=2)
    module(c,(512,704),(220,64),0,tone=2)
    mandible(c,(455,718),-1,length=62,spread=35,width=28,heavy=True)
    mandible(c,(569,718),1,length=62,spread=35,width=28,heavy=True)
    return c

def render_titan():
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); add_shadow_for_machine(c,1.15)
    left=[
        ((410,350),(295,265),(190,240),(135,225)),
        ((395,440),(255,405),(160,420),(100,430)),
        ((395,570),(250,605),(155,650),(95,675)),
        ((420,670),(310,765),(220,845),(165,900)),
    ]
    legs_radial(c,mirror_specs(left),width=48,heavy=True,claws=True)
    module(c,(512,515),(290,300),90,tone=1)
    module(c,(512,375),(240,84),0,tone=2)
    module(c,(512,510),(250,74),0,tone=1)
    module(c,(512,655),(270,88),0,tone=0)
    for sx in (-1,1):
        module(c,(512+sx*150,505),(100,210),90,tone=2)
    mandible(c,(455,690),-1,length=155,spread=80,width=34,heavy=True)
    mandible(c,(569,690),1,length=155,spread=80,width=34,heavy=True)
    sensor_core(c,(512,392),22)
    return c

def render_siegemass():
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); add_shadow_for_machine(c,1.25)
    left=[
        ((390,335),(275,255),(175,230),(115,215)),
        ((370,410),(225,370),(125,380),(70,390)),
        ((365,500),(215,500),(110,525),(55,545)),
        ((375,590),(230,635),(130,685),(75,720)),
        ((405,670),(295,770),(210,850),(160,915)),
    ]
    legs_radial(c,mirror_specs(left),width=50,heavy=True,claws=True)
    module(c,(512,520),(350,280),0,tone=1)
    for x in (390,512,634):
        module(c,(x,400),(104,110),0,tone=2)
        module(c,(x,520),(104,105),0,tone=1)
        module(c,(x,635),(104,105),0,tone=0)
    module(c,(512,710),(330,84),0,tone=2)
    for sx in (-1,1):
        mandible(c,(512+sx*80,725),sx,length=145,spread=88,width=38,heavy=True)
    sensor_core(c,(512,708),26,'amber')
    return c

def render_controller():
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); add_shadow_for_machine(c,.95)
    left=[
        ((445,405),(330,335),(235,310),(180,295)),
        ((430,515),(300,515),(205,555),(150,575)),
        ((445,625),(345,705),(270,770),(220,815)),
    ]
    legs_radial(c,mirror_specs(left),width=31)
    module(c,(512,525),(210,245),90,tone=1)
    module(c,(512,415),(170,84),0,tone=2,accent='cyan')
    module(c,(512,625),(172,78),0,tone=0)
    sensor_core(c,(512,455),30)
    for sx in (-1,1):
        base=(512+sx*66,430); tip=(512+sx*122,335)
        chain(c,base,tip,15,3,tone=0,accent_end=True)
        joint(c,tip,7,accent=True)
    chain(c,(412,370),(612,370),18,4,tone=1)
    sensor_core(c,(512,370),14)
    return c

def render_repairer():
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); add_shadow_for_machine(c,.9)
    left=[
        ((450,420),(340,355),(255,340),(205,335)),
        ((438,525),(310,525),(220,565),(170,585)),
        ((455,625),(355,695),(285,755),(240,795)),
    ]
    legs_radial(c,mirror_specs(left),width=26)
    module(c,(512,525),(178,226),90,tone=1)
    module(c,(512,435),(132,78),0,tone=2)
    for sx in (-1,1):
        root=(512+sx*58,560); elbow=(512+sx*105,620); hand=(512+sx*145,675)
        chain(c,root,elbow,18,2,tone=1); joint(c,elbow,8,accent=True)
        chain(c,elbow,hand,14,2,tone=0,accent_end=True)
        for dy in (-16,0,16):
            chain(c,hand,(hand[0]+sx*42,hand[1]+dy+20),8,1,tone=2)
    sensor_core(c,(512,458),18)
    return c

def render_burrower():
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); add_shadow_for_machine(c,.95)
    left=[
        ((430,390),(305,315),(205,300),(145,295)),
        ((415,520),(280,520),(180,555),(120,575)),
        ((445,635),(340,720),(260,790),(205,835)),
    ]
    legs_radial(c,mirror_specs(left),width=34,heavy=True)
    module(c,(512,530),(210,260),90,tone=1)
    for y,w in [(410,165),(470,185),(590,190)]:
        module(c,(512,y),(w,58),0,tone=2 if y<500 else 0)
    for sx in (-1,1):
        mandible(c,(512+sx*54,640),sx,length=125,spread=65,width=28,heavy=True)
        chain(c,(512+sx*30,665),(512+sx*42,790),20,3,tone=2)
    return c

def render_artillery():
    c=Image.new('RGBA',(HI,HI),(0,0,0,0)); add_shadow_for_machine(c,1.05)
    left=[
        ((410,390),(295,315),(205,290),(145,275)),
        ((395,475),(255,440),(160,450),(100,455)),
        ((400,585),(260,625),(160,665),(100,690)),
        ((430,660),(325,750),(245,825),(195,875)),
    ]
    legs_radial(c,mirror_specs(left),width=35,heavy=True)
    module(c,(512,545),(240,250),90,tone=1)
    module(c,(512,450),(205,80),0,tone=2)
    module(c,(512,650),(220,72),0,tone=0)
    sensor_core(c,(512,520),22,'amber')
    for y,w,h,t in [(620,110,58,2),(675,90,52,1),(730,72,46,1),(780,56,40,0)]:
        module(c,(512,y),(w,h),0,tone=t,accent='amber' if y==780 else None)
    module(c,(392,540),(78,160),90,tone=2)
    module(c,(632,540),(78,160),90,tone=2)
    return c

def finalize(img, target=430):
    a=img.getchannel('A'); box=a.getbbox()
    if not box: raise RuntimeError('empty')
    crop=img.crop(box)
    sc=min(target/crop.width,target/crop.height)
    nw=max(1,round(crop.width*sc)); nh=max(1,round(crop.height*sc))
    crop=crop.resize((nw,nh),Image.Resampling.LANCZOS)
    out=Image.new('RGBA',(FINAL,FINAL),(0,0,0,0))
    out.alpha_composite(crop,((FINAL-nw)//2,(FINAL-nh)//2))
    px=out.load()
    for y in range(FINAL):
        for x in range(FINAL):
            if px[x,y][3]==0:
                px[x,y]=(0,0,0,0)
    return out

RENDERS = {
    'Drone': render_drone,
    'Hunter': render_hunter,
    'Bulwark': render_bulwark,
    'Titan': render_titan,
    'SiegeMass': render_siegemass,
    'Controller': render_controller,
    'Repairer': render_repairer,
    'Burrower': render_burrower,
    'Artillery': render_artillery,
}

TARGET_PIXELS = {
    'Drone': 360,
    'Hunter': 398,
    'Bulwark': 410,
    'Titan': 430,
    'SiegeMass': 440,
    'Controller': 398,
    'Repairer': 390,
    'Burrower': 405,
    'Artillery': 420,
}

for role, renderer in RENDERS.items():
    south = finalize(renderer(), TARGET_PIXELS[role])
    south.save(OUT / f'WNG_Replicator{role}.png', 'PNG', optimize=True)
    south.save(OUT / f'WNG_Replicator{role}_south.png', 'PNG', optimize=True)
    south.rotate(180, Image.Resampling.BICUBIC).save(OUT / f'WNG_Replicator{role}_north.png', 'PNG', optimize=True)
    south.rotate(90, Image.Resampling.BICUBIC).save(OUT / f'WNG_Replicator{role}_east.png', 'PNG', optimize=True)
    south.rotate(270, Image.Resampling.BICUBIC).save(OUT / f'WNG_Replicator{role}_west.png', 'PNG', optimize=True)

expected = []
for role in RENDERS:
    for suffix in ('', '_north', '_east', '_south', '_west'):
        expected.append(OUT / f'WNG_Replicator{role}{suffix}.png')

for path in expected:
    with Image.open(path) as im:
        im.load()
        if im.mode != 'RGBA' or im.size != (512,512):
            raise RuntimeError(f'{path}: expected RGBA 512x512, got {im.mode} {im.size}')
        alpha = im.getchannel('A')
        box = alpha.getbbox()
        if box is None:
            raise RuntimeError(f'{path}: empty alpha')
        if box[0] <= 2 or box[1] <= 2 or box[2] >= 510 or box[3] >= 510:
            raise RuntimeError(f'{path}: art touches unsafe canvas edge {box}')
        px = im.load()
        for y in range(512):
            for x in range(512):
                if px[x,y][3] == 0 and px[x,y][:3] != (0,0,0):
                    raise RuntimeError(f'{path}: dirty transparent RGB at {(x,y)}')

print(f'Generated and validated {len(expected)} differentiated Replicator pawn textures.')
