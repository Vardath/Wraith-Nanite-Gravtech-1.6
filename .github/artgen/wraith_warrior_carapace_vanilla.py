from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
from pathlib import Path
import numpy as np, math, random, os
from scipy.ndimage import distance_transform_edt, gaussian_filter, binary_dilation, map_coordinates
import cairosvg

ROOT = Path("Textures/Things/Pawn/Humanlike/Apparel/Wraith")
REF_ARMOR = Path("/tmp/madness/Raw sprites/apparel")
REF_BODY = Path("/tmp/rwvela/src/common/human/bodies")
ROOT.mkdir(parents=True, exist_ok=True)

HI = 768
OUT = 192
BODIES = ["Male","Female","Thin","Hulk","Fat"]
DIRS = ["south","north","east"]
SEED = 77031

def svg_mask(direction):
    src = REF_ARMOR / f"PowerArmor_Male_{direction}.svg"
    png = Path("/tmp") / f"pa_{direction}.png"
    cairosvg.svg2png(url=str(src), write_to=str(png), output_width=HI, output_height=HI)
    im = Image.open(png).convert("RGBA")
    a = np.array(im.getchannel("A"), dtype=np.uint8)
    return a

def body_alpha(body, direction):
    p = REF_BODY / f"Naked_{body}_{direction}.png"
    im = Image.open(p).convert("RGBA")
    im = im.resize((HI,HI), Image.Resampling.LANCZOS)
    return np.array(im.getchannel("A"), dtype=np.uint8)

def bbox(mask):
    ys,xs = np.nonzero(mask > 8)
    return (xs.min(), ys.min(), xs.max()+1, ys.max()+1)

def deform_mask(base, body, direction):
    male = body_alpha("Male", direction)
    targ = body_alpha(body, direction)
    mb = bbox(male); tb = bbox(targ); ab = bbox(base)
    mw,mh = mb[2]-mb[0], mb[3]-mb[1]
    tw,th = tb[2]-tb[0], tb[3]-tb[1]
    sx = tw/max(1,mw); sy = th/max(1,mh)
    # Armor scales with the body but keeps vanilla armor's extra shell bulk.
    acx=(ab[0]+ab[2])/2; acy=(ab[1]+ab[3])/2
    mcx=(mb[0]+mb[2])/2; mcy=(mb[1]+mb[3])/2
    tcx=(tb[0]+tb[2])/2; tcy=(tb[1]+tb[3])/2
    crop = Image.fromarray(base, "L").crop(ab)
    nw=max(1,int(round(crop.width*sx))); nh=max(1,int(round(crop.height*sy)))
    crop=crop.resize((nw,nh),Image.Resampling.LANCZOS)
    out=Image.new("L",(HI,HI),0)
    cx = acx + (tcx-mcx)
    cy = acy + (tcy-mcy)
    out.paste(crop,(int(round(cx-nw/2)),int(round(cy-nh/2))))
    arr=np.array(out,dtype=np.uint8)
    # Preserve the vanilla contour, but keep every body variant inside the texture canvas.
    ys,xs=np.nonzero(arr>8)
    if len(xs):
        pad=20
        dx=0; dy=0
        if xs.min()<pad: dx=pad-xs.min()
        if xs.max()>HI-1-pad: dx=(HI-1-pad)-xs.max()
        if ys.min()<pad: dy=pad-ys.min()
        if ys.max()>HI-1-pad: dy=(HI-1-pad)-ys.max()
        if dx or dy:
            shifted=np.zeros_like(arr)
            y0=max(0,dy); y1=min(HI,HI+dy)
            x0=max(0,dx); x1=min(HI,HI+dx)
            sy0=max(0,-dy); sy1=sy0+(y1-y0)
            sx0=max(0,-dx); sx1=sx0+(x1-x0)
            shifted[y0:y1,x0:x1]=arr[sy0:sy1,sx0:sx1]
            arr=shifted
    return arr

def smooth_noise(shape, sigma, seed):
    rng=np.random.default_rng(seed)
    n=rng.normal(0,1,shape)
    n=gaussian_filter(n,sigma=sigma)
    n=(n-n.mean())/(n.std()+1e-6)
    return n

