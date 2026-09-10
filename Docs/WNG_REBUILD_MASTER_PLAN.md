# WNG RimWorld 1.6 — Rebuild Master Plan and Continuity Contract

Last consolidated: 2026-09-10  
Repository governed by this document: `Vardath/Wraith-Nanite-Gravtech-1.6`

## How to use this document

This file is the in-repository continuity authority for the current public RimWorld 1.6 rebuild. When the user says **“refresh memory and continue”**, **“continue the rebuild”**, or equivalent:

1. Read `CONTINUE_WNG_REBUILD.md`.
2. Read this file completely.
3. Fetch the current `main` head and recent commits/workflow status.
4. Inspect the subsystem named by the most recent commits and the “Immediate rebuild queue” below.
5. Compare any questionable implementation against the rules in this file and the newest explicit user instructions.
6. Historical/private WNG builds and private continuity files may be consulted only as design/reference evidence. They are not code authorities and are not “known-good states”.
7. Never copy an old implementation wholesale just because it once compiled or passed CI. Rebuild cleanly from the intended behavior.
8. Do not ask the user to repeat requirements already recorded here or in current conversation history.
9. If a later explicit user correction conflicts with this document, the newer user correction wins and this document must be updated in the same rebuild pass.
10. A green static/compile workflow is supporting evidence, not proof that the mod is finished.

## Authority order

For the current rebuild, use this order:

1. Newest explicit user instruction/correction.
2. This public `Docs/WNG_REBUILD_MASTER_PLAN.md`.
3. Current player-facing evidence: screenshots, Player.log, RimDoctor, live game behavior.
4. Current public source and purpose-built audits that agree with the requirements.
5. Historical/private requirement documents, conversation checkpoints and old builds as reference material only.
6. Historical source implementation details last.

**There are no known-good historical builds.** Old/private builds can show intended features, art, data relationships, or prior behavior, but must never be assumed correct as a whole.

The older private consolidated requirements file `Vardath/Wraith-Nanite-Gravtech/Docs/WNG_CANONICAL_REQUIREMENTS.md` is useful historical requirement evidence. The public document you are reading incorporates that plan plus the later 2026-09-09/10 corrections and therefore governs the current public rebuild.

---

# 1. Rebuild mission and release discipline

WNG is a complete professional RimWorld **1.6** mod, not a loose collection of Defs.

The current public repository is now the explicitly authorized active reconstruction workspace. This supersedes older “private repo only” instructions that applied before the user authorized the public 1.6 rebuild.

The September 2026 rebuild instruction is:

- rebuild the mod cleanly rather than trying to rescue a corrupted whole;
- preserve the approved Replicator graphics and the genuinely working Replicator split/recombine behavior;
- preserve the intended faction designs and how those factions are supposed to behave;
- re-create everything else from the requirements and evidence;
- historical builds/private repo are reference material only;
- do not call any old state “known good”;
- do not import original/old code wholesale as a shortcut;
- commits should represent coherent ready checkpoints rather than strings of intentionally broken intermediate states where practical.

Every subsystem must be checked for:
- correct Def/class links;
- actual player-facing behavior;
- acquisition/progression;
- save/load safety;
- optional-mod safety;
- graphics/audio;
- compile correctness;
- live behavior where a static test cannot prove the requirement.

# 2. Whole-mod coherence contract

Every WNG Def must make sense and be reachable.

No:
- orphaned Defs;
- missing PawnKinds;
- impossible faction references;
- invalid parent Defs;
- missing C# classes/comps;
- dead research nodes;
- unreachable recipes;
- unused products;
- impossible ingredients;
- duplicate Def names;
- descriptions promising nonexistent behavior;
- decorative “functional” buildings that do nothing;
- dev-mode-only content with no normal acquisition route when normal acquisition is intended.

Craftable/manufacturable items require real bills/recipes, work, ingredients, research and stations unless their intended acquisition is biological growth, construction, transformation, salvage, quest reward, analysis/reconstruction, or another explicit special route.

Installable/minified special structures must retain their special state and function after minify/reinstall/save/load.

# 3. Identity model: do not confuse race, xenotype, caste, role, faction and backstory

This distinction is mandatory.

## Wraith

Wraith are **one Wraith xenotype/civilization identity**.

Hierarchy:

**Wraith xenotype -> Wraith caste/PawnKind -> faction role/behavior -> optional biography/backstory**

Wraith castes include the established:
- Hunter;
- Warrior;
- Commander;
- Keeper;
- Queen;
- player Wraith variant where appropriate.

These are **castes implemented through PawnKinds and caste-specific generation/gear/behavior**, not separate races.

A Queen is a Wraith caste and faction leadership role. A Keeper is a Wraith caste specializing in dormant populations, feeding reserves and Hive biological maintenance.

## Backstories

Backstories are biography/history data. **Backstories are not races and are not castes.** Never use a backstory category as a substitute for correct xenotype, PawnKind/caste, faction identity, or synthetic identity.

