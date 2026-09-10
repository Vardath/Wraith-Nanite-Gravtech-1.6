# WNG — Shuttle / Stargate-entry / Replicator behavior contract

Author/final design authority: **Vardath**.

This document records the current first-build contract for the four primary WNG shuttle/craft families, Stargate-entry raids, and autonomous block-Replicator behavior. Newer explicit Vardath instructions override it.

## Four primary shuttle/craft families

Exact craft names retained from the current design history:

1. `WNG_WraithDart`
2. `WNG_WraithStrikeCraft`
3. `WNG_WraithCruiser`
4. `WNG_PuddleJumper`

Family ownership:
- Wraith: Dart, Strike Craft, Cruiser.
- Ancient/Asuran/Precursor: Puddle Jumper.

All four must use native RimWorld/Odyssey shuttle transport and boarding systems wherever available. WNG must not replace vanilla/native boarding with a custom pseudo-boarding implementation.

### Wraith-specific craft behavior

`WNG_WraithDart` carries the Wraith ray-of-absorption/culling system. Captured pawns remain exact pawns in the craft's real transport/capture buffer until recovered, the craft is destroyed with an explicitly resolved outcome, or the Wraith successfully escape with the Dart and transfer those exact pawns into WNG captivity.

The Wraith Strike Craft and Cruiser remain Wraith craft variants but are not automatically given Dart absorption unless explicitly specified later.

### Ancient/Asuran/Precursor shuttle behavior

`WNG_PuddleJumper` uses native shuttle boarding/transport behavior and retains the Ancient-style control-chair/drone identity. Its offensive pass behavior uses Ancient chair-controlled drone fire rather than Wraith absorption.

## Stargate-entry raids with shuttle support

Optional Stargate integration remains an enhancement, not a requirement for ordinary raids.

When a Stargate-entry raid includes a Wraith Dart or Puddle Jumper:
- the shuttle may emerge through the gate as part of the raid;
- if the gate exit is under an ordinary constructed/light roof, the craft breaks through the roof, takes some damage, remains functional if it survives, then performs its attack-pass sequence;
- if the gate exit is under a heavy/rock roof, the shuttle cannot safely punch through: it explodes and causes a cave-in/collapse event around the entry area;
- after successful entry, the craft performs **two real flyby passes**, then lands and participates in the raid/outcome;
- Wraith Dart passes attempt ray-of-absorption capture of a couple of valid pawns per opportunity, bounded by an absolute craft capacity of **up to 3 exact pawns**;
- Puddle Jumper passes fire a small number of Ancient drone shots;
- the landed craft remains governed by native shuttle boarding/transport rules;
- surviving Wraith may retreat using their Dart; if they abandon it, it remains hackable salvage under the separate Dart salvage contract;
- surviving Asuran/Ancient users may retreat using the Jumper through native boarding/launch behavior.

The roof interaction must be based on the actual roof over/around the gate emergence cells, not a generic map-wide roof flag.

## Autonomous block-Replicator behavior

Default autonomous block-Replicator priorities are:

**consume -> convert material to replication matter/fuel -> reproduce -> combine into larger forms -> continue consuming**

They are not ordinary kill-on-sight raiders during the normal feeding phase.

### What they consume

Replicators may consume, where reachable/applicable:
- floors/terrain layers;
- gravship/substructure terrain or equivalent structural floor layers;
- roof material, including natural rock roof where mechanically representable;
- loose items;
- buildings/structures;
- trees/plant biomass;
- other accessible material targets not protected by explicit containment rules.

Burrowers still prioritize genuine access blockers/containment where needed to reach matter, but ordinary swarm members should preferentially consume rather than attack pawns.

### Provocation and local retaliation

Before terminal map consumption is reached, Replicators attack pawns only when:
- they are directly provoked/attacked; or
- they have exhausted usable replication matter/fuel and cannot obtain more through feeding.

When one Replicator is provoked, it sends a local retaliation signal. At most **five nearby Replicators** are recruited into that retaliation response, including/alongside the provoked unit as implementation permits. The larger swarm does not globally switch into combat mode; most Replicators continue consuming, reproducing, and combining.

Retaliation must be bounded in range/time and save-safe. It must not permanently turn the entire faction into a kill-on-sight swarm.

### Terminal consumption state — map stripped of matter

Once the swarm has consumed roughly **90–95% of the map's eligible consumable matter**, it enters a terminal predation state.

First-build tuning target: **92.5% consumed**, with the trigger centralized/tunable so Vardath can move it anywhere in the 90–95% band without code edits.

Required behavior after the threshold is crossed:
- the swarm stops preserving living/moving targets as non-food obstacles;
- Replicators may attack **all remaining attackable entities on the map**, including colonists, prisoners, visitors, hostile pawns, animals, insects/creatures and mechs;
- this is a swarm-wide terminal state, unlike the ordinary bounded five-unit retaliation response;
- surviving Replicators may still consume/reproduce/combine opportunistically, but exterminating remaining entities becomes a valid high-priority behavior;
- terminal state should be derived from a stable measure of how much eligible map feedstock has actually been consumed, not ordinary raid points or faction hostility;
- the baseline/remaining-consumable accounting must be save-safe and should avoid expensive full-map recalculation every tick;
- newly created/dropped material after terminal state does not automatically make the swarm peaceful again unless a future explicit design says it should. The terminal state is latched for that map encounter once genuinely reached.

The phrase “biological” in the design intent does not exclude mechs here: the terminal behavior target set is deliberately broader and includes mechs as well as organic life.

### Material inheritance

What the swarm consumes influences what newly produced Replicators are physically made from.

