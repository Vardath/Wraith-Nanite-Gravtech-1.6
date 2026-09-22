from pathlib import Path
from PIL import Image
import base64
import hashlib

ROOT = Path(__file__).resolve().parents[2]
ASSET_DIR = ROOT / ".github" / "artgen" / "assets"
ASSET = ASSET_DIR / "alkesh_master.png"
OUT = ROOT / "Textures" / "Things" / "Building" / "Goauld" / "Shuttle"
OUT.mkdir(parents=True, exist_ok=True)

EXPECTED_SHA256 = "cd09944860af2a3db98b2c4a8c974fb982ce17d109e1caf34a136f6ffeb661bf"
PARTS = [ASSET_DIR / f"alkesh_master.b64.part{i:02d}" for i in range(1, 7)]

if not all(p.exists() for p in PARTS):
    missing = [str(p) for p in PARTS if not p.exists()]
    raise RuntimeError(f"Missing Al'kesh master transfer parts: {missing}")

encoded = "".join(p.read_text(encoding="utf-8").strip() for p in PARTS)
raw = base64.b64decode(encoded, validate=True)
digest = hashlib.sha256(raw).hexdigest()
if digest != EXPECTED_SHA256:
    raise RuntimeError(f"Al'kesh master checksum mismatch: expected {EXPECTED_SHA256}, got {digest}")

ASSET.write_bytes(raw)

with Image.open(ASSET) as source:
    source.load()
    if source.size != (512, 512):
        raise RuntimeError(f"Al'kesh master must be 512x512, got {source.size}")
    north = source.convert("RGBA")

alpha = north.getchannel("A")
bbox = alpha.getbbox()
if not bbox:
    raise RuntimeError("Al'kesh master has no visible pixels")
if bbox[0] <= 0 or bbox[1] <= 0 or bbox[2] >= 512 or bbox[3] >= 512:
    raise RuntimeError(f"Al'kesh master touches texture edge: {bbox}")

# RimWorld Graphic_Multi requires all four facings. The same authored ship is rotated
# so the silhouette, engine placement and hull identity remain coherent in every direction.
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
        a = check.getchannel("A")
        box = a.getbbox()
        if not box:
            raise RuntimeError(f"{path}: empty alpha")
        edges = [
            a.crop((0, 0, 512, 1)).getextrema()[1],
            a.crop((0, 511, 512, 512)).getextrema()[1],
            a.crop((0, 0, 1, 512)).getextrema()[1],
            a.crop((511, 0, 512, 512)).getextrema()[1],
        ]
        if any(edges):
            raise RuntimeError(f"{path}: alpha touches canvas edge")

for suffix, image in views.items():
    path = OUT / f"WNG_AlkeshTransport{suffix}.png"
    image.save(path, "PNG", optimize=True)
    validate(path)

required = [
    OUT / "WNG_AlkeshTransport.png",
    OUT / "WNG_AlkeshTransport_north.png",
    OUT / "WNG_AlkeshTransport_east.png",
    OUT / "WNG_AlkeshTransport_south.png",
    OUT / "WNG_AlkeshTransport_west.png",
]
for path in required:
    if not path.exists():
        raise RuntimeError(f"Missing Al'kesh production facing: {path}")

print("Installed verified professional Al'kesh art: base + north/east/south/west.")
