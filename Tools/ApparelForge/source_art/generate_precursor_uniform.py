from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
import numpy as np

ROOT=Path(__file__).resolve().parents[3]
SRC=ROOT/"ArtSource/Apparel/human_form_uniform"
OUT=ROOT/"ArtSource/Apparel/precursor_uniform"
OUT.mkdir(parents=True,exist_ok=True)

def recolor(im,view):
    im=im.convert("RGBA")
    arr=np.array(im).astype(np.float32)
    rgb=arr[...,:3]
    alpha=arr[...,3]/255.0
    lum=.2126*rgb[...,0]+.7152*rgb[...,1]+.0722*rgb[...,2]
    m=alpha>.03
    vals=lum[m]
    lo=np.percentile(vals,1.5); hi=np.percentile(vals,99.0)
    L=np.clip((lum-lo)/(hi-lo+1e-6),0,1)
    L=.5+.5*np.tanh((L-.46)*2.2)

    r,g,b=rgb[...,0],rgb[...,1],rgb[...,2]
    warm=(r-b)>7
    cool=(b-r)>4
    dark=lum<62

    out=np.zeros_like(rgb)

    # Ancient / Lantean off-white field cloth: rich, worn, not white plastic.
    sh=np.array([34,39,39.],np.float32)
    md=np.array([137,146,143.],np.float32)
    hiC=np.array([226,222,205.],np.float32)
    lower=L<.52
    q=np.clip(L/.52,0,1)
    out[lower]=sh+(md-sh)*q[lower,None]
    q2=np.clip((L-.52)/.48,0,1)
    out[~lower]=md+(hiC-md)*q2[~lower,None]

    # Warm existing trim -> aged champagne / pale naquadah-like metal.
    wm=warm & (lum>58) & m
    metal_lo=np.array([72,68,57.],np.float32)
    metal_hi=np.array([222,205,168.],np.float32)
    qm=np.clip((L-.10)/.90,0,1)
    out[wm]=metal_lo+(metal_hi-metal_lo)*qm[wm,None]

    # Blue-grey woven panels -> cool silver cloth.
    cm=cool & (lum>45) & m
    cool_lo=np.array([39,51,55.],np.float32)
    cool_hi=np.array([194,207,204.],np.float32)
    out[cm]=cool_lo+(cool_hi-cool_lo)*qm[cm,None]

    # Keep deepest technical underlayer truly dark.
    dm=dark & m
    graph_lo=np.array([5,8,9.],np.float32)
    graph_hi=np.array([56,66,67.],np.float32)
    qd=np.clip(L/.44,0,1)
    out[dm]=graph_lo+(graph_hi-graph_lo)*qd[dm,None]

    # Re-inject source high-frequency painterly detail so cloth/leather/metal stay tactile.
    src_lum=Image.fromarray(np.uint8(np.clip(lum,0,255)),"L")
    lowpass=np.asarray(src_lum.filter(ImageFilter.GaussianBlur(2.0)),dtype=np.float32)
    detail=lum-lowpass
    out += detail[...,None]*0.55

    # Preserve existing cool highlights as restrained Ancient conductor accents.
    cyan=(b>r*1.06)&(g>r*1.02)&(lum>52)&m
    out[cyan]=out[cyan]*.58+np.array([72,145,165.],np.float32)*.42

    out=np.clip(out,0,255)
    rgba=np.dstack([out,arr[...,3]])
    res=Image.fromarray(np.uint8(rgba),"RGBA")
    res=ImageEnhance.Contrast(res).enhance(1.08)
    res=ImageEnhance.Sharpness(res).enhance(1.16)

    # Add only a few small Ancient geometric seam accents over the finished painting.
    w,h=res.size
    lay=Image.new("RGBA",res.size,(0,0,0,0))
    d=ImageDraw.Draw(lay)
    if view in ("south","north"):
        cx=w//2
        d.line([(cx,int(h*.26)),(cx,int(h*.70))],fill=(93,154,167,120),width=max(2,w//260))
        d.line([(int(w*.38),int(h*.36)),(cx,int(h*.42)),(int(w*.62),int(h*.36))],fill=(174,164,132,95),width=max(2,w//320))
    else:
        d.line([(int(w*.48),int(h*.28)),(int(w*.58),int(h*.48)),(int(w*.54),int(h*.73))],fill=(93,154,167,115),width=max(2,w//220))
    lay=lay.filter(ImageFilter.GaussianBlur(.35))
    res.alpha_composite(lay)
    res.putalpha(im.getchannel("A"))
    return res

for view in ("south","north","east"):
    p=SRC/f"master_{view}.png"
    if not p.exists():
        raise FileNotFoundError(p)
    out=recolor(Image.open(p),view)
    out.save(OUT/f"master_{view}.png",optimize=True)
    print("painted",view,out.size)
