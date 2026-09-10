# WNG optional integrations and Goa'uld/Jaffa craft plan — 2026-09-10

Author/final design authority: **Vardath**.

This document is a current design contract. Newer explicit Vardath instructions override it.

## Core rule: WNG remains standalone from third-party mods

Wraith & Nanite Gravtech must continue to function without CatCraft Stargates!, ONAC, RimGate/Jaffa, or any other third-party Stargate mod installed.

- Third-party Stargate mods are **optional integrations**, never hard dependencies.
- WNG must not fail Def loading, C# initialization, incidents, save/load, or normal standalone gameplay when those mods are absent.
- Optional content activates only when the relevant external mod(s)/Defs/factions are actually present.
- WNG may retain its required RimWorld DLC foundation separately; this rule concerns third-party mod dependencies.
- Compatibility code must use dependency-free detection/reflection/Def lookup or optional XML mechanisms rather than direct compile-time references to third-party assemblies unless a future explicit design intentionally changes that.

## Integration ownership boundaries

### CatCraft Stargates!
CatCraft owns:
- Stargate network/address data;
- dialing and connection direction;
- iris/shield state;
- receive-buffer/rematerialization rules;
- gate usability and traversal state.

WNG owns only its added content around those systems:
- optional Stargate raid composition;
- shuttle/craft emergence and roof interaction;
- Wraith Dart culling passes;
- Puddle Jumper drone passes;
- raid objectives and retreat decisions;
- WNG captivity outcomes.

Normal Stargate rules remain authoritative: one-way solid-matter travel, closed iris/shield destroys incoming matter before rematerialization, and an inbound active connection cannot be reused as an outbound retreat path.

### ONAC and its exact RimGate dependency
ONAC and RimGate/Jaffa remain owners of their Goa'uld, Tok'ra, Jaffa, symbiote and related faction/gameplay systems.

WNG must **not duplicate or replace** those factions or biological systems.

The Steam dependency relationship is now pinned and must not be broadened casually:
- **ONAC:** Steam Workshop `3775612635`.
- **Required RimGate target for ONAC:** **RimGate - Jaffa, Kree! (Biotech)**, Steam Workshop **`3762118088`**, by CraveMode.
- This is the separate **Biotech rewrite** where Jaffa are a xenotype and the mod no longer requires Humanoid Alien Races.
- **Do not treat** `3759441429` (`RimGate - Jaffa, Kree! (Updated 1.6)`, HAR-based) as the ONAC dependency.
- **Do not treat** `2395671127` (`RimGate - Jaffa, Kree! (Continued)`, Mlie/HAR lineage) as the ONAC dependency.
- ONAC's published required-items list explicitly names Harmony and `RimGate - Jaffa, Kree! (Biotech)` and recommends the load order Harmony -> Core -> Biotech -> RimGate Biotech -> ONAC.

This version distinction is gameplay-significant: the Biotech rewrite owns Jaffa through xenotypes/genes/Prim'ta-compatible state, whereas the older HAR versions use a different race architecture and distinct internal names.

When the relevant optional mods are installed, WNG may add interoperability and additional craft/gravship content to the externally owned factions. When absent, that integration content must remain inactive and WNG must continue normally.

Runtime activation rule:
- WNG's ONAC/Jaffa integration activates only when ONAC and the exact Biotech RimGate target are both present.
- Until the Biotech RimGate `packageId` is verified from its actual `About.xml`, WNG may identify it by the exact published mod title as a temporary safe runtime discriminator.
- Once packageId and external Def names are verified, switch to exact package/Def identity; never fall back to generic `rimgate`/`jaffa` substring matching because that can bind the HAR versions incorrectly.

## Optional craft families

### Existing Wraith/Ancient shuttle families
These remain part of WNG's own content:
- `WNG_WraithDart`
- `WNG_WraithStrikeCraft`
- `WNG_WraithCruiser`
- `WNG_PuddleJumper`

All use native RimWorld/Odyssey shuttle boarding/transport systems wherever possible. Optional Stargate integration adds gate travel behavior but is not required for ordinary shuttle use.

### Goa'uld/Jaffa optional craft content
When ONAC + **RimGate - Jaffa, Kree! (Biotech) (`3762118088`)** are present, WNG adds craft associated with those external Jaffa/Goa'uld factions rather than creating WNG-owned duplicate Jaffa factions.

#### `WNG_AlkeshTransport`
Stargate basis:
- derived from the Goa'uld Al'kesh mid-range bomber;
- fast and maneuverable;
- capable of cloaking in canon;
- suited to troop transport/bombing/assault support.