Required examples:
- trees/wood/low-grade organic feed -> weaker bodies and increased flammability/fire vulnerability;
- ordinary industrial material -> baseline phenotype;
- high-grade/high-tech/strong materials -> stronger bodies and appropriate defensive/performance buffs.

Material phenotype is inherited by newly reproduced Replicators and carried through combine/split transformations in a mass/state-conserving way. Learned technological adaptations remain a separate cumulative system; material phenotype must not overwrite shield/grav/power/etc. adaptation history.

The phenotype should be derived from meaningful recent/accumulated feedstock rather than an arbitrary cosmetic label, and its gameplay effects must be centralized/tunable rather than scattered magic constants.

### Destruction and lower-tier breakup

A genuinely destroyed higher-form Replicator produces **both**:
- living Replicators of the next tier down; and
- loose Replicator blocks/material from the destroyed body.

Current physical ladder remains:
**Drone/base -> Hunter -> Bulwark -> Titan -> Siege Mass**.

Therefore genuine destruction reverses that ladder one step while also shedding loose blocks:
- Siege Mass -> Titans + loose blocks;
- Titan -> Bulwarks + loose blocks;
- Bulwark -> Hunters + loose blocks;
- Hunter -> Drone/base Replicators + loose blocks;
- Drone/base -> loose blocks only.

Intentional upward recombination is not destruction and must not emit breakup offspring or salvage blocks as though the source units died.

### Loose Replicator blocks: reformation risk

Loose Replicator blocks are dangerous salvage rather than inert scrap.

Required behavior:
- a sufficiently large pile has a **full one in-game day dormancy/recombination timer** before it may reform a Replicator;
- after that day expires, the chance of reformation starts low and **grows the longer the blocks remain exposed/uncontained**;
- a successful reformation consumes an appropriate amount of blocks and creates a hostile base Replicator;
- remaining blocks restart the dormant cycle after a successful reformation rather than chaining instantly;
- powered containment suppresses reformation and resets/freezes the danger clock;
- save/load preserves the dormancy/exposure state correctly.

### Disposal and EMP-room containment

Players need two practical ways to manage loose Replicator blocks:

1. **Smelting/destruction:** Replicator blocks are smeltable at an appropriate vanilla electric smelter or equivalent standard smelting worktable. Smelting permanently destroys the processed blocks so they cannot later reform. The recipe should use normal RimWorld bill/worktable behavior rather than a custom disposal UI.

2. **EMP containment room:** Replicator blocks can be stored safely in a room kept under continuous/regular EMP suppression. The intended buildable approach is a **wall-mounted EMP pulser/emitter** (or equivalent compact EMP field device) that lets the player convert an ordinary enclosed room into a Replicator containment room. While the room is effectively EMP-suppressed:
   - loose blocks cannot advance/reform;
   - active block Replicators inside the effective field are suppressed consistently with the main Replicator EMP rules;
   - the reformation danger clock is reset/frozen as appropriate;
   - loss of power/EMP coverage restarts the normal one-day exposed dormancy period before reformation risk resumes.

The EMP-room system should be based on actual covered cells/room geometry, not just a decorative room label. It should coexist with the dedicated Replicator containment projector rather than making one or the other meaningless. Exact emitter radius/pulse interval/power draw remains tunable.

## Separation rules

- Native shuttle boarding/transport owns boarding for Wraith and Asuran craft.
- CatCraft owns Stargate network/address/dial/iris/shield/receive-buffer mechanics.
- WNG owns raid composition, shuttle entry/roof interaction, attack passes, absorption/drone attacks, landing, objectives, retreat outcomes, and WNG captivity.
- Wraith Dart absorption is distinct from ordinary Drain Life and strategic faction hunger.
- Replicator material feeding is distinct from Wraith Life Force/biomass systems.
- Replicator block disposal/EMP containment is player counterplay to the Replicator ecology and must remain compatible with normal vanilla hauling, bills, rooms and power behavior.

## Current implementation checkpoint — 2026-09-10

Implemented on public `main` in the fresh rebuild:
- local save-persistent Replicator retaliation state;
- provocation recruits a bounded nearby response rather than globally switching the swarm to ordinary raid combat;
- adaptive ranged fire now obeys retaliation permission rather than firing opportunistically at any hostile pawn;
- starvation checks look for usable nearby feedstock before triggering combat;
- loose Replicator blocks wait one full day before any reformation attempt;
- post-dormancy reformation chance grows with continued exposure and resets after successful reforming or powered containment;
- room-wide powered EMP containment uses actual enclosed-room geometry;
- `WNG_WallEMPContainmentPulser` exposes that room-wide EMP containment as a buildable powered device;
- `WNG_SmeltReplicatorBlocks` uses a normal electric-smelter bill to permanently destroy blocks;
- material phenotype is accumulated across feedstock instead of simply replacing the last material label;
- material phenotype is copied into offspring and through existing combine/split state transfer;
- organic/wood-heavy phenotype increases ordinary damage taken and strongly increases fire damage, while reinforced/advanced phenotype reduces damage;
- plant/tree targets are now consumable feedstock and influence the inherited material phenotype.

Explicitly still unfinished:
- floor/terrain consumption;
- gravship/substructure terrain consumption;
- roof consumption, including natural rock roof;
- terminal 90–95% map-consumption accounting and latched swarm-wide predation state;
- those map-consumption features require dedicated cell/map accounting rather than being forced through the existing Thing-target assimilation job;
- four native-boardable shuttle Defs/behavior layer;
- Dart absorption/flyby/landing/hacking implementation;
- Puddle Jumper chair/drone flyby implementation;
- Stargate roof breakthrough/heavy-roof explosion/cave-in entry behavior.
