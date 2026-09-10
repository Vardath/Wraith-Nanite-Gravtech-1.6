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

### ONAC and its dependency / RimGate-Jaffa ecosystem
ONAC and RimGate/Jaffa remain owners of their Goa'uld, Tok'ra, Jaffa, symbiote and related faction/gameplay systems.

WNG must **not duplicate or replace** those factions or biological systems.

When the relevant optional mods are installed, WNG may add interoperability and additional craft/gravship content to the externally owned factions. When absent, that integration content must remain inactive and WNG must continue normally.

Detection must not rely on one guessed package ID where a stable external Def/faction identity can be used instead. The exact installed package IDs/Def names should be verified from the actual mods before final activation patches are written.

## Optional craft families

### Existing Wraith/Ancient shuttle families
These remain part of WNG's own content:
- `WNG_WraithDart`
- `WNG_WraithStrikeCraft`
- `WNG_WraithCruiser`
- `WNG_PuddleJumper`

All use native RimWorld/Odyssey shuttle boarding/transport systems wherever possible. Optional Stargate integration adds gate travel behavior but is not required for ordinary shuttle use.

### Goa'uld/Jaffa optional craft content
When the ONAC/RimGate-Jaffa ecosystem is present, WNG adds craft associated with those external Jaffa/Goa'uld factions rather than creating WNG-owned duplicate Jaffa factions.

#### `WNG_AlkeshTransport`
Stargate basis:
- derived from the Goa'uld Al'kesh mid-range bomber;
- fast and maneuverable;
- capable of cloaking in canon;
- suited to troop transport/bombing/assault support.

RimWorld role:
- native-boardable Odyssey shuttle/transport foundation;
- carries Jaffa troops from the externally owned RimGate/ONAC faction set;
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
- tied to the external RimGate/ONAC Jaffa/Goa'uld faction ecosystem when present;
- player capture/use may be supported through normal WNG/Odyssey gravship systems if that remains compatible with the broader gravtech progression;
- expected systems to reconcile later: grav engine/power, shields, heavy staff/energy batteries, troop/transport-ring analogue, hangar/Al'kesh or Glider support where feasible, command/pilot requirements, fuel/power economy, damage state, landing/takeoff and save/load;
- **never restore the obsolete WNG Gravcore**. Use the current Wraith/gravtech architecture and Odyssey gravship foundations.

## Faction attachment rules

When RimGate/ONAC Jaffa factions are present:
- Al'kesh and Ha'tak-derived content should spawn/be assigned through those existing faction identities;
- diplomacy/hostility comes from the external faction state rather than WNG hard-coding every Jaffa as hostile;
- Tok'ra-friendly/neutral relationships must remain distinguishable where the external mods expose them;
- WNG should not create duplicate Goa'uld, Tok'ra or Jaffa world factions solely to make its craft spawn;
- if external faction identity cannot be resolved safely, the optional craft content stays inactive rather than binding to the wrong faction.

## Activation matrix

- **WNG only:** all core WNG Replicator/Wraith/Asuran/gravtech content works; no CatCraft/ONAC/RimGate calls are required.
- **WNG + Stargates!:** optional gate-entry/exit behavior, iris/direction/roof interactions and Stargate raid enhancements activate.
- **WNG + ONAC + its required RimGate/Jaffa dependency:** Jaffa/Goa'uld faction interoperability plus Al'kesh and Ha'tak-derived content activate.
- **WNG + Stargates! + ONAC/RimGate-Jaffa:** both integration families may cooperate, while ownership boundaries remain unchanged.
- Missing optional mods must produce no red errors and no broken Def references.

## Implementation order from this checkpoint

1. Keep current Wraith Dart native-shuttle/culling work intact.
2. Finish Puddle Jumper two-pass Ancient-drone layer.
3. Build dependency-free optional-mod/faction detection utility for Stargates!, ONAC and RimGate/Jaffa.
4. Verify exact package IDs and external Jaffa/Goa'uld/Tok'ra Def names from the actual installed/reference mods before writing activation patches.
5. Add `WNG_AlkeshTransport` as a native-boardable shuttle foundation; keep its faction activation dormant until the external identity bridge verifies a correct Jaffa/Goa'uld owner.
6. Design/build `WNG_HatakGravship` on the Odyssey gravship system, using Ha'tak lore as the mechanical/visual guide and the current WNG gravtech architecture rather than historical Gravcore code.
7. Reconcile all craft against historical WNG code only as reference evidence.
8. Compile/test in at least these configurations conceptually/where CI can represent them: WNG alone; WNG + Stargates; WNG + ONAC/RimGate-Jaffa; all integrations together.

## Mandatory design question for every integration feature

Before implementation ask:
1. What is this in Stargate?
2. What does it actually do in Stargate?
3. What does Vardath's current plan require?
4. What did historical WNG do?
5. Which external mod owns the underlying system/faction?
6. How can WNG add value without taking ownership or becoming dependent on it?