RimWorld role:
- native-boardable Odyssey shuttle/transport foundation;
- carries Jaffa troops from the externally owned Biotech RimGate/ONAC faction set;
- transport capacity materially larger than a Dart/Puddle Jumper but smaller than a capital gravship;
- combat role emphasizes bombing/energy-weapon support rather than Wraith culling or Ancient drones;
- optional cloak behavior only if it can be implemented cleanly without breaking native boarding/selection/save-load;
- may participate in optional Stargate-linked scenarios only where physically/canonically sensible, but it does not gain arbitrary gate traversal merely because Stargates! is installed if its size/entry route is unsuitable.

#### `WNG_HatakGravship`
Stargate basis:
- modelled on the Goa'uld Ha'tak mothership rather than pretending a map-scale vehicle is a literal full-size television-series capital ship;
- Ha'tak identity includes the central pyramid/superstructure silhouette, shields, heavy energy weapons, command section, transport capability and Jaffa complement;
- actual Ha'taks carry Death Gliders and transport rings and function as major Goa'uld/Jaffa warships.

RimWorld role:
- **Odyssey gravship variant/family**, not an oversized native shuttle;
- visually and mechanically Ha'tak-inspired while scaled to RimWorld gravship gameplay;
- tied to the external Biotech RimGate/ONAC Jaffa/Goa'uld faction ecosystem when present;
- player capture/use may be supported through normal WNG/Odyssey gravship systems if that remains compatible with the broader gravtech progression;
- expected systems to reconcile later: grav engine/power, shields, heavy staff/energy batteries, troop/transport-ring analogue, hangar/Al'kesh or Glider support where feasible, command/pilot requirements, fuel/power economy, damage state, landing/takeoff and save/load;
- **never restore the obsolete WNG Gravcore**. Use the current Wraith/gravtech architecture and Odyssey gravship foundations.

## Faction attachment rules

When the exact Biotech RimGate + ONAC ecosystem is present:
- Al'kesh and Ha'tak-derived content should spawn/be assigned through those existing faction identities;
- diplomacy/hostility comes from the external faction state rather than WNG hard-coding every Jaffa as hostile;
- Tok'ra-friendly/neutral relationships must remain distinguishable where ONAC exposes them;
- WNG should not create duplicate Goa'uld, Tok'ra or Jaffa world factions solely to make its craft spawn;
- if external faction identity cannot be resolved safely, the optional craft content stays inactive rather than binding to the wrong faction;
- HAR RimGate variants may coexist in a user's list for save compatibility, but WNG's ONAC integration must still bind only to the Biotech rewrite's internal identities.

## Activation matrix

- **WNG only:** all core WNG Replicator/Wraith/Asuran/gravtech content works; no CatCraft/ONAC/RimGate calls are required.
- **WNG + Stargates!:** optional gate-entry/exit behavior, iris/direction/roof interactions and Stargate raid enhancements activate.
- **WNG + ONAC + RimGate Biotech `3762118088`:** Jaffa/Goa'uld faction interoperability plus Al'kesh and Ha'tak-derived content activate.
- **WNG + ONAC + wrong/older HAR RimGate only:** WNG ONAC/Jaffa craft integration stays inactive rather than binding incorrectly.
- **WNG + Stargates! + ONAC + RimGate Biotech:** both integration families may cooperate, while ownership boundaries remain unchanged.
- Missing optional mods must produce no red errors and no broken Def references.

## Buildability, research and Architect-menu contract — added 2026-09-11

### All shuttle families become buildable after research
The WNG shuttle and gravship families are not raid-only set pieces. After the appropriate research is completed, the player can construct them using normal RimWorld/Odyssey systems wherever practical.

Core WNG research/buildability includes the Wraith Dart, Wraith Strike Craft, Wraith Cruiser, Puddle Jumper and WNG gravship-family components. Their research and construction Defs must remain valid without ONAC, RimGate or CatCraft installed.

Native systems are authoritative wherever RimWorld already provides the behavior. In particular:
- use native Odyssey shuttle loading/boarding/transport/launch rather than a WNG replacement boarding system;
- use normal research prerequisites, construction recipes, resource filters, power/fuel networks and Architect placement where those systems can express the intended behavior;
- add WNG code only for behavior RimWorld does not provide, such as culling, living-tech fuel identity, faction-specific combat logic, optional Stargate traversal, special gravship systems and compatibility bridges.

### WNG Architect category
WNG must provide a dedicated **WNG** Architect tab/category containing WNG-owned player-buildable structures after their relevant research is complete.

