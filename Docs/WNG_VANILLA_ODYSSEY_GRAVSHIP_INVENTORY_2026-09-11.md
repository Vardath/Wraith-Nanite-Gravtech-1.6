# WNG vanilla Odyssey gravship inventory — 2026-09-11

Newest explicit Vardath instructions override this checkpoint.

Purpose: before WNG clones or themes gravship systems, account for the actual vanilla RimWorld 1.6 Odyssey gravship grammar so WNG reuses native behavior wherever possible instead of recreating it badly.

## Vanilla functional baseline

A vanilla gravship is built around one `GravEngine`. Connected `GravshipSubstructure` defines the transported ship footprint. A flightworthy ship minimally requires:

- Grav engine (`GravEngine`)
- connected gravship substructure (`GravshipSubstructure`)
- pilot console (`PilotConsole`)
- at least one fuel tank (`ChemfuelTank` / large tank equivalent)
- at least one thruster (`SmallThruster` / `LargeThruster`)

The grav engine's facility family also includes the following meaningful systems which must be accounted for in WNG ship-family design:

- `GravFieldExtender`
- `PilotConsole`
- small chemfuel tank
- large chemfuel tank
- `SmallThruster`
- `LargeThruster`
- `FuelOptimizer`
- `SignalJammer`
- `PilotSubpersonaCore`
- `GravshipShieldGenerator`

Related Odyssey ship/orbit construction that must be considered where appropriate:

- `GravshipHull`
- `GravAnchor` (ground-side/map-preservation utility, not necessarily a themed ship component)
- `OxygenPump`
- `VacBarrier`
- `OrbitalScanner`
- `PassengerShuttle`
- `GravcorePowerCell`

No vanilla item may simply be forgotten. Each WNG family must classify every entry as: themed equivalent, intentionally shared vanilla component, or deliberate omission with reason.

## Vanilla behavior that WNG should preserve

### Substructure / ship membership
Use Odyssey's native gravship/substructure ownership and launch model wherever it can support the desired themed ships. Do not invent a second independent map-moving ship system.

### Walls and corners
`GravshipHull` is the vanilla reference for airtight gravship exterior construction. WNG Wraith, Asuran and Goa'uld hull sets must follow the same adjacency grammar expected of vanilla ship walls. In particular, exterior/corner placements must form the angled/cut-corner visual silhouette rather than remaining isolated square sprites.

Faction art can differ radically in appearance, but placement, joining and corner readability should feel native to RimWorld/Odyssey.

### Pilot/control stations
The vanilla pilot console is the behavioral reference for launch/pilot interaction. WNG-themed bridge/control stations should reuse/extend the native gravship pilot-console system rather than replacing boarding/launch rituals with bespoke commands where vanilla already does the job.

### Thrusters
Small/large thrusters are directional gravship propulsion. WNG variants require correct directional/rotational graphics and should use the native propulsion/facility behavior where possible.

### Fuel tanks
Vanilla Odyssey gravship tanks are directly refuelled storage buildings. They are **not connected through a vanilla fuel-pipe network**. This is an important correction to earlier shorthand.

WNG's requested visible/hidden fuel piping is therefore a deliberate WNG extension, not a copied vanilla Odyssey system. It should visually and ergonomically follow RimWorld conduit connection rules while feeding WNG's themed ship-fuel economy.

### Power
Where WNG ship components use ordinary electrical power, use RimWorld's native power-net system. WNG visible/hidden themed power conduits must remain native power conduits functionally; art/connection presentation may be themed.

## WNG family mapping

### Wraith living ships
Required themed family direction:

- Wraith grav engine / living propulsion core — native gravship engine behavior adapted through WNG, **never obsolete Gravcore**.
- Wraith living substructure.
- Wraith living hull with vanilla-style joins and angled corners.
- Wraith bridge/pilot interface.
- Wraith small/large living thrusters.
- Wraith small/large bio-sludge storage.
- Wraith bio-sludge fuel plumbing: visible and hidden routing variants.
- Wraith grav-field extender analogue.
- Wraith signal-jammer analogue where it serves the same Odyssey gameplay role.
- Wraith power-generation analogue if needed, reconciled with bioelectric organs/living-tech economy.
- Wraith fuel-optimizer analogue only if it has a coherent living-ship interpretation.
- Wraith shield generator consistent with Wraith shield technology.
- Wraith pilot-assist/subpersona analogue only if lore/design supports one; do not blindly reskin a persona core.
- vacuum/oxygen pieces only where appropriate to a living Wraith vessel; classify explicitly rather than copying by default.

Fuel: `WNG_WraithBioSludge` (planned WNG resource). Requires final art, production recipe, storage and fuel-network support.

