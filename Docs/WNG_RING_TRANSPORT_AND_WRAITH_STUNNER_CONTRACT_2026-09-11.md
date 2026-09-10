# WNG ring transport + Wraith stunner contract — 2026-09-11

Newest explicit Vardath instruction overrides this checkpoint.

## Design authority

Fresh WNG implementation is based on:
1. Vardath's current requirements;
2. Stargate television-series behavior/lore as reference;
3. RimWorld 1.6 native loading/ThingOwner/map-transfer contracts where they fit;
4. optional ONAC/RimGate/Stargates integration without hard dependency.

Historical WNG code is reference evidence only and is not a known-good source.

---

# Goa'uld transport rings

## Stargate reference behavior

The Goa'uld transport-ring system is a matter transporter related to Ancient technology and widely used aboard Goa'uld vessels.

Relevant canon/reference points for WNG:
- ring platforms move people and cargo;
- rings are commonly installed on Goa'uld ships including Ha'tak/Tel'tak-class craft;
- ring rooms exchange occupants/cargo without a Stargate-style address/dialing sequence;
- ring transport can operate ship-to-ground in canon even without a permanently installed receiving platform when the sending assembly can physically project rings onto the target location;
- ring transport is distinct from Stargate wormhole travel and from Wraith Dart culling storage.

For ordinary player-built WNG gameplay, installed endpoint-to-endpoint transfer is the deterministic baseline. Receiverless tactical ring projection may be added later as a separate craft/raid capability; it must not complicate the basic player network.

## Core WNG behavior

`WNG_GoauldTransportRings` is a buildable transport platform.

It must:
- use RimWorld's native `CompTransporter` loading assignment wherever practical so people, animals, mechs and items are selected/loaded using familiar shuttle/transporter workflow;
- preserve exact pawn/item identity rather than creating copies/proxies;
- transport the actual loaded things;
- require no Stargate dialing/address sequence;
- work between compatible powered ring platforms on the **same map**;
- work between compatible powered ring platforms on **different currently loaded maps**;
- be buildable on ordinary maps and on gravships;
- preserve save/load state while cargo is loaded;
- never require CatCraft Stargates!, ONAC or RimGate merely to load WNG itself.

## Destination selection / 3+ ring rule

A sender never guesses which ring should receive the transfer.

When loaded rings are activated:
1. gather every other compatible powered ring endpoint owned by the same faction on all currently loaded maps;
2. present a selectable list;
3. sort by map label, then position;
4. display the map name plus endpoint position, e.g. `Colony — rings at 113, 88` or `Gravship map — rings at 42, 51`;
5. same-map endpoints are included, so rings can service sealed vaults/rooms, internal ship compartments, prisons, storage rooms, etc.;
6. sender itself is excluded;
7. unpowered/inactive/incompatible endpoints are excluded.

This resolves 3, 4, or any larger number of ring platforms without introducing dialing.

Custom user-assigned endpoint names are desirable later, but map label + position is the first-build deterministic identity and must remain a fallback even after renaming support exists.

## Transfer semantics

First-build transfer is endpoint-to-endpoint and immediate once activated:
- sender and receiver must both be powered;
- loaded assignment must be complete before activation;
- all currently loaded exact contents are transferred in one ring cycle;
- arrival is constrained to the receiving platform footprint/nearby valid ring area so sealed-room use does not randomly eject cargo outside the destination room;
- pawns receive the normal teleport-position refresh notification after rematerialization;
- failed placement must not silently delete any Thing;
- after a successful cycle the sender's native transporter loading state is reset for the next load.

## Loaded maps vs unloaded world sites

The first build deliberately exposes only currently loaded maps because RimWorld has a concrete Map and spawn grid for those destinations. A persistent ring endpoint on an unloaded world site is a separate world-object problem and must not be faked by deleting/recreating pawns.

