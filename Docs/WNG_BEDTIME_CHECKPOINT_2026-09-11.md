# WNG bedtime checkpoint — 2026-09-11

Newest explicit Vardath instructions override this checkpoint.

## Current repository state entering this checkpoint

Previous main checkpoint before this design update:
- `2cf77ef79cdb1b5e0b94dfcd6fff50454ab155c7`
- exact-craft Dart/Puddle Jumper physical two-pass system implemented and compile/XML validated against RimWorld 1.6.4871;
- live in-game trajectory/landing/save-reload verification still pending;
- native Odyssey shuttle boarding remains authoritative.

## New instructions recorded tonight

### Research and buildability
- After appropriate research, **all WNG shuttle families are intended to be player-buildable** rather than raid-only craft.
- Use native RimWorld/Odyssey construction, shuttle boarding, loading, transporter and launch systems wherever possible.
- WNG must not replace vanilla/native behavior merely because custom code is possible.

### Architect UI
- Add a dedicated **WNG** Architect tab/category containing WNG-owned available structures.
- Research prerequisites determine what becomes available.
- Core WNG content remains standalone from third-party mods.
- When the supported ONAC/RimGate Biotech ecosystem is present, the **new Goa'uld-themed WNG structures/craft appear under ONAC's existing Architect tab/category** rather than a separate competing Goa'uld category.
- Exact ONAC category Def names must be verified before implementation.

### Optional-dependency rule clarified
- Goa'uld shuttle/gravship research and additions are WNG optional integration content.
- They become available when the correct external owning mod ecosystem is installed.
- Their presence in WNG must never make ONAC/RimGate a hard dependency.
- No unconditional external Def references.
- Wrong/older HAR RimGate variants must not accidentally activate the Biotech/ONAC bridge.

### Vanilla-faithful gravship clones
WNG gravship families are to mirror the functional completeness and visual behavior of vanilla Odyssey gravship construction.

Before implementation, inventory the **actual full vanilla RimWorld 1.6/Odyssey gravship Def set** and map every meaningful ship component to:
1. a themed WNG equivalent;
2. an intentional shared vanilla component; or
3. an explicit deliberate omission.

Expected coverage includes, but is not limited to:
- gravship hull/floor/substructure;
- walls;
- angled/corner wall behavior;
- doors/bulkheads as appropriate;
- grav engines;
- all vanilla gravship consoles/control stations;
- fuel pipes;
- fuel tanks/containers;
- thrusters;
- power systems/conduits;
- any other vanilla part required for a complete functional player-built gravship.

### Vanilla-style wall/corner art
- WNG wall art must use vanilla-style connection logic rather than a single static square.
- Gravship exterior corners must form the angled wall silhouette expected by the vanilla gravship set.
- Faction styling may differ, but the adjacency/corner grammar should behave like vanilla.

### Pipe and conduit art/behavior
Fuel pipes and conduits should follow vanilla connection behavior wherever possible.

Fuel pipes:
- visible when exposed;
- hidden under/behind walls where vanilla-style routing allows;
- support straight/corner/junction connection states;
- include deliberate hidden fuel-conduit/pipe variants.

Power conduits:
- provide visible and hidden WNG-themed variants where appropriate;
- use native power-network behavior;
- support rotational/connection/corner art and wall hiding consistent with vanilla behavior.

All directional ship machinery and structural pieces need correct rotational art where the vanilla equivalent rotates or changes by adjacency.

### Three fuel/resource families

#### Wraith
- Wraith ships use **Wraith bio sludge**.
- Needs dedicated resource art, crafting/production recipes, storage/tank presentation and ship fuel-network integration.
- Later reconcile recipe inputs with living-tech/biomass/Growth Chamber economy.

#### Asuran
- Asuran ships use **nanite sludge**.
- Needs dedicated resource art, crafting/production recipes, storage/tank presentation and ship fuel-network integration.
- Must remain distinct from Wraith Life Force/biomass systems.

#### Goa'uld
- Goa'uld variants use **liquid naquadah from ONAC** when the supported ONAC ecosystem is installed.
- Do not duplicate ONAC liquid naquadah with a second WNG item if the correct resource exists.
- Exact resource/research/category Def names require source verification before coding.

## Art requirements now explicitly tracked
Final production art must account for:
- Wraith bio sludge;
- Asuran nanite sludge;
- faction fuel tanks/containers;
- fuel-pipe connection states;
- hidden fuel-routing presentation;
- visible/hidden power conduit states where themed;
- wall adjacency/corner/angled states;
- rotational thrusters/consoles/machinery;
- player-buildable shuttle/gravship graphics and native flight presentation.

Placeholder vanilla graphics may be used for technical prototyping only. They do not satisfy the final art pass.

## Updated next-work order

1. Preserve the exact-craft native-boardable shuttle architecture already implemented.
2. Finish hostile Dart landed crew/retreat and tie captive commit to **confirmed native shuttle departure**, not a request/state transition.
3. Inventory the complete vanilla 1.6/Odyssey gravship Def/component set before cloning anything.
4. Build the WNG Architect category/research layout.
5. Make all WNG shuttle families buildable after research using native systems.
6. Build Wraith and Asuran gravship clone families from the vanilla component inventory.
7. Implement vanilla-faithful wall/corner/rotation/connection graphics.
8. Implement visible/hidden fuel and power conduit systems.
9. Implement Wraith bio sludge and Asuran nanite sludge resources, recipes and storage/network support.
10. Verify ONAC source/package/category/research/liquid-naquadah internal identities before optional Goa'uld UI/resource patches.
11. Surface Goa'uld additions under ONAC's UI when integration is active and use ONAC liquid naquadah.
12. Finish production art coverage and cross-configuration validation.

## Standing guardrails
- Public repo `Vardath/Wraith-Nanite-Gravtech-1.6`, branch `main`, is the active working tree.
- Historical builds are reference evidence only, never a known-good source to restore wholesale.
- Do not restore obsolete Gravcore.
- CatCraft, ONAC and RimGate remain optional.
- Prefer vanilla/native RimWorld systems wherever they already provide the required behavior.
- Do not call live behavior proven until tested in RimWorld.
