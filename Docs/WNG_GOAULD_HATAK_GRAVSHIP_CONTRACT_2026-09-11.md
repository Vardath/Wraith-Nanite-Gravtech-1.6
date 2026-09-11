# WNG — Goa'uld Ha'tak gravship contract

Date: **2026-09-11**  
Author/design authority: **Vardath**

## Stargate identity

A Ha'tak is a Goa'uld mothership/capital warship, not a shuttle. Its WNG representation is a RimWorld-scale Odyssey gravship family preserving the recognizable Goa'uld role: pel'tac command deck, strong energy shields, Jaffa/transport capacity, internal transport rings, sublight propulsion and heavy energy-weapon capability.

Transport rings are canonically used inside Goa'uld motherships between decks and are expected internal Ha'tak equipment in WNG. The Ha'tak substructure therefore deliberately provides Heavy as well as Substructure affordance so the already implemented `WNG_GoauldTransportRings` can be built aboard the ship.

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
- same-family fuel-pipe enforcement and technology-family isolation on the shared native engine.

## ONAC ownership boundary

This first Ha'tak slice remains `MayRequire="idolord.ONAC"` because the safe standalone Goa'uld ship resource/research path has not been defined by Vardath.

When ONAC is present, use the verified external resource surface:
- `ONAC_Naquadah` for construction;
- `ONAC_LiquidNaquadria` for ship fuel;
- existing `WNG_GoauldShuttles` / ONAC liquefaction progression.

Do **not** invent uranium/chemfuel or another arbitrary standalone substitute. Al'kesh remains implemented and retained.

## Explicitly unfinished Ha'tak dependencies

These are required and must not be forgotten:
- dedicated Ha'tak heavy plasma/energy weapon systems suitable for gravship combat/orbital fire;
- Death Glider craft/bay relationship rather than a fake decorative bay;
- final Goa'uld sensors/other Odyssey equivalents only where Stargate function justifies them;
- final Goa'uld/Ha'tak textures, hull/deck/pipe/conduit connection art, shield effects and audio;
- live RimWorld testing with ONAC present, including Def load, Architect visibility, construction, engine linking, ring placement aboard substructure, fuel-pipe connectivity, shield operation, launch/travel/save-load.

## Validation status

The XML for the new files was syntax-parsed before commit. No new C# was required because the existing WNG gravship theme and fuel-network code already supports `WNGGravshipTheme.Goauld`.

This is **not** a claim of live-game validation.
