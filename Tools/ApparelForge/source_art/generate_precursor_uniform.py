from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageEnhance
import numpy as np
from scipy.ndimage import gaussian_filter
import random, math

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "ArtSource" / "Apparel" / "precursor_uniform"
OUT.mkdir(parents=True, exist_ok=True)

S = 1536
SEEDS = {"south": 12101, "north": 12102, "east": 12103}

# Stargate-first design brief embodied here:
# Lantean/Ancient clothing reads as refined, pale, practical and technologically
# advanced without turning into plated power armour.  The garment is a fitted
# high-collar field uniform with woven ivory-silver cloth, graphite underweave,
# restrained blue conductor embroidery and aged silver fasteners.
# RimWorld geometry is applied later by ApparelForge; these are finished masters.

def _noise(seed: int):
    rng = np.random.default_rng(seed)
    n = rng.normal(0, 1, (S, S))
    low = gaussian_filter(n, 34)
    mid = gaussian_filter(n, 8) - gaussian_filter(n, 28)
    fine = n - gaussian_filter(n, 2.2)
    for a in (low, mid, fine):
        a -= a.mean()
        a /= a.std() + 1e-6
    return low, mid, fine

def _garment_mask(view: str) -> Image.Image:
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    if view in ("south", "north"):
        pts = [
            (430,315),(520,258),(660,226),(768,218),(876,226),(1016,258),(1106,315),
            (1152,452),(1138,706),(1090,980),(1020,1188),(912,1320),(768,1372),
            (624,1320),(516,1188),(446,980),(398,706),(384,452)
        ]
        d.polygon(pts, fill=255)
        d.ellipse((430,220,1106,688), fill=255)
        d.rounded_rectangle((422,468,1114,1225), radius=246, fill=255)
    else:
        pts = [
            (516,315),(610,265),(748,236),(870,244),(982,300),(1056,405),
            (1082,590),(1058,830),(1010,1055),(936,1238),(832,1336),(716,1360),
            (616,1318),(534,1210),(476,1040),(442,822),(430,590),(452,408)
        ]
        d.polygon(pts, fill=255)
        d.ellipse((505,236,1037,680), fill=255)
        d.rounded_rectangle((494,448,1048,1224), radius=214, fill=255)
    return m.filter(ImageFilter.GaussianBlur(2.4))

def _base_cloth(view: str, mask: Image.Image, seed: int) -> Image.Image:
    low, mid, fine = _noise(seed)
    yy, xx = np.mgrid[0:S, 0:S]

    # Hand-painted broad illumination and soft cloth body.
    cx = .43 if view == "east" else .46
    key = np.exp(-(((xx/S)-cx)/.42)**2 - (((yy/S)-.33)/.56)**2)
    lower = np.clip((yy/S)-.58, 0, 1)
    side = np.clip(np.abs((xx/S)-.5)-.25, 0, 1)

    # Fine woven structure; visible at master resolution, restrained at 192 px.
    weave = (
        1.8*np.sin(xx/8.0) +
        1.2*np.sin(yy/10.5) +
        .7*np.sin((xx+yy)/15.5)
    )

    L = (
        .52 + .25*key - .16*lower - .055*side
        + .055*low + .050*mid + .016*fine + .006*weave
    )
    L = np.clip(L, .23, .96)

    # Lantean ivory-silver engineered textile, not sterile white plastic.
    shadow = np.array([42, 48, 50.], np.float32)
    midc   = np.array([152, 160, 158.], np.float32)
    high   = np.array([220, 222, 211.], np.float32)
    rgb = np.empty((S,S,3), np.float32)
    lo = L < .52
    t = np.clip(L/.52, 0, 1)
    rgb[lo] = shadow + (midc-shadow)*t[lo,None]
    t2 = np.clip((L-.52)/.48, 0, 1)
    rgb[~lo] = midc + (high-midc)*t2[~lo,None]

    arr = np.zeros((S,S,4), np.uint8)
    arr[...,:3] = np.clip(rgb,0,255).astype(np.uint8)
    arr[...,3] = np.asarray(mask)
    return Image.fromarray(arr, "RGBA")

def _poly_mask(points, blur=1.6):
    m = Image.new("L", (S,S), 0)
    ImageDraw.Draw(m).polygon(points, fill=255)
    if blur:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    return m

def _paint_region(im, points, colour, alpha=225, seed=0, edge=(125,134,132), edge_alpha=125):
    pm = _poly_mask(points, 2.0)
    low, mid, fine = _noise(seed + 700)
    yy, xx = np.mgrid[0:S,0:S]
    tex = .052*low + .070*mid + .020*fine
    grad = np.clip(.86 - .17*(yy/S) + tex, .45, 1.08)
    a = np.zeros((S,S,4), np.uint8)
    for c in range(3):
        a[...,c] = np.clip(colour[c]*grad,0,255)
    a[...,3] = (np.asarray(pm)*(alpha/255)).astype(np.uint8)
    im.alpha_composite(Image.fromarray(a, "RGBA"))

    e = Image.new("RGBA",(S,S),(0,0,0,0))
    d = ImageDraw.Draw(e)
    d.line(points+[points[0]], fill=(*edge,edge_alpha), width=4, joint="curve")
    im.alpha_composite(e.filter(ImageFilter.GaussianBlur(.8)))

