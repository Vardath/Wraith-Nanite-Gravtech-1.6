from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import math

SIZE = 256
OUT = Path("Textures/Things/Building/Precursor/Furniture")
OUT.mkdir(parents=True, exist_ok=True)

IVORY = (216, 226, 230, 255)
LIGHT = (244, 248, 249, 255)
MID = (154, 174, 184, 255)
DARK = (54, 68, 78, 255)
EDGE = (33, 44, 52, 255)
CYAN = (90, 220, 244, 255)
CYAN_HI = (184, 247, 255, 255)
BLUE = (54, 126, 186, 255)
GOLD = (202, 170, 92, 255)

def canvas():
    return Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))

def glow_layer():
    return Image.new("RGBA", (SIZE, SIZE), (0,0,0,0))

def add_shadow(im, bbox, radius=16, alpha=70, offset=(0,8)):
    sh = Image.new("RGBA", im.size, (0,0,0,0))
    d = ImageDraw.Draw(sh)
    x0,y0,x1,y1=bbox
    dx,dy=offset
    d.rounded_rectangle((x0+dx,y0+dy,x1+dx,y1+dy), radius=radius, fill=(0,0,0,alpha))
    sh=sh.filter(ImageFilter.GaussianBlur(8))
    im.alpha_composite(sh)

def rounded_panel(im, bbox, radius=18, top=LIGHT, bottom=MID, outline=EDGE, width=4):
    x0,y0,x1,y1=bbox
    mask=Image.new("L", im.size, 0)
    md=ImageDraw.Draw(mask)
    md.rounded_rectangle(bbox, radius=radius, fill=255)
    grad=Image.new("RGBA", im.size, (0,0,0,0))
    gd=ImageDraw.Draw(grad)
    h=max(1,y1-y0)
    for y in range(y0,y1+1):
        t=(y-y0)/h
        c=tuple(int(top[i]*(1-t)+bottom[i]*t) for i in range(4))
        gd.line((x0,y,x1,y), fill=c)
    im.alpha_composite(Image.composite(grad, Image.new("RGBA", im.size,(0,0,0,0)), mask))
    d=ImageDraw.Draw(im)
    d.rounded_rectangle(bbox, radius=radius, outline=outline, width=width)

