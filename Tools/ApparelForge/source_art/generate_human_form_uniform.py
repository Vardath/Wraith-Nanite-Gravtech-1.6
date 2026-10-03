from __future__ import annotations

from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "ArtSource" / "Apparel" / "human_form_uniform"
OUT.mkdir(parents=True, exist_ok=True)

S = 1536  # Step 5 master resolution; torso-only pawn art
PALE = np.array([156, 166, 170], dtype=np.float32)
MID = np.array([112, 124, 129], dtype=np.float32)
GRAPH = (27, 33, 37, 255)
GRAPH2 = (45, 53, 57, 255)
LIGHT = (208, 215, 216, 255)
METAL = (128, 139, 142, 255)
TEAL = (70, 102, 108, 255)

def garment_mask(view: str) -> Image.Image:
    m = Image.new("L", (S, S), 0)
    d = ImageDraw.Draw(m)
    if view in ("south", "north"):
        pts = [
            (450,315),(565,250),(768,220),(971,250),(1086,315),
            (1110,520),(1045,1040),(920,1190),(768,1235),
            (616,1190),(491,1040),(426,520)
        ]
        d.polygon(pts, fill=255)
        d.ellipse([450,245,1086,670], fill=255)
        d.rounded_rectangle([450,400,1086,1120], radius=180, fill=255)
    else:
        pts = [
            (560,315),(665,255),(820,240),(960,310),(1005,520),
            (950,1040),(830,1190),(700,1210),(595,1110),(530,660)
        ]
        d.polygon(pts, fill=255)
        d.ellipse([555,250,985,650], fill=255)
        d.rounded_rectangle([545,410,985,1120], radius=155, fill=255)
    return m.filter(ImageFilter.GaussianBlur(2.0))