def _stroke(im, pts, colour, width, alpha=160, blur=.5):
    lay = Image.new("RGBA",(S,S),(0,0,0,0))
    ImageDraw.Draw(lay).line(pts, fill=(*colour,alpha), width=width, joint="curve")
    if blur:
        lay = lay.filter(ImageFilter.GaussianBlur(blur))
    im.alpha_composite(lay)

def _stitches(im, a, b, count, seed=0):
    rnd = random.Random(seed)
    x1,y1=a; x2,y2=b
    dx=x2-x1; dy=y2-y1
    ll=(dx*dx+dy*dy)**.5
    nx=-dy/ll; ny=dx/ll
    lay=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(lay)
    for i in range(count+1):
        t=i/count
        x=x1+dx*t+rnd.uniform(-1.2,1.2)
        y=y1+dy*t+rnd.uniform(-1.2,1.2)
        ln=5+rnd.uniform(-1.2,1.2)
        d.line((x-nx*ln,y-ny*ln,x+nx*ln,y+ny*ln),
               fill=(195,201,195,95), width=2)
    im.alpha_composite(lay)

def _folds(im, mask, view, seed):
    rnd = random.Random(seed)
    lay = Image.new("RGBA",(S,S),(0,0,0,0))
    d = ImageDraw.Draw(lay)
    for i in range(44):
        if view == "east":
            x=rnd.randint(520,970); y=rnd.randint(430,1180)
        else:
            x=rnd.randint(470,1060); y=rnd.randint(420,1190)
        ln=rnd.randint(55,170)
        bend=rnd.randint(-20,20)
        if i%2:
            col=(18,24,26,rnd.randint(13,28))
        else:
            col=(238,239,227,rnd.randint(9,20))
        d.line([(x,y),(x+bend,y+ln//2),(x+bend//2,y+ln)],
               fill=col,width=rnd.randint(3,8))
    lay=lay.filter(ImageFilter.GaussianBlur(5.5))
    lay.putalpha(ImageChops.multiply(lay.getchannel("A"),mask))
    im.alpha_composite(lay)

def _wear(im, mask, seed):
    rng=np.random.default_rng(seed)
    arr=np.asarray(im).astype(np.float32)
    m=np.asarray(mask)>30
    yy,xx=np.mgrid[0:S,0:S]
    for _ in range(90):
        x=int(rng.integers(410,1125)); y=int(rng.integers(290,1300))
        if not (0<=x<S and 0<=y<S and m[y,x]):
            continue
        rx=float(rng.integers(10,42)); ry=float(rng.integers(18,72))
        g=np.exp(-(((xx-x)/rx)**2+((yy-y)/ry)**2)/2)
        amt=float(rng.uniform(-8,8))
        arr[...,:3]=np.clip(arr[...,:3]+g[...,None]*amt*m[...,None],0,255)
    return Image.fromarray(arr.astype(np.uint8),"RGBA")

def _ancient_inlay(im, pts, width=6, alpha=145, glow=0):
    if glow:
        gl=Image.new("RGBA",(S,S),(0,0,0,0))
        ImageDraw.Draw(gl).line(pts,fill=(58,158,196,min(80,alpha//2)),width=width+glow,joint="curve")
        im.alpha_composite(gl.filter(ImageFilter.GaussianBlur(glow/2)))
    _stroke(im,pts,(72,137,157),width,alpha,.4)
    _stroke(im,pts,(180,214,218),max(1,width//3),min(120,alpha),.25)

def _render(view: str) -> Image.Image:
    seed=SEEDS[view]
    mask=_garment_mask(view)
    im=_base_cloth(view,mask,seed)

    graphite=(31,38,41)
    silver=(108,120,121)
    palecool=(176,184,181)

    if view=="south":
        # Tall split collar and dark underweave.
        _paint_region(im,[(622,295),(700,240),(768,230),(836,240),(914,295),(884,410),(768,466),(652,410)],
                      graphite,236,1,(149,157,154),160)
        _paint_region(im,[(686,472),(768,506),(850,472),(878,740),(846,1032),(768,1144),(690,1032),(658,740)],
                      graphite,198,2,(111,124,124),118)

        # Soft Ancient shoulder mantle, not armour plates.
        _paint_region(im,[(448,420),(552,330),(676,300),(768,322),(860,300),(984,330),(1088,420),(1016,520),(858,500),(768,520),(678,500),(520,520)],
                      palecool,122,3,(182,188,183),85)

        # Tailored side cloth with a slight robe fall.
        _paint_region(im,[(438,520),(558,470),(644,555),(650,1040),(566,1180),(468,1090)],
                      (82,94,96),145,4,(124,137,136),80)
        _paint_region(im,[(1098,520),(978,470),(892,555),(886,1040),(970,1180),(1068,1090)],
                      (82,94,96),145,5,(124,137,136),80)

        # Lantean seam rhythm.
        _stroke(im,[(486,616),(610,684),(684,670)],silver,5,118,.8)
        _stroke(im,[(1050,616),(926,684),(852,670)],silver,5,118,.8)
        _stroke(im,[(520,842),(650,902),(704,886)],silver,4,105,.8)
        _stroke(im,[(1016,842),(886,902),(832,886)],silver,4,105,.8)
        _stroke(im,[(566,1042),(690,1100),(768,1088),(846,1100),(970,1042)],silver,5,125,.8)

        # Restrained blue conductor embroidery – Ancient tech accent, not neon piping.
        _ancient_inlay(im,[(768,505),(768,1088)],6,138,4)
        _ancient_inlay(im,[(606,606),(690,646),(768,624),(846,646),(930,606)],5,108,2)
        _ancient_inlay(im,[(620,928),(700,960),(768,948),(836,960),(916,928)],4,92,1)

        _stitches(im,(651,410),(690,1034),30,21)
        _stitches(im,(885,410),(846,1034),30,22)

        # Small Ancient diamond clasp.
        lay=Image.new("RGBA",(S,S),(0,0,0,0)); d=ImageDraw.Draw(lay)
        q=[(768,566),(792,594),(768,624),(744,594)]
        d.polygon(q,fill=(72,86,88,220),outline=(176,190,188,190))
        d.polygon([(768,578),(782,594),(768,610),(754,594)],fill=(79,144,161,155))
        im.alpha_composite(lay)

    elif view=="north":
        _paint_region(im,[(620,300),(700,245),(768,234),(836,245),(916,300),(886,405),(768,456),(650,405)],
                      graphite,232,31,(147,155,152),155)

        # Rear yoke and long central spine seam.
        _paint_region(im,[(596,470),(768,514),(940,470),(920,690),(842,1028),(768,1136),(694,1028),(616,690)],
                      (47,56,59),190,32,(116,130,130),115)

        _paint_region(im,[(436,524),(560,474),(636,554),(648,1042),(560,1176),(466,1084)],
                      (80,92,94),140,33,(122,134,134),78)
        _paint_region(im,[(1100,524),(976,474),(900,554),(888,1042),(976,1176),(1070,1084)],
                      (80,92,94),140,34,(122,134,134),78)

        _stroke(im,[(510,664),(628,716),(674,702)],silver,5,112,.8)
        _stroke(im,[(1026,664),(908,716),(862,702)],silver,5,112,.8)
        _stroke(im,[(560,1040),(690,1098),(768,1086),(846,1098),(976,1040)],silver,5,120,.8)

        _ancient_inlay(im,[(768,460),(768,1122)],6,130,4)
        _ancient_inlay(im,[(604,610),(690,650),(768,630),(846,650),(932,610)],5,102,2)
        _stitches(im,(650,407),(694,1036),30,41)
        _stitches(im,(886,407),(842,1036),30,42)

    else:
        _paint_region(im,[(610,306),(736,248),(858,266),(960,330),(994,406),(936,472),(684,448)],
                      graphite,232,61,(145,154,152),150)
        _paint_region(im,[(676,466),(802,498),(918,452),(956,736),(926,1030),(818,1148),(704,1068),(648,758)],
                      (48,57,60),192,62,(116,130,130),112)
        _paint_region(im,[(458,542),(558,498),(642,566),(662,1048),(578,1174),(490,1080)],
                      (80,92,94),142,63,(121,134,133),80)

        _stroke(im,[(620,518),(686,592),(650,760),(706,1066)],silver,5,112,.8)
        _stroke(im,[(918,466),(954,628),(950,838),(910,1052)],silver,5,110,.8)
        _ancient_inlay(im,[(686,696),(804,744),(914,696)],5,100,2)
        _ancient_inlay(im,[(704,926),(818,968),(910,934)],4,88,1)
        _stitches(im,(686,446),(704,1068),30,68)

    _folds(im,mask,view,seed+70)
    im=_wear(im,mask,seed+400)

    # Soft worn edge only; no bright perimeter.
    inner=mask.filter(ImageFilter.GaussianBlur(16))
    edge=ImageChops.subtract(mask,inner)
    sh=Image.new("RGBA",(S,S),(15,19,20,0))
    sh.putalpha(edge.point(lambda p:min(62,int(p*.25))))
    im.alpha_composite(sh)

    im=ImageEnhance.Contrast(im).enhance(1.045)
    im.putalpha(ImageChops.multiply(im.getchannel("A"),mask))
    return im

for view in ("south","north","east"):
    out=_render(view)
    p=OUT/f"master_{view}.png"
    out.save(p,optimize=True,compress_level=9)
    print("painted",p,out.size)
