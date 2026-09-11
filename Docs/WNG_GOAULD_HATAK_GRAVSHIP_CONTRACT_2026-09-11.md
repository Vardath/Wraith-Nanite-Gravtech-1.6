# WNG — Goa'uld Ha'tak gravship contract

Date: **2026-09-11**  
Author/design authority: **Vardath**

## RimWorld content dependency

Vardath requires WNG 1.6 to depend on the complete RimWorld DLC set: **Royalty, Ideology, Biotech, Anomaly and Odyssey**. WNG does not have to use every DLC in every subsystem, but any of their native mechanics may be used where they produce the correct implementation.

## Stargate identity

A Ha'tak is a Goa'uld mothership/capital warship, not a shuttle. Its WNG representation is a RimWorld-scale Odyssey gravship family preserving the recognizable Goa'uld role: pel'tac command deck, strong energy shields, Jaffa/transport capacity, internal transport rings, sublight propulsion, heavy energy weapons, carried Death Glider fighters and orbital/planetary fire support.

Transport rings are canonically used inside Goa'uld motherships between decks and are expected internal Ha'tak equipment in WNG. A Ha'tak can also land on a planetary surface. WNG's hostile carrier encounter therefore uses a landed world site rather than labeling a generic ground raid as an orbital mothership.

## Current implementation slice

Implemented as ONAC-backed WNG-owned technology:
- `WNG_GoauldGravships` research, extending `WNG_GoauldShuttles` + Odyssey `BasicGravtech`;
- `WNG_GoauldGravEngineSeed` converting into Odyssey's exact native `GravEngine` with `Goauld` theme;
- `WNG_GoauldSubstructure` using Odyssey's native connected-substructure tag;
- `WNG_GoauldHullSeed` converting into Odyssey's exact native `GravshipHull` with Goa'uld runtime theme;
- `WNG_GoauldPeltac` using native `CompPilotConsole` controls;
- small/large ONAC `ONAC_LiquidNaquadria` reservoirs;
- small/large direction-sensitive sublight drives using native gravship thruster behavior;
- gravitic field projector using native substructure-footprint/support behavior;
- true Ha'tak energy shield using native `CompGravshipShieldGenerator` behavior;
- naquadah power core using native gravship power-plant behavior and ordinary RimWorld power nets;
- visible/hidden Goa'uld liquid-Naquadria fuel conduits;
- visible/hidden Goa'uld power conduits;
- same-family fuel-pipe enforcement and technology-family isolation on the shared native engine;
- `WNG_GoauldHeavyPlasmaBattery`, a powered on-map heavy shipboard energy battery for gravship combat;
- `CompGoauldHatakOrbitalBombardment`, allowing that exact powered battery on a connected Goa'uld Odyssey gravship in `PlanetLayerDefOf.Orbit` to target a separate generated Surface-layer map and deliver Royalty's exact native `Bombardment` entity there;
- `WNG_GoauldDeathGlider`, a real native-boardable two-seat fighter with short non-hyperdrive world range, ONAC liquid-Naquadria fuel, physical paired staff-cannon combat sorties and hacking/capture support;
- `WNG_GoauldDeathGliderStrike`, a bounded optional hostile strike using exact existing System-Lord factions and exact RimGate Biotech Jaffa crew, followed by real native shuttle withdrawal when possible;
- `WNG_GoauldHatakCarrier`, a bounded landed hostile carrier site built from Odyssey's exact native GravEngine/GravshipHull, real WNG Goa'uld substructure/facilities, exact Jaffa defenders and physically parked exact Death Gliders that perform a bounded defensive sortie.

## Orbital bombardment boundary

Orbital fire is deliberately separate from ordinary on-map turret combat:
- the physical source is the exact `WNG_GoauldHeavyPlasmaBattery`;
- the source map must be Odyssey Orbit, not merely a surface map described as orbital;
- the battery must be powered and on connected substructure belonging to a Goa'uld-themed exact native `GravEngine`;
- the world target must be a different generated Surface-layer `MapParent` within layer-aware world range;
- the exact impact cell is selected on the target map;
- the target map receives Royalty's real `ThingDefOf.Bombardment` / `Bombardment` Thing;
- WNG configures that native bombardment from author-tunable Def values;
- no same-map turret shot or generic local explosion is relabeled as orbital bombardment.

Current orbital scope targets already generated surface maps. Generating an unloaded settlement/site map solely for bombardment is not claimed by this slice.

## Death Glider / Ha'tak carrier relationship

The fighter relationship is deliberately physical rather than decorative:
- the Death Glider is a real `Building_PassengerShuttle` using native `CompShuttle`, `CompTransporter`, `CompLaunchable` and `CompRefuelable` behavior;
- combat sorties require two conscious humanlike crew actually loaded in the native transporter;
- the exact Glider Thing and its exact crew are carried through WNG's physical attack-pass skyfaller; no proxy craft or copied crew are created;
- paired staff-cannon passes return the exact fighter to its original landing cell when that cell remains available;
- Death Gliders have a deliberately short world range because they do not carry an independent hyperdrive;
- when a Death Glider is parked on connected Ha'tak gravship substructure, Odyssey's native gravship transport carries that exact spawned fighter with the mothership;
- the hostile carrier site uses this exact same physical model: the Gliders exist on the generated Ha'tak deck and launch from there;
- WNG does **not** create a fake decorative hangar inventory to simulate the carrier relationship.

## ONAC ownership boundary

This Ha'tak family remains `MayRequire="idolord.ONAC"` because the safe standalone Goa'uld ship resource/research path has not been defined by Vardath.

When ONAC is present, use the verified external resource surface:
- `ONAC_Naquadah` for construction;
- `ONAC_LiquidNaquadria` for ship and Death Glider fuel;
- existing `WNG_GoauldShuttles` / ONAC liquefaction progression.

Do **not** invent uranium/chemfuel or another arbitrary standalone substitute. Al'kesh remains implemented and retained.

## Explicitly unfinished Ha'tak dependencies

These are required and must not be forgotten:
- final Goa'uld sensors/other Odyssey equivalents only where Stargate function justifies them;
- final hostile-carrier world behavior beyond a landed bounded site (takeoff/retreat/pursuit and hostile use of orbital fire) only if it can be implemented through genuine native gravship/world mechanics rather than a proxy;
- optional future support for bombarding an otherwise-unloaded settlement/site only if its map generation and consequences can be handled without faking map damage;
- final Goa'uld/Ha'tak/Death-Glider textures, central-pyramid/outer-superstructure visual treatment, hull/deck/pipe/conduit connection art, shield effects and audio;
- live RimWorld testing with the complete DLC set and ONAC present, including Def load, storyteller incidents, exact external faction/crew binding, carrier-site generation, native engine/facility links, Death Glider launch/return, fuel/power networks, shield/turret operation, gravship launch/travel, orbital bombardment world/cell targeting, save-load and actual impact behavior.

## Validation status

The current Ha'tak source uses APIs/patterns verified against current RimWorld 1.6 source for Odyssey gravships/world layers and Royalty orbital bombardment. This is source/API validation, not a claim of live-game validation.
