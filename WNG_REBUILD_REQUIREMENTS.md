# WNG clean rebuild requirements

This file is the active repository contract for the clean RimWorld 1.6 rebuild. Later explicit project-chat corrections override older historical notes.

## Repository and evidence rules

- `Vardath/Wraith-Nanite-Gravtech-1.6` `main` is the active public working branch for this rebuild.
- The original WNG source tree is **not** a reconstruction dependency and is not to be imported.
- The current reconstructed private-repo work is the project state to move into this public repo; older private builds remain feature/reference evidence only and must not be blindly copied as implementation.
- The user's entire WNG project chat history and this requirements file are the design authority. Newer explicit corrections win over earlier code/tests.
- Live RimWorld behavior, Player.log and RimDoctor evidence outrank a static green check.
- Preserve working Replicator graphics, splitting/behavior, faction structure, and other already-established working systems while reconstructing the broken/missing remainder around them.

## Locked recent corrections

- Replicator split-born recombination cooldown is **2,500 ticks — one in-game hour**.
- A Child's Toy that goes feral must **turn into a Replicator**, not merely become an inert hostile toy.

## Platform and compatibility

- RimWorld 1.6.
- Hard dependencies: Biotech and Odyssey.
- Optional, dependency-safe integrations: CatCraft Stargates!, ONAC and RimGate.
- Prefer native RimWorld/Odyssey behavior and local components/Defs over broad Harmony patches.
- CatCraft owns gate addresses, dialing, iris state and receive buffers. WNG must never replace or take ownership of those systems.
- ONAC/RimGate own Goa'uld, Tok'ra and Jaffa gameplay. WNG only interoperates.

## Wraith

- Distinct regenerative Wraith civilization with a visible Life Force resource.
- Drain Life/Wither is one coherent touch-range/downed-target feeding path.
- Full Drain Life ages the victim +50 biological years and de-ages the Wraith 5 biological years, never below age 18.
- Victim receives temporary Life Drained (~1–2 days); Wraith receives Fed Recently (~1 day).
- Low reserve throttles regeneration; zero can cause torpor; high reserve supports expensive regeneration/limb restoration.
- Wraith appearance is consistently pale/white-haired; long straight Wraith-appropriate hair is preferred where practical.
- Gene overwrite/deduplication must restore WNG gene-granted abilities without deleting unrelated abilities.
- Hibernation, Keeper/Queen caste behavior, telepathy, captives, thralls, experiments, hybrids, cloning/host cultivation and rescue loops are real gameplay systems, not labels.
- Living Forge/workshop and Wraith Grav Engine cultivation support living hosts and corpses. Intended incubation for these paths is one in-game day.
- Obsolete Gravcore progression is forbidden. The intended object is the functional Wraith Grav Engine.

## Replicators

- Technological infestation with material/technology assimilation, bounded growth and delayed adaptation.
- Preserve the proven modular Replicator gameplay identity and the specifically approved Replicator graphics.
- Hostile Replicators prefer reachable material; biological/nanite predation is a starvation fallback. Player-owned Replicators do not consume the player's colony autonomously.
- A completed relevant assimilation is what teaches the lineage. Merely seeing technology or being attacked by it teaches nothing.
- Adaptation must require successful assimilation, material and time; it must not be an instant combat reaction.
- Lineage unlock thresholds are: ranged 3 completed relevant assimilations; armour 4; power/construction 4; gravtech 5; shields 8.
- Learned material signatures and adaptation state propagate through reproduction, recombination and death-splitting.
- Exact split chain: Siege Mass -> 2 Titans -> 2 Bulwarks -> 2 Hunters -> 2 Drones. Drone is irreducible.
- Split children inherit relevant material signature, specialization/adaptation and temporary lattice state.
- Intentional recombination/destruction must not invoke death breakup.
- **Split-born recombination lock: 2,500 ticks (~1 in-game hour).**
- Dangerous Replicator Matter minimum stack: 10.
- Dormancy/self-assembly timing: 30,000 ticks where applicable.
- Child's Toy gestation: exactly 90,000 ticks.
- If the Child's Toy goes feral, it transforms into a Replicator and enters the Replicator threat loop.
- EMP suppression, containment, specialists, Controller/Siege Mass, crisis pressure and retaliation remain part of the swarm design.

## Human-form Replicators / Asurans

