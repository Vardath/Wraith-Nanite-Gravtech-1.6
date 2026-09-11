# WNG Replicator Queen sovereignty checkpoint — 2026-09-11

Author/final design authority: **Vardath**.

## Scope

This slice implements the first genuine sovereign-control layer between the exact persistent Replicator Queen and the existing physical block-Replicator system. It does not replace block ecology, adaptation, matter, EMP, containment or hierarchy mechanics.

## Controller-domain model

Every WNG block Replicator inherits `CompReplicatorSovereignty` from `WNG_ReplicatorRaceBase`.

The persistent authority model distinguishes:
- `Queen` — implemented here;
- `NeuralLattice` — reserved for the later Sovereign Neural Lattice implant;
- `TemporaryAsuran` — reserved for later temporary lattice intrusion;
- `None` — ordinary autonomous block behavior.

Those authority types intentionally share one persistence/transaction model but are not treated as equivalent ownership sources.

## Exact Queen boundary

Only the exact pawn stored by `GameComponent_ReplicatorQueenState` qualifies for innate Queen authority. Ordinary Asuran operatives, technicians and commanders do not receive Queen control merely because they use the same nanite-humanoid physiology.

Current first-build tuning remains Def-driven through `ReplicatorQueenSovereigntyExtension`:
- direct acquisition range: 40 cells;
- nearby-swarm acquisition radius: 24 cells;
- normal acquisition cap: 12 currently controlled spawned block bodies on the Queen's map.

These values are tuning, not immutable design doctrine.

## Genuine ownership

Successful Queen acquisition:
- targets an exact living WNG block pawn;
- records the exact Queen reference and a save-persistent Queen domain key;
- records the block's pre-control faction;
- changes the exact block pawn to the Queen's current faction;
- interrupts its previous job immediately.

This is genuine controller state and real faction ownership, not an outbreak modifier, stat aura or proxy pawn.

If authority is released or becomes invalid, the block returns to its recorded prior faction when possible, otherwise to the real `WNG_ReplicatorSwarm` faction.

## Physical-presence rule

Queen authority remains valid only while controller and block remain physically together:
- both spawned on the same map; or
- both present in the same caravan.

If the Queen is dead, no longer the exact Queen, changes away from the recorded control faction, or is physically separated from the block, that block releases the invalid authority rather than pretending remote global control.

## EMP and containment

Sovereignty does not bypass existing block counterplay.

Control acquisition/commands are blocked when:
- the Queen's human-form nanite lattice is EMP disrupted;
- the block is EMP suppressed;
- the block is inside active WNG Replicator containment;
- the Queen herself is inside active containment.

Controlled blocks use a dedicated non-combat suppression job while sovereign command is interfered with. That suppression is above queued/player orders in the Replicator ThinkTree and cannot be replaced by damage-triggered ordinary combat AI.

## Player command surface

Because vanilla Biotech normally hides ordinary mech drafting without a mechanitor overseer, Queen sovereignty does not pretend that native mechanitor control exists.

Queen-controlled blocks expose dedicated controller-linked commands:
- move;
- attack an exact hostile target;
- Repairer: repair an exact same-domain Replicator;
- Burrower: breach an exact eligible structure;
- explicitly recombine upward when the normal hierarchy requirements are met;
- release sovereign control.

The Queen exposes commands to acquire one eligible block, acquire an eligible nearby swarm up to the configured cap, and release currently spawned blocks in her domain.

## Hierarchy persistence

Sovereign identity is preserved through the real existing physical hierarchy transactions.

Upward recombination conserves:
- learned adaptations;
- stored Replicator matter;
- controller authority/domain.

Genuine destruction split conserves:
- learned adaptations;
- divided stored Replicator matter;
- controller authority/domain;
- the normal post-breakup recombination lockout.

Different controller domains cannot recombine merely because their current faction is the same.

## Specialist/retaliation isolation

Controller coordination, Repairer targeting and local retaliation signaling now use the same sovereign-domain test rather than faction equality alone. This prevents future Queen/implant/temporary-Asuran controller domains from leaking coordination or repair into one another.

Operational sovereign authority is also a valid combat-permission source for later hostile controlled domains; EMP/containment interference disables that operational permission.

## Deliberately unfinished

Still required later:
- Sovereign Neural Lattice implant acquisition/control rules and bounded capacity;
- temporary Asuran lattice-intrusion override plus restoration/expiry semantics;
- recurring Asuran Queen-recovery attacks after the first operation;
- real consequences/threat composition if the Asuran Lattice retains the captured Queen;
- mixed Asuran + sovereign block-Replicator threats;
- infiltration;
- live RimWorld validation and balance testing of Queen range/cap/commands.

## Validation status

The source/Def/API surface was checked against RimWorld 1.6 decompiled definitions for Hediff gizmos, `ThingComp.CompGetGizmosExtra`, targeting commands, `JobDefOf.Goto`, JobDef damage-override controls, faction changes and the existing WNG hierarchy/specialist/EMP/containment interfaces.

Temporary GitHub Actions validation run **34576583840** completed successfully:
- `Source/WNG/WNG.csproj` C# build: **SUCCESS**;
- all current Def/Patch XML parsed: **SUCCESS**;
- Queen-sovereignty wiring/domain invariants: **SUCCESS**.

The temporary validation workflow was removed immediately afterward and is not part of the promoted net diff.

No claim is made that RimWorld itself was launched in this environment; live gameplay validation remains required.
