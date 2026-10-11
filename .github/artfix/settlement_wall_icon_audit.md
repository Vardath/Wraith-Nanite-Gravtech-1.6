# Settlement wall linked-art audit (11 October 2026)

The Precursor wall `WNG_PrecursorWall` and Wraith living wall
`WNG_WraithLivingWall` use the same 16-state linked atlases as their
respective family gravship hulls. RimWorld's `MaterialAtlasPool` samples
an indexed sub-rectangle of the material from `Graphic_Linked`.
Removing the atlas breaks corner, run and door-adjacency artwork even if
the replacement is a visually attractive single tile.

The in-map graphics must therefore retain:
- Precursor `Things/Building/Precursor/Gravship/WNG_PrecursorHull_Atlas`
- Wraith `Things/Building/Wraith/Gravship/WNG_OrganicHull_Atlas`
- `Graphic_Single`, `CornerFiller` and Wall/Rock link flags

Before this fix, the XML incorrectly nested `uiIconPath` under
`graphicData`; `uiIconPath` is a property of `BuildableDef`.
The former Precursor icon PNG also had visibly corrupted bottom rows,
while the Wraith fallback artwork was itself a 4x4 atlas, not one icon.
The main-thread artwork generator
`.github/artfix/generate_settlement_wall_icons.py` extracts one approved
16-state panel into a separate 192x192 PNG for each Architect icon.
It does not change existing atlases, door rotation or placement.

The new `D121 Settlement Family Validation` asserts paths, correct XML
nesting, icon existence and single-tile PNG dimensions.
The accepted changes affect UI art only; in-game render links and
saved map wall definitions are preserved.

Expected QA: Precursor wall menu icon is a single cross-shaped composite
rather than the 4x4 atlas or corrupted strip; Wraith menu icon is the
same family look; a wall-line connected to iris doors and Wraith doors
still behaves as vanilla `CornerFiller` walls.
