from pathlib import Path
from PIL import Image

WORLD = Path("Textures/Things/Building/Precursor/Shuttle")
STEM = "WNG_AsuranQueenRecoveryCarrier"
UI = Path("Textures/UI/WNG/Build/WNG_AsuranQueenRecoveryCarrier.png")

north_path = WORLD / f"{STEM}_north.png"
north = Image.open(north_path).convert("RGBA")
if north.size != (256, 256):
    raise ValueError(f"{north_path}: expected 256x256, got {north.size}")

variants = {
    "": north,
    "_north": north,
    "_east": north.transpose(Image.Transpose.ROTATE_270),
    "_south": north.transpose(Image.Transpose.ROTATE_180),
    "_west": north.transpose(Image.Transpose.ROTATE_90),
}

for suffix, image in variants.items():
    out = WORLD / f"{STEM}{suffix}.png"
    image.save(out, format="PNG", optimize=True)

bbox = north.getchannel("A").getbbox()
if bbox is None:
    raise ValueError("Asuran carrier master has no visible pixels")

subject = north.crop(bbox)
subject.thumbnail((220, 220), Image.Resampling.LANCZOS)
icon = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
icon.alpha_composite(subject, ((256-subject.width)//2, (256-subject.height)//2))
UI.parent.mkdir(parents=True, exist_ok=True)
icon.save(UI, format="PNG", optimize=True)

print("Finalized professional Asuran Queen Recovery Carrier world sprites and build icon.")