## Human-form Replicators / Asurans

Human-form Replicators and nanite precursors are Human-pawn xenotype identities with WNG nanite systems layered onto them. They are not block Replicator custom races and are not just ordinary reskinned colonists.

## Block Replicators

Block Replicators are custom machine/mechanoid-like races/forms with their own PawnKinds, matter economy, recombination and split hierarchy.

# 4. Wraith Life Force biology

Life Force is a real visible Wraith gene resource and is the source of truth for the Wraith feeding/regeneration economy.

Core behavior:
- feeding is touch range;
- the victim must be a valid living biological pawn and downed/incapacitated where the ability requires it;
- Wraith do not treat Asuran/human-form nanite synthetics as feeding stock;
- Drain Life and Wither are one coherent full-feed ability, not duplicate competing mechanics;
- full Drain Life adds **50 biological years** to the victim;
- the feeding Wraith becomes **5 biological years younger**;
- Wraith biological age may not fall below **18**;
- victim receives `Life Drained` for roughly **1–2 days**;
- Wraith receives `Fed Recently` for roughly **one day**;
- a repeated full feed before the existing Life Drained state clears may be lethal according to the accepted feeding design;
- partial feeding remains distinct from full-feed lethal-repeat behavior where implemented;
- low Life Force throttles regeneration;
- zero/near-zero Life Force can produce torpor;
- high reserve supports expensive regeneration, including missing-part/limb recovery where intended;
- deliberate hibernation reduces Life Force consumption to roughly **2% of normal**;
- feeding resistance/treatment must be legible and real; the intended strong resistance window is about **five days** without becoming absolute immunity;
- gene overwrite/deduplication must restore missing WNG gene-granted abilities without deleting unrelated abilities.

Wraith appearance:
- hair must not retain inappropriate ordinary human color;
- enforce pale/white/colorless Wraith hair consistently;
- long straight Wraith-appropriate hair is preferred where practical;
- caste/faction generation should reinforce Wraith identity rather than random generic-human presentation.

# 5. Strategic Wraith faction hunger — separate from ordinary feeding

This is a major correction and must remain explicit.

There are **two different layers**:

1. ordinary pawn-level Wraith feeding abilities;
2. strategic faction-level hunger.

Ordinary `Drain Life` / feeding:
- does **not** open the faction feeding-request popup;
- does not itself invoke the strategic request UI;
- is local pawn biology.

Strategic Wraith hunger:
- belongs to a Wraith faction/lineage;
- tracks whether the faction is becoming genuinely hungry for feeding stock/access;
- affects the likelihood/timing of **feeding requests** and **Wraith attack/raid pressure**;
- does not need to be represented as a generic quest system;
- when hunger reaches a real request condition, the faction may ask the player to provide feeding stock/access;
- if the player refuses/does not accept an actual hunger request, Wraith attack/raid likelihood escalates according to the faction design;
- the popup exists only for a genuine strategic hunger request, never for routine pawn feeding.

Do not leak strategic hunger identifiers/UI into unrelated Wraith systems such as mature-Hive site generation or retaliation.

## Strategic feeding-request UI contract

The accepted request-flow detail must be preserved:

- first modal shows the feeding-stock/prisoner subject name(s) relevant to the request;
- the player does **not** choose individual Wraiths in the first modal;
- Submit/continue opens the next stage showing the count/names of Wraiths involved;
- the game/request flow remains paused through the multi-stage decision and resumes after the final submission/decision;
- this UI is only for a genuine faction hunger request.

# 6. Wraith factions and politics

Preserve coherent lineage doctrines rather than generic recolors.

Established current Wraith faction identities:
- **Sable Brood** — uncompromising hostile predatory Hive;
- **Cinder Court** — militant Queen-led Hive, aggressive but politically mutable rather than automatically permanent-enemy;
- **Veiled Hive** — wary/selective/concealment-oriented Hive capable of negotiation while still predatory;
- **Pale Covenant** — exile/offshoot group capable of coexistence/trade if supplied by less destructive means.

Wraith:
- raid independently without requiring Stargates/ONAC/RimGate;
- use caste-appropriate raid/settlement composition;
- maintain lineage-specific diplomacy and threat behavior;
- retain exact lineage/faction identity through captivity/rescue/retaliation systems where required.

# 7. Wraith prisoners, feeding stock, thralls, experiments and hybrids

Wraith gameplay includes:
- real captive identity;
- prisoner feeding;
- feeding stock;
- thrall handling where designed;
- experimentation/hybrid content where defined;
- rescue/recovery of exact abducted pawns.

Captive systems must:
- operate on the correct biological targets;
- preserve exact pawn identity through abduction/storage/rescue;
- be save safe;
- expose meaningful outcomes;
- not replace real pawns with fake proxy victims.

Telepathic/experimental abilities must have distinct purposes, icons and descriptions.

