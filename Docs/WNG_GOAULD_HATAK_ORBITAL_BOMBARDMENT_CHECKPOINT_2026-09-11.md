# WNG — Ha'tak orbital bombardment checkpoint

Date: **2026-09-11**  
Author/design authority: **Vardath**

## Dependency correction

Vardath explicitly requires the complete RimWorld DLC set for WNG 1.6: **Royalty, Ideology, Biotech, Anomaly and Odyssey**. WNG does not have to force every feature to use every DLC, but the rebuild may use the best native DLC mechanic where appropriate.

`About/About.xml` therefore declares all five DLCs as hard dependencies.

## Implemented orbital-fire slice

- `WNG_GoauldHeavyPlasmaBattery` remains the same physical powered Ha'tak shipboard battery used for local gravship combat.
- The battery now carries `CompGoauldHatakOrbitalBombardment` with all firing-envelope/balance values exposed in XML rather than buried in C#.
- The orbital command appears only on player-owned batteries.
- It requires the exact battery to be powered.
- It requires the battery to be marked `Goauld` and to sit on connected substructure belonging to an exact Odyssey `GravEngine` carrying the Goa'uld WNG runtime theme.
- The source map must be Odyssey's real `PlanetLayerDefOf.Orbit` layer.
- World targeting accepts only a **different generated Surface-layer `MapParent`** with a real map.
- Range is calculated through RimWorld 1.6's layer-aware `WorldGrid.TraversalDistanceBetween(... canTraverseLayers: true)` rather than pretending two maps are adjacent.
- After choosing the surface world object, WNG switches to that exact target map and requires a real local impact cell.
- Fogged cells and thick overhead mountain are rejected, matching the native Royalty orbital-strike targeting boundary.
- The target map receives Royalty's exact native `ThingDefOf.Bombardment` / `Bombardment` Thing.
- WNG configures that native bombardment's impact area, explosion radius, interval, warmup, count, random-fire radius, instigator and source weapon.
- The battery has a save-persisted cooldown.
- This is therefore genuinely **cross-map/orbital**: source weapon and source gravship exist on one Orbit-layer map while the native orbital-strike entity exists and resolves on a separate Surface-layer map.
- No local turret projectile, same-map explosion or decorative effect is renamed as "orbital".

## Author-tunable defaults

Current XML defaults on the battery:
- world range: 12 tiles;
- cooldown: 60,000 ticks;
- impact-area radius: 14;
- explosion-radius range: 4.5–6.5;
- impact interval: 18 ticks;
- warmup: 90 ticks;
- explosion count: 18;
- random-fire radius: 18.

These are implementation defaults, not immutable doctrine. Vardath can change them directly through Def data.

## Scope boundary

This slice targets **already generated surface maps**. It does not yet generate an unloaded settlement/site map solely to bombard it, and it does not yet give hostile System-Lord Ha'taks autonomous orbit/retreat/pursuit/bombardment world AI.

The landed hostile carrier site remains a separate real encounter. If hostile carrier takeoff/world behavior is added later, it must use genuine Odyssey gravship/world mechanics rather than deleting the landed ship and replacing it with an abstract proxy.

## Validation status

Implementation is based on exact current RimWorld 1.6 APIs/source for:
- `WorldTargeter.BeginTargeting`;
- `PlanetLayerDefOf.Orbit` / `PlanetLayerDefOf.Surface`;
- `WorldGrid.TraversalDistanceBetween(... canTraverseLayers: true)`;
- `MapParent.HasMap` / `MapParent.Map`;
- `Targeter.BeginTargeting`;
- Royalty `ThingDefOf.Bombardment` / `Bombardment`;
- native Royalty orbital-strike targeting restrictions.

This is **source/API validation only**. It is not a claim that RimWorld has been launched and the feature has been live-tested.
