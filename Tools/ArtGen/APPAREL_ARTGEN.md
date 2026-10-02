# WNG Apparel ArtGen — Costume-First Architecture

The permanent apparel generator is **not** a generic armour-panel compositor.

## Enforced order

1. `StargateReferencePass`
2. `RimWorldImplementationPass`
3. `SynthesisPass`
   - costume construction
   - material painting
   - seams / closures / wear
4. Visual and RimWorld QA
5. Live mod PNGs

## Costume Blueprint v2

New clothing and uniform work should use:

```json
"renderer": "costume_blueprint_v2"
```

A profile supplies named Stargate materials and a semantic garment blueprint for south,
north and east. Pieces must be real clothing/construction concepts such as **lapel,
collar, yoke, vest, epaulette, cuff, waistband, sash, placket, corset, coat tail,
cuirass or guard**. Anonymous `panel`, `plate`, `polygon` and generic geometric
piece roles are rejected by profile validation.

The vanilla RimWorld alpha remains the silhouette authority. The blueprint only
describes construction *inside* that silhouette.

## Material model

Materials are profile-defined using shadow/mid/high response plus a material kind.
The generator supports cloth/woven uniform fabric, leather, reptile leather,
silk/satin and high-technology fabrics without reducing faction identity to colour.

Broad form, authored fold guides, seams, stitching, closures, wear and controlled
historical grayscale relief create the surface. Random noise is deliberately
subordinate and cannot define the garment structure.

## Historical WNG art

Historical WNG clothing may contribute fine grayscale relief and continuity cues.
It may **not** provide the silhouette, faction brief, or replacement for Stargate
research.

## Garment QA

Costume Blueprint v2 rejects:
- one oversized overlay that hides the base garment;
- excessive semantic pieces that become panel fragmentation;
- missing garment-construction seams;
- anonymous panel/plate geometry;
- undefined materials;
- invalid RimWorld silhouette output.

Automated QA is still only a gate. The 192 px contact sheet must be visually
inspected before an item is accepted.