# 8. Mature Wraith Hive ecology

Mature Hives are a local ecology/settlement system and must not be conflated with strategic faction hunger.

The current rebuild direction includes:
- a bounded active founder/defender population;
- caste-correct Wraith founders including Queen and Keeper where the site calls for them;
- dedicated Hibernation Pods for ordinary dormant Wraith;
- separate Dormancy Vault reserve behavior where designed;
- finite biological feeding stock;
- Feeding Niches holding exact feeding-stock pawns;
- Keeper/Queen-supervised feeding/restoration behavior;
- real stored biological resources/infrastructure;
- bounded population replacement/growth rather than infinite spawning;
- exact site-faction ownership;
- cleanup of partial generation on failed site setup;
- active defenders only in the initial defensive Lord, leaving dormant occupants dormant.

The current mature-Hive site population initializer has been expanded to include finite feeding stock/niches. Audits must follow the intended behavior rather than preserve an obsolete method signature.

## Mature Hive discovery and retaliation

Discovery/site behavior and retaliation are separate from strategic feeding hunger.

A hostile lineage whose mature Hive is genuinely neutralized may schedule retaliation:
- save-persistent;
- tied to the exact source site and lineage where possible;
- duplicate scheduling suppressed;
- approximately **2–4 days** delay in the current rebuild;
- failed execution retries rather than silently disappearing;
- uses a real RimWorld raid with immediate attack;
- may stage WNG strike craft/cruiser support if those craft are available, without making raid execution depend on unfinished craft content.

Mere discovery of a non-hostile lineage must not schedule retaliation.

Mature-Hive retaliation must not contain:
- strategic `WraithFactionHunger` request logic;
- feeding-request dialogs;
- captivity request UI.

# 9. Wraith living technology and production

Wraith technology is organic, grown and biomechanical.

Progression/bootstrap:
- begins from Wraith biological interaction/implantation where appropriate, not a generic industrial bench chain;
- Living Forge/workshop growth supports the intended **living-host route**;
- it must also support the later requested **corpse route** so progression/testing does not require a living victim only;
- Wraith Grav Engine seed/implant progression also supports living-host and corpse routes where applicable;
- accepted incubation for the Living Forge/workshop and Wraith Grav Engine host/corpse route is **one in-game day**;
- do not apply that one-day timing to Child’s Toy Replicator gestation.

Remove obsolete **Gravcore** naming and behavior. The intended technology is a functional **Wraith Grav Engine**.

Living equipment:
- matures/operates as described;
- Bone Blade and other living weapons retain distinct behavior/graphics;
- Wraith buildings/furniture should form a coherent organic settlement family including appropriate equivalents of beds, chairs, tables and support structures where the design calls for them.

# 10. Block Replicator core ecology

Block Replicators are a technological infestation.

They:
- consume matter/technology;
- maintain a real matter economy;
- use bounded growth;
- prioritize reachable environmental material/technology;
- only fall back to biological/nanite humanoid predation under starvation after reachable material is exhausted;
- player-owned Replicators must not autonomously consume the player’s colony.

Assimilation/adaptation:
- weapons/turrets can drive weapon adaptation;
- shield technology can drive shield adaptation;
- armor/materials can drive protection/material adaptation;
- power technology can alter power behavior;
- gravtech can produce grav-related capability;
- Asuran/advanced precursor tech can produce appropriately advanced learned capabilities;
- adaptations require actual successful assimilation plus time/material/evidence rather than arbitrary instant upgrades;
- learned state should influence gameplay rather than act as cosmetic flags.

Containment, EMP suppression, encounter memory, specialists, Controller, Siege Mass, crisis pressure and retaliation remain intended systems.

Dangerous Replicator Matter:
- minimum dangerous floor **10**;
- accepted dormancy/self-assembly delay **30,000 ticks** unless explicitly revised;
- recovered matter must not become inert decorative salvage if above the danger threshold.

# 11. Replicator split/recombine hierarchy — preserve this

This is one of the explicitly preserved working systems.

Canonical death breakdown:

**Siege Mass -> 2 Titans -> 2 Bulwarks -> 2 Hunters -> 2 Drones/base Replicators**

Rules:
- Drone/base is irreducible;
- larger units must split into smaller existing forms when genuinely destroyed instead of disappearing;
- split transaction must be at-most-once and save-safe;
- intentional `Vanish` used by successful upward recombination must not trigger death-split;
- split children inherit relevant material state, adaptation and lattice/control state where appropriate;
- larger-unit death must not erase the Replicator Matter economy;
- surviving smaller units can recombine upward when sufficient units/material/conditions are met;
- accepted split-born recombination cooldown is **2,500 ticks / one in-game hour**, not one day.

Specialist/support forms outside the main four-rung combat ladder must not accidentally corrupt the canonical death chain.

# 12. Replicator faction/raid composition

