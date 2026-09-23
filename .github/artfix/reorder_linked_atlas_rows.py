from pathlib import Path
from PIL import Image

# RimWorld MaterialAtlasPool maps link indices 0..15 from the BOTTOM texture row upward.
# These WNG atlases were authored in ordinary top-down row-major order (0..3 on the top row),
# so every linked wall/conduit selected the wrong directional tile.  Reorder only the four
# atlas row bands; never mirror, rotate, redraw, rescale, or otherwise alter the approved art.

ATLAS_PATHS = [
    Path("Textures/Things/Building/Goauld/Gravship/WNG_GoauldHull_Atlas.png"),
    Path("Textures/Things/Building/Precursor/Gravship/WNG_PrecursorHull_Atlas.png"),
    Path("Textures/Things/Building/Wraith/Gravship/WNG_OrganicHull_Atlas.png"),
    Path("Textures/Things/Building/Goauld/Gravship/WNG_GoauldPowerConduit_Atlas.png"),
    Path("Textures/Things/Building/Goauld/Gravship/WNG_GoauldFuelConduit_Atlas.png"),
    Path("Textures/Things/Building/Precursor/Gravship/WNG_AsuranPowerConduit_Atlas.png"),
    Path("Textures/Things/Building/Precursor/Gravship/WNG_AsuranFuelConduit_Atlas.png"),
    Path("Textures/Things/Building/Wraith/Gravship/WNG_WraithNeuralConduit_Atlas.png"),
    Path("Textures/Things/Building/Wraith/Gravship/WNG_WraithFuelConduit_Atlas.png"),
]


def reorder_material_atlas_rows(path: Path) -> None:
    if not path.exists():
        raise FileNotFoundError(path)

    with Image.open(path) as src:
        image = src.convert("RGBA")
        width, height = image.size
        if width <= 0 or height <= 0 or height % 4 != 0:
            raise ValueError(f"{path}: expected positive height divisible by four, got {image.size}")

        band_h = height // 4
        out = Image.new("RGBA", image.size, (0, 0, 0, 0))
        for source_row in range(4):
            band = image.crop((0, source_row * band_h, width, (source_row + 1) * band_h))
            destination_row = 3 - source_row
            out.paste(band, (0, destination_row * band_h))

        # Exact pixel movement only; no interpolation is performed.
        out.save(path, format="PNG", optimize=True)
        print(f"reordered linked-atlas rows: {path} ({width}x{height})")


for atlas_path in ATLAS_PATHS:
    reorder_material_atlas_rows(atlas_path)
