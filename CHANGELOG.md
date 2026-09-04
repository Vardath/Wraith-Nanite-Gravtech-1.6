# Changelog

## Unreleased — final 1.6 release polish

- Re-staged WNG's major archaeology/discovery sequence to reduce early clustering: ruined Wraith laboratory day 20, Replicator-consumed ruin day 28, Ancient laboratory day 36, Wraith cloning installation day 44, Ancient vault day 52, dormant Asuran facility day 60, deceptive Ancient survey annex day 72, and Replicator Queen vault day 84. Existing base incident chances remain unchanged.
- Added `Tools/discovery_pacing_audit.py` to lock the global progression **mystery → encounter → evidence → understanding → reconstruction → mastery**, live-tested `Misc` incident category, spacing, base chances and key player-facing site/counterplay text without spoiling the hidden Asuran reveal.
- Rewrote archaeology discovery letters where needed so they distinguish early evidence, hazardous Replicator Matter, damaged reconstruction material, deeper Wraith biology, dormant Asuran security and warned late-stage threats instead of presenting unrelated advanced systems as equivalent discoveries.
- Expanded Vital Resistance item/Health descriptions to state the approximate five-day duration, strong-but-not-total feeding reduction, immediate treatment strain, victim-side rejection crisis and Wraith-side backlash.
- Reviewed the requested high-complexity communication surfaces and retained the existing telemetry where it was already strong: Replicator adaptation/counterplay letters, Wraith doctrine descriptions, recovered precursor alignment/fault inspection, Asuran Pattern Archive scan/knowledge inspection, and exact Stargate corridor objective/denial alerts.
- Refreshed `Docs/WORKSHOP_DESCRIPTION.md`, `Docs/RELEASE_CHECKLIST.md`, `Docs/CURRENT_CHECKPOINT.md`, `Docs/RELEASE_PLAN_20260905.md` and added `Docs/RELEASE_NOTES_1.6_NEXT.md` for the next public 1.6 release.
- The release remains gated on another exact release-candidate audit plus fresh live Player.log/RimDoctor evidence; automated semantic/runtime-contract audit, compile and packaging results are not described as a live RimWorld test.

## Unreleased — active development

- Completed the fresh five-pass whole-mod audit at repository/static/compile/package level: repository/Defs/CI, save-load/runtime/performance, gameplay/quests/factions/optional integrations, art/audio/UI/presentation, and release-readiness/cross-system regression were re-read and reconciled. Live RimWorld testing remains the final proof layer rather than being falsely treated as established by CI.
- Split Wraith telepathy presentation into dedicated icons for the telepathy gene, Telepathic Probe and Captive Experiment; the runtime asset gate now rejects a future three-way gene/ability icon reuse regression.
- Workshop and Wraith gravcore implantation support biological corpses as an alternative to living hosts while preserving the established living-host routes.
- Corpse implantation creates a save-safe, progress-visible incubation mass; after 60,000 ticks it produces a minified Living Forge or Odyssey Gravcore.
- Removed generic vanilla weapon-generation tags from WNG faction weapons so unrelated spacer PawnKinds/mods cannot select Wraith or precursor technology through broad weapon pools; CI now regression-locks that isolation.
- Marked the corpse-less Replicator Drone as meatless to match its mechanical race definition.
- Roof-breached Wraith Dart fly-over attack passes perform the intended culling-beam absorption instead of being visual-only: each pass can remove up to two nearby live humanlike colonists/prisoners, with a hard shared total of three captives across fly-over and landed culling.
- Fly-over abductees are the exact pawn instances transferred directly into the persistent Wraith abduction/rescue registry; the final Dart still materialises only once, so the visual proxy cannot duplicate pawns, fuel, shuttle state or salvage.
- Stargate Dart and Puddle Jumper flyovers use dedicated WNG drive cues instead of the generic vanilla shuttle-leaving sound.
- Optional Stargate active/hibernation/iris state reflection now tolerates compatible bool fields or properties, including non-public instance members, while remaining hard-dependency-free and never taking ownership of another mod's receive buffer.
- Asuran precision strikes can recognize WNG vacuum-energy/ZPM-equivalent objectives, assign one recovery operative, steal one portable module or recover stored charge from an eligible tap, and switch the strike toward Stargate extraction. The ZPM objective audit is now part of the main CI path.
- Added `WNG_ReplicatorMatterMeteorShower`: 3–4 separated open-sky meteorites deliver 12–18 Replicator Matter each as hazardous salvage. The incident never creates live Replicators directly, will not stack onto an active block swarm, and is suppressed when too much unresolved Replicator Matter is already present.
- Restored the accepted Replicator Matter hazard tuning after detecting a regression: minimum dangerous stack 10, dormancy 30,000 ticks / half a RimWorld day, base rare-tick wake chance 0.0007 with existing stack scaling, and consumption only after successful hostile Drone placement.
- Replicator target caching now avoids full-map Thing scans on maps without an autonomous hostile/feral harvesting Replicator; active swarms retain the established 300-tick responsiveness.
- Ancient control-chair/Jumper/shield network queries now use Def-indexed Thing lookups rather than repeatedly walking every Thing on the map.
- Added dedicated original sounds for the Wraith heavy bio-weapon, Replicator integrated pulse caster and Ancient drone launch. These actions no longer borrow vanilla Charge Lance, precursor rifle or generic precursor-console cues.
- Expanded the original WNG runtime audio suite to **30 cues** with format/headroom/RMS/duplicate/duration gates plus a faction-audio identity audit.
- Rebuilt the manual `Tools/BuildPlayable.ps1` path to use the same critical art/audio generators and gameplay audits as CI, including the 30-cue order and Asuran ZPM audit.
- CI and local playable packaging now both include and verify `Languages/English/Keyed/WNG_Keys.xml`; CI previously omitted the Languages folder from the assembled artifact.
- Added build-pipeline parity and optional-integration resilience audits so local/CI packaging or Stargate reflection assumptions cannot silently drift again.
- Updated the live test matrix for half-day Replicator Matter, meteor arrival/containment, Stargate iris and Dart culling, exact abductee persistence, Asuran ZPM recovery, gene-ability reconciliation, performance, signature audio and balance observations.
- Repository status remains active development/integration. The next genuine proof point is broad in-game runtime testing, followed by demonstrated-fault repair, evidence-led balance and deeper live audio/VFX/visual polish.

