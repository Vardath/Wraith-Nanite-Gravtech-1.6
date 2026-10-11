"""Generate accurate Architect icons from existing WNG linked wall atlases.

The actual linked-wall textures and Unity MaterialAtlasPool indices are never
modified. Index 15 represents a four-way junction and occupies the top-right
sheet cell in PNG coordinates; only its padded center is used for the icon.
"""
from pathlib import Path
from PIL import Image

OUT = Path("Textures/UI/WNG/Build")
SOURCES = [
    ("Textures/Things/Building/Precursor/Gravship/WNG_PrecursorHull_Atlas.png",
     "WNG_PrecursorWall.png"),
    ("Textures/Things/Building/Wraith/Gravship/WNG_OrganicHull_Atlas.png",
     "WNG_WraithLivingWall.png"),
]
OUT.mkdir(parents=True, exist_ok=True)
for path, name in SOURCES:
    with Image.open(path) as file:
        atlas = file.convert("RGBA")
    w,h=atlas.size
    if w != h or w % 4:
        raise RuntimeError(f"{path}: expected square 4x4 linked texture")
    side=w//4
    padding=side//8
    icon=atlas.crop((3*side+padding,padding,4*side-padding,side-padding))
    if icon.size != (192,192):
        icon=icon.resize((192,192),Image.Resampling.LANCZOS)
    result=OUT/name
    icon.save(result,format="PNG",optimize=True)
    with Image.open(result) as verify:
        verify.verify()
    print("Generated", result, "from", path)
