from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Textures" / "Things" / "Building" / "Goauld" / "Shuttle"
OUT.mkdir(parents=True, exist_ok=True)

S = 1536
FINAL = 512

EDGE = (18, 14, 10, 255)
DEEP = (39, 29, 20, 255)
BRONZE = (86, 61, 34, 255)
MID = (124, 89, 48, 255)
GOLD = (178, 132, 70, 255)
LIGHT = (226, 190, 122, 255)
ENGINE = (255, 151, 36, 255)
ENGINE_HI = (255, 226, 138, 255)


def layer():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def glow(base, shape, blur=26, alpha=130):
    mask = Image.new("L", base.size, 0)
    md = ImageDraw.Draw(mask)
    shape(md)
    mask = mask.filter(ImageFilter.GaussianBlur(blur))
    g = Image.new("RGBA", base.size, (*ENGINE[:3], 0))
    g.putalpha(mask.point(lambda p: int(p * alpha / 255)))
    base.alpha_composite(g)


def polygon_shaded(draw, pts, fill, outline=EDGE, width=10):
    draw.polygon(pts, fill=fill)
    draw.line(pts + [pts[0]], fill=outline, width=width, joint="curve")


def panel_lines(draw, pts, count=4):
    # restrained Goa'uld panel segmentation following the wing chord
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    x0, x1 = min(xs), max(xs)
    y0, y1 = min(ys), max(ys)
    for i in range(1, count + 1):
        t = i / (count + 1)
        x = int(x0 * (1 - t) + x1 * t)
        draw.line((x, int(y0 + 0.18*(y1-y0)), x, int(y1 - 0.13*(y1-y0))), fill=(61, 43, 25, 210), width=5)


