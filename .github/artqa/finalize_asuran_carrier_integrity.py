from pathlib import Path
from PIL import Image

ROOT=Path("Textures/Things/Building/Precursor/Shuttle")
NAMES=[
"WNG_AsuranQueenRecoveryCarrier.png",
"WNG_AsuranQueenRecoveryCarrier_north.png",
"WNG_AsuranQueenRecoveryCarrier_east.png",
"WNG_AsuranQueenRecoveryCarrier_south.png",
"WNG_AsuranQueenRecoveryCarrier_west.png",
]

def center_rgba(p):
    im=Image.open(p).convert("RGBA")
    alpha=im.getchannel("A")
    bbox=alpha.getbbox()
    if not bbox:
        raise ValueError(f"{p}: empty alpha")
    cx=(bbox[0]+bbox[2]-1)/2
    cy=(bbox[1]+bbox[3]-1)/2
    tx=round((im.width-1)/2-cx)
    ty=round((im.height-1)/2-cy)
    out=Image.new("RGBA",im.size,(0,0,0,0))
    out.alpha_composite(im,(tx,ty))
    px=out.load()
    for y in range(out.height):
        for x in range(out.width):
            r,g,b,a=px[x,y]
            if a==0 and (r or g or b):
                px[x,y]=(0,0,0,0)
    out.save(p,format="PNG",optimize=True)

for n in NAMES:
    p=ROOT/n
    center_rgba(p)
    Image.open(p).verify()
print("Centered and fully decoded all five Asuran recovery-carrier sprites without rotating authored facings.")