Do not confuse swarm growth rules with raid composition.

The autonomous block swarm remains a block-machine infestation, but **raids/encounters may intentionally mix block and human-form Replicators** where the design calls for a mature/advanced threat.

The current requirement explicitly retains:
- hostile block Replicator faction content;
- hostile human-form/Lattice content;
- mixed block + human-form raid groups where appropriate;
- specialized groups;
- non-hostile human-form enclave content;
- player human-form variants;
- player block Replicators;
- Child’s Toy player content.

Do not remove human-form units from raid composition merely because block recombination itself is block-only.

# 13. Human-form Replicators / Asurans

Human-form Replicators are synthetic nanite humanoids.

Required roles include:
- engineer;
- infiltrator;
- soldier;
- coordinator/commander;
- player-aligned variants.

## Nanite physiology

Ordinary Food need remains present.

Nanite bodies:
- have effectively zero food-poisoning outcome;
- convert eating/feedstock intake into a visible **Nanite Reserve**;
- do not passively drain the reserve just because time passes.

Accepted reconstruction economy:
- Neural Interface copy cost: **60% Nanite Reserve**;
- no reserve cost if the copy/assembly operation fails before successful placement;
- reserve-powered injury healing: every **450 ticks**, **1.5% reserve** only when an actual healing pulse occurs;
- depleted emergency injury repair: **1,800 ticks**;
- reserve-powered missing-part reconstruction: **30,000 ticks** base, **25% reserve** only when a part is actually restored;
- depleted emergency missing-part reconstruction: **90,000 ticks**;
- EMP disruption pauses nanite reconstruction;
- old-save reserve migration is conservative, one-shot and only for established WNG nanite identities.

## Neural Interface / copying

Human-form systems support the intended:
- recruit;
- imprison;
- enslave where game rules permit/design calls for it;
- copy personality/skills/passions/pattern;
- create/reconstruct human-form synthetic interaction.

An exact copied person must preserve before nanite layering:
- biography/backstories;
- title/surname/name identity;
- skills;
- passions;
- XP where required;
- appearance;
- genome/xenotype source data as intended.

Generated synthetic backstories must never overwrite an exact copied person’s actual history.

Reconstruction/backup/collective continuity may have real cost/loss/risk; it must not become free duplication.

## Infiltration

Human-form infiltrators may appear human until scanning, injury or suspicious behavior reveals the synthetic nature. This must become an actual mechanic, not just flavor text.

# 14. Replicator Queen and sovereign control

The Replicator Queen is a unique human-form Replicator individual, not a separate “race” for every human form.

Current design:
- Queen PawnKind: `WNG_ReplicatorQueenChild`;
- female;
- biological/generation age **13**;
- ordinary body uses the human-form Replicator xenotype;
- unique sovereign authority is layered onto the exact Queen pawn, not placed in the ordinary human-form xenotype;
- the exact Queen pawn identity must persist through the quest/outcome/save system;
- there must never be accidental multiplication into multiple simultaneous “Queens” from ordinary generation.

## Queen authority vs Sovereign Neural Lattice implant

These are intentionally related but not identical.

Real Queen:
- possesses innate sovereign coordination;
- while alive/present/aligned, can provide broad local same-faction coordination over base/block Replicators;
- can issue direct sovereign commands to block Replicators.

Sovereign Neural Lattice implant bearer:
- is **not** turned into a Queen;
- gets the explicit sovereign directive ability;
- controls block Replicators on a **target-specific bound** basis;
- the binding is persistent/save-safe;
- the binding remains valid only while the exact controller remains alive/present and still has the implant authority;
- implant authority must not silently grant passive whole-swarm Queen coordination.

The sovereign directive targets **base/block WNG Replicators**, not other human-form Replicators.

Temporary Asuran lattice intrusion:
- remains a separate temporary overwrite mechanism;
- must restore original faction/state when its save-safe timer ends;
- must not be treated as genuine Queen/implant sovereign ownership.

Split/recombine transactions must preserve the appropriate bound/temporary control state where the controlling design requires it.

# 15. Replicator Queen vault and consequence

Discovery progression currently places the Replicator Queen vault around **day 84**.

The Queen-vault system must:
- generate/register the one exact Queen pawn;
- preserve her through all outcome paths;
- allow player shelter/recovery path;
- allow hostile Lattice recovery/capture path;
- serialize outcome state safely.

If the Lattice Collective captures the Queen, the consequence must be **genuine sovereign Replicator access**, not merely the old simplistic `+1 outbreak` bonus.

That consequence should feed future Lattice/Replicator threat systems so the hostile collective can deploy/control appropriate base Replicators under actual sovereign authority until the narrative/system changes that state.

Player possession/alignment with the Queen should conversely make her actual sovereign block-Replicator control usable, subject to balance and presence rules.

# 16. Child’s Toy / player Replicators

Player can have a distinct friendly Child’s Toy style Replicator.

