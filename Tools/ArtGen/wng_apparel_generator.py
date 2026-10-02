from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageEnhance
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
                row["ok"]=all(row["terms"].values()) if row["terms"] else True
                if row["ok"]: verified+=1
                if src.get("image_policy")!="metadata_only":
                    for u in self._candidate_images(r.text,src["url"]):
                        st=self._download_image_stats(u)
                        if st: image_stats.append(st)
            except Exception as e:
                row["error"]=str(e)[:300]
            pages.append(row)

        minimum=max(3,math.ceil(len(self.cfg["sources"])*0.5))
        if verified < minimum:
            raise RuntimeError(f"StargateReferencePass failed: {verified}/{len(self.cfg['sources'])} verified; need {minimum}")
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
        m=np.array(mask,dtype=np.float32)/255
        yy,xx=np.mgrid[0:HI,0:HI]
        broad=(1-yy/HI)*.65+(1-xx/HI)*.35
        n1=self._noise(1.0 if kind=="leather" else 1.8)
        n2=self._noise(5.5 if kind=="leather" else 8.0)
        n3=self._noise(22)
        grain=4.5*n1+7*n2+4*n3
        if kind=="leather":
            grain += 3*np.sin((xx+1.7*yy)/19.0)
        else:
            grain += 5*np.cos((xx-.8*yy)/28.0)

        fold=np.zeros((HI,HI),dtype=np.float32)
        if hist is not None:
            hl=np.array(hist.convert("L"),dtype=np.float32)
            fold=(hl-gaussian_filter(hl,7))*.42+(gaussian_filter(hl,18)-gaussian_filter(hl,45))*.28

        dist=distance_transform_edt(m>0.1)
        edge=np.clip(1-dist/14,0,1)
        lum=np.clip(.42+.24*broad+grain/120+fold/255-.18*edge,0,1)

        lo=np.array(shadow,float); mi=np.array(mid,float); hi=np.array(high,float)
        rgb=np.empty((HI,HI,3),dtype=np.float32)
        lower=lum<.5
        t=np.clip(lum*2,0,1)
        rgb[lower]=lo+(mi-lo)*t[lower,None]
        t2=np.clip((lum-.5)*2,0,1)
        rgb[~lower]=mi+(hi-mi)*t2[~lower,None]
        out=np.dstack([np.clip(rgb,0,255).astype(np.uint8),(m*255).astype(np.uint8)])
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

    def _bone(self, mask, hist=None):
        p=self.palette
        return self._material(mask,p["bone_shadow"],p["bone_mid"],p["bone_high"],"bone",hist)

    def _leather(self, mask, hist=None, contrast=False):
        p=self.palette
        if contrast:
            sh=[max(0,x+7) for x in p["leather_shadow"]]
            md=[min(255,x+15) for x in p["leather_mid"]]
            hi=[min(255,x+18) for x in p["leather_high"]]
        else:
            sh=p["leather_shadow"]; md=p["leather_mid"]; hi=p["leather_high"]
        return self._material(mask,sh,md,hi,"leather",hist)

    def _stitch(self, im, pts, spacing=18):
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
                    t=remain/max(1,L); x=a[0]+(b[0]-a[0])*t; y=a[1]+(b[1]-a[1])*t
                    d.ellipse((x-1.5,y-1.5,x+1.5,y+1.5),fill=(137,124,134,170)); break
                remain-=L
            pos+=spacing

    def _gem(self, im,x,y,r):
        col=tuple(self.palette["biotech"])
        g=Image.new("RGBA",(HI,HI),(0,0,0,0)); gd=ImageDraw.Draw(g)
        gd.ellipse((x-r*4,y-r*4,x+r*4,y+r*4),fill=col+(40,))
        im.alpha_composite(g.filter(ImageFilter.GaussianBlur(r*2)))
        d=ImageDraw.Draw(im)
        d.ellipse((x-r,y-r,x+r,y+r),fill=(218,211,248,238),outline=(70,62,91,255),width=max(1,r//3))

    def _paint(self, body, direction, mask):
        bb=mask.getbbox(); x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
        hist=self._historical("WNG_HunterCoat",body,direction,bb)
        accent=self._historical("WNG_QueenRaiment",body,direction,bb)
        out=self._leather(mask,hist,False)

        # Canon-derived three-piece construction: body + two contrast-leather side/lapel pieces.
        if direction=="south":
            left=self._poly_mask(bb,[(.08,.11),(.40,.04),(.47,.26),(.40,.78),(.16,.92),(.10,.54)],4)
            right=self._poly_mask(bb,[(.92,.11),(.60,.04),(.53,.26),(.60,.78),(.84,.92),(.90,.54)],4)
            left=ImageChops.multiply(left,mask); right=ImageChops.multiply(right,mask)
            out.alpha_composite(self._clip(self._leather(left,hist,True),left))
            out.alpha_composite(self._clip(self._leather(right,accent,True),right))

            # restrained bone shoulder/collar accents, inside vanilla Duster silhouette.
            boneL=self._poly_mask(bb,[(.06,.10),(.29,.04),(.41,.13),(.34,.26),(.14,.28)],3)
            boneR=self._poly_mask(bb,[(.94,.10),(.71,.04),(.59,.13),(.66,.26),(.86,.28)],3)
            for bm in (boneL,boneR):
                bm=ImageChops.multiply(bm,mask)
                out.alpha_composite(self._bone(bm,hist))

            # diagonal waist belt and asymmetrical closure; no neon piping.
            belt=[(int(x0+.19*w),int(y0+.57*h)),(int(x0+.49*w),int(y0+.61*h)),(int(x0+.80*w),int(y0+.55*h))]
            bmask=ImageChops.multiply(self._line_mask(belt,max(8,.032*h),1.2),mask)
            out.alpha_composite(self._clip(self._leather(bmask,accent,True),bmask))
            self._stitch(out,[(int(x0+.25*w),int(y0+.28*h)),(int(x0+.29*w),int(y0+.84*h))],max(14,.05*h))
            self._gem(out,int(x0+.47*w),int(y0+.36*h),max(3,int(.012*w)))

        elif direction=="north":
            shoulder=self._poly_mask(bb,[(.07,.09),(.34,.03),(.45,.14),(.38,.29),(.14,.29)],3)
            shoulder2=Image.fromarray(np.fliplr(np.array(shoulder)).copy(),"L")
            for bm in (shoulder,shoulder2):
                bm=ImageChops.multiply(bm,mask)
                out.alpha_composite(self._bone(bm,hist))
            spine=[(int(x0+.50*w),int(y0+.15*h)),(int(x0+.49*w),int(y0+.76*h))]
            sm=ImageChops.multiply(self._line_mask(spine,max(6,.018*w),1),mask)
            out.alpha_composite(self._clip(self._leather(sm,accent,True),sm))
            self._stitch(out,[(int(x0+.27*w),int(y0+.28*h)),(int(x0+.25*w),int(y0+.80*h))],max(14,.05*h))

        else:
            bone=self._poly_mask(bb,[(.20,.08),(.69,.04),(.87,.20),(.73,.34),(.51,.29)],3)
            bone=ImageChops.multiply(bone,mask)
            out.alpha_composite(self._bone(bone,hist))
            seam=[(int(x0+.58*w),int(y0+.22*h)),(int(x0+.62*w),int(y0+.70*h))]
            sm=ImageChops.multiply(self._line_mask(seam,max(5,.018*w),1),mask)
            out.alpha_composite(self._clip(self._leather(sm,accent,True),sm))
            self._stitch(out,[(int(x0+.36*w),int(y0+.29*h)),(int(x0+.38*w),int(y0+.79*h))],max(14,.05*h))

        # Professional readability treatment benchmarked against WNG ships, without copying their pixels.
        target_contrast=np.mean([x["contrast"] for x in self.benchmark_stats]) if self.benchmark_stats else 35
        a=np.array(out,dtype=np.float32); m=np.array(mask)>16
        rgb=a[...,:3]; lum=.2126*rgb[...,0]+.7152*rgb[...,1]+.0722*rgb[...,2]
        cur=lum[m].std() if m.any() else 1
        scale=np.clip(target_contrast/max(cur,1),.92,1.28)
        mean=rgb[m].mean(axis=0) if m.any() else np.array([60,50,65])
        rgb=(rgb-mean)*scale+mean
        a[...,:3]=np.clip(rgb,0,255)
        a[...,3]=np.array(mask)
        out=Image.fromarray(a.astype(np.uint8),"RGBA")
        out=out.filter(ImageFilter.UnsharpMask(radius=3.0,percent=85,threshold=4))
        out=ImageEnhance.Contrast(out).enhance(1.06)
        out.putalpha(mask)
        return out.resize((OUT,OUT),Image.Resampling.LANCZOS)

    def _tile(self, mask):
        bb=mask.getbbox()
        hist=self._historical("WNG_HunterCoat","Male","south",bb)
        out=self._leather(mask,hist,False)
        x0,y0,x1,y1=bb; w=x1-x0; h=y1-y0
        for pts in (
            [(.07,.10),(.32,.03),(.43,.17),(.34,.31),(.12,.30)],
            [(.93,.10),(.68,.03),(.57,.17),(.66,.31),(.88,.30)]
        ):
            bm=ImageChops.multiply(self._poly_mask(bb,pts,3),mask)
            out.alpha_composite(self._bone(bm,hist))
        belt=[(int(x0+.20*w),int(y0+.58*h)),(int(x0+.50*w),int(y0+.61*h)),(int(x0+.80*w),int(y0+.56*h))]
        bm=ImageChops.multiply(self._line_mask(belt,max(8,.03*h),1),mask)
        out.alpha_composite(self._clip(self._leather(bm,hist,True),bm))
        self._gem(out,int(x0+.47*w),int(y0+.36*h),max(3,int(.012*w)))
        out.putalpha(mask)
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
            if key=="tile": vm=impl["tile"].resize((OUT,OUT),Image.Resampling.LANCZOS)
            else:
                body,d=key.split("_",1)
                srcd="east" if d=="west" else d
                vm=impl["masks"][body][srcd].resize((OUT,OUT),Image.Resampling.LANCZOS)
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
