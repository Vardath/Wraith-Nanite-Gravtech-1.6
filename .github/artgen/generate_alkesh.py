from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import random

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Textures" / "Things" / "Building" / "Goauld" / "Shuttle"
OUT.mkdir(parents=True, exist_ok=True)

S = 2048
FINAL = 512
C = S // 2
RNG = random.Random(137)

BLACK = (5, 6, 7, 255)
EDGE = (18, 14, 10, 255)
DEEP = (27, 24, 21, 255)
METAL_D = (42, 42, 40, 255)
METAL = (70, 68, 63, 255)
METAL_H = (116, 106, 88, 255)
BRONZE_D = (73, 45, 24, 255)
BRONZE = (126, 80, 38, 255)
BRONZE_H = (184, 127, 61, 255)
GOLD = (222, 166, 80, 255)
LIGHT = (246, 220, 164, 255)
AMBER = (255, 137, 27, 255)
AMBER_H = (255, 230, 145, 255)


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def mask_poly(points, blur=0):
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).polygon(points, fill=255)
    if blur:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    return m


def vertical_gradient(mask, top, bottom):
    grad = Image.new("RGBA", (1, S))
    gp = grad.load()
    for y in range(S):
        t = y / (S - 1)
        gp[0, y] = tuple(round(top[i] * (1 - t) + bottom[i] * t) for i in range(4))
    grad = grad.resize((S, S))
    out = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    out.paste(grad, (0, 0), mask)
    return out