This includes, as appropriate after reconciliation:
- Wraith structures and living-tech infrastructure;
- Replicator containment/countermeasure structures;
- WNG gravship walls, hull/floor/substructure pieces and ship systems;
- WNG fuel/sludge infrastructure;
- visible and hidden conduit variants;
- WNG shuttle/gravship construction access where the vanilla/Odyssey UI model expects Architect placement.

The goal is one coherent place to find WNG-owned construction rather than scattering core WNG structures across unrelated vanilla categories unless vanilla UI mechanics require a specific placement category.

### ONAC Architect category integration
When the exact supported ONAC + RimGate Biotech ecosystem is present, the **new Goa'uld-themed WNG additions appear in the existing ONAC Architect tab/category** rather than creating a competing Goa'uld category.

This includes Goa'uld-themed shuttle/gravship structures and related construction infrastructure that WNG adds for that optional ecosystem.

Important dependency rule:
- the Goa'uld integration research and content are implemented as WNG optional content, not as a hard dependency;
- they activate/become available only when the relevant owning external mod ecosystem is installed and safely identified;
- WNG must still load and function when ONAC is absent;
- no unconditional Def reference may point at an ONAC/RimGate Def when those mods are absent;
- exact ONAC category/research/resource Def names must be verified from the installed/source mod before implementation; do not guess them.

## Vanilla-faithful gravship clone contract — added 2026-09-11

WNG gravship-family construction is to behave like a themed extension/clone of vanilla Odyssey gravship building, not as unrelated custom static buildings.

### Structural completeness
Each WNG gravship family must be audited against the current vanilla Odyssey gravship construction set and include themed equivalents wherever the vanilla system has a meaningful shipbuilding role. The audit must account for at least:
- gravship hull/floor/substructure pieces;
- walls and doors/bulkheads where applicable;
- corner/angled wall behavior;
- grav engine equivalents;
- all relevant vanilla gravship consoles/control stations;
- fuel pipes;
- fuel containers/tanks;
- thrusters;
- power conduits and ship power infrastructure;
- any other vanilla gravship component required for a fully functional player-built gravship.

Do not assume this list is exhaustive. Before implementation, inventory the actual RimWorld 1.6/Odyssey gravship Def set and map every vanilla role to WNG equivalents, an explicit intentional omission, or a shared vanilla component.

### Vanilla-style wall and corner art
WNG walls must use the same connection logic and visual grammar as vanilla walls. Art must support the connection states expected by RimWorld rather than relying on one static square texture.

For gravship exterior/corner geometry, connected walls must form the same angled/corner silhouette behavior the vanilla gravship set uses. Themed Wraith/Asuran/Goa'uld art should visually belong to its faction while preserving the vanilla connection/corner semantics.

### Rotational art requirement
Any buildable whose vanilla equivalent has directional/corner/connection graphics must receive the corresponding WNG directional variants. This applies especially to:
- pipes;
- conduits;
- thrusters;
- ship wall/corner pieces;
- directional consoles or machinery;
- any structure whose function/readability changes by rotation.

Art must be built to the vanilla copy/atlas expectations so rotation and adjacency work naturally.

## Fuel and conduit networks — added 2026-09-11

### Fuel pipes
Faction-themed gravship fuels use a pipe network visually and behaviorally analogous to vanilla conduit-style networks where possible.

Fuel pipes must:
- be visible when exposed;
- be hidden visually when routed beneath/through a wall where the vanilla-style system permits it;
- include straight, turn/corner, junction and other connection art needed by the chosen vanilla connection system;
- follow rotation/adjacency automatically rather than requiring separate awkward manual decorative pieces when vanilla connection graphics can do the work.

There must also be **hidden fuel conduit/pipe variants** for deliberate concealed routing.

### Power conduits
WNG shipbuilding also gets visible and hidden power-conduit options, retaining vanilla power-network behavior wherever possible.

Visible/hidden conduit art must support correct connection and corner states and should disappear beneath walls where appropriate, matching the visual language of vanilla conduit routing rather than drawing over walls.

## Faction fuel/sludge economies — added 2026-09-11

The three technological families use distinct ship fuels/material fluids.

### Wraith — bio sludge
Wraith ships use **Wraith bio sludge** as their themed ship fuel/resource.

Requirements:
- dedicated ThingDef/resource identity;
- crafting/production recipe(s) integrated with the Wraith living-tech economy;
- dedicated item/container/tank/pipeline art;
- fuel storage and ship-consumption behavior implemented through vanilla/Odyssey fuel/network systems where possible;
- exact recipe inputs and balance remain tunable and must be reconciled with the later living-tech/biomass/Growth Chamber economy.

