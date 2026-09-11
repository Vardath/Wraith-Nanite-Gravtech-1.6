# WNG — Goa'uld Ha'tak gravship contract

Date: **2026-09-11**  
Author/design authority: **Vardath**

## Stargate identity

A Ha'tak is a Goa'uld mothership/capital warship, not a shuttle. Its WNG representation is a RimWorld-scale Odyssey gravship family preserving the recognizable Goa'uld role: pel'tac command deck, strong energy shields, Jaffa/transport capacity, internal transport rings, sublight propulsion, heavy energy weapons and carried Death Glider fighters.

Transport rings are canonically used inside Goa'uld motherships between decks and are expected internal Ha'tak equipment in WNG. The Ha'tak substructure deliberately provides Heavy as well as Substructure affordance so `WNG_GoauldTransportRings` can be built aboard the ship.

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
- `WNG_GoauldDeathGlider`, a real native-boardable two-seat fighter with short non-hyperdrive world range, ONAC liquid-Naquadria fuel, physical paired staff-cannon combat sorties and hacking/capture support;
- `WNG_GoauldDeathGliderStrike`, a bounded optional hostile strike using exact existing System-Lord factions and exact RimGate Biotech Jaffa crew, followed by real native shuttle withdrawal when possible.

## Death Glider / Ha'tak carrier relationship

The fighter relationship is deliberately physical rather than decorative:
- the Death Glider is a real `Building_PassengerShuttle` using native `CompShuttle`, `CompTransporter`, `CompLaunchable` and `CompRefuelable` behavior;
- combat sorties require two conscious humanlike crew actually loaded in the native transporter;
- the exact Glider Thing and its exact crew are carried through WNG's physical attack-pass skyfaller; no proxy craft or copied crew are created;
- paired staff-cannon passes return the exact fighter to its original landing cell when that cell remains available;
- Death Gliders have a deliberately short world range because they do not carry an independent hyperdrive;
- when a Death Glider is parked on connected Ha'tak gravship substructure, Odyssey's native gravship transport carries that exact spawned fighter with the mothership;
- WNG therefore does **not** create a fake decorative hangar inventory to simulate the carrier relationship.

Dedicated production-quality Ha'tak/Death-Glider bay art and hostile Ha'tak carrier encounter staging remain later work; they must preserve the physical fighter model above.

## ONAC ownership boundary

This Ha'tak family remains `MayRequire="idolord.ONAC"` because the safe standalone Goa'uld ship resource/research path has not been defined by Vardath.

When ONAC is present, use the verified external resource surface:
- `ONAC_Naquadah` for construction;
- `ONAC_LiquidNaquadria` for ship and Death Glider fuel;
- existing `WNG_GoauldShuttles` / ONAC liquefaction progression.

Do **not** invent uranium/chemfuel or another arbitrary standalone substitute. Al'kesh remains implemented and retained.

## Explicitly unfinished Ha'tak dependencies

These are required and must not be forgotten:
- a real hostile Ha'tak map/site/encounter path that can physically stage/deploy its carried Death Gliders rather than relying only on the standalone edge strike;
- true cross-map/orbital bombardment distinct from the implemented on-map heavy plasma battery;
- final Goa'uld sensors/other Odyssey equivalents only where Stargate function justifies them;
- final Goa'uld/Ha'tak/Death-Glider textures, hull/deck/pipe/conduit connection art, shield effects and audio;
- live RimWorld testing with ONAC present, including Def load, storyteller incident, exact external faction/crew binding, Death Glider construction/crew/loading/sortie/return/withdrawal/hacking, Ha'tak carriage, engine linking, ring placement aboard substructure, fuel-pipe connectivity, shield operation, launch/travel/save-load.

## Validation status

The Death Glider XML and incident XML were syntax-structured before commit and the C# stays on APIs already used by current WNG systems (`CompTransporter`, `CompRefuelable`, exact-craft skyfallers, native `TransportShip` / `ShipJob_FlyAway`). The current environment cannot run RimWorld itself, so this is **not** a claim of live-game validation.
