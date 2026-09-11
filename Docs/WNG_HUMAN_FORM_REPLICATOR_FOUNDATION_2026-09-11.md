# WNG human-form Replicator / Asuran foundation checkpoint — 2026-09-11

Author/final design authority: **Vardath**.

## New explicit Vardath correction

Human-form Replicators/Asurans use RimWorld's **food system itself as their personal nanite/matter reserve**.

- They ingest ordinary edible matter through native RimWorld eating behavior.
- The need is displayed as **Nanite Reserve**, not Food.
- Eating refills the reserve because matter is broken down into nanite feedstock.
- Normal operation drains it through the native Need_Food loop.
- Self-repair and nanite fabrication spend additional reserve.
- Critical depletion weakens the lattice and leads toward shutdown rather than biological malnutrition.

This is an explicit exception to the older broad lore-protocol sentence rejecting Replicator hunger/survival fuel. The exception applies to **human-form nanite bodies only**. Block Replicators retain their existing separate matter/reproduction/adaptation economy and do not gain hunger.

## Stargate reconciliation

GateWorld's Replicator references distinguish block Replicators from humanoid Replicators made of microscopic nanites, while Asurans are the Pegasus human-form nanite civilization derived from Ancient anti-Wraith technology. Canon supports nanites replacing damaged biological cells and Asuran reconstruction; using edible RimWorld matter as a personal finite feedstock reserve is a WNG gameplay extrapolation approved explicitly by Vardath, not a claim that canon depicts Asurans eating meals.

## Implemented foundation in this slice

- `WNG_NaniteHumanoid` Biotech xenotype is the common physical identity for Asurans and compatible human-form Replicator individuals.
- Political role/faction/caste remains separate from physical identity.
- `WNG_AsuranNanitePhysiology` disables vanilla Food and enables `WNG_NaniteMatterReserve`.
- `WNG_NaniteMatterReserve` uses the exact native `Need_Food` class so ordinary eating, feeding and caravan nutrition systems remain available without a parallel hunger implementation.
- vanilla biological Malnutrition is suppressed/reconciled away; `WNG_NaniteDepletion` supplies synthetic low-reserve consequences.
- biological ageing is stopped by the nanite physiology gene.
- the body is sterile and immune to food poisoning effects from its feedstock intake.
- `WNG_AsuranNaniteLattice` spends the same reserve to repair ordinary injuries.
- EMP temporarily disrupts the lattice, impairs capacities and suspends self-repair.
- the existing Asuran workshop assembly ability now spends a fraction of the same Need_Food-derived reserve rather than a second Gene_Resource bar.
- `WNG_AsuranLattice` is a hidden hostile operational faction for later recovery/infiltration/mixed-threat systems.
- current ordinary Lattice PawnKinds are Operative, Technician and Commander.
- none of these ordinary PawnKinds receives sovereign Queen authority.

## Explicitly still unfinished

- exact unique female Replicator Queen, age 13, cryosleep recovery and immediate player recruitment;
- Queen recovery/kidnap AI and exact physical carrier exit semantics;
- sovereign block-Replicator control tied to the exact Queen;
- Sovereign Neural Lattice implant control;
- temporary Asuran lattice intrusion;
- mixed human-form + block Replicator threats;
- infiltration/impersonation mechanics;
- complete synthetic disease/implant/temperature/vacuum physiology audit;
- dedicated Asuran/human-form art/audio/presentation;
- live RimWorld validation of the custom Need_Food substitution, eating AI, caravans, repair, EMP and save/load.

## Validation boundary

This checkpoint records source/Def implementation. It does **not** claim live-game validation until RimWorld is actually run with the current build.
