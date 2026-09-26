#!/usr/bin/env python3
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]

FAMILIES = {
    "Precursor": {
        "atlas": ROOT / "Textures/Things/Building/Precursor/Gravship/WNG_PrecursorHull_Atlas.png",
        "out": ROOT / "Textures/Things/Building/Precursor/Gravship/HullCorners",
    },
    "Wraith": {
        "atlas": ROOT / "Textures/Things/Building/Wraith/Gravship/WNG_OrganicHull_Atlas.png",
        "out": ROOT / "Textures/Things/Building/Wraith/Gravship/HullCorners",
    },
    "Goauld": {
        "atlas": ROOT / "Textures/Things/Building/Goauld/Gravship/WNG_GoauldHull_Atlas.png",
        "out": ROOT / "Textures/Things/Building/Goauld/Gravship/HullCorners",
    },
}

# The family wall atlases use RimWorld's 4x4 linked-wall layout.
# Index 10 is the left+right straight-wall cell.  We use that exact authored
# family wall texture as the source material for the Odyssey 2x2 outside-corner
# overlay, so the diagonal hull pieces are literally made from the same wall art.
HORIZONTAL_LINK_INDEX = 10

ORIENTATIONS = {
    "northeast": (-45, False, False),
    "northwest": (45, True, False),
    "southeast": (45, False, True),
    "southwest": (-45, True, True),
}

FULL_WIDTH = 292
PARTIAL_WIDTH = 215
CANVAS_SIZE = 256


def atlas_cell(image: Image.Image, index: int) -> Image.Image:
    cw = image.width // 4
    ch = image.height // 4
    col = index % 4
    row_from_top = 3 - (index // 4)
    return image.crop((col * cw, row_from_top * ch, (col + 1) * cw, (row_from_top + 1) * ch))


def diagonal_piece(source: Image.Image, width: int, thickness: int, angle: int,
                   flip_x: bool, flip_y: bool) -> Image.Image:
    strip = source.resize((width, thickness), Image.Resampling.LANCZOS)
    rotated = strip.rotate(angle, resample=Image.Resampling.BICUBIC, expand=True)

    canvas = Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), (0, 0, 0, 0))
    canvas.alpha_composite(
        rotated,
        ((CANVAS_SIZE - rotated.width) // 2, (CANVAS_SIZE - rotated.height) // 2),
    )

    if flip_x:
        canvas = canvas.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    if flip_y:
        canvas = canvas.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    return canvas


def main() -> None:
    for family, info in FAMILIES.items():
        image = Image.open(info["atlas"]).convert("RGBA")
        cell = atlas_cell(image, HORIZONTAL_LINK_INDEX)
        bbox = cell.getbbox()
        if bbox is None:
            raise RuntimeError(f"{family} horizontal wall atlas cell is empty")

        source = cell.crop(bbox)
        cell_height = image.height // 4

        # Preserve the straight wall's in-game visual thickness.  The source
        # atlas may be 512 or 1024 px; the Odyssey corner quad is always 256 px.
        thickness = max(24, round((source.height / cell_height) * (CANVAS_SIZE / 2)))

        out_dir = info["out"]
        out_dir.mkdir(parents=True, exist_ok=True)

        for direction, (angle, flip_x, flip_y) in ORIENTATIONS.items():
            full = diagonal_piece(source, FULL_WIDTH, thickness, angle, flip_x, flip_y)
            partial = diagonal_piece(source, PARTIAL_WIDTH, thickness, angle, flip_x, flip_y)

            full.save(out_dir / f"AngledGravshipHull_{direction}.png", "PNG", optimize=True)
            partial.save(out_dir / f"AngledGravshipHull_Partial_{direction}.png", "PNG", optimize=True)


if __name__ == "__main__":
    main()
