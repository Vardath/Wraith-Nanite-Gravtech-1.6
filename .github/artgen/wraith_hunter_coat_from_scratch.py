from PIL import Image, ImageDraw, ImageFilter
from pathlib import Path
import hashlib

OUT = Path("Textures/Things/Pawn/Humanlike/Apparel/Wraith")
OUT.mkdir(parents=True, exist_ok=True)

SS = 4
C = 192
W = C * SS

BODIES = {
    "Male":   (1.00, 1.00, 0),
    "Female": (0.91, 0.94, 0),
    "Thin":   (0.83, 0.88, 1),
    "Fat":    (1.15, 1.12, 0),
    "Hulk":   (1.28, 1.20, 0),
}

def S(v): return int(round(v * SS))
def P(points): return [(S(x), S(y)) for x,y in points]

def rgba():
    return Image.new("RGBA", (W,W), (0,0,0,0))

def polygon(im, points, fill, outline=None, width=1):
    d=ImageDraw.Draw(im)
    q=P(points)
    d.polygon(q, fill=fill)
    if outline:
        d.line(q+[q[0]], fill=outline, width=S(width), joint="curve")

def line(im, points, fill, width=1):
    ImageDraw.Draw(im).line(P(points), fill=fill, width=S(width), joint="curve")

def ellipse(im, box, fill, outline=None, width=1):
    ImageDraw.Draw(im).ellipse(tuple(S(v) for v in box), fill=fill, outline=outline, width=S(width))

def glow_line(im, points, color, width=1):
    g=rgba()
    line(g, points, color[:3]+(105,), width*5)
    im.alpha_composite(g.filter(ImageFilter.GaussianBlur(S(2.2))))
    line(im, points, color, width)

def mask_fill(mask, top, bottom):
    out=rgba()
    px=out.load(); ma=mask.load()
    for y in range(W):
        t=y/max(1,W-1)
        c=tuple(round(top[i]*(1-t)+bottom[i]*t) for i in range(4))
        for x in range(W):
            if ma[x,y]:
                px[x,y]=c[:3]+(ma[x,y],)
    return out

def apply_mask(layer, mask):
    a=layer.getchannel("A")
    a2=Image.new("L", layer.size, 0)
    p=a2.load(); q=a.load(); m=mask.load()
    for y in range(W):
        for x in range(W):
            p[x,y]=(q[x,y]*m[x,y])//255
    layer.putalpha(a2)
    return layer

def add_material(base, mask, bbox, direction):
    x0,y0,x1,y1=bbox
    w=x1-x0; h=y1-y0
    # broad satin highlight
    hi=rgba()
    d=ImageDraw.Draw(hi)
    d.ellipse((S(x0+w*.12),S(y0-h*.06),S(x0+w*.66),S(y0+h*.58)), fill=(205,191,215,28))
    hi=hi.filter(ImageFilter.GaussianBlur(S(7)))
    base.alpha_composite(apply_mask(hi,mask))
    # organic micro-ribbing readable at RimWorld scale
    tex=rgba()
    for k in range(6):
        yy=y0+h*(.42+k*.075)
        if direction in ("south","north"):
            line(tex,[(x0+w*.18,yy),(x0+w*.42,yy+h*.018),(x0+w*.50,yy+h*.045),
                      (x0+w*.58,yy+h*.018),(x0+w*.82,yy)],(142,121,150,44),1)
        else:
            line(tex,[(x0+w*.25,yy),(x0+w*.72,yy+h*.015)],(142,121,150,38),1)
    base.alpha_composite(apply_mask(tex,mask))