## Earlier public-release preparation work

### Wraith
- Added Wraith Biotech xenotype with Life Force, regeneration and long-term feeding pressure.
- Life Drain ages the victim by 50 biological years and reverses 5 biological years from the Wraith, with a minimum Wraith biological age of 18.
- Successful Life Drain restores Life Force and activates the fed-regeneration effect.
- Added partial Odyssey vacuum resistance; Wraiths endure exposure much longer than ordinary humans but are not vacuum-proof.
- Added Life Drain, Wither, Enthrall and Host Seed Gestation.
- Life Drain, Wither and Enthrall can target standing biological pawns; Host Seed remains downed/stunned-only for living targets and now also supports valid biological corpses.
- Added saveable host incubation producing Living Forge technology.
- Added planted Living Forge and Growth Chamber technological seeds with save-safe maturation.
- Added Living Forge gravcore-seed production and biological-host implantation. Gravcore incubation consumes the host and creates an Odyssey-compatible vanilla Gravcore.
- Living Forge and Growth Chamber explicitly use worktable/Bills behavior.
- Added distinct Wraith production, seed, structure, furniture, power and storage art.
- Added Stun Staff, Stunner, Carbine and Heavy Bio-Weapon.
- Added Warrior and Commander carapace armor with dedicated worn sprites for all standard body types.
- Added living walls, membrane door, furniture, lighting, bioelectric generation and living power storage.
- Added enhanced Wraith Odyssey gravship hull tissue, neural pilot node and living gravitic drive family.
- Added the Wraith Dart as an independent Odyssey-native passenger shuttle with dedicated four-direction art.
- Added Wraith surface and orbital starting scenarios.
- Added Wraith appearance enforcement favoring long straight/sleek near-white hair.

### Human-form nanites / precursor technology
- Added human-form nanite Biotech xenotype with reconstruction and EMP-vulnerability foundations.
- Human-form Replicator PawnKinds use a dedicated nanite xenotype and vanilla Deathless behavior while preserving catastrophic brain destruction as a meaningful failure mode.
- Human-form Replicators have partial Odyssey vacuum resistance rather than total immunity.
- Added precursor pulse rifle, field armor, fabrication bench, structures, furniture, lighting and power systems.
- Added enhanced precursor Odyssey gravship hull, pilot console, vector drive, gravitic power and shield emitter.
- Added the Puddle Jumper as an independent Odyssey-native passenger shuttle with dedicated four-direction art.
- Corrected research/labels that previously confused shuttlecraft with modular gravships.
- Neural Interface targeting is faction-agnostic for other living flesh-and-blood humanlikes.
- Neural Interface now automatically pauses the game and opens a dedicated popup window rather than a cursor FloatMenu.
- Neural Interface popup provides Recruit/rewrite allegiance, Imprison, Enslave, Copy skills and passions, and Build human-form copy.
- Neural Interface cooldown is exactly 5,000 ticks, or 2 RimWorld hours.
- Human-form copy generation now copies visible appearance, source xenotype/endogenes, custom xenotype presentation, childhood/adulthood backstory and exact skills/passions, then adds the WNG nanite package.
- Human-form copies are freshly generated pawns and spawn without generated apparel, avoiding duplicate load IDs and deep-object cloning.
- Added a gene-ability reconciler that periodically restores missing WNG gene-granted abilities without deleting unrelated abilities.
- Added human-form surface and orbital starting scenarios.