### Asuran / Ancient-derived nanite ships
Asuran gravship family must use Odyssey's native gravship mechanics while presenting Asuran/Ancient-derived technology:

- Asuran grav engine.
- Asuran substructure.
- Asuran hull with vanilla-style joins/angled corners.
- Asuran control console.
- small/large Asuran thrusters.
- small/large nanite-sludge storage.
- visible/hidden nanite-sludge fuel plumbing.
- field extender, signal jammer, power, shield and other equivalents where mechanically appropriate.

Fuel: `WNG_NaniteSludge` (planned WNG resource), distinct from Replicator loose blocks/matter and distinct from Wraith Life Force/biomass. Requires final art, production recipe, storage and fuel-network support.

The Puddle Jumper is Ancient/Lantean shuttle content and is **not automatically defined as a nanite-sludge craft** merely because Asuran technology derives from the Ancients. Its eventual fuel/power abstraction should remain a separate deliberate decision.

### Goa'uld / Jaffa Ha'tak-inspired gravship
Optional integration only. It activates with ONAC (`idolord.ONAC`) and the supported RimGate Biotech ecosystem (`CraveMode.RimGateJaffaKreeBiotech`).

- Ha'tak-inspired grav engine/drive presentation on the native Odyssey gravship system.
- Goa'uld substructure and hull architecture, with vanilla-compatible placement/join/corner grammar but Ha'tak visual identity.
- Goa'uld command/pilot console.
- small/large propulsion/thruster equivalents.
- small/large liquid-Naquadria storage and requested visible/hidden fuel-routing network.
- shield generator and heavy systems consistent with Goa'uld ship identity.
- signal jammer / scanner / other Odyssey equivalents only when their gameplay role makes sense.

Fuel: **use ONAC's existing `ONAC_LiquidNaquadria`**. Do not create a duplicate WNG liquid-naquadah/naquadria item. ONAC already provides `ONAC_MakeLiquidNaquadria` and research `ONAC_NaquadahLiquefaction`.

Goa'uld WNG construction surfaces under ONAC's verified Architect category `ONAC_Architect` when ONAC is installed. No unresolved `ONAC_` references may exist when ONAC is absent.

## Conduit/piping grammar requested by Vardath

WNG custom fuel routing should behave visually like a native network:

- exposed pipe/conduit is visible;
- straight, bend, T-junction, cross and endpoint connections read correctly from adjacent network cells;
- wall-covered runs are visually hidden where appropriate;
- explicit hidden fuel-conduit variant exists for intentional concealed routing;
- hidden and exposed pieces remain the same functional network unless a later gameplay reason requires otherwise;
- power conduits similarly provide ordinary and hidden WNG-themed presentation while remaining on the native RimWorld power net;
- directional machinery uses rotated graphics instead of one sprite pretending to fit all orientations.

This custom fuel network must not be described as vanilla Odyssey behavior. It is a WNG extension designed to look and operate consistently with vanilla RimWorld construction language.

## Shuttle/native-system correction discovered during audit

Current fresh WNG shuttles inherited `ShuttleBase`, which preserved `CompShuttle` / `CompTransporter` foundations but did **not** by itself reproduce the complete player-shuttle launch stack.

The correct vanilla-style buildable shuttle pattern includes:

- `Building_PassengerShuttle`
- `CompProperties_Shuttle` with a `TransportShipDef`
- `CompProperties_Launchable`
- `CompProperties_Transporter`
- `CompProperties_Refuelable` where the shuttle uses consumable fuel
- incoming and leaving shuttle skyfaller Defs
- a shuttle world-object Def
- native boarding/loading/launch behavior

Therefore the Dart, Wraith scout, Wraith cruiser transport, Puddle Jumper and optional Al'kesh are being upgraded to that complete native pattern before further hostile-retreat code is accepted.

## Art acceptance rule

Vanilla placeholder textures may be used during API/mechanics prototyping only. Final completion requires themed production art for every visible state that vanilla behavior can expose, including rotation, adjacency, wall corners, conduit junctions, tanks, thrusters, consoles and resources.

## Next implementation sequence

1. Correct all WNG shuttle Defs to the complete native player-shuttle stack.
2. Add WNG Architect category and fresh shuttle research gates; optional Goa'uld craft uses `ONAC_Architect` only when ONAC is present.
3. Add Wraith bio-sludge and Asuran nanite-sludge resource foundations without pretending their final recipes/art are already complete.
4. Tie hostile Dart captive commitment to the actual native leaving-skyfaller departure boundary.
5. Add real Wraith pilot/crew requirements for hostile Dart retreat.
6. Build gravship families only after this inventory is kept as the component checklist.
