# Wraith & Nanite Gravtech

Unofficial, non-commercial RimWorld 1.6 fan mod inspired by Stargate Atlantis themes. Built around Biotech genetics and Odyssey-native shuttle/gravship systems, with original project-created runtime art and procedural audio.

Requires **Biotech** and **Odyssey**. **Ideology is optional** and is only required for the Neural Interface Enslave operation.

This project is not affiliated with, endorsed by, or sponsored by the Stargate rights holders, Ludeon Studios, or any associated rights holder.

## Development status

WNG is in **active development and integration**, not final-release sign-off. The current branch contains substantial playable Wraith, block-Replicator, human-form Replicator/Asuran, Ancient/precursor, Odyssey and optional Stargate systems. Static/runtime-contract cleanup is increasingly mature, but broad real-game validation, demonstrated-fault repair, balance and live audio/VFX judgement still remain.

Automated green status means the exact source head compiles, passes its regression contracts and packages successfully. It does not replace in-game verification or mean the content design is complete.

## Core pillars

### Wraith xenotype
A regenerative, life-draining Biotech xenotype with a Life Force resource, Drain Life, Enthrall, Host Seed Gestation and limb regeneration. The earlier separate Wither concept is folded into Drain Life rather than exposed as a second feeding ability.

Drain Life and Enthrall are combat abilities and can target standing biological pawns. Host Seed Gestation accepts a downed/stunned biological enemy, prisoner, slave, or biological corpse.

The first Drain Life use on a victim adds 50 biological years and applies the temporary `Life Drained` marker. A second Drain Life while that marker is still present kills the victim through complete life-force depletion. Every successful feeding reverses 5 biological years from the Wraith, never reducing the Wraith below biological age 18, restores Life Force and starts the fed-regeneration effect.

Wraith physiology is driven by Life Force rather than ordinary food. The reserve is tuned around roughly one quadrum from full to empty. Wraiths retain sleep/rest but do not use ordinary food, beauty or comfort needs. Low Life Force causes progressive weakness and zero reserve is nonlethal.

A Wraith can implant workshop tissue into an incapacitated biological host or biological corpse. Either route matures in one day and produces a minified Living Forge; the corpse route consumes the corpse immediately into a visible incubating mass. The Living Forge cultures ordinary biomass and grows the first Growth Chamber Seed. The Growth Chamber owns advanced Wraith production: replacement Living Forge Seeds, Wraith gravcore seeds, Wraith weapons and Wraith armour/clothing. It also has a slower, less efficient emergency biomass-recovery recipe so losing the last Living Forge does not permanently strand an established Wraith colony. A Wraith gravcore seed likewise accepts either a living biological prisoner/slave or a biological humanlike corpse and produces an Odyssey-compatible vanilla Gravcore after one day.

Wraith equipment includes a stun staff, capture stunner, line carbine, heavy bio-weapon, Hunter Coat, Queen Raiment, Warrior Carapace and Commander Carapace. Living colony technology includes Living Forge and Growth Chamber production organs, furniture, lighting, walls, membrane doors, bioelectric generation, living power storage and storage fixtures.

Wraith attack behavior is increasingly mission-specific rather than generic raiding. Roof-breached Wraith Dart passes perform actual culling/abduction runs, persistent abductees can feed later Wraith rescue content, and mature Hive/craft systems support a broader predatory-civilisation layer.

### Human-form nanites / precursor technology
Human-form synthetic beings use a Biotech xenotype with nanite reconstruction and EMP-vulnerability foundations. Their technology family includes precursor weapons, armor, fabrication, power systems, furniture/architecture and themed Odyssey gravship components.

The **Neural Interface** is faction-agnostic and can target any other living flesh-and-blood humanlike pawn whether friendly, neutral, prisoner, slave or hostile. Activating it automatically pauses the game and opens a dedicated centered popup with five operations:

- Recruit / rewrite allegiance
- Imprison
- Enslave when Ideology is active
- Copy skills and passions into the operator
- Build a fresh human-form Replicator copy

The popup owns the temporary pause; closing it releases that pause instead of leaving the game manually paused. The Neural Interface cooldown is exactly **2 RimWorld hours** (5,000 ticks).

Human-form copy creation generates a fresh pawn rather than cloning the original pawn object. It copies visible appearance, source xenotype/endogenes, custom xenotype presentation, childhood/adulthood backstory and exact skills/passions, then layers the WNG nanite gene package on top. The assembled copy begins without generated apparel.

Asuran precision-strike behavior can recognize WNG vacuum-energy/ZPM-equivalent objectives and assign a recovery operative to steal a module before extracting through an available Stargate.

### Small Replicators
Small Replicators are mechanoid-class mechanical pawns, distinct from human-form nanite people.

Hostile or feral small Replicators defend themselves, then seek reachable matter. They can consume loose items and resources, artificial structures, ruins/wreckage and natural mineable resource deposits. They ignore player Forbidden designations because those are colony work controls, not protections against an enemy swarm. Ordinary mountain mass is excluded.

A completed assimilation destroys its target exactly once, keeps the parent alive and produces two new Replicators, limited by the configurable per-map emergency cap. Player-controlled Replicators do not autonomously eat the colony.

