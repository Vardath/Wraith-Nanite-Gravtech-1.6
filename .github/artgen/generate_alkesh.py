from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ASSET = ROOT / ".github" / "artgen" / "assets" / "alkesh_master.png"
OUT = ROOT / "Textures" / "Things" / "Building" / "Goauld" / "Shuttle"
OUT.mkdir(parents=True, exist_ok=True)

if not ASSET.exists():
    raise RuntimeError(f"Missing approved Al'kesh master: {ASSET}")

with Image.open(ASSET) as source:
    source.load()
    north = source.convert("RGBA")

if north.size != (512, 512):
    north.thumbnail((512, 512), Image.Resampling.LANCZOS)
    framed = Image.new("RGBA", (512, 512), (0, 0, 0, 0))
    framed.alpha_composite(north, ((512 - north.width) // 2, (512 - north.height) // 2))
    north = framed

# One coherent screen-inspired Al'kesh hull, rendered for every RimWorld Graphic_Multi facing.
# North is the authored master; the other facings preserve the exact same ship and geometry.
views = {
    "": north,
    "_north": north,
    "_east": north.transpose(Image.Transpose.ROTATE_270),
    "_south": north.transpose(Image.Transpose.ROTATE_180),
    "_west": north.transpose(Image.Transpose.ROTATE_90),
}


def validate(path: Path):
    with Image.open(path) as check:
        check.load()
        if check.mode != "RGBA" or check.size != (512, 512):
            raise RuntimeError(f"{path}: expected 512x512 RGBA, got {check.mode} {check.size}")
        alpha = check.getchannel("A")
        bbox = alpha.getbbox()
        if not bbox:
            raise RuntimeError(f"{path}: empty alpha")
        if bbox[0] <= 0 or bbox[1] <= 0 or bbox[2] >= 512 or bbox[3] >= 512:
            raise RuntimeError(f"{path}: art touches canvas edge: {bbox}")


for suffix, image in views.items():
    path = OUT / f"WNG_AlkeshTransport{suffix}.png"
    image.save(path, "PNG", optimize=True)
    validate(path)

# Confirm the complete production family exists. Graphic_Multi and the shuttle/world/bombing defs all
# point to this same family, so losing a facing is a gameplay-facing regression, not just an art issue.
required = [
    OUT / "WNG_AlkeshTransport.png",
    OUT / "WNG_AlkeshTransport_north.png",
    OUT / "WNG_AlkeshTransport_east.png",
    OUT / "WNG_AlkeshTransport_south.png",
    OUT / "WNG_AlkeshTransport_west.png",
]
for path in required:
    if not path.exists():
        raise RuntimeError(f"Missing production facing: {path}")

print("Installed professional Al'kesh Graphic_Multi family: base + north/east/south/west from one coherent long 5x7 hull master.")
