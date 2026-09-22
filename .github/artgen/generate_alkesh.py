from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageChops
import random

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Textures" / "Things" / "Building" / "Goauld" / "Shuttle"
OUT.mkdir(parents=True, exist_ok=True)

S = 1536
FINAL = 512
RNG = random.Random(137)

EDGE = (12, 10, 8, 255)
DEEP = (31, 24, 18, 255)
BRONZE_D = (66, 48, 30, 255)
BRONZE = (104, 76, 43, 255)
BRONZE_H = (151, 112, 62, 255)
GOLD = (196, 149, 78, 255)
LIGHT = (233, 204, 143, 255)
AMBER = (255, 156, 35, 255)
AMBER_H = (255, 227, 147, 255)


def canvas():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def poly_mask(points, blur=0):
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    d.polygon(points, fill=255)
    if blur:
        m = m.filter(ImageFilter.GaussianBlur(blur))
    return m


def gradient_fill(base, points, top, bottom):
    mask = poly_mask(points)
    grad = Image.new("RGBA", (S, S))
    px = grad.load()
    for y in range(S):
        t = y / (S - 1)
        col = tuple(round(top[i] * (1 - t) + bottom[i] * t) for i in range(4))
        for x in range(S):
            px[x, y] = col
    base.alpha_composite(Image.composite(grad, Image.new("RGBA", (S, S)), mask))


def outline(base, points, color=EDGE, width=12):
    ImageDraw.Draw(base).line(points + [points[0]], fill=color, width=width, joint="curve")


def glow(base, xy, blur=28, alpha=145):
    x0, y0, x1, y1 = xy
    m = Image.new("L", (S, S), 0)
    ImageDraw.Draw(m).ellipse(xy, fill=alpha)
    m = m.filter(ImageFilter.GaussianBlur(blur))
    g = Image.new("RGBA", (S, S), (*AMBER[:3], 0))
    g.putalpha(m)
    base.alpha_composite(g)