def base_fabric(mask: Image.Image, seed: int) -> Image.Image:
    yy, xx = np.mgrid[0:S, 0:S]
    t = np.clip((yy - 220) / 1020.0, 0.0, 1.0)[..., None]
    rgb = PALE * (1.0 - t) + MID * t
    grain = (((xx * 17 + yy * 29 + seed * 37) % 31) - 15).astype(np.float32)
    weave = np.where(((xx + yy) // 8) % 2 == 0, 4.0, -3.0)
    noise = (grain * 0.35 + weave)[..., None]
    rgb = np.clip(rgb + noise, 0, 255)

    # Broad material lighting: pale engineered fabric, not glossy plate.
    nx = (xx - 650) / 900.0
    ny = (yy - 480) / 1100.0
    light = np.clip(1.0 - (nx + 0.25) ** 2 - (ny + 0.15) ** 2, 0.0, 1.0)
    shade = np.clip((xx / S) * 0.45 + (yy / S) * 0.28 - 0.28, 0.0, 1.0)
    rgb = np.clip(rgb + light[..., None] * 28.0 - shade[..., None] * 38.0, 0, 255)

    a = np.array(mask, dtype=np.uint8)
    rgba = np.dstack([rgb.astype(np.uint8), a])
    return Image.fromarray(rgba, "RGBA")

def line(d: ImageDraw.ImageDraw, pts, fill, width):
    d.line(pts, fill=fill, width=width, joint="curve")

def render(view: str) -> Image.Image:
    mask = garment_mask(view)
    im = base_fabric(mask, {"south":19, "north":31, "east":47}[view])
    det = Image.new("RGBA", (S, S), (0,0,0,0))
    d = ImageDraw.Draw(det)

    if view == "south":
        # High collar; no humanoid neck, arms or legs.
        d.polygon([(650,272),(768,220),(886,272),(860,372),(676,372)], fill=GRAPH)
        d.polygon([(690,285),(768,252),(846,285),(828,336),(708,336)], fill=GRAPH2)
        line(d, [(680,370),(768,410),(856,370)], LIGHT, 12)

        # Fitted Asuran / Ancient seam language. These are tailored seams, not armour plates.
        for yy, span in [(420,235),(510,270),(610,300)]:
            line(d, [(768-span,yy),(768,yy+92),(768+span,yy)], GRAPH2, 30)
            line(d, [(768-span+24,yy+3),(768,yy+72),(768+span-24,yy+3)], METAL, 9)

        d.polygon([(462,470),(585,420),(620,1090),(520,1030)], fill=GRAPH2)
        d.polygon([(1074,470),(951,420),(916,1090),(1016,1030)], fill=GRAPH2)
        line(d, [(768,380),(768,1035)], GRAPH, 22)
        line(d, [(768,390),(768,1015)], METAL, 6)
        d.rounded_rectangle([700,930,836,980], radius=15, fill=GRAPH)
        d.rounded_rectangle([728,944,808,968], radius=8, fill=METAL)

        line(d, [(610,835),(768,930),(926,835)], TEAL, 13)
        line(d, [(642,846),(768,914),(894,846)], LIGHT, 6)
        d.arc([425,300,710,650], 195,318, fill=GRAPH2, width=28)
        d.arc([826,300,1111,650], 222,345, fill=GRAPH2, width=28)
        d.arc([445,320,700,625], 200,312, fill=LIGHT, width=7)
        d.arc([836,320,1091,625], 228,340, fill=LIGHT, width=7)

    elif view == "north":
        d.polygon([(650,280),(768,232),(886,280),(858,360),(678,360)], fill=GRAPH)
        line(d, [(768,360),(768,1065)], GRAPH2, 24)
        line(d, [(768,370),(768,1045)], METAL, 6)
        for yy, span in [(430,225),(535,260),(650,285),(770,255)]:
            line(d, [(768-span,yy),(768,yy+84),(768+span,yy)], GRAPH2, 29)
            line(d, [(768-span+24,yy+4),(768,yy+64),(768+span-24,yy+4)], METAL, 8)
        d.polygon([(462,475),(575,430),(610,1080),(520,1020)], fill=GRAPH2)
        d.polygon([(1074,475),(961,430),(926,1080),(1016,1020)], fill=GRAPH2)
        d.arc([435,315,700,630],198,315, fill=GRAPH2, width=26)
        d.arc([836,315,1101,630],225,342, fill=GRAPH2, width=26)
        line(d, [(635,910),(768,985),(901,910)], TEAL, 11)

    else:
        d.polygon([(610,292),(706,250),(832,270),(914,322),(885,392),(670,385)], fill=GRAPH)
        d.polygon([(588,470),(690,410),(725,1110),(610,1055)], fill=GRAPH2)
        line(d, [(718,390),(742,620),(720,1040)], GRAPH, 24)
        line(d, [(722,398),(746,620),(724,1025)], METAL, 6)
        d.arc([600,400,1010,910],205,326, fill=GRAPH2, width=34)
        d.arc([628,430,985,885],210,320, fill=LIGHT, width=8)
        d.rounded_rectangle([680,930,805,978], radius=14, fill=GRAPH)
        d.rounded_rectangle([706,943,780,966], radius=7, fill=METAL)
        line(d, [(700,805),(806,865),(900,815)], TEAL, 11)

    # Fine stitch / nanite-weave detail.
    for i in range(96):
        x = 500 + ((i * 137 + 53) % 530)
        y = 390 + ((i * 211 + 29) % 690)
        if mask.getpixel((x,y)) > 128:
            col = (220,224,223,70) if i % 3 else (36,44,48,80)
            d.ellipse([x,y,x+3,y+3], fill=col)

    det.putalpha(Image.composite(det.getchannel("A"), Image.new("L",(S,S),0), mask))
    im = Image.alpha_composite(im, det)

    edge = mask.filter(ImageFilter.FIND_EDGES).filter(ImageFilter.GaussianBlur(1.5))
    edge_rg = Image.new("RGBA",(S,S),(196,202,201,0))
    edge_rg.putalpha(edge.point(lambda p: min(38, p // 4)))
    return Image.alpha_composite(im, edge_rg)

def main():
    for view in ("south","north","east"):
        out = render(view)
        out.save(OUT / f"master_{view}.png", optimize=True, compress_level=9)
        print(f"wrote {OUT / ('master_' + view + '.png')}")

if __name__ == "__main__":
    main()