def silhouette(body, direction):
    width, bulk, yoff = BODIES[body]
    mask=Image.new("L",(W,W),0)
    d=ImageDraw.Draw(mask)
    cx=96
    top=53+yoff
    bottom=181+yoff
    sh=35*bulk
    waist=27*width
    hem=35*width
    if direction in ("south","north"):
        pts=[
          (cx-12,top),(cx-22,top+8),(cx-sh,top+15),(cx-sh-11,top+28),
          (cx-sh-9,top+47),(cx-waist-5,top+60),(cx-hem, bottom-18),
          (cx-16,bottom),(cx,bottom-7),(cx+16,bottom),(cx+hem,bottom-18),
          (cx+waist+5,top+60),(cx+sh+9,top+47),(cx+sh+11,top+28),
          (cx+sh,top+15),(cx+22,top+8),(cx+12,top)
        ]
        d.polygon(P(pts),fill=255)
        # strong high collar notch
        d.polygon(P([(cx-9,top-1),(cx-6,top+10),(cx,top+15),(cx+6,top+10),(cx+9,top-1)]),fill=0)
    else:
        sx=1 if direction=="east" else -1
        front=cx+sx*(26*width)
        back=cx-sx*(24*width)
        if sx>0:
            pts=[(back,top+9),(cx-8,top+1),(cx+9,top+4),(front,top+15),(front+10,top+30),
                 (front+6,top+50),(front,top+68),(front+7,bottom-18),(cx+13,bottom),
                 (cx-4,bottom-7),(back-12,bottom-15),(back-6,top+56),(back-12,top+28)]
        else:
            pts=[(back,top+9),(cx+8,top+1),(cx-9,top+4),(front,top+15),(front-10,top+30),
                 (front-6,top+50),(front,top+68),(front-7,bottom-18),(cx-13,bottom),
                 (cx+4,bottom-7),(back+12,bottom-15),(back+6,top+56),(back+12,top+28)]
        d.polygon(P(pts),fill=255)
        d.ellipse((S(cx-7),S(top-4),S(cx+7),S(top+10)),fill=0)
    return mask

