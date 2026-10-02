from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance, ImageChops
from bs4 import BeautifulSoup
import argparse, hashlib, io, json, math, random, re, subprocess
import numpy as np
import requests
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
    def __init__(self, profile: dict, vanilla_dir: Path):
        self.profile=profile
        self.cfg=profile["stage_2_rimworld"]
        self.vanilla_dir=vanilla_dir

    def _visible_mask(self, path: Path) -> Image.Image:
        im=Image.open(path).convert("RGBA").resize((HI,HI),Image.Resampling.LANCZOS)
        a=np.array(im.getchannel("A"))
        rgb=np.array(im)[...,:3]
        m=((a>8)&(rgb.max(axis=2)>28)).astype(np.uint8)*255
        return Image.fromarray(m,"L").filter(ImageFilter.GaussianBlur(.45))

    def run(self) -> dict:
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
        target=float(np.median(vals)) if vals else 32.0
        a=np.array(out,dtype=np.float32)
        m=np.array(mask)>16
        rgb=a[...,:3]
        lum=.2126*rgb[...,0]+.7152*rgb[...,1]+.0722*rgb[...,2]
        cur=lum[m].std() if m.any() else 1
        target=min(target,36.0)
        scale=np.clip(target/max(cur,1),1.00,2.20)
        mean=rgb[m].mean(axis=0) if m.any() else np.array([45,48,46])
        rgb=(rgb-mean)*scale+mean
        a[...,:3]=np.clip(rgb,0,255)
        a[...,3]=np.array(mask)
        out=Image.fromarray(a.astype(np.uint8),"RGBA")
        out=ImageEnhance.Contrast(out).enhance(1.18)
        out=out.filter(ImageFilter.UnsharpMask(radius=1.55,percent=88,threshold=3))
        out.putalpha(mask)
        return out

    def _paint(self, body, direction, mask):
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

    def _tile(self, mask):
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
        return out.resize((OUT,OUT),Image.Resampling.LANCZOS)

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
            biotech=np.all(rgb>np.array([135,130,190]),axis=2)&ga
            lumfrac=float(biotech.sum()/max(1,ga.sum()))
            sym=self._symmetry(im)
            rows.append({"key":key,"iou":iou,"contrast":contrast,"luminous_fraction":lumfrac,"symmetry":sym})
            if iou<q["silhouette_iou_min"]: raise RuntimeError(f"QA silhouette {key}: {iou}")
            if lumfrac>q["max_luminous_area_fraction"]: raise RuntimeError(f"QA luminous area {key}: {lumfrac}")
            if contrast<q["min_local_contrast"]: raise RuntimeError(f"QA contrast {key}: {contrast}")
        return rows

    def run(self, preview: Path|None, workdir: Path):
        cfg=self.profile["stage_2_rimworld"]
        outdir=APPAREL_ROOT/"Wraith"
        generated={}
        tile=self._tile(self.impl["tile"])
        generated["tile"]=tile
        save_clean(tile,outdir/"WNG_HunterCoat.png")
        for body in cfg["body_types"]:
            for d in ("south","north","east"):
                im=self._paint(body,d,self.impl["masks"][body][d])
                generated[f"{body}_{d}"]=im
                if d=="south": save_clean(im,outdir/f"WNG_HunterCoat_{body}.png")
                save_clean(im,outdir/f"WNG_HunterCoat_{body}_{d}.png")
                if d=="east":
                    west=im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                    generated[f"{body}_west"]=west
                    save_clean(west,outdir/f"WNG_HunterCoat_{body}_west.png")

        qa=self._qa(generated,self.impl)
        workdir.mkdir(parents=True,exist_ok=True)
        (workdir/"synthesis_qa.json").write_text(json.dumps(qa,indent=2))
        if preview:
            order=["tile","Male_south","Male_north","Male_east","Female_south","Hulk_south"]
            sh=Image.new("RGBA",(OUT*3,OUT*2),(18,16,22,255))
            for i,k in enumerate(order): sh.alpha_composite(generated[k],((i%3)*OUT,(i//3)*OUT))
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
    ap.add_argument("--workdir",type=Path,default=Path(".github/artgen/runtime"))
    ap.add_argument("--preview",type=Path)
    args=ap.parse_args()

    profile=load_profile(PROFILE_ROOT/f"{args.profile}.json")

    # ORDER IS ENFORCED: Stargate -> RimWorld -> synthesis.
    ref=StargateReferencePass(profile,args.workdir).run()
    impl=RimWorldImplementationPass(profile,args.vanilla_dir).run()
    synth=SynthesisPass(profile,ref,impl)
    synth.run(args.preview,args.workdir)

    print(json.dumps({
        "status":"ok",
        "order":["StargateReferencePass","RimWorldImplementationPass","SynthesisPass"],
        "verified_stargate_sources":f"{ref.verified}/{ref.total}"
    }))

if __name__=="__main__":
    main()
