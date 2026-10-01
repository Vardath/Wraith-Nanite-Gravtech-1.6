from PIL import Image,ImageDraw,ImageFilter,ImageChops
from pathlib import Path

R=Path("Textures/Things/Pawn/Humanlike/Apparel/Wraith")
F=sorted(R.glob("WNG_HunterCoat*.png"))
assert len(F)==26,len(F)

def clip(im,mask):
    im.putalpha(ImageChops.multiply(im.getchannel("A"),mask)); return im

def line(base,xy,col,w=2,glow=False):
    if glow:
        g=Image.new("RGBA",base.size,(0,0,0,0)); gd=ImageDraw.Draw(g)
        gd.line(xy,fill=col[:3]+(90,),width=w*4,joint="curve")
        base.alpha_composite(g.filter(ImageFilter.GaussianBlur(3)))
    ImageDraw.Draw(base).line(xy,fill=col,width=w,joint="curve")

def repaint(p):
    old=Image.open(p).convert("RGBA"); old.load()
    mask=old.getchannel("A"); b=mask.getbbox(); assert b
    x0,y0,x1,y1=b; w=x1-x0; h=y1-y0; cx=(x0+x1)//2
    s=p.stem.lower()
    side="east" if s.endswith("_east") else "west" if s.endswith("_west") else "north" if s.endswith("_north") else "south"

    out=Image.new("RGBA",old.size,(0,0,0,0))
    top=Image.new("RGBA",old.size,(72,58,78,255))
    bot=Image.new("RGBA",old.size,(23,19,28,255))
    grad=Image.blend(top,bot,0.55)
    gp=grad.load()
    for y in range(old.height):
        t=y/max(1,old.height-1)
        c=(int(79*(1-t)+22*t),int(64*(1-t)+18*t),int(84*(1-t)+28*t),255)
        for x in range(old.width): gp[x,y]=c
    grad.putalpha(mask); out.alpha_composite(grad)

    sh=Image.new("RGBA",old.size,(0,0,0,0)); sd=ImageDraw.Draw(sh)
    sd.rectangle((0,0,cx-int(.10*w),old.height),fill=(7,6,10,72))
    sd.rectangle((cx+int(.28*w),0,old.width,old.height),fill=(8,7,11,38))
    out.alpha_composite(clip(sh,mask))

    lay=Image.new("RGBA",old.size,(0,0,0,0)); d=ImageDraw.Draw(lay)
    def P(q,fill,ol=(20,16,24,255)):
        z=[(x0+int(a*w),y0+int(b*h)) for a,b in q]
        d.polygon(z,fill=fill); d.line(z+[z[0]],fill=ol,width=2,joint="curve")
    def L(q,col=(103,84,108,230),ww=2):
        z=[(x0+int(a*w),y0+int(b*h)) for a,b in q]; d.line(z,fill=col,width=ww,joint="curve")

    if side=="south":
        P([(.08,.17),(.37,.06),(.49,.19),(.43,.52),(.19,.82),(.10,.94)],(54,43,62,230))
        P([(.92,.17),(.63,.06),(.51,.19),(.57,.52),(.81,.82),(.90,.94)],(42,34,50,235))
        P([(.17,.15),(.34,.03),(.47,.18),(.40,.29),(.22,.31)],(83,66,88,242))
        P([(.83,.15),(.66,.03),(.53,.18),(.60,.29),(.78,.31)],(73,58,79,242))
        for k in range(4):
            yy=.30+k*.115
            L([(.18,yy),(.39,yy+.035),(.47,yy+.075)],(78,62,83,245),2)
            L([(.82,yy),(.61,yy+.035),(.53,yy+.075)],(78,62,83,245),2)
        L([(.30,.18),(.44,.36),(.40,.60),(.29,.84)],(139,121,145,200),1)
    elif side=="north":
        P([(.09,.18),(.37,.07),(.47,.21),(.42,.88),(.20,.95)],(45,36,54,232))
        P([(.91,.18),(.63,.07),(.53,.21),(.58,.88),(.80,.95)],(38,31,46,235))
        P([(.35,.10),(.50,.04),(.65,.10),(.60,.82),(.50,.94),(.40,.82)],(63,49,68,225))
        for k in range(6):
            yy=.17+k*.105
            P([(.43,yy),(.50,yy-.03),(.57,yy),(.54,yy+.045),(.46,yy+.045)],(92,73,96,245))
        for k in range(3):
            yy=.24+k*.13
            L([(.48,yy),(.31,yy+.03),(.16,yy+.10)],(76,60,81,245),2)
            L([(.52,yy),(.69,yy+.03),(.84,yy+.10)],(76,60,81,245),2)
    elif side=="east":
        P([(.16,.14),(.50,.03),(.84,.18),(.77,.42),(.66,.92),(.28,.98),(.20,.58)],(48,38,57,235))
        P([(.47,.13),(.84,.18),(.77,.42),(.56,.48),(.42,.31)],(79,62,84,235))
        for k in range(4):
            yy=.33+k*.12; L([(.40,yy),(.68,yy+.02),(.75,yy+.08)],(77,61,82,245),2)
        L([(.59,.17),(.64,.38),(.56,.65),(.49,.87)],(140,122,145,205),1)
    else:
        P([(.84,.14),(.50,.03),(.16,.18),(.23,.42),(.34,.92),(.72,.98),(.80,.58)],(48,38,57,235))
        P([(.53,.13),(.16,.18),(.23,.42),(.44,.48),(.58,.31)],(79,62,84,235))
        for k in range(4):
            yy=.33+k*.12; L([(.60,yy),(.32,yy+.02),(.25,yy+.08)],(77,61,82,245),2)
        L([(.41,.17),(.36,.38),(.44,.65),(.51,.87)],(140,122,145,205),1)

    out.alpha_composite(clip(lay,mask))
    bio=(92,162,139,255)
    if side=="south":
        line(out,[(cx,y0+int(.22*h)),(cx-2,y0+int(.42*h)),(cx+2,y0+int(.64*h))],bio,1,True)
        line(out,[(x0+int(.24*w),y0+int(.34*h)),(x0+int(.32*w),y0+int(.46*h))],bio,1,True)
        r=max(2,w//42); cy=y0+int(.31*h)
        ImageDraw.Draw(out).ellipse((cx-r,cy-r,cx+r,cy+r),fill=(112,155,126,255),outline=(139,207,177,255))
    elif side=="north":
        line(out,[(cx,y0+int(.17*h)),(cx,y0+int(.78*h))],bio,1,True)
    elif side=="east":
        line(out,[(x0+int(.62*w),y0+int(.20*h)),(x0+int(.67*w),y0+int(.72*h))],bio,1,True)
    else:
        line(out,[(x0+int(.38*w),y0+int(.20*h)),(x0+int(.33*w),y0+int(.72*h))],bio,1,True)

    hi=Image.new("RGBA",old.size,(0,0,0,0)); hd=ImageDraw.Draw(hi)
    hd.ellipse((x0+int(.17*w),y0,x0+int(.73*w),y0+int(.42*h)),fill=(190,173,197,22))
    out.alpha_composite(clip(hi.filter(ImageFilter.GaussianBlur(8)),mask))
    out.putalpha(mask)

    px=out.load()
    for y in range(out.height):
        for x in range(out.width):
            if px[x,y][3]==0: px[x,y]=(0,0,0,0)
    out.save(p,optimize=True)

for p in F: repaint(p)
for p in F:
    im=Image.open(p).convert("RGBA"); im.load(); a=im.getchannel("A"); b=a.getbbox()
    assert im.size==(192,192) and b and b[0]>0 and b[1]>0 and b[2]<192 and b[3]<192,p
print("Hunter Coat repaint complete:",len(F))