def render_worn(body, direction):
    mask=silhouette(body,direction)
    bbox=tuple(v/SS for v in mask.getbbox())
    x0,y0,x1,y1=bbox; w=x1-x0; h=y1-y0; cx=(x0+x1)/2

    out=rgba()
    out.alpha_composite(mask_fill(mask,(86,67,91,255),(24,19,30,255)))

    # dark perimeter for vanilla RimWorld readability
    edge=mask.filter(ImageFilter.MaxFilter(S(3)|1))
    ep=edge.load(); mp=mask.load()
    rim=rgba(); rp=rim.load()
    for y in range(W):
        for x in range(W):
            a=max(0,ep[x,y]-mp[x,y])
            if a: rp[x,y]=(15,11,18,a)
    out.alpha_composite(rim)

    plates=rgba()
    dark=(39,29,45,235); mid=(66,48,71,242); high=(92,70,97,245); rib=(33,25,39,255)
    if direction=="south":
        # split long coat with asymmetrical organic lapels
        polygon(plates,[(x0+w*.08,y0+h*.18),(x0+w*.34,y0+h*.07),(x0+w*.49,y0+h*.22),
                        (x0+w*.44,y0+h*.52),(x0+w*.18,y0+h*.91),(x0+w*.11,y0+h*.97)],mid,rib,1)
        polygon(plates,[(x0+w*.92,y0+h*.18),(x0+w*.66,y0+h*.07),(x0+w*.51,y0+h*.22),
                        (x0+w*.56,y0+h*.52),(x0+w*.82,y0+h*.91),(x0+w*.89,y0+h*.97)],dark,rib,1)
        polygon(plates,[(x0+w*.16,y0+h*.16),(x0+w*.34,y0+h*.02),(x0+w*.48,y0+h*.19),
                        (x0+w*.40,y0+h*.31),(x0+w*.22,y0+h*.32)],high,rib,2)
        polygon(plates,[(x0+w*.84,y0+h*.16),(x0+w*.66,y0+h*.02),(x0+w*.52,y0+h*.19),
                        (x0+w*.60,y0+h*.31),(x0+w*.78,y0+h*.32)],mid,rib,2)
        # chitin shoulder caps
        polygon(plates,[(x0+w*.05,y0+h*.20),(x0+w*.16,y0+h*.11),(x0+w*.28,y0+h*.15),
                        (x0+w*.25,y0+h*.28),(x0+w*.10,y0+h*.31)],(77,57,82,250),rib,2)
        polygon(plates,[(x0+w*.95,y0+h*.20),(x0+w*.84,y0+h*.11),(x0+w*.72,y0+h*.15),
                        (x0+w*.75,y0+h*.28),(x0+w*.90,y0+h*.31)],(66,49,72,250),rib,2)
        for k in range(4):
            yy=.33+k*.105
            line(plates,[(x0+w*.17,y0+h*yy),(x0+w*.38,y0+h*(yy+.035)),(x0+w*.47,y0+h*(yy+.075))],(97,73,102,235),2)
            line(plates,[(x0+w*.83,y0+h*yy),(x0+w*.62,y0+h*(yy+.035)),(x0+w*.53,y0+h*(yy+.075))],(84,63,90,235),2)
    elif direction=="north":
        polygon(plates,[(x0+w*.10,y0+h*.18),(x0+w*.36,y0+h*.07),(x0+w*.47,y0+h*.22),
                        (x0+w*.42,y0+h*.89),(x0+w*.19,y0+h*.96)],mid,rib,1)
        polygon(plates,[(x0+w*.90,y0+h*.18),(x0+w*.64,y0+h*.07),(x0+w*.53,y0+h*.22),
                        (x0+w*.58,y0+h*.89),(x0+w*.81,y0+h*.96)],dark,rib,1)
        # raised spinal lattice
        polygon(plates,[(x0+w*.38,y0+h*.10),(x0+w*.50,y0+h*.03),(x0+w*.62,y0+h*.10),
                        (x0+w*.58,y0+h*.84),(x0+w*.50,y0+h*.94),(x0+w*.42,y0+h*.84)],(72,53,77,245),rib,2)
        for k in range(6):
            yy=.18+k*.105
            polygon(plates,[(x0+w*.43,y0+h*yy),(x0+w*.50,y0+h*(yy-.03)),(x0+w*.57,y0+h*yy),
                            (x0+w*.54,y0+h*(yy+.045)),(x0+w*.46,y0+h*(yy+.045))],(101,77,106,245),rib,1)
    elif direction=="east":
        polygon(plates,[(x0+w*.14,y0+h*.14),(x0+w*.49,y0+h*.03),(x0+w*.86,y0+h*.17),
                        (x0+w*.78,y0+h*.43),(x0+w*.66,y0+h*.93),(x0+w*.28,y0+h*.98),(x0+w*.19,y0+h*.59)],mid,rib,2)
        polygon(plates,[(x0+w*.49,y0+h*.12),(x0+w*.86,y0+h*.17),(x0+w*.77,y0+h*.40),
                        (x0+w*.57,y0+h*.48),(x0+w*.42,y0+h*.31)],high,rib,1)
        for k in range(4):
            yy=.34+k*.12
            line(plates,[(x0+w*.39,y0+h*yy),(x0+w*.68,y0+h*(yy+.02)),(x0+w*.75,y0+h*(yy+.08))],(91,68,96,235),2)
    else:
        polygon(plates,[(x0+w*.86,y0+h*.14),(x0+w*.51,y0+h*.03),(x0+w*.14,y0+h*.17),
                        (x0+w*.22,y0+h*.43),(x0+w*.34,y0+h*.93),(x0+w*.72,y0+h*.98),(x0+w*.81,y0+h*.59)],mid,rib,2)
        polygon(plates,[(x0+w*.51,y0+h*.12),(x0+w*.14,y0+h*.17),(x0+w*.23,y0+h*.40),
                        (x0+w*.43,y0+h*.48),(x0+w*.58,y0+h*.31)],high,rib,1)
        for k in range(4):
            yy=.34+k*.12
            line(plates,[(x0+w*.61,y0+h*yy),(x0+w*.32,y0+h*(yy+.02)),(x0+w*.25,y0+h*(yy+.08))],(91,68,96,235),2)

    out.alpha_composite(apply_mask(plates,mask))

    bio=(100,177,151,255)
    if direction=="south":
        glow_line(out,[(cx,y0+h*.23),(cx-2,y0+h*.43),(cx+2,y0+h*.66)],bio,1)
        glow_line(out,[(x0+w*.24,y0+h*.36),(x0+w*.32,y0+h*.47)],bio,1)
        ellipse(out,(cx-3,y0+h*.30-3,cx+3,y0+h*.30+3),(116,164,131,255),(157,220,184,255),1)
    elif direction=="north":
        glow_line(out,[(cx,y0+h*.16),(cx,y0+h*.79)],bio,1)
    elif direction=="east":
        glow_line(out,[(x0+w*.62,y0+h*.19),(x0+w*.67,y0+h*.73)],bio,1)
    else:
        glow_line(out,[(x0+w*.38,y0+h*.19),(x0+w*.33,y0+h*.73)],bio,1)

    add_material(out,mask,bbox,direction)
    out.putalpha(mask)
    return out.resize((C,C),Image.Resampling.LANCZOS)