def paint_base(mask, seed):
    m = mask.astype(np.float32)/255
    inside = m>0.02
    dist = distance_transform_edt(inside)
    edge = np.clip(dist/30,0,1)
    yy,xx=np.mgrid[0:HI,0:HI]
    # upper-left studio-style vanilla light
    lx=(1-xx/HI)*0.55 + (1-yy/HI)*0.45
    n1=smooth_noise((HI,HI),3.2,seed)
    n2=smooth_noise((HI,HI),17,seed+1)
    n3=smooth_noise((HI,HI),48,seed+2)
    # dark living chitin / leather, with muted purple-brown undertone
    base=np.zeros((HI,HI,4),dtype=np.float32)
    lum = 18*lx + 5*n1 + 9*n2 + 6*n3 - 13*(1-edge)
    base[...,0]=np.clip(38+lum*0.72,0,255)
    base[...,1]=np.clip(32+lum*0.62,0,255)
    base[...,2]=np.clip(43+lum*0.78,0,255)
    base[...,3]=mask
    return Image.fromarray(base.astype(np.uint8),"RGBA")

def add_outline(img, mask):
    inside=mask>8
    outer=binary_dilation(inside,iterations=15)
    ring=outer & ~inside
    arr=np.array(img)
    arr[ring,0]=8; arr[ring,1]=7; arr[ring,2]=9; arr[ring,3]=235
    return Image.fromarray(arr,"RGBA")

def path_mask(points,width,blur=0):
    im=Image.new("L",(HI,HI),0); d=ImageDraw.Draw(im)
    d.line(points,fill=255,width=width,joint="curve")
    if blur: im=im.filter(ImageFilter.GaussianBlur(blur))
    return np.array(im,dtype=np.uint8)

