# WNG walls for RimWorld 1.6

The five functional walls use three material families but four linked atlases.
Wraith settlement art is independent from the Wraith gravship hull so each wall
can be redesigned and reviewed individually.

| Family | ThingDefs | Runtime atlas |
| --- | --- | --- |
| Wraith settlement | `WNG_WraithLivingWall` | `Wraith/Structure/WNG_LivingWall_Atlas` |
| Wraith gravship | `WNG_OrganicGravshipHull` | `Wraith/Gravship/WNG_OrganicHull_Atlas` |
| Asuran / Precursor | `WNG_PrecursorWall`, `WNG_PrecursorGravshipHull` | `Precursor/Gravship/WNG_PrecursorHull_Atlas` |
| Goa'uld | `WNG_GoauldGravshipHull` | `Goauld/Gravship/WNG_GoauldHull_Atlas` |

Atlas paths are under `Textures/Things/Building`. Wall lumens are attached lights,
and `WNG_ReplicatorStrippedWallHusk` is pass-through ruin scenery; neither is a
functional wall and neither belongs in the linked atlas pipeline.

## Geometry and native hooks

Each 1024×1024 RGBA atlas contains sixteen 256-pixel cells. RimWorld's
`MaterialAtlasPool` samples the central 192×192 pixels of each cell, inset by
32 pixels. Masks use N=1, E=2, S=4, W=8; PNG row is `3 - mask // 4`.
The native renderer prints that sampled area on an exact 1×1 map cell.

Older gravship/Precursor/Goa'uld isolated walls retain their earlier six-pixel
insets (87.9% sampled coverage). The Wraith settlement wall is now made of 16
distinct painterly architectural silhouettes: continuous longitudinal chitin
ribs, visible membrane walls, projecting terminal posts, curved junctions,
thick cross/T connections and hand-painted highlights. Native size stays
1x1; transparency outside the physical wall is intentional. Compatible linked
edge ports have bit-identical RGBA boundaries, and each atlas gutter clamps
its sampled edge. The old 100%-filled square cap is no longer the runtime art.

All walls retain `Wall` inheritance, `CornerFiller`, Wall/Rock flags, native
roof/room/pathing/attachment/replacement behavior, damage marks and painting.
The two fixed-material settlement walls explicitly seal vacuum without a
stuff-dependent airtight query. Existing hull materials, recipes, abilities,
stats, research and placement workers are preserved.

The settlement artwork is sourced from
`ArtSource/Walls/Wraith/wraith_architectural_painting.webp`, a genuinely
painted library of organic structures rather than procedural square-tile
decoration. `generate_wraith_settlement_wall.py` fits individually authored
straight, terminal, corner, T and cross pieces to the 192px native sample
area. It also applies matching painted boundary ports and native atlas padding.
No native wall Print method is intercepted. Wall inheritance, paint, roof,
shadows, attachment, blueprint and Odyssey placement logic remain unchanged.

Twenty-four full/partial hull overlays use the native 2×2 corner coordinates,
with different silhouettes for a present or absent diagonal neighbor. The
family layer keeps Odyssey's map masks and also appends its indoor corner
meshes to the native launch/landing bake.

## Rebuild and verify

For the independently authored Wraith settlement artwork, run
`python Tools/WallArt/generate_wraith_settlement_wall.py` first (Pillow and
NumPy). It generates the PNG atlas and in-game single-tile Architect/legacy
icons. Use `--preview ArtSource/Walls/Wraith/QA/HandPainted` to render an
actual multi-wall test layout without opening the game.
The Wraith gravship hull atlas and angled corners remain untouched by that pass.
The original gravship material also has its own isolated-tile Architect icon,
`UI/WNG/Build/WNG_WraithGravshipHull`, so changing the settlement wall does not
silently change gravship build-menu artwork.

Node.js and ImageMagick (`magick`, or ImageMagick 6 `convert`) are needed to pack
the committed masters. Pillow and NumPy are needed for validation.

```sh
node Tools/WallArt/assemble_wall_art.mjs
python Tools/WallArt/validate_wall_art.py
python Tools/WallArt/validate_wall_art.py --json wall-evidence.json
```

`--icons-only` and `--corners-only` support the older narrow workflows. The
authoritative masters live in `ArtSource/Walls`; the packer derives masks and
bevels without inventing replacement painting. Menu icons and legacy single
wall textures show the isolated mask, not an atlas sheet or four-way cross.

`--preview DIRECTORY` renders isolated tiles and a small wall topology at
gameplay scale for visual inspection. These are renderer simulations, not live
RimWorld screenshots. Check horizontal/vertical runs, ends, L/T/cross junctions,
thick intersections, and full versus partial angled hulls after future changes.

The focused validator checks all five defs, all 64 sampled atlas states,
compatible edge pairs, native linking and the painted Wraith silhouettes,
24 angled sprites, family icons,
padding, and the rendering hooks. Structural checks supplement visual review;
they do not claim a live save-load or launch test.

The isolated cinematic bake bridge also has a runnable harness:
`python Tools/WallArt/RuntimeTests/run_tests.py --dotnet /path/to/dotnet`.
It compiles the actual hull-layer source against small fixtures and tests native
geometry, masks, relative coordinates and mixed-family precedence. A production
Release build against the project's RimWorld 1.6 reference package is still
required before shipping the assembly.
