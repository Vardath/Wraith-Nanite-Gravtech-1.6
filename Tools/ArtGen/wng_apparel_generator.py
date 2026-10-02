from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageChops
from bs4 import BeautifulSoup
import argparse, hashlib, io, json, math, random, re, subprocess
import numpy as np
import requests
import cairosvg
from scipy.ndimage import gaussian_filter, distance_transform_edt

ROOT = Path(__file__).resolve().parents[2]
APPAREL_ROOT = ROOT / "Textures/Things/Pawn/Humanlike/Apparel"
BUILDING_ROOT = ROOT / "Textures/Things/Building"
PROFILE_ROOT = ROOT / "Tools/ArtGen/profiles"
OUT = 192
HI = 768
HIST_REF = "22ab5e03335aad03731814541361ab29d5124319"
UA = "WNG-ArtGen/2.0 (+reference research for local mod art)"

@dataclass
class RefReport:
    verified: int
    total: int
    pages: list[dict]
    image_stats: list[dict]

class StargateReferencePass:
    def __init__(self, profile: dict, workdir: Path):
        self.profile = profile
        self.cfg = profile["stage_1_stargate"]
        self.workdir = workdir
        self.session = requests.Session()
        self.session.headers["User-Agent"] = UA

    def _text(self, html: str) -> str:
        soup = BeautifulSoup(html, "html.parser")
        for tag in soup(["script","style","noscript"]):
            tag.extract()
        return " ".join(soup.stripped_strings)

    def _candidate_images(self, html: str, base_url: str) -> list[str]:
        soup = BeautifulSoup(html, "html.parser")
        urls = []
        for prop in ("og:image","twitter:image"):
            node = soup.find("meta", attrs={"property":prop}) or soup.find("meta", attrs={"name":prop})
            if node and node.get("content"):
                urls.append(requests.compat.urljoin(base_url,node["content"]))
        for img in soup.find_all("img"):
            words = " ".join([img.get("alt",""),img.get("title",""),img.get("class",[None])[0] or ""]).lower()
            src = img.get("src") or img.get("data-src")
            if src and any(k in words for k in ("wraith","costume","queen","armor","armour","hive","stargate")):
                urls.append(requests.compat.urljoin(base_url,src))
        dedup=[]
        for u in urls:
            if u not in dedup: dedup.append(u)
        return dedup[:4]

    def _download_image_stats(self, url: str) -> dict|None:
        try:
            r=self.session.get(url,timeout=18)
            r.raise_for_status()
            if len(r.content)>8_000_000: return None
            im=Image.open(io.BytesIO(r.content)).convert("RGB")
            if im.width < 100 or im.height < 100:
                return None
            bad=url.lower()
            if any(k in bad for k in ("gravatar","banner","poster","button","logo","avatar")):
                return None
            im.thumbnail((512,512),Image.Resampling.LANCZOS)
            a=np.array(im,dtype=np.float32)
            lum=.2126*a[...,0]+.7152*a[...,1]+.0722*a[...,2]
            sat=a.max(2)-a.min(2)
            return {
                "url":url,
                "sha256":hashlib.sha256(r.content).hexdigest(),
                "mean_luminance":float(lum.mean()),
                "contrast":float(lum.std()),
                "mean_saturation":float(sat.mean()),
                "size":[im.width,im.height]
            }
        except Exception:
            return None

    def run(self) -> RefReport:
        pages=[]; image_stats=[]; verified=0
        for src in self.cfg["sources"]:
            row={"url":src["url"],"role":src["role"],"ok":False,"terms":{}}
            try:
                r=self.session.get(src["url"],timeout=20)
                row["http_status"]=r.status_code
                row["sha256"]=hashlib.sha256(r.content).hexdigest()
                r.raise_for_status()
                text=self._text(r.text)
                low=text.lower()
                for term in src.get("required_terms",[]):
                    row["terms"][term]=term.lower() in low
                # A source is considered live/usable when it returned substantial Stargate
                # content and at least one expected cue is present. Exact phrase sets are
                # intentionally not brittle gates because interviews are mirrored/reformatted.
                row["ok"]=len(text) > 500 and (any(row["terms"].values()) if row["terms"] else True)
                if row["ok"]: verified+=1
                if src.get("image_policy")!="metadata_only":
                    for u in self._candidate_images(r.text,src["url"]):
                        st=self._download_image_stats(u)
                        if st: image_stats.append(st)
            except Exception as e:
                row["error"]=str(e)[:300]
            pages.append(row)

        minimum=int(self.cfg.get("minimum_live_sources",max(3,math.ceil(len(self.cfg["sources"])*0.5))))
        min_visual=int(self.cfg.get("minimum_visual_references",0))
        if verified < minimum:
            raise RuntimeError(f"StargateReferencePass failed: {verified}/{len(self.cfg['sources'])} live sources; need {minimum}")
        if len(image_stats) < min_visual:
            raise RuntimeError(f"StargateReferencePass failed: {len(image_stats)} live visual references; need {min_visual}")
        rep=RefReport(verified,len(self.cfg["sources"]),pages,image_stats)
        self.workdir.mkdir(parents=True,exist_ok=True)
        (self.workdir/"stargate_reference_report.json").write_text(json.dumps(rep.__dict__,indent=2))
        return rep