def render_icon():
    out=rgba()
    # item tile: standalone long organic coat, not a pawn-body silhouette
    shadow=rgba()
    polygon(shadow,[(46,42),(72,28),(94,41),(116,28),(145,43),(136,68),(126,76),
                    (135,160),(108,171),(96,156),(84,171),(57,160),(66,76),(55,68)],(0,0,0,100))
    shadow=shadow.filter(ImageFilter.GaussianBlur(S(4)))
    out.alpha_composite(shadow)

    # full garment
    polygon(out,[(47,37),(72,24),(95,42),(118,24),(145,38),(137,67),(123,73),
                 (134,158),(108,172),(96,151),(84,172),(58,158),(69,73),(55,67)],
            (62,46,68,255),(15,11,18,255),3)
    polygon(out,[(47,37),(72,24),(95,42),(82,67),(57,64)],(92,70,98,255),(22,16,26,255),2)
    polygon(out,[(145,38),(118,24),(97,42),(110,67),(136,64)],(72,53,79,255),(22,16,26,255),2)
    polygon(out,[(70,70),(94,48),(94,143),(83,164),(59,154)],(57,42,64,255),(25,18,30,255),2)
    polygon(out,[(122,70),(98,48),(98,143),(109,164),(133,154)],(42,32,49,255),(25,18,30,255),2)

    # high collar / throat opening
    polygon(out,[(78,25),(88,17),(96,29),(104,17),(114,25),(107,44),(96,51),(85,44)],
            (76,57,83,255),(18,13,21,255),2)
    ellipse(out,(90,25,102,38),(0,0,0,0))

    # chitin ribbing and bioluminescent living seams
    for k in range(5):
        y=77+k*14
        line(out,[(64,y),(84,y+4),(93,y+9)],(105,81,110,230),2)
        line(out,[(128,y),(108,y+4),(99,y+9)],(87,66,94,230),2)
    glow_line(out,[(96,55),(94,91),(98,128)],(104,181,154,255),1)
    glow_line(out,[(71,47),(78,60)],(104,181,154,255),1)
    ellipse(out,(92,65,100,73),(119,166,133,255),(161,221,186,255),1)

    # controlled highlights
    hi=rgba()
    ellipse(hi,(55,30,107,99),(210,193,216,30))
    hi=hi.filter(ImageFilter.GaussianBlur(S(6)))
    out.alpha_composite(hi)

    return out.resize((C,C),Image.Resampling.LANCZOS)

def save_clean(im,path):
    px=im.load()
    for y in range(C):
        for x in range(C):
            r,g,b,a=px[x,y]
            if a==0: px[x,y]=(0,0,0,0)
    im.save(path,optimize=True)

# Inventory/tile
save_clean(render_icon(), OUT/"WNG_HunterCoat.png")

# Worn art: completely new, generated from blank transparent canvases
for body in BODIES:
    south=render_worn(body,"south")
    save_clean(south, OUT/f"WNG_HunterCoat_{body}.png")
    for direction in ("north","south","east","west"):
        save_clean(render_worn(body,direction), OUT/f"WNG_HunterCoat_{body}_{direction}.png")

files=sorted(OUT.glob("WNG_HunterCoat*.png"))
assert len(files)==26, len(files)

for p in files:
    im=Image.open(p).convert("RGBA"); im.load()
    assert im.size==(192,192), (p,im.size)
    a=im.getchannel("A"); b=a.getbbox()
    assert b and b[0]>0 and b[1]>0 and b[2]<192 and b[3]<192, (p,b)
    # transparent RGB hygiene
    for r,g,bv,av in im.getdata():
        if av==0:
            assert (r,g,bv)==(0,0,0), p

print("WNG_HUNTER_COAT_FROM_SCRATCH_OK",len(files))
for p in files:
    print(p.name, hashlib.sha256(p.read_bytes()).hexdigest(), p.stat().st_size)
