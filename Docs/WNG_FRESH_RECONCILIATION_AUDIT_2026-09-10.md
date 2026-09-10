# WNG fresh rebuild reconciliation audit — 2026-09-10

Author/final design authority: **Vardath**.

This audit checks the current fresh RimWorld 1.6 rebuild against four inputs:
1. newest explicit Vardath instructions and current public WNG contracts;
2. Stargate canon/source behavior;
3. historical WNG implementation as reference evidence only;
4. the current fresh source on public `main`.

Conclusion: **the fresh rebuild remains structurally usable and should continue. A whole-mod restart is not warranted.** The deviations found in this pass were localized behavioral leaks and have been corrected or explicitly quarantined as unfinished.

## Replicators

### Canon/source identity checked
- Milky Way block Replicators are purely mechanical self-replicating machines.
- Reese created the original machines as toys, later teaching them to replicate and protect themselves/her.
- Their core purpose is self-replication; they consume raw material and can incorporate/modify technology they consume.
- Individual blocks can combine into multiple forms for different tasks.
- Resource consumption is **not biological hunger and not survival fuel**.

### Current WNG design checked
Normal autonomous behavior:
`consume useful matter -> convert it into replication mass/state -> make more Replicators -> combine into larger forms -> continue stripping the map`.

Combat rules:
- no ordinary kill-on-sight behavior while the swarm is in its normal consumption phase;
- direct provocation causes the attacked unit to retaliate and recruit at most five nearby Replicators;
- the rest of the swarm continues consuming/reproducing/combining;
- no starvation trigger exists;
- terminal map-clearing aggression begins only after roughly 90–95% of eligible map matter has been consumed; first-build target remains 92.5%, tunable.

### Historical WNG material consulted
Historical `Source/WNGR2/Replicators/` contains distinct implementations for hierarchy, material state, matter reassembly, control, regeneration, salvage and the Child's Toy transformation. These are useful evidence for intended relationships and edge cases but are not restored wholesale.

### Fresh implementation status
Sound and retained:
- separate state/adaptation/hierarchy/assimilation/containment/EMP/matter/reassembly/regeneration/specialist components;
- base -> Hunter -> Bulwark -> Titan -> Siege Mass upward recombination;
- genuine destruction creates next-tier-down living Replicators plus loose blocks; base form produces blocks only;
- intentional recombination does not fake a death split;
- split children preserve learned state/matter and have a recombination lockout;
- loose blocks have one full day dormant before reformation risk starts, then chance grows with exposure;
- powered containment resets/suppresses the block danger clock;
- blocks can be disposed through a normal electric-smelter bill;
- room-wide EMP containment is exposed through a powered wall EMP containment pulser;
- plant/tree feedstock is consumable;
- accumulated material phenotype is inherited through reproduction/combine/split and remains separate from learned technological adaptations;
- weak organic/wood-heavy bodies are more vulnerable, especially to fire; stronger/high-grade feedstock produces tougher bodies;
- adaptive ranged fire obeys the same combat-permission boundary as other attacks.

Corrections made during this audit:
- removed all Replicator starvation/fuel-to-survive logic before this audit and confirmed no such behavior remains in the Replicator source;
- removed the ability for local retaliation to jump from the actual aggressor to an unrelated hostile pawn when the original target disappears;
- provocation now keys to the actual non-swarm pawn attacker rather than requiring pre-existing faction hostility;
- Controller and Artillery offensive helpers are explicitly combat-permission gated even when dormant in the current ThinkTree;
- Burrowers no longer treat every hostile structure as a military target while calm: outside combat they breach containment/access blockers in service of reaching matter, not generic destruction.

Still unfinished and must be implemented before the Replicator ecology is considered complete:
- floor/terrain consumption;
- gravship/substructure terrain consumption;
- roof consumption including natural rock roof;
- efficient map-wide accounting of eligible consumable matter;
- latched terminal-consumption state at the configured ~90–95% threshold;
- terminal target selection across remaining colonists, prisoners, visitors, animals, creatures/insects and mechs;
- exact reconciliation of material phenotype weights for terrain/roof/substructure feedstock;
- final gameplay tuning after in-game tests.

## Wraith biological core

### Canon/source identity checked
Wraith are biological predators dependent on feeding on life force. Food shortage materially affects their behavior and politics; they can hibernate for long periods and display rapid healing/regeneration when adequately fed. Hive ships commonly contain large hibernating populations watched by a small active caretaker/guard group.

### Current WNG design checked
- pawn-level Life Force and Drain Life are biological Wraith mechanics;
- strategic faction hunger influences feeding requests and raid pressure;
- ordinary Drain Life never opens the strategic feeding-request popup;
- Sable Brood never requests feeding;
- request-capable lineages only produce a box when genuinely strategically hungry;
- refusal/declining increases attack pressure according to lineage policy;
- Mature-Hive feeding stock is local hive ecology and does not directly invoke the strategic request UI.

### Fresh implementation status
Sound and retained:
- one core Wraith xenotype plus caste PawnKinds;
- Life Force resource with low-resource penalties/torpor and reduced drain during hibernation;
- regeneration tied to Life Force and accelerated by recent feeding;
- full Drain Life behavior: victim biological aging, Wraith rejuvenation with age floor, temporary Life Drained/Fed Recently state, lethal repeated full drain as currently planned;
- gene ability reconciliation after gene duplication/overwrite;
- four Wraith lineages with separate strategic hunger tuning;
- ordinary feeding and strategic faction hunger remain code-separated.