def clip_to(mask, layer_alpha):
    return ((layer_alpha.astype(np.uint16)*mask.astype(np.uint16))//255).astype(np.uint8)

def add_ridge(img, mask, points, width, color, highlight, shadow=0.45, seed=0):
    # sculpted organic rib, not a flat polygon
    core=path_mask(points,width)
    core=clip_to(mask,core)
    if core.max()==0: return
    # shadow underneath
    sh=Image.new("RGBA",(HI,HI),(0,0,0,0))
    sha=Image.fromarray(core,"L").filter(ImageFilter.GaussianBlur(max(2,width//5)))
    sha=sha.point(lambda p:int(p*shadow))
    sh.putalpha(sha)
    img.alpha_composite(sh,(max(1,width//8),max(1,width//8)))
    # textured ridge fill
    arr=np.zeros((HI,HI,4),dtype=np.uint8)
    n=smooth_noise((HI,HI),max(1,width/10),SEED+seed)
    for i,c in enumerate(color):
        arr[...,i]=np.clip(c+n*7,0,255)
    arr[...,3]=core
    img.alpha_composite(Image.fromarray(arr,"RGBA"))
    # specular crest
    hi=path_mask([(x-int(width*.09),y-int(width*.12)) for x,y in points],max(2,int(width*.12)),blur=max(1,width//12))
    hi=clip_to(core,hi)
    hia=np.zeros((HI,HI,4),dtype=np.uint8)
    hia[...,0]=highlight[0]; hia[...,1]=highlight[1]; hia[...,2]=highlight[2]
    hia[...,3]=(hi.astype(np.float32)*0.62).astype(np.uint8)
    img.alpha_composite(Image.fromarray(hia,"RGBA"))

def add_vein(img, mask, points, width=4, strength=0.45):
    core=path_mask(points,width)
    core=clip_to(mask,core)
    glow=Image.fromarray(core,"L").filter(ImageFilter.GaussianBlur(width*2))
    g=np.zeros((HI,HI,4),dtype=np.uint8)
    g[...,0]=62; g[...,1]=154; g[...,2]=135
    g[...,3]=(np.array(glow)*strength).astype(np.uint8)
    img.alpha_composite(Image.fromarray(g,"RGBA"))
    c=np.zeros((HI,HI,4),dtype=np.uint8)
    c[...,0]=91; c[...,1]=183; c[...,2]=157
    c[...,3]=(core.astype(np.float32)*0.52).astype(np.uint8)
    img.alpha_composite(Image.fromarray(c,"RGBA"))

def add_scratches(img, mask, seed, count=55):
    rng=random.Random(seed)
    d=ImageDraw.Draw(img)
    b=bbox(mask)
    for _ in range(count):
        x=rng.randint(b[0],b[2]-1); y=rng.randint(b[1],b[3]-1)
        if mask[y,x] < 32: continue
        L=rng.randint(5,20); ang=rng.uniform(-0.6,0.6)
        x2=x+int(math.cos(ang)*L); y2=y+int(math.sin(ang)*L)
        col=(138,126,140,rng.randint(25,60))
        d.line((x,y,x2,y2),fill=col,width=rng.choice([1,1,2]))

def decorate_south(img, mask):
    b=bbox(mask); x0,y0,x1,y1=b; w=x1-x0; h=y1-y0; cx=(x0+x1)//2
    # heavy organic shoulder carapace
    add_ridge(img,mask,[(x0+int(.08*w),y0+int(.24*h)),(x0+int(.22*w),y0+int(.13*h)),(x0+int(.40*w),y0+int(.20*h))],28,(74,65,71),(146,134,139),seed=1)
    add_ridge(img,mask,[(x1-int(.08*w),y0+int(.24*h)),(x1-int(.22*w),y0+int(.13*h)),(x1-int(.40*w),y0+int(.20*h))],28,(70,61,68),(139,128,134),seed=2)
    # central breastbone
    add_ridge(img,mask,[(cx,y0+int(.20*h)),(cx-3,y0+int(.42*h)),(cx+2,y0+int(.66*h)),(cx,y0+int(.84*h))],20,(67,57,65),(134,120,130),seed=3)
    # rib arcs
    for i,yy in enumerate([.34,.47,.60,.73]):
        y=y0+int(yy*h); bend=int((i-1.5)*3)
        add_ridge(img,mask,[(cx-7,y),(x0+int(.34*w),y+12+bend),(x0+int(.18*w),y+24+bend)],12,(58,49,58),(118,105,117),seed=10+i)
        add_ridge(img,mask,[(cx+7,y),(x1-int(.34*w),y+12+bend),(x1-int(.18*w),y+24+bend)],12,(56,47,56),(114,102,113),seed=20+i)
    # abdomen overlapping chitin bands
    for i,yy in enumerate([.66,.74,.82]):
        y=y0+int(yy*h)
        add_ridge(img,mask,[(x0+int(.31*w),y),(cx,y+8),(x1-int(.31*w),y)],10,(51,44,52),(103,94,103),seed=30+i)
    add_vein(img,mask,[(cx-6,y0+int(.28*h)),(cx-10,y0+int(.45*h)),(cx-3,y0+int(.58*h))],4,.28)

def decorate_north(img, mask):
    b=bbox(mask); x0,y0,x1,y1=b; w=x1-x0; h=y1-y0; cx=(x0+x1)//2
    add_ridge(img,mask,[(x0+int(.08*w),y0+int(.24*h)),(x0+int(.22*w),y0+int(.14*h)),(x0+int(.39*w),y0+int(.20*h))],27,(72,63,70),(140,130,136),seed=41)
    add_ridge(img,mask,[(x1-int(.08*w),y0+int(.24*h)),(x1-int(.22*w),y0+int(.14*h)),(x1-int(.39*w),y0+int(.20*h))],27,(69,60,67),(136,126,132),seed=42)
    # segmented spinal carapace
    for i,yy in enumerate([.26,.38,.50,.62,.74]):
        y=y0+int(yy*h)
        add_ridge(img,mask,[(cx,y-12),(cx-5,y),(cx,y+12),(cx+5,y)],15,(67,57,65),(133,120,130),seed=50+i)
    for i,yy in enumerate([.38,.52,.66]):
        y=y0+int(yy*h)
        add_ridge(img,mask,[(cx-6,y),(x0+int(.35*w),y+10),(x0+int(.20*w),y+18)],10,(55,47,56),(110,100,110),seed=60+i)
        add_ridge(img,mask,[(cx+6,y),(x1-int(.35*w),y+10),(x1-int(.20*w),y+18)],10,(55,47,56),(110,100,110),seed=70+i)
    add_vein(img,mask,[(cx+4,y0+int(.23*h)),(cx+7,y0+int(.48*h)),(cx+3,y0+int(.70*h))],3,.23)

def decorate_east(img, mask):
    b=bbox(mask); x0,y0,x1,y1=b; w=x1-x0; h=y1-y0
    # shoulder crown follows vanilla side profile
    add_ridge(img,mask,[(x0+int(.26*w),y0+int(.20*h)),(x0+int(.54*w),y0+int(.10*h)),(x0+int(.78*w),y0+int(.22*h))],27,(75,65,71),(145,133,138),seed=81)
    # long flank plates
    for i,xx in enumerate([.44,.56,.66]):
        x=x0+int(xx*w)
        add_ridge(img,mask,[(x,y0+int(.30*h)),(x+5,y0+int(.49*h)),(x-2,y0+int(.70*h)),(x+3,y0+int(.86*h))],12,(58,49,57),(116,104,114),seed=90+i)
    for i,yy in enumerate([.50,.64,.77]):
        y=y0+int(yy*h)
        add_ridge(img,mask,[(x0+int(.32*w),y),(x0+int(.56*w),y+7),(x0+int(.76*w),y+4)],9,(51,44,52),(104,95,103),seed=100+i)
    add_vein(img,mask,[(x0+int(.60*w),y0+int(.25*h)),(x0+int(.64*w),y0+int(.46*h)),(x0+int(.60*w),y0+int(.60*h))],3,.22)

def render(mask, direction, seed):
    img=paint_base(mask,seed)
    if direction=="south": decorate_south(img,mask)
    elif direction=="north": decorate_north(img,mask)
    else: decorate_east(img,mask)
    add_scratches(img,mask,seed+900,48)
    img=add_outline(img,mask)
    # clean fully transparent RGB and downsample
    img=img.resize((OUT,OUT),Image.Resampling.LANCZOS)
    arr=np.array(img)
    arr[arr[...,3]==0,:3]=0
    return Image.fromarray(arr,"RGBA")

base_masks={d:svg_mask(d) for d in DIRS}
rendered={}
for body in BODIES:
    for d in DIRS:
        m=deform_mask(base_masks[d],body,d)
        rendered[(body,d)]=render(m,d,SEED+hash(body+d)%10000)

# Tile/inventory art: vanilla PowerArmor front silhouette, centered as a ground item.
tile=rendered[("Male","south")].copy()
a=np.array(tile.getchannel("A"))
ys,xs=np.nonzero(a>0)
crop=tile.crop((xs.min(),ys.min(),xs.max()+1,ys.max()+1))
# item tile keeps same silhouette but is centered and slightly smaller, like vanilla ground apparel
crop.thumbnail((118,118),Image.Resampling.LANCZOS)
canvas=Image.new("RGBA",(OUT,OUT),(0,0,0,0))
canvas.alpha_composite(crop,((OUT-crop.width)//2,(OUT-crop.height)//2))
arr=np.array(canvas); arr[arr[...,3]==0,:3]=0
Image.fromarray(arr,"RGBA").save(ROOT/"WNG_WarriorCarapace.png",optimize=True)

for body in BODIES:
    south=rendered[(body,"south")]
    south.save(ROOT/f"WNG_WarriorCarapace_{body}.png",optimize=True)
    south.save(ROOT/f"WNG_WarriorCarapace_{body}_south.png",optimize=True)
    rendered[(body,"north")].save(ROOT/f"WNG_WarriorCarapace_{body}_north.png",optimize=True)
    east=rendered[(body,"east")]
    east.save(ROOT/f"WNG_WarriorCarapace_{body}_east.png",optimize=True)
    east.transpose(Image.Transpose.FLIP_LEFT_RIGHT).save(ROOT/f"WNG_WarriorCarapace_{body}_west.png",optimize=True)

# Preserve legacy bare directional roots used by current repo, using Male body.
rendered[("Male","south")].save(ROOT/"WNG_WarriorCarapace_south.png",optimize=True)
rendered[("Male","north")].save(ROOT/"WNG_WarriorCarapace_north.png",optimize=True)
rendered[("Male","east")].save(ROOT/"WNG_WarriorCarapace_east.png",optimize=True)
rendered[("Male","east")].transpose(Image.Transpose.FLIP_LEFT_RIGHT).save(ROOT/"WNG_WarriorCarapace_west.png",optimize=True)

files=sorted(ROOT.glob("WNG_WarriorCarapace*.png"))
assert len(files)==30, len(files)
# The live family historically includes 30 files: tile + 5 roots + 20 body directions + 4 bare directions.
for p in files:
    im=Image.open(p).convert("RGBA"); im.load()
    assert im.size==(192,192), (p,im.size)
    a=np.array(im.getchannel("A"))
    ys,xs=np.nonzero(a>0)
    assert len(xs)>0,p
    assert xs.min()>0 and ys.min()>0 and xs.max()<191 and ys.max()<191,(p,(xs.min(),ys.min(),xs.max(),ys.max()))
    rgba=np.array(im)
    assert np.all(rgba[rgba[...,3]==0,:3]==0),p
print("WARRIOR_CARAPACE_VANILLA_POWERARMOR_IMPLEMENTATION_OK",len(files))