Locked timing:
- gestation **90,000 ticks**.

Do not replace this with the one-day living-tech incubation used by Wraith host/corpse growth.

Toy/player behavior:
- friendly behavior;
- charging/wandering;
- response to hostile threats;
- reproduction/consumption rules;
- population cap;
- configurable feral timing where exposed;
- if the accepted toy system has a feral transformation, it transforms into/enters the Replicator swarm threat loop rather than simply becoming a generic mech.

# 17. Native WNG backstory system

Backstories must use RimWorld 1.6 native `BackstoryDef`.

Canonical set:
- **30 original WNG backstories**;
- six origin/childhood;
- twenty-four adult histories.

Wraith origins:
- Hive creche broodling;
- Living-ship broodling;
- Feeding-court ward.

Wraith adult pools:
- Hunter;
- Warrior;
- Commander;
- Keeper;
- Queen;
- Player Wraith.

Synthetic/Asuran origins:
- Archive-born template;
- Constructed service pattern;
- Recovered biological imprint.

Synthetic/Asuran adult pools:
- Engineer;
- Soldier;
- Commander;
- general Human-form;
- Player synthetic;
- other defined role pools within the accepted 24 adults.

Every concrete WNG humanlike PawnKind maps to the right origin and role-specific adult categories.

Block Replicators receive **no human childhood/adulthood biography filtering**.

Backstories should provide modest coherent bonuses only:
- no forced traits solely as hidden balance locks;
- no broad arbitrary work disables;
- no forced body types purely from biography;
- no possession packages just because of biography.

Live generation must confirm pools do not cross.

# 18. Faction and pawn-generation completeness

All factions must be real reachable gameplay systems, not decorative XML.

Required broad set:
- Sable/Cinder/Veiled/Pale Wraith lineages;
- player Wraith support;
- autonomous hostile block Replicator swarm;
- hostile human-form/Lattice collective;
- non-hostile human-form enclave/Quiet Lattice;
- player human-form Replicators;
- player Replicators/toy content.

All `basicMemberKind`, group options, xenotype references and caste/PawnKind links must resolve.

Audits must search all `Defs/**/*.xml`, not impose a false assumption that every `PawnKindDef` must live physically in `Defs/PawnKindDefs`. Existing block Replicator PawnKinds are legitimately colocated with their custom race Defs.

# 19. Discovery, ruins, faction quests and progression

Theme:

**mystery -> encounter -> evidence -> understanding -> reconstruction -> mastery**

Accepted discovery cadence:
- day 20 — ruined Wraith laboratory;
- day 28 — Replicator-consumed ruin;
- day 36 — Ancient precursor laboratory;
- day 44 — abandoned Wraith cloning installation;
- day 52 — Ancient precursor vault;
- day 60 — dormant Asuran facility;
- day 72 — deceptive Ancient survey annex / hidden Asuran reveal;
- day 84 — Replicator Queen vault.

Advanced technology should arrive first as:
- encounters;
- damaged evidence;
- salvage;
- analysis subjects;
- incomplete relics;
- reconstruction paths.

Do not dump stable advanced technology into starting research.

Asuran content includes:
- splinters/enclaves;
- collective/continuity themes;
- precision strikes;
- pattern theft;
- archive/fabrication systems;
- hostile objectives.

All site/quest Def references must resolve.

# 20. CatCraft Stargates! integration

CatCraft **Stargates!** is optional.

Ownership boundary:
- CatCraft owns gate network/address/dial/iris/shield/receive-buffer mechanics;
- WNG owns WNG incidents, corridors, craft, objectives and outcomes;
- WNG does not implement a replacement Stargate network;
- no hard dependency;
- no Harmony takeover where API/components suffice;
- no stealing CatCraft receive-buffer ownership.

Gate eligibility respects:
- usable gate;
- intended open/active corridor state;
- iris/shield safety where relevant;
- inactive/nonhibernating/open-iris conditions for friendly arrival designs where those requirements apply.

## Wraith Dart corridor

Canonical hostile Wraith Dart Stargate sequence:
- exactly **two real flyover/culling passes**;
- each pass performs real ray-of-absorption/culling/abduction gameplay;
- real pawns are taken;
- exact abductee identities persist into Wraith captivity/rescue;
- after the two passes, transition to the intended final craft arrival/state;
- not one pass;
- not endless passes;
- not decorative-only animation.

Friendly/cloaked Puddle Jumper/Ancient-style courier behavior stays separate from hostile Dart culling.

Historical Quiet Lattice friendly delegation behavior remains useful design reference:
- CatCraft can perform pawn gate arrival;
- visitors behave as normal neutral visitors;
- a real WNG Puddle Jumper can accompany them as a finite courier then depart;
- it must not inherit hostile Wraith cloak/sabotage/ZPM-recovery/pattern-theft/raid/capture-buffer logic.