def engine(base, center, size=(78, 126)):
    x, y = center
    w, h = size
    d = ImageDraw.Draw(base)
    d.rounded_rectangle((x-w//2, y-h//2, x+w//2, y+h//2), radius=w//3, fill=DEEP, outline=GOLD, width=8)
    d.rounded_rectangle((x-w//2+10, y-h//2+12, x+w//2-10, y+h//2-12), radius=w//4, fill=(55,42,28,255), outline=MID, width=5)
    for off in (-h*0.22, 0, h*0.22):
        yy = int(y+off)
        d.line((x-w//2+8, yy, x+w//2-8, yy), fill=(155,112,59,235), width=4)
    glow(base, lambda md: md.ellipse((x-24, y+h//2-28, x+24, y+h//2+18), fill=220), blur=18, alpha=135)
    d = ImageDraw.Draw(base)
    d.ellipse((x-20, y+h//2-25, x+20, y+h//2+15), fill=(89,48,19,255), outline=GOLD, width=4)
    d.ellipse((x-11, y+h//2-17, x+11, y+h//2+7), fill=ENGINE, outline=ENGINE_HI, width=3)


def make_north():
    im = layer()
    d = ImageDraw.Draw(im)

    # Screen-accurate massing: broad swept wings around a large raised central body.
    # Nose is north/up; four engine nacelles sit aft/south.
    left_wing = [
        (706, 470), (575, 430), (420, 454), (264, 542), (148, 666),
        (94, 790), (170, 884), (318, 846), (474, 770), (620, 690), (724, 640)
    ]
    right_wing = [(S-x, y) for x, y in left_wing]

    # Deep underside mass first so the craft never reads as a paper-thin flying wing.
    under = [(768, 274), (875, 356), (946, 510), (972, 750), (920, 1004),
             (842, 1174), (768, 1260), (694, 1174), (616, 1004), (564, 750),
             (590, 510), (661, 356)]
    polygon_shaded(d, under, (46,34,24,255), outline=(10,8,7,255), width=14)

    for pts in (left_wing, right_wing):
        polygon_shaded(d, pts, BRONZE, outline=EDGE, width=14)
        inner = []
        for x, y in pts:
            inner.append((int(x + (768-x)*0.11), int(y + (760-y)*0.07)))
        d.line(inner + [inner[0]], fill=GOLD, width=7, joint="curve")
        panel_lines(d, pts, 4)

    # Raised center hull and pyramid/command section.
    center_hull = [(768, 238), (866, 340), (914, 518), (900, 814),
                   (852, 1034), (768, 1192), (684, 1034), (636, 814),
                   (622, 518), (670, 340)]
    polygon_shaded(d, center_hull, (72,52,31,255), outline=EDGE, width=14)
    d.line(center_hull[:5], fill=LIGHT, width=5, joint="curve")

    pyramid = [(768, 338), (858, 520), (822, 806), (768, 914), (714, 806), (678, 520)]
    polygon_shaded(d, pyramid, MID, outline=(53,38,23,255), width=10)
    left_face = [(768,338),(768,914),(714,806),(678,520)]
    right_face = [(768,338),(858,520),(822,806),(768,914)]
    d.polygon(left_face, fill=(110,78,43,255))
    d.polygon(right_face, fill=(145,105,58,255))
    d.line(pyramid + [pyramid[0]], fill=GOLD, width=8, joint="curve")
    d.line((768,348,768,905), fill=LIGHT, width=5)

    # Goa'uld eye motif, small and structural rather than the whole center body.
    eye = [(724, 606), (768, 578), (812, 606), (768, 635)]
    d.polygon(eye, fill=(25,20,15,255), outline=GOLD)
    d.ellipse((754,592,782,620), fill=DEEP, outline=LIGHT, width=3)

    # Four aft engine nacelles, two per side; visibly part of the rear mass.
    for x, y in ((640, 925), (694, 1010), (842, 1010), (896, 925)):
        engine(im, (x, y), (70, 118))

    d = ImageDraw.Draw(im)
    # central keel and nose/cockpit taper
    polygon_shaded(d, [(768,238),(804,314),(792,480),(768,548),(744,480),(732,314)], (128,91,48,255), outline=GOLD, width=6)
    d.line((768,248,768,528), fill=LIGHT, width=4)

    # wing-root shoulders emphasize volume from side/east-west views
    for sx in (-1,1):
        pts=[(768+sx*82,470),(768+sx*210,540),(768+sx*244,664),(768+sx*176,742),(768+sx*92,682)]
        d.polygon(pts, fill=(92,65,37,255), outline=MID)
        d.line(pts, fill=GOLD, width=5, joint="curve")

    # crop to a deliberate 8.8:6.4-ish silhouette instead of filling a square.
    box = im.getchannel("A").getbbox()
    crop = im.crop(box)
    target_w, target_h = 448, 326
    scale = min(target_w / crop.width, target_h / crop.height)
    crop = crop.resize((round(crop.width*scale), round(crop.height*scale)), Image.Resampling.LANCZOS)
    out = Image.new("RGBA", (FINAL, FINAL), (0,0,0,0))
    out.alpha_composite(crop, ((FINAL-crop.width)//2, (FINAL-crop.height)//2))
    return out


def validate(path):
    with Image.open(path) as im:
        im.load()
        if im.size != (512,512):
            raise RuntimeError(f"{path}: expected 512x512")
        rgba = im.convert("RGBA")
        alpha = rgba.getchannel("A")
        box = alpha.getbbox()
        if not box:
            raise RuntimeError(f"{path}: empty alpha")
        edges = [
            alpha.crop((0,0,512,1)).getextrema()[1],
            alpha.crop((0,511,512,512)).getextrema()[1],
            alpha.crop((0,0,1,512)).getextrema()[1],
            alpha.crop((511,0,512,512)).getextrema()[1],
        ]
        if any(edges):
            raise RuntimeError(f"{path}: alpha touches canvas edge")


north = make_north()
# Base defaults to north, matching the landed shuttle's default placing rotation.
views = {
    "": north,
    "_north": north,
    "_east": north.transpose(Image.Transpose.ROTATE_270),
    "_south": north.transpose(Image.Transpose.ROTATE_180),
    "_west": north.transpose(Image.Transpose.ROTATE_90),
}

for suffix, image in views.items():
    p = OUT / f"WNG_AlkeshTransport{suffix}.png"
    image.save(p, "PNG", optimize=True)
    validate(p)

print("Generated corrected Al'kesh family: 512 RGBA base+north/east/south/west, broad raised center hull, swept wings and four aft nacelles, proportioned for 8x6 / drawSize 8.8x6.4.")