class RimWorldImplementationPass:
    def __init__(self, profile: dict, vanilla_dir: Path, body_dir: Path|None=None):
        self.profile=profile
        self.cfg=profile["stage_2_rimworld"]
        self.vanilla_dir=vanilla_dir
        self.body_dir=body_dir

    def _visible_mask(self, path: Path) -> Image.Image:
        im=Image.open(path).convert("RGBA").resize((HI,HI),Image.Resampling.LANCZOS)
        a=np.array(im.getchannel("A"))
        rgb=np.array(im)[...,:3]
        m=((a>8)&(rgb.max(axis=2)>28)).astype(np.uint8)*255
        return Image.fromarray(m,"L").filter(ImageFilter.GaussianBlur(.45))

    def _svg_mask(self, path: Path) -> Image.Image:
        raw=cairosvg.svg2png(url=str(path),output_width=HI,output_height=HI)
        im=Image.open(io.BytesIO(raw)).convert("RGBA")
        a=np.array(im.getchannel("A"),dtype=np.uint8)
        return Image.fromarray(a,"L")

    def _body_alpha(self, body: str, direction: str) -> Image.Image:
        if self.body_dir is None:
            raise RuntimeError("body_dir is required for powerarmor_svg_deformed")
        p=self.body_dir/f"Naked_{body}_{direction}.png"
        if not p.exists():
            raise FileNotFoundError(p)
        im=Image.open(p).convert("RGBA").resize((HI,HI),Image.Resampling.LANCZOS)
        return im.getchannel("A")

    @staticmethod
    def _bbox_arr(mask: Image.Image):
        a=np.array(mask)>8
        ys,xs=np.nonzero(a)
        if not len(xs): raise RuntimeError("empty implementation mask")
        return xs.min(),ys.min(),xs.max()+1,ys.max()+1

    def _deform_powerarmor(self, base: Image.Image, body: str, direction: str) -> Image.Image:
        male=self._body_alpha("Male",direction)
        targ=self._body_alpha(body,direction)
        mb=self._bbox_arr(male); tb=self._bbox_arr(targ); ab=self._bbox_arr(base)
        mw,mh=mb[2]-mb[0],mb[3]-mb[1]
        tw,th=tb[2]-tb[0],tb[3]-tb[1]
        sx=tw/max(1,mw); sy=th/max(1,mh)
        acx=(ab[0]+ab[2])/2; acy=(ab[1]+ab[3])/2
        mcx=(mb[0]+mb[2])/2; mcy=(mb[1]+mb[3])/2
        tcx=(tb[0]+tb[2])/2; tcy=(tb[1]+tb[3])/2

        crop=base.crop(ab)
        nw=max(1,int(round(crop.width*sx))); nh=max(1,int(round(crop.height*sy)))
        crop=crop.resize((nw,nh),Image.Resampling.LANCZOS)
        out=Image.new("L",(HI,HI),0)
        px=int(round(acx+(tcx-mcx)-nw/2)); py=int(round(acy+(tcy-mcy)-nh/2))
        out.paste(crop,(px,py))

        # keep vanilla contour safely within canvas without distorting it
        b=out.getbbox()
        if b:
            pad=16
            dx=0; dy=0
            if b[0]<pad: dx=pad-b[0]
            elif b[2]>HI-pad: dx=(HI-pad)-b[2]
            if b[1]<pad: dy=pad-b[1]
            elif b[3]>HI-pad: dy=(HI-pad)-b[3]
            if dx or dy:
                shifted=Image.new("L",(HI,HI),0)
                shifted.paste(out,(dx,dy))
                out=shifted
        return out

    def _powerarmor_run(self) -> dict:
        masks={}; metrics={}
        bases={}
        for d in ("south","north","east"):
            p=self.vanilla_dir/f"PowerArmor_Male_{d}.svg"
            if not p.exists(): raise FileNotFoundError(p)
            bases[d]=self._svg_mask(p)

        for body in self.cfg["body_types"]:
            masks[body]={}
            for d in ("south","north","east"):
                m=self._deform_powerarmor(bases[d],body,d)
                masks[body][d]=m
                b=m.getbbox()
                metrics[f"{body}_{d}"]={"bbox":list(b),"opaque":int((np.array(m)>128).sum())}

        # Ground tile derives from the vanilla PowerArmor front contour, centered smaller.
        src=masks["Male"]["south"]
        sb=src.getbbox()
        crop=src.crop(sb)
        tw=int((sb[2]-sb[0])*.78); th=int((sb[3]-sb[1])*.78)
        crop=crop.resize((tw,th),Image.Resampling.LANCZOS)
        tile=Image.new("L",(HI,HI),0)
        tile.paste(crop,((HI-tw)//2,(HI-th)//2))
        return {"masks":masks,"tile":tile,"metrics":metrics}

    def run(self) -> dict:
        mode=self.cfg.get("implementation_mode","png_family")
        if mode=="powerarmor_svg_deformed":
            return self._powerarmor_run()

        fam=self.cfg["vanilla_family"]
        masks={}
        metrics={}
        for body in self.cfg["body_types"]:
            masks[body]={}
            for d in ("south","north","east"):
                p=self.vanilla_dir/f"{fam}_{body}_{d}.png"
                if not p.exists(): raise FileNotFoundError(p)
                m=self._visible_mask(p)
                if not m.getbbox(): raise RuntimeError(f"empty vanilla mask {p}")
                masks[body][d]=m
                b=m.getbbox()
                metrics[f"{body}_{d}"]={"bbox":list(b),"opaque":int((np.array(m)>128).sum())}
        tile=self._visible_mask(self.vanilla_dir/f"{self.cfg['tile_family']}.png")
        return {"masks":masks,"tile":tile,"metrics":metrics}

class SynthesisPass:
    def __init__(self, profile: dict, ref: RefReport, impl: dict):
        self.profile=profile
        self.s1=profile["stage_1_stargate"]
        self.s3=profile["stage_3_synthesis"]
        self.ref=ref
        self.impl=impl
        self.palette=self.s3["palette"]
        self.rng=np.random.default_rng(int(hashlib.sha256(profile["id"].encode()).hexdigest()[:8],16))
        self.benchmark_stats=self._benchmark_stats()

    def _benchmark_stats(self):
        stats=[]
        for rel in self.s3["wNG_quality_benchmarks"]:
            p=ROOT/rel
            im=Image.open(p).convert("RGBA")
            a=np.array(im)
            m=a[...,3]>16
            if not m.any(): continue
            rgb=a[...,:3].astype(np.float32)
            lum=.2126*rgb[...,0]+.7152*rgb[...,1]+.0722*rgb[...,2]
            stats.append({"contrast":float(lum[m].std()),"luminance":float(lum[m].mean())})
        return stats

    def _historical(self, item: str, body: str, direction: str, bb) -> Image.Image:
        base=f"Textures/Things/Pawn/Humanlike/Apparel/Wraith/{item}_{body}_{direction}.png"
        try: raw=subprocess.check_output(["git","show",f"{HIST_REF}:{base}"])
        except subprocess.CalledProcessError:
            raw=subprocess.check_output(["git","show",f"{HIST_REF}:Textures/Things/Pawn/Humanlike/Apparel/Wraith/{item}_Male_{direction}.png"])
        im=Image.open(io.BytesIO(raw)).convert("RGBA")
        sb=im.getchannel("A").getbbox()
        crop=im.crop(sb).resize((bb[2]-bb[0],bb[3]-bb[1]),Image.Resampling.LANCZOS)
        out=Image.new("RGBA",(HI,HI),(0,0,0,0)); out.alpha_composite(crop,(bb[0],bb[1]))
        return out

    def _noise(self, sigma: float, shape=(HI,HI)):
        n=self.rng.normal(0,1,shape)
        n=gaussian_filter(n,sigma)
        return (n-n.mean())/(n.std()+1e-6)

    def _material(self, mask: Image.Image, shadow, mid, high, kind: str, hist: Image.Image|None=None):
        """
        Painted material model for RimWorld scale.
        Broad garment folds define form; grain is subtle. The leather response is
        smooth and polished, while reptile scales are added separately.
        """
        m=np.array(mask,dtype=np.float32)/255.0
        yy,xx=np.mgrid[0:HI,0:HI]

        light=((1-yy/HI)*0.67 + (1-xx/HI)*0.33)
        light=(light-light.min())/(light.max()-light.min()+1e-6)

        fine=self._noise(1.25)
        medium=self._noise(9.0)
        broad_noise=self._noise(36.0)
        grain=.18*fine + .34*medium + .08*broad_noise

        fold_shape=np.zeros((HI,HI),dtype=np.float32)
        crease=np.zeros((HI,HI),dtype=np.float32)
        if hist is not None:
            hl=np.array(hist.convert("L"),dtype=np.float32)
            valid=m>.10
            vals=hl[valid]
            if vals.size:
                p12=float(np.percentile(vals,12))
                p90=float(np.percentile(vals,90))
                norm=np.clip((hl-p12)/max(8.0,p90-p12),0,1)
                fold_shape=gaussian_filter(norm,5.0)-.5
            crease=hl-gaussian_filter(hl,4.0)

        dist=distance_transform_edt(m>.1)
        edge=np.clip(1-dist/13,0,1)

        # Historical painted folds provide most of the value structure.
        lum=.235 + .255*light + grain/480.0 + .285*fold_shape + crease/335.0 - .09*edge
        lum=np.clip(lum,0,1)

        lo=np.array(shadow,float); mi=np.array(mid,float); hi=np.array(high,float)
        rgb=np.empty((HI,HI,3),dtype=np.float32)
        lower=lum<.5
        t=np.clip(lum*2,0,1)
        rgb[lower]=lo+(mi-lo)*t[lower,None]
        t2=np.clip((lum-.5)*2,0,1)
        rgb[~lower]=mi+(hi-mi)*t2[~lower,None]

        # Narrow polished-leather catchlights sit on raised folds only.
        raised=np.clip(fold_shape+.18,0,1)
        spec=(np.clip((lum-.41)/.34,0,1)**2.15) * (.34+.66*raised)
        sheen=np.array([46,50,46],dtype=np.float32) if kind=="leather" else np.array([31,30,34],dtype=np.float32)
        rgb=np.clip(rgb+spec[...,None]*sheen,0,255)

        out=np.dstack([rgb.astype(np.uint8),(m*255).astype(np.uint8)])
        return Image.fromarray(out,"RGBA")

    def _poly_mask(self, bb, pts, feather=5):
        x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
        q=[(int(x0+x*w),int(y0+y*h)) for x,y in pts]
        m=Image.new("L",(HI,HI),0)
        ImageDraw.Draw(m).polygon(q,fill=255)
        if feather: m=m.filter(ImageFilter.GaussianBlur(feather))
        return m

    def _line_mask(self, pts, width, blur=1.5):
        m=Image.new("L",(HI,HI),0); d=ImageDraw.Draw(m)
        d.line(pts,fill=255,width=max(1,int(width)),joint="curve")
        if blur: m=m.filter(ImageFilter.GaussianBlur(blur))
        return m

    def _clip(self, layer, mask):
        a=np.array(layer.getchannel("A"),dtype=np.uint16)
        mm=np.array(mask,dtype=np.uint16)
        layer=layer.copy(); layer.putalpha(Image.fromarray(((a*mm)//255).astype(np.uint8),"L"))
        return layer

    def _leather(self, mask, hist=None, contrast=False):
        p=self.palette
        if contrast:
            return self._material(mask,p["contrast_shadow"],p["contrast_mid"],p["contrast_high"],"leather",hist)
        return self._material(mask,p["leather_shadow"],p["leather_mid"],p["leather_high"],"leather",hist)

    def _reptile(self, mask, hist=None, seed_offset=0):
        """
        Black reptile-pattern leather from the Stargate commander costume.
        Pattern is deliberately larger/cleaner than noise so it survives 192px.
        """
        p=self.palette
        base=self._material(mask,p["reptile_shadow"],p["reptile_mid"],p["reptile_high"],"reptile",hist)
        bb=mask.getbbox()
        if not bb:
            return base
        x0,y0,x1,y1=bb
        w=x1-x0
        rng=random.Random(49031+seed_offset+x0+y0)

        tex=Image.new("RGBA",(HI,HI),(0,0,0,0))
        d=ImageDraw.Draw(tex)
        step_x=max(18,int(w*.072))
        step_y=max(12,int(step_x*.58))
        row=0
        for y in range(y0-step_y,y1+step_y,step_y):
            offset=step_x//2 if row%2 else 0
            for x in range(x0-step_x,x1+step_x,step_x):
                cx=x+offset+rng.randint(-1,1)
                cy=y+rng.randint(-1,1)
                rx=max(6,int(step_x*.43))
                ry=max(4,int(step_y*.43))
                # Dark lower edge + restrained upper catchlight = leather scale, not armour plate.
                d.arc((cx-rx,cy-ry,cx+rx,cy+ry),188,352,fill=(9,8,11,175),width=max(2,int(HI/320)))
                d.arc((cx-rx+2,cy-ry+2,cx+rx-2,cy+ry-2),12,168,fill=(154,144,158,78),width=max(1,int(HI/420)))
            row+=1
        tex=self._clip(tex,mask)
        base.alpha_composite(tex)
        return base

    def _stitch(self, im, pts, spacing=18, color=(122,112,119,150)):
        d=ImageDraw.Draw(im)
        seg=[]
        for a,b in zip(pts[:-1],pts[1:]):
            L=math.dist(a,b); seg.append((a,b,L))
        total=sum(x[2] for x in seg)
        pos=0
        while pos<total:
            remain=pos
            for a,b,L in seg:
                if remain<=L:
                    t=remain/max(1,L)
                    x=a[0]+(b[0]-a[0])*t; y=a[1]+(b[1]-a[1])*t
                    d.line((x-2,y-1,x+2,y+1),fill=color,width=1)
                    break
                remain-=L
            pos+=spacing

    def _gem(self, im, x, y, r=4):
        col=tuple(self.palette.get("biotech",[145,140,198]))
        glow=Image.new("RGBA",(HI,HI),(0,0,0,0))
        gd=ImageDraw.Draw(glow)
        gd.ellipse((x-r*4,y-r*4,x+r*4,y+r*4),fill=col+(34,))
        im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(max(2,r*2))))
        d=ImageDraw.Draw(im)
        d.ellipse((x-r,y-r,x+r,y+r),fill=col+(220,),outline=(48,45,58,240),width=max(1,r//3))
        d.ellipse((x-r*.35,y-r*.35,x+r*.35,y+r*.35),fill=(225,231,236,215))

    def _snap(self, im, x, y, r=3):
        col=tuple(self.palette["metal"])
        d=ImageDraw.Draw(im)
        d.ellipse((x-r,y-r,x+r,y+r),fill=(32,30,33,230),outline=col+(205,),width=max(1,r//2))
        d.ellipse((x-r*.35,y-r*.35,x+r*.35,y+r*.35),fill=(174,169,175,180))

    def _lacing(self, im, left_pts, right_pts, pairs=7):
        d=ImageDraw.Draw(im)
        # Sample straight-ish paired eyelets and crossing cords.
        for i in range(pairs):
            t=(i+.5)/pairs
            lx=left_pts[0][0]*(1-t)+left_pts[-1][0]*t
            ly=left_pts[0][1]*(1-t)+left_pts[-1][1]*t
            rx=right_pts[0][0]*(1-t)+right_pts[-1][0]*t
            ry=right_pts[0][1]*(1-t)+right_pts[-1][1]*t
            self._snap(im,int(lx),int(ly),2)
            self._snap(im,int(rx),int(ry),2)
            if i%2==0:
                d.line((lx,ly,rx,ry+3),fill=(20,18,21,210),width=2)
            else:
                d.line((rx,ry,lx,ly+3),fill=(20,18,21,210),width=2)

    def _finish(self, out, mask):
        ship_contrast=[x["contrast"] for x in self.benchmark_stats]
        live_contrast=[x["contrast"] for x in self.ref.image_stats if min(x.get("size",[0,0]))>=100 and 15<x["contrast"]<95]
        vals=ship_contrast+live_contrast
        target=min(float(np.median(vals)) if vals else 32.0,32.0)

        a=np.array(out,dtype=np.float32)
        m=np.array(mask)>16
        rgb=a[...,:3]
        yy,xx=np.mgrid[0:HI,0:HI]

        # Broad leather modelling, not cloud noise.
        bb=mask.getbbox()
        if bb:
            x0,y0,x1,y1=bb
            w=max(1,x1-x0); h=max(1,y1-y0)
            localx=(xx-x0)/w; localy=(yy-y0)/h
            broad=np.exp(-(((localx-.34)/.30)**2+((localy-.34)/.58)**2))
            broad=(broad-.22)*14.0
            rgb=np.clip(rgb+broad[...,None]*m[...,None],0,255)

        # Vanilla-readable leather edge: dark boundary, narrow polished catch just inside it.
        dist=distance_transform_edt(m)
        outer=(dist>0)&(dist<=3)
        catch=(dist>3)&(dist<=8)
        recess=(dist>8)&(dist<=15)
        rgb[outer]=np.clip(rgb[outer]-np.array([8,8,8]),0,255)
        rgb[catch]=np.clip(rgb[catch]+np.array([15,16,15]),0,255)
        rgb[recess]=np.clip(rgb[recess]-np.array([3,3,3]),0,255)

        lum=.2126*rgb[...,0]+.7152*rgb[...,1]+.0722*rgb[...,2]
        cur=lum[m].std() if m.any() else 1
        scale=np.clip(target/max(cur,1),1.0,1.88)
        mean=rgb[m].mean(axis=0) if m.any() else np.array([45,48,46])
        rgb=(rgb-mean)*scale+mean
        a[...,:3]=np.clip(rgb,0,255)
        a[...,3]=np.array(mask)
        out=Image.fromarray(a.astype(np.uint8),"RGBA")
        out=ImageEnhance.Contrast(out).enhance(1.15)
        out=out.filter(ImageFilter.UnsharpMask(radius=1.10,percent=76,threshold=3))
        out.putalpha(mask)
        return out

    def _paint_hunter(self, body, direction, mask):
        bb=mask.getbbox(); x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
        hist=self._historical("WNG_HunterCoat",body,direction,bb)
        accent=self._historical("WNG_QueenRaiment",body,direction,bb)
        out=self._leather(mask,hist,False)

        if direction=="south":
            # Black reptile-leather inner vest, matching production Wraith commander construction.
            vest=self._poly_mask(bb,[(.34,.05),(.66,.05),(.62,.55),(.56,.83),(.44,.83),(.38,.55)],1)
            vest=ImageChops.multiply(vest,mask)
            out.alpha_composite(self._reptile(vest,accent,10))

            # Narrow contrast leather lapels following the Duster neck/torso, not armour plates.
            lapL=self._poly_mask(bb,[(.31,.06),(.43,.05),(.48,.22),(.42,.51),(.35,.47)],1)
            lapR=self._poly_mask(bb,[(.69,.06),(.57,.05),(.52,.22),(.58,.51),(.65,.47)],1)
            for lm in (lapL,lapR):
                lm=ImageChops.multiply(lm,mask)
                out.alpha_composite(self._leather(lm,hist,True))

            # Layered reptile-leather epaulettes; three small overlapping panels per shoulder.
            shoulders=[
              [(.10,.10),(.22,.055),(.32,.085),(.30,.17),(.14,.18)],
              [(.13,.16),(.25,.11),(.34,.14),(.31,.22),(.17,.23)]
            ]
            for idx,pts in enumerate(shoulders):
                for side in (pts,[(1-x,y) for x,y in pts]):
                    sm=ImageChops.multiply(self._poly_mask(bb,side,1),mask)
                    out.alpha_composite(self._reptile(sm,hist,30+idx))

            # Side lacing and subtle front closures.
            self._lacing(
                out,
                [(int(x0+.18*w),int(y0+.38*h)),(int(x0+.20*w),int(y0+.74*h))],
                [(int(x0+.24*w),int(y0+.38*h)),(int(x0+.26*w),int(y0+.74*h))],
                6
            )
            for i in range(5):
                yy=int(y0+h*(.27+i*.075))
                self._snap(out,int(x0+.51*w),yy,2)
            self._stitch(out,[(int(x0+.70*w),int(y0+.30*h)),(int(x0+.74*w),int(y0+.78*h))],max(13,.055*h))

        elif direction=="north":
            # Back yoke and layered shoulder panels.
            yoke=self._poly_mask(bb,[(.12,.08),(.88,.08),(.79,.29),(.50,.34),(.21,.29)],1)
            yoke=ImageChops.multiply(yoke,mask)
            out.alpha_composite(self._reptile(yoke,hist,80))
            center=self._poly_mask(bb,[(.43,.18),(.57,.18),(.55,.80),(.45,.80)],1)
            center=ImageChops.multiply(center,mask)
            out.alpha_composite(self._leather(center,accent,True))

            # Characteristic side/back lacing from production costume.
            self._lacing(
                out,
                [(int(x0+.36*w),int(y0+.31*h)),(int(x0+.38*w),int(y0+.76*h))],
                [(int(x0+.43*w),int(y0+.31*h)),(int(x0+.45*w),int(y0+.76*h))],
                7
            )
            self._lacing(
                out,
                [(int(x0+.57*w),int(y0+.31*h)),(int(x0+.55*w),int(y0+.76*h))],
                [(int(x0+.64*w),int(y0+.31*h)),(int(x0+.62*w),int(y0+.76*h))],
                7
            )

        else:
            # Side profile: reptile shoulder panel + inner vest glimpse + visible lacing.
            shoulder=self._poly_mask(bb,[(.28,.06),(.70,.05),(.86,.20),(.72,.29),(.43,.23)],1)
            shoulder=ImageChops.multiply(shoulder,mask)
            out.alpha_composite(self._reptile(shoulder,hist,110))
            sidepanel=self._poly_mask(bb,[(.48,.24),(.72,.26),(.69,.76),(.53,.80)],1)
            sidepanel=ImageChops.multiply(sidepanel,mask)
            out.alpha_composite(self._reptile(sidepanel,accent,120))
            self._lacing(
                out,
                [(int(x0+.50*w),int(y0+.36*h)),(int(x0+.52*w),int(y0+.74*h))],
                [(int(x0+.57*w),int(y0+.36*h)),(int(x0+.59*w),int(y0+.74*h))],
                6
            )
            self._stitch(out,[(int(x0+.31*w),int(y0+.28*h)),(int(x0+.34*w),int(y0+.78*h))],max(13,.055*h))

        out=self._finish(out,mask)
        return out.resize((OUT,OUT),Image.Resampling.LANCZOS)

    def _tile_hunter(self, mask):
        bb=mask.getbbox(); x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
        hist=self._historical("WNG_HunterCoat","Male","south",bb)
        out=self._leather(mask,hist,False)
        vest=self._poly_mask(bb,[(.34,.05),(.66,.05),(.62,.58),(.55,.84),(.45,.84),(.38,.58)],1)
        vest=ImageChops.multiply(vest,mask)
        out.alpha_composite(self._reptile(vest,hist,150))
        shoulders=[
          [(.10,.10),(.22,.055),(.32,.085),(.30,.17),(.14,.18)],
          [(.13,.16),(.25,.11),(.34,.14),(.31,.22),(.17,.23)]
        ]
        for idx,pts in enumerate(shoulders):
            for side in (pts,[(1-x,y) for x,y in pts]):
                sm=ImageChops.multiply(self._poly_mask(bb,side,1),mask)
                out.alpha_composite(self._reptile(sm,hist,160+idx))
        self._lacing(
            out,
            [(int(x0+.18*w),int(y0+.39*h)),(int(x0+.20*w),int(y0+.73*h))],
            [(int(x0+.24*w),int(y0+.39*h)),(int(x0+.26*w),int(y0+.73*h))],
            6
        )
        for i in range(5):
            self._snap(out,int(x0+.51*w),int(y0+h*(.27+i*.075)),2)
        out=self._finish(out,mask)
        # Ground/inventory tiles need slightly stronger value separation than worn art.
        out=ImageEnhance.Contrast(out).enhance(1.20)
        out.putalpha(mask)
        return out.resize((OUT,OUT),Image.Resampling.LANCZOS)


    def _chitin(self, mask, hist=None, bone=False, seed_offset=0):
        """Paint grown Wraith shell with sculpted bevels and restrained organic variation."""
        p=self.palette
        if bone:
            shadow,mid,high=p["bone_shadow"],p["bone_mid"],p["bone_high"]
        else:
            shadow,mid,high=p["shell_shadow"],p["shell_mid"],p["shell_high"]
        m=np.array(mask,dtype=np.float32)/255.0
        inside=m>.08
        dist=distance_transform_edt(inside)
        yy,xx=np.mgrid[0:HI,0:HI]
        bb=mask.getbbox()
        if not bb:
            return Image.new("RGBA",(HI,HI),(0,0,0,0))
        x0,y0,x1,y1=bb; w=max(1,x1-x0); h=max(1,y1-y0)
        lx=(xx-x0)/w; ly=(yy-y0)/h
        broad=np.exp(-(((lx-.32)/.40)**2+((ly-.28)/.65)**2))
        n1=self._noise(2.4)
        n2=self._noise(14.0)
        # organic shell should read as one grown surface, not noisy stone
        organic=.7*n1+1.15*n2
        if hist is not None:
            hl=np.array(hist.convert("L"),dtype=np.float32)
            valid=inside
            vals=hl[valid]
            if vals.size:
                lo=float(np.percentile(vals,12)); hi=float(np.percentile(vals,90))
                hn=np.clip((hl-lo)/max(8.0,hi-lo),0,1)-.5
            else:
                hn=0
        else:
            hn=0
        bevel=np.clip(dist/18,0,1)
        rim=np.clip(1-dist/9,0,1)
        lum=.28+.34*broad+.12*bevel-.12*rim+organic/42.0+.12*hn
        lum=np.clip(lum,0,1)
        lo=np.array(shadow,float); mi=np.array(mid,float); hi=np.array(high,float)
        rgb=np.empty((HI,HI,3),dtype=np.float32)
        lower=lum<.5
        t=np.clip(lum*2,0,1)
        rgb[lower]=lo+(mi-lo)*t[lower,None]
        t2=np.clip((lum-.5)*2,0,1)
        rgb[~lower]=mi+(hi-mi)*t2[~lower,None]
        # narrow moist/chitin catchlight
        spec=(np.clip((lum-.54)/.34,0,1)**2.4)*(0.4+0.6*bevel)
        rgb=np.clip(rgb+spec[...,None]*(np.array([28,33,30]) if not bone else np.array([20,20,21])),0,255)
        out=np.dstack([rgb.astype(np.uint8),(m*255).astype(np.uint8)])
        return Image.fromarray(out,"RGBA")

    def _membrane(self, mask, hist=None):
        p=self.palette
        return self._material(mask,p["membrane_shadow"],p["membrane_mid"],p["membrane_high"],"reptile",hist)

    def _plate(self, out, fullmask, bb, pts, hist, bone=False, seed=0):
        pm=ImageChops.multiply(self._poly_mask(bb,pts,2),fullmask)
        out.alpha_composite(self._chitin(pm,hist,bone,seed))
        return pm

    def _paint_warrior(self, body, direction, mask):
        bb=mask.getbbox(); x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
        hist=self._historical("WNG_WarriorCarapace",body,direction,bb)
        cmd=self._historical("WNG_CommanderCarapace",body,direction,bb)

        # Flexible black/reptile understructure remains visible between grown plates.
        out=self._membrane(mask,hist)

        if direction=="south":
            # Anatomical breast shell and clavicle plates.
            self._plate(out,mask,bb,[(.23,.10),(.42,.05),(.49,.19),(.45,.48),(.31,.57),(.18,.39)],hist,False,1)
            self._plate(out,mask,bb,[(.77,.10),(.58,.05),(.51,.19),(.55,.48),(.69,.57),(.82,.39)],hist,False,2)
            # Layered shoulder carapace from Wraith warrior/commander language.
            self._plate(out,mask,bb,[(.04,.08),(.22,.03),(.35,.10),(.30,.22),(.11,.24)],cmd,True,3)
            self._plate(out,mask,bb,[(.96,.08),(.78,.03),(.65,.10),(.70,.22),(.89,.24)],cmd,True,4)
            # Central sternum and segmented abdominal ribs.
            stern=self._line_mask([(int(x0+.50*w),int(y0+.17*h)),(int(x0+.49*w),int(y0+.69*h))],max(8,.035*w),1)
            stern=ImageChops.multiply(stern,mask); out.alpha_composite(self._chitin(stern,cmd,True,5))
            for i,yy in enumerate((.48,.58,.68,.78)):
                left=[(.27,yy),(.43,yy+.015),(.49,yy+.045)]
                right=[(.73,yy),(.57,yy+.015),(.51,yy+.045)]
                self._plate(out,mask,bb,left,hist,False,10+i)
                self._plate(out,mask,bb,right,hist,False,20+i)
            # Thigh/hip shell leaves membrane channels visible.
            self._plate(out,mask,bb,[(.18,.72),(.42,.70),(.44,.95),(.25,.96),(.13,.86)],hist,False,31)
            self._plate(out,mask,bb,[(.82,.72),(.58,.70),(.56,.95),(.75,.96),(.87,.86)],hist,False,32)
            # Tiny biotech node only.
            self._gem(out,int(x0+.50*w),int(y0+.31*h),max(3,int(.010*w)))

        elif direction=="north":
            self._plate(out,mask,bb,[(.05,.09),(.28,.03),(.43,.12),(.36,.27),(.12,.28)],cmd,True,40)
            self._plate(out,mask,bb,[(.95,.09),(.72,.03),(.57,.12),(.64,.27),(.88,.28)],cmd,True,41)
            # Grown spinal chain.
            for i,yy in enumerate((.18,.30,.42,.54,.66,.78)):
                self._plate(out,mask,bb,[(.44,yy-.035),(.50,yy-.065),(.56,yy-.035),(.54,yy+.045),(.46,yy+.045)],cmd,False,50+i)
            # Back ribs and lower flank plates.
            for i,yy in enumerate((.36,.50,.64)):
                self._plate(out,mask,bb,[(.19,yy),(.39,yy+.025),(.46,yy+.065),(.31,yy+.12)],hist,False,60+i)
                self._plate(out,mask,bb,[(.81,yy),(.61,yy+.025),(.54,yy+.065),(.69,yy+.12)],hist,False,70+i)
            self._plate(out,mask,bb,[(.19,.72),(.43,.70),(.44,.95),(.24,.96),(.13,.84)],hist,False,80)
            self._plate(out,mask,bb,[(.81,.72),(.57,.70),(.56,.95),(.76,.96),(.87,.84)],hist,False,81)

        else:
            # Side-facing shoulder crown and overlapping flank carapace.
            self._plate(out,mask,bb,[(.22,.07),(.58,.03),(.83,.15),(.78,.29),(.48,.31),(.30,.22)],cmd,True,90)
            self._plate(out,mask,bb,[(.34,.24),(.72,.25),(.75,.48),(.61,.58),(.38,.50)],hist,False,91)
            for i,yy in enumerate((.50,.62,.74)):
                self._plate(out,mask,bb,[(.34,yy),(.62,yy+.01),(.72,yy+.06),(.58,yy+.12),(.36,yy+.10)],hist,False,100+i)
            self._plate(out,mask,bb,[(.31,.72),(.64,.70),(.68,.94),(.43,.97),(.27,.86)],hist,False,110)
            # Organic seam/ridge.
            ridge=self._line_mask([(int(x0+.59*w),int(y0+.23*h)),(int(x0+.62*w),int(y0+.67*h))],max(6,.020*w),1)
            ridge=ImageChops.multiply(ridge,mask)
            out.alpha_composite(self._chitin(ridge,cmd,True,111))

        out=self._finish(out,mask)
        out.putalpha(mask)
        return out.resize((OUT,OUT),Image.Resampling.LANCZOS)

    def _tile_warrior(self, mask):
        bb=mask.getbbox(); x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
        hist=self._historical("WNG_WarriorCarapace","Male","south",bb)
        cmd=self._historical("WNG_CommanderCarapace","Male","south",bb)
        out=self._membrane(mask,hist)
        for pts,bone,seed in [
            ([(.23,.10),(.42,.05),(.49,.19),(.45,.48),(.31,.57),(.18,.39)],False,201),
            ([(.77,.10),(.58,.05),(.51,.19),(.55,.48),(.69,.57),(.82,.39)],False,202),
            ([(.04,.08),(.22,.03),(.35,.10),(.30,.22),(.11,.24)],True,203),
            ([(.96,.08),(.78,.03),(.65,.10),(.70,.22),(.89,.24)],True,204),
            ([(.18,.72),(.42,.70),(.44,.95),(.25,.96),(.13,.86)],False,205),
            ([(.82,.72),(.58,.70),(.56,.95),(.75,.96),(.87,.86)],False,206)
        ]:
            self._plate(out,mask,bb,pts,cmd if bone else hist,bone,seed)
        for i,yy in enumerate((.48,.59,.70)):
            self._plate(out,mask,bb,[(.27,yy),(.43,yy+.015),(.49,yy+.045)],hist,False,210+i)
            self._plate(out,mask,bb,[(.73,yy),(.57,yy+.015),(.51,yy+.045)],hist,False,220+i)
        self._gem(out,int(x0+.50*w),int(y0+.31*h),max(3,int(.010*w)))
        out=self._finish(out,mask)
        out=ImageEnhance.Contrast(out).enhance(1.12)
        out.putalpha(mask)
        return out.resize((OUT,OUT),Image.Resampling.LANCZOS)

    def _paint(self, body, direction, mask):
        if self.profile["id"]=="wraith_warrior_carapace":
            return self._paint_warrior(body,direction,mask)
        return self._paint_hunter(body,direction,mask)

    def _tile(self, mask):
        if self.profile["id"]=="wraith_warrior_carapace":
            return self._tile_warrior(mask)
        return self._tile_hunter(mask)

    def _symmetry(self, im):
        a=np.array(im.convert("L"),dtype=np.float32)
        b=np.fliplr(a)
        denom=max(1,float(np.mean(np.abs(a))+np.mean(np.abs(b))))
        return float(1-np.mean(np.abs(a-b))/denom)

    def _qa(self, generated: dict, impl: dict):
        q=self.s3["quality"]
        rows=[]
        for key,im in generated.items():
            if key=="tile":
                vm=impl["tile"].resize((OUT,OUT),Image.Resampling.LANCZOS)
            else:
                body,d=key.split("_",1)
                if d=="west":
                    vm=impl["masks"][body]["east"].resize((OUT,OUT),Image.Resampling.LANCZOS).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                else:
                    vm=impl["masks"][body][d].resize((OUT,OUT),Image.Resampling.LANCZOS)
            ga=np.array(im.getchannel("A"))>16; va=np.array(vm)>16
            inter=(ga&va).sum(); union=(ga|va).sum()
            iou=float(inter/max(1,union))
            rgb=np.array(im)[...,:3].astype(np.float32)
            lum=.2126*rgb[...,0]+.7152*rgb[...,1]+.0722*rgb[...,2]
            contrast=float(lum[ga].std()) if ga.any() else 0
            # Count only intentional cyan-biotech chroma. Wraith shell highlights
            # are green/grey; actual biotech nodes are blue > green > red.
            biotech=(rgb[...,2]>rgb[...,1]+4)&(rgb[...,1]>rgb[...,0]+14)&(lum>105)&ga
            lumfrac=float(biotech.sum()/max(1,ga.sum()))
            sym=self._symmetry(im)
            rows.append({"key":key,"iou":iou,"contrast":contrast,"luminous_fraction":lumfrac,"symmetry":sym})
            if iou<q["silhouette_iou_min"]: raise RuntimeError(f"QA silhouette {key}: {iou}")
            if lumfrac>q["max_luminous_area_fraction"]: raise RuntimeError(f"QA luminous area {key}: {lumfrac}")
            if contrast<q["min_local_contrast"]: raise RuntimeError(f"QA contrast {key}: {contrast}")
        return rows

    def run(self, preview: Path|None, workdir: Path):
        cfg=self.profile["stage_2_rimworld"]
        outdir=APPAREL_ROOT/self.profile.get("output_dir","Wraith")
        item=self.profile["item"]
        generated={}
        tile=self._tile(self.impl["tile"])
        generated["tile"]=tile
        save_clean(tile,outdir/f"{item}.png")
        for body in cfg["body_types"]:
            for d in ("south","north","east"):
                im=self._paint(body,d,self.impl["masks"][body][d])
                generated[f"{body}_{d}"]=im
                if d=="south":
                    save_clean(im,outdir/f"{item}_{body}.png")
                save_clean(im,outdir/f"{item}_{body}_{d}.png")
                if d=="east":
                    west=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                    generated[f"{body}_west"]=west
                    save_clean(west,outdir/f"{item}_{body}_west.png")

        if cfg.get("legacy_bare_directions"):
            save_clean(generated["Male_south"],outdir/f"{item}_south.png")
            save_clean(generated["Male_north"],outdir/f"{item}_north.png")
            save_clean(generated["Male_east"],outdir/f"{item}_east.png")
            save_clean(generated["Male_west"],outdir/f"{item}_west.png")

        qa=self._qa(generated,self.impl)
        workdir.mkdir(parents=True,exist_ok=True)
        (workdir/"synthesis_qa.json").write_text(json.dumps(qa,indent=2))
        if preview:
            order=["tile","Male_south","Male_north","Male_east","Female_south","Hulk_south"]
            sh=Image.new("RGBA",(OUT*3,OUT*2),(18,16,22,255))
            for i,k in enumerate(order):
                sh.alpha_composite(generated[k],((i%3)*OUT,(i//3)*OUT))
            preview.parent.mkdir(parents=True,exist_ok=True)
            sh.save(preview,optimize=True)

def save_clean(im: Image.Image, p: Path):
    p.parent.mkdir(parents=True,exist_ok=True)
    a=np.array(im.convert("RGBA")); a[a[...,3]==0,:3]=0
    Image.fromarray(a.astype(np.uint8),"RGBA").save(p,optimize=True)

def load_profile(path: Path):
    return json.loads(path.read_text())

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("profile",help="profile id, e.g. wraith_hunter_coat")
    ap.add_argument("--vanilla-dir",type=Path,required=True)
    ap.add_argument("--body-dir",type=Path)
    ap.add_argument("--workdir",type=Path,default=Path(".github/artgen/runtime"))
    ap.add_argument("--preview",type=Path)
    args=ap.parse_args()

    profile=load_profile(PROFILE_ROOT/f"{args.profile}.json")

    # ORDER IS ENFORCED: Stargate -> RimWorld -> synthesis.
    ref=StargateReferencePass(profile,args.workdir).run()
    impl=RimWorldImplementationPass(profile,args.vanilla_dir,args.body_dir).run()
    synth=SynthesisPass(profile,ref,impl)
    synth.run(args.preview,args.workdir)

    print(json.dumps({
        "status":"ok",
        "order":["StargateReferencePass","RimWorldImplementationPass","SynthesisPass"],
        "verified_stargate_sources":f"{ref.verified}/{ref.total}"
    }))

if __name__=="__main__":
    main()
