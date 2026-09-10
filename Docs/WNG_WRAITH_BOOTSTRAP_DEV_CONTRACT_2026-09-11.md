# WNG Wraith bootstrap + developer spawn contract — 2026-09-11

Newest explicit Vardath instruction overrides this document.

## Non-negotiable identity correction

The Wraith gravship bootstrap endpoint is a **Wraith grav engine** integrated with RimWorld/Odyssey's real `GravEngine` system.

- Do **not** restore or reintroduce the obsolete WNG "Gravcore" concept.
- Odyssey's vanilla `Gravcore` resource may exist in the base game, but WNG must not mislabel its Wraith engine as a Gravcore or use an obsolete WNG Gravcore as the ship heart.
- The fresh WNG engine construction bridge deliberately resolves to the exact native Odyssey `GravEngine` because RimWorld 1.6 hard-codes that Def in critical gravship paths.

## Wraith bootstrap path

Wraith living infrastructure is intended to be biologically grown rather than appearing from an ordinary steel construction chain by default.

Two bootstrap growth programs are required:
1. **Wraith workshop / living forge growth**
2. **Wraith grav engine growth**

The growth program may be implanted into a suitable living humanlike host or a suitable humanlike corpse.

Supported living host ownership/status includes at minimum:
- colonists;
- slaves;
- prisoners;
- downed captives under player control/reach.

A corpse is also a valid host so the player is not forced to sacrifice a living pawn.

The implant/growth state must be visible, save-safe, and remain associated with the exact host/body until maturation. If a living host dies during growth, the implanted process continues in the resulting corpse rather than being silently deleted.

At maturity the biological host/body is consumed and the appropriate WNG structure emerges nearby if a valid placement cell exists. If placement is temporarily blocked, the growth state waits/retries rather than deleting the host or result.

Growth time and other tuning values should live in Defs/comp properties rather than scattered constants.

## Direct-crafting bypass option

The normal design keeps implantation as the Wraith-flavoured bootstrap path.

A WNG mod option must allow players who do not want the implantation process to enable **direct crafting recipes** for equivalent deployable Wraith workshop and Wraith grav-engine growth cores.

- Default: direct bootstrap crafting **off**.
- When enabled: recipes appear at an appropriate vanilla crafting bench (initial implementation: fabrication bench) after the relevant research.
- When disabled: those direct recipes are not offered.
- The direct grav-engine result must still resolve into the real Odyssey `GravEngine` architecture; the bypass must never create an obsolete Gravcore.
- This setting changes player acquisition convenience only. It must not change hostile Wraith hive behaviour or Stargate integrations.

## Developer-mode requirement

Every WNG-owned structure and spawnable object must be discoverable/testable in developer mode.

WNG must provide its own developer spawn menu in addition to whatever generic RimWorld debug lists happen to expose. This is specifically to prevent a repeat of the historical failure where the intended grav engine could not be found/spawned for testing.

The WNG dev menu should:
- enumerate WNG `ThingDef`s dynamically by package ownership/`WNG_` DefName rather than maintain a fragile manual list;
- include buildings, items, shuttles and other map-spawnable WNG Things;
- include an explicit **native Wraith grav engine** spawn action which creates the real `GravEngine` and applies the Wraith theme marker;
- provide terrain/foundation spawning separately where practical;
- never require research or the direct-crafting bypass setting;
- use developer mode only and not leak cheat commands into normal gameplay.

## Historical-code rule

Historical WNG bootstrap/gravship code is reference/failure evidence only. It is not a known-good implementation.

For this subsystem use the authority order:
1. newest Vardath instruction;
2. this/current rebuild contracts;
3. Stargate Wraith living-technology identity;
4. actual RimWorld 1.6/Odyssey APIs;
5. historical WNG only to recover intent or identify prior mistakes.