Offspring inherit the matter used to produce them. The consumed source material supplies their visible tint and a persistent material signature. Stuff durability/flammability factors determine a fragile, standard, hardened or ultra-dense stat profile, allowing modded StuffDefs to participate without a vanilla-only hard-coded list.

The machine ecology also includes learned technology adaptation, hierarchy/recombination, a Controller side-form, a late Siege Mass, powered containment, EMP suppression, changing matter priorities and dangerous Replicator Matter salvage.

Replicator Matter becomes self-assembly-capable after **30,000 ticks / half a RimWorld day** when not contained. A storyteller meteor-shower incident can seed 3–4 separated clusters of dormant Replicator Matter before a live outbreak, giving the player a salvage/custody problem rather than immediately spawning attackers.

Replicators can also be created as the deliberately innocuous **Child's Toy** colony mech. A controlled toy remains friendly; prolonged loss of active player control lets its feral replication routines reassert themselves. The hidden **Replication Swarm** faction can field both block-form and human-form Replicators.

### Ancient / precursor technology
Ancient-oriented content includes recovered control-chair/drone systems, high-energy shields, vacuum-energy modules/taps, precursor vaults and other rare technology intended to feel like archaeological access to an extraordinary lost technological layer rather than merely ordinary industrial research with larger numbers.

### Shuttlecraft, gravships and optional Stargates
The **Wraith Dart** and **Puddle Jumper** are independent shuttlecraft, not modular gravship structures. They use Odyssey passenger-shuttle machinery for loading, launching and world travel while preserving dedicated WNG identities and art through the full travel cycle.

Full-sized Odyssey gravships receive themed technology families:

- Wraith: regenerative organic hull tissue, neural pilot node, living gravitic drive and living power technology
- Precursor: high-strength composite hull, advanced pilot console, vector drive, gravitic power core and stronger shield emitter

When the external Stargates mod is present, WNG uses a dependency-light optional integration for faction assaults, iris-aware arrival, bounded redials and WNG-owned support-craft ingress. WNG does not manipulate another mod's receive buffer or require Stargates to load. Reflective state reads tolerate compatible field/property API refactors without creating a compile dependency.

## Architect organization
Buildable content is grouped under the dedicated **Wraith & Nanite** Architect category. CI generates and validates distinct command icons, dedicated shuttle sprites, weapons, directional armor bodytype graphics, Replicator facings and production structures.

## Four scenarios

- Wraith: Landfall
- Wraith: In Orbit
- Human-form Replicators: Landfall
- Human-form Replicators: In Orbit

The orbital starts use Odyssey native orbit/gravship systems.

## Settings
Current settings expose Wraith regeneration, human-form nanite regeneration, maximum hostile block Replicators per map, storyteller Replicator outbreaks, Replicator feral delay and Stargate-incursion frequency where relevant. The core rule of two offspring per successful hostile assimilation is intentionally fixed.

## Audio identity

The current runtime suite contains **30 original procedural cues**. Wraith heavy bio-weapons, Replicator integrated pulse weapons and Ancient drone launches have separate faction-specific sounds rather than borrowing vanilla or another WNG technology family's cue. The full manifest and remaining live-audition requirements are documented in `Docs/SOUND_ASSET_MANIFEST.md` and `Docs/SOUND_DESIGN.md`.

## Automated validation

The development branch has a strict build pipeline. CI regenerates runtime art and the 30-cue audio suite, validates XML/custom references, checks RimWorld 1.6 gameplay contracts, compiles `WraithNaniteGravtech.dll`, verifies language/assets/payload contents and uploads a playable mod artifact.

The local/manual `Tools/BuildPlayable.ps1` path is kept in parity with CI. A dedicated parity audit rejects drift in critical generators, audits, audio ordering, language packaging and signature assets. The optional Stargate bridge also has a resilience audit that preserves dependency-free field/property reflection without receive-buffer ownership.

Runtime testing remains defined in `Docs/TESTING.md`. Current cross-session state and the continuing development sequence are maintained in `Docs/CURRENT_CHECKPOINT.md`. Original-asset/provenance guidance is maintained in `Docs/ASSET_PROVENANCE.md` and `Docs/LEGAL_AND_NAMING.md`.

## Stability principles

- No Humanoid Alien Races dependency
- Prefer genes, Hediffs, abilities, comps, custom jobs and Odyssey-native systems
- Avoid broad global Harmony patches
- No per-pawn full-map scan every tick
- Interrupted incubation/assimilation terminates safely
- Player-controlled small Replicators do not auto-assimilate
- Copy pawns are freshly generated; no deep-object cloning
- Persistent custom state is save/load serialized
- Compatibility and error containment take priority over cleverness
- Exact-head green is required for code/config changes, but live RimWorld evidence outranks static confidence

## Current next phase

The fresh five-pass repository/runtime/gameplay/presentation/release audit is complete at static/compile/package level. The next genuine proof point is the broad **live RimWorld test matrix**, followed by demonstrated-fault repair and evidence-led balance/audio/VFX/visual polish.