def bevel_poly(base, points, top=BRONZE_H, bottom=BRONZE_D, edge=EDGE, width=10):
    gradient_fill(base, points, top, bottom)
    d = ImageDraw.Draw(base)
    d.line(points[: max(2, len(points)//2 + 1)], fill=LIGHT, width=max(3, width//3), joint="curve")
    outline(base, points, edge, width)


def metal_grain(base, mask, amount=420):
    grain = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grain)
    for _ in range(amount):
        x = RNG.randrange(180, S-180)
        y = RNG.randrange(180, S-180)
        if mask.getpixel((x, y)):
            a = RNG.randrange(8, 22)
            c = RNG.randrange(120, 205)
            gd.line((x, y, x + RNG.randrange(2, 12), y), fill=(c, c-18, c-45, a), width=1)
    base.alpha_composite(grain)


def engine(base, cx, cy, scale=1.0):
    d = ImageDraw.Draw(base)
    w, h = int(78*scale), int(118*scale)
    x0, y0, x1, y1 = cx-w//2, cy-h//2, cx+w//2, cy+h//2
    d.rounded_rectangle((x0, y0, x1, y1), radius=max(12, w//4), fill=(26,21,17,255), outline=GOLD, width=7)
    d.rounded_rectangle((x0+10, y0+10, x1-10, y1-12), radius=max(8, w//5), fill=(60,42,24,255), outline=BRONZE_H, width=4)
    for yy in (y0+28, cy, y1-30):
        d.line((x0+12, yy, x1-12, yy), fill=(117,82,43,230), width=4)
    glow(base, (cx-20, y1-24, cx+20, y1+18), blur=18, alpha=120)
    d = ImageDraw.Draw(base)
    d.ellipse((cx-18, y1-22, cx+18, y1+14), fill=(89,47,17,255), outline=GOLD, width=4)
    d.ellipse((cx-10, y1-14, cx+10, y1+6), fill=AMBER, outline=AMBER_H, width=3)


def make_north():
    im = canvas()

    # The Al'kesh is one continuous, bulky pyramid-based craft. There are no separate airplane-like wings.
    hull = [
        (768, 154),
        (860, 230), (950, 342),
        (1080, 430), (1222, 556), (1322, 710),
        (1288, 842), (1186, 946), (1052, 1038),
        (934, 1148), (850, 1260), (768, 1368),
        (686, 1260), (602, 1148), (484, 1038),
        (350, 946), (248, 842), (214, 710),
        (314, 556), (456, 430), (586, 342), (676, 230)
    ]

    # soft ground/depth shadow makes the hull read as a heavy ship instead of a flat emblem
    sm = poly_mask([(x+18, y+28) for x, y in hull], 22)
    shadow = Image.new("RGBA", (S,S), (0,0,0,0)); shadow.putalpha(sm.point(lambda p: int(p*.42)))
    im.alpha_composite(shadow)

    bevel_poly(im, hull, top=(132,94,50,255), bottom=(48,35,24,255), width=16)
    hm = poly_mask(hull)
    metal_grain(im, hm, 750)
    d = ImageDraw.Draw(im)

    # Integrated curved side masses. These are hull shoulders, not detached wings.
    left_side = [(676,318),(564,376),(430,478),(330,608),(284,724),(326,822),(444,870),(566,810),(654,700),(704,548)]
    right_side = [(S-x,y) for x,y in left_side]
    for pts in (left_side, right_side):
        bevel_poly(im, pts, top=(112,80,44,255), bottom=(54,39,26,255), edge=(35,27,19,255), width=9)
        d = ImageDraw.Draw(im)
        inner=[(int(x+(768-x)*.13), int(y+(740-y)*.09)) for x,y in pts]
        d.line(inner, fill=(181,132,68,210), width=5, joint="curve")

    # Raised pyramid/bridge dominates the craft, as it should.
    pyramid = [(768,196),(896,366),(984,594),(1002,824),(920,1040),(768,1224),(616,1040),(534,824),(552,594),(640,366)]
    bevel_poly(im, pyramid, top=(154,112,61,255), bottom=(61,44,29,255), width=13)
    d = ImageDraw.Draw(im)

    left_face=[(768,216),(768,1188),(632,1018),(560,814),(578,602),(652,382)]
    right_face=[(768,216),(884,382),(958,602),(976,814),(904,1018),(768,1188)]
    d.polygon(left_face, fill=(95,67,39,235))
    d.polygon(right_face, fill=(132,94,49,235))
    d.line(left_face+[left_face[0]], fill=(72,50,31,210), width=5, joint="curve")
    d.line(right_face+[right_face[0]], fill=(184,136,71,210), width=5, joint="curve")
    d.line((768,220,768,1180), fill=LIGHT, width=5)

    # Prominent forward bridge/nose cap rather than a fighter nose.
    bridge=[(768,238),(836,342),(842,486),(806,564),(768,604),(730,564),(694,486),(700,342)]
    bevel_poly(im, bridge, top=(177,132,73,255), bottom=(76,53,31,255), edge=(54,39,25,255), width=8)
    d = ImageDraw.Draw(im)
    d.polygon([(726,432),(768,398),(810,432),(768,470)], fill=(18,17,15,255), outline=GOLD)
    d.ellipse((756,414,780,438), fill=(52,35,22,255), outline=LIGHT, width=3)

    # Inset hull panels and Goa'uld ribs; curves follow the body instead of looking like aircraft spars.
    ribs = [
        [(350,682),(468,620),(590,578)],[(326,756),(456,736),(584,682)],[(372,830),(490,834),(598,772)],
        [(1186,682),(1068,620),(946,578)],[(1210,756),(1080,736),(952,682)],[(1164,830),(1046,834),(938,772)],
        [(668,912),(710,1006),(768,1086)],[(868,912),(826,1006),(768,1086)]
    ]
    for line in ribs:
        d.line(line, fill=(183,130,66,205), width=6, joint="curve")
        d.line([(x,y+5) for x,y in line], fill=(37,28,20,180), width=4, joint="curve")

    # Ventral/weapon housings remain integrated, not dangling guns.
    for cx in (694,842):
        d.rounded_rectangle((cx-35,760,cx+35,842), radius=20, fill=(45,32,22,255), outline=BRONZE_H, width=5)
        d.ellipse((cx-16,786,cx+16,818), fill=(24,20,16,255), outline=GOLD, width=3)

    # Four recessed aft engine nacelles, visibly embedded in rear hull mass.
    rear_plate=[(560,902),(976,902),(930,1130),(768,1244),(606,1130)]
    d.polygon(rear_plate, fill=(49,36,25,230), outline=(116,80,42,240))
    for cx,cy in ((630,1014),(710,1088),(826,1088),(906,1014)):
        engine(im,cx,cy,.92)

    # Fine gold tracery and panel fasteners for production-art finish.
    d = ImageDraw.Draw(im)
    for sx in (-1,1):
        d.line([(768+sx*86,402),(768+sx*148,548),(768+sx*164,744),(768+sx*118,900)], fill=(210,155,78,200), width=4, joint="curve")
        d.line([(768+sx*222,494),(768+sx*310,610),(768+sx*346,738),(768+sx*292,858)], fill=(144,101,54,190), width=4, joint="curve")
    for x,y in ((768,650),(656,606),(880,606),(606,894),(930,894),(768,1010)):
        d.ellipse((x-6,y-6,x+6,y+6), fill=(29,22,16,255), outline=LIGHT, width=2)

    # Scale to the 8x6 footprint while keeping strong margins on a transparent 512 sheet.
    box = im.getchannel("A").getbbox()
    crop = im.crop(box)
    target_w, target_h = 452, 338
    scale = min(target_w/crop.width, target_h/crop.height)
    crop = crop.resize((round(crop.width*scale),round(crop.height*scale)),Image.Resampling.LANCZOS)
    out = Image.new("RGBA",(FINAL,FINAL),(0,0,0,0))
    out.alpha_composite(crop,((FINAL-crop.width)//2,(FINAL-crop.height)//2))
    return out


def validate(path):
    with Image.open(path) as chk:
        chk.load()
        if chk.mode != "RGBA" or chk.size != (512,512):
            raise RuntimeError(f"{path}: expected 512x512 RGBA")
        a=chk.getchannel("A")
        if not a.getbbox():
            raise RuntimeError(f"{path}: empty alpha")
        edges=[a.crop((0,0,512,1)).getextrema()[1],a.crop((0,511,512,512)).getextrema()[1],a.crop((0,0,1,512)).getextrema()[1],a.crop((511,0,512,512)).getextrema()[1]]
        if any(edges):
            raise RuntimeError(f"{path}: alpha touches edge")

north=make_north()
views={
    "":north,
    "_north":north,
    "_east":north.transpose(Image.Transpose.ROTATE_270),
    "_south":north.transpose(Image.Transpose.ROTATE_180),
    "_west":north.transpose(Image.Transpose.ROTATE_90),
}
for suffix,image in views.items():
    p=OUT/f"WNG_AlkeshTransport{suffix}.png"
    image.save(p,"PNG",optimize=True)
    validate(p)

print("Generated Al'kesh production family: integrated heavy Goa'uld hull, dominant raised pyramid/bridge, continuous curved side mass, four recessed aft nacelles; base+north/east/south/west for the 8x6 craft.")