Lore/plan note:
- Wraith `starved` terminology is valid in the **Wraith** biological subsystem and must never be generalized to Replicators.
- The numeric strategic-faction hunger meter is a WNG gameplay abstraction derived from canon Wraith food scarcity, territorial hives and food-driven conflict; it is not presented as a literal on-screen canonical Wraith measurement.

## Mature Wraith Hive / captivity / rescue

### Canon/source identity checked
- Hive ships can hold large numbers of Wraith in hibernation with a small active caretaker force.
- Humans are retained for later feeding in organic storage/cocoons/holding systems.
- Keepers/caretakers are directly associated with guarding sleeping Wraith.

### Current WNG design checked
- exact captured pawn identity must persist through raid capture, Dart capture, holding sites, Mature-Hive feeding stock and rescue;
- ordinary sleepers, active demographic Wraith, combat reserve and human feeding stock are separate populations;
- finite Dormancy Vault reserve is not demographic growth;
- future Growth Chamber replacement must remain bounded by the Heart's founding population cap.

### Fresh implementation status
Sound and retained:
- exact-pawn `WraithCaptivityRegistry` rather than proxy victims;
- exact captor lineage and rescue timing/site state persist through save/load;
- ordinary Wraith raid kidnapping is adopted only after vanilla kidnapping has genuinely completed;
- rescue sites carry the exact abducted pawn rather than generating replacements;
- Mature Hive separates active founders, ordinary hibernators, finite combat reserve and biological feeding stock;
- Keeper/Queen-supervised feeding stock does not alter strategic faction hunger;
- Mature-Hive retaliation is a separate consequence system.

Lore/plan note:
- `WNG_WraithFeedingNiche` is a RimWorld gameplay abstraction of Wraith human storage/feeding infrastructure. Canon more commonly depicts organic cocoons/holding chambers rather than a RimWorld-style prisoner bed. Keep the mechanic unless Vardath changes it, but future art/naming/visual presentation should remain recognizably Wraith-organic rather than implying the bed itself is canonical.

Still intentionally unresolved:
- experimentation/conditioning/thrall/hybrid captive-treatment branch;
- Growth Chamber replacement;
- Hive Heart biological-resource/repair economy;
- professional Wraith audio/visual polish.

## Wraith Dart and shuttle layer

### Canon/source identity checked
- Wraith Darts are small gate-capable Wraith craft whose principal culling role uses a ground-sweeping transport/culling beam to dematerialize people into onboard storage and later rematerialize them.
- Puddle Jumpers are Ancient/Lantean gate-capable shuttle/fighter craft with neural/Ancient controls, retractable drive pods, cloaking capability and drone weapons.

### Current WNG design checked
- all four primary WNG shuttle/craft families must remain boardable through native RimWorld/Odyssey shuttle/transport systems;
- `WNG_WraithDart` gets the Wraith absorption/culling role;
- `WNG_PuddleJumper` gets Ancient control/drone identity;
- Stargate-entry support may perform two flyby passes then land;
- Dart can capture up to three exact pawns;
- abandoned surviving Dart remains hackable salvage and successful hacking releases exact buffered captives and transfers the craft intact;
- light-roof Stargate entry damages/breaks through; heavy/rock roof destroys the shuttle and causes collapse/cave-in;
- ordinary Wraith raids remain possible without Stargates.

### Historical WNG material consulted
Historical `WraithDartAbduction.cs` is useful for exact-pawn `CompTransporter` buffering, max-three capture, rollback on failed buffering and rescue-registry integration. Its disposable auto-withdraw/despawn behavior is rejected because it conflicts with the current same-craft landing/boarding/salvage contract.

### Fresh implementation status
- contracts and exact-pawn captivity support exist;
- actual fresh four-craft native-shuttle implementation remains **unfinished**;
- no old custom boarding implementation is to be restored.

## Child's Toy

This feature is strongly Stargate-derived rather than arbitrary: Reese canonically created the original Replicators as toys before teaching them self-replication and defensive behavior. The WNG player-mech toy that later transforms into an ordinary hostile Replicator is therefore a deliberate gameplay extrapolation from a direct canon origin, not generic flavor.

## Verification performed

A temporary GitHub Actions workflow compiled `Source/WNG/WNG.csproj` against `Krafs.Rimworld.Ref` 1.6.4871 / .NET Framework 4.8 after the reconciliation corrections. The final audit-triggered run completed with **Build WNG: SUCCESS**.

The temporary workflow was then removed. No permanent reconciliation/release-gate workflow was left in the repository.

## Reconciliation verdict

**Proceed with the current fresh rebuild. Do not restart from scratch.**

The architecture remains clean enough to continue. The deviations found were behavioral and local, not systemic. The next implementation order should be:
1. finish Replicator cell-level consumption and map-consumption accounting;
2. implement terminal-consumption aggression as its own explicit state;
3. recompile/validate;
4. build the four shuttle/craft families around native RimWorld/Odyssey transport/boarding;
5. implement Dart two-pass absorption/landing/hacking and Puddle Jumper drone passes;
6. add optional CatCraft Stargate-entry roof interaction without taking ownership of CatCraft's gate systems;
7. continue Wraith living-tech/Growth/Grav Engine work through the same lore/plan/history reconciliation protocol.