- Distinct nanite humanoids with engineer, infiltrator, soldier, commander/coordinator and player roles.
- Copy/reconstruction preserves exact source identity where specified: sex/gender, appearance, name, biological age, biography, skills, passions/XP and genome before WNG nanites are layered in.
- Neural Interface and pattern-library continuity are save/load safe.
- Visible Nanite Reserve; eating replenishes it and food poisoning chance is zero for the intended nanite body.
- Copy cost is 60% reserve and failed pre-assembly operations spend nothing.
- Healing/reconstruction timings and costs remain those established in the canonical requirement ledger unless explicitly revised.
- Reconstruction is not consequence-free duplication; backup age/resource cost/continuity consequences remain meaningful.

## Factions, backstories and progression

- Wraith doctrines/identities include Sable, Cinder, Veiled and Pale with the established hostile/exile/player roles.
- Replicator factions include hostile block Replicators, hostile human-form Replicators, non-hostile/enclave content and player variants/toy content.
- Wraith and Replicators can raid independently; Replicator raids can include human-form and specialized units.
- Use native RimWorld 1.6 BackstoryDef. Maintain 30 original WNG backstories and correct role-specific PawnKind pools.
- Progression is mystery -> encounter -> evidence -> understanding -> reconstruction -> mastery, with ruins/labs/vaults and discovery-gated technology rather than dumping all advanced tech at start.

## Stargate behavior

- Optional CatCraft integration only.
- Wraith Dart Stargate culling uses exactly two real flyover/culling passes before final craft state/arrival.
- Flyovers abduct real pawns; exact pawn identity persists into captive/rescue gameplay.
- Friendly/cloaked Puddle Jumper courier behavior remains separate from hostile Dart culling.

## Four required craft

- `WNG_WraithDart`
- `WNG_PuddleJumper`
- `WNG_WraithStrikeCraft`
- `WNG_WraithCruiser`

Each must be genuinely boardable, loadable, correctly fueled, launchable, world/Stargate usable where appropriate and save/load safe. Prefer native Odyssey `Building_PassengerShuttle` behavior. `Get in shuttle` must not be disabled.

## Gravships

- Two complete, mechanically isolated Odyssey-compatible families: Wraith organic and Asuran/Precursor clean geometric.
- Canonical fuels: `WNG_WraithBiofluidFuel` and `WNG_AsuranNaniteSlurry`.
- No cultured-biomass or Gravcore fuel path.
- Both families require real grav engines, pilot control, field extenders, small/large fuel storage, small/large thrust/drive, optimizer, jammer/veil, shield and any other native-equivalent support required for parity.
- Opposite-family facilities must not cross-connect.
- Wraith/Asuran power and fuel conduits require real connected topology: straight, corner, T, cross and end-cap behavior as required, including usable rendering under/through walls.
- Hull/substructure rendering requires straight edges, outside/inside corners and diagonal transitions. Known runtime transition identities include `corner_nw`, `corner_ne`, `corner_sw`, `corner_se`, `diag_nw`, `diag_ne`, `diag_sw`, `diag_se`.

## Art and audio

- Professional, cohesive presentation. No missing textures, generic one-icon-for-everything reuse, accidental duplicates, flat placeholder panels or missing directional/worn variants.
- Wraith visual language: organic, grown, asymmetrical, biomechanical, internal glow.
- Asuran/Precursor visual language: precise, clean, geometric, Ancient-derived/nanite-engineered.
- Replicators remain visibly modular/mechanical with recognizable silhouettes.
- Do not generate new images unless the user explicitly asks. Approved existing Replicator graphics are the stated preservation exception.
- Canonical requirements target 30 distinct functional audio cues. Historical private builds with 32 cues are feature evidence only and do not override the 30-cue requirement.

## Validation and acceptance

Before completion, audit XML/class links, acquisition paths, research reachability, structures/resources, jobs/abilities/gizmos, weapons/apparel/genes/implants, PawnKinds/factions/raids, all 30 backstories, discovery progression, Wraith feeding/living tech, Replicator split/recombine/matter economy, human-form reconstruction, optional integrations, all four craft, both gravship families, graphics, audio and save/load transactions.

A real RimWorld 1.6 compile and packaged DLL are required. A green CI result is not completion: the exact candidate must survive live startup/gameplay, visual inspection and Player.log/RimDoctor review.