def bevel(base, points, top, bottom, edge=EDGE, outer=18, inner=5):
    m = mask_poly(points)
    base.alpha_composite(vertical_gradient(m, top, bottom))
    d = ImageDraw.Draw(base)
    d.line(points + [points[0]], fill=edge, width=outer, joint="curve")
    d.line(points + [points[0]], fill=BRONZE_H, width=max(4, outer // 3), joint="curve")
    # small upper-left highlight, kept restrained so the ship still reads as dark Goa'uld metal
    upper = points[: max(3, len(points)//2)]
    if len(upper) > 1:
        d.line(upper, fill=LIGHT, width=inner, joint="curve")


def glow_ellipse(base, box, color=AMBER, blur=26, alpha=150):
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).ellipse(box, fill=alpha)
    m = m.filter(ImageFilter.GaussianBlur(blur))
    g = Image.new("RGBA", (S, S), (*color[:3], 0))
    g.putalpha(m)
    base.alpha_composite(g)


def metal_grain(base, hullmask, count=1500):
    layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        x = RNG.randrange(520, 1528)
        y = RNG.randrange(120, 1930)
        if hullmask.getpixel((x, y)):
            a = RNG.randrange(8, 25)
            warm = RNG.randrange(74, 160)
            length = RNG.randrange(3, 18)
            d.line((x, y, x + length, y + RNG.choice((-1, 0, 1))), fill=(warm, max(30, warm-25), max(18, warm-55), a), width=1)
    base.alpha_composite(layer)


def inset_panel(d, pts, fill=(38, 35, 31, 240), line=(135, 88, 43, 220), width=6):
    d.polygon(pts, fill=fill)
    d.line(pts + [pts[0]], fill=EDGE, width=width+5, joint="curve")
    d.line(pts + [pts[0]], fill=line, width=width, joint="curve")


def engine(base, cx, cy, w=118, h=150):
    d = ImageDraw.Draw(base)
    box = (cx-w//2, cy-h//2, cx+w//2, cy+h//2)
    d.rounded_rectangle(box, radius=26, fill=(18,18,17,255), outline=EDGE, width=14)
    d.rounded_rectangle((box[0]+12,box[1]+12,box[2]-12,box[3]-12), radius=18, fill=(49,37,26,255), outline=BRONZE_H, width=7)
    for yy in (cy-40, cy, cy+40):
        d.line((cx-w//2+18, yy, cx+w//2-18, yy), fill=(115,76,38,235), width=5)
    glow_ellipse(base, (cx-31, cy+38, cx+31, cy+112), blur=24, alpha=145)
    d = ImageDraw.Draw(base)
    d.ellipse((cx-27,cy+43,cx+27,cy+97), fill=(74,42,18,255), outline=GOLD, width=6)
    d.ellipse((cx-16,cy+54,cx+16,cy+86), fill=AMBER, outline=AMBER_H, width=4)


def make_north():
    im = canvas()

    # Screen-accurate design intent: a long, thick pyramid-bodied bomber/transport.
    # NO airplane wings, NO Death-Glider crescent, NO detached lateral fins.
    # The outer hull is one continuous stretched pyramid with maximum width around the engine/transport mass.
    hull = [
        (C, 108),
        (C+86, 210), (C+170, 342), (C+255, 520),
        (C+344, 760), (C+420, 1040), (C+438, 1268),
        (C+398, 1475), (C+315, 1662), (C+190, 1810),
        (C+72, 1912), (C, 1942),
        (C-72, 1912), (C-190, 1810), (C-315, 1662),
        (C-398, 1475), (C-438, 1268), (C-420, 1040),
        (C-344, 760), (C-255, 520), (C-170, 342), (C-86, 210)
    ]

    # depth shadow underneath the single hull body
    sm = mask_poly([(x+22,y+28) for x,y in hull], 24)
    sh = Image.new("RGBA", (S,S), (0,0,0,0))
    sh.putalpha(sm.point(lambda p: int(p * .43)))
    im.alpha_composite(sh)

    bevel(im, hull, top=(102,85,66,255), bottom=(33,31,29,255), outer=22, inner=6)
    hm = mask_poly(hull)
    metal_grain(im, hm)
    d = ImageDraw.Draw(im)

    # Continuous faceted outer hull planes: these remain INSIDE the outer silhouette.
    left_plane = [(C,144),(C-82,234),(C-208,454),(C-350,815),(C-400,1140),(C-356,1430),(C-240,1674),(C,1878)]
    right_plane = [(2*C-x,y) for x,y in left_plane]
    d.polygon(left_plane, fill=(58,52,45,220))
    d.polygon(right_plane, fill=(82,67,49,225))
    d.line(left_plane, fill=(25,23,21,230), width=8, joint="curve")
    d.line(right_plane, fill=(150,106,56,190), width=7, joint="curve")

    # Raised central pyramid / command spine — the defining Al'kesh feature.
    pyramid = [
        (C,170), (C+118,350), (C+188,620), (C+202,1030),
        (C+168,1370), (C+104,1640), (C,1848),
        (C-104,1640), (C-168,1370), (C-202,1030),
        (C-188,620), (C-118,350)
    ]
    bevel(im, pyramid, top=(146,112,71,255), bottom=(54,45,36,255), edge=(24,20,16,255), outer=18, inner=5)
    d = ImageDraw.Draw(im)

    # Pyramid facets create real height, not a flat emblem.
    left_facet=[(C,194),(C,1810),(C-90,1618),(C-148,1350),(C-174,1020),(C-160,638),(C-100,372)]
    right_facet=[(C,194),(C+100,372),(C+160,638),(C+174,1020),(C+148,1350),(C+90,1618),(C,1810)]
    d.polygon(left_facet, fill=(72,58,45,218))
    d.polygon(right_facet, fill=(111,82,51,218))
    d.line((C,210,C,1802), fill=LIGHT, width=6)

    # Bridge cap / forward command block integrated into the pyramid.
    bridge=[(C,278),(C+82,398),(C+96,594),(C+58,708),(C,760),(C-58,708),(C-96,594),(C-82,398)]
    bevel(im, bridge, top=(172,132,78,255), bottom=(62,48,35,255), edge=(31,24,17,255), outer=12, inner=4)
    d = ImageDraw.Draw(im)
    d.polygon([(C,430),(C+54,482),(C,540),(C-54,482)], fill=(13,14,14,255), outline=GOLD)
    d.ellipse((C-15,467,C+15,497), fill=(49,38,27,255), outline=LIGHT, width=4)

    # Layered Goa'uld hull terraces follow the stretched pyramid. No wing edges exist.
    terraces = [
        (390, [(C-112,590),(C-248,690),(C-314,878),(C-322,1044)], [(C+112,590),(C+248,690),(C+314,878),(C+322,1044)]),
        (760, [(C-154,900),(C-316,1010),(C-356,1210),(C-332,1395)], [(C+154,900),(C+316,1010),(C+356,1210),(C+332,1395)]),
        (1160,[(C-142,1290),(C-278,1405),(C-300,1530),(C-224,1660)],[(C+142,1290),(C+278,1405),(C+300,1530),(C+224,1660)])
    ]
    for _,left,right in terraces:
        d.line(left, fill=EDGE, width=18, joint="curve"); d.line(left, fill=BRONZE_H, width=6, joint="curve")
        d.line(right, fill=EDGE, width=18, joint="curve"); d.line(right, fill=BRONZE_H, width=6, joint="curve")

    # Inset armour / service panels inside the body, kept narrow and architectural.
    for sx in (-1,1):
        pts=[(C+sx*215,720),(C+sx*294,812),(C+sx*314,980),(C+sx*276,1115),(C+sx*188,1045),(C+sx*172,858)]
        inset_panel(d, pts, fill=(35,34,32,235), line=(120,78,39,220), width=5)
        pts2=[(C+sx*205,1210),(C+sx*286,1290),(C+sx*282,1450),(C+sx*210,1570),(C+sx*154,1470),(C+sx*158,1324)]
        inset_panel(d, pts2, fill=(31,31,30,235), line=(111,72,36,215), width=5)

    # Twin staff-cannon apertures near the forward belly, integrated into the hull rather than protruding guns.
    for sx in (-1,1):
        cx=C+sx*122; cy=805
        d.ellipse((cx-31,cy-31,cx+31,cy+31), fill=(11,12,12,255), outline=BRONZE_H, width=7)
        d.ellipse((cx-13,cy-13,cx+13,cy+13), fill=(64,39,19,255), outline=GOLD, width=4)

    # Four engines recessed into the broad aft transport/engine section.
    aft=[(C-322,1438),(C+322,1438),(C+250,1760),(C+94,1880),(C-94,1880),(C-250,1760)]
    d.polygon(aft, fill=(26,25,24,235), outline=(118,78,39,235))
    d.line(aft+[aft[0]], fill=EDGE, width=10, joint="curve")
    for cx,cy in ((C-205,1588),(C-72,1650),(C+72,1650),(C+205,1588)):
        engine(im,cx,cy,92,124)

    # Goa'uld luminous conduits: restrained, structural, no neon toy look.
    d = ImageDraw.Draw(im)
    for sx in (-1,1):
        path=[(C+sx*88,505),(C+sx*138,680),(C+sx*154,1010),(C+sx*126,1284),(C+sx*96,1438)]
        glow_layer=Image.new("RGBA",(S,S),(0,0,0,0)); gd=ImageDraw.Draw(glow_layer)
        gd.line(path, fill=(*AMBER[:3],95), width=22, joint="curve")
        glow_layer=glow_layer.filter(ImageFilter.GaussianBlur(18)); im.alpha_composite(glow_layer)
        d=ImageDraw.Draw(im); d.line(path, fill=(170,101,39,240), width=8, joint="curve"); d.line(path, fill=(237,175,82,220), width=3, joint="curve")

    # Fine plate seams and fasteners add production-art detail without changing silhouette.
    seam_sets=[
        [(C-72,314),(C-138,454),(C-180,648)],[(C+72,314),(C+138,454),(C+180,648)],
        [(C-240,1110),(C-290,1240),(C-286,1378)],[(C+240,1110),(C+290,1240),(C+286,1378)],
        [(C-176,1500),(C-128,1740)],[(C+176,1500),(C+128,1740)]
    ]
    for s in seam_sets:
        d.line(s, fill=(16,16,15,220), width=7, joint="curve")
        d.line([(x,y-3) for x,y in s], fill=(123,89,49,160), width=2, joint="curve")
    for x,y in [(C,912),(C-196,958),(C+196,958),(C-254,1270),(C+254,1270),(C,1376)]:
        d.ellipse((x-7,y-7,x+7,y+7), fill=(17,16,14,255), outline=LIGHT, width=2)

    # Crop to an accurate long-hull planform (the RimWorld 5x7 long footprint), then centre on a square transparent texture.
    box = im.getchannel('A').getbbox()
    if not box:
        raise RuntimeError('Al\'kesh render has empty alpha')
    crop = im.crop(box)
    target_w, target_h = 336, 470
    scale = min(target_w/crop.width, target_h/crop.height)
    crop = crop.resize((round(crop.width*scale), round(crop.height*scale)), Image.Resampling.LANCZOS)
    out = Image.new('RGBA',(FINAL,FINAL),(0,0,0,0))
    out.alpha_composite(crop,((FINAL-crop.width)//2,(FINAL-crop.height)//2))
    return out


def validate(path):
    with Image.open(path) as chk:
        chk.load()
        if chk.mode != 'RGBA' or chk.size != (512,512):
            raise RuntimeError(f'{path}: expected 512x512 RGBA')
        a=chk.getchannel('A')
        bbox=a.getbbox()
        if not bbox:
            raise RuntimeError(f'{path}: empty alpha')
        edges=[a.crop((0,0,512,1)).getextrema()[1],a.crop((0,511,512,512)).getextrema()[1],a.crop((0,0,1,512)).getextrema()[1],a.crop((511,0,512,512)).getextrema()[1]]
        if any(edges):
            raise RuntimeError(f'{path}: alpha touches edge')
        w,h=bbox[2]-bbox[0],bbox[3]-bbox[1]
        # north/south should be a long pyramid body; east/west naturally reverse the bbox ratio.
        if path.name.endswith(('_north.png','Transport.png','_south.png')) and not (0.66 <= w/h <= 0.76):
            raise RuntimeError(f'{path}: north/south hull ratio {w/h:.3f} is not Al\'kesh-like')
        if path.name.endswith(('_east.png','_west.png')) and not (1.31 <= w/h <= 1.52):
            raise RuntimeError(f'{path}: east/west hull ratio {w/h:.3f} is not rotated Al\'kesh-like')


north=make_north()
views={
    '': north,
    '_north': north,
    '_east': north.transpose(Image.Transpose.ROTATE_270),
    '_south': north.transpose(Image.Transpose.ROTATE_180),
    '_west': north.transpose(Image.Transpose.ROTATE_90),
}
for suffix,image in views.items():
    p=OUT/f'WNG_AlkeshTransport{suffix}.png'
    image.save(p,'PNG',optimize=True)
    validate(p)

print("Generated corrected Al'kesh family: long continuous pyramid hull, no wings, raised command pyramid, integrated twin staff apertures, four recessed aft engines; base+north/east/south/west.")
