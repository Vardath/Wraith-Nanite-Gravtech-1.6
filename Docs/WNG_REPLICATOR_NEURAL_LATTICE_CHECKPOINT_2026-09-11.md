# WNG Sovereign Neural Lattice checkpoint — 2026-09-11

Author/final design authority: **Vardath**.

## Status

The implementation described here has passed the candidate C# / XML / narrow wiring validation described below. It is not a claim of live RimWorld gameplay validation.

## Stargate identity / extrapolation boundary

`Sovereign Neural Lattice` is a **WNG-specific technology name and implementation**, not a claim that Stargate canon names an implant this way.

The Stargate basis is the demonstrated Replicator command architecture:
- Reese created block Replicators to obey her and could directly order them to stop;
- the Asgard later exploited a command embedded in Reese to call Replicators together;
- block Replicators are shown/recorded as receiving commands from an external source;
- later human-form Replicators command block-based Replicator forces.

WNG extrapolates from that established controller/network behavior into a deliberately weaker surgically implanted controller interface.

## Identity boundary

The implant bearer is **not** the Replicator Queen.

- innate Queen authority remains exclusive to the one exact pawn stored by `GameComponent_ReplicatorQueenState`;
- ordinary Asurans/humans/human-form Replicators may use the implant if it is physically installed;
- installing the implant on the exact Queen does not create a second overlapping controller domain; her innate Queen authority remains authoritative;
- `Queen`, `NeuralLattice` and later `TemporaryAsuran` remain distinct authority domains even when their current faction is the same.

## Physical implant / acquisition

The implementation adds a tangible `WNG_SovereignNeuralLattice` health item.

It is manufactured only at `WNG_AsuranWorkshop`, in accordance with the Asuran production contract, using:
- one real `WNG_ReplicatorCoreFragment` as the recovered command-lattice source;
- Asuran `WNG_NaniteSludge`;
- plasteel;
- advanced components.

The current first-build fabrication quantities/work values are Def-tunable and not permanent doctrine.

Research `WNG_SovereignNeuralLatticeResearch` requires both:
- `WNG_ReplicatorStudy`;
- `WNG_AsuranFabrication`.

This keeps the progression tied to actual recovered Replicator command structure plus the real WNG Asuran nanite-production branch.

## Surgery

Installation uses RimWorld's native `Recipe_InstallImplant` on the **Brain** and adds `WNG_SovereignNeuralLattice`.

Removal uses native `Recipe_RemoveImplant`.

The Hediff defines `spawnThingOnRemoved=WNG_SovereignNeuralLattice`, so successful native removal returns the physical implant item rather than deleting or proxying it.

`Hediff_SovereignNeuralLattice.PostRemoved()` releases the exact bearer's Neural-Lattice controller domain when the implant is removed.

## Controller-domain behavior

The implant reuses the already-live `CompReplicatorSovereignty` architecture.

A successfully acquired block stores:
- `ReplicatorControlAuthority.NeuralLattice`;
- the exact implant bearer pawn reference;
- the exact bearer's save-persistent domain key;
- the block's pre-control faction;
- the bearer's current control faction.

The exact physical block pawn changes faction to the bearer's real faction. This is not an aura, abstract outbreak bonus, proxy Replicator or fake mechanitor relationship.

Current first-build tuning is Def-driven:
- target acquisition range: **24 cells**;
- normal acquisition cap: **3 controlled block bodies**;
- implant EMP disruption after EMP damage: **1,800 ticks**.

The implant does **not** receive the Queen's nearby-swarm seizure command. Acquisition is exact-target only.

The 24-cell value is the current **acquisition range**. After acquisition, controller validity uses the shared physical-presence rule (same map or same caravan). This keeps ordinary move/attack orders from silently severing a valid implant domain while still making the implant materially weaker than Queen control through shorter acquisition range, exact-target-only acquisition and a much smaller normal acquisition cap. Vardath may change this after live testing.

## EMP / containment

The implant does not bypass Replicator counterplay.

Acquisition/command is blocked when:
- the implant itself is EMP disrupted;
- the target block is EMP suppressed;
- the target block is inside active WNG Replicator containment;
- the implant bearer is inside active containment.

An already controlled block under interference uses the existing high-priority sovereign suppression path rather than falling into unrelated autonomous combat.

## Hierarchy / specialists / state

Neural-Lattice-controlled blocks use the same real block systems as Queen-controlled or autonomous bodies:
- learned adaptations remain real;
- stored Replicator matter remains separate from human-form Nanite Reserve;
- Controller/Repairer/retaliation use exact controller-domain identity;
- split children inherit the exact Neural-Lattice controller/domain;
- upward recombination conserves stored matter, adaptation and controller identity;
- blocks from different Queen/implant/later-temporary-Asuran domains cannot merge merely because their faction matches.

The configured acquisition cap governs new acquisitions; genuine hierarchy breakup may temporarily create more physical bodies in the same exact domain because controller identity is conserved rather than stripped from valid split children.

## Presentation boundary

The item currently uses RimWorld's vanilla health-item graphic as a **mechanics placeholder only**.

No art was generated. Dedicated final Sovereign Neural Lattice item/UI presentation remains required for the later professional art pass.

## Explicitly still later

- temporary Asuran lattice intrusion with explicit expiry/restoration semantics;
- recurring Queen recovery operations;
- hostile/captured-Queen sovereign consequences and mixed Asuran + block threats;
- infiltration;
- broader Neural Interface copy/reconstruction operations;
- live RimWorld validation of implant fabrication, surgery, save/load, acquisition, EMP/containment, split/recombine and removal/release.

## Validation

Temporary GitHub Actions run **34580535936** exposed one concrete C# defect before promotion: the candidate attempted to set protected `Gizmo.disabled` directly for the Queen-only explanatory command. That was corrected to RimWorld's public `Command.Disable(...)` API.

Corrected temporary run **34580647197** then completed successfully:
- `Source/WNG/WNG.csproj` C# build: **SUCCESS**;
- all current Def/Patch XML parsed: **SUCCESS**;
- physical item/Hediff/research/fabrication/install/remove wiring checks: **SUCCESS**;
- `NeuralLattice` controller-domain/hierarchy wiring checks: **SUCCESS**.

The temporary workflow is removed before promotion and is not part of the intended net mod diff.

Static validation is not live RimWorld gameplay validation.