### Small Replicators
- Added light mechanoid-class Replicator pawn and PawnKind.
- Added Child's Toy light-mech gestator recipe; completed pawn is visibly named Replicator.
- Player-built Replicators are friendly colony mechs while actively overseen.
- Added a small situational mood thought for colonists near a controlled Child's Toy Replicator.
- Added configurable feral grace period; loss of active player control eventually transfers the unit to the hidden Replication Swarm faction.
- Added Replicator Matter and core-fragment resources.
- Replicator Matter is dangerous salvage: exposed stacks of at least 10 remain dormant for half a RimWorld day and then have a stack-size-scaled chance to consume 10 matter and self-assemble into a hostile Replicator, subject to the configured hostile whole-block map cap.
- Added electric-smelter reprocessing: 10 Replicator Matter can be disrupted into 6 steel, while a core fragment can be deliberately destroyed for 8 steel.
- Replicator Matter exposes its dormant/unstable state in the inspect pane so players receive warning before stockpiles become hazardous.
- Expanded autonomous assimilation to loose items/resources, artificial buildings, ruins/wreckage and natural mineable resource deposits while excluding ordinary mountain mass.
- Hostile/feral Replicators ignore player Forbidden designations and use a bounded target cache.
- Reworked swarm AI into a passive consumption-first threat. Directly attacked units can defend themselves, and a throttled minority of nearby Replicators may rally while most continue assimilation.
- Successful assimilation destroys the target once and creates exactly two offspring while the parent survives, subject to the configured map cap.
- Added persistent material inheritance: offspring tint and fragile/standard/hardened/ultra-dense profiles derive from the consumed StuffDef.
- Added hidden Replication Swarm faction with block-form and human-form Replicator groups and a storyteller outbreak seed event.
- Added articulated six-legged four-facing Replicator pawn art.

### UI, art, audio and validation
- Added dedicated Wraith & Nanite Architect category.
- Added generated per-ThingDef Architect command icons instead of a repeated generic icon.
- Reworked physical art around distinct Wraith-organic and precursor-geometric visual languages.
- Added dedicated UI concept art for major genes, xenotypes and abilities, including Neural Interface and Life Drain.
- Added dedicated weapon silhouettes and true role-specific worn armor sprites.
- Added original directional Wraith Dart and Puddle Jumper shuttle sprites.
- CI regenerates art/audio/build icons, audits XML and runtime contracts, checks texture/audio resolution and duplicate placeholder art, compiles the DLL, verifies the payload and packages a playable folder.
- Added regression guards for Bills tabs, targeting contracts, shuttle-vs-gravship separation, planted seed growth, Replicator assimilation/material inheritance, passive swarm response, unique build icons, the paused Neural Interface popup, Dart culling quotas, hazardous Matter meteors and current audio quality/identity.

### Release preparation already performed but not final sign-off
- About metadata identifies the project as an unofficial, non-commercial fan-made mod and includes dependency/disclaimer information.
- Prepared Steam Workshop copy, release checklist and asset-provenance documentation exist, but should be treated as future release material rather than evidence that development is complete.
- Added a project rights/third-party notice without claiming ownership of RimWorld or Stargate properties.

### Major runtime work still required
- Core + DLC + WNG clean-load smoke test and large-mod-list load test.
- Living Forge, Growth Chamber and precursor Fabricator Bills/recipe verification.
- Living and corpse technological implantation plus save/reload verification.
- Wraith Dart and Puddle Jumper full load/launch/world/return/relaunch tests using Odyssey transport mechanics.
- Stargate iris/roof-breach/Dart-culling and exact abductee-rescue testing.
- Asuran ZPM/vacuum-energy recovery objective testing.
- Neural Interface popup and all five operations, including automatic pause and exact cooldown behavior.
- Human-form copy appearance/genome/backstory/skill verification.
- Human-form Replicator Deathless/vacuum/EMP behavior verification.
- Wraith Life Force progression, feeding balance, appearance enforcement and partial vacuum resistance verification.
- Replicator Matter meteor, storage, half-day wake-up, containment and reassembly testing.
- Replicator material consumption, inheritance, hierarchy/adaptation, passive-threat behavior, feral transition and population-cap stress testing.
- Wraith/Asuran gravship testing, faction/raid balance, signature-audio/live-VFX audition, save/load torture, long-session test and fresh Player.log/RimDoctor audit.