Wraith retreat/rescue, reinforcement waves, gate-control objectives, Asuran precision strikes and Replicator outbreak/cargo interactions remain intended optional integration areas.

# 21. ONAC / RimGate compatibility

ONAC/RimGate own their Goa’uld/Tok’ra/Jaffa systems.

WNG:
- does not duplicate them;
- only recognizes/integrates diplomacy/threat/interaction where intended;
- treats Replicators as extreme technological threats where appropriate;
- treats Wraith as rival-power actors where appropriate;
- remains safe when ONAC/RimGate are absent.

# 22. Shuttle/craft contract

Primary WNG craft:
- `WNG_WraithDart`;
- `WNG_PuddleJumper`;
- `WNG_WraithStrikeCraft`;
- `WNG_WraithCruiser`.

Every craft must be genuinely usable through:

**boardable -> loadable -> correctly fueled -> launchable -> world/Stargate usable -> save/load safe**

Prefer native Odyssey `Building_PassengerShuttle` / normal boarding/loading behavior.

Never preserve a broken state in which “Get in shuttle” is disabled merely because a static audit calls it native.

Do not reintroduce custom right-click `EnterTransporter`/boarding hacks if Odyssey native boarding can be made to work correctly.

Craft maintain:
- dedicated role;
- dedicated graphics;
- dedicated event behavior;
- appropriate audio;
- correct roof/landing/launch behavior;
- correct Stargate arrival sequence where applicable.

# 23. Gravship overall contract

There are two complete distinct Odyssey-compatible gravship families:
- Wraith;
- Asuran/Precursor.

Use vanilla Odyssey gravship behavior wherever possible without sacrificing WNG family identity.

Families remain isolated:
- Wraith systems do not connect to/consume Asuran resources;
- Asuran systems do not connect to/consume Wraith resources.

Each family requires a complete functional set:
- hull/substructure;
- walls;
- corners/diagonals/transitions;
- functional Grav Engine;
- pilot console/node;
- fuel tank/storage;
- fuel feed/pipes;
- thrusters/engines;
- field extenders;
- shields;
- optimizers/jammers/support modules where defined;
- real power conduits;
- correct network/topology behavior.

Every structure must provide the behavior/stats it advertises.

# 24. Wraith gravship family

Visual/fiction:
- organic;
- grown;
- asymmetrical;
- biomechanical;
- internal glow/light;
- unmistakably Wraith-family.

Connectivity:
- Wraith-only where family isolation calls for it.

Canonical fuel:
- `WNG_WraithBiofluidFuel`;
- label/identity: Wraith bio-fluid propellant;
- finite;
- haulable/storeable;
- produced by the intended Wraith biological chain.

No obsolete cultured-biomass requirement should remain in Wraith shuttle/gravship fuel paths once bio-fluid is canonical.

Wraith:
- real fuel tanks/reservoirs;
- real fuel-feed behavior;
- neural conduits must be functional power conduits, not decorative lines;
- pipes/conduits need connected graphics/atlas behavior and turn correctly;
- complete hull transitions.

The obsolete **Gravcore** must not return. Use functional **Wraith Grav Engine**.

# 25. Asuran/Precursor gravship family

Visual/fiction:
- precise;
- clean;
- geometric;
- Ancient-derived;
- nanite-engineered;
- clearly different from Wraith organic technology.

Canonical fuel:
- `WNG_AsuranNaniteSlurry`;
- finite;
- haulable/storeable;
- produced through the appropriate Asuran/Replicator chain.

Asuran family:
- isolated connectivity;
- real tanks/reservoirs;
- real fuel feed;
- functional power conduits;
- complete connected atlases/directional graphics;
- hull transition support;
- functional **Precursor Grav Engine** synthesized through the correct human-form/Asuran pathway.

# 26. Pilot consoles, topology and conduit graphics

Pilot console/node:
- prominent;
- professional;
- functional;
- exposes actual Odyssey gravship control;
- not a tiny generic placeholder;
- Wraith and Asuran versions distinct.

All support structures—thrusters, tanks, extenders, shields, jammers, optimizers, conduits—need:
- actual gameplay function;
- unique/appropriate family art;
- correct build-menu icons.

Hull topology:
- intentional walls/corners/diagonals;
- no ugly “technically connected” diagonal breaks;
- do not draw transition pieces through invalid cells/edifices;
- complete facings where renderer expects them.

Fuel pipes and power conduits must:
- turn corners;
- snap/connect;
- use connected graphics;
- hide/transition around walls appropriately;
- not lose previously functioning patch coverage when gravship functionality is expanded.

# 27. Art direction

The mod should look like a professional cohesive RimWorld mod, not placeholder or amateur early assets.

Preserve approved Replicator graphics unless a specific defect requires correction.

All visible content needing unique art should have it:
- pawns;
- items;
- buildings;
- apparel;
- worn apparel facings;
- weapons;
- wielded graphics;
- genes;
- abilities;
- resources;
- craft;
- gravship parts;
- furniture;
- faction/environment structures.