def glow_line(im, xy, width=6, color=CYAN, blur=8):
    g=glow_layer()
    gd=ImageDraw.Draw(g)
    gd.line(xy, fill=(color[0],color[1],color[2],150), width=width*2, joint="curve")
    g=g.filter(ImageFilter.GaussianBlur(blur))
    im.alpha_composite(g)
    d=ImageDraw.Draw(im)
    d.line(xy, fill=color, width=width, joint="curve")
    d.line(xy, fill=CYAN_HI, width=max(1,width//3), joint="curve")

def node(im, center, r=8, color=CYAN):
    x,y=center
    g=glow_layer()
    gd=ImageDraw.Draw(g)
    gd.ellipse((x-r*2,y-r*2,x+r*2,y+r*2), fill=(color[0],color[1],color[2],130))
    g=g.filter(ImageFilter.GaussianBlur(7))
    im.alpha_composite(g)
    d=ImageDraw.Draw(im)
    d.ellipse((x-r,y-r,x+r,y+r), fill=color, outline=EDGE, width=2)
    d.ellipse((x-r//2,y-r//2,x+r//2,y+r//2), fill=CYAN_HI)

def bevel_poly(im, pts, fill=IVORY, edge=EDGE, inner=MID):
    d=ImageDraw.Draw(im)
    d.polygon(pts, fill=fill, outline=edge)
    cx=sum(x for x,y in pts)/len(pts); cy=sum(y for x,y in pts)/len(pts)
    innerpts=[]
    for x,y in pts:
        innerpts.append((int(cx+(x-cx)*0.86),int(cy+(y-cy)*0.86)))
    d.polygon(innerpts, fill=inner)
    inner2=[]
    for x,y in pts:
        inner2.append((int(cx+(x-cx)*0.72),int(cy+(y-cy)*0.72)))
    d.polygon(inner2, fill=LIGHT)

def rest_platform(double=False):
    im=canvas()
    bbox=(50 if not double else 38,38,206 if not double else 218,218)
    add_shadow(im,bbox,18)
    rounded_panel(im,bbox,20)
    d=ImageDraw.Draw(im)
    # recessed sleeping field
    inset=(66 if not double else 52,54,190 if not double else 204,205)
    d.rounded_rectangle(inset, radius=18, fill=(38,55,66,255), outline=DARK, width=4)
    if double:
        d.line((128,60,128,198), fill=(100,121,130,255), width=3)
        glow_line(im,(128,66,128,194),4)
        for x in (84,172):
            d.rounded_rectangle((x-26,66,x+26,102), radius=12, fill=(116,139,150,255), outline=EDGE, width=3)
            glow_line(im,(x-18,109,x+18,109),3)
    else:
        d.rounded_rectangle((82,66,174,104), radius=13, fill=(116,139,150,255), outline=EDGE, width=3)
        glow_line(im,(82,118,174,118),4)
    # frame nodes
    for p in ((62,52),(194,52),(62,204),(194,204)):
        node(im,p,6)
    return im

def chair(settee=False):
    im=canvas()
    if settee:
        bbox=(34,72,222,184)
        add_shadow(im,bbox,18)
        # sculpted back
        bevel_poly(im,[(40,92),(58,70),(198,70),(216,92),(204,124),(52,124)],IVORY)
        d=ImageDraw.Draw(im)
        d.rounded_rectangle((50,112,206,180), radius=22, fill=(96,119,130,255), outline=EDGE, width=4)
        d.line((128,118,128,174), fill=(53,72,82,255), width=3)
        glow_line(im,(68,136,188,136),5)
        for x in (76,180): node(im,(x,165),6)
    else:
        bbox=(66,52,190,204)
        add_shadow(im,bbox,18)
        bevel_poly(im,[(78,58),(178,58),(190,92),(170,126),(86,126),(66,92)],IVORY)
        d=ImageDraw.Draw(im)
        d.rounded_rectangle((82,116,174,194), radius=26, fill=(91,115,127,255), outline=EDGE, width=4)
        glow_line(im,(96,137,160,137),5)
        node(im,(128,177),7)
    return im

def table(big=False):
    im=canvas()
    if big:
        pts=[(54,76),(82,50),(174,50),(202,76),(202,180),(174,206),(82,206),(54,180)]
        add_shadow(im,(50,46,206,210),18)
        bevel_poly(im,pts)
        d=ImageDraw.Draw(im)
        d.rounded_rectangle((78,78,178,178), radius=18, fill=(100,119,128,255), outline=DARK, width=3)
        d.ellipse((96,96,160,160), fill=(37,56,68,255), outline=EDGE, width=3)
        node(im,(128,128),12)
        for p in ((75,75),(181,75),(75,181),(181,181)): node(im,p,5)
    else:
        pts=[(88,32),(168,32),(190,56),(190,200),(168,224),(88,224),(66,200),(66,56)]
        add_shadow(im,(62,28,194,228),18)
        bevel_poly(im,pts)
        d=ImageDraw.Draw(im)
        d.rounded_rectangle((88,58,168,198), radius=16, fill=(100,119,128,255), outline=DARK, width=3)
        glow_line(im,(128,70,128,188),5)
        for p in ((78,48),(178,48),(78,208),(178,208)): node(im,p,5)
    return im

def bedside():
    im=canvas()
    add_shadow(im,(76,62,180,198),16)
    rounded_panel(im,(78,60,178,196),18)
    d=ImageDraw.Draw(im)
    d.polygon([(92,78),(164,78),(154,120),(102,120)], fill=(30,55,70,255), outline=EDGE)
    glow_line(im,(102,95,154,95),4)
    d.rounded_rectangle((92,132,164,178), radius=12, fill=(103,124,133,255), outline=EDGE, width=3)
    node(im,(128,155),7)
    return im

def plinth():
    im=canvas()
    add_shadow(im,(56,70,200,190),18)
    bevel_poly(im,[(64,88),(84,70),(172,70),(192,88),(192,174),(172,190),(84,190),(64,174)])
    d=ImageDraw.Draw(im)
    d.rounded_rectangle((82,96,174,164), radius=14, fill=(57,74,84,255), outline=EDGE, width=3)
    glow_line(im,(92,130,164,130),4)
    for p in ((76,84),(180,84),(76,176),(180,176)): node(im,p,5)
    return im

def lumen(wall=False):
    im=canvas()
    if wall:
        add_shadow(im,(72,74,184,182),14,alpha=50,offset=(0,5))
        bevel_poly(im,[(76,104),(94,82),(162,82),(180,104),(166,170),(90,170)],IVORY)
        d=ImageDraw.Draw(im)
        d.rounded_rectangle((101,96,155,156), radius=14, fill=(31,60,74,255), outline=EDGE, width=3)
        glow_line(im,(128,104,128,148),7)
    else:
        add_shadow(im,(76,66,180,190),16,alpha=55,offset=(0,6))
        bevel_poly(im,[(128,58),(176,92),(162,178),(94,178),(80,92)],IVORY)
        d=ImageDraw.Draw(im)
        d.polygon([(128,76),(154,100),(146,154),(110,154),(102,100)], fill=(28,55,68,255), outline=EDGE)
        node(im,(128,120),16)
    return im

def save_family(name, image, rotate=True):
    # Base image is kept as South for compatibility with Graphic_Multi fallback.
    image.save(OUT / f"{name}.png", optimize=True)
    if not rotate:
        return
    image.save(OUT / f"{name}_south.png", optimize=True)
    image.rotate(180, resample=Image.Resampling.BICUBIC, expand=False).save(OUT / f"{name}_north.png", optimize=True)
    image.rotate(-90, resample=Image.Resampling.BICUBIC, expand=False).save(OUT / f"{name}_east.png", optimize=True)
    image.rotate(90, resample=Image.Resampling.BICUBIC, expand=False).save(OUT / f"{name}_west.png", optimize=True)

save_family("WNG_PrecursorRestPlatform", rest_platform(False))
save_family("WNG_PrecursorRestPlatformDouble", rest_platform(True))
save_family("WNG_PrecursorFormChair", chair(False))
save_family("WNG_PrecursorSettee", chair(True))
save_family("WNG_PrecursorTableSmall", table(False))
save_family("WNG_PrecursorTable", table(True), rotate=False)
save_family("WNG_PrecursorBedsideConsole", bedside())
save_family("WNG_PrecursorStoragePlinth", plinth())
save_family("WNG_PrecursorLumen", lumen(False))
save_family("WNG_PrecursorWallLumen", lumen(True))

# Decode/alpha sanity: generated production art must be real transparent PNG data.
for path in sorted(OUT.glob("WNG_Precursor*.png")):
    with Image.open(path) as im:
        im.load()
        if im.mode != "RGBA":
            raise SystemExit(f"{path}: expected RGBA, got {im.mode}")
        if im.size != (SIZE, SIZE):
            raise SystemExit(f"{path}: wrong size {im.size}")
        alpha = im.getchannel("A")
        if alpha.getbbox() is None:
            raise SystemExit(f"{path}: fully transparent")
        # Keep a safe transparent border for RimWorld texture filtering.
        edge = []
        edge.extend(alpha.crop((0,0,SIZE,2)).getdata())
        edge.extend(alpha.crop((0,SIZE-2,SIZE,SIZE)).getdata())
        edge.extend(alpha.crop((0,0,2,SIZE)).getdata())
        edge.extend(alpha.crop((SIZE-2,0,SIZE,SIZE)).getdata())
        if any(v > 8 for v in edge):
            raise SystemExit(f"{path}: alpha touches canvas edge")

print(f"Generated {len(list(OUT.glob('WNG_Precursor*.png')))} dedicated Precursor settlement furniture sprites.")
