from pathlib import Path
from PIL import Image
import json, re, collections

ROOT=Path("Textures/Things/Pawn/Humanlike/Apparel")
rows=[]
for p in sorted(ROOT.rglob("*.png")):
    rec={"path":str(p)}
    try:
        im=Image.open(p).convert("RGBA"); im.load()
    except Exception as e:
        rec["error"]=repr(e); rows.append(rec); continue
    w,h=im.size; a=im.getchannel("A"); bbox=a.getbbox()
    rec["size"]=[w,h]
    if bbox:
        cx=(bbox[0]+bbox[2]-1)/2; cy=(bbox[1]+bbox[3]-1)/2
        rec["bbox"]=bbox
        rec["margins"]=[bbox[0],bbox[1],w-bbox[2],h-bbox[3]]
        rec["center_offset"]=[round(cx-(w-1)/2,2),round(cy-(h-1)/2,2)]
    else:
        rec["bbox"]=None; rec["center_offset"]=None
    edge=0
    for x in range(w):
        edge += int(a.getpixel((x,0))>0) + int(a.getpixel((x,h-1))>0)
    for y in range(1,h-1):
        edge += int(a.getpixel((0,y))>0) + int(a.getpixel((w-1,y))>0)
    rec["edge_alpha_pixels"]=edge
    rec["corner_alpha"]=[a.getpixel((0,0)),a.getpixel((w-1,0)),a.getpixel((0,h-1)),a.getpixel((w-1,h-1))]
    rec["nonzero_ratio"]=round(sum(v>0 for v in a.getdata())/(w*h),4)
    rows.append(rec)

print("WNG_APPAREL_AUDIT_START")
for r in rows:
    print(json.dumps(r,sort_keys=True))
print("WNG_APPAREL_AUDIT_END")
print("WNG_APPAREL_COUNT",len(rows))
bad=[r for r in rows if "error" in r or r.get("edge_alpha_pixels",0)>0 or (r.get("center_offset") and (abs(r["center_offset"][0])>12 or abs(r["center_offset"][1])>12))]
print("WNG_APPAREL_FLAGGED",len(bad))
for r in bad:
    print("WNG_APPAREL_FLAG",json.dumps(r,sort_keys=True))