No:
- one generic icon for unrelated concepts;
- missing textures;
- tiny placeholder images;
- accidental duplicate aliases;
- flat/blank generated panels;
- bad chroma-key remnants;
- missing directional variants.

Wraith art language: organic/grown/asymmetrical/biomechanical.

Asuran art language: geometric/precise/Ancient-derived/nanite-engineered.

Replicators: articulated mechanical block identity through Drone/larger forms plus appropriate human-form presentation.

Do **not** generate/replace mod art merely because an audit is underway unless the user asked for art work or the current rebuild task specifically requires an asset fix.

# 28. Audio

Professional audio is part of completion.

Accepted historical target boundary: **30 distinct WNG cues**.

Audio should cover appropriate:
- Replicator metallic/electrical clickety-clack movement;
- assimilation;
- assembly/recombination;
- splitting/reproduction;
- power;
- Wraith weapons;
- living-tech growth/operation;
- Wraith drives/interfaces;
- culling/craft;
- Hive ambience;
- Asuran/precursor/nanite systems.

Every SoundDef/path must resolve and package.

Waveform/format checks are insufficient. Final acceptance includes in-game judgement of:
- mix;
- loudness;
- repetition;
- thematic cohesion.

# 29. Research and acquisition progression

Research unlocks must lead to actual usable content.

Avoid:
- missing/obsolete Def rewards;
- unreachable prerequisite loops;
- generic industrial recipes that contradict biological/reconstruction fiction;
- obsolete gravcore names.

Use:
- discovery;
- analysis;
- salvage;
- biological growth;
- reconstruction;
- faction encounters;
- ruins/vaults;
- precursor evidence.

Weapons/apparel/genes/implants/medical operations must have coherent acquisition and installation paths.

Implant recipes require:
- correct body targets;
- ingredients;
- research;
- worker behavior;
- removal path where appropriate.

# 30. Save/load and transactional safety

Persist major state:
- Replicator split/recombine;
- split latches;
- adaptation/material/control inheritance;
- temporary lattice override;
- target-specific sovereign binding;
- Queen exact pawn and capture/alignment outcome;
- Wraith abductee identity;
- captive/rescue state;
- Neural Interface copied identity;
- Nanite Reserve migration;
- reconstruction timers;
- strategic hunger state;
- feeding-request state;
- Stargate corridor state;
- craft/shuttle state;
- gravship state;
- living-tech incubation;
- discovery/quest objectives;
- mature Hive population/retaliation queue.

At-most-once operations remain at-most-once after reload:
- no duplicate split children;
- no duplicate rewards;
- no duplicate craft;
- no duplicate victims;
- no repeated transformation;
- no double-spent reserve;
- no duplicate Queen.

Temporary timers/hediffs resume correctly.

# 31. Compatibility/performance

WNG must survive the user’s large mod stack.

Prefer local, component/native solutions over brittle global Harmony.

Do not “fix” unrelated external-mod errors inside WNG unless evidence shows WNG owns/triggers them.

Critical death/split transactions must tolerate crowded modded death-hook environments without speculatively blaming other mods.

Optional integrations fail safe when absent.

# 32. Acceptance and testing discipline

A checkpoint is not complete just because source looks right.

Required evidence layers:
1. source correctness;
2. XML/Def semantic coherence;
3. purpose-built audits;
4. real RimWorld 1.6 C# compile;
5. assembly exists;
6. correct playable package;
7. normal player-facing acquisition paths;
8. live startup;
9. live interactions;
10. save/reload;
11. visual inspection at gameplay zoom;
12. Player.log/RimDoctor review for the exact candidate build.

If an audit prevents correct intended behavior because it encoded an obsolete implementation detail, **fix the audit**. Never force gameplay backward to make a stale string check green.

Never claim CI passed unless the exact workflow/run actually completed successfully.

Never call the whole mod complete until live candidate-build testing agrees.

Green CI is supporting evidence, not proof that the mod is finished. A green static/compile workflow is supporting evidence only.

# 33. Required final whole-mod audit matrix

Before release/acceptance, explicitly audit:

1. every XML Def and C# class/comp link;
2. Def-name uniqueness;
3. parent inheritance;
4. all PawnKind references regardless of XML folder;
5. xenotype/gene/ability/hediff references;
6. recipes/bills;
7. biological/transformation/salvage/non-recipe acquisition;
8. research reachability;
9. jobs/workgivers/gizmos;
10. weapons and apparel;
11. implants and surgery;
12. factions/raids/incidents;
13. Wraith caste mapping;
14. all 30 backstories and their actual PawnKind category mappings;
15. Wraith Life Force/full feed/partial feed/regeneration/hibernation;
16. strategic Wraith hunger request/raid behavior and UI;
17. Wraith captives/thralls/abductee identity/rescue;
18. mature Hive ecology/discovery/retaliation;
19. living-tech host/corpse incubation;
20. Replicator matter economy;
21. adaptation;
22. exact split ladder;
23. recombination;
24. control/lattice state inheritance;
25. human-form infiltration;
26. Neural Interface copy;
27. exact copied biography/genome/skills/passions;
28. Nanite Reserve/reconstruction/EMP;
29. Queen quest/identity/sovereign outcomes;
30. Child’s Toy;
31. discovery chronology and quest/site reachability;
32. optional Stargates integration;
33. exact two-pass Dart culling;
34. ONAC/RimGate safety;
35. four-craft board/load/fuel/launch/world/Stargate/save path;
36. Wraith gravship family;
37. Asuran gravship family;
38. family isolation;
39. Wraith bio-fluid resource/network;
40. Asuran nanite slurry resource/network;
41. pilot consoles and topology;
42. conduits/pipes and connected graphics;
43. all graphic paths/facings/worn assets/icons;
44. all 30 audio cues and references;
45. save/load transactions;
46. compile;
47. assembly/package;
48. live startup/gameplay;
49. screenshots/visual review;
50. Player.log/RimDoctor.

# 34. Immediate rebuild queue as of the 2026-09-10 public reconstruction

Always verify current `main` before acting, because commits may have advanced.

The active sequence after the mature-Hive rebuild is:

## A. Stabilize human-form/Asuran identity checkpoint
- keep the newly restored nanite precursor/human-form xenotypes and PawnKinds if they compile and align with this plan;
- fix audits that incorrectly assume PawnKinds only live in `Defs/PawnKindDefs`;
- verify every faction `basicMemberKind` and group option resolves across **all Def XML**;
- verify nanite genes/classes/hediffs/ability references compile;
- verify Food/Nanite Reserve timing/cost semantics;
- verify no ordinary human-form xenotype contains the Queen marker;
- verify Queen direct command only targets block Replicators;
- verify Sovereign Neural Lattice implant binding remains target-specific.

## B. Control inheritance and Queen consequences
- make target-specific sovereign control survive/propagate through valid Replicator split/recombine transactions where it should;
- keep temporary Asuran lattice override semantically separate;
- implement hostile Lattice “Queen captured” consequence as genuine sovereign access rather than `+1` outbreak;
- integrate captured-Queen authority into appropriate future Lattice/Replicator raids/threats;
- ensure the player-aligned Queen path grants usable real sovereign control.

## C. Day-84 Queen vault
- build the reachable site/quest;
- generate exactly one age-13 female Queen pawn;
- persist exact pawn identity;
- implement player shelter vs hostile recovery paths;
- save/load test both outcomes.

## D. Human-form functionality
- functional infiltration/reveal;
- Neural Interface operations;
- exact copy preservation;
- reserve spending transaction safety;
- collective reconstruction/backup consequences;
- role-appropriate factions/gear/backstories.

## E. Backstories
- restore/create the 30 native WNG BackstoryDefs;
- map Wraith castes correctly;
- map synthetic roles correctly;
- remove generic Outlander/Offworld generation where WNG role pools should apply;
- remember: backstory is biography, not race/caste.

## F. Continue remaining master-plan systems
Proceed through discovery/quests, living technology, craft, gravships, art/audio and final audit without regressing already rebuilt Wraith/Replicator mechanics.

# 35. Current known protected accomplishments

Do not casually rewrite these merely because later systems are unfinished:

- canonical Replicator death split ladder and one-hour split-born recombination delay;
- approved Replicator graphics;
- Replicator material/adaptation/containment/crisis framework where current source remains requirement-aligned;
- Wraith Life Force and Drain Life age-transfer behavior;
- Wraith hibernation;
- Wraith caste PawnKinds;
- Wraith lineage faction definitions;
- strategic hunger kept separate from routine pawn feeding;
- mature Hive site/discovery/retaliation architecture;
- finite mature-Hive feeding stock/niches;
- exact Dart two-pass contract where already rebuilt;
- current public rebuild’s shift from missing human-form references toward real nanite xenotypes/PawnKinds.

“Protected” means preserve behavior unless evidence shows a defect. It does **not** mean assume current implementation is perfect.

# 36. Future continuation protocol

When a future assistant is told to refresh memory and continue:

- do not respond with a generic summary and stop;
- inspect `main`;
- inspect workflow status for the exact head;
- read this plan and `CONTINUE_WNG_REBUILD.md`;
- inspect recent commit diffs;
- determine whether recent changes align with the plan;
- keep aligned work;
- discard/revert/repair misaligned work;
- continue the next unfinished dependency;
- add/update regression audits for the actual behavior contract;
- keep user informed with concise concrete findings;
- do not ask already-answered questions;
- do not make completion claims from CI alone;
- update this plan when the user changes the design.

This file must remain referenced from the repository root README and from `CONTINUE_WNG_REBUILD.md`.
