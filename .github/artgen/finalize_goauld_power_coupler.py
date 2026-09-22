from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
PATH = ROOT / "Textures/Things/Building/Goauld/Gravship/WNG_GoauldPowerCoupler.png"

def transparent_edges(alpha):
    w, h = alpha.size
    return [
        alpha.crop((0, 0, w, 1)).getextrema()[1],
        alpha.crop((0, h - 1, w, h)).getextrema()[1],
        alpha.crop((0, 0, 1, h)).getextrema()[1],
        alpha.crop((w - 1, 0, w, h)).getextrema()[1],
    ]

with Image.open(PATH) as source:
    source.load()
    if source.size != (512, 512):
        raise RuntimeError(f"Unexpected coupler size: {source.size}")
    image = source.convert("RGBA")

# Canonical transparent RGB prevents edge halos.
px = image.load()
for y in range(image.height):
    for x in range(image.width):
        r, g, b, a = px[x, y]
        if a == 0:
            px[x, y] = (0, 0, 0, 0)

alpha = image.getchannel("A")
bbox = alpha.getbbox()
if bbox is None:
    raise RuntimeError("Coupler alpha is empty")
if any(transparent_edges(alpha)):
    raise RuntimeError("Coupler alpha touches canvas edge")

# Centre the visible 1x1 building mass on the sprite canvas.
cx = (bbox[0] + bbox[2]) / 2
cy = (bbox[1] + bbox[3]) / 2
dx = round(256 - cx)
dy = round(256 - cy)
if dx or dy:
    canvas = Image.new("RGBA", image.size, (0, 0, 0, 0))
    canvas.alpha_composite(image, (dx, dy))
    image = canvas

image.save(PATH, "PNG", optimize=True)

with Image.open(PATH) as check:
    check.load()
    if check.mode != "RGBA" or check.size != (512, 512):
        raise RuntimeError(f"Final coupler must be 512x512 RGBA, got {check.size} {check.mode}")
    alpha = check.getchannel("A")
    bbox = alpha.getbbox()
    if bbox is None or any(transparent_edges(alpha)):
        raise RuntimeError(f"Invalid final alpha bounds: {bbox}")
    cx = (bbox[0] + bbox[2]) / 2
    cy = (bbox[1] + bbox[3]) / 2
    if abs(cx - 256) > 2 or abs(cy - 256) > 2:
        raise RuntimeError(f"Coupler not centred: bbox={bbox} centre={(cx, cy)}")

print(f"Finalized Goa'uld power coupler: 512x512 RGBA, bbox={bbox}, centre={(cx, cy)}")