### Asuran — nanite sludge
Asuran ships use **nanite sludge**.

Requirements mirror the Wraith resource but remain mechanically/economically distinct:
- dedicated ThingDef/resource identity;
- crafting/production recipe(s);
- dedicated art for loose resource/storage/tanks/network presentation;
- compatible with Asuran/nanite progression rather than Wraith Life Force/biomass systems.

### Goa'uld/ONAC — liquid naquadah
Optional Goa'uld variants use **liquid naquadah supplied by the supported ONAC ecosystem**.

Rules:
- do not create an unconditional hard reference to ONAC's resource Def;
- when ONAC is installed and the exact resource Def is verified, the WNG Goa'uld fuel network consumes the external liquid-naquadah resource directly;
- do not silently create a second competing WNG liquid-naquadah item when the correct ONAC resource is available;
- Goa'uld fuel research/infrastructure appears only with the supported optional ecosystem and should be surfaced in the ONAC Architect/research presentation where technically appropriate;
- exact Def names and recipe interfaces must be verified from ONAC source before writing XML patches.

## Art quality contract — added 2026-09-11

All new WNG structure/resource art must be production-quality and compatible with vanilla RimWorld/Odyssey rendering conventions.

Required art inventory now explicitly includes:
- Wraith bio sludge item/resource graphics;
- Asuran nanite sludge item/resource graphics;
- faction-specific fuel tanks/containers where not shared;
- visible fuel-pipe connection graphics;
- hidden fuel-pipe designation/build graphics as needed;
- visible and hidden power-conduit graphics where WNG supplies themed versions;
- wall connection/corner sets;
- gravship angled corner pieces/connection states;
- rotational thrusters, consoles and machinery as required;
- all shuttle/gravship building graphics needed for player construction and native flight presentation.

Do not generate placeholder art and call the visual pass complete. Existing vanilla graphics may be used temporarily for technical implementation/testing, but the final art audit must account for every required directional/connection state.

## Updated implementation order from this checkpoint

1. Keep the compiled exact-craft two-pass Dart/Puddle Jumper shuttle architecture and native boarding foundation intact; live RimWorld verification remains required.
2. Tie hostile Dart retreat/captive commit to actual native launch completion and landed crew behavior.
3. Inventory the complete vanilla RimWorld 1.6/Odyssey gravship construction Def set: walls/corners, hull/substructure, consoles, fuel system, tanks, thrusters, power and all other functional components.
4. Create the dedicated WNG Architect category and map every WNG-owned buildable into it with research gating.
5. Make all WNG shuttle families player-buildable after appropriate research using native Odyssey construction/boarding/launch systems wherever possible.
6. Design the Wraith and Asuran gravship clone families against the vanilla inventory rather than ad-hoc historical WNG structures.
7. Implement vanilla-faithful connected/angled wall graphics and rotational/connection behavior for ship structures.
8. Implement visible/hidden power conduits and visible/hidden fuel-pipe networks using vanilla connection behavior wherever possible.
9. Implement Wraith bio sludge and Asuran nanite sludge Defs, recipes, tanks/network interfaces and final art requirements.
10. Verify ONAC's exact Architect category, research and liquid-naquadah Def/API names from its source before adding optional patches.
11. When the supported ONAC/RimGate ecosystem is present, expose WNG Goa'uld research and new Goa'uld-themed shuttle/gravship construction in the ONAC UI/category and consume ONAC liquid naquadah directly.
12. Build the Goa'uld/Ha'tak/Al'kesh family on the same vanilla-faithful construction principles, without making ONAC a hard WNG dependency.
13. Continue CatCraft adapter work independently; CatCraft remains optional and does not gate ordinary shuttle/gravship construction.
14. Complete production art audit for every connection, rotation, corner, resource and structure state before release.
15. Validate WNG-only, WNG+CatCraft, WNG+ONAC/RimGate Biotech, wrong-HAR-RimGate coexistence, and full intended integration configurations.

## Mandatory design question for every integration feature

Before implementation ask:
1. What is this in Stargate?
2. What does it actually do in Stargate?
3. What does Vardath's current plan require?
4. What did historical WNG do?
5. Which exact external mod/version owns the underlying system/faction/resource/UI category?
6. Which vanilla RimWorld/Odyssey system already does part of this job?
7. How can WNG add value without replacing that vanilla behavior or becoming dependent on a third-party mod?
