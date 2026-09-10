# WNG Asuran gravship core checkpoint — 2026-09-11

Newest explicit Vardath instruction overrides this checkpoint.

## Validation

Temporary GitHub Actions validation run **34541165427** completed successfully.

Validated:
- WNG C# assembly compile: SUCCESS;
- all current Def/Patch XML parse: SUCCESS;
- Asuran core-family/resource/reference sanity: SUCCESS.

The temporary workflow was removed immediately after validation.

## Authority and architecture

This is a fresh rebuild against:
1. Vardath's current requirements;
2. Stargate Asuran/Ancient-derived identity;
3. RimWorld 1.6 Odyssey's real gravship contracts;
4. fresh WNG implementation.

Historical WNG gravship code is not design authority and no historical state is treated as known-good.

WNG deliberately uses Odyssey's **exact native `GravEngine`** at runtime because RimWorld 1.6 hard-codes that Def in important gravship systems. WNG applies a technology theme rather than creating an incompatible replacement engine.

## Family isolation correction

A themed WNG engine now accepts only `CompGravshipFacility` parts carrying the same WNG technology-family theme.

Therefore:
- Wraith engine cannot use Asuran/Goa'uld/vanilla gravship facilities;
- Asuran engine cannot use Wraith/Goa'uld/vanilla gravship facilities;
- vanilla chemfuel tanks and vanilla thrusters cannot silently satisfy a WNG themed ship;
- WNG fuel families remain isolated.

The check occurs when the engine receives its theme, when themed parts spawn/load, and periodically as a defensive reconciliation.

## Implemented Asuran/Precursor gravship core

### Research/resource foundation
- `WNG_AsuranGravships` research requires `WNG_AsuranFabrication`, `WNG_AncientShuttles` and Odyssey `BasicGravtech`.
- `WNG_NaniteSludge` is the Asuran ship-engineering/fuel medium and remains distinct from Replicator blocks/matter and Wraith bio sludge.
- Nanite sludge is produced at `WNG_AsuranWorkshop`.

### Native ship framework
- `WNG_AsuranSubstructure` — genuine Odyssey-compatible substructure foundation.
- `WNG_AsuranGravEngineSeed` — directly constructed Asuran grav-engine endpoint that converts to Odyssey's exact native `GravEngine` and applies Asuran theme state.
- `WNG_AsuranHullSeed` — converts into Odyssey's exact `GravshipHull` so native airtight behavior and cut/angled corner rendering remain functional while the resulting hull records Asuran architecture.
- `WNG_AsuranControlInterface` — native `CompPilotConsole` controls plus required `CompBreakdownable`.
- `WNG_AsuranNaniteSludgeTank` / `WNG_AsuranLargeNaniteSludgeTank` — native gravship fuel facilities accepting only `WNG_NaniteSludge`.
- `WNG_AsuranSmallThruster` / `WNG_AsuranLargeThruster` — native Odyssey gravship-thruster behavior.
- `WNG_AsuranFieldExtender` — native substructure-footprint/support extension.
- `WNG_AsuranSignalJammer` — native Odyssey signal-jammer component type.
- `WNG_AsuranFuelOptimizer` — native fuel-savings facility.
- `WNG_AsuranShieldEmitter` — true powered native `CompGravshipShieldGenerator` energy shield. This is intentionally distinct from Wraith biological hull regeneration.
- `WNG_AsuranPowerCell` — uses Odyssey's native `CompPowerPlantGravcore` behavior so it outputs electrical power only while on gravship substructure. This is an Asuran **power cell**, not a restored WNG Gravcore item.

`Patches/AsuranGravshipNativeBridge.xml` links the Asuran facilities, hull command and substructure into Odyssey's exact GravEngine definition.

## Adjacent Wraith correction found during native audit

`CompPilotConsole` dereferences `CompBreakdownable` in RimWorld 1.6. `WNG_WraithControlInterface` was missing that required comp, so `Patches/WraithPilotConsoleSafety.xml` now adds it.

## Explicitly unfinished gravship items

These are not allowed to disappear from later passes.

### Asuran/Precursor
- airtight gravship door / vacuum-safe door family — exact Odyssey door/vacuum behavior still needs reconciliation;
- visible nanite-sludge fuel pipe;
- hidden nanite-sludge fuel pipe;
- actual WNG fuel-network topology and tank feed behavior — deliberate WNG extension, because vanilla Odyssey tanks are directly refuelled and have no fuel-pipe network;
- visible Asuran power conduit;
- hidden Asuran power conduit, both remaining functionally on RimWorld's native power net;
- pilot-assist / subpersona analogue — lore-appropriate for Ancient-derived Asuran systems but exact native `PilotSubpersonaCore` gameplay contract still needs mapping;
- oxygen/vacuum equipment classification (`OxygenPump`, `VacBarrier`) and implementation where appropriate;
- orbital scanner classification/implementation;
- `GravAnchor` classification — likely intentionally shared vanilla ground/map utility rather than themed ship component, but must be settled explicitly;
- final dedicated Asuran gravship graphics for every visible rotation/adjacency/state;
- professional Asuran gravship audio;
- live RimWorld launch/fuel/shield/power testing.

### Cross-family / Wraith follow-up
- visible/hidden Wraith bio-sludge fuel piping remains unfinished;
- themed native-power conduit presentation remains unfinished;
- Wraith pilot-assist equivalent remains intentionally undecided until lore/native role reconciliation;
- final Wraith gravship art/audio remains unfinished.

## Current next step

Reconcile and implement the exact native door/vacuum and native power-conduit grammar first, then design the custom WNG fuel network on top of the already isolated Asuran/Wraith fuel families. Do not guess these systems or copy historical failed implementations.