If later requested, unloaded-map ring transfer must use a persistent exact-Thing/world-pawn handoff with deterministic rematerialization when the map is regenerated.

## Gravship interaction

Transport rings are normal buildable structures and may be installed on gravship substructure.

They are not themselves a grav engine, thruster or shuttle and do not become part of Odyssey fuel/range calculation. They simply remain on the gravship map/structure as carried buildings and continue to participate in the ring endpoint registry while their map is loaded and the platform is powered.

A Goa'uld Ha'tak family should include transport rings as an expected internal ship system rather than an optional decorative prop.

## Architect/category routing

Transport rings are Goa'uld technology.

- When ONAC is active and its verified `ONAC_Architect` category exists, WNG Goa'uld buildables should appear in that existing ONAC Architect category.
- When ONAC is absent, the same standalone-capable WNG Goa'uld buildables appear in the WNG Architect category instead.
- RimGate and CatCraft Stargates! are not required merely to expose the ring transporter.
- The same category-routing mechanism should be reusable by standalone-capable Goa'uld shuttles, gravship parts and future Goa'uld infrastructure.

This supersedes the older assumption that all Goa'uld WNG construction must disappear whenever ONAC is missing. External ONAC/RimGate factions/biology remain externally owned when those mods are present; WNG standalone fallback must not invent duplicate external factions.

## Resource/progression follow-up

The current ONAC integration uses verified ONAC liquid Naquadria when ONAC is present. Making the broader Goa'uld shuttle/gravship family standalone requires a separately reconciled fallback material/fuel route which must not create two competing ONAC resources when ONAC is installed.

The ring transporter itself may use ordinary WNG/vanilla construction materials in the first standalone implementation and does not need liquid fuel to move a local load.

---

# Wraith stun staff / stunner

## Lore identity

The Wraith ranged stunner is a capture weapon, not their normal kill weapon. It fires a blue/electrifying energy discharge intended to paralyze/immobilize prey so the Wraith can capture them for later feeding. This is consistent with Wraith culling doctrine and WNG's captivity systems.

Vardath's requested game identity is **Wraith stun staff**: a staff/rifle-form ranged Wraith weapon used to incapacitate rather than kill.

## Gameplay contract

Add `WNG_WraithStunStaff` as a real equippable ranged weapon.

Requirements:
- ranged primary attack;
- projectile/impact applies a direct temporary stun/immobilization effect;
- ordinary intended hit should not deal lethal health damage;
- usable against normal stun-capable pawns including humans, animals and mechs where RimWorld's own stun handler permits it;
- tuned to facilitate capture/culling rather than function as a high-DPS rifle;
- Wraith combat PawnKinds should preferentially spawn with it where appropriate;
- player-acquired examples remain usable as ordinary equipment;
- final weapon/projectile art and professional audio are required later; vanilla visuals may be used only as mechanics placeholders during the fresh implementation.

The weapon is distinct from:
- Goa'uld staff weapon (lethal plasma weapon);
- zat'ni'katel;
- Wraith Dart culling beam;
- Wraith melee feeding hand ability.

---

# Immediate implementation order from this contract

1. Add generic conditional Goa'uld Architect routing: ONAC category when present, WNG fallback otherwise.
2. Add the standalone buildable ring platform using native `CompTransporter` loading.
3. Add powered same-map and cross-loaded-map destination selection and exact-content transfer.
4. Validate save/load/load-dialog/transfer compile and XML contracts.
5. Add the Wraith stun staff/stunner as a nonlethal ranged capture weapon and assign it to suitable Wraith combat PawnKinds.
6. Validate weapon projectile/stun contracts.
7. Reconcile standalone Goa'uld Al'kesh/Ha'tak resource and research fallback so those craft can follow the same ONAC-category/WNG-category routing without duplicating ONAC-owned factions/resources.
8. Add final Stargate-authentic ring animation/art/audio and Wraith stunner art/audio in the presentation pass